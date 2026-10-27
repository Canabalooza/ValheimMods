using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SimpleSort
{
    // Replaces vanilla grave looting for your OWN grave (TombStone.Interact).
    // CreateTombStone/MoveInventoryToGrave snapshot the player grid exactly, so every
    // grave stack still carries its death m_gridPos. Desired behavior:
    //  - empty inventory: every stack goes back into its exact death slot.
    //  - inventory not empty but everything fits: stacks colliding with grave slots
    //    get pushed into free slots (never onto other grave target slots), then every
    //    grave stack lands in its exact death slot.
    //  - inventory too full: only grave stacks whose original slot is free (and that
    //    don't exceed carry weight) move; nothing else is touched; the rest stays in
    //    the grave for a later visit.
    // Items are transplanted as ItemData references (no clone, no stack-merge, no
    // auto-place scatter that vanilla MoveAll's fallback pass does). Inventory.Changed
    // is fired via reflection so weight/UI/ZDO-save events run as usual. Anything
    // unexpected falls through to vanilla untouched.
    [HarmonyPatch(typeof(TombStone), "Interact")]
    public static class TombStone_Interact_Patch
    {
        private static readonly FieldInfo f_container = AccessTools.Field(typeof(TombStone), "m_container");
        private static readonly MethodInfo m_isOwner = AccessTools.Method(typeof(TombStone), "IsOwner");
        private static readonly MethodInfo m_changed = AccessTools.Method(typeof(Inventory), "Changed");

        public static bool Prefix(TombStone __instance, Humanoid character, bool hold, ref bool __result)
        {
            if (!BepInExPlugin.modEnabled.Value || !BepInExPlugin.graveRestoreOriginalSlots.Value || hold)
                return true;
            Player player = character as Player;
            if (player == null || player.GetInventory() == null)
                return true;
            Container container = f_container.GetValue(__instance) as Container;
            Inventory grave = container != null ? container.GetInventory() : null;
            if (grave == null || grave.NrOfItems() == 0)
                return true;
            if (!(bool)m_isOwner.Invoke(__instance, null))
                return true;
            try
            {
                Restore(grave, player);
                if (grave.NrOfItems() == 0)
                    container.m_onTakeAllSuccess?.Invoke();
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                BepInExPlugin.Dbgl("grave slot restore failed, using vanilla looting: " + e);
                return true;
            }
        }

        private static void Restore(Inventory grave, Player player)
        {
            Inventory dest = player.GetInventory();
            int w = dest.GetWidth();
            int h = dest.GetHeight();
            List<ItemDrop.ItemData> stacks = new List<ItemDrop.ItemData>(grave.GetAllItems());
            stacks.Sort((a, b) => (a.m_gridPos.y * 1000 + a.m_gridPos.x).CompareTo(b.m_gridPos.y * 1000 + b.m_gridPos.x));

            HashSet<Vector2i> targets = new HashSet<Vector2i>();
            foreach (ItemDrop.ItemData s in stacks)
                if (InRange(s.m_gridPos, w, h))
                    targets.Add(s.m_gridPos);

            float weight = dest.GetTotalWeight();
            bool fits = dest.GetAllItems().Count + stacks.Count <= w * h
                && weight + grave.GetTotalWeight() <= player.GetMaxCarryWeight();

            foreach (ItemDrop.ItemData s in stacks)
            {
                Vector2i p = s.m_gridPos;
                if (weight + s.GetWeight() > player.GetMaxCarryWeight())
                    continue; // leave overweight stacks in the grave
                if (!InRange(p, w, h))
                {
                    // grid shape changed since death (e.g. Megingjord): only relocate when everything fits
                    if (!fits)
                        continue;
                    Vector2i free = FindFree(dest, targets, w, h);
                    if (free.x < 0)
                        continue;
                    Move(grave, dest, s, free);
                    weight += s.GetWeight();
                    continue;
                }
                ItemDrop.ItemData occupant = dest.GetItemAt(p.x, p.y);
                if (occupant != null)
                {
                    if (!fits)
                        continue; // strict mode: never rearrange the player's items
                    Vector2i free = FindFree(dest, targets, w, h);
                    if (free.x < 0)
                        continue;
                    occupant.m_gridPos = free;
                }
                Move(grave, dest, s, p);
                weight += s.GetWeight();
            }

            m_changed.Invoke(dest, new object[] { false, false });
            m_changed.Invoke(grave, new object[] { false, false });
        }

        private static bool InRange(Vector2i p, int w, int h)
        {
            return p.x >= 0 && p.y >= 0 && p.x < w && p.y < h;
        }

        // first empty slot that is not reserved for a grave stack (top-left to bottom-right)
        private static Vector2i FindFree(Inventory inv, HashSet<Vector2i> reserved, int w, int h)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (!reserved.Contains(new Vector2i(x, y)) && inv.GetItemAt(x, y) == null)
                        return new Vector2i(x, y);
            return new Vector2i(-1, -1);
        }

        private static void Move(Inventory from, Inventory to, ItemDrop.ItemData item, Vector2i pos)
        {
            // GetAllItems() returns the live backing list (vanilla decompile confirms)
            from.GetAllItems().Remove(item);
            item.m_gridPos = pos;
            to.GetAllItems().Add(item);
        }
    }
}
