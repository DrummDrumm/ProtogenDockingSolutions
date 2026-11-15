# Standalone test run: Protogen Systems Docking Solutions v1.1.0 (vanilla only)

This checklist tests the plugin **on its own**: no other plugins, no mods, vanilla blocks only.
The fuller list with the SDX2 and Protogen checks is `IN-GAME-TESTS.md`.

## Setup (once)

1. **Pulsar profile.** Start the game. In Pulsar's plugin list, open **Profiles** and load **Docking Solutions Vanilla Test**.
   It has only Docking Solutions: no other plugins and no client mods. Restart when Pulsar asks.
   *To go back afterwards, load your usual profile the same way. It's still saved as "Current".*
2. **Test save.** Load **Docking Solutions Test (vanilla)**. It's a Creative Empty World with no mods, planets or asteroids.
   It was made by `Tools/build-vanilla-test-world.py`; run that again any time for a fresh copy (the old one is kept, renamed).
   - **DOCK TEST Skiff** is about 25 m from where you spawn. It has a Cockpit, a **Nose Connector** (vanilla Connector),
     a **Starboard Connector** (vanilla Inset Connector), **LCD Dock** to the front left of the cockpit, batteries,
     gyros and thrusters on all six sides.
   - **DOCK TEST Station** is about 100 m beyond the skiff. Its docks face the skiff:
     - **Dock A** (Connector), 5 m left of the skiff's line
     - **Dock B** (Inset Connector), 5 m right of it
     - **Dock C [NoDock]** (Connector), above them, which should never be picked
3. **Check the load.** `%AppData%\SpaceEngineers\SpaceEngineers_*.log` (newest) should have these lines and no errors starting `Docking Solutions:`:
   - `Docking Solutions: PiP dock camera ready.`
   - Pulsar's *Enabled Plugins: Protogen Systems Docking Solutions* and nothing else
   - *Enabled Mods:* with nothing listed
4. Get in the skiff's cockpit. Its toolbar starts empty. You add the dock actions in section 6.

Tick each box as you go. If something fails, note what you did, what you saw, and the log lines from that time.

## 1. Loads and settings

- [ ] `%AppData%\SpaceEngineers\Storage\ProtogenDockingSolutions\DockingSolutions.ini` exists and has the `Quality` and `Pip*` lines.
- [ ] `/dock help` opens a **Docking Solutions / Commands** window listing the commands one per line, including `pip` and `quality`. Nothing goes to chat. `/dock pip help` does the same for the PiP settings.
- [ ] `/dock` (nothing after it) shows a notification: *Dock camera off. /dock on to start it, /dock help for the commands.*

## 2. Dock camera (full screen)

- [ ] `/dock on` in the skiff. The view moves to the **Nose Connector**'s face, looking out at the station. The REC / DOCK CAM frame, crosshair, brackets on a dock, corridor rings and the bottom strip all show. A notification names the pair (Nose Connector > Dock A or Dock B).
- [ ] WASD, Space/C and the mouse still fly the ship. The view stays upright to the seat and doesn't sway.
- [ ] Fly in. RANGE falls. CLOSING turns amber, then red when you're faster than SAFE. OFFSET reaches ON AXIS on the crosshair. The advice line moves from TURN TO FACE through SLIDE ... to READY - LOCK.
- [ ] **Lock** with P. LOCKED shows, and about 2.5 s later the view returns to the cockpit.
- [ ] Unlock (P) and back off. **V** goes to the normal chase view (*Dock camera paused*). V again returns to the dock camera.
- [ ] `/dock off` gives the normal view. Turn it on again and leave the seat: it turns off.
- [ ] `/dock next` swaps to the **Starboard Connector** (the inset one) and the view moves to the skiff's right side. `/dock next` again goes back to the nose.
- [ ] `/dock target` swaps between **Dock A** and **Dock B**. It never picks **Dock C [NoDock]**.

- [ ] **Docking Assist view:** with the full-screen dock camera on, use **Dock Camera: camera view / Docking Assist (full screen)** (or `/dock mode assist`). The screen shows the Docking Assist (*DOCKING ASSIST - LIVE*): the live picture in the view box with the tunnel and outline lined up on it, and the scope, gauges and next step beside it. It moves smoothly. Toggle again (or `/dock mode cam`): the normal dock camera view returns. V in assist mode: third person as usual; V again: back to the assist view.
- [ ] **Leave with F / X:** dock camera on (cam and assist modes): press F (controller X). *Dock camera off.*, the normal cockpit view, and you're still seated. F again leaves the seat as usual.
- [ ] **Camera block from the hotbar:** dock camera on (assist mode, full screen): pick a camera block on the hotbar. That camera's picture shows (no Docking Assist over it) and *Dock camera off (<camera name>).* F/X leaves the camera back to the cockpit's normal view.
- [ ] **Ignore tag after a manual pick:** use Next connector once, then rename one of the connectors `... [NoDock]`. Next connector never lands on it again; if it was the one in use, the screens move to another within a few seconds.
- [ ] **Docking Assist style:** the assist view uses **Holo Line** by default: thin lettering, the live picture filling most of the screen, and a slim rail on the right (offset scope, align/roll, range, closing speed) with the next step along the bottom. The PiP and dock LCDs show the picture full-bleed with readouts at the bottom. `/dock style next` (or **Docking Assist: next style**) cycles **Holo Line > Tactical Phosphor > Vector Bracket > Glass > Radial HUD > Classic**; the choice survives a restart. Tactical: green, a left rail with an offset grid, align/roll boxes and range/closing bar gauges (amber segment = safe speed); on the PiP/LCD the bars run down the left and the numbers sit in a box. Bracket: amber, five cut-corner panels along the bottom (next step, offset octagon, align/roll, range, closing with a white safe-speed marker); on the PiP/LCD one panel along the bottom, and on square LCDs the next step on an amber bar. Glass: the picture fills the screen in every view; a title card, the connector pair and a mint phase tag on top, the offset scope card bottom left, range and closing (thin bars, amber safe mark) bottom right, the next step on a pill in between. Radial: a ring round your connector's axis with the range filling the left arc and the closing speed the right (orange tick = safe speed), the roll as an orange marker on top, the offset as a dot inside; range and closing numbers beside the ring, offset/align/roll under it, the next step on a bar at the bottom. On square LCDs the ring sits a little high with the numbers under its sides.
- [ ] **Per-place styles:** `/dock style lcd radial`: the LCDs switch to Radial HUD, the PiP and full screen keep the main style. `/dock style pip glass`: only the PiP changes. `/dock style`: says the main style and lists PiP: Glass, LCDs: Radial HUD. Put `DockStyle=bracket` in one LCD's Custom Data: that LCD shows Vector Bracket within a second, the others stay Radial; delete the line and it goes back. `/dock style lcd same` and `/dock style pip same`: everything follows the main style again. Restart: the choices are kept.

## 3. Connectors and tags (vanilla)

- [ ] **Both connector types work:** the Inset Connector is picked as yours (Starboard) and as a dock (Dock B), the same as the plain Connector.
- [ ] **Ejector mode:** in the skiff's terminal, switch the Nose Connector to ejector (throw out). `/dock next` skips it. Switch it back.
- [ ] **Dock tag:** rename the Starboard Connector to `Starboard [Dock]`. Turn the dock camera off and on again: it's picked first. Rename it back.
- [ ] **Ignore tag:** rename the Nose Connector to `Nose [NoDock]`. `/dock next` never offers it. Rename it back.
- [ ] **Ignored dock:** Dock C is never targeted. Rename Dock A to `Dock A [NoDock]`: only Dock B is offered. Rename it back.
- [ ] `/dock tag Port`, then rename a connector to include `Port`: it's picked first. `/dock tag [Dock]` puts it back.
- [ ] `/dock range 50` with the skiff about 100 m away shows NO DOCK. `/dock range 300` finds them again. Both changes show in the .ini.

## 4. Docking Assist screens

- [ ] Set **LCD Dock** (terminal > LCD Dock > Content: Script > **DOCK - Docking Assist**). It shows:
  - Nose Connector > the dock, and a phase badge
  - the station's outline box, the dock face and the tunnel
  - the scope, the gauges and the next step

  All of it moves as you fly.
- [ ] Set one of the **cockpit's own screens** to the Docking Assist: the same picture, laid out for that screen.
- [ ] Move far away, or try `/dock range 20`: the screen shows NO DOCK. Put the range back.
- [ ] **Shared choice:** with the dock camera on, *Dock: next connector* changes the LCD's connector too. With the camera off, pick on the screens, then turn the camera on: it starts on that connector.
- [ ] Save, quit to the menu and load again. LCD Dock still shows the Docking Assist without being picked again.
- [ ] **Live camera:** LCD Dock's view box shows the real picture from the Nose Connector, looking out at the station, with the tunnel, outline and dock face drawn on top and lined up with the real dock.
  - colours look like the game's (not washed out or too dark), and there are no black or transparent holes
  - it moves smoothly as you fly (about 10 pictures a second at `balanced`)
  - the cockpit screen gets its own picture, shaped for that screen
  - walk more than 150 m away, or turn your back on the LCD, and come back: the picture returns
- [ ] **Screen rotation:** terminal > LCD Dock > **Rotation** 90°: the whole screen (picture and assist) turns with it, and the picture stays lined up with the assist. Back to 0°. A modded LCD (e.g. *Unarmored LCD Panel*) mounted any way round shows the picture upright to the cockpit at 0°; its Rotation slider turns it from there.
- [ ] `/dock lcdcam off`: the view box shows the drawn view only. `/dock lcdcam on` brings the picture back.

## 5. Camera spot

- [ ] `/dock offset 0 1.5`. The dock camera looks from 1.5 m higher. The brackets still sit on the dock, and RANGE / OFFSET don't change.
- [ ] `/dock next` to the Starboard Connector: it's still on its own face. Back to the nose: still raised.
- [ ] Save, restart the game and load: the spot is still saved. `/dock reset` puts it back on the face.

## 6. Toolbar and theme

- [ ] In the cockpit, open the toolbar config (G) and drag the **Cockpit** into slots. These are listed and work:
  - **Dock Camera: on / off** (slot text Dock / 3rd / Off)
  - **Dock: next connector** (Conn)
  - **Dock: next dock** (Tgt)
  - **Dock Camera: picture-in-picture on / off** (PiP / Off)
  - **Dock PiP: next corner** (TL / TR / BR / BL)
  - **Dock PiP: move / resize with the mouse** (Edit)
  - **Dock PiP: dock camera / Docking Assist** (Cam / Asst)
  - **Dock Camera: camera view / Docking Assist (full screen)** (Cam / Asst)
  - **Docking: next quality preset** (Perf / Bal / Qual / Ultra / Cust)
- [ ] *(if you have a gamepad)* The slots work from the controller's toolbar.
- [ ] `/dock theme cyan`: the dock camera overlay, the PiP and LCD Dock turn cyan. `/dock theme amber` turns them back.
- [ ] `/dock theme auto` without Protogen keeps the .ini's colours (amber).

## 7. Picture-in-picture

- [ ] **Dock Camera: picture-in-picture on / off** (or `/dock pip`). A window in the top right shows the Nose Connector's view of the station. Check that:
  - the picture moves with the ship, without lagging behind it at speed
  - the brackets sit on the real dock in the picture
  - the readout strip matches the full-screen dock camera's
- [ ] Fly a full approach and lock using only the PiP.
- [ ] `/dock next`: the PiP switches to the Starboard Connector's view.
- [ ] **Full-screen dock camera** (`/dock on`): the PiP disappears and comes back after `/dock off`.
- [ ] **Third person** (V, dock camera off): the PiP stays and still shows the connector's view.
- [ ] **Docking Assist mode:** **Dock PiP: dock camera / Docking Assist** (or `/dock pip mode assist`). The PiP shows the Docking Assist (title *DOCKING ASSIST - LIVE*): the live picture in its view box with the tunnel and outline on top and lined up, and the scope, gauges and next step beside or below it. Toggle back: the dock camera view returns.
- [ ] **Auto in third person:** `/dock pip off`, then `/dock on` (full screen). Press V: the PiP comes up by itself. Press V again: it goes and the full-screen dock camera is back. Works in both PiP modes. `/dock off` while in third person: the PiP goes.
- [ ] `/dock pip auto off`, then repeat: no PiP in third person. `/dock pip auto on` puts it back.
- [ ] **Hide HUD** (the game's hide-HUD key, usually Tab): the PiP hides with it.
- [ ] **Leave the seat**: the PiP goes. Sit back down: it returns.
- [ ] **Editor:** run **Dock PiP: move / resize with the mouse** (or `/dock pip mouse` / `/dock pip edit`).
  - Drag the window into each quarter of the screen: it stays where you drop it.
  - Drag the yellow corner square, and use the mouse wheel: it resizes.
  - Shift + wheel changes its shape.
  - Right click jumps it to the next corner. R resets it.
  - Enter saves. The .ini's `PipCorner`, `PipX`, `PipY`, `PipSize` and `PipAspect` change.
  - Open the editor again, move the window and press Esc: it goes back to where it was.
- [ ] **Toolbar:** **Dock PiP: next corner** goes round all four corners.
- [ ] **Commands:**
  - `/dock pip size 40`
  - `/dock pip corner bl`
  - `/dock pip pos 5 5`
  - `/dock pip aspect 4:3`
  - `/dock pip overlay off`: picture only, with a DOCK PIP label
  - `/dock pip overlay on`
  - `/dock pip reset`
  - `/dock pip status` describes it
- [ ] **No connector:** grind (or turn off) both skiff connectors. The PiP says *NO FREE CONNECTOR*. Rebuild them, or load the save again.
- [ ] Main view with the PiP on: no shadow, lighting or level-of-detail flicker as you look around.

## 8. Quality and performance

Use the game's FPS counter (F11 debug, or Shift+F11). Park facing the station, PiP on, and take each reading after about 10 s.

| Preset | FPS PiP off | FPS PiP on | PiP looks |
|---|---|---|---|
| `/dock quality performance` | | | |
| `/dock quality balanced` | | | |
| `/dock quality quality` | | | |
| `/dock quality ultra` | | | |

- [ ] Each preset gives a notification describing it. The .ini's `Quality`, `PipFps`, `PipRes`, `PipEffects`, `LcdEvery`, `LcdFps`, `LcdRes` and `ScanEvery` follow.
- [ ] Going up the presets, the PiP gets smoother and sharper. `quality` and `ultra` have softer lighting (SSAO) and glow (bloom).
- [ ] With `balanced`, the PiP costs about a quarter of your FPS or less.
- [ ] `performance`: LCD Dock updates visibly slower (3 a second) but still follows. Its camera picture is softer.
- [ ] `ultra`: LCD Dock's camera picture is sharp and smooth (30 a second). Note FPS with and without `/dock lcdcam off`: ______
- [ ] **Docking: next quality preset** steps Perf > Bal > Qual > Ultra > Perf.
- [ ] `/dock pip fps 20`: the slot and `/dock pip status` show *custom*. `/dock quality balanced` puts it back.
- [ ] Edit the .ini by hand (`Quality=custom`, `PipFps=10`) and save it while in game. It reloads, and the PiP gets choppier.

## 9. Server-free check

- [ ] Nothing in the log mentions sending anything. *(This is an offline world, so this only confirms there are no errors. Network silence was checked in the code: everything is local.)*

## Afterwards

- Load your usual Pulsar profile (**Current**) and restart.
- Settings changed during the tests stay in `DockingSolutions.ini`. `/dock pip reset`, `/dock quality balanced`, `/dock theme auto` and `/dock range 300` put the main ones back.
