using System;
using HarmonyLib;

namespace SimpleSort
{
    // Vanilla TombStone.Interact never opens the container panel when the whole grave
    // fits: it calls Container.TakeAll directly (EasyFitInInventory). The sticky sort
    // hangs off InventoryGui.Show, so the auto-loot path sorted nothing and items
    // landed in death order, while the full-inventory path opened the panel, sorted,
    // and looted sorted — two different outcomes from the same E press. Sort the
    // tombstone's inventory right before TakeAll so both recovery paths match normal
    // chest behavior. Fully guarded: any error just leaves vanilla looting intact.
    [HarmonyPatch(typeof(Container), "TakeAll")]
    public static class GraveAutoLootSort_Patch
    {
        public static void Prefix(Container __instance)
        {
            try
            {
                if (!BepInExPlugin.modEnabled.Value || !BepInExPlugin.containerStickySort.Value)
                {
                    return;
                }
                if (__instance.GetComponent<TombStone>() == null)
                {
                    return;
                }
                Inventory inventory = __instance.GetInventory();
                if (inventory == null || inventory.NrOfItems() == 0)
                {
                    return;
                }
                BepInExPlugin.SortByType(BepInExPlugin.containerSortType.Value, inventory,
                    BepInExPlugin.containerSortAsc.Value, false);
            }
            catch (Exception e)
            {
                BepInExPlugin.Dbgl("grave auto-loot sort skipped: " + e.Message);
            }
        }
    }
}
