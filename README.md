# Protogen Systems Docking Solutions

A Space Engineers client plugin (Pulsar) that helps you dock: a full-screen **dock camera** on your connector's face
with docking guidance, the **DOCK - Docking Assist** LCD script for any screen, and an **API** that other plugins
and mods use to show the docking pictures and the dock camera feed on their own screens.

It works alone, and with other plugins through its API (see [Working with other plugins](#working-with-other-plugins)).

It also has a **picture-in-picture** dock camera window you can move and resize, and **quality presets** that trade
picture for frame rate.

Client-side only: nothing extra is sent to the server, and other players see nothing.

It switches itself off while you're in admin mode or AFK, as some servers' plugin rules require (SDX2's, for one).
Admin mode means creative tools, any admin-menu toggle, the admin menu being open, or the spectator camera. AFK means
no key, mouse or controller input for 30 s, and applies in multiplayer only. While it's off:
- the dock camera stops, and you turn it back on yourself;
- the picture-in-picture window is hidden;
- Docking Assist screens show only "Docking Assist paused", with no live picture;
- `/dock` commands, toolbar actions and API calls do nothing.

The settings window still opens.

## Install

Pulsar → Plugins → **Protogen Systems Docking Solutions** → enable, restart the game. For a local build, run
`Deploy-LocalDev.ps1` (with the game closed), then enable it under Pulsar's local plugins.

## Dock camera (full screen)

`/dock` (or the cockpit toolbar's **Dock Camera: on / off**) moves the cockpit's first-person view onto your
connector's face, looking straight out along its axis, with a video-feed look (scanlines, vignette, REC, timestamp)
and docking guidance on top. You stay in the seat, so you keep flying.

- **On screen:** the crosshair is your connector's axis. Brackets mark the dock (an arrow at the screen edge when it's
  out of view), and rings mark its approach corridor. The strip along the bottom gives range, closing speed against
  a safe speed for that range, offset, drift and alignment. The line above it says what to do next: TURN TO FACE,
  SLIDE RIGHT 1.2 M, SLOW DOWN, READY - LOCK, LOCKED.
- **Lock range** uses the game's own test: connection points within 0.59 m and faces within 45° of square.
- **Third person** (V, or the controller's camera button) pauses it and brings back the normal chase view. Go back to
  first person and the dock camera returns.
- **Leave it like a camera block's view:** F (Use), or X on a controller, turns the full-screen dock camera off and keeps you in the seat; press it again to leave the seat as usual. Picking a camera block on the hotbar (or any other view taking over) also turns it off, so that camera's picture shows.
- It switches off by itself 2.5 s after the connector locks, or when you leave the seat.
- How it works: a postfix on the cockpit's `ControlCamera` sets the camera from the connector. Nothing extra is rendered.

**Docking Assist view:** `/dock mode assist` (or the toolbar's **Dock Camera: camera view / Docking Assist**) makes the
full-screen dock camera show the Docking Assist across the screen, with the live camera picture in its view box and the
scope, gauges and next step beside it. `/dock mode cam` goes back to the camera view with its guidance. The assist view
renders the picture the way the PiP does, so it follows the PiP's quality preset (at least 720 px tall).

## Picture-in-picture dock camera

A small live window of the dock camera (your connector's face, looking out) while you fly from the normal view, with
the dock camera's guidance on top: crosshair, brackets on the dock, corridor rings and the readout strip. Turn it on
with the cockpit toolbar's **Dock Camera: picture-in-picture on / off** or `/dock pip`. It only shows while a free
connector of yours is within range of a dock (the *Up by itself near a dock* range if that's on, else *Look for docks
within*), and goes once you're connected or out of range; it comes back by itself when you're in range again.

- **Adjustable:** `/dock pip mouse` (or `/dock pip edit`, or the toolbar's **Dock PiP: move / resize with the mouse**) opens an editor over
  the live window. Drag it anywhere (it anchors to the nearest screen corner), drag the corner square or use the mouse
  wheel to size it, Shift + wheel to change its shape, right click for the next corner, R to reset. Enter saves, Esc
  cancels. The toolbar also has **Dock PiP: next corner**, and `/dock pip size`, `corner`,
  `pos`, `aspect` and `overlay` set it exactly.
- **Two modes:** `/dock pip mode cam | assist` (or the toolbar's **Dock PiP: dock camera / Docking Assist**). `cam`
  is the dock camera picture with its guidance; `assist` is the Docking Assist screen (scope, range and closing-speed
  gauges, next step) with the live picture in its view box.
- **Comes up by itself in third person:** while the full-screen dock camera is on and you press V for third person,
  the PiP appears (even if it's switched off); back to first person and it goes, as the full-screen view returns.
  `/dock pip auto off` stops that.
- **Pops up near a dock (optional):** set **Up by itself near a dock** in the settings window (Ctrl+Alt+D) to a range,
  or `/dock pip near 50`. While you sit in a cockpit and your connector comes within that many metres of the dock it
  would go to, the PiP appears; it goes again a little further out (so it doesn't flicker at the edge) and once you're
  connected. Off by default. Docks are only looked for within *Look for docks within* (300 m by default). It runs on
  your PC only: it reads positions your game already has and sends nothing.
- It hides while the full-screen dock camera is up (same view) and when the game HUD is hidden.
- **How it works:** after the game's own scene render, the plugin renders the scene a second time from the dock camera
  into a small texture and puts it in the window (the technique Target View uses). That's one extra scene render per
  PiP picture, so its rate and sharpness come from the quality preset. Flares, shadow-map and reflection-probe updates,
  offscreen LCD rendering and LOD changes are always skipped for it.

## Flying while docking

Both are off by default and run on your PC only: they change the move and rotate input your seat already sends,
so nothing extra goes to the server.

- **Steer from the camera:** while the dock camera's picture is up, your controls are relative to where it looks.
  W flies the way the camera faces, A/D and Space/C move left, right, up and down in its picture, and the mouse and Q/E
  turn the ship about the camera's own axes. Full screen and PiP have separate toggles: **Steer from the full-screen
  camera** (`/dock steer on`) and **Steer from the PiP** (`/dock pip steer on`). Both are also toolbar actions.
  It only applies while you're docking: once you're connected, the view is off, or the connectors are out of range
  again, you're back on the seat's normal controls, and the toggles switch themselves off (switch it on again next dock).
  The dampener works the same way, but keeps its setting.
- **Views off after docking:** once the connector you were docking with connects, the dock camera (or full-screen
  Docking Assist) and the PiP hide after a time (settings window, 0-60 s, default 3), or never (*Never (keep up)*:
  they stay up on the docked pair). They don't move on to another free connector. Once that connector lets go, the
  PiP comes back in range as before (switched on, or the near-a-dock pop-up). Camera feeds on LCDs aren't affected and keep showing.
- **Steer from the camera: Auto** (settings window *Steer from the camera* → *Auto while docking*, or `/dock steer auto`):
  steering from the camera comes on by itself as soon as a dock is in range with the dock camera or PiP up, with a
  warning on screen, and goes off when docking ends. **Your controls change without you pressing anything**, so only
  use it if you expect that. The steer key or toolbar action switches it off (and back on) for that dock;
  `/dock steer manual` goes back to switching it on by hand. *Auto, full screen only* (`/dock steer autofull`) does the
  same in the full-screen dock camera only; the PiP never steers.
  To flip it mid-dock, bind **Steer on / off key** in the settings window (click it, then press the key with any
  Ctrl / Alt / Shift; Backspace clears it), or put **Dock Camera: steer from the camera on / off (the view that's up)**
  on the toolbar. Both switch steering for the view that's up: full screen, else the PiP; with neither up, both
  together. The key isn't taken from the game, so pick one that does nothing else in a cockpit.
- **Docking dampener:** while you're docking (the dock camera or PiP is up, a dock is picked, and you're not yet
  connected), a key you hold sets a speed instead of full thrust: the ship flies that way at up to the **speed limit**
  (0.5-200 m/s), and speeds up or slows down no harder than the **acceleration limit** (0.1-50 m/s²). Speed is
  measured against the dock's ship or station, so a moving carrier counts as standing still, and a held up or down key
  allows for gravity. Only the keys you press are limited: the other axes, and everything once you let go, are left to
  the game's own dampeners (with them off, the ship drifts). If you're already going faster than the limit along a key
  you press, it slows to the limit with all the thrust there is. Set both in the settings window, or use
  `/dock damp speed 3` and `/dock damp accel 1.5` (`/dock damp off` turns it off).
- **Dampener auto:** the dampener's limits come down as the dock gets closer, to the safe closing speed the Docking
  Assist advises for that range (10 m/s at 100 m and beyond, about 2 m/s at 20 m, 0.5 m/s in the last few metres),
  and the acceleration limit with them (0.3 × that speed per second). Holding a key can then never take you into the
  assist's red SLOW DOWN (1.6 × the safe speed). It never goes above your own two limits. Setting **Dampener auto**,
  `/dock damp auto`, or the toolbar action.
- **Dampener pause after undocking:** when one of your connectors lets go, the dampener stays off for a time
  (0-120 s, default no pause) or *Until out of range*, so it only works on the way in and you can leave at full thrust.
  A message says so when you undock.
- **The dampener's range** is the PiP's pop-up range (*Up by itself near a dock*, 50 m by default): it works while a
  free connector of yours is that close to a dock, whether or not the dock camera or PiP is up.


## Settings per ship

The settings window's **Settings for** box (bottom left) picks what you are changing:
- **All ships (global)** changes the global settings.
- **This ship** gives the ship you're sitting in its own settings, starting from a copy of the global ones. They are
  used whenever you sit in a ship with that name, so ships with the same name share them. The dock camera's spot per
  connector stays shared.

Switching a ship back to global keeps its own settings for later: pick **This ship** again and they come back. The
chat commands are `/dock ship save` (this ship gets its own settings), `/dock ship global` (back to global) and
`/dock ship` (which settings are in use). The files are in `Storage\ProtogenDockingSolutions\Ships\<ship name>.ini`;
a ship that is back on global has its file renamed to `.ini.off`.

## Quality and performance presets

`/dock quality performance | balanced | quality | ultra` (or the toolbar's **Docking: next quality preset**):

| Preset | PiP pictures / s | PiP sharpness (max height) | PiP SSAO + bloom | Docking Assist screens | LCD camera (pictures / s, max height) | Looking for docks |
|---|---|---|---|---|---|---|
| `performance` | 15 | 270 px | off | 3 / s | 3 / s, 180 px | every 3 s |
| `balanced` (default) | 30 | 450 px | off | 6 / s | 10 / s, 270 px | every 1.5 s |
| `quality` | 45 | 720 px | on | 6 / s | 20 / s, 360 px | every 1.5 s |
| `ultra` | 60 | 1080 px | on | 6 / s | 30 / s, 512 px | every 1 s |

`/dock pip fps <5-60>`, `/dock pip res <180-1080>` and `/dock pip effects on|off` set one value and switch to `custom`.
The full-screen dock camera costs nothing extra at any preset: it moves your own view rather than rendering another.

## Docking Assist on screens

Pick **DOCK - Docking Assist** in any screen's Script list: LCDs, and cockpit, seat and modded screens (the SDX2
crash couches included). The script id keeps its old `ScriptHud:dock.view` name, so screens already set to it keep working.

The view box shows the **live dock camera picture** (from your connector's face, looking out), with the assist drawn
over it:
- the dock's ship as an outline box
- the dock's face
- an approach tunnel of square gates at 2–150 m, with your connector's square for rolling square to it
- an offset scope
- range and closing-speed gauges
- a phase badge (APPROACH, TURN, LINE UP, FINAL, LOCK READY, LOCKED, TOO FAST, OVERSHOT, NO DOCK)
- the next step

Wide screens put the gauges beside the view; square and tall ones put them below it.

**The live picture** is rendered by your own game, like the PiP, into a small texture the screen shows under the
assist. Only screens within 150 m of you and in view get one, and one picture is rendered per frame, so several screens
share the rate. It needs the PiP's renderer hooks; if they fail after a game update, screens fall back to the drawn view.
`/dock lcdcam off` turns it off (the drawn view only, no rendering cost). The quality preset sets its rate and
sharpness (table above); above 6 pictures a second the plugin redraws the screen between the game's own updates.

## Which connectors: works with any connector block

Docking uses every block the game treats as a connector (`MyShipConnector`): vanilla, SDX2's
`sdx_connectorDocking*`, and any modded connector. Connectors in ejector mode are skipped. There are no subtype lists
to keep up to date.

- **Yours:** a connector with the **dock tag** (`[Dock]`) in its name is picked first. Otherwise it's one that's
  connected or can connect, then the free one nearest a dock. Mechanical subgrids (rotors, pistons, hinges) count as
  the ship.
- **The dock:** the connector yours can connect to. Otherwise it's the nearest working, free, same-size connector
  within range (300 m) that isn't an enemy's, preferring ones facing you.
- **Leave one out:** put the **ignore tag** (`[NoDock]`) in a connector's name and it's never used, as yours or as a dock.
- `/dock tag <text>`, `/dock ignore <text>` and `/dock range <m>` change these.

Pick by hand with **Dock: next connector of yours** and **Dock: next dock to aim at**, from the toolbar or with
`/dock next` and `/dock target`. That choice is shared by the dock camera and the screens, and it never switches your view.

## Commands

| Command | What it does |
|---|---|
| `/dock` | what the dock camera is doing |
| `/dock on` / `off` | the full-screen dock camera |
| `/dock next` / `target` | your next connector / the next dock |
| `/dock view` | first / third person (as V) |
| `/dock offset <forward> [<up> <right>]` | moves the dock camera's spot for the connector in use (metres), saved per connector; `/dock reset` puts it back on the face |
| `/dock mode cam \| assist \| next` | the full-screen dock camera shows the camera view, or the Docking Assist with the live picture |
| `/dock style holo \| tactical \| bracket \| glass \| radial \| classic \| next` | the Docking Assist's look: **Holo Line** (default; cyan, thin lines, slim side rail), **Tactical Phosphor** (green, boxed readouts and segmented bar gauges on a left rail), **Vector Bracket** (amber, cut-corner panels along the bottom, octagon scope), **Glass** (the picture fills every screen, readouts on dark see-through cards, mint accents), **Radial HUD** (a ring reticle over the full-screen picture: range fills the left arc, closing speed the right one, roll marker on top, offset dot inside) or **Classic** (the original layout). Each has a full-screen layout (big live picture) and a PiP/LCD layout (picture full-bleed, readouts over it). Toolbar: **Docking Assist: next style** |
| `/dock style full \| pip \| lcd <style> \| next \| same` | a look for one place only: the full-screen dock camera, the PiP or the LCDs; `same` makes it follow the main style again. `/dock style` on its own lists the main style and any place that has its own. A single LCD can have its own look too: put a line `DockStyle=radial` (any style name) in its **Custom Data**; that beats the LCD setting |
| `/dock pip` | the picture-in-picture dock camera on / off; `/dock pip help` lists its settings |
| `/dock pip mouse` (or `edit`, `window`) | move and resize the PiP window with the mouse |
| `/dock pip mode cam \| assist \| next` | the PiP shows the dock camera, or the Docking Assist with the live picture |
| `/dock pip auto on \| off` | the PiP comes up by itself while the full-screen dock camera is in third person |
| `/dock pip near on \| off \| <metres>` | the PiP pops up by itself when your connector comes within that range of a dock |
| `/dock pip size <10-80>`, `bigger`, `smaller`, `corner tl\|tr\|bl\|br\|next`, `pos <x%> <y%>`, `aspect 16:9\|4:3\|1:1\|<w/h>`, `overlay on\|off`, `reset` | the PiP window: height (% of the screen), corner, margins from it, shape, guidance on top |
| `/dock steer on \| off` | move and rotate relative to the full-screen dock camera while it's up |
| `/dock pip steer on \| off` | move and rotate relative to the PiP's camera while it's up |
| `/dock damp on \| off \| speed <m/s> \| accel <m/s2>` | the docking dampener and its limits |
| `/dock ship [save \| global]` | this ship's own settings, or the global ones |
| `/dock lcdcam on \| off` | the live dock camera picture on Docking Assist LCDs |
| `/dock quality performance \| balanced \| quality \| ultra \| next` | the quality preset (above) |
| `/dock pip fps`, `res`, `effects` | one quality value by hand (`custom`) |
| `/dock theme amber \| cyan \| green \| white \| auto \| file` | colours; `auto` follows a theme another plugin hands over |
| `/dock tag`, `/dock ignore`, `/dock range` | see above |

Toolbar actions on every cockpit and control block keep their old ids, so existing toolbar slots
keep working:
- **Dock Camera: on / off**
- **Dock: next connector**
- **Dock: next dock**

New with the PiP:
- **Dock Camera: picture-in-picture on / off** (shows PiP / Off)
- **Dock PiP: next corner** (shows TL, TR, BR, BL)
- **Dock PiP: move / resize with the mouse**
- **Dock PiP: dock camera / Docking Assist** (shows Cam or Asst)
- **Dock Camera: camera view / Docking Assist (full screen)** (shows Cam or Asst)
- **Docking: next quality preset** (shows Perf, Bal, Qual, Ultra or Cust)

## Settings

**Settings window:** Pulsar's **Config** button for the plugin, or **Ctrl+Alt+D** in game, opens a window with the
dock camera's and the PiP's view and style, the PiP's corner, size and shape, the quality preset, the LCD camera,
colours, the dock and ignore tags, and the search range. Changes apply at once and are saved when the window closes;
**Defaults** puts them all back (the dock camera's spot per connector is kept). The **Help** tab at the top left has
brief instructions and explanations for every feature. Everything is also in the file:

`%AppData%\SpaceEngineers\Storage\ProtogenDockingSolutions\DockingSolutions.ini`. Saving the file reloads it in game.
A ship with its own settings uses `Ships\<ship name>.ini` in that folder instead (see *Settings per ship*). Both files have the same keys.

| Key | Default | |
|---|---|---|
| `DockCamOffsets` | | `connectorId:forward:up:right`, space-separated |
| `DockTag` | `[Dock]` | picked first as yours |
| `IgnoreTag` | `[NoDock]` | never used |
| `TargetRange` | `300` | how far to look for docks, 20–5000 m |
| `ThemeSource` | `auto` | `auto` = another plugin's theme when one is handed over; `file` = the colours below |
| `ThemeMain`, `ThemeDim`, `ThemeGood`, `ThemeWarn`, `ThemeBad`, `ThemeText`, `ThemeBackground` | amber | `r,g,b[,a]` or `#rrggbb` |
| `ThemeFont`, `ThemeNumberFont` | `White`, `Monospace` | |
| `Quality` | `balanced` | `performance`, `balanced`, `quality`, `ultra` (sets the five values below on load) or `custom` (uses them as written) |
| `PipFps`, `PipRes`, `PipEffects` | `30`, `450`, `off` | PiP pictures a second (5-60), max picture height (180-1080), SSAO + bloom |
| `LcdEvery` | `1` | Docking Assist screens redraw every n-th run (runs are 6 a second) |
| `LcdFeed` | `on` | the live dock camera picture in Docking Assist screens' view box |
| `LcdFps`, `LcdRes` | `10`, `270` | its pictures a second (1-30) and max height (120-1024) |
| `ScanEvery` | `1.5` | seconds between looking for docks |
| `PipOn` | `off` | the PiP |
| `PipCorner`, `PipX`, `PipY` | `tr`, `1.5`, `12` | corner, and margins from it in % of the screen's width / height |
| `PipSize`, `PipAspect` | `28`, `1.778` | height in % of the screen's height (10-80), width / height (0.5-3) |
| `PipOverlay` | `on` | the dock camera's guidance over the PiP |
| `PipMode` | `cam` | `cam` = dock camera with guidance; `assist` = the Docking Assist with the live picture in its view box |
| `PipAuto` | `on` | the PiP comes up by itself while the full-screen dock camera is in third person |
| `PipNear` | `off` | the PiP pops up by itself while your connector is within `PipNearRange` of a dock |
| `DockedOff` | `3` | seconds after docking before the dock camera and PiP switch off (0-60), or `never` to keep them up; LCD feeds stay |
| `PipNearRange` | `50` | that range in metres (5-300), from any free connector of yours |
| `SteerFull` | `off` | move and rotate relative to the full-screen dock camera while it's up |
| `SteerPip` | `off` | move and rotate relative to the PiP's camera while it's up |
| `SteerAuto` | `off` | steering from the camera comes on by itself as soon as a dock is in range (with a warning) and goes off when docking ends |
| `SteerAutoPip` | `on` | with `SteerAuto`, the PiP steers by itself too (`off` = only the full-screen dock camera) |
| `SteerKey` | *(none)* | a key that switches steering for the view that's up, e.g. `Ctrl+Alt+K` (key names as in the game's MyKeys: `K`, `F6`, `NumPad5`...) |
| `Damp` | `off` | docking dampener: while docking, the keys you press ask for a speed and acceleration is limited; letting go is left to the game |
| `DampSpeed` | `5` | its speed limit in m/s (0.5-200), relative to the dock's ship or station |
| `DampAccel` | `2` | its acceleration limit in m/s² (0.1-50) |
| `DampUndock` | `0` | seconds the dampener stays off after a connector lets go (0-120), or `range` = until out of range |
| `DampAuto` | `off` | the limits come down near the dock, to the assist's safe speed for the range |
| `CamMode` | `cam` | the full-screen dock camera: `cam` = camera view with guidance; `assist` = the Docking Assist with the live picture |
| `AssistStyle` | `holo` | the Docking Assist's look: `holo`, `tactical`, `bracket`, `glass`, `radial` or `classic` |
| `FullStyle`, `PipStyle`, `LcdStyle` | (empty) | a look for the full-screen dock camera / the PiP / the LCDs only; empty = `AssistStyle`. An LCD's Custom Data line `DockStyle=<style>` beats `LcdStyle` for that LCD |

## API for other plugins and mods

Copy [`Api/DockingSolutionsApi.cs`](Api/DockingSolutionsApi.cs) into your mod or plugin. It uses only types on the mod
whitelist, so a client-side mod script can use it as well as a plugin. It talks over the game's mod messages (channel
`0x50524F544F444F43`) and never reaches the server.

```csharp
var dock = new ProtogenDocking.Api.DockingSolutionsApi();
dock.Load(() => MyLog.Default.WriteLine("Docking Solutions ready"));   // LoadData / BeforeStart
...
// the figures, for your own HUD
var r = dock.Reading(seat);               // Range, Closing, OffRight, OffUp, Misalign, Advice, Locked, ...
// the Docking Assist as LCD sprites, into any LCD frame (or your own sprite drawer)
var sprites = new List<MySprite>();
dock.DrawAssist(seat, new Vector4(0, 0, 512, 512), false, myScreenState ?? (myScreenState = dock.NewScreen()), sprites, surface);
frame.AddRange(sprites);
// a live dock camera feed: render a camera at this spot, with this field of view, then lay the overlay on it
var f = dock.CameraFrame(seat);           // Item1 ok, Item2 world matrix, Item3 the connector's grid
float fov = dock.LensFovY(w, h);
dock.DrawOverlay(seat, new Vector4(x, y, w, h), myOverlayState, sprites);
...
dock.Unload();                            // UnloadData
```

| Call | |
|---|---|
| `IsReady`, `Version` | the plugin answered |
| `Active()`, `Showing()` | the full-screen dock camera is on / is filling the screen (HUD plugins can step aside) |
| `Command("on")` | runs a `/dock` command and returns its message |
| `Pair(grid)` | the connector in use and the dock it's lined up with |
| `Reading(block)` | the docking figures, as a dictionary |
| `CameraFrame(block)` | the dock camera's world matrix for a screen or seat on that ship |
| `LensFovY(w, h)` | the field of view to render a feed with, so the overlays line up |
| `AssistViewBox(w, h)` | where the Docking Assist's view box is on a w × h screen, for putting a live feed in it |
| `NewScreen()` | the per-screen drawing state to pass back each draw |
| `DrawAssist(...)`, `DrawOverlay(...)` | the pictures as LCD sprites; pass the surface to measure text exactly, or none for HUD pixels |
| `SetTheme(dict)` | hand over your colours (`Main`… as `Color`, `Vector4` or `"r,g,b"`, plus `Font`, `NumberFont`); `null` = back to the player's |
| `GetOffset(connector)`, `SetOffset(connector, v)` | the dock camera's spot per connector (X forward, Y up, Z right) |

Load order doesn't matter: the client asks on `Load` and the plugin answers then, and it also announces itself when
the world loads. New entries may be added; `Version` changes only for breaking changes.

## Working with other plugins

**Protogen Systems - Virtual Displays** uses this API for its *DOCK - Docking Assist* and *CAM - Dock camera* screens,
and steps its screens aside while the full-screen dock camera is up. Other plugins can use it to hand over a theme
(`/dock theme auto`).

## Building

```
dotnet build ClientPlugin/ClientPlugin.csproj -c Release -f net10.0
dotnet run --project Tests -c Release
```

The PiP needs the renderer's internal types: `VRage.Render` and `VRage.Render11` are publicized at build time
(Krafs.Publicizer), and SharpDX is referenced from the game's Bin64.

`Shared/` holds the docking maths, drawing, PiP layout and presets, free of game types, so the tests compile it too. The tests write
preview pictures to `Tests/`.

## After a game update: auto-rebuild

The auto-rebuild script is `Protogen Systems\Plugin-Legacy\Tools\Update-AfterGameUpdate.ps1` (see its README,
"After a game update"). It rebuilds and redeploys both plugins when Steam updates the game, and first checks that
everything this plugin patches still exists in the new game DLLs. That list is `Protogen Systems\Plugin-Legacy\Tools\HookCheck\hooks.txt`:
add to it when adding a patch here.

## Releasing

How to release an update to PluginHub, and the `Release-PluginHub.ps1` script that does it: [RELEASING.md](RELEASING.md).

## Disclaimer

Protogen Systems is an unofficial fan project. I made these plugins for my own personal use on the Sigma Draconis
Expanse 2 server; they are free, and anyone is welcome to use and enjoy them. The name is a nod to *The Expanse*.
This project is not affiliated with, endorsed by or sponsored by the creators or rights holders of *The Expanse*,
Keen Software House (Space Engineers), or the Sigma Draconis server. All trademarks belong to their respective owners.
