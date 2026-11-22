namespace ProtogenDocking;

/// <summary>The settings window's Help tab: short instructions and explanations, as (heading, text) pairs.</summary>
internal static class DockHelp
{
    public static readonly (string Heading, string Text)[] Topics =
    {
        ("GETTING STARTED",
            "1. Sit in a cockpit on a ship with a connector.\n" +
            "2. Type /dock in chat, or put Dock Camera: on / off on the cockpit toolbar (G menu, search \"Dock\").\n" +
            "3. Your view moves to the connector's face. Fly the crosshair onto the dock and follow the line at the bottom: " +
            "TURN TO FACE, SLIDE RIGHT 1.2 M, SLOW DOWN, READY - LOCK.\n" +
            "4. Lock the connector as usual (P). The dock camera switches off by itself once locked."),

        ("DOCK CAMERA (FULL SCREEN)",
            "Your own first-person view, moved to your connector and looking out along it. You stay in the seat and keep flying.\n" +
            "- The crosshair is your connector's axis. Brackets mark the dock, rings mark its approach corridor.\n" +
            "- The strip at the bottom shows range, closing speed against a safe speed, offset, drift and alignment.\n" +
            "- V (third person) pauses it; first person brings it back. F leaves it and keeps you in the seat.\n" +
            "- Full screen shows: the camera view, or the Docking Assist with the live picture (/dock mode).\n" +
            "- /dock next picks your next connector, /dock target the next dock. /dock offset moves the camera spot."),

        ("PICTURE-IN-PICTURE (PIP)",
            "A small live dock camera window while you fly from the normal view (/dock pip, or the toolbar).\n" +
            "- Move / resize with the mouse: drag it, drag its corner or scroll to size it, Shift + scroll for its shape, " +
            "right click for the next corner, R to reset, Enter to save, Esc to cancel.\n" +
            "- Up by itself in 3rd person: shows while the full-screen dock camera is paused with V.\n" +
            "- Up by itself near a dock: pops up when your connector comes within that range of a dock, and goes once you're connected.\n" +
            "- The PiP only shows while a dock is in range (that range, else Look for docks within), however it was turned on.\n" +
            "- Views off after docking: once you've docked, the dock camera and the PiP switch off after that many seconds, or never (they stay up). " +
            "They stay on the docked pair, not moving on to your next free connector, until you undock. LCD feeds keep showing."),

        ("FLYING WHILE DOCKING",
            "\"Docking\" means the dock camera or PiP is up, a dock is in range, and you're not yet connected. " +
            "Steering and the dampener only work then: connect, turn the view off or move out of range and the seat's normal controls are back.\n" +
            "- Steer from the camera: your keys and mouse are relative to where the camera looks, so W flies towards what it shows. " +
            "Full screen and PiP each have their own toggle. Handy for connectors on the side or back of the ship. " +
            "Steer on / off key (set it here) and the toolbar action Dock Camera: steer from the camera switch it for the view that's up. " +
            "Switched on by hand, it switches itself off when docking ends.\n" +
            "- Steer from the camera: Auto: it comes on by itself as soon as a dock is in range with the view up, with a warning on screen, " +
            "and goes off when docking ends. Careful: your controls change without you pressing anything. The key switches it off for that dock. " +
            "Auto, full screen only: only the full-screen dock camera steers by itself, never the PiP.\n" +
            "- Dampener speed limit: a key you hold asks for a speed instead of full thrust, so the ship flies that way at up to " +
            "the limit (0.5-200 m/s), measured against the dock's ship or station. Only the keys you press are limited: let go " +
            "and the game's own dampeners stop the ship as usual (with them off, it drifts).\n" +
            "- Dampener acceleration: the hardest a key may speed the ship up or slow it down (0.1-50 m/s\u00b2). Lower is gentler.\n" +
            "- Dampener auto: the limits come down as the dock gets closer, to the safe speed the assist shows (CLOSING / SAFE): " +
            "10 m/s at 100 m, about 2 m/s at 20 m, 0.5 m/s at the end. Holding a key then can't take you into the red SLOW DOWN.\n" +
            "- Dampener pause after undocking: when a connector lets go, the dampener stays off for that many seconds, " +
            "or until you're out of range (only on the way in), so you can fly off at full thrust.\n" +
            "- The dampener works within the PiP's pop-up range (Up by itself near a dock) of a dock, with or without a view up."),

        ("SETTINGS PER SHIP",
            "Settings for (bottom left of the Settings tab) picks what you are changing:\n" +
            "- All ships (global): the settings every ship uses unless it has its own.\n" +
            "- This ship: the ship you sit in gets its own copy, used whenever you sit in a ship with that name. " +
            "Ships with the same name share it. The camera spot per connector stays shared.\n" +
            "Going back to global keeps the ship's own settings for later; pick This ship again to get them back. " +
            "Chat: /dock ship save | global."),

        ("DOCKING ASSIST ON SCREENS",
            "Pick DOCK - Docking Assist in any screen's Script list (LCDs, cockpit and seat screens). It shows the dock, " +
            "range, speed, alignment and next step, with the live camera picture if Live camera on LCDs is on. " +
            "An LCD's Custom Data line DockStyle=<style> gives that screen its own look."),

        ("WHICH CONNECTORS",
            "Any connector block works, modded ones included.\n" +
            "- Dock tag: a connector of yours with this in its name is picked first.\n" +
            "- Ignore tag: a connector with this in its name is never used.\n" +
            "- Look for docks within: how far away a dock may be to be picked."),

        ("QUALITY",
            "The preset trades picture for frame rate: the PiP's pictures per second and sharpness, the LCD camera, and how " +
            "often docks are looked for. The full-screen dock camera costs nothing extra at any preset. " +
            "Lower it if your frame rate drops with the PiP up."),

        ("SERVERS, TRAFFIC AND PAUSES",
            "- Everything runs on your PC. Steering and the dampener only change the move and turn input your seat already " +
            "sends, so nothing extra goes to the server and other players see nothing.\n" +
                        "- It pauses itself in admin mode (creative tools, admin menu, spectator) and when you're AFK for 30 s in " +
            "multiplayer, as some servers' rules require. Turn the dock camera back on yourself afterwards."),

        ("KEYS AND COMMANDS",
            "Ctrl+Alt+D: this window.   V: pause / resume the full-screen dock camera.   F: leave it.\n" +
            "/dock on | off   /dock pip   /dock pip mouse   /dock mode cam | assist   /dock next | target\n" +
            "/dock steer on | off   /dock pip steer on | off   /dock damp auto | speed <m/s> | accel <m/s2> | off\n" +
            "/dock ship save | global   /dock quality <preset>   /dock help (all commands)"),

        ("IF SOMETHING'S WRONG",
            "- NO FREE CONNECTOR: every connector of yours is connected, ignored or broken.\n" +
            "- No dock picked: fly closer (Look for docks within), or check the dock's connector isn't ignored by its name.\n" +
            "- The ship shakes back and forth with the dampener on: raise the acceleration limit a little, or lower the speed limit.\n" +
            "- Controls feel turned around: Steer from the camera is on for that view; switch it off there.\n" +
            "- The settings file is Storage\\ProtogenDockingSolutions\\DockingSolutions.ini in the game's AppData folder; " +
            "ships' own settings are in its Ships folder."),
    };
}
