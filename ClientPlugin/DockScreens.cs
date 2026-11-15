using System;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRageMath;

namespace ProtogenDocking;

/// <summary>
/// The Docking Assist on screens: the "DOCK - Docking Assist" LCD script (any LCD, cockpit, seat or SDX2 crash-couch
/// screen), and any plugin or mod screen through the API.
///
/// The view box is drawn from the same spot as the dock camera (your connector's face, looking out, upright to the ship's
/// cockpit) with the target ship's outline, the dock's face and the approach tunnel, beside an offset scope, range and
/// closing-speed gauges and the next step (DockAssistRenderer). On LCDs the live camera picture goes under it (DockLcdFeed).
/// All on this PC; nothing is sent to the server.
/// </summary>
internal static class DockScreens
{
    /// <summary>The connector pair the dock screens (and the feed) use on the ship you sit in, when the dock camera is off.</summary>
    public static DockTrack ScreensTrack()
    {
        var cockpit = MySession.Static?.ControlledEntity as MyCockpit;
        return cockpit?.CubeGrid == null ? null : DockTrack.For(cockpit.CubeGrid);
    }

    /// <summary>Your next connector as the screens' camera (stays picked until you change it or the connector is gone).</summary>
    public static string NextConnector()
    {
        var t = ScreensTrack();
        if (t == null) return "Sit in a cockpit first.";
        if (t.MineList.Count == 0) t.FindMine(false);
        if (t.MineList.Count == 0) return "No connector on this ship.";
        t.AutoPick = false;
        if (t.Mine == null) { t.MineIndex = 0; t.Mine = t.MineList[0]; t.Scan(); }
        else if (t.MineList.Count > 1) t.NextMine();
        else return "Only one connector on this ship: " + DockTrack.Name(t.Mine) + ".";
        return "Docking Assist: " + DockTrack.Name(t.Mine) + (t.Target != null ? " -> " + DockTrack.Name(t.Target) : " (no dock in range yet)") +
               (t.MineList.Count > 1 ? " (" + (t.MineIndex + 1) + " of " + t.MineList.Count + ")" : "");
    }

    /// <summary>The next dock for the screens to aim at.</summary>
    public static string NextTarget()
    {
        var t = ScreensTrack();
        if (t == null) return "Sit in a cockpit first.";
        if (t.Mine == null) return "No connector on this ship.";
        t.Scan();
        if (t.Targets.Count < 2) return t.Target == null ? "No dock in range." : "Only one dock in range: " + DockTrack.Name(t.Target) + ".";
        t.NextTarget();
        return "Dock screens: docking with " + DockTrack.Name(t.Target) + " on " + t.Target.CubeGrid.DisplayName + ".";
    }

    /// <summary>The vertical field of view (radians) of a DockLens with its default angles on a w x h view: a live
    /// camera feed rendered with it lines up with what the lens projects.</summary>
    public static float LensFovY(float w, float h)
    {
        double fx = (w / 2) / Math.Tan(70 * Math.PI / 360), fy = (h / 2) / Math.Tan(45 * Math.PI / 360);
        return (float)(2 * Math.Atan((h / 2) / Math.Min(fx, fy)));
    }

    /// <summary>Where the Docking Assist's view box goes on a w x h screen (sets the screen's scale and layout).</summary>
    public static void Layout(DockScreen s, float w, float h)
    {
        float k = Math.Max(0.35f, Math.Min(h / 360f, w / 500f));
        s.Scale = k;
        s.Compact = w / k < 1100;
        DockStyles.Layout(StyleOf(s), s, w, h);
    }

    /// <summary>The screen's own style (an LCD's Custom Data), else its place's setting, else AssistStyle.</summary>
    public static string StyleOf(DockScreen s) => DockStyles.For(DockConfig.Current, s.Place, s.Style);

    /// <summary>
    /// /dock style [full | pip | lcd] [name | next | same]: the Docking Assist's look everywhere, or in one place
    /// ("same" = that place follows the main style again).
    /// </summary>
    public static string SetStyle(string a, string b = null)
    {
        var set = DockConfig.Current;
        string place = Array.IndexOf(DockStyles.Places, a) >= 0 ? a : null;
        string to = place != null ? b : a;
        string main = DockStyles.Norm(set.AssistStyle) ?? DockStyles.Default;
        if (string.IsNullOrEmpty(to))
        {
            if (place != null)
            {
                string own = DockStyles.Norm(DockStyles.PlaceSetting(set, place));
                return "Docking Assist on the " + DockStyles.PlaceName(place) + ": " + (own != null ? DockStyles.Title(own) : DockStyles.Title(main) + " (same as everywhere)")
                       + ". /dock style " + place + " <style> | next | same";
            }
            return "Docking Assist style: " + DockStyles.Title(main) + Overrides(set) + ". /dock style [full | pip | lcd] " + string.Join(" | ", DockStyles.Names) + " | next | same";
        }
        if (place != null && (to == "same" || to == "default" || to == "main"))
        {
            DockConfig.Change(x => SetPlace(x, place, ""));
            return "Docking Assist on the " + DockStyles.PlaceName(place) + ": same as everywhere (" + DockStyles.Title(main) + ").";
        }
        string cur = place != null ? DockStyles.For(set, place, null) : main;
        string n = to == "next" ? DockStyles.Next(cur) : DockStyles.Norm(to);
        if (n == null) return "No style called '" + to + "'. Styles: " + string.Join(", ", DockStyles.Names) + ".";
        if (place != null)
        {
            DockConfig.Change(x => SetPlace(x, place, n));
            return "Docking Assist on the " + DockStyles.PlaceName(place) + ": " + DockStyles.Title(n) + ".";
        }
        DockConfig.Change(x => x.AssistStyle = n);
        return "Docking Assist style: " + DockStyles.Title(n) + Overrides(DockConfig.Current) + ".";
    }

    private static void SetPlace(DockSettings x, string place, string v)
    {
        if (place == "full") x.FullStyle = v;
        else if (place == "pip") x.PipStyle = v;
        else x.LcdStyle = v;
    }

    /// <summary>" (PiP: Glass, LCDs: Radial HUD)" for the places with their own look.</summary>
    private static string Overrides(DockSettings set)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (var p in DockStyles.Places)
        {
            string own = DockStyles.Norm(DockStyles.PlaceSetting(set, p));
            if (own != null) parts.Add(DockStyles.PlaceName(p) + ": " + DockStyles.Title(own));
        }
        return parts.Count == 0 ? "" : " (" + string.Join(", ", parts) + ")";
    }

    /// <summary>Draws the Docking Assist in w x h pixels (the canvas offset puts it on the screen).</summary>
    public static void Draw(ICanvas c, MyCubeBlock block, float w, float h, DockScreen s, DockTheme theme = null)
    {
        s.Clear();
        s.Time = DockClock.GameNow;
        s.Synthetic = true;
        s.Title = s.LiveView ? "DOCKING ASSIST  -  LIVE" : "DOCKING ASSIST";
        Layout(s, w, h);
        var t = DockTrack.For(block?.CubeGrid);
        DockReading r;
        if (t?.Mine != null && t.FrameFor(block, out var world))
        {
            r = t.Read(world);
            var frame = new DockFrame { Pos = V(world.Translation), Fwd = V(world.Forward), Up = V(world.Up), Right = V(world.Right) };
            var lens = new DockLens(frame, s.ViewW, s.ViewH) { OffsetX = s.ViewX, OffsetY = s.ViewY };
            var target = t.Target;
            if (target != null && !target.Closed)
            {
                var tp = target.ConstraintPositionWorld();
                var ta = DockTrack.Axis(target);
                Box(lens, target.CubeGrid, s, 0);
                Face(lens, target, tp, ta, s);
                Tunnel(lens, target, tp, ta, s);
                MineSquare(lens, t, world, target, ta, s);
                s.Target = lens.Point(V(tp));
                if (s.Target == null) s.OffScreen = lens.Direction(V(tp));
                foreach (double d in DockGuide.CorridorMarks)
                {
                    if (d > r.Range * 1.5 + 10) break;
                    var p = lens.Point(V(tp + ta * d));
                    if (p.HasValue) s.Corridor.Add((p.Value.X, p.Value.Y, d));
                }
            }
        }
        else
            r = new DockReading { HasTarget = false };
        string style = StyleOf(s);
        DockStyles.Draw(style, c, DockStyles.Palette(style, theme ?? DockConfig.Theme), r, s, w, h);
    }

    /// <summary>
    /// The dock camera's guidance over a live dock camera feed on a w x h virtual screen: the full-screen dock camera's
    /// overlay (crosshair, target brackets, corridor, readout strip), projected with the feed's own lens (LensFovY).
    /// </summary>
    public static void DrawFeed(ICanvas c, MyCubeBlock block, float w, float h, DockScreen s, DockTheme theme = null)
    {
        s.Clear();
        s.Time = DockClock.GameNow;
        s.Synthetic = false;
        s.LiveView = false;
        float k = Math.Max(0.3f, Math.Min(h / 420f, w / 760f));
        s.Scale = k;
        s.Compact = true;
        s.Title = "DOCK CAM";
        s.ViewX = s.ViewY = 0; s.ViewW = w; s.ViewH = h;
        var t = DockTrack.For(block?.CubeGrid);
        DockReading r;
        if (t?.Mine != null && t.FrameFor(block, out var world))
        {
            r = t.Read(world);
            var frame = new DockFrame { Pos = V(world.Translation), Fwd = V(world.Forward), Up = V(world.Up), Right = V(world.Right) };
            var lens = new DockLens(frame, w, h);
            var target = t.Target;
            if (target != null && !target.Closed)
            {
                var tp = target.ConstraintPositionWorld();
                var ta = DockTrack.Axis(target);
                s.Target = lens.Point(V(tp));
                if (s.Target == null) s.OffScreen = lens.Direction(V(tp));
                foreach (double d in DockGuide.CorridorMarks)
                {
                    if (d > r.Range * 1.5 + 10) break;
                    var p = lens.Point(V(tp + ta * d));
                    if (p.HasValue) s.Corridor.Add((p.Value.X, p.Value.Y, d));
                }
            }
        }
        else
            r = new DockReading { HasTarget = false };
        DockCamRenderer.Draw(c, theme ?? DockConfig.Theme, r, s, w, h);
    }

    /// <summary>The grid's bounding box (its own axes) as 12 lines.</summary>
    private static void Box(DockLens lens, MyCubeGrid g, DockScreen s, int kind)
    {
        var box = g.PositionComp.LocalAABB;
        var m = g.PositionComp.WorldMatrixRef;
        var c = new V3[8];
        for (int i = 0; i < 8; i++)
        {
            var l = new Vector3D((i & 1) == 0 ? box.Min.X : box.Max.X, (i & 2) == 0 ? box.Min.Y : box.Max.Y, (i & 4) == 0 ? box.Min.Z : box.Max.Z);
            c[i] = V(Vector3D.Transform(l, m));
        }
        for (int i = 0; i < 8; i++)
            for (int bit = 1; bit < 8; bit <<= 1)
                if ((i & bit) == 0) Edge(lens, c[i], c[i | bit], s, kind);
    }

    /// <summary>The dock's face: a square across its axis with a cross in it.</summary>
    private static void Face(DockLens lens, Sandbox.Game.Entities.Cube.MyShipConnector target, Vector3D tp, Vector3D ta, DockScreen s)
    {
        double half = target.CubeGrid.GridSize * 0.4;
        DockTrack.Across(target, ta, out var u, out var v);
        V3 a = V(tp + (u + v) * half), b = V(tp + (u - v) * half), c = V(tp - (u + v) * half), d = V(tp - (u - v) * half);
        Edge(lens, a, b, s, 1); Edge(lens, b, c, s, 1); Edge(lens, c, d, s, 1); Edge(lens, d, a, s, 1);
        Edge(lens, V(tp + u * half * 0.5), V(tp - u * half * 0.5), s, 1);
        Edge(lens, V(tp + v * half * 0.5), V(tp - v * half * 0.5), s, 1);
    }

    /// <summary>The approach tunnel: a square gate across the dock's axis at each corridor mark (wider further out), joined by rails.</summary>
    private static void Tunnel(DockLens lens, Sandbox.Game.Entities.Cube.MyShipConnector target, Vector3D tp, Vector3D ta, DockScreen s)
    {
        // the gates' sides run along the dock block's own sides: line your connector's square up with them
        DockTrack.Across(target, ta, out var u, out var v);
        V3[] prev = null;
        foreach (double d in DockGuide.CorridorMarks)
        {
            double g = DockGuide.GateHalf(target.CubeGrid.GridSize, d);
            var o = tp + ta * d;
            var q = new[] { V(o + (u + v) * g), V(o + (u - v) * g), V(o - (u + v) * g), V(o - (u - v) * g) };
            for (int i = 0; i < 4; i++)
            {
                if (lens.Segment(q[i], q[(i + 1) % 4], out var l)) s.Tunnel.Add((l.X1, l.Y1, l.X2, l.Y2, d, false));
                if (prev != null && lens.Segment(prev[i], q[i], out var rl)) s.Tunnel.Add((rl.X1, rl.Y1, rl.X2, rl.Y2, d, true));
            }
            prev = q;
        }
    }

    /// <summary>
    /// Your connector's own square, a little way out in front of the view, with its sides along your connector block's
    /// sides (as the tunnel's run along the dock's): turn (roll) until the two line up and the ships sit the same way round.
    /// </summary>
    private static void MineSquare(DockLens lens, DockTrack t, MatrixD world, Sandbox.Game.Entities.Cube.MyShipConnector target, Vector3D ta, DockScreen s)
    {
        var fwd = DockTrack.Axis(t.Mine);
        DockTrack.Across(t.Mine, fwd, out var mu, out var mv);
        DockTrack.Across(target, ta, out var tu, out _);
        // the dock's side seen along your axis, against yours, folded into 0-45 degrees (a square matches every 90)
        var p = tu - fwd * Vector3D.Dot(tu, fwd);
        if (p.LengthSquared() > 1e-6)
        {
            p.Normalize();
            double ang = MathHelper.ToDegrees(Math.Atan2(Vector3D.Dot(p, mv), Vector3D.Dot(p, mu)));
            ang = ((ang % 90) + 90) % 90;
            s.Roll = Math.Min(ang, 90 - ang);
        }
        var o = world.Translation + fwd * 2.0;
        const double half = 0.3;
        V3 a = V(o + (mu + mv) * half), b = V(o + (mu - mv) * half), c = V(o - (mu + mv) * half), d = V(o - (mu - mv) * half);
        Edge(lens, a, b, s, 3); Edge(lens, b, c, s, 3); Edge(lens, c, d, s, 3); Edge(lens, d, a, s, 3);
    }

    private static void Edge(DockLens lens, V3 a, V3 b, DockScreen s, int kind)
    {
        if (lens.Segment(a, b, out var l)) s.Edges.Add((l.X1, l.Y1, l.X2, l.Y2, kind));
    }

    private static V3 V(Vector3D v) => new V3(v.X, v.Y, v.Z);
}
