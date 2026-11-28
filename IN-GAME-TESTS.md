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
  - **Dock Camera: on / off** (slot shows Dock / 3rd / Off)
  - **Dock: next connector of yours**
  - **Dock: next dock to aim at**
  - **Dock Camera: camera view / Docking Assist** (slot shows Cam / Asst)
  - **Docking Assist: next style**
  - **Dock Camera: picture-in-picture on / off**, **Dock PiP: next corner**, **Dock PiP: dock camera / Docking Assist**,
    **Dock PiP: move / resize with the mouse** (tested in section 9)
  - **Docking: next quality preset**

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
- [ ] **Editor:** `/dock pip edit` (or **Dock PiP: move / resize with the mouse**).
  - Drag the window to each quarter of the screen: it stays where it's dropped.
  - Drag the corner square and use the mouse wheel: it resizes. Shift + wheel changes its shape.
  - Right click: it jumps corner. R: back to the default.
  - Enter: saved (the .ini has the new `PipCorner`, `PipX`, `PipY`, `PipSize`, `PipAspect`). Open again, move it and press Esc: it goes back.
- [ ] **Docking Assist PiP:** `/dock pip mode assist` (or **Dock PiP: dock camera / Docking Assist**). The PiP shows the Docking Assist with the live picture in its view box; `/dock pip mode cam` goes back.
- [ ] **Auto PiP:** PiP off, `/dock on`, press V: the PiP comes up. V again: it goes and the full-screen dock camera returns. `/dock pip auto off` stops it.
- [ ] **PiP near a dock:** PiP off, dock camera off. In the settings window (Ctrl+Alt+D) set *Up by itself near a dock* to 50 m. Fly towards a station connector: the PiP comes up at about 50 m and shows that dock. Back off past about 60 m: it goes. Fly in and connect: it goes once connected. Drag the slider all the way left (Off): it no longer comes up. `/dock pip near 30` turns it on at 30 m; `/dock pip status` shows "near a dock within 30 m".
- [ ] **PiP near a dock, several connectors:** on a ship with two or more connectors, one of them already connected (a small craft docked, say), fly another connector towards a station dock: the PiP comes up at the range and shows that connector and that dock. The slider goes up to 300 m.
- [ ] **Toolbar:** **Dock PiP: next corner** goes round the four corners.
- [ ] **Commands:** `/dock pip size 40`, `/dock pip corner bl`, `/dock pip pos 5 5`, `/dock pip aspect 4:3`, `/dock pip overlay off` (picture only, with a DOCK PIP label), `/dock pip reset`.
- [ ] **Quality:** `/dock quality performance`. The PiP is softer and choppier, and FPS is higher than with `/dock quality ultra` (check the game's FPS counter, PiP on, looking at a busy station). **Docking: next quality preset** steps Perf > Bal > Qual > Ultra. `/dock pip fps 20` shows *custom*.
- [ ] **Cost:** with `balanced` and the PiP on, FPS drops by no more than about a quarter against PiP off.
- [ ] *(with Virtual Displays)* PiP on and two camera feeds on the Virtual Displays screens at once: all three pictures are right, and nothing flickers.
- [ ] Look around the main view with the PiP on: no shadow, lighting or LOD flicker in the main view.

## 10. Admin and AFK pause, world reloads (added 2026-11-16, 1.2.1)

Set up: dock camera on (`/dock on`), PiP on, and an LCD on the Docking Assist.

- [ ] **Admin toggle:** Alt+F10, turn on any admin option (e.g. invulnerable). The log has *Docking Solutions: paused: admin mode*:
  - the dock camera stops, with *Dock camera off. Docking Solutions is paused (admin mode).*
  - the PiP goes
  - the LCD shows only *Docking Assist paused* and the reason, with no live picture
- [ ] **While paused:** `/dock on` and the toolbar slots only say *Docking Solutions is paused (...)*. Ctrl+Alt+D still opens the settings.
- [ ] **Back:** turn the admin option off. The log has *running again*, the LCD comes back by itself, and `/dock on` works.
- [ ] **Admin menu open, creative tools, spectator (F8):** each pauses it the same way, with its own reason.
- [ ] **AFK, multiplayer** (a hosted or server game): no key, mouse or controller input for 30 s pauses it (*paused: AFK*); moving the mouse resumes it. With a controller plugged in and left alone, it still pauses.
- [ ] **AFK, single player:** idle for over 30 s: the dock camera stays on and the log has no *paused: AFK*.
- [ ] **Second world:** load a world with Build Info, go back to the main menu (don't restart), load a vanilla world, then turn the dock camera on from the toolbar. No crash, and the slot's label updates.

## 11. Flying while docking, settings per ship (added 2026-11-22)
- [ ] **Steer, full screen:** turn on *Steer from the full-screen camera* in the settings window (Ctrl+Alt+D). Use a connector on the ship's side or back, and `/dock on`. W flies towards what the camera shows; A/D slide left and right in the picture; the mouse turns the ship about the camera's axes. Press V (third person): the seat's own controls come back. First person again: steering from the camera again.
- [ ] **Steer goes back to normal:** steering from the camera on and working. (a) Connect: the seat's normal controls straight away. (b) Undock, steer again, turn the view off (`/dock off` or PiP off): normal controls. (c) View on, fly back out past the range (the near-a-dock range, else Look for docks within): normal controls; fly back in: steering from the camera again. The steer toggles keep their setting throughout.
- [ ] **PiP only in range:** PiP switched on (`/dock pip`), near-a-dock at 50 m. 80 m from any dock: no PiP. Fly in: it shows at about 50 m. Back out past about 60 m: it goes. Connect: it goes. `/dock pip` says it shows while a dock is within 50 m.
- [ ] **Docked = views off:** a ship with two or more free connectors, at a station with several connectors. Dock camera on (and/or PiP on): connect. With *Views off after docking* at 3 s: the views stay on the docked pair for about 3 s, then "Docked - dock camera and PiP off" shows; neither view comes back on another connector, and the near-a-dock pop-up stays away. At *Straight away*: off within half a second. At *Never (keep up)*: both stay up on the docked pair. An LCD with the camera feed keeps showing in every case. Undock and fly off and back in: the pop-up works again.
- [ ] **Steer by hand switches off:** *Steer from the camera* set to *Both (this dock)*. Dock (or turn the view off, or fly out of range): a message says normal cockpit controls, and the setting shows *Off*. Next dock: steering only if you switch it on again (key / toolbar / settings).
- [ ] **Steer Auto:** set *Steer from the camera* to *Auto while docking*: the orange warning line shows under it. PiP near a dock at 50 m. Fly in: at about 50 m the PiP comes up and the message "DOCKING - CONTROLS NOW FOLLOW THE DOCK CAMERA" shows, and W flies the way the camera looks. Press the steer key: the seat's controls for this dock; again: back on. Connect: "Docking ended: normal cockpit controls". Undock and fly out and back in: Auto again with the warning. The toolbar action shows Auto, and Steer while it's steering.
- [ ] **Steer Auto, full screen only:** *Auto, full screen only*. PiP near a dock: it comes up but W still flies the seat's way, no warning. Full-screen dock camera on (`/dock on`) in range: the warning shows and W flies the way the camera looks. Third person (V, PiP takes over): the seat's controls.
- [ ] **Dampener comes back:** dampener on, *Dampener pause after undocking* at *Until out of range*, pop-up range 50 m. Undock: full thrust. Fly out past about 60 m, then back in: held back again inside 50 m, with or without the PiP up. Also with a timer (10 s): after 10 s, held back again while in range.
- [ ] **Steer, PiP:** full-screen steering off, *Steer from the PiP* on, PiP on: the same, relative to the PiP's camera. `/dock on` (full screen, its own toggle off): the seat's own controls.
- [ ] **Steer key:** in the settings window click *Steer on / off key*, press Ctrl+K: it shows Ctrl+K. Close the window. With the full-screen dock camera up, Ctrl+K flips full-screen steering only (the message says which); with only the PiP up, PiP steering only; with neither, both. K alone does nothing. Ctrl+K while chat or the terminal is open does nothing. Click the row again and press Backspace: None. Click it and press Esc: the key stays and the window stays open.
- [ ] **Steer toolbar action:** put *Dock Camera: steer from the camera on / off (the view that's up)* on the toolbar: it does the same as the key, and its label shows Steer / Seat for the view that's up.
- [ ] **Separate toggles:** `/dock steer off` leaves PiP steering on, and `/dock pip steer off` leaves full-screen steering on. `/dock pip status` shows "steer from it on|off".
- [ ] **Dampener:** set *Dampener speed limit* to 3 m/s and *Dampener acceleration* to 1 m/s². Pick a station connector, PiP on. Hold W: the speed climbs gently and stops near 3 m/s (check the HUD speed). Hold S: it slows gently and backs off at up to 3 m/s. Slide left and right: the same.
- [ ] **Dampener, let go:** game dampeners **on**: let go of W and the game's dampeners stop the ship at their usual full strength. Game dampeners **off**: let go and the ship keeps drifting at that speed.
- [ ] **Dampener, other axes:** game dampeners on, drifting sideways at 2 m/s: hold W. The sideways drift is stopped by the game's dampeners as usual while W is held to 3 m/s.
- [ ] **Dampener, already too fast:** fly at 30 m/s towards a dock, bring the PiP up and hold W (limit 3 m/s): the ship slows hard to about 3 m/s and holds it.
- [ ] **Dampener auto:** dampener on at 20 m/s and 5 m/s², *Dampener auto* on, a station dock about 150 m ahead, Docking Assist up. Hold W all the way in: the speed sits at about 10 m/s until 100 m, then drops with the range (about 2 m/s at 20 m, 0.5 m/s at the end). The CLOSING gauge stays green or amber, never red, and SLOW DOWN never shows. Auto off: back to 20 m/s.
- [ ] **Dampener pause after undocking:** dampener at 3 m/s, *Dampener pause after undocking* at 10 s, docked with the PiP or dock camera up. Undock: "Undocked: docking dampener off for 10 s"; hold W: full thrust for 10 s, then held back to 3 m/s again (if still in range). At *Until out of range*: full thrust until you're out past the range; fly back in: the dampener works again. At *No pause*: held back straight away.
- [ ] **Dampener auto, your limits win:** dampener at 3 m/s, Auto on: never faster than 3 m/s, even 150 m out.
- [ ] **Dampener, sliders:** the speed slider goes from Off to 200 m/s and the acceleration slider from 0.1 to 50 m/s², with fine steps at the low end.
- [ ] **Dampener, moving dock:** dock with a ship that is flying at a steady speed: the limits count against that ship, not space.
- [ ] **Dampener, gravity:** on a planet near a connector, game dampeners on: Space and C move the ship up and down at up to the limit, and it hovers when you let go.
- [ ] **Dampener, not docking:** PiP and dock camera off, or no dock in range: full thrust as normal. Once connected: no limits.
- [ ] **Dampener, smooth:** with a fusion drive ship (SDX2), hold W: no shaking back and forth at the limit. Note it if there is.
- [ ] **Help tab:** Ctrl+Alt+D, click **Help** (top left): the topics show in a scrolling box with orange headings, the text wraps inside the box, and the mouse wheel scrolls to IF SOMETHING'S WRONG at the end. **Settings** goes back. Close and reopen: it opens on the tab you left.
- [ ] **Per ship:** sit in a ship, open the settings window, set *Settings for* to *This ship*. Change the PiP corner. Close the window, sit in another ship: the global corner. Back in the first ship: its own corner. `/dock ship` says which settings are in use.
- [ ] **Back to global and back:** in the first ship pick *All ships (global)*: the global corner. Pick *This ship* again: its own corner comes back. In the Ships folder, the file goes from `.ini` to `.ini.off` and back.
- [ ] **Camera spot stays shared:** with *This ship* on, `/dock offset 1`. Switch the ship back to *All ships (global)*: the camera spot is still moved.

- [x] **No flashing from the PiP or LCD live picture (fixed 2026-11-28):** with a seat or LCD screen running *DOCK - Docking Assist* with its live picture (and with the PiP up), the whole view stays steady. Before the fix, every dock-camera render (about 3 a second on *performance*) let the older-style models (cockpit, screens, block details) switch level of detail for the far dock camera and dither-fade back on the main view, a halo that flashed and faded about every 0.4 s. Look around the cockpit and outside: nothing pulses.
