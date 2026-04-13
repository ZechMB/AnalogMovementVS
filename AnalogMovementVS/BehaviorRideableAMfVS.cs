using HarmonyLib;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace AnalogMovementVS
{
    [HarmonyPatch(typeof(EntityBehaviorRideable), nameof(EntityBehaviorRideable.SeatsToMotion))]
    class EntityBehaviorRideablePatch
    {
        //1.22 switches return value to double for just the turning
        public static bool Prefix(EntityBehaviorRideable __instance, ref Vec2d __result, float dt)
        {
            var coyoteTimer = Traverse.Create(__instance).Field("coyoteTimer").GetValue<float>();
            var capi = Traverse.Create(__instance).Field("capi").GetValue<ICoreClientAPI>();
            var lastJumpMs = Traverse.Create(__instance).Field("lastJumpMs").GetValue<long>();
            var scheme = Traverse.Create(__instance).Field("scheme").GetValue<EnumControlScheme>();
            var ebg = Traverse.Create(__instance).Field("ebg").GetValue<EntityBehaviorGait>();
            var api = Traverse.Create(__instance).Field("api").GetValue<ICoreAPI>();
            var angularMotionWild = Traverse.Create(__instance).Field("angularMotionWild").GetValue<float>();

            double linearMotion = 0;
            double angularMotion = 0;

            Traverse.Create(__instance).Field("jumpNow").SetValue(false);
            coyoteTimer -= dt;

            __instance.Controller = null;
            foreach (var seat in __instance.Seats)
            {
                if (seat.Config.Controllable && seat.Passenger != null)
                {
                    __instance.Controller = seat.Passenger;
                    break;
                }
            }

            foreach (var seat in __instance.Seats)
            {
                if (__instance.entity.OnGround) coyoteTimer = 0.15f;

                if (seat.Passenger == null) continue;

                if (seat.Passenger is EntityPlayer eplr)
                {
                    eplr.Controls.LeftMouseDown = seat.Controls.LeftMouseDown;
                    if (eplr.HeadYawLimits == null)
                    {
                        eplr.BodyYawLimits = new AngleConstraint(__instance.entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, seat.Config.BodyYawLimit ?? GameMath.PIHALF);
                        eplr.HeadYawLimits = new AngleConstraint(__instance.entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, GameMath.PIHALF);
                    }
                    else
                    {
                        eplr.BodyYawLimits.X = __instance.entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD;
                        eplr.BodyYawLimits.Y = seat.Config.BodyYawLimit ?? GameMath.PIHALF;
                        eplr.HeadYawLimits.X = __instance.entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD;
                        eplr.HeadYawLimits.Y = GameMath.PIHALF;
                    }

                }

                if (__instance.Controller != seat.Passenger) continue;

                var controls = seat.Controls;
                EntityControlsAMfVS? amcontrols = null;
                if (seat.Passenger is EntityPlayer eplr2)
                {
                    if (eplr2.Controls is EntityControlsAMfVS amcon)
                    {
                        amcontrols = amcon;
                    }
                }
                if (amcontrols is null) continue;

                bool canride = true;
                bool canturn = true;

                if (__instance.RemainingSaddleBreaks > 0)
                {
                    if (__instance.entity.Api.World.Rand.NextDouble() < 0.05) angularMotionWild = ((float)__instance.entity.Api.World.Rand.NextDouble() * 2 - 1) / 10f;
                    angularMotion = angularMotionWild;
                    canturn = false;
                }

                var canRideField = AccessTools.Field(typeof(EntityBehaviorRideable), "CanRide");
                var canRideDelegate = canRideField.GetValue(__instance) as MulticastDelegate;
                if (canRideDelegate != null && (controls.Jump || controls.TriesToMove))
                {
                    foreach (CanRideDelegate dele in canRideDelegate.GetInvocationList().Cast<CanRideDelegate>())
                    {
                        if (!dele(seat, out string? errMsg))
                        {
                            if (capi != null && seat.Passenger == capi.World.Player.Entity)
                            {
                                capi.TriggerIngameError(__instance, "cantride", Lang.Get("cantride-" + errMsg));
                            }
                            canride = false;
                            break;
                        }
                    }
                }

                var canTurnField = AccessTools.Field(typeof(EntityBehaviorRideable), "CanTurn");
                var canTurnDelegate = canTurnField.GetValue(__instance) as MulticastDelegate;
                if (canTurnDelegate != null && (controls.Left || controls.Right))
                {
                    foreach (CanRideDelegate dele in canTurnDelegate.GetInvocationList().Cast<CanRideDelegate>())
                    {
                        if (!dele(seat, out string? errMsg))
                        {
                            if (capi != null && seat.Passenger == capi.World.Player.Entity)
                            {
                                capi.TriggerIngameError(__instance, "cantride", Lang.Get("cantride-" + errMsg));
                            }
                            canturn = false;
                            break;
                        }
                    }
                }
                
                if (!canride) continue;
 
                if (amcontrols.Jump && __instance.entity.World.ElapsedMilliseconds - lastJumpMs > 1500L && __instance.entity.Alive && (__instance.entity.OnGround || coyoteTimer > 0f || (api.Side == EnumAppSide.Client && __instance.entity.EntityId != __instance.Controller.EntityId)))
                {
                    lastJumpMs = __instance.entity.World.ElapsedMilliseconds;
                    Traverse.Create(__instance).Field("jumpNow").SetValue(true);
                }

                var CurrentGait = Traverse.Create(__instance).Field("CurrentGait").GetValue<GaitMeta>();
                if (scheme != EnumControlScheme.Hold || amcontrols.TriesToMove || CurrentGait != ebg.IdleGait)
                {
                    if (api.Side != EnumAppSide.Server)
                    {
                        var prevForwardKey = Traverse.Create(__instance).Field("prevForwardKey").GetValue<bool>();
                        var prevBackwardKey = Traverse.Create(__instance).Field("prevBackwardKey").GetValue<bool>();
                        var prevSprintKey = Traverse.Create(__instance).Field("prevSprintKey").GetValue<bool>();
                        var prevPrevForwardsKey = Traverse.Create(__instance).Field("prevPrevForwardsKey").GetValue<bool>();
                        var prevPrevBackwardsKey = Traverse.Create(__instance).Field("prevPrevBackwardsKey").GetValue<bool>();
                        //var prevPrevSprintKey = Traverse.Create(__instance).Field("prevPrevSprintKey").GetValue<bool>(); //for 1.22
                        long lastGaitChangeMs = 0;
                        var onlyTwoGaits = Traverse.Create(__instance).Field("onlyTwoGaits").GetValue<bool>();
                        

                        bool forward = amcontrols.Forward;
                        bool backward = amcontrols.Backward;
                        bool sprint = controls.Sprint;
                        bool ShouldSprint = sprint && !prevSprintKey;
                        Traverse.Create(__instance).Field("prevSprintKey").SetValue(sprint);
                        bool ShouldStopSprint = backward && !prevBackwardKey && !prevPrevBackwardsKey;
                        long elapsedMilliseconds = __instance.entity.World.ElapsedMilliseconds;

                        if (scheme == EnumControlScheme.Press && !onlyTwoGaits)
                        {
                            Traverse.Create(__instance).Field("prevSprintKey").SetValue(false);
                        }
                        
                        if (forward && !prevForwardKey)
                        {
                            if (ebg.IsIdleGait(CurrentGait) && !prevPrevForwardsKey)
                            {
                                __instance.SpeedUp();
                                Traverse.Create(__instance).Field("lastGaitChangeMs").SetValue(elapsedMilliseconds);
                            }
                            else if (ebg.IsBackwards(CurrentGait))
                            {
                                Traverse.Create(__instance).Field("CurrentGait").SetValue(ebg.IdleGait);
                                Traverse.Create(__instance).Field("lastGaitChangeMs").SetValue(elapsedMilliseconds);
                            }
                        }
                        CurrentGait = Traverse.Create(__instance).Field("CurrentGait").GetValue<GaitMeta>();
                        if (scheme == EnumControlScheme.Hold && ((!forward && ebg.IsForwards(CurrentGait)) || (!backward && ebg.IsBackwards(CurrentGait))))
                        {
                            Traverse.Create(__instance).Field("CurrentGait").SetValue(ebg.IdleGait);
                            Traverse.Create(__instance).Field("lastGaitChangeMs").SetValue(elapsedMilliseconds);
                        }
                        lastGaitChangeMs = Traverse.Create(__instance).Field("lastGaitChangeMs").GetValue<long>();
                        CurrentGait = Traverse.Create(__instance).Field("CurrentGait").GetValue<GaitMeta>();
                        if (ShouldSprint && ebg.IsForwards(CurrentGait) && elapsedMilliseconds - lastGaitChangeMs > 300L)
                        {
                            __instance.SpeedUp();
                            Traverse.Create(__instance).Field("lastGaitChangeMs").SetValue(elapsedMilliseconds);
                        }
                        lastGaitChangeMs = Traverse.Create(__instance).Field("lastGaitChangeMs").GetValue<long>();
                        if ((ShouldStopSprint || (!sprint && CurrentGait.IsSprint && scheme == EnumControlScheme.Hold)) && elapsedMilliseconds - lastGaitChangeMs > 300L)
                        {
                            __instance.SlowDown();
                            Traverse.Create(__instance).Field("lastGaitChangeMs").SetValue(elapsedMilliseconds);
                        }
                        Traverse.Create(__instance).Field("prevPrevForwardsKey").SetValue(prevForwardKey);
                        Traverse.Create(__instance).Field("prevPrevBackwardsKey").SetValue(prevBackwardKey);
                        bool temp = scheme == EnumControlScheme.Press && forward;
                        Traverse.Create(__instance).Field("prevForwardKey").SetValue(temp);
                        temp = scheme == EnumControlScheme.Press && backward;
                        Traverse.Create(__instance).Field("prevBackwardKey").SetValue(temp);
                    }
                }

                if (canturn && (amcontrols.Left || amcontrols.Right))
                {
                    float dir = amcontrols.amLeftRight + amcontrols.amLeftRight2;
                    angularMotion += (ebg.GetYawMultiplier() * dir * dt);
                }
                var CurrentGait2 = Traverse.Create(__instance).Field("CurrentGait").GetValue<GaitMeta>();
                if (ebg.IsForwards(CurrentGait2) || ebg.IsBackwards(CurrentGait2))
                {
                    //float dir = amcontrols.amForwardBackward + amcontrols.amForwardBackward2;
                    //CurrentGait2.MoveSpeed = 2; //maybe try adding later
                    float dir = ebg.IsForwards(CurrentGait2) ? 1 : -1;
                    linearMotion += (dir * dt * 2f);
                }
            }
            __result = new Vec2d(linearMotion, angularMotion);
            return false;
        }
    }
}
