using HarmonyLib;
using Vintagestory.API.Common;

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
}
