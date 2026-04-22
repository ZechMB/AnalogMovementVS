using HarmonyLib;
using System;
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
        public static bool Prefix(EntityBehaviorRideable __instance, ref double __result, float dt)
        {
            var coyoteTimer = Traverse.Create(__instance).Field("coyoteTimer").GetValue<float>();
            var capi = Traverse.Create(__instance).Field("capi").GetValue<ICoreClientAPI>();
            var lastJumpMs = Traverse.Create(__instance).Field("lastJumpMs").GetValue<long>();
            var scheme = Traverse.Create(__instance).Field("scheme").GetValue<EnumControlScheme>();
            var ebg = Traverse.Create(__instance).Field("ebg").GetValue<EntityBehaviorGait>();
            var api = Traverse.Create(__instance).Field("api").GetValue<ICoreAPI>();
            var angularMotionWild = Traverse.Create(__instance).Field("angularMotionWild").GetValue<float>();

            Traverse.Create(__instance).Field("jumpNow").SetValue(false);
            double angularMotion = 0;
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
                    foreach (CanRideDelegate dele in canRideDelegate.GetInvocationList())
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
                    foreach (CanRideDelegate dele in canTurnDelegate.GetInvocationList())
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


                if (controls.Jump && __instance.entity.World.ElapsedMilliseconds - lastJumpMs > 1500 && __instance.entity.Alive && 
                    (__instance.entity.OnGround || coyoteTimer > 0 || (__instance.entity.Api.Side == EnumAppSide.Client && __instance.entity.EntityId != __instance.Controller.EntityId)))
                {
                    lastJumpMs = __instance.entity.World.ElapsedMilliseconds;
                    Traverse.Create(__instance).Field("jumpNow").SetValue(true);
                }

                if (__instance.entity.Api.Side != EnumAppSide.Server)
                {
                    var prevForwardKey = Traverse.Create(__instance).Field("prevForwardKey").GetValue<bool>();
                    var prevBackwardKey = Traverse.Create(__instance).Field("prevBackwardKey").GetValue<bool>();
                    var prevSprintKey = Traverse.Create(__instance).Field("prevSprintKey").GetValue<bool>();
                    var prevPrevForwardKey = Traverse.Create(__instance).Field("prevPrevForwardKey").GetValue<bool>();
                    var prevPrevBackwardKey = Traverse.Create(__instance).Field("prevPrevBackwardKey").GetValue<bool>();
                    var prevPrevSprintKey = Traverse.Create(__instance).Field("prevPrevSprintKey").GetValue<bool>();

                    bool forward = amcontrols.Forward;
                    bool backward = amcontrols.Backward;
                    bool sprint = controls.Sprint;

                    bool wasIdle = ebg.IsIdle;

                    if (forward && !prevForwardKey && !prevPrevForwardKey)
                    {
                        __instance.SpeedUp(false);
                    }

                    if (backward && !prevBackwardKey && !prevPrevBackwardKey)
                    {
                        __instance.SlowDown();
                    }

                    if (sprint && (wasIdle || (!prevSprintKey && !prevPrevSprintKey)))
                    {
                        if (ebg.CurrentGait.HasForwardMotion) __instance.SpeedUp(true);
                        else if (ebg.CurrentGait.HasBackwardMotion) __instance.SlowDown();
                    }

                    if (scheme == EnumControlScheme.Hold)
                    {
                        var IsSprinting = Traverse.Create(__instance).Method("IsSprinting").GetValue<bool>();
                        if (IsSprinting && !sprint)
                        {
                            __instance.SlowDown();
                        }
                        if ((!forward && !backward && !ebg.IsIdle)
                            || (!forward && ebg.CurrentGait.HasForwardMotion)
                            || (!backward && ebg.CurrentGait.HasBackwardMotion))
                        {
                            ebg.SetIdle();
                        }
                        if (forward && !backward && !ebg.CurrentGait.HasForwardMotion)
                        {
                            __instance.SpeedUp(false);
                        }
                        if (backward && !forward && !ebg.CurrentGait.HasBackwardMotion)
                        {
                            __instance.SlowDown();
                        }
                    }

                    Traverse.Create(__instance).Field("prevPrevForwardsKey").SetValue(prevForwardKey);
                    Traverse.Create(__instance).Field("prevPrevBackwardsKey").SetValue(prevBackwardKey);
                    Traverse.Create(__instance).Field("prevPrevSprintKey").SetValue(prevSprintKey);
                    Traverse.Create(__instance).Field("prevForwardKey").SetValue(forward);
                    Traverse.Create(__instance).Field("prevBackwardKey").SetValue(backward);
                    Traverse.Create(__instance).Field("prevSprintKey").SetValue(sprint);
                }

                #region Motion update
                if (canturn && (amcontrols.amLeftRight != 0 || amcontrols.amLeftRight2 != 0))
                {
                    float dir = amcontrols.amLeftRight + amcontrols.amLeftRight2;
                    angularMotion += ebg.GetYawMultiplier() * dir * dt;
                }
                #endregion
            }

            __result = angularMotion;
            return false;
        }
    }
}
