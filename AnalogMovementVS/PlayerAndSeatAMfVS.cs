using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace AnalogMovementVS
{    
    [HarmonyPatch(typeof(EntityPlayer), MethodType.Constructor)]
    class EntityPlayerPatch
    {
        public static void Postfix(EntityPlayer __instance)
        {
            Traverse.Create(__instance).Field("controls").SetValue(new EntityControlsAMfVS());
            Traverse.Create(__instance).Field("servercontrols").SetValue(new EntityControlsAMfVS());
        }
    }
    
    //might break any mod inheriting from EntitySeat idk
    [HarmonyPatch(typeof(EntitySeat), MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(IMountable), typeof(string), typeof(SeatConfig) })]
    public static class EntitySeatPatch
    {
        public static void Postfix(EntitySeat __instance)
        {
            var controls = new EntityControlsMountAMfVS();
            __instance.controls = controls;
            
            var onControlsMethod = AccessTools.Method(typeof(EntitySeat), "onControls");
            if (onControlsMethod != null)
            {
                controls.OnAction = (OnEntityAction)Delegate.CreateDelegate(typeof(OnEntityAction), __instance, onControlsMethod);
            }
        }
    }
}
