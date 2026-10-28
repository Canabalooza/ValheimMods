using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SimpleSort;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class BepInExPlugin : BaseUnityPlugin
{
    private const string PluginGuid = "aedenthorn.SimpleSort";
    private const string PluginName = "Simple Sort";
    private const string PluginVersion = "1.0.0";

    private enum SortType
    {
        Name,
        Weight,
        Value,
    }

    private static BepInExPlugin _instance = null!;
    private static ConfigEntry<bool> _modEnabled = null!;
    private static ConfigEntry<bool> _useGamepad = null!;
    private static ConfigEntry<bool> _allowPlayerInventorySort = null!;
    private static ConfigEntry<bool> _playerStickySort = null!;
    private static ConfigEntry<bool> _containerStickySort = null!;
    private static ConfigEntry<SortType> _playerSortType = null!;
    private static ConfigEntry<SortType> _containerSortType = null!;
    private static ConfigEntry<bool> _playerSortAscending = null!;
    private static ConfigEntry<bool> _containerSortAscending = null!;
    private static ConfigEntry<string> _ascendingModifierKey = null!;
    private static ConfigEntry<string> _descendingModifierKey = null!;
    private static ConfigEntry<string> _sortByNameKey = null!;
    private static ConfigEntry<string> _sortByWeightKey = null!;
    private static ConfigEntry<string> _sortByValueKey = null!;
    private static ConfigEntry<int> _playerSortStartRow = null!;
    private static ConfigEntry<int> _playerSortEndRow = null!;
    internal static ConfigEntry<bool> _graveRestoreOriginalSlots = null!;

    private Harmony _harmony = null!;

    private void Awake()
    {
        _instance = this;
        _modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod.");
        _useGamepad = Config.Bind("General", "UseGamepad", true, "Allow sorting with a gamepad.");
        _allowPlayerInventorySort = Config.Bind("General", "AllowPlayerInventorySort", false, "Allow player inventory sorting. Off sorts containers only.");
        _playerStickySort = Config.Bind("General", "PlayerStickySort", true, "Automatically apply the last sort type to the player inventory when opened.");
        _containerStickySort = Config.Bind("General", "ContainerStickySort", true, "Automatically apply the last sort type to a container inventory when opened.");
        _playerSortAscending = Config.Bind("General", "PlayerSortAscending", true, "Current player sort direction.");
        _containerSortAscending = Config.Bind("General", "ContainerSortAscending", true, "Current container sort direction.");
        _playerSortType = Config.Bind("General", "PlayerSortType", SortType.Name, "Current player sort type.");
        _containerSortType = Config.Bind("General", "ContainerSortType", SortType.Name, "Current container sort type.");
        _sortByValueKey = Config.Bind("Keyboard", "SortByValueKey", "v", "Sort by value key.");
        _sortByWeightKey = Config.Bind("Keyboard", "SortByWeightKey", "h", "Sort by weight key.");
        _sortByNameKey = Config.Bind("Keyboard", "SortByNameKey", "n", "Sort by name key.");
        _ascendingModifierKey = Config.Bind("Keyboard", "AscendingModifierKey", "left alt", "Sort ascending modifier key.");
        _descendingModifierKey = Config.Bind("Keyboard", "DescendingModifierKey", "left ctrl", "Sort descending modifier key.");
        _playerSortStartRow = Config.Bind("General", "PlayerSortStartRow", 2, "Player sort start row.");
        _playerSortEndRow = Config.Bind("General", "PlayerSortEndRow", -1, "Player sort end row; use -1 to sort to the end.");
        _graveRestoreOriginalSlots = Config.Bind("General", "GraveRestoreOriginalSlots", true, "When looting your own grave, restore items to the exact inventory slots they were in at death; items in the way are pushed to free slots, grave stacks are only left behind when the inventory is full or the stack would overweight you.");

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    internal static bool ModEnabled => _modEnabled.Value;

    internal static void LogGraveWarning(string message)
    {
        _instance?.Logger.LogWarning(message);
    }

    private static bool IsControllerSortModifierHeld()
    {
        if (!_modEnabled.Value || !_useGamepad.Value || !ZInput.IsGamepadActive())
        {
            return false;
        }

        string modifier = ZInput.IsNonClassicFunctionality() ? "JoyAltKeys" : "JoyAltPlace";
        return ZInput.instance != null && ZInput.GetButton(modifier);
    }

    private static bool IsControllerSortChord()
    {
        return IsControllerSortModifierHeld()
               && (ZInput.GetButton("JoyDPadLeft")
                   || ZInput.GetButton("JoyDPadUp")
                   || ZInput.GetButton("JoyDPadRight"));
    }

    private static bool TryGetControllerSort(out SortType sortType)
    {
        sortType = SortType.Name;
        if (!IsControllerSortModifierHeld())
        {
            return false;
        }

        if (ZInput.GetButtonDown("JoyDPadLeft"))
        {
            sortType = SortType.Name;
            return true;
        }

        if (ZInput.GetButtonDown("JoyDPadUp"))
        {
            sortType = SortType.Weight;
            return true;
        }

        if (ZInput.GetButtonDown("JoyDPadRight"))
        {
            sortType = SortType.Value;
            return true;
        }

        return false;
    }

    private static bool NextDirection(SortType requested, SortType current, bool currentAscending)
    {
        return requested != current || !currentAscending;
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    private static class InventoryGuiShowPatch
    {
        private static void Postfix(Container ___m_currentContainer)
        {
            if (!_modEnabled.Value)
            {
                return;
            }

            if (Player.m_localPlayer && _playerStickySort.Value && _allowPlayerInventorySort.Value)
            {
                SortByType(_playerSortType.Value, Player.m_localPlayer.GetInventory(), _playerSortAscending.Value, true);
            }

            if (_containerStickySort.Value && ___m_currentContainer?.GetInventory() != null && ___m_currentContainer.GetComponent<TombStone>() == null)
            {
                SortByType(_containerSortType.Value, ___m_currentContainer.GetInventory(), _containerSortAscending.Value, false);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
    private static class InventoryGuiUpdatePatch
    {
        private static void Postfix(InventoryGui __instance, Container ___m_currentContainer, int ___m_activeGroup)
        {
            if (!_modEnabled.Value)
            {
                return;
            }

            string inventoryName = string.Empty;
            SortType sortType = SortType.Name;
            bool ascending = true;

            if (Input.GetKey(_ascendingModifierKey.Value.ToLowerInvariant())
                || Input.GetKey(_descendingModifierKey.Value.ToLowerInvariant()))
            {
                ascending = Input.GetKey(_ascendingModifierKey.Value.ToLowerInvariant());
                TryGetKeyboardSort(ref inventoryName, ref sortType);
            }

            if (inventoryName.Length == 0 && TryGetControllerSort(out sortType))
            {
                if (___m_activeGroup == 0 && !__instance.IsContainerOpen())
                {
                    return;
                }

                if (___m_activeGroup == 0)
                {
                    inventoryName = "Container";
                    ascending = NextDirection(sortType, _containerSortType.Value, _containerSortAscending.Value);
                }
                else if (___m_activeGroup == 1)
                {
                    inventoryName = "Player";
                    ascending = NextDirection(sortType, _playerSortType.Value, _playerSortAscending.Value);
                }
            }

            if (inventoryName.Length == 0 || inventoryName == "Player" && !_allowPlayerInventorySort.Value)
            {
                return;
            }

            if (inventoryName == "Player")
            {
                _playerSortType.Value = sortType;
                _playerSortAscending.Value = ascending;
                SortByType(sortType, Player.m_localPlayer.GetInventory(), ascending, true);
            }
            else if (___m_currentContainer?.GetInventory() != null)
            {
                _containerSortType.Value = sortType;
                _containerSortAscending.Value = ascending;
                SortByType(sortType, ___m_currentContainer.GetInventory(), ascending, false);
            }

            _instance.Config.Save();
        }

        private static void TryGetKeyboardSort(ref string inventoryName, ref SortType sortType)
        {
            PointerEventData eventData = new(EventSystem.current)
            {
                position = Input.mousePosition,
            };
            List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (RaycastResult result in results)
            {
                Transform parent = result.gameObject.transform.parent;
                if (result.gameObject.layer != LayerMask.NameToLayer("UI")
                    || result.gameObject.name != "Bkg"
                    || parent == null
                    || parent.name != "Player" && parent.name != "Container")
                {
                    continue;
                }

                if (Input.GetKeyDown(_sortByNameKey.Value.ToLowerInvariant()))
                {
                    inventoryName = parent.name;
                    sortType = SortType.Name;
                }
                else if (Input.GetKeyDown(_sortByWeightKey.Value.ToLowerInvariant()))
                {
                    inventoryName = parent.name;
                    sortType = SortType.Weight;
                }
                else if (Input.GetKeyDown(_sortByValueKey.Value.ToLowerInvariant()))
                {
                    inventoryName = parent.name;
                    sortType = SortType.Value;
                }

                return;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGamepad))]
    private static class InventoryGridUpdateGamepadPatch
    {
        private static bool Prefix()
        {
            return !IsControllerSortChord();
        }
    }

    [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.Update))]
    private static class HotkeyBarUpdatePatch
    {
        private static bool Prefix()
        {
            return !IsControllerSortChord();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.StartGuardianPower))]
    private static class PlayerStartGuardianPowerPatch
    {
        private static bool Prefix()
        {
            return !IsControllerSortChord();
        }
    }

    private static void SortByType(SortType type, Inventory inventory, bool ascending, bool player)
    {
        List<ItemDrop.ItemData> items = inventory.GetAllItems();
        SortUtils.SortByName(items, true);

        for (int i = 0; i < items.Count; i++)
        {
            if (player && !IsPlayerItemInSortRows(items[i]))
            {
                continue;
            }

            while (i < items.Count - 1
                   && items[i].m_stack < items[i].m_shared.m_maxStackSize
                   && items[i + 1].m_shared.m_name == items[i].m_shared.m_name)
            {
                int amount = Mathf.Min(items[i].m_shared.m_maxStackSize - items[i].m_stack, items[i + 1].m_stack);
                items[i].m_stack += amount;
                if (amount == items[i + 1].m_stack)
                {
                    items.RemoveAt(i + 1);
                }
                else
                {
                    items[i + 1].m_stack -= amount;
                }
            }
        }

        switch (type)
        {
            case SortType.Name:
                SortUtils.SortByName(items, ascending);
                break;
            case SortType.Weight:
                SortUtils.SortByWeight(items, ascending);
                break;
            case SortType.Value:
                SortUtils.SortByValue(items, ascending);
                break;
        }

        SortToGrid(inventory, player);
    }

    private static bool IsPlayerItemInSortRows(ItemDrop.ItemData item)
    {
        return (_playerSortStartRow.Value <= 1 || item.m_gridPos.y >= _playerSortStartRow.Value - 1)
               && (_playerSortEndRow.Value <= 0 || item.m_gridPos.y < _playerSortEndRow.Value);
    }

    private static void SortToGrid(Inventory inventory, bool player)
    {
        List<ItemDrop.ItemData> items = inventory.GetAllItems();
        int width = inventory.GetWidth();
        int index = 0;

        foreach (ItemDrop.ItemData item in items)
        {
            if (player && !IsPlayerItemInSortRows(item))
            {
                continue;
            }

            int x = index % width;
            int y = index / width + (player ? _playerSortStartRow.Value - 1 : 0);
            item.m_gridPos = new Vector2i(x, y);
            index++;
        }

        inventory.Changed(false, false);
    }
}
