# Changelog

All notable changes to the Cover Up! Map SDK. Format follows
[Keep a Changelog](https://keepachangelog.com/); this package uses semantic versioning.

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
