using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.Client;

namespace AnalogMovementVS
{
    public class EntityControlsAMfVS : EntityControls
    {
        //enable normal keyboard movement controls(wasd)
        public bool EnableKeyboardBoolMovement = true;

        //also jump=up and sneak=down when swimming or flying
        public bool EnableKeyboardJumpSneakSprint = true;

        //game won't considered you moving until you move this fast (0 to 1.0 float); you'll still move but won't animate or step up slabs
        public float MinSpeedForMovement = 0.1f;

        //mounted on seat or saddle
        public bool IsMounted { get; internal set; } = false;

        //should disable jump, sneak, & togglesprint when false
        public bool IsMouseGrabbed { get; internal set; } = false;

        //aka GuiDialogEscapeMenu
        public bool IsPauseMenuOpen { get; internal set; } = false;

        //control movement along axis(-1.0 to 0 to 1.0 float)
        public float amForwardBackward = 0;

        //control movement along axis(-1.0 to 0 to 1.0 float)
        public float amLeftRight = 0;

        //use these instead of the inherited ones from entitycontrols
        public bool amJump = false;
        public bool amSneak = false;
        public bool amSprint = false;

        //secondary controls for keyboard input while also using analog inputs
        internal int amForwardBackward2 = 0;
        internal int amLeftRight2 = 0;
        internal bool amJump2 = false;
        internal bool amSneak2 = false;
        internal bool amSprint2 = false;

        //the speed set by SystemPlayerControl from WorldPlayerData
        internal float amIncomingMoveSpeed = 1f;


        public override void CalcMovementVectors(EntityPos pos, float dt)
        {
            //disable movement when tabbed out or 'paused' in multiplayer
            if (!ScreenManager.Platform.IsFocused || IsPauseMenuOpen)
            {
                WalkVector.Set(0,0,0);
                FlyVector.Set(0,0,0);
                Forward = false;
                Backward = false;
                Left = false;
                Right = false;
                return;
            }

            double moveSpeed = dt * GlobalConstants.BaseMoveSpeed * amIncomingMoveSpeed * GlobalConstants.OverallSpeedMultiplier;
            
            double dz = amForwardBackward + amForwardBackward2;
            double dx = amLeftRight + amLeftRight2;
            dz = Math.Clamp(dz, -1, 1);
            dx = Math.Clamp(dx, -1, 1);

            float amMoveSpeed;
            
            if (dz == 0 && dx == 0) amMoveSpeed = 1f;
            else if (Math.Abs(dz) > Math.Abs(dx)) amMoveSpeed = (float)dz;
            else amMoveSpeed = (float)dx;
            MovespeedMultiplier = amMoveSpeed * amIncomingMoveSpeed; //might cause speed changes to be behind ~1 tick but its fine

            if (dz > MinSpeedForMovement) Forward = true; else Forward = false;
            if (dz < -MinSpeedForMovement) Backward = true; else Backward = false;
            if (dx > MinSpeedForMovement) Left = true; else Left = false;
            if (dx < -MinSpeedForMovement) Right = true; else Right = false;
            
            double cosPitch = Math.Cos(pos.Pitch);
            double sinPitch = Math.Sin(pos.Pitch);

            double cosYaw = Math.Cos(-pos.Yaw);
            double sinYaw = Math.Sin(-pos.Yaw);

            WalkVector.Set(
                dx * cosYaw - dz * sinYaw,
                0,
                dx * sinYaw + dz * cosYaw
            );

            if (WalkVector.Length() > 1)
            {
                WalkVector.Normalize();
            }
            WalkVector.Mul(moveSpeed, 1, moveSpeed);


            if (FlyPlaneLock == EnumFreeMovAxisLock.Y) { cosPitch = -1; }

            FlyVector.Set(
                dx * cosYaw + dz * cosPitch * sinYaw,
                dz * sinPitch,
                dx * sinYaw - dz * cosPitch * cosYaw
            );
            FlyVector.Mul(moveSpeed);

            if (FlyPlaneLock == EnumFreeMovAxisLock.X) { FlyVector.X = 0; }
            if (FlyPlaneLock == EnumFreeMovAxisLock.Y) { FlyVector.Y = 0; }
            if (FlyPlaneLock == EnumFreeMovAxisLock.Z) { FlyVector.Z = 0; }
        }
    }
    //an extra class just so we can tell when we are mounted
    public class EntityControlsMountAMfVS : EntityControlsAMfVS 
    {
        public EntityControlsMountAMfVS()
        {
            IsMounted = true;
        }
    }
}
