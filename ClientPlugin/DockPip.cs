using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using SharpDX.DXGI;
using VRage.Game.GUI.TextPanel;
using VRage.Generics;
using VRage.Render.Scene;
using VRage.Render11.Common;
using VRage.Render11.LightingStage.EnvironmentProbe;
using VRage.Render11.Render;
using VRage.Render11.Resources;
using VRage.Render11.Resources.Internal;
using VRageMath;
using VRageRender;
using VRageRender.Messages;

namespace ProtogenDocking;

/// <summary>
/// The picture-in-picture dock camera: a small live window of your connector's view (the full-screen dock camera's spot
/// and lens) with its guidance on top, while you fly from the normal view. Toggled from the cockpit toolbar or /dock pip.
///
/// How: the game renders its 3D scene once a frame, for the main camera only. So, straight after the main scene (a postfix
/// on MyRender11.DrawGameScene, on the render thread), the camera and resolution are swapped to the dock camera's, the
/// game's untouched DrawGameScene (a Harmony reverse patch) runs into a texture of ours, everything is put back, and the
/// picture is stretched into the PiP window on the frame. The overlay's sprites are drawn after the scene, so they go on
/// top. The quality preset sets how often (PipFps) and how sharp (PipRes) it renders and whether SSAO and bloom stay on;
/// flares, shadow-map and environment-probe updates, offscreen LCD rendering and LOD changes are always skipped.
/// All on this PC: nothing is sent, and it shows only what your own client already has.
/// </summary>
internal static class DockPip
{
    /// <summary>Picture heights: a few sizes, so the renderer's texture pools don't churn as the window resizes.</summary>
    private static readonly int[] Heights = { 180, 270, 360, 450, 540, 720, 900, 1080 };
    private static readonly int[] SizeSteps = { 18, 24, 28, 34, 42, 52 };

    /// <summary>The PiP editor's layout while it's open (shown live, saved on Save).</summary>
    public static DockSettings Preview;
    private static DockSettings Layout => Preview ?? DockConfig.Current;

    private static readonly DockScreen screen = new DockScreen();
    private static readonly SpriteListCanvas canvas = new SpriteListCanvas();
    private static readonly List<MySprite> sprites = new List<MySprite>();

    /// <summary>The PiP window on screen this frame (GUI pixels, absolute), for the editor; empty when not drawn.</summary>
    public static RectangleF Shown { get; private set; }

    // ------------------------------------------------------------------ switching and commands

    public static bool On => DockConfig.Current.PipOn;

    public static string Toggle() => Set(!On);

    public static string Set(bool on)
    {
        DockConfig.Change(s => s.PipOn = on);
        if (!on) { Clear(); return "Dock PiP off."; }
        if (failed != null) return failed;
        return "Dock PiP on (" + DockPipLayout.CornerName(Layout.PipCorner) + ", " + DockConfig.Current.Quality + "): it shows while a dock is within " +
               Range.ToString("0", CultureInfo.InvariantCulture) + " m of a free connector of yours. /dock pip edit to move or resize it.";
    }

    public const string Usage =
        "/dock pip [on | off | status] | reset\n" +
        "/dock pip mouse (or edit / window): move and resize the PiP window with the mouse\n" +
        "/dock pip size <10-80> | bigger | smaller\n" +
        "/dock pip corner tl | tr | bl | br | next | pos <x%> <y%>\n" +
        "/dock pip aspect 16:9 | 4:3 | 1:1 | <w/h> | overlay on | off\n" +
        "/dock pip mode cam | assist | next (dock camera / Docking Assist)\n" +
        "/dock pip auto on | off (PiP while the dock camera is in 3rd person)\n" +
        "/dock pip near on | off | <metres> (PiP pops up when your connector comes that close to a dock)\n" +
        "/dock pip steer on | off (move and rotate relative to the PiP's camera)\n" +
        "/dock pip fps <5-60> | res <180-1080> | effects on | off";

    public static string Command(string[] args)
    {
        if (args.Length == 0) return Toggle();
        string a = args[0].ToLowerInvariant();
        string b = args.Length > 1 ? args[1].ToLowerInvariant() : "";
        var s = DockConfig.Current;
        switch (a)
        {
            case "on": return Set(true);
            case "off": return Set(false);
            case "toggle": return Toggle();
            case "status": return Describe();
            case "size":
                if (b == "bigger" || b == "+") return Bigger(1);
                if (b == "smaller" || b == "-") return Bigger(-1);
                if (!float.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out float size)) return "PiP size: " + s.PipSize.ToString("0.#", CultureInfo.InvariantCulture) + "% of the screen's height. /dock pip size <10-80>";
                size = DockPipLayout.Clamp(size, DockPipLayout.MinSize, DockPipLayout.MaxSize);
                DockConfig.Change(x => x.PipSize = size);
                return "PiP size: " + size.ToString("0.#", CultureInfo.InvariantCulture) + "% of the screen's height.";
            case "bigger": case "+": return Bigger(1);
            case "smaller": case "-": return Bigger(-1);
            case "corner":
                string c = b == "next" || b == "" ? DockPipLayout.NextCorner(s.PipCorner) : DockPipLayout.NormCorner(b);
                if (c == null) return "Use /dock pip corner tl | tr | bl | br | next.";
                DockConfig.Change(x => x.PipCorner = c);
                return "PiP: " + DockPipLayout.CornerName(c) + ".";
            case "pos": case "move":
                if (args.Length < 3 || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float px)
                                    || !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float py))
                    return "PiP: " + s.PipX.ToString("0.#", CultureInfo.InvariantCulture) + "% / " + s.PipY.ToString("0.#", CultureInfo.InvariantCulture) + "% in from the " + DockPipLayout.CornerName(s.PipCorner) + " corner. /dock pip pos <x%> <y%>";
                px = DockPipLayout.Clamp(px, 0, 100); py = DockPipLayout.Clamp(py, 0, 100);
                DockConfig.Change(x => { x.PipX = px; x.PipY = py; });
                return "PiP: " + px.ToString("0.#", CultureInfo.InvariantCulture) + "% / " + py.ToString("0.#", CultureInfo.InvariantCulture) + "% in from the " + DockPipLayout.CornerName(s.PipCorner) + " corner.";
            case "aspect": case "shape":
                if (!DockPipLayout.TryAspect(b, out float asp)) return "PiP shape: " + DockPipLayout.AspectText(s.PipAspect) + ". /dock pip aspect 16:9 | 4:3 | 1:1 | 21:9 | <width/height>";
                DockConfig.Change(x => x.PipAspect = asp);
                return "PiP shape: " + DockPipLayout.AspectText(asp) + ".";
            case "overlay":
                bool ov = DockSettings.TryBool(b, out var o) ? o : !s.PipOverlay;
                DockConfig.Change(x => x.PipOverlay = ov);
                return "PiP guidance overlay " + (ov ? "on." : "off (picture only).");
            case "mode":
            {
                string m = b == "" || b == "next" ? NextMode(s.PipMode) : DockSettings.NormPipMode(b);
                if (m == null) return "PiP shows: " + ModeName(s.PipMode) + ". /dock pip mode cam | assist | next";
                DockConfig.Change(x => x.PipMode = m);
                return "PiP shows " + ModeName(m) + ".";
            }
            case "assist": DockConfig.Change(x => x.PipMode = "assist"); return "PiP shows " + ModeName("assist") + ".";
            case "cam": case "camera": DockConfig.Change(x => x.PipMode = "cam"); return "PiP shows " + ModeName("cam") + ".";
            case "auto":
                bool au = DockSettings.TryBool(b, out var ab) ? ab : !s.PipAuto;
                DockConfig.Change(x => x.PipAuto = au);
                return au ? "The PiP comes up by itself while the dock camera is in third person." : "The PiP no longer comes up by itself in third person.";
            case "steer": return DockCam.Steer(true, b == "" ? null : b);
            case "near": case "proximity":
            {
                if (float.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out float m))
                {
                    m = DockPipLayout.Clamp(m, DockSettings.NearMin, DockSettings.NearMax);
                    DockConfig.Change(x => { x.PipNear = true; x.PipNearRange = m; });
                }
                else
                {
                    bool nr = DockSettings.TryBool(b, out var nb) ? nb : !s.PipNear;
                    DockConfig.Change(x => x.PipNear = nr);
                }
                return NearText();
            }
            case "edit": case "adjust": case "mouse": case "window": case "drag": case "resize": return Edit();
            case "reset":
                DockConfig.Change(x => x.ResetPipLayout());
                return "PiP back in the top right corner at its usual size.";
            case "fps":
                if (!int.TryParse(b, out int fps)) return "PiP: " + s.PipFps + " pictures a second. /dock pip fps <5-60>";
                fps = Math.Max(5, Math.Min(60, fps));
                DockConfig.Change(x => { x.PipFps = fps; x.Quality = DockQuality.Custom; });
                return "PiP: " + fps + " pictures a second (quality: custom).";
            case "res":
                if (!int.TryParse(b, out int res)) return "PiP picture: up to " + s.PipRes + " px tall. /dock pip res <180-1080>";
                res = Math.Max(180, Math.Min(1080, res));
                DockConfig.Change(x => { x.PipRes = res; x.Quality = DockQuality.Custom; });
                return "PiP picture: up to " + res + " px tall (quality: custom).";
            case "effects":
                bool fx = DockSettings.TryBool(b, out var f) ? f : !s.PipEffects;
                DockConfig.Change(x => { x.PipEffects = fx; x.Quality = DockQuality.Custom; });
                return "PiP SSAO and bloom " + (fx ? "on" : "off") + " (quality: custom).";
            case "help": case "?": return Usage;
            default: return "Unknown: /dock pip " + args[0] + ". /dock pip help lists its settings.";
        }
    }

    public static string NextMode(string m) => m == "assist" ? "cam" : "assist";

    public static string ModeName(string m) => m == "assist" ? "the Docking Assist (live picture in its view box)" : "the dock camera with its guidance";

    /// <summary>The cockpit toolbar's mode switch.</summary>
    public static string ToggleMode() => Command(new[] { "mode", "next" });

    /// <summary>The full-screen dock camera in Docking Assist mode: drawn here, across the whole screen.</summary>
    public static bool FullAssist => DockCam.Showing && DockConfig.Current.CamMode == "assist";

    /// <summary>The full-screen dock camera's view: the camera with its guidance, or the Docking Assist (toolbar, /dock mode).</summary>
    public static string CamMode(string to)
    {
        var s = DockConfig.Current;
        string m = to == "" || to == "next" ? NextMode(s.CamMode) : DockSettings.NormPipMode(to);
        if (m == null) return "Dock camera shows: " + ModeName(s.CamMode) + ". /dock mode cam | assist | next";
        DockConfig.Change(x => x.CamMode = m);
        return "Dock camera shows " + ModeName(m) + ".";
    }

    /// <summary>Shown this frame: switched on, by itself while the full-screen dock camera is paused in third person, or
    /// by itself while your connector is near a dock; in every case only while a dock is in range (InRange).</summary>
    public static bool Wanted => InRange && (On || (DockConfig.Current.PipAuto && DockCam.Active && DockCam.Suspended) || Near);

    /// <summary>Your connector is within the pop-up range of a dock (PipNear), updated at the start of each Draw.</summary>
    public static bool Near { get; private set; }

    /// <summary>
    /// A free connector of yours is within range of a dock: the pop-up range with Up by itself near a dock on, else
    /// Look for docks within. The PiP, steering from the camera and the dampener only work while it is.
    /// </summary>
    public static bool InRange { get; private set; }

    /// <summary>A free connector of yours is within the pop-up range (PipNearRange) of a dock: the dampener works.</summary>
    public static bool DampInRange { get; private set; }
    /// <summary>That pair, for the dampener (auto limits by its range, speeds against the dock's grid).</summary>
    public static MyShipConnector DampMine { get; private set; }
    public static MyShipConnector DampDock { get; private set; }

    /// <summary>The range InRange uses, in metres.</summary>
    public static double Range => DockConfig.Current.PipNear ? DockConfig.Current.PipNearRange : DockConfig.Current.TargetRange;

    public static string NearText()
    {
        var s = DockConfig.Current;
        return s.PipNear
            ? "The PiP pops up when your connector is within " + s.PipNearRange.ToString("0", CultureInfo.InvariantCulture) + " m of a dock" +
              (s.PipNearRange > s.TargetRange ? " (docks are only looked for within " + s.TargetRange.ToString("0", CultureInfo.InvariantCulture) + " m)." : ".")
            : "The PiP no longer pops up near a dock.";
    }

    /// <summary>
    /// In range and the pop-up: any free connector of yours (the one picked as yours may be another, or already
    /// connected) closer to a dock than the range. It goes out of range a little further out than it came in, so it
    /// doesn't flicker at the edge, and once you're connected (no free connector near a dock).
    /// </summary>
    private static void UpdateNear(MyCockpit cockpit)
    {
        var s = DockConfig.Current;
        if (cockpit == null || !Needed(s)) { InRange = Near = DampInRange = false; watch.Clear(); dockedOn = null; return; }
        double now = DockClock.Now;
        if (now < nextNear) return; // looked at twice a second
        nextNear = now + 0.5;
        var t = DockTrack.For(cockpit.CubeGrid);
        if (t == null) { InRange = Near = DampInRange = false; watch.Clear(); return; }
        Released(t, now);
        // docked: a connector you were docking with has connected
        if (dockedOn == null)
            foreach (var c in watch)
                if (!c.Closed && c.Connected) { dockedOn = c; dockedAt = now; dockedDone = false; break; }
        watch.Clear();
        if (dockedOn != null)
        {
            if (!dockedOn.Closed && dockedOn.Connected)
            {
                // after Views off after docking the dock camera and PiP switch off; until then (or with never) they stay
                // on the docked pair. Either way they don't move on to the next free connector until it lets go.
                float off = s.DockedOff;
                if (!dockedDone && off >= 0 && now - dockedAt >= off) { Docked(); dockedDone = true; }
                if (dockedDone) InRange = Near = false;
                DampInRange = false;
                return;
            }
            dockedOn = null;
            dockedDone = false;
            InRange = Near = false;
        }
        double range = Range, dampRange = s.PipNearRange;
        double inR = InRange ? range + Math.Max(5, range * 0.2) : range, dampR = DampInRange ? dampRange + Math.Max(5, dampRange * 0.2) : dampRange;
        t.ClosestFree(Math.Max(inR, dampR), out var mine, out var dock, out double dist);
        InRange = mine != null && dist <= inR;
        // the dampener: within the pop-up range (Up by itself near a dock), whether or not a view is up
        DampInRange = mine != null && dist <= dampR;
        DampMine = DampInRange ? mine : null;
        DampDock = DampInRange ? dock : null;
        if (!DampInRange) releasedInRange = false; // Until out of range: works again on the next way in
        Near = InRange && s.PipNear && !s.PipOn;
        // show that pair
        if (InRange) t.Use(mine, dock);
        // the free connectors you're docking with: when one of them connects, you've docked
        if (InRange || DockCam.Active)
            foreach (var c in new[] { mine, t.Mine, DockCam.Track?.Mine })
                if (c != null && !c.Connected && !watch.Contains(c)) watch.Add(c);
    }

    private static double nextNear;

    /// <summary>Something uses the range check: the PiP (on, near a dock, or auto in 3rd person), the dock camera or the dampener.
    /// With all of them off, no scan for docks runs and the picked pair stays as it was.</summary>
    private static bool Needed(DockSettings s) => s.PipOn || s.PipNear || s.Damp || DockCam.Active;

    // ------------------------------------------------------------------ undocking: the dampener's pause

    private static List<MyShipConnector> connectedWas = new List<MyShipConnector>(), connectedNow = new List<MyShipConnector>();
    private static double releasedAt = double.MinValue;
    private static bool releasedInRange;

    /// <summary>One of your connectors let go (and still exists): you've undocked.</summary>
    private static void Released(DockTrack t, double now)
    {
        t.ConnectedNow(connectedNow);
        bool released = false;
        foreach (var c in connectedWas)
            if (!c.Closed && c.IsFunctional && !c.Connected) released = true;
        (connectedWas, connectedNow) = (connectedNow, connectedWas);
        if (!released) return;
        releasedAt = now;
        releasedInRange = true;
        var s = DockConfig.Current;
        if (s.Damp && s.DampUndock != 0)
            DockCam.Say(s.DampUndock < 0 ? "Undocked: docking dampener off until you're out of range."
                                         : "Undocked: docking dampener off for " + s.DampUndock.ToString("0", CultureInfo.InvariantCulture) + " s.", 2500);
    }

    /// <summary>The dampener is paused after undocking (DampUndock): for that time, or until out of range.</summary>
    public static bool DampPaused
    {
        get
        {
            float p = DockConfig.Current.DampUndock;
            if (p == 0) return false;
            if (p > 0) return DockClock.Now - releasedAt < p;
            return releasedInRange; // cleared once out of the pop-up range (UpdateNear)
        }
    }
    private static readonly List<MyShipConnector> watch = new List<MyShipConnector>();
    private static MyShipConnector dockedOn;
    private static double dockedAt;
    private static bool dockedDone;

    /// <summary>Docked and the time is up: the dock camera (or full-screen Docking Assist) and the PiP off.</summary>
    private static void Docked()
    {
        bool any = DockCam.Active || Shown.Width > 0;
        if (DockCam.Active) DockCam.Stop("");
        Clear(); // the PiP stays switched on as it was: hidden while docked, back in range after undocking
        if (any) DockCam.Say("Docked - dock camera and PiP off.");
    }

    public static string Describe()
    {
        var s = DockConfig.Current;
        return "Dock PiP " + (s.PipOn ? "on" : "off") + ": " + DockPipLayout.CornerName(s.PipCorner) + ", " + s.PipSize.ToString("0.#", CultureInfo.InvariantCulture)
               + "% tall, " + DockPipLayout.AspectText(s.PipAspect) + ", showing " + (s.PipMode == "assist" ? "the Docking Assist" : "the dock camera") + ", overlay " + (s.PipOverlay ? "on" : "off") + ", auto in 3rd person " + (s.PipAuto ? "on" : "off") + ", near a dock " + (s.PipNear ? "within " + s.PipNearRange.ToString("0", CultureInfo.InvariantCulture) + " m" : "off") + ", steer from it " + (s.SteerPip ? "on" : "off") + "; quality " + s.Quality + " ("
               + s.PipFps + " fps, up to " + s.PipRes + "p" + (s.PipEffects ? ", SSAO + bloom" : "") + ")." + (failed != null ? " " + failed : "");
    }

    /// <summary>Steps the window's size up or down (the toolbar's bigger / smaller; bigger wraps round to the smallest).</summary>
    public static string Bigger(int step, bool wrap = false)
    {
        float cur = DockConfig.Current.PipSize;
        float next;
        if (step > 0)
        {
            next = SizeSteps.FirstOrDefault(x => x > cur + 0.5f);
            if (next == 0) next = wrap ? SizeSteps[0] : SizeSteps.Last();
        }
        else
        {
            next = SizeSteps.LastOrDefault(x => x < cur - 0.5f);
            if (next == 0) next = SizeSteps[0];
        }
        DockConfig.Change(x => x.PipSize = next);
        return "PiP size: " + next.ToString("0", CultureInfo.InvariantCulture) + "% of the screen's height.";
    }

    public static string Edit()
    {
        try
        {
            if (Preview != null) return "";
            MyGuiSandbox.AddScreen(new DockPipEditor());
            return "";
        }
        catch (Exception e) { Plugin.Log.Write("PiP editor failed: " + e); return "The PiP editor couldn't open (see the log)."; }
    }

    // ------------------------------------------------------------------ the game thread: every frame

    private static int ticksWithoutDraw;

    /// <summary>Every update: no draw for two ticks (another screen, loading...) = no picture.</summary>
    public static void Tick()
    {
        if (++ticksWithoutDraw >= 2) Clear();
    }

    /// <summary>After the game world is drawn: the window's frame and overlay now, and the picture for the render thread.</summary>
    public static void Draw()
    {
        ticksWithoutDraw = 0;
        Shown = default;
        var s = DockConfig.Current;
        bool editing = Preview != null;
        try
        {
            bool fullAssist = FullAssist && !editing;
            UpdateNear(MySession.Static?.ControlledEntity as MyCockpit);
            if (!fullAssist && ((!Wanted && !editing) || DockCam.Showing || MyHud.CutsceneHud || (!MyHud.IsVisible && !editing))) { Clear(); return; }
            var cam = MySector.MainCamera;
            var cockpit = MySession.Static?.ControlledEntity as MyCockpit;
            if (cam == null || (cockpit == null && !editing)) { Clear(); return; }
            var full = MyGuiManager.GetFullscreenRectangle();
            var (x, y, w, h) = fullAssist ? (0f, 0f, full.Width, full.Height) : DockPipLayout.Rect(Layout, full.Width, full.Height);
            Shown = new RectangleF(full.Left + x, full.Top + y, w, h);

            string why = cockpit == null ? "SIT IN A COCKPIT" : null;
            DockTrack t = null;
            MatrixD world = MatrixD.Identity;
            if (why == null)
            {
                t = DockTrack.For(cockpit.CubeGrid);
                if (t?.Mine == null || !t.FrameFor(cockpit, out world)) why = "NO FREE CONNECTOR";
                else if (failed != null) why = "PIP UNAVAILABLE (SEE LOG)";
            }
            var grid = t?.Mine?.CubeGrid;
            uint actor = grid?.Render?.GetRenderObjectID() ?? uint.MaxValue;
            if (why == null && actor == uint.MaxValue) why = "NO PICTURE";
            // the Docking Assist: the picture goes in its view box only
            bool assist = (fullAssist || s.PipMode == "assist") && cockpit != null;
            var pic = new RectangleF(x, y, w, h);
            if (assist)
            {
                screen.Full = fullAssist;
                screen.Place = fullAssist ? "full" : "pip";
                DockScreens.Layout(screen, w, h);
                pic = new RectangleF(x + screen.ViewX, y + screen.ViewY, screen.ViewW, screen.ViewH);
            }
            if (why == null && pic.Width >= 16 && pic.Height >= 16)
            {
                Volatile.Write(ref request, new Request
                {
                    Actor = actor,
                    Local = world * MatrixD.Invert(grid.WorldMatrix),
                    Fov = DockScreens.LensFovY(pic.Width, pic.Height), Near = cam.NearPlaneDistance, Far = cam.FarPlaneDistance, FarFar = cam.FarFarPlaneDistance,
                    Rect = pic, Screen = new Vector2(full.Width, full.Height),
                    Fps = Math.Max(5, Math.Min(60, s.PipFps)), MaxHeight = Math.Max(fullAssist ? 720 : 180, Math.Min(1080, s.PipRes)), Effects = s.PipEffects,
                });
            }
            else Clear();

            var theme = DockConfig.Theme;
            sprites.Clear();
            var c = canvas.Begin(sprites, Vector2.Zero, null, theme);
            float k = Math.Max(0.6f, h / 300f);
            bool picture = why == null && Volatile.Read(ref ready);
            if (assist)
            {
                // the assist's own backing goes round the view box, so the picture shows through it
                screen.LiveView = picture;
                DockScreens.Draw(c, cockpit, w, h, screen, theme);
            }
            else if (!picture)
            {
                c.Rect(0, 0, w, h, theme.Background.A > 0 ? theme.Background : new HudColor(0, 0, 0, 200));
                c.Text(why ?? "CONNECTING...", w / 2, h / 2 - 9 * k, 16 * k, theme.Dim, TextAlign.Center, true);
            }
            else if (s.PipOverlay && cockpit != null)
                DockScreens.DrawFeed(c, cockpit, w, h, screen, theme);
            if (fullAssist) { HudSprites.Draw(sprites, new Vector2(Shown.X, Shown.Y)); return; }
            // the window's frame and caption
            float b = Math.Max(1.5f, 2 * k);
            var edge = theme.Main;
            c.Rect(0, 0, w, b, edge); c.Rect(0, h - b, w, b, edge); c.Rect(0, 0, b, h, edge); c.Rect(w - b, 0, b, h, edge);
            if (!assist && (!picture || !s.PipOverlay))
            {
                c.Rect(b, b, Math.Min(w - 2 * b, 120 * k), 20 * k, new HudColor(0, 0, 0, 150));
                c.Text("DOCK PIP", b + 6 * k, b + 2 * k, 14 * k, edge, TextAlign.Left, true);
            }
            HudSprites.Draw(sprites, new Vector2(Shown.X, Shown.Y));
        }
        catch (Exception e) { Clear(); Plugin.Log.Throttled("PiP draw failed: " + e.Message); }
    }

    // ------------------------------------------------------------------ hand-over to the render thread

    private sealed class Request
    {
        public uint Actor;           // the connector's grid's render object
        public MatrixD Local;        // the camera's matrix relative to that grid
        public float Fov, Near, Far, FarFar;
        public RectangleF Rect;      // on the display, in GUI pixels from its top-left
        public Vector2 Screen;       // the display's size, in the same pixels
        public int Fps, MaxHeight;
        public bool Effects;
    }

    private static Request request;
    private static bool ready;

    public static void Clear()
    {
        if (Volatile.Read(ref request) != null) Volatile.Write(ref request, null);
    }

    // ------------------------------------------------------------------ hooking the renderer

    private static readonly Harmony harmony = new Harmony("ProtogenDockingSolutions.Pip");
    private static bool patched;
    private static string failed;

    /// <summary>Called once from Plugin.Init (before the first frame, so no patch lands while the renderer runs).</summary>
    public static void Init()
    {
        if (patched) return;
        try
        {
            var draw = AccessTools.Method(typeof(MyRender11), "DrawGameScene");
            if (draw == null) throw new MissingMethodException("MyRender11.DrawGameScene");
            harmony.CreateReversePatcher(draw, new HarmonyMethod(typeof(DockPip), nameof(DrawGameSceneOriginal))).Patch();
            harmony.Patch(draw, postfix: new HarmonyMethod(typeof(DockPip), nameof(DrawGameScenePostfix)));
            var skip = new HarmonyMethod(typeof(DockPip), nameof(SkipWhilePip));
            foreach (var m in new[]
                     {
                         AccessTools.Method(typeof(MyEnvironmentProbe), nameof(MyEnvironmentProbe.UpdateProbe)),
                         AccessTools.Method(typeof(MyEnvironmentProbe), nameof(MyEnvironmentProbe.UpdateCullQuery)),
                         AccessTools.Method(typeof(MyEnvironmentProbe), nameof(MyEnvironmentProbe.FinalizeEnvProbes)),
                         AccessTools.Method(typeof(MyOffscreenRenderer), nameof(MyOffscreenRenderer.Render)),
                         AccessTools.Method(typeof(MyHighlight), nameof(MyHighlight.CopyDepthStencil)),
                         AccessTools.Method(typeof(MyHighlight), nameof(MyHighlight.Run)),
                         AccessTools.Method(typeof(MyShadowCascades), nameof(MyShadowCascades.Gather),
                             new[] { typeof(VRage.Render11.RenderContext.MyRenderContext), typeof(MyCommon.MyScreenLayout).MakeByRefType(), typeof(ISrvBindable), typeof(int) }),
                     })
            {
                if (m == null) { Plugin.Log.Write("PiP: a renderer method to skip wasn't found (the PiP may cost more)"); continue; }
                harmony.Patch(m, prefix: skip);
            }
            var prepare = AccessTools.Method(typeof(MyShadowCascades), nameof(MyShadowCascades.PrepareQueries));
            if (prepare != null) harmony.Patch(prepare, prefix: new HarmonyMethod(typeof(DockPip), nameof(PrepareQueriesPrefix)));
            var depth = AccessTools.Method(typeof(MyDepthStencilManager), nameof(MyDepthStencilManager.CreateDepthStencil));
            if (depth != null) harmony.Patch(depth, transpiler: new HarmonyMethod(typeof(DockPip), nameof(DepthStencilTranspiler)));
            patched = true;
            Plugin.Log.Write("PiP dock camera ready.");
        }
        catch (Exception e)
        {
            failed = "Dock PiP unavailable: " + e.GetType().Name + " (see the log).";
            Plugin.Log.Write("PiP: hooking the renderer failed, the PiP will say so: " + e);
            try { harmony.UnpatchAll(harmony.Id); } catch { }
        }
    }

    public static void Dispose()
    {
        try { harmony.UnpatchAll(harmony.Id); } catch { }
        patched = false;
    }

    /// <summary>The game's own DrawGameScene, without anyone's patches (filled in by the reverse patch).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DrawGameSceneOriginal(IRtvBindable renderTarget, out IBorrowedRtvTexture debugAmbientOcclusion) =>
        throw new NotImplementedException("reverse patch not applied");

    /// <summary>The renderer hooks are in (the LCD feeds need them too).</summary>
    public static bool Hooked => patched && failed == null;

    /// <summary>True while the PiP's (or an LCD feed's) scene is being rendered.</summary>
    public static bool Drawing { get; private set; }

    private static bool SkipWhilePip() => !Drawing;

    /// <summary>The PiP reuses this frame's shadow maps: only the cascades' constants are refreshed for its camera.</summary>
    private static bool PrepareQueriesPrefix(MyShadowCascades __instance, VRage.Render11.RenderContext.MyRenderContext rc)
    {
        if (!Drawing || !__instance.Enabled) return true;
        __instance.m_cascadeStats.Update();
        __instance.FillConstantBuffer(rc, __instance.m_csmConstants2);
        __instance.FillConstantBuffer(rc, __instance.m_csmConstants);
        return false;
    }

    /// <summary>The depth-stencil pool returns null (and the game throws) once its first 16 are used; the PiP's size needs
    /// its own depth buffers, so the pool may grow like the other pools. (Harmless if another plugin already did this.)</summary>
    private static IEnumerable<CodeInstruction> DepthStencilTranspiler(IEnumerable<CodeInstruction> code)
    {
        var allocate = AccessTools.Method(typeof(MyObjectsPool<MyDepthStencil>), nameof(MyObjectsPool<MyDepthStencil>.Allocate));
        var grow = AccessTools.Method(typeof(DockPip), nameof(AllocateDepthStencil));
        foreach (var i in code)
        {
            if ((i.opcode == OpCodes.Callvirt || i.opcode == OpCodes.Call) && Equals(i.operand, allocate)) yield return new CodeInstruction(OpCodes.Call, grow).WithLabels(i.labels);
            else yield return i;
        }
    }

    private static MyDepthStencil AllocateDepthStencil(MyObjectsPool<MyDepthStencil> pool, bool nullAllowed)
    {
        if (nullAllowed) return pool.Allocate(true);
        pool.AllocateOrCreate(out var item);
        return item;
    }

    // ------------------------------------------------------------------ the render thread

    private static IRtvTexture texture;
    private static int texW, texH;
    private static Format texFormat;
    private static long renderedAt;
    private static int idleFrames;

    /// <summary>After the main scene: the PiP (render when due, stretch into its window), then a Docking Assist LCD's feed.</summary>
    private static void DrawGameScenePostfix(IRtvBindable renderTarget)
    {
        if (Drawing || renderTarget == null || PlayGuard.Blocked) return;
        DrawPip(renderTarget);
        DockLcdFeed.RenderFrame(renderTarget);
    }

    private static void DrawPip(IRtvBindable renderTarget)
    {
        var r = Volatile.Read(ref request);
        if (r == null)
        {
            // not on show for a while: give the texture back
            if (++idleFrames == 300) Free();
            if (idleFrames > 2) Volatile.Write(ref ready, false);
            return;
        }
        idleFrames = 0;
        try
        {
            var size = renderTarget.Size;
            if (size.X < 64 || size.Y < 64) return;
            var format = renderTarget.Rtv.Description.Format;
            long now = Stopwatch.GetTimestamp();
            var (w, h) = PictureSize(r, size);
            bool stale = texture == null || !Volatile.Read(ref ready) || texW != w || texH != h || texFormat != format;
            if (stale || now - renderedAt >= Stopwatch.Frequency / r.Fps) Render(r, w, h, format, now);
            if (texture == null || !Volatile.Read(ref ready)) return;
            float sx = size.X / Math.Max(1f, r.Screen.X), sy = size.Y / Math.Max(1f, r.Screen.Y);
            var vp = new MyViewport(r.Rect.X * sx, r.Rect.Y * sy, r.Rect.Width * sx, r.Rect.Height * sy);
            MyRender11.RC.PixelShader.SetSamplers(0, MySamplerStateManager.StandardSamplers);
            MyCopyToRT.Run(renderTarget, texture, false, vp, true);
        }
        catch (Exception e) { Plugin.Log.Throttled("PiP render failed: " + e.Message); }
    }

    private static (int W, int H) PictureSize(Request r, Vector2I screen)
    {
        float want = r.Rect.Height * screen.Y / Math.Max(1f, r.Screen.Y);
        int cap = Math.Min(screen.Y, r.MaxHeight);
        int h = Heights.FirstOrDefault(x => x >= want * 0.9f);
        if (h == 0 || h > cap) h = Math.Min(cap, Heights.LastOrDefault(x => x <= cap) is int c && c > 0 ? c : Heights[0]);
        int w = (int)Math.Round(h * r.Rect.Width / Math.Max(1f, r.Rect.Height) / 2) * 2;
        return (Math.Max(32, Math.Min(screen.X, w)), Math.Max(24, h));
    }

    /// <summary>The scene into the PiP's texture: the game's own scene render, from the dock camera, at the picture's size.</summary>
    private static void Render(Request r, int w, int h, Format format, long now)
    {
        if (!(MyIDTracker<MyActor>.FindByID(r.Actor) is MyActor)) return;
        if (texture == null || texW != w || texH != h || texFormat != format)
        {
            if (texture != null) MyManagers.RwTextures.DisposeTex(ref texture);
            texture = MyManagers.RwTextures.CreateRtv("ProtogenDockingSolutions.Pip", w, h, format);
            texW = w; texH = h; texFormat = format;
            Volatile.Write(ref ready, false);
        }
        if (RenderScene(texture, r.Actor, r.Local, r.Fov, r.Near, r.Far, r.FarFar, w, h, r.Effects))
        {
            Volatile.Write(ref ready, true);
            renderedAt = now;
        }
    }

    /// <summary>
    /// The game's own scene render into target (w x h), from a camera at local on the grid whose render object is actor.
    /// The grid's matrix at this frame's render is used (interpolated), so the camera isn't a frame behind a fast ship.
    /// Render thread only. Shared by the PiP and the Docking Assist LCD feeds.
    /// </summary>
    internal static bool RenderScene(IRtvBindable target, uint actorId, MatrixD local, float fov, float near, float far, float farFar, int w, int h, bool effects)
    {
        if (!(MyIDTracker<MyActor>.FindByID(actorId) is MyActor actor)) return false;
        MatrixD world = local * actor.WorldMatrix;

        var env = MyRender11.Environment.Matrices;
        var view0 = env.ViewD;
        var proj0 = env.OriginalProjection;
        var projFar0 = env.OriginalProjectionFar;
        float fov0 = env.FovH, near0 = env.NearClipping, far0 = env.FarClipping;
        float offX = env.Projection.M31, offY = env.Projection.M32;
        var pos0 = env.CameraPosition;
        bool smooth0 = env.LastUpdateWasSmooth;
        var viewport0 = MyRender11.ViewportResolution;
        var resolution0 = MyRender11.m_resolution;
        var overrides = MyRender11.DebugOverrides;
        bool flares0 = overrides.Flares, ssao0 = overrides.SSAO, bloom0 = overrides.Bloom;
        bool frozen0 = MyRender11.Settings.ShadowCameraFrozen, postPP0 = MyRender11.Settings.DrawBillboardsPostPP;
        var geometry = MyManagers.GeometryRenderer;
        bool lod0 = geometry.IsLodUpdateEnabled;
        // the older renderer's models (cockpits, screens, many block parts) pick their LOD from whichever camera renders, and
        // start a dithered fade when it changes: a far feed camera would set them fading each time, a halo on the main view
        var lodding = MyCommon.LoddingSettings;
        bool oldLod0 = lodding != null && lodding.Global.IsUpdateEnabled;
        bool lumKept = false;
        // the eye adaptation's exposure: two 1x1 textures the scene render swaps each time (the new one first); a feed's
        // render would leave its own (dark scene) exposure as the main view's, which then re-adapts over a few frames, a
        // bright pulse each time a feed renders. The pair goes back in its order after, so the main view's stays first.
        // the frame constants' camera movement (this frame's camera minus the last one set): put back too
        var lastCamera0 = MyCommon.m_lastCameraPosition;
        var exposure = MyEyeAdaptation.m_autoExposure;
        var exposure0 = exposure != null && exposure.Length > 1 ? exposure[0] : null;
        var exposure1 = exposure0 != null ? exposure[1] : null;
        try
        {
            lumKept = SaveLuminance();
            MyRender11.ViewportResolution = new Vector2I(w, h);
            MyRender11.m_resolution = new Vector2I(w, h);
            overrides.Flares = false;
            if (!effects) overrides.SSAO = overrides.Bloom = false;
            MyRender11.Settings.ShadowCameraFrozen = true;
            // PostPP billboards are the pilot's overlays (Virtual Displays' cockpit screens): not part of a camera's picture
            MyRender11.Settings.DrawBillboardsPostPP = false;
            geometry.IsLodUpdateEnabled = false;
            if (lodding != null) lodding.Global.IsUpdateEnabled = false;
            SetCamera(MatrixD.Invert(world), proj0, projFar0, fov, near, far, farFar, world.Translation, 0, 0, false);
            Drawing = true;
            DrawGameSceneOriginal(target, out var ao);
            ao?.Release();
            return true;
        }
        finally
        {
            Drawing = false;
            if (lumKept) RestoreLuminance();
            if (exposure0 != null) { exposure[0] = exposure0; exposure[1] = exposure1; }
            MyRender11.ViewportResolution = viewport0;
            MyRender11.m_resolution = resolution0;
            overrides.Flares = flares0; overrides.SSAO = ssao0; overrides.Bloom = bloom0;
            MyRender11.Settings.ShadowCameraFrozen = frozen0;
            MyRender11.Settings.DrawBillboardsPostPP = postPP0;
            geometry.IsLodUpdateEnabled = lod0;
            if (lodding != null) lodding.Global.IsUpdateEnabled = oldLod0;
            SetCamera(view0, proj0, projFar0, fov0, near0, far0, farFar, pos0, offX, offY, smooth0);
            // the rest of this frame (debug draws, lines) uses the main camera's constants again
            MyCommon.m_lastCameraPosition = lastCamera0;
            MyCommon.UpdateFrameConstants();
            MyCommon.m_lastCameraPosition = lastCamera0;
        }
    }

    /// <summary>
    /// The eye adaptation's memory (the game's 1x1 average-luminance texture): every scene render updates it, so a feed's
    /// render (usually darker than the cockpit) would make the next frames of the main view over-exposed, a bright bloom
    /// round the screens that fades in a few frames, each time a feed renders. It's saved before a feed and put back after.
    /// </summary>
    private static IUavTexture lumSaved;

    private static bool SaveLuminance()
    {
        var lum = MyLuminanceAverage.m_prevLum;
        if (lum == null) return false;
        lumSaved ??= MyManagers.RwTextures.CreateUav("ProtogenDockingSolutions.PrevLum", 1, 1, Format.R32G32_Float);
        MyRender11.RC.CopyResource(lum, lumSaved);
        return true;
    }

    private static void RestoreLuminance()
    {
        var lum = MyLuminanceAverage.m_prevLum;
        if (lum != null && lumSaved != null) MyRender11.RC.CopyResource(lumSaved, lum);
    }

    private static void SetCamera(MatrixD view, MatrixD proj, MatrixD projFar, float fov, float near, float far, float farFar, Vector3D position, float offX, float offY, bool smooth)
    {
        var msg = MyRenderProxy.MessagePool.Get<MyRenderMessageSetCameraViewMatrix>(MyRenderMessageEnum.SetCameraViewMatrix);
        try
        {
            msg.ViewMatrix = view;
            msg.ProjectionMatrix = proj;
            msg.ProjectionFarMatrix = projFar;
            msg.FOV = fov;
            msg.FOVForSkybox = fov;
            msg.NearPlane = near;
            msg.FarPlane = far;
            msg.FarFarPlane = farFar;
            msg.CameraPosition = position;
            msg.ProjectionOffsetX = offX;
            msg.ProjectionOffsetY = offY;
            msg.Smooth = smooth;
            MyRender11.SetupCameraMatrices(msg);
        }
        finally { msg.Dispose(); }
    }

    private static void Free()
    {
        try
        {
            if (texture != null) MyManagers.RwTextures.DisposeTex(ref texture);
            Volatile.Write(ref ready, false);
        }
        catch (Exception e) { Plugin.Log.Throttled("PiP cleanup failed: " + e.Message); }
    }
}
