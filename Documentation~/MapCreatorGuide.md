# Cover Up! — Map Creator Guide

*Author maps with the `com.coverup.mapsdk` package in a Unity project (the
[map-template](https://github.com/cover-up/map-template) starter is the quickest way),
then export a package the game loads. Third-party **publishing** to Steam Workshop is
still being finalized — that step is marked ⏳ below.*

Making a map is two things side by side:

1. **The Map SDK** — the `com.coverup.mapsdk` UPM package (the `_CoverUpMap`
   components + the Validate/Export tooling), added to a Unity project pinned to the
   game's Unity version, where you author your scene and **export** it to a map
   package. The quickest start is the **[map-template](https://github.com/cover-up/map-template)**
   starter — a bare URP project already referencing the package by git URL; open it in
   Unity 6000.5 and you're ready. Authoring needs the Unity editor; there is no in-game map editor.
2. **The game itself, in Sandbox mode** — where you *test* the exported package,
   live. You need the game installed; Sandbox is offline and single-player.

You never symlink anything or hand-edit paths. The two sides meet at **one folder**.

---

## The Local Maps folder

The game reads your in-progress maps from a single folder, and your SDK exports
into that same folder. Default location:

```
Windows:  Documents\CoverUpMaps\
Linux:    ~/Documents/CoverUpMaps/   (or ~/CoverUpMaps if you have no Documents)
macOS:    ~/Documents/CoverUpMaps/
```

Both the game and the SDK exporter default to this folder, so on a normal setup it
*just works* with zero configuration.

### Changing the folder (optional)

Map-making is a developer feature, so the override is a plain **config file**, not
an in-game setting: drop a `localmaps.txt` next to the game executable containing the
absolute path to your folder on a single line. The game reads that folder instead of
the default. Point the SDK exporter's output at the same path (see the SDK's
config). Example — reuse your SDK project's own output folder:

```
# <game install>/localmaps.txt
/home/you/CoverUpProject/Workshop
```

This is why there are no relative paths or symlinks: the game and the SDK are
independent installs that agree on one folder, wherever it lives.

---

## What a map package looks like

One subfolder per map. The game scans one level down for any folder containing a
`map.json`.

```
CoverUpMaps/
  cold_storage/                ← your map package (folder name = map id by default)
    map.json                   manifest (id, title, tags, per-platform hashes)
    map.win.bundle             the built map, Windows
    map.linux.bundle           the built map, Linux
    preview.png                thumbnail (Workshop + in-game browser)
  neon_alley/
    map.json
    ...
```

You never write these files by hand — **Export** produces them. `map.*.bundle` are
compiled Unity AssetBundles (one per platform); a shipped game can't read a raw
`.unity` scene, which is why the export/build step exists.

---

## The `_CoverUpMap` contract

Every map scene is built from the template's `_CoverUpMap` root:

```
_CoverUpMap                [MapConfig, MapSizeVariants, WorkshopMapInfo]
├── Base                    spawn disc + shared geometry (always present)
└── Sizes                   (optional) size variants
    ├── Small               bounds + doors for the small build
    ├── Medium
    └── Large
```

- **`MapConfig`** — player/doll scale for the map (a slider with a live "≈ X m tall"
  readout).
- **`MapSpawnDisc`** (in `Base`) — where players land. Required.
- **`MapSizeVariants`** (optional) — small/medium/large variants of the same map;
  smaller sizes *add* doors/boundaries. Bounds volumes live **only** inside the size
  roots, never in `Base`.
- **`WorkshopMapInfo`** — title, description, tags, preview image (read into
  `map.json` on export).
- A map must be a **pure environment**: no cameras, players, or input systems.

Run **Cover Up! → Maps → Validate Map** to check the contract before exporting.

---

## The workflow

1. Install the game.
2. Get the SDK project. Easiest: **Use this template** on the
   [map-template](https://github.com/cover-up/map-template) repo (a bare URP project
   referencing `com.coverup.mapsdk` by git URL) and open it in Unity 6000.5. Or add the
   package to your own URP project's `Packages/manifest.json`:
   `"com.coverup.mapsdk": "https://github.com/cover-up/map-sdk.git#v0.2.0"`.
   (By default both the game and the SDK use `~/Documents/CoverUpMaps` — no path
   setup needed. To relocate, see *Changing the folder* above.)
3. Build your map from the `_CoverUpMap` template. **Cover Up! → Maps → Create
   Example Sized Map** gives you a working reference to copy (it lands in your
   project's `Assets/`).
4. **Cover Up! → Maps → Validate Map**, then **Export Workshop Map** (or turn on
   auto-export-on-save).
5. In the game → **Sandbox** → pick your map from the list → you drop straight in,
   solo, no round. Move around, paint, check sight-lines.
6. Edit + export again; the game **hot-reloads** you into the updated map within a
   second. This is the core loop — keep the game running while you iterate in Unity.

### A note on camouflage balance

This is a hiding game, so **lighting and materials are gameplay**. Flat/unlit
surfaces make hiding trivial; high-frequency noise everywhere makes it impossible; a
stray rim light makes every hider pop. There is no automated check for this yet —
it's on you to keep the map fair. Test from a hider's eye, not just a flythrough.

---

## Publishing to Steam Workshop ⏳

Once your map plays well in Sandbox, publishing uploads the same package folder as a
Workshop item (bundles + `map.json` + `preview.png`). Subscribers download it and it
appears in the host's map rotation. This step is still being finalized.
