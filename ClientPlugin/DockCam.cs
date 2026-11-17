using System;
using System.Collections.Generic;
using VRage.Game.GUI.TextPanel;
using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.Graphics;
using Sandbox.ModAPI;
using VRage.Game.Utils;
using VRageMath;

namespace ProtogenDocking;

/// <summary>
/// The full-screen dock camera: the cockpit's own first-person view is moved to your connector's face, looking out along
/// its axis, with the video-feed overlay and docking guidance drawn on top. You stay in the seat, so you keep flying.
///
/// How: a postfix on MyCockpit's IMyCameraController.ControlCamera (called once a frame by MyGuiScreenGamePlay) sets the
/// main camera's view matrix again. Nothing extra is rendered and nothing is sent: it's your own view, on this PC, from a
/// spot on your own ship (what a camera block there would show).
///
/// Third person (V, or the controller's camera button) pauses it: the normal chase view and HUD come back; switch back
/// to first person and the dock camera is there again. Which connectors: DockTrack (shared with the Docking Assist screens).
/// </summary>
internal static class DockCam
{
    private const double LockedHold = 2.5;
    private static readonly Harmony harmony = new Harmony("ProtogenDockingSolutions.DockCam");
    private static bool patched;

    public static bool Active { get; private set; }
    /// <summary>On, but you're in third person: the view and overlay are the game's own until you go back to first person.</summary>
    public static bool Suspended { get; private set; }
    /// <summary>The connectors in use while on (screens on this ship show the same pair).</summary>
    public static DockTrack Track { get; private set; }
    private static long cockpitId;
    private static bool? wasFirstPerson;
    private static double lockedAt;
    private static DockReading reading;
    private static readonly DockScreen screen = new DockScreen();
    private static readonly SpriteListCanvas canvas = new SpriteListCanvas();
    private static readonly List<MySprite> sprites = new List<MySprite>();

    // ------------------------------------------------------------------ switching

    /// <summary>Off -> on; on -> off; on but paused in third person -> back to the dock camera view.</summary>
    public static string Toggle() => Active ? (Suspended ? Resume() : Stop("Dock camera off.")) : Start();

    /// <summary>Back from third person to the dock camera.</summary>
    public static string Resume()
    {
        if (!Active) return Start();
        if (!Suspended) return Describe();
        try { if (MyEntities.TryGetEntityById(cockpitId, out MyCockpit c) && !c.Closed) c.IsInFirstPersonView = true; } catch { }
        return ""; // Update announces it
    }

    public static string Start()
    {
        var cockpit = MySession.Static?.ControlledEntity as MyCockpit;
        if (cockpit == null || cockpit.CubeGrid == null) return "Sit in a cockpit to use the dock camera.";
        if (!Patch()) return "The dock camera couldn't hook the cockpit view (see the log).";
        var t = new DockTrack(cockpit.CubeGrid) { AutoPick = false };
        t.FindMine(true);
        if (t.MineList.Count == 0) return "No free connector on this ship for the dock camera.";
        // start from the pair the dock screens show (picked by hand or by them), when that connector is free
        var screens = DockTrack.For(cockpit.CubeGrid);
        int at = screens?.Mine != null ? t.MineList.IndexOf(screens.Mine) : -1;
        t.MineIndex = Math.Max(0, at);
        t.Mine = t.MineList[t.MineIndex];
        t.Scan();
        if (at >= 0 && screens.Target != null && t.Targets.Contains(screens.Target)) { t.Target = screens.Target; t.TargetIndex = t.Targets.IndexOf(t.Target); }
        Track = t;
        SyncScreens();
        cockpitId = cockpit.EntityId;
        try { wasFirstPerson = cockpit.IsInFirstPersonView; cockpit.IsInFirstPersonView = true; } catch { wasFirstPerson = null; }
        Active = true;
        Suspended = false;
        lockedAt = 0;
        return "Dock camera: " + Pair() + ". Third person (V) pauses it.";
    }

    public static string Stop(string why)
    {
        if (!Active) return why;
        Active = false;
        try
        {
            // back to third person only if you started there and are still looking through the dock camera
            if (wasFirstPerson == false && !Suspended && MyEntities.TryGetEntityById(cockpitId, out MyCockpit c) && !c.Closed) c.IsInFirstPersonView = false;
        }
        catch { }
        try { if (MySector.MainCamera != null) MySector.MainCamera.CameraSpring.Enabled = true; } catch { }
        Suspended = false;
        SyncScreens();
        wasFirstPerson = null; reading = null; cockpitId = 0; Track = null;
        return why;
    }

    /// <summary>Next connector of yours: the dock camera's while it's on, else the dock screens' (never switches the view).</summary>
    public static string NextMine()
    {
        if (!Active) return DockScreens.NextConnector();
        if (Track.MineList.Count < 2) return "Only one free connector on this ship.";
        Track.NextMine();
        SyncScreens();
        return "Dock camera: " + Pair();
    }

    /// <summary>Next connector to dock with: the dock camera's while it's on, else the dock screens' (never switches the view).</summary>
    public static string NextTarget()
    {
        if (!Active) return DockScreens.NextTarget();
        var t = Track;
        if (t.Targets.Count < 2) return t.Target == null ? "No dock in range." : "Only one dock in range: " + DockTrack.Name(t.Target) + ".";
        t.NextTarget();
        SyncScreens();
        return "Docking with " + DockTrack.Name(t.Target) + " on " + t.Target.CubeGrid.DisplayName + ".";
    }

    /// <summary>The screens keep the camera's pair: one choice for both, before, during and after the camera.</summary>
    private static void SyncScreens()
    {
        try { if (Track?.Grid != null) DockTrack.Own(Track.Grid)?.Follow(Track); }
        catch (Exception e) { Plugin.Log.Throttled("dock camera: sharing the pair with the screens failed: " + e.Message); }
    }

    /// <summary>
    /// /dock offset [forward [up right] | reset]: moves the dock camera's spot for the connector in use (the dock
    /// camera's while it's on, else the dock screens' and virtual screen feed's), saved for that connector. The guidance
    /// numbers still measure from the connector itself.
    /// </summary>
    public static string Offset(string[] args)
    {
        var t = Active ? Track : DockScreens.ScreensTrack();
        if (t == null) return "Sit in a cockpit first.";
        if (t.Mine == null) { t.FindMine(false); if (t.MineList.Count > 0) { t.MineIndex = 0; t.Mine = t.MineList[0]; } }
        var mine = t.Mine;
        if (mine == null) return "No connector on this ship.";
        string name = DockTrack.Name(mine);
        string usage = " /dock offset <forward> [<up> <right>] (metres) | reset";
        if (args.Length == 0) return "Dock camera on " + name + ": " + DockTrack.OffsetText(DockTrack.Offset(mine.EntityId)) + " of its face." + usage;
        if (args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            DockTrack.SetOffset(mine.EntityId, 0, 0, 0);
            return "Dock camera on " + name + ": back on its face.";
        }
        var n = new double[3];
        var cur = DockTrack.Offset(mine.EntityId);
        n[0] = cur.Forward; n[1] = cur.Up; n[2] = cur.Right;
        for (int i = 0; i < Math.Min(3, args.Length); i++)
            if (!double.TryParse(args[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out n[i]))
                return "Not a number: " + args[i] + "." + usage;
        DockTrack.SetOffset(mine.EntityId, n[0], n[1], n[2]);
        return "Dock camera on " + name + ": " + DockTrack.OffsetText(DockTrack.Offset(mine.EntityId)) + " of its face (saved for this connector).";
    }

    /// <summary>First person and third person in the seat (what V does), for a toolbar slot (gamepad).</summary>
    public static string SwitchView()
    {
        var cockpit = MySession.Static?.ControlledEntity as MyCockpit;
        if (cockpit == null) return "Sit in a cockpit first.";
        try { cockpit.IsInFirstPersonView = !cockpit.IsInFirstPersonView; } catch { }
        // the change is picked up (and announced) by Update
        return Active ? "" : cockpit.IsInFirstPersonView ? "First person" : "Third person";
    }

    private static string Pair()
    {
        var t = Track;
        return DockTrack.Name(t?.Mine) + (t?.Target != null ? " -> " + DockTrack.Name(t.Target) : " (no dock in range yet)");
    }

    public static string Describe() =>
        Active ? "Dock camera on" + (Suspended ? " (paused in third person)" : "") + ": " + Pair() + (Track?.Target != null ? " (" + Track.Target.CubeGrid.DisplayName + ")" : "") + ". " + DockGuide.Advice(reading)
               : "Dock camera off. /dock on to start it, /dock help for the commands.";

    // ------------------------------------------------------------------ every update

    public static void Update()
    {
        DockTrack.Prune();
        if (!Active) return;
        try
        {
            var cockpit = MySession.Static?.ControlledEntity as MyCockpit;
            if (cockpit == null || cockpit.EntityId != cockpitId || cockpit.Closed) { Say(Stop("Dock camera off (left the seat).")); return; }
            var mine = Track.Mine;
            if (mine == null || mine.Closed || !mine.IsFunctional) { Say(Stop("Dock camera off (its connector is gone).")); return; }
            // a camera block (hotbar), turret or other view took over: that view is yours, the dock camera steps aside
            if (OtherView())
            {
                string who = (MySession.Static.CameraController as Sandbox.Game.Entities.Cube.MyTerminalBlock)?.CustomName?.ToString();
                Suspended = true; // so Stop leaves the seat's first/third person alone
                Stop("");
                Say("Dock camera off" + (string.IsNullOrEmpty(who) ? "" : " (" + who + ")") + ".");
                return;
            }
            // third person (V) = look around in the normal view; first person again = back on the dock camera
            bool third = !cockpit.IsInFirstPersonView;
            if (third != Suspended)
            {
                Suspended = third;
                if (third) { try { if (MySector.MainCamera != null) MySector.MainCamera.CameraSpring.Enabled = true; } catch { } }
                Say(third ? "Dock camera paused (third person). First person to go back." : "Dock camera");
            }
            double now = DockClock.Now;
            Track.Tick();
            if (mine.Connected)
            {
                if (lockedAt == 0) lockedAt = now;
                else if (now - lockedAt > LockedHold) { Say(Stop("Docked - dock camera off.")); return; }
            }
            else lockedAt = 0;
        }
        catch (Exception e) { Plugin.Log.Throttled("dock camera update failed: " + e.Message); }
    }

    // ------------------------------------------------------------------ the view

    private static bool Frame(MyCockpit cockpit, out MatrixD world)
    {
        world = MatrixD.Identity;
        return Track != null && Track.Frame(cockpit.WorldMatrix.Up, cockpit.WorldMatrix.Forward, out world);
    }

    /// <summary>Called by the camera patch: the view matrix for this cockpit, while the dock camera is on and you're in first person.</summary>
    internal static bool TryView(MyCockpit cockpit, out MatrixD view)
    {
        view = MatrixD.Identity;
        if (!Active || cockpit == null || cockpit.EntityId != cockpitId) return false;
        try
        {
            if (!cockpit.IsInFirstPersonView) return false;
            if (!Frame(cockpit, out var world)) return false;
            view = MatrixD.Invert(world);
            return true;
        }
        catch { return false; }
    }

    private static bool Patch()
    {
        if (patched) return true;
        try
        {
            var m = AccessTools.Method(typeof(MyCockpit), "VRage.Game.ModAPI.Interfaces.IMyCameraController.ControlCamera");
            if (m == null) { Plugin.Log.Write("dock camera: MyCockpit.ControlCamera not found"); return false; }
            harmony.Patch(m, postfix: new HarmonyMethod(typeof(DockCam), nameof(ControlCameraPostfix)));
            // F / controller X (the game's Use) leaves the dock camera, as it leaves a camera block's view, instead of the seat
            var u = AccessTools.Method(typeof(MyCockpit), "VRage.Game.ModAPI.Interfaces.IMyCameraController.HandleUse");
            if (u != null) harmony.Patch(u, postfix: new HarmonyMethod(typeof(DockCam), nameof(HandleUsePostfix)));
            else Plugin.Log.Write("dock camera: MyCockpit.HandleUse not found (F won't leave the dock camera)");
            patched = true;
            return true;
        }
        catch (Exception e) { Plugin.Log.Write("dock camera patch failed: " + e); return false; }
    }

    private static void HandleUsePostfix(MyCockpit __instance, ref bool __result)
    {
        try
        {
            if (__result || !Showing || __instance == null || __instance.EntityId != cockpitId) return;
            __result = true; // handled: you stay in the seat
            try { Sandbox.Game.GUI.MyGuiAudio.PlaySound(VRage.Audio.MyGuiSounds.HudClick); } catch { }
            Say(Stop("Dock camera off."));
        }
        catch (Exception e) { Plugin.Log.Throttled("dock camera: leaving on Use failed: " + e.Message); }
    }

    /// <summary>Another view (a camera block picked on the hotbar, a turret, spectator) has taken over from the cockpit.</summary>
    private static bool OtherView()
    {
        try
        {
            var cc = MySession.Static?.CameraController as VRage.Game.Entity.MyEntity;
            return cc != null && cc.EntityId != cockpitId;
        }
        catch { return false; }
    }

    private static void ControlCameraPostfix(MyCockpit __instance, MyCamera currentCamera)
    {
        if (!Active || currentCamera == null || PlayGuard.Blocked) return;
        if (!TryView(__instance, out var view)) return;
        currentCamera.SetViewMatrix(view);
        // no seat sway or shake on a camera bolted to the hull
        currentCamera.CameraSpring.Enabled = false;
    }

    // ------------------------------------------------------------------ the overlay

    /// <summary>True while the dock camera's overlay owns the screen (other HUD plugins can ask the API and step aside).</summary>
    public static bool Showing => Active && !Suspended && !OtherView();

    public static void Draw()
    {
        if (!Showing) return;
        try
        {
            var cam = MySector.MainCamera;
            var cockpit = MySession.Static?.ControlledEntity as MyCockpit;
            var t = Track;
            if (cam == null || cockpit == null || t?.Mine == null) return;
            var full = MyGuiManager.GetFullscreenRectangle();
            float w = full.Width, h = full.Height;
            screen.Time = DockClock.GameNow;
            screen.Clear();
            if (!Frame(cockpit, out var meWorld)) return;
            reading = t.Read(meWorld);
            var target = t.Target;
            if (target != null && !target.Closed)
            {
                var tp = target.ConstraintPositionWorld();
                var ta = DockTrack.Axis(target);
                screen.Target = Project(cam, tp, w, h);
                if (screen.Target == null) screen.OffScreen = Direction(cam, tp);
                foreach (double d in DockGuide.CorridorMarks)
                {
                    if (d > reading.Range * 1.5 + 10) break;
                    var p = Project(cam, tp + ta * d, w, h);
                    if (p.HasValue) screen.Corridor.Add((p.Value.X, p.Value.Y, d));
                }
            }
            // Docking Assist mode: the PiP draws it across the screen (DockPip.Draw); the reading above still feeds /dock
            if (DockConfig.Current.CamMode == "assist") return;
            sprites.Clear();
            DockCamRenderer.Draw(canvas.Begin(sprites, Vector2.Zero), DockConfig.Theme, reading, screen, w, h);
            HudSprites.Draw(sprites, new Vector2(full.Left, full.Top));
        }
        catch (Exception e) { Plugin.Log.Throttled("dock camera draw failed: " + e.Message); }
    }

    /// <summary>World point to screen pixels with the camera as it is this frame, or null when behind or off screen.</summary>
    private static (float X, float Y)? Project(MyCamera cam, Vector3D p, float w, float h)
    {
        var m = cam.WorldMatrix;
        var d = p - cam.Position;
        double z = Vector3D.Dot(d, m.Forward);
        if (z < 0.05) return null;
        double x = Vector3D.Dot(d, m.Right), y = Vector3D.Dot(d, m.Up);
        double f = (h / 2) / Math.Tan(cam.FovWithZoom / 2);
        float sx = (float)(w / 2 + x / z * f), sy = (float)(h / 2 - y / z * f);
        if (sx < -50 || sy < -50 || sx > w + 50 || sy > h + 50) return null;
        return (sx, sy);
    }

    /// <summary>Which way the point lies on screen (x right, y down), for the edge arrow.</summary>
    private static (float X, float Y)? Direction(MyCamera cam, Vector3D p)
    {
        var m = cam.WorldMatrix;
        var d = p - cam.Position;
        double x = Vector3D.Dot(d, m.Right), y = Vector3D.Dot(d, m.Up);
        double l = Math.Sqrt(x * x + y * y);
        if (l < 1e-6) return (0, 1);
        return ((float)(x / l), (float)(-y / l));
    }

    internal static void Say(string text, int ms = 2000)
    {
        if (string.IsNullOrEmpty(text)) return;
        // Several lines (the help) go in the game's text window: a notification doesn't wrap and would run off the screen.
        if (text.IndexOf('\n') >= 0)
        {
            try { MyAPIGateway.Utilities?.ShowMissionScreen("Docking Solutions", "", "Commands", text, null, "Close"); return; } catch { }
        }
        try { MyAPIGateway.Utilities?.ShowNotification(text.Replace('\n', ' '), ms); } catch { }
    }

    public static void Reset()
    {
        Stop("");
        lockedAt = 0;
        DockTrack.Reset();
    }
}
