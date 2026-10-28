Original source: https://github.com/aedenthorn/ValheimMods (SimpleSort subfolder); this repo holds our modified copy with git history starting from our changes.

# CHANGES.md — our copy vs upstream SimpleSort subfolder

Baseline: `upstream/master` (aedenthorn/ValheimMods monorepo), subtree `SimpleSort/`.
Diff summary: 7 files, +430/−287.

## Version & metadata
- Plugin version bumped 0.9.0 -> 1.0.0 (GUID `aedenthorn.SimpleSort` unchanged).
- Added `README.md` (controls, config table, build instructions, attribution) and `icon.png`.
- Added `.gitignore` (bin/, obj/, .vs/).

## Restructure / modernization (TBD: purely cosmetic vs required by our build setup)
- File-scoped namespace `SimpleSort`, `sealed class`, plugin GUID/name/version as consts,
  `null!`-annotated statics with `<Nullable>enable`, `OnDestroy` unpatches the harmony instance.
- `AedenthornUtils.cs` deleted — its input-helper logic inlined into `BepInExPlugin.cs`
  (TBD: confirm no behavior change from the inlined helpers).
- `SortUtils.cs` moved into the `SimpleSort` namespace, made `internal`, comparators
  reworked (TBD: exact per-field tie-breaking changes vs upstream).

## Standalone build
- `SimpleSort.csproj` no longer imports the monorepo `valheim.targets`; it is now a
  self-contained project: netstandard2.1, explicit references to local Valheim managed +
  publicized assemblies and a Gale-managed BepInEx (`%APPDATA%\com.kesomannen.gale\...`).

## Behavioral changes visible in the diff
- New gamepad sorting: hold Valheim's Alternate-Function button + D-pad
  (Left=name, Up=weight, Right=value); sorting applies to the active inventory tab.
  While the chord is held, vanilla `InventoryGrid.UpdateGamepad`, `HotkeyBar.Update`,
  and `Player.StartGuardianPower` handlers are suppressed so the chord doesn't move the
  cursor, swap hotbar slots, or trigger a guardian power.
- Repeating the same sort toggles asc/desc; switching sort type resets to ascending.
- New config keys: `UseGamepad`, sticky-sort flags plus remembered sort type/direction
  per inventory (`PlayerStickySort`, `ContainerStickySort`, `PlayerSortType`,
  `ContainerSortType`, `PlayerSortAscending`, `ContainerSortAscending`).
  (TBD: upstream also had sticky-sort settings; the "remembered sort type/direction"
  persistence is ours — confirm which keys existed at 0.9.0.)
