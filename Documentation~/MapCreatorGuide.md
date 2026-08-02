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
Linux:    ~/CoverUpMaps/            (NOT ~/Documents/CoverUpMaps)
macOS:    ~/CoverUpMaps/
```

Both the game and the SDK exporter default to this folder, so on a normal setup it
*just works* with zero configuration.

On Linux and macOS the folder sits directly in your home directory: the path comes from
`Environment.GetFolderPath(MyDocuments)`, which Unity's Mono resolves to `$HOME` there —
it ignores `XDG_DOCUMENTS_DIR`, so having a `~/Documents` changes nothing.

Because it follows `$HOME`, anything that **runs the game under a different HOME** —
a multi-instance test launcher, a sandbox, a service account — gets a different (empty)
folder and will show no local maps at all. Use the override below in that case.

### Changing the folder (optional)

Map-making is a developer feature, so the override is a plain **config file**, not
an in-game setting: drop a `localmaps.txt` next to the game executable containing the
absolute path to your folder on a single line. The game reads that folder instead of
the default. Point the SDK exporter's output at the same path (drop the same file next
to the SDK project). Example — pin both sides to one folder regardless of `$HOME`:

```
# <game install>/localmaps.txt
/home/you/CoverUpMaps
```

**Both sides need it.** The file is read next to whichever executable is running, so a
copy beside the game does not affect the SDK and vice versa. One side overridden and the
other on its default is the classic "my map exported fine but the game can't see it" —
the exporter writes to one folder and the game reads another.

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
├── Base                    everything shared across sizes (always present)
│   ├── Fixtures            spawn disc, the arena sun — the map breaks without these
│   └── Content             your geometry and props
├── Sizes                   (optional) size variants
│   ├── Small               bounds + doors for the small build
│   ├── Medium
│   └── Large
└── Reference               (optional) scale dolls — stripped at export, never ships
```

**The `Fixtures` / `Content` split is required**, not a suggestion: Validate Map errors if
either group is missing or if anything sits loose directly under `Base`, and the exporter
refuses a scene with errors. The reason is deletion safety — a handful of objects keep your
map loadable, and thousands are yours to gut and rebuild. Keeping them apart means "select
everything in `Content` and start over" is a safe move.

Building your map onto the example? Everything you add goes in `Content`. Migrating a scene
you made earlier (or one built flat, with no `_CoverUpMap` root at all)? Run
**Cover Up! → Maps → Group Base** once — it creates the groups and sorts what's already
there, moving nothing out of the scene and deleting nothing.

- **`MapConfig`** — player/doll scale for the map (a slider with a live "≈ X m tall"
  readout), plus **Hunter Camera** — see **The hunter's camera** below.
- **`MapSpawnDisc`** (in `Base/Fixtures`) — where players land. Required. Place more than
  one to give hiders and hunters separate spawns; see **Where each side lands** below.
- **`MapSizeVariants`** (optional) — small/medium/large variants of the same map;
  smaller sizes *add* doors/boundaries. Bounds volumes live **only** inside the size
  roots, never in `Base`. On a one-size map they belong in `Base/Fixtures`.
- **`WorkshopMapInfo`** — title, description, tags, preview image (read into
  `map.json` on export). See **Your preview image** below — it's the whole card players
  see, and you can't publish without one.
- A map must be a **pure environment**: no cameras, players, or input systems.

Run **Cover Up! → Maps → Validate Map** to check the contract before exporting.

### Where each side lands

One `MapSpawnDisc` set to **Both** drops everyone in the same place, and that is a complete,
valid map — most maps need nothing more.

Give the two sides their own discs when the *opening seconds* should differ: hiders deeper
in the space with a head start on cover, hunters held at an entrance so they arrive rather
than appear. Set each disc's **Role** to `Hiders` or `Hunters`; they draw green and orange
in the scene view so you can read a map's opening at a glance (a `Both` disc stays cyan).

- **A dedicated disc wins over a shared one.** Add a `Hunters` disc to a map that already
  has a `Both` disc and only the hunters move — hiders keep landing where they always did.
- **Several discs can share a role.** A player lands in a randomly chosen one, so scattering
  three small `Hiders` discs around a floor is a legitimate way to spread a spawn out
  instead of stacking everyone on one point.
- **Validate Map errors if either side has nowhere to land** — so a map with a lone
  `Hunters` disc and no hider spawn is caught before you export it, not in a live round.

#### Different spawns per size

A disc's **position in the hierarchy** decides which sizes it exists at:

| where you put it | live at |
|---|---|
| `Base/Fixtures` | every size — the shared default |
| `Sizes/Small` (or Medium/Large) | that size only |

So a map that wants one hider spawn at Small, two at Medium and three at Large puts one
disc under `Sizes/Small`, two under `Sizes/Medium`, three under `Sizes/Large`, and leaves
the hunter disc in `Base/Fixtures` where it serves all three. Nothing special switches this
on: only one size root is active at a time, and spawn lookup ignores inactive objects.

**Size roots are exclusive, not additive** — `Large` does *not* inherit `Medium`'s discs.
Each size root holds the complete set for that size. That's more discs in the scene, but
you can read a size's spawn count straight off the hierarchy instead of adding two numbers.

Validate Map checks **each built size on its own**: both roles must have a live disc there,
and every disc live at that size must sit inside *that size's* bounds. A shared disc in
`Fixtures` is live everywhere, so it is checked against every size — which is the old
"must be inside the smallest bounds" rule, now actually enforced and reported with the
name of the size that breaks.

### The hunter's camera

`MapConfig ▸ Hunter Camera` decides which camera the hunter plays your map in, because the
right answer is a property of the space: cramped corridors read better down the barrel,
open arenas need the peripheral vision of a boom camera.

- **Auto** (default) — the player's own toggle applies. Leave it here unless your map
  genuinely only works one way.
- **Third Person** / **First Person** — forced for this map; the toggle is disabled and its
  on-screen hint disappears while the map is loaded.

Hiders are always third person and are unaffected. The setting is read when the doors open
and released when players return to the island, so it never touches the lobby.

### Scale reference dolls

A map's player scale is a number in `MapConfig`, which tells you very little about whether
a hider fits behind that crate. So drop in a **reference doll**: a stand-in body at exactly
the height your map's `MapConfig` authors, that you place anywhere and as often as you
like — beside a doorway, on a shelf, at the far end of a sightline.

- **Cover Up! → Maps → Add Reference Dolls** adds a hider/hunter pair (the starter map
  ships one of each, standing in its own side's spawn disc). They live in
  `_CoverUpMap/Reference`.
- Each doll is one role, drawn in its own colour and labelled with its height in metres.
  Hider and hunter size **independently**, so place one of each — a map can pair
  mouse-sized hiders with a human-sized hunter, and that reads very differently in a room.
- Change a scale in `MapConfig` and every doll in the scene resizes immediately.

**Dolls never reach players.** They're drawn as scene-view gizmos — no mesh, no collider,
nothing that exists outside the editor — and their group is tagged `EditorOnly` so the
export strips it. Validate Map errors if that tag ever goes missing. Place them as freely
as you like: they're exempt from the `Base`/`Fixtures`/`Content` rules, and Group Base
leaves them alone.

### Mirrors

Any flat surface can be a real, live mirror: add a **`MapMirror`** component to a Quad (or
any flat mesh whose pivot sits on the glass). The reflective side is the face you see on a
Quad. Keep a normal opaque material on the mesh — polished metal reads well — because that
is the **fallback face**, and players will see it often:

- Only **one** mirror is live at a time — the nearest one within its **Activation
  Distance** (5–40 m, yours to tune) that the player is actually in front of. A live
  mirror re-renders the whole scene, so this cap is what keeps mirrors affordable; every
  other mirror shows its fallback material.
- Players can turn mirrors off in graphics settings, and older game versions don't know
  the component at all. In both cases your fallback face is the mirror, so make it look
  deliberate.
- The reflection skips shadows and runs at half resolution — fine on a wall, don't build
  a puzzle that depends on reading fine detail in it.

One gameplay note worth designing around: hunters can genuinely spot hiders in a mirror,
but the exposure system doesn't score reflections — a hider visible only in the glass
builds no exposure. A mirror is a tool for attentive hunters, not a scoring surface.

**Validate Map** asks whether you meant it if a scene has more than 4.

### Your preview image

The preview isn't a thumbnail any more — the game's map browser draws it **full-width as
the entire card**, with the title and stats laid over it. It's the only thing a player
sees of your map before deciding to download it, so it's worth a real screenshot rather
than an afterthought.

- **Required to publish.** No preview, no Workshop upload.
- **≈1280×720, 16:9.** Nothing is enforced — the exporter writes whatever texture you
  assign — but the card centre-crops to fill, so a square or ultra-wide image loses a lot
  of its height, and anything under 640 px wide looks soft blown up.
- **Under 1 MB as PNG**, 8-bit. That's Steam's limit, and a publish silently fails without it.
- Shoot it in-engine at a spot that reads as *your map* at a glance — the crop keeps the
  middle, so put the subject there.

**Validate Map** warns about all of the above before you get as far as publishing.

---

## The workflow

1. Install the game.
2. Get the SDK project. Easiest: **Use this template** on the
   [map-template](https://github.com/cover-up/map-template) repo (a bare URP project
   referencing `com.coverup.mapsdk` by git URL) and open it in Unity 6000.5. Or add the
   package to your own URP project's `Packages/manifest.json`:
   `"com.coverup.mapsdk": "https://github.com/cover-up/map-sdk.git#v0.7.0"`.
   (By default both the game and the SDK use `~/CoverUpMaps` (Linux/macOS) or
   `Documents\CoverUpMaps` (Windows) — no path setup needed. To relocate, see
   *Changing the folder* above.)
3. Build your map from the `_CoverUpMap` template. **Cover Up! → Maps → Create
   Example Sized Map** gives you a working reference to copy (it lands in your
   project's `Assets/`).
4. **Cover Up! → Maps → Validate Map**, then **Export Workshop Map** (or turn on
   auto-export-on-save).
5. In the game → **Sandbox** → pick your map from the list → you drop straight in,
   solo, no round. Move around, paint, check sight-lines.
6. Edit + export again; the game **hot-reloads** you into the updated map within a
   second. This is the core loop — keep the game running while you iterate in Unity.

### What a map is allowed to cost

Maps have a budget. **Validate Map** and **Export Workshop Map** check it, and the game
checks it again when it loads your map, so nothing gets past by being exported some
other way.

Two kinds of limit:

- **Limits** (✗) stop the export. Your map can't be published over one.
- **Guidelines** (•) are warnings. Nothing blocks; they're the point at which it's worth
  looking again at what you built.

| | limit | guideline |
|---|---|---|
| Bundle file, per platform | 1.5 GB | 300 MB |
| Asset memory | 3 GB | 1.5 GB |
| Realtime directional lights ("suns") | 1 | |
| Realtime point / spot lights | 64 | 24 |
| Audio sources | 64 | 16 |
| Combined volume of looping non-positional audio | 1.0 | |
| Non-kinematic rigidbodies | 128 | 32 |
| Particle systems / total max particles | 64 / 200,000 | 16 / 50,000 |
| Renderers | | 5,000 |
| Vertices | | 12,000,000 |
| Mesh collider vertices | | 500,000 |

**Cover Up! → Maps → Diagnostics → Map Budget Report** prints every one of these numbers
for the open scene next to its cap, whether or not anything is over. **Map Size Report**
in the same menu breaks the memory figure down by asset and by source pack, which is how
you find out that one texture is a third of your map.

Things worth knowing before you hit one of these:

- **Baked lights are free.** They're already in the lightmap and cost nothing at runtime,
  so they don't count against the light limits at all. Only realtime and mixed lights do.
- **A sized map is measured at its heaviest single size**, not the sum of Small, Medium
  and Large. Only one size root is ever live, so you're never charged for two at once.
- **The guidelines are generous.** For scale: the game's own hub island is 959 renderers,
  5.7M vertices and 587 MB of assets, and sits inside every guideline here.

### Audio and lights behave differently in game

Two things the game does to a map's audio and lighting that you should know about, so the
difference doesn't look like a bug:

- **Map audio plays under the player's Game Sounds volume.** Your `AudioSource` volumes
  are relative, not absolute. A player who has turned game sounds down has turned your
  ambience down too, which is what they asked for. Looping non-positional sources (the
  ones that play at the same volume everywhere) are additionally scaled as a group so
  they can't sum to more than full volume.
- **A map's lights can't strobe.** How fast a light may change brightness is capped for
  photosensitivity, always, not just for players who have asked for reduced flashing.
  Slow fades, dusk transitions and gentle flicker pass through exactly as you authored
  them — the cap only bites on large, fast, repeated changes. If you animate a light
  faster than about three flashes a second, players will see it slower than you do in the
  editor. Validate Map tells you when a light is animated.

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
