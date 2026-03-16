using HarmonyLib;
using Vintagestory.API.Common;

namespace AnalogMovementVS
{
    public class AnalogMovementVSModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            var harmony = new Harmony("AnalogMovementVS");
            harmony.PatchAll();
        }
    }
}
