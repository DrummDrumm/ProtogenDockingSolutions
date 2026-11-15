# In-game tests: Protogen Systems Docking Solutions

Use a fresh SDX2 creative test world (see the SDX2 test-world steps). You need a skiff with two connectors, a cockpit
and an LCD, and the Echo (or a station of yours) with a free `sdx_connectorDocking*` 50–150 m away. Enable
**Protogen Systems Docking Solutions** in Pulsar. The checks marked *(with Virtual Displays)* need that plugin enabled too.

## 1. Loads

- [ ] The game log has no `Docking Solutions:` errors. `%AppData%\SpaceEngineers\Storage\ProtogenDockingSolutions\DockingSolutions.ini` exists.
- [ ] `/dock help` opens a **Docking Solutions / Commands** window, one command per line. `/dock` says *Dock camera off. /dock on to start it, /dock help for the commands.* Neither goes to chat.

## 2. Dock camera

- [ ] `/dock on` in the skiff: the view moves to your connector's face, looking out. The REC / DOCK CAM frame, crosshair, brackets on the dock, corridor rings and bottom strip show, and a notification names the pair.
- [ ] WASD, Space/C and the mouse still fly the ship. The view is upright to the seat and doesn't sway.
- [ ] **Figures:** RANGE falls, CLOSING turns amber then red above SAFE, OFFSET reaches ON AXIS on the crosshair, and the advice line moves from TURN TO FACE through SLIDE ... to READY - LOCK.
- [ ] **Lock** (P): LOCKED shows, then the view returns to the cockpit after about 2.5 s.
- [ ] **V:** goes to the normal chase view ("Dock camera paused"). V again: back on the dock camera. `/dock off` or leaving the seat: the normal view.
- [ ] `/dock next` swaps to the other connector. `/dock target` (with two docks in range) swaps the dock.

- [ ] **Docking Assist view:** `/dock on`, then `/dock mode assist` (or **Dock Camera: camera view / Docking Assist (full screen)**). The full screen shows the Docking Assist with the live picture in its view box. `/dock mode cam` goes back.
- [ ] **Docking Assist style:** Holo Line is the default on the full screen, PiP and dock LCDs; `/dock style tactical`, `bracket`, `glass` and `radial` switch to the other looks, `/dock style classic` brings back the old look, `/dock style holo` returns. Try each on the full screen, the PiP and a square and a wide LCD. Check the lettering is readable on a small LCD and in the PiP.
- [ ] **Per-place styles:** `/dock style full tactical`, `/dock style pip glass`, `/dock style lcd radial` give each place its own look; `DockStyle=bracket` in one LCD's Custom Data overrides just that LCD; `/dock style <place> same` puts a place back on the main style.

## 3. Modded connectors, tags

- [ ] **Modded connectors:** put an `sdx_connectorDockingLarge1` on the skiff: it's picked as yours, and the Echo's SDX2 docking collar is found as the dock.
- [ ] **Ejector mode:** switch a connector to ejector mode: it's skipped.
- [ ] **Dock tag:** rename one connector `Nose [Dock]`: it's picked first.
- [ ] **Ignore tag:** rename the other `Aft [NoDock]`: `/dock next` never offers it.
- [ ] **Ignore tag after picking:** pick a connector with Next connector, then tag another (or that one) `[NoDock]`: the LCDs never show it again.
- [ ] **F / X leaves the dock camera:** full-screen dock camera (cam or assist): F / controller X turns it off, you stay seated. A camera block picked on the hotbar takes over and shows its own picture.
- [ ] **Ignored dock:** rename the station's dock `... [NoDock]`: it's not targeted.
- [ ] `/dock tag <Port>`: connectors with `<Port>` in their name are picked first. `/dock range 1000` finds docks further away. Check the .ini shows both changes.

## 4. Docking Assist LCD

- [ ] Set an LCD to **DOCK - Docking Assist**. It shows:
  - your connector > the dock, and the phase badge
  - the outline box, the dock face and the tunnel
  - the scope, the gauges and the next step

  All of it moves as you fly.
- [ ] An LCD that was already set to the Docking Assist before still shows it, without being picked again.
- [ ] **Live camera:** the view box shows the real dock camera picture with the assist drawn over it and lined up with the real dock. Colours look right, it moves smoothly, and it comes back after you leave (150 m) and return. `/dock lcdcam off` gives the drawn view only.
- [ ] The same on a cockpit screen and on an MCRN Corvette / UNN Truman crash couch screen (the wide layout).
- [ ] **Shared choice:** with the dock camera on, *Dock: next connector* changes the LCD's connector too. With the camera off, pick on the screens, then turn the camera on: it starts on that connector.

## 5. Camera spot

- [ ] `/dock offset 0 1.5`: the dock camera looks from 1.5 m higher. The brackets still sit on the dock, and RANGE / OFFSET don't change.
- [ ] `/dock next` to another connector: that one is on its face. Restart the game: the spot is still saved. `/dock reset`: back on the face.

## 6. Toolbar, controller, theme

- [ ] Drag the cockpit into the toolbar. These are listed and work:
  - **Dock Camera: on / off**
  - **Dock: next connector**
  - **Dock: next dock**

  A toolbar that had them before still works.
- [ ] With a gamepad, the slots work from the controller's toolbar.
- [ ] `/dock theme cyan`: the overlay and the LCDs turn cyan. `/dock theme amber` turns them back.

## 7. With Virtual Displays

- [ ] *(with Virtual Displays)* **Full-screen camera:** `/dock on`. The virtual screens and their feeds step aside; `/dock off`: they come back.
- [ ] *(with Virtual Displays)* **Virtual screens:** put *DOCK - Docking Assist* and *CAM - Dock camera* on two virtual screens.
  - The Docking Assist shows the live picture in its view box (*DOCKING ASSIST - LIVE*), with the tunnel lined up over the real dock.
  - The dock feed has the dock camera overlay, with brackets on the dock in the picture.
- [ ] *(Virtual Displays without this plugin)* Disable Docking Solutions and restart. The App list has no dock entries; a seat already set to one says *Docking Solutions plugin not loaded*. Everything else works.

## 8. API from a mod

- [ ] *(optional)* A test mod with `Api/DockingSolutionsApi.cs` gets its `Load` callback in a world. `Reading(cockpit)["Range"]` matches the overlay's RANGE, and `DrawAssist` into an LCD frame draws the same picture as the LCD script.

## 9. Picture-in-picture and quality (added 2026-11-13)

- [ ] The log has *PiP dock camera ready.* and no PiP errors.
- [ ] Drag the cockpit's **Dock Camera: picture-in-picture on / off** into the toolbar and use it (or `/dock pip`). A window in the top right shows your connector's view, looking out:
  - the picture moves with the ship, with no lag behind it at speed
  - the brackets sit on the real dock in the picture, and the readout strip matches the full-screen dock camera's
  - the slot shows PiP / Off
- [ ] **No connector:** on a ship without a free connector, the window says *NO FREE CONNECTOR*. Leave the seat: it goes.
- [ ] **Full-screen dock camera** (`/dock on`): the PiP goes while it's up and comes back after `/dock off`.
- [ ] **Hide the HUD** (the game's hide-HUD key): the PiP goes with it.
- [ ] **Editor:** `/dock pip mouse` (also `/dock pip edit`, or **Dock PiP: move / resize with the mouse**).
  - Drag the window to each quarter of the screen: it stays where it's dropped.
  - Drag the corner square and use the mouse wheel: it resizes. Shift + wheel changes its shape.
  - Right click: it jumps corner. R: back to the default.
  - Enter: saved (the .ini has the new `PipCorner`, `PipX`, `PipY`, `PipSize`, `PipAspect`). Open again, move it and press Esc: it goes back.
- [ ] **Docking Assist PiP:** `/dock pip mode assist` (or **Dock PiP: dock camera / Docking Assist**). The PiP shows the Docking Assist with the live picture in its view box; `/dock pip mode cam` goes back.
- [ ] **Auto PiP:** PiP off, `/dock on`, press V: the PiP comes up. V again: it goes and the full-screen dock camera returns. `/dock pip auto off` stops it.
- [ ] **Toolbar:** **Dock PiP: next corner** goes round the four corners.
- [ ] **Commands:** `/dock pip size 40`, `/dock pip corner bl`, `/dock pip pos 5 5`, `/dock pip aspect 4:3`, `/dock pip overlay off` (picture only, with a DOCK PIP label), `/dock pip reset`.
- [ ] **Quality:** `/dock quality performance`. The PiP is softer and choppier, and FPS is higher than with `/dock quality ultra` (check the game's FPS counter, PiP on, looking at a busy station). **Docking: next quality preset** steps Perf > Bal > Qual > Ultra. `/dock pip fps 20` shows *custom*.
- [ ] **Cost:** with `balanced` and the PiP on, FPS drops by no more than about a quarter against PiP off.
- [ ] *(with Virtual Displays)* PiP on and two camera feeds on the Virtual Displays screens at once: all three pictures are right, and nothing flickers.
- [ ] Look around the main view with the PiP on: no shadow, lighting or LOD flicker in the main view.
