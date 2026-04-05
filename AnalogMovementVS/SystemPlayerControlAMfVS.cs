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
                var inputapi = Traverse.Create(game).Field("inputapi").GetValue<InputAPI>();
                var OpenedGuis = Traverse.Create(game).Field("OpenedGuis").GetValue<List<GuiDialog>>();
                var player = Traverse.Create(game).Field("player").GetValue<ClientPlayer>();
                var worlddata = Traverse.Create(player).Field("worlddata").GetValue<ClientWorldPlayerData>();
                var nowFloorSitting = Traverse.Create(__instance).Field("nowFloorSitting").GetValue<bool>();
                var prevControls = Traverse.Create(__instance).Field("prevControls").GetValue<EntityControls>();

                var entityPlayer = game.EntityPlayer;
                var entityControls = (entityPlayer.MountedOn == null) ? entityPlayer.Controls : entityPlayer.MountedOn.Controls;
                if (entityControls == null)
                {
                    return false;
                }

                //game.EntityPlayer.Controls.OnAction = new OnEntityAction(inputapi.TriggerInWorldAction);
                //i wouldn't be surprised if i broke whatever this does
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

                if (entityControls is EntityControlsAMfVS amcontrols)
                {
                    amcontrols.IsMouseGrabbed = flag2;

                    //reenable the controls that were removed and optionally disable them
                    if (amcontrols.EnableKeyboardBoolMovement)
                    {
                        amcontrols.amForwardBackward2 = (game.KeyboardState[forwardKey] ? 1 : 0) + (game.KeyboardState[backwardKey] ? -1 : 0);
                        amcontrols.amLeftRight2 = (game.KeyboardState[leftKey] ? 1 : 0) + (game.KeyboardState[rightKey] ? -1 : 0);
                    }
                    if (amcontrols.EnableKeyboardJumpSneakSprint)
                    {
                        amcontrols.amJump2 = game.KeyboardState[jumpKey] && flag2 && (game.EntityPlayer.PrevFrameCanStandUp || worlddata.NoClip);
                        amcontrols.amSneak2 = game.KeyboardState[sneakKey] && flag2;
                        amcontrols.amSprint2 = (game.KeyboardState[sprintKey] || (amcontrols.Sprint && entityControls.TriesToMove && ClientSettings.ToggleSprint)) && flag2;
                    }

                    //disable jumpsneaksprint when tabbed out
                    if (ScreenManager.Platform.IsFocused)
                    {
                        amcontrols.Jump = amcontrols.amJump || amcontrols.amJump2;
                        amcontrols.Sneak = amcontrols.amSneak || amcontrols.amSneak2;
                        amcontrols.Sprint = amcontrols.amSprint || amcontrols.amSprint2;
                    }
                    else
                    {
                        amcontrols.Jump = false;
                        amcontrols.Sneak = false;
                        amcontrols.Sprint = false;
                    }
                }
                else
                {
                    //use default controls for mounts
                    entityControls.Forward = game.KeyboardState[forwardKey];
                    entityControls.Backward = game.KeyboardState[backwardKey];
                    entityControls.Left = game.KeyboardState[leftKey];
                    entityControls.Right = game.KeyboardState[rightKey];
                    entityControls.Jump = game.KeyboardState[jumpKey] && flag2 && (game.EntityPlayer.PrevFrameCanStandUp || worlddata.NoClip);
                    entityControls.Sneak = game.KeyboardState[sneakKey] && flag2;
                    bool sprint = entityControls.Sprint;
                    entityControls.Sprint = (game.KeyboardState[sprintKey] || (sprint && entityControls.TriesToMove && ClientSettings.ToggleSprint)) && flag2;
                }

                entityControls.CtrlKey = game.KeyboardState[ctrlKey];
                entityControls.ShiftKey = game.KeyboardState[shiftKey];
                entityControls.DetachedMode = worlddata.FreeMove || game.EntityPlayer.IsEyesSubmerged();
                entityControls.FlyPlaneLock = worlddata.FreeMovePlaneLock;
                entityControls.Up = entityControls.DetachedMode && entityControls.Jump;
                entityControls.Down = entityControls.DetachedMode && entityControls.Sneak;
                entityControls.MovespeedMultiplier = worlddata.MoveSpeedMultiplier;
                entityControls.IsFlying = worlddata.FreeMove;
                entityControls.NoClip = worlddata.NoClip;
                entityControls.LeftMouseDown = game.InWorldMouseState.Left;
                entityControls.RightMouseDown = game.InWorldMouseState.Right;
                entityControls.FloorSitting = nowFloorSitting;
                Traverse.Create(__instance).Method("SendServerPackets", prevControls, entityControls).GetValue();

                return false;
            }
        }
    }
}
