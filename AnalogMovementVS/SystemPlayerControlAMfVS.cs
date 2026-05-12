using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace AnalogMovementVS
{
    internal class SystemPlayerControlAMfVS
    {
        [HarmonyPatch(typeof(SystemPlayerControl), nameof(SystemPlayerControl.OnGameTick))]
        class SystemPlayerControlPatch
        {
            static bool Prefix(float dt, SystemPlayerControl __instance)
            {
                int forwardKey = Traverse.Create(__instance).Field("forwardKey").GetValue<int>();
                int backwardKey = Traverse.Create(__instance).Field("backwardKey").GetValue<int>();
                int leftKey = Traverse.Create(__instance).Field("leftKey").GetValue<int>();
                int rightKey = Traverse.Create(__instance).Field("rightKey").GetValue<int>();
                int jumpKey = Traverse.Create(__instance).Field("jumpKey").GetValue<int>();
                int sneakKey = Traverse.Create(__instance).Field("sneakKey").GetValue<int>();
                int sprintKey = Traverse.Create(__instance).Field("sprintKey").GetValue<int>();
                int ctrlKey = Traverse.Create(__instance).Field("ctrlKey").GetValue<int>();
                int shiftKey = Traverse.Create(__instance).Field("shiftKey").GetValue<int>();

                var game = Traverse.Create(__instance).Field("game").GetValue<ClientMain>();
                var inputapi = Traverse.Create(game.api).Field("inputapi").GetValue<InputAPI>();
                var OpenedGuis = Traverse.Create(game).Field("OpenedGuis").GetValue<List<GuiDialog>>();
                var player = Traverse.Create(game).Field("player").GetValue<ClientPlayer>();
                var worlddata = Traverse.Create(player).Field("worlddata").GetValue<ClientWorldPlayerData>();
                var prevControls = Traverse.Create(__instance).Field("prevControls").GetValue<EntityControls>();

                var entityPlayer = game.EntityPlayer;
                var entityControls = (entityPlayer.MountedOn == null) ? entityPlayer.Controls : entityPlayer.MountedOn.Controls;
                EntityControlsAMfVS? PlayerControls = null;
                if (entityPlayer.Controls is EntityControlsAMfVS amcon) PlayerControls = amcon;
                if (entityControls == null || PlayerControls == null) return false;

                if (inputapi is not null)
                {
                    MethodInfo? triggerMethod = inputapi.GetType().GetMethod("TriggerInWorldAction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (triggerMethod is not null)
                    {
                        var onActionDelegate = (OnEntityAction)Delegate.CreateDelegate(typeof(OnEntityAction), inputapi, triggerMethod);
                        game.EntityPlayer.Controls.OnAction = onActionDelegate;
                    }
                }

                bool flag;
                if (!game.MouseGrabbed)
                {
                    if (game.api.Settings.Bool["immersiveMouseMode"])
                    {
                        flag = OpenedGuis.All((GuiDialog gui) => !gui.PrefersUngrabbedMouse);
                    }
                    else
                    {
                        flag = false;
                    }
                }
                else
                {
                    flag = true;
                }
                bool flag2 = flag;
                bool IsPauseMenuOpen = OpenedGuis.Any(gui => gui.GetType().Name == "GuiDialogEscapeMenu");

                //controls for both walking and mounted
                if (entityControls is EntityControlsAMfVS)
                {
                    PlayerControls.IsMouseGrabbed = flag2;
                    PlayerControls.IsPauseMenuOpen = IsPauseMenuOpen;

                    //optionally reenable the keyboard controls that were removed
                    if (PlayerControls.EnableKeyboardBoolMovement)
                    {
                        PlayerControls.amForwardBackward2 = (game.KeyboardState[forwardKey] ? 1 : 0) + (game.KeyboardState[backwardKey] ? -1 : 0);
                        PlayerControls.amLeftRight2 = (game.KeyboardState[leftKey] ? 1 : 0) + (game.KeyboardState[rightKey] ? -1 : 0);
                    }
                    if (PlayerControls.EnableKeyboardJumpSneakSprint)
                    {
                        PlayerControls.amJump2 = game.KeyboardState[jumpKey] && flag2 && (game.EntityPlayer.PrevFrameCanStandUp || worlddata.NoClip);
                        PlayerControls.amSneak2 = game.KeyboardState[sneakKey] && flag2;
                        PlayerControls.amSprint2 = (game.KeyboardState[sprintKey] || (PlayerControls.Sprint && entityControls.TriesToMove && ClientSettings.ToggleSprint)) && flag2;
                    }

                    //disable jumpsneaksprint when tabbed out or 'paused' in multiplayer
                    if (ScreenManager.Platform.IsFocused || !IsPauseMenuOpen)
                    {
                        PlayerControls.Jump = PlayerControls.amJump || PlayerControls.amJump2;
                        PlayerControls.Sneak = PlayerControls.amSneak || PlayerControls.amSneak2;
                        PlayerControls.Sprint = PlayerControls.amSprint || PlayerControls.amSprint2;
                    }
                    else
                    {
                        PlayerControls.Jump = false;
                        PlayerControls.Sneak = false;
                        PlayerControls.Sprint = false;
                    }

                    //mouse click inputs
                    if (PlayerControls.LeftMouse && !PlayerControls.PrevLeftMouse)
                    {
                        PlayerControls.PrevLeftMouse = true;
                        game.UpdateMouseButtonState(EnumMouseButton.Left, true);
                    }
                    else if (!PlayerControls.LeftMouse && PlayerControls.PrevLeftMouse)
                    {
                        PlayerControls.PrevLeftMouse = false;
                        game.UpdateMouseButtonState(EnumMouseButton.Left, false);
                    }

                    if (PlayerControls.RightMouse && !PlayerControls.PrevRightMouse)
                    {
                        PlayerControls.PrevRightMouse = true;
                        game.UpdateMouseButtonState(EnumMouseButton.Right, true);
                    }
                    else if (!PlayerControls.RightMouse && PlayerControls.PrevRightMouse)
                    {
                        PlayerControls.PrevRightMouse = false;
                        game.UpdateMouseButtonState(EnumMouseButton.Right, false);
                    }


                    //items specific to mounted or walking
                    if (entityControls is EntityControlsMountAMfVS ammount) //mounted
                    {
                        PlayerControls.IsMounted = true;
                        entityControls.MovespeedMultiplier = worlddata.MoveSpeedMultiplier;

                        //jumpsneaksprint needs to be forwarded to the mount controls
                        ammount.Jump = PlayerControls.Jump;
                        ammount.Sneak = PlayerControls.Sneak;
                        ammount.Sprint = PlayerControls.Sprint;

                        //forward the rest of the controls in case another mod is using them
                        ammount.Forward = PlayerControls.Forward;
                        ammount.Backward = PlayerControls.Backward;
                        ammount.Left = PlayerControls.Left;
                        ammount.Right = PlayerControls.Right;
                    }
                    else //walking
                    {
                        PlayerControls.IsMounted = false;
                        PlayerControls.amIncomingMoveSpeed = worlddata.MoveSpeedMultiplier;

                        //floor sitting disabler
                        if (PlayerControls.WalkVector.X > 0 || PlayerControls.WalkVector.Y > 0 || PlayerControls.WalkVector.Z > 0 || PlayerControls.amJump || PlayerControls.amJump2)
                        {
                            Traverse.Create(__instance).Field("nowFloorSitting").SetValue(false);
                        }
                    }
                }
                else //use default keyboard controls for unsupported controllables
                {
                    entityControls.MovespeedMultiplier = worlddata.MoveSpeedMultiplier;
                    entityControls.Forward = game.KeyboardState[forwardKey];
                    entityControls.Backward = game.KeyboardState[backwardKey];
                    entityControls.Left = game.KeyboardState[leftKey];
                    entityControls.Right = game.KeyboardState[rightKey];
                    entityControls.Jump = game.KeyboardState[jumpKey] && flag2 && (game.EntityPlayer.PrevFrameCanStandUp || worlddata.NoClip);
                    entityControls.Sneak = game.KeyboardState[sneakKey] && flag2;
                    bool sprint = entityControls.Sprint;
                    entityControls.Sprint = (game.KeyboardState[sprintKey] || (sprint && entityControls.TriesToMove && ClientSettings.ToggleSprint)) && flag2;
                }


                //unmodified controls
                entityControls.CtrlKey = game.KeyboardState[ctrlKey]; //might need to do ctrl shift
                entityControls.ShiftKey = game.KeyboardState[shiftKey];
                entityControls.DetachedMode = worlddata.FreeMove || game.EntityPlayer.IsEyesSubmerged();
                entityControls.FlyPlaneLock = worlddata.FreeMovePlaneLock;
                entityControls.Up = entityControls.DetachedMode && entityControls.Jump;
                entityControls.Down = entityControls.DetachedMode && entityControls.Sneak;
                entityControls.IsFlying = worlddata.FreeMove;
                entityControls.NoClip = worlddata.NoClip;
                entityControls.LeftMouseDown = game.InWorldMouseState.Left;
                entityControls.RightMouseDown = game.InWorldMouseState.Right;
                var nowFloorSitting = Traverse.Create(__instance).Field("nowFloorSitting").GetValue<bool>();
                entityControls.FloorSitting = nowFloorSitting;
                Traverse.Create(__instance).Method("SendServerPackets", prevControls, entityControls).GetValue();

                return false;
            }
        }
    }
}
