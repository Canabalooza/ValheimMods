# Simple Sort

A Valheim BepInEx plugin that sorts the player and container inventories by name, weight, or value.

## Controls

### Keyboard

Hold a modifier and press a sort key while the mouse is hovering over the player or container inventory panel.

| Action | Keys |
| --- | --- |
| Sort by name | `Left Alt` + `N` (ascending) / `Left Ctrl` + `N` (descending) |
| Sort by weight | `Left Alt` + `H` (ascending) / `Left Ctrl` + `H` (descending) |
| Sort by value | `Left Alt` + `V` (ascending) / `Left Ctrl` + `V` (descending) |

### Gamepad

Hold the Valheim **Alternate Function** button (`JoyAltKeys` in non-classic mode, `JoyAltPlace` in classic mode) and tap a D-pad direction. The sort applies to whichever inventory tab is currently active.

| Action | Buttons |
| --- | --- |
| Sort by name | Alternate Function + D-pad **Left** |
| Sort by weight | Alternate Function + D-pad **Up** |
| Sort by value | Alternate Function + D-pad **Right** |

Repeating the same sort toggles between ascending and descending. Choosing a different sort type always starts ascending.

While the Alternate Function + D-pad chord is held, the mod suppresses the vanilla `InventoryGrid.UpdateGamepad`, `HotkeyBar.Update`, and `Player.StartGuardianPower` handlers so the chord does not move the grid cursor, swap hotbar slots, or trigger a guardian power.

## Configuration

Settings live in `BepInEx/config/aedenthorn.SimpleSort.cfg`.

| Key | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Enable the mod. |
| `UseGamepad` | `true` | Allow sorting with a gamepad. |
| `AllowPlayerInventorySort` | `true` | Allow sorting the player inventory. |
| `PlayerStickySort` | `true` | Reapply the last player sort when the inventory opens. |
| `ContainerStickySort` | `true` | Reapply the last container sort when a container opens. |
| `PlayerSortStartRow` | `2` | First player inventory row to sort (keeps the hotbar untouched). |
| `PlayerSortEndRow` | `-1` | Last player inventory row to sort; `-1` sorts to the end. |
| `AscendingModifierKey` | `left alt` | Keyboard ascending modifier. |
| `DescendingModifierKey` | `left ctrl` | Keyboard descending modifier. |
| `SortByNameKey` | `n` | Keyboard sort-by-name key. |
| `SortByWeightKey` | `h` | Keyboard sort-by-weight key. |
| `SortByValueKey` | `v` | Keyboard sort-by-value key. |

## Building

```
dotnet build .\SimpleSort.csproj -c Release
```

The output DLL is written to `bin/Release/netstandard2.1/SimpleSort.dll`. Copy it into `BepInEx/plugins`.

## Attribution

Based on **SimpleSort** by aedenthorn: <https://github.com/aedenthorn/ValheimMods/tree/master/SimpleSort>
