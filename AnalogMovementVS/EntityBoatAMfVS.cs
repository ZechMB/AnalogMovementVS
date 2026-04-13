using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace AnalogMovementVS
{    
    [HarmonyPatch(typeof(EntityBoat), nameof(EntityBoat.SeatsToMotion))]
    public class EntityBoatPatch
    {
        public static bool Prefix(EntityBoat __instance, float dt, ref Vec2d __result)
        {
            var CurrentlyControllingEntityId = Traverse.Create(__instance).Field("CurrentlyControllingEntityId").GetValue<long>();
            var capi = Traverse.Create(__instance).Field("capi").GetValue<ICoreClientAPI>();
            var unfurlSails = Traverse.Create(__instance).Field("unfurlSails").GetValue<bool>();
            int sailPosition = __instance.WatchedAttributes.GetInt("sailPosition", 0);
            var requiresPaddlingTool = Traverse.Create(__instance).Field("requiresPaddlingTool").GetValue<bool>();
            var Pos = __instance.Pos;

            int seatsRowing = 0;
            double linearMotion = 0;
            double angularMotion = 0;
                        
            var bh = __instance.GetBehavior<EntityBehaviorSeatable>();
            if (bh is null) return false;

            bh.Controller = null;

            foreach (var sseat in bh.Seats)
            {
                if (sseat is not EntityBoatSeat seat) continue;
                if (seat.Passenger == null) continue;

                if (seat.Passenger is not EntityPlayer)
                {
                    seat.Passenger.Pos.Yaw = Pos.Yaw;
                }
                if (seat.Config.BodyYawLimit != null && seat.Passenger is EntityPlayer eplr)
                {
                    if (eplr.BodyYawLimits == null)
                    {
                        eplr.BodyYawLimits = new AngleConstraint(Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, (float)seat.Config.BodyYawLimit);
                        eplr.HeadYawLimits = new AngleConstraint(Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, GameMath.PIHALF);
                    }
                    else
                    {
                        eplr.BodyYawLimits.X = Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD;
                        eplr.BodyYawLimits.Y = (float)seat.Config.BodyYawLimit;
                        eplr.HeadYawLimits.X = Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD;
                        eplr.HeadYawLimits.Y = GameMath.PIHALF;
                    }
                }
                
                if (!seat.Config.Controllable || bh.Controller != null)
                {
                    continue;
                }

                //use player controls for movement but seat.controls still exist to prevent a bug of the boat pushing you when you get out
                EntityControlsAMfVS? controls = null;                              
                if (seat.Passenger is EntityPlayer eplr2)
                {
                    if (eplr2.Controls is EntityControlsAMfVS controls2)
                    {
                        controls = controls2;
                    }
                }
                if (controls is null)
                {
                    continue;
                }

                
                if (seat.Passenger.EntityId != CurrentlyControllingEntityId) continue;
                
                bh.Controller = seat.Passenger;
                
                bool HasPaddle = Traverse.Create(__instance).Method("HasPaddle", seat.Passenger).GetValue<bool>();
                if (!HasPaddle)
                {
                    seat.Passenger.AnimManager?.StopAnimation(__instance.MountAnimations["ready"]);
                    seat.actionAnim = null;
                    continue;
                }

                if (controls.Left == controls.Right && capi == null)
                {
                    __instance.StopAnimation("turnLeft");
                    __instance.StopAnimation("turnRight");
                }
                if (controls.Left && !controls.Right)
                {
                    __instance.StartAnimation("turnLeft");
                    __instance.StopAnimation("turnRight");
                }
                if (controls.Right && !controls.Left)
                {
                    __instance.StopAnimation("turnLeft");
                    __instance.StartAnimation("turnRight");
                }
        
                float str = ++seatsRowing == 1 ? 1 : 0.5f;

                if (unfurlSails && sailPosition > 0)
                {
                    linearMotion += str * dt * sailPosition * 1.5f;
                }
                
                if (!controls.TriesToMove)
                {
                    seat.actionAnim = null;
                    if (seat.Passenger.AnimManager != null && !seat.Passenger.AnimManager.IsAnimationActive(__instance.MountAnimations["ready"]))
                    {
                        seat.Passenger.AnimManager.StartAnimation(__instance.MountAnimations["ready"]);
                    }
                }
                else
                {
                    if (controls.Right && !controls.Backward && !controls.Forward)
                    {
                        seat.actionAnim = __instance.MountAnimations["backwards"];
                    }
                    else
                    {
                        seat.actionAnim = __instance.MountAnimations[controls.Backward ? "backwards" : "forwards"];
                    }

                    seat.Passenger.AnimManager?.StopAnimation(__instance.MountAnimations["ready"]);
                }
                
                if (controls.amLeftRight != 0 || controls.amLeftRight2 != 0)
                {
                    float dir = controls.amLeftRight + controls.amLeftRight2;
                    angularMotion += str * dir * dt;
                }

                if (controls.amForwardBackward != 0 || controls.amForwardBackward2 != 0)
                {
                    float dir = controls.amForwardBackward + controls.amForwardBackward2;

                    var yawdist = Math.Abs(GameMath.AngleRadDistance(Pos.Yaw, seat.Passenger.Pos.Yaw));
                    bool isLookingBackwards = yawdist > GameMath.PIHALF;
                    if (isLookingBackwards && requiresPaddlingTool) dir *= -1;

                    float ctrlstr = 2f;
                    if (unfurlSails) ctrlstr = sailPosition == 0 ? 0.4f : 0f;

                    linearMotion += str * dir * dt * ctrlstr;
                }
            }
            __result = new Vec2d(linearMotion, angularMotion);
            return false;
        }
    }
}