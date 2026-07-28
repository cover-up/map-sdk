# Changelog

All notable changes to the Cover Up! Map SDK. Format follows
[Keep a Changelog](https://keepachangelog.com/); this package uses semantic versioning.

## [0.4.0] — 2026-07-28

### Changed — BREAKING
- **`Base` is now split into `Fixtures` and `Content`.** The map shape is
  `_CoverUpMap → Base → {Fixtures, Content}`, with the size roots under `Sizes`:

  ```
  _CoverUpMap            [MapConfig, MapSizeVariants, WorkshopMapInfo]
  ├── Base
  │   ├── Fixtures        spawn disc, the arena sun, one-size bounds
  │   └── Content         your geometry and props
  └── Sizes
      ├── Small / Medium / Large
  ```

  The point is deletion safety: the handful of objects a map cannot lose sit apart from
  the thousands a mapper freely rebuilds, so "delete Content and start over" is a safe
  move and "delete Fixtures" is visibly not. **Validate Map now errors** on a missing
  group, on anything loose directly under `Base`, on a spawn outside `Fixtures`, and on a
  size root inside `Base` — and the exporter refuses a scene with errors, so this is a
  hard gate on export, not advice. A directional light outside `Fixtures` is a warning.
- **Migrate an existing map with `Cover Up! ▸ Maps ▸ Group Base`.** It creates the missing
  groups and re-parents what's in the wrong place — spawn, bounds and lights into
  `Fixtures`, everything else into `Content` — and never deletes anything. It also adopts a
  flat scene with no `_CoverUpMap` root at all (it creates one and moves every loose root
  object in). Undoable; save and re-run Validate Map afterwards.
- A scene with **no `_CoverUpMap` root** is warned, not errored — the game's own `box_*`
  maps predate the contract and never pass through the exporter.

### Added
- **Per-map auto-size brackets.** `MapSizeVariants` gains `smallMaxPlayers` (10) and
  `mediumMaxPlayers` (22) — the player counts at which *this* map switches Small → Medium →
  Large when the lobby's Map Size is **Auto**. Defaults reproduce the previous global
  behaviour exactly, and a forced Small/Medium/Large in the lobby ignores them as it always
  ignored the player count. `MapSizeRules.ForPlayers`/`Resolve` gain overloads taking the
  brackets; the old signatures remain and use the defaults. Values are sanitized
  (`MapSizeRules.Sanitize`) because a Workshop map's numbers are untrusted input. Host-side
  only — the host still ships just the resolved `MapSize` byte, so **no wire change**.
  `map.json`'s contract gains advisory `autoSmallMaxPlayers` / `autoMediumMaxPlayers`.
- Validate Map: warns when authored brackets get sanitized, and when brackets are set on a
  map that hasn't built every size (unbuilt sizes clamp to a neighbour, so those brackets
  don't resolve to the size they name). The summary line reports the map's brackets.
- `MapContract` — the reserved group names in one place, shared by the builder, Validate Map
  and Group Base.
- Headless migration: `-executeMethod CoverUp.EditorTools.MapBaseGrouping.RunHeadless
  -mapScene Assets/…/my_map.unity` groups, saves and validates a map scene without opening
  the editor, exiting non-zero if the scene still has contract errors.
- Validate Map warns about objects left at the scene root, outside `_CoverUpMap` — which is
  where a new scene puts the directional light, so it's the usual reason the sun is unowned.

## [0.3.0] — 2026-07-28

### Changed — BREAKING
- **Hiders and hunters now size independently.** `MapConfig.playerScale` is replaced by
  `hiderScale` + `hunterScale`, both defaulting to `GameScale.Default` (1.185). There is no
  `FormerlySerializedAs` fallback: a scene saved against 0.2.x loses its authored scale and
  comes up at the default. **Migrate before opening:** in the scene's YAML, replace the
  `  playerScale: <v>` line with `  hiderScale: <v>` and `  hunterScale: <v>` (do it with
  Unity closed, or the editor rewrites the file first).
- **`GameScale.Player` is deleted**, not aliased, so stale reads fail to compile rather than
  silently resolve to one role. Replacements: `GameScale.Local` (the player at this keyboard —
  resolves the local role for you), and `GameScale.Hider` / `GameScale.Hunter` where the role is
  explicit. `SetPlayerScale` → `SetPlayerScales(hider, hunter)` / `SetUniformScale(scale)`.
- **`map.json` is now format 2.** `contract.playerScale` / `contract.approxDollMeters` become
  `hiderScale`, `hunterScale`, `approxHiderMeters`, `approxHunterMeters`. A format-1 manifest
  still parses (new fields read 0) — the contract block is advisory, and the scales the game
  applies come from the `MapConfig` inside the bundle.

### Added
- `GameScale.AdvisedMaxRoleRatio` (2×) — past this the shared camera framing and the
  `CharacterController` step/skin tuning stop suiting both roles at once.
- `MapConfigEditor`: two sliders with a **Link roles** toggle (on by default), per-role height
  readouts, and a warning when the roles diverge past the advised ratio.
- Validate Map: per-role guard-rail checks plus the same divergence warning; the summary line
  now reports both role heights.

### Notes
- Exposure scoring and the aim-ray rings are **hider-scaled only** — the ring is a bubble
  measured at the hider. Sizing hunters therefore never moves the balance; it governs only the
  hunter's own body, collider, movement, camera and gun.
- Scaling stays whole-player, so a hunter authored at twice a hider's size also covers ground
  twice as fast, in the same stride rhythm.

## [0.2.0] — 2026-07-25

### Added
- **Editor tooling now ships in the package**: `MapSizeTools` (Validate Map + Preview Size),
  `WorkshopMapExporter` (Export Workshop Map), `WorkshopAutoExport` (Auto-Export On Save),
  `MapConfigEditor`, `MapBoundsGizmos`, `ArenaStandards`, and `ExampleSizedMapBuilder`
  (Create Example Sized Map) — assembly `CoverUp.MapSdk.Editor`.
- `WorkshopMapManifest` and `MapSceneGuard` moved into the package runtime (`CoverUp.MapSdk`), so a
  bare project can validate/export without the game.
- `LocalMapsFolder` — the local-maps folder-path convention (the SDK↔game meeting point).

### Changed
- The package is now **standalone-distributable** (git URL) rather than game-embedded only.
- Licensed under **MIT** (see `LICENSE.md`).

### Notes
- `MapInfo` mapping (`ToMapInfo`) stays in the game, not the package, to keep the SDK free of game
  code.

## [0.1.0] — 2026-07-25

### Added
- Initial release: the `_CoverUpMap` runtime components (`MapConfig`, `MapSizeVariants`,
  `MapBoundsVolume`, `CamouflageSurface`, `MapSpawnDisc`, `WorkshopMapInfo`) plus `GameScale`,
  `MapSize`/`MapSizeSetting`/`MapSizeRules` — assembly `CoverUp.MapSdk`, GUIDs frozen.
