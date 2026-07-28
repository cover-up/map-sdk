# Cover Up! Map SDK (`com.coverup.mapsdk`)

The authoring SDK for **Cover Up!** custom maps. Add it to a bare URP Unity project and you get
everything needed to build a map and export it as a Workshop package the shipping game loads:

- The `_CoverUpMap` authoring components — `MapConfig`, `MapSizeVariants`, `MapBoundsVolume`,
  `CamouflageSurface`, `MapSpawnDisc`, `WorkshopMapInfo` — plus the map-size + player-scale
  contract (`MapSize`, `GameScale`).
- Editor tooling under **Cover Up! → Maps**: *Create Example Sized Map*, *Validate Map*,
  *Preview Size*, *Show Bounds Volumes*, *Export Workshop Map*, *Auto-Export On Save*.

The map bundle's component scripts bind to the game's classes **by identical GUIDs**, which is the
whole reason this is a shared package: the game embeds it and every map project consumes it, so the
GUIDs match structurally.

## Requirements

- **Unity 6000.5** (matches the shipping game — see `package.json` `unity`).
- **URP** (`com.unity.render-pipelines.universal`), same major as the game.

## Install

**Fastest — the starter template repo.** The
[Cover Up! Map Project starter](https://github.com/cover-up/map-template) is a bare URP
project that already references this package. Use **"Use this template"** on GitHub (or clone it),
open it in Unity 6000.5, and you're ready to author — skip the manual setup below.

**Manual — add to your own URP project.** In `Packages/manifest.json`:

```json
"com.coverup.mapsdk": "https://github.com/cover-up/map-sdk.git#v0.5.4"
```

(Your project must be URP on Unity 6000.5. If you have no URP asset yet, create one via
Assets → Create → Rendering → URP Asset and assign it under Project Settings → Graphics.)

## Workflow

See **[Documentation~/MapCreatorGuide.md](Documentation~/MapCreatorGuide.md)** (bundled in this
repo) for the full author → Validate → Export → Sandbox-test → Publish loop. In short:
**Cover Up! → Maps → Create Example Sized Map**, edit it, **Validate Map**, **Export Workshop Map**.
Exports land in your Local maps folder — `~/CoverUpMaps/` on Linux/macOS, `Documents\CoverUpMaps\`
on Windows — which the installed game reads in **Sandbox** for live testing.
