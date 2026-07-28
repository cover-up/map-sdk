# Changelog

All notable changes to the Cover Up! Map SDK. Format follows
[Keep a Changelog](https://keepachangelog.com/); this package uses semantic versioning.

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
