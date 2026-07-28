# Changelog

All notable changes to the Cover Up! Map SDK. Format follows
[Keep a Changelog](https://keepachangelog.com/); this package uses semantic versioning.

## [Unreleased]

## [0.5.1] — 2026-07-28

### Removed
- **The role-scale divergence warning, and `GameScale.AdvisedMaxRoleRatio` with it.** Both
  Validate Map and the `MapConfig` inspector used to warn once hider and hunter scales differed
  by more than 2×. That was wrong: sizing the two sides far apart is the *whole point* of having
  two fields, so the check fired hardest on exactly the maps using the feature as intended —
  a warning that means "you did the thing" is noise, and noise trains mappers to ignore the
  list that also holds the real errors. Each role is still checked against the scale guard
  rails on its own; only the comparison between them is gone.

## [0.5.0] — 2026-07-28

### Added
- **Per-role spawns (`MapSpawnDisc.Role`).** A disc can now place `Both` (the default),
  `Hiders` or `Hunters`, so a map can open with hiders deep in the space and hunters held
  at an entrance instead of everyone materialising on one point. Discs draw green/orange/
  cyan by role in the scene view.

  **Additive, not a migration.** `Both` is the default and the value every existing disc
  deserializes to, and a dedicated disc only overrides its own side — adding a `Hunters`
  disc to a map that has a `Both` disc leaves hiders exactly where they were. Several discs
  may share a role; a player lands in a random one of them, which is a cheap way to spread a
  spawn out. **Validate Map** now errors if either side has nowhere to land, and checks
  every disc — not just the first — for the size-root and `Fixtures` placement rules.

- **Per-map hunter camera (`MapConfig ▸ Hunter Camera`).** `Auto` (default) leaves the
  player's toggle alone; `ThirdPerson`/`FirstPerson` force it for that map and hide the
  toggle's on-screen hint. Corridors and open arenas want different cameras, and that's a
  property of the space, so the mapper decides. Hiders are unaffected. Applied at the
  door-open transition and released on return to the island, so it never touches the lobby.

- **Scale reference dolls (`MapReferenceDoll`).** Stand-in bodies at the map's authored
  player height, for judging scale while building: drop one beside a doorway or at the end
  of a sightline and see whether a hider actually fits. Height comes from the scene's
  `MapConfig` for the doll's role, so dragging a scale slider resizes every doll live —
  nothing is baked. Place any number, anywhere; hider and hunter draw in different colours
  and are labelled with their height in metres.

  **They never ship.** The doll is drawn entirely as a gizmo — no mesh, renderer or
  collider — so it cannot render outside the editor by construction. On top of that the
  GameObject force-tags itself `EditorOnly`, so Unity's build pipeline strips it from the
  exported bundle, and **Validate Map errors** if that tag is ever missing. Even a doll
  that somehow survived all three would be an empty transform with an inert component.

  The starter map ships one of each, standing in its own side's spawn disc, in a new
  `_CoverUpMap/Reference` group.
  Add them to an existing map with **Cover Up! ▸ Maps ▸ Add Reference Dolls**. Because they
  never ship, they're exempt from the Base/Fixtures/Content rules — Validate won't complain
  about a doll loose under `Base`, and Group Base leaves them where you put them.

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
