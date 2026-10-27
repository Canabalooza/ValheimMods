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
    //  - inventory not empty: stacks colliding with grave slots get pushed into free
    //    slots (never onto other grave target slots), then every grave stack lands in
    //    its exact death slot; greedily, stack by stack.
    //  - a grave stack is only left behind when the inventory is genuinely full
    //    (no free slot to push into) or picking it would exceed carry weight.
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
                if (Fits(s, s.m_gridPos, w, h))
                    AddFootprint(targets, s);

            float weight = dest.GetTotalWeight();
            List<ItemDrop.ItemData> outOfRange = new List<ItemDrop.ItemData>();

            // greedy: always restore in-place when possible, pushing the occupant into a
            // free area (never one reserved for another grave stack). Only leaves a grave
            // stack behind when there is genuinely no free space left or it would
            // overweight the player.
            foreach (ItemDrop.ItemData s in stacks)
            {
                Vector2i p = s.m_gridPos;
                if (weight + s.GetWeight() > player.GetMaxCarryWeight())
                    continue; // leave overweight stacks in the grave
                if (!Fits(s, p, w, h))
                {
                    // grid shape changed since death (e.g. Megingjord): place last, anywhere free
                    outOfRange.Add(s);
                    continue;
                }
                ItemDrop.ItemData occupant = dest.GetItemAt(p.x, p.y);
                if (occupant != null)
                {
                    Vector2i free = FindFreeArea(dest, targets, w, h, occupant);
                    if (free.x < 0)
                        continue; // full: leave this grave stack, occupant stays put
                    occupant.m_gridPos = free;
                }
                Move(grave, dest, s, p);
                weight += s.GetWeight();
            }

            foreach (ItemDrop.ItemData s in outOfRange)
            {
                if (weight + s.GetWeight() > player.GetMaxCarryWeight())
                    continue;
                Vector2i free = FindFreeArea(dest, targets, w, h, s);
                if (free.x < 0)
                    break;
                Move(grave, dest, s, free);
                weight += s.GetWeight();
            }

            m_changed.Invoke(dest, new object[] { false, false });
            m_changed.Invoke(grave, new object[] { false, false });
        }

        private static bool Fits(ItemDrop.ItemData item, Vector2i p, int w, int h)
        {
            return p.x >= 0 && p.y >= 0 && p.x + item.m_size.x <= w && p.y + item.m_size.y <= h;
        }

        private static void AddFootprint(HashSet<Vector2i> set, ItemDrop.ItemData item)
        {
            for (int dx = 0; dx < item.m_size.x; dx++)
                for (int dy = 0; dy < item.m_size.y; dy++)
                    set.Add(new Vector2i(item.m_gridPos.x + dx, item.m_gridPos.y + dy));
        }

        // first free rectangular area big enough for item, skipping cells reserved for grave stacks
        private static Vector2i FindFreeArea(Inventory inv, HashSet<Vector2i> reserved, int w, int h, ItemDrop.ItemData item)
        {
            for (int y = 0; y + item.m_size.y <= h; y++)
                for (int x = 0; x + item.m_size.x <= w; x++)
                {
                    bool ok = true;
                    for (int dx = 0; dx < item.m_size.x && ok; dx++)
                        for (int dy = 0; dy < item.m_size.y && ok; dy++)
                        {
                            Vector2i c = new Vector2i(x + dx, y + dy);
                            if (reserved.Contains(c) || inv.GetItemAt(c.x, c.y) != null)
                                ok = false;
                        }
                    if (ok)
                        return new Vector2i(x, y);
                }
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
