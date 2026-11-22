using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProtogenDocking;

/// <summary>Where the dock camera sits and looks: your connector's connection point and axes (unit vectors).</summary>
public struct DockFrame
{
    public V3 Pos, Fwd, Up, Right;
}

/// <summary>One frame's docking figures: your connector against the target connector.</summary>
public sealed class DockReading
{
    public bool HasTarget;
    /// <summary>Metres between the two connection points.</summary>
    public double Range;
    /// <summary>How far ahead along your connector's axis the target point is (m; negative = behind the face).</summary>
    public double Ahead;
    /// <summary>The target point's offset across your view (m): right and up of your connector's axis.</summary>
    public double OffRight, OffUp;
    /// <summary>Closing speed (m/s, positive = getting closer).</summary>
    public double Closing;
    /// <summary>Sideways drift across your view (m/s, right and up).</summary>
    public double DriftRight, DriftUp;
    /// <summary>Degrees off face-to-face (0 = the two connectors face each other square).</summary>
    public double Misalign;
    /// <summary>The closing speed that's comfortable at this range (m/s).</summary>
    public double Safe;
    /// <summary>0 fine, 1 a bit fast, 2 too fast.</summary>
    public int SpeedLevel;
    /// <summary>Close and square enough that the game would lock (0.59 m and 45°, MyShipConnector).</summary>
    public bool InLockRange;
    /// <summary>The game says it can connect now / is connected.</summary>
    public bool Connectable, Locked;
    public string Mine = "", Target = "", TargetGrid = "";
}

/// <summary>What the overlay needs on screen: projected points (pixels), null when behind the camera.</summary>
public sealed class DockScreen
{
    public (float X, float Y)? Target;
    /// <summary>The approach corridor: points out along the target connector's axis, nearest first, with their distance (m).</summary>
    public readonly List<(float X, float Y, double D)> Corridor = new List<(float, float, double)>();
    /// <summary>Unit direction (x right, y down) towards the target when it's off screen or behind.</summary>
    public (float X, float Y)? OffScreen;
    /// <summary>Seconds of game time, for the timestamp and the flicker.</summary>
    public double Time;
    /// <summary>Scale (1 = 1080p full screen); 0 = from the height.</summary>
    public float Scale;
    /// <summary>Small screens (LCDs, cockpit and crash-couch screens): tighter margins, four readouts.</summary>
    public bool Compact;
    /// <summary>A drawn picture (screens), not over the real camera view: a dark background and wireframes.</summary>
    public bool Synthetic;
    /// <summary>Docking Assist: the real dock camera picture shows in the view box (virtual screens), so it isn't painted over.</summary>
    public bool LiveView;
    public string Title = "DOCK CAM";
    /// <summary>Docking Assist: the full-screen dock camera's layout (big picture, slim readouts), not the PiP / screen one.</summary>
    public bool Full;
    /// <summary>Docking Assist: the look (DockStyles); empty = the AssistStyle setting.</summary>
    public string Style = "";
    /// <summary>Docking Assist: where it's shown ("full", "pip", "lcd"; empty = none of them), for the place's style setting.</summary>
    public string Place = "";
    /// <summary>Wireframe lines (pixels): Kind 0 = the target ship's outline, 1 = the dock's face, 2 = other ships.</summary>
    public readonly List<(float X1, float Y1, float X2, float Y2, int Kind)> Edges = new List<(float, float, float, float, int)>();

    /// <summary>The approach tunnel (Docking Assist): gate sides and the rails between gates, in pixels, with the gate's distance out from the dock (m).</summary>
    public readonly List<(float X1, float Y1, float X2, float Y2, double D, bool Rail)> Tunnel = new List<(float, float, float, float, double, bool)>();
    /// <summary>Docking Assist: where the drawn view sits on the screen (pixels); 0 width = the whole screen.</summary>
    public float ViewX, ViewY, ViewW, ViewH;

    /// <summary>Docking Assist: how far (degrees, 0-45) your connector's square is rolled from the dock's; -1 = not known.</summary>
    public double Roll = -1;

    public void Clear() { Target = null; OffScreen = null; Corridor.Clear(); Edges.Clear(); Tunnel.Clear(); Roll = -1; }

}

/// <summary>
/// A pinhole camera on a DockFrame (screens draw the dock view this way): world points to pixels, x right, y down.
/// </summary>
public sealed class DockLens
{
    private readonly DockFrame cam;
    private readonly double f;
    private readonly float w, h;
    public const double Near = 0.1;
    /// <summary>Where the lens's w x h view sits on the screen (added to every point it returns).</summary>
    public float OffsetX, OffsetY;

    /// <summary>hFov = the horizontal field of view in degrees, narrowed so the shorter side still sees at least minFov.</summary>
    public DockLens(DockFrame cam, float w, float h, double hFov = 70, double minFov = 45)
    {
        this.cam = cam; this.w = w; this.h = h;
        double fx = (w / 2) / Math.Tan(hFov * Math.PI / 360), fy = (h / 2) / Math.Tan(minFov * Math.PI / 360);
        f = Math.Min(fx, fy);
    }

    private (double X, double Y, double Z) Local(V3 p)
    {
        var d = p - cam.Pos;
        return (V3.Dot(d, cam.Right), V3.Dot(d, cam.Up), V3.Dot(d, cam.Fwd));
    }

    private (float X, float Y) Px(double x, double y, double z) => ((float)(w / 2 + x / z * f), (float)(h / 2 - y / z * f));

    /// <summary>On screen (with a 50 px margin), or null.</summary>
    public (float X, float Y)? Point(V3 p)
    {
        var (x, y, z) = Local(p);
        if (z < 0.05) return null;
        var s = Px(x, y, z);
        if (s.X < -50 || s.Y < -50 || s.X > w + 50 || s.Y > h + 50) return null;
        return (s.X + OffsetX, s.Y + OffsetY);
    }

    /// <summary>Which way a point lies on screen (unit, x right, y down).</summary>
    public (float X, float Y) Direction(V3 p)
    {
        var (x, y, _) = Local(p);
        double l = Math.Sqrt(x * x + y * y);
        return l < 1e-6 ? (0f, 1f) : ((float)(x / l), (float)(-y / l));
    }

    /// <summary>A line cut at the near plane; false when it's wholly behind the camera.</summary>
    public bool Segment(V3 a, V3 b, out (float X1, float Y1, float X2, float Y2) line)
    {
        line = default;
        var (ax, ay, az) = Local(a);
        var (bx, by, bz) = Local(b);
        if (az < Near && bz < Near) return false;
        if (az < Near) { double t = (Near - az) / (bz - az); ax += (bx - ax) * t; ay += (by - ay) * t; az = Near; }
        else if (bz < Near) { double t = (Near - bz) / (az - bz); bx += (ax - bx) * t; by += (ay - by) * t; bz = Near; }
        var p = Px(ax, ay, az); var q = Px(bx, by, bz);
        // far off screen both ends on one side: skip
        if ((p.X < 0 && q.X < 0) || (p.X > w && q.X > w) || (p.Y < 0 && q.Y < 0) || (p.Y > h && q.Y > h)) return false;
        // keep huge numbers (points just past the near plane) out of the sprite list
        line = (Clamp(p.X, w), Clamp(p.Y, h), Clamp(q.X, w), Clamp(q.Y, h));
        if (Math.Abs(p.X) > 20 * w || Math.Abs(p.Y) > 20 * h || Math.Abs(q.X) > 20 * w || Math.Abs(q.Y) > 20 * h) line = ClipBox(p, q);
        line = (line.X1 + OffsetX, line.Y1 + OffsetY, line.X2 + OffsetX, line.Y2 + OffsetY);
        return true;
    }

    private static float Clamp(float v, float size) => Math.Max(-20 * size, Math.Min(21 * size, v));

    private (float, float, float, float) ClipBox((float X, float Y) p, (float X, float Y) q)
    {
        // scale the far end in towards the near one so both stay within a few screens
        float dx = q.X - p.X, dy = q.Y - p.Y, lim = 4 * Math.Max(w, h);
        float len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len > lim) { q = (p.X + dx / len * lim, p.Y + dy / len * lim); }
        return (Clamp(p.X, w), Clamp(p.Y, h), Clamp(q.X, w), Clamp(q.Y, h));
    }
}

/// <summary>
/// The docking maths: range, closing speed, offset and alignment of your connector against a target connector.
/// The game locks two connectors when their connection points are within about 0.59 m (√0.35) and their connect axes
/// are more than 135° apart (MyShipConnector.TryAttach); the same tests are used here for "in lock range".
/// </summary>
public static class DockGuide
{
    public const double LockDistance = 0.59, LockAngle = 45;
    /// <summary>The corridor marks, in metres out from the target connector.</summary>
    public static readonly double[] CorridorMarks = { 2, 5, 10, 20, 40, 80, 150 };

    /// <summary>Comfortable closing speed at a range: 10 m/s at 100 m and up, about 2 m/s at 20 m, 0.5 m/s in the last few metres.</summary>
    public static double SafeSpeed(double range) => Math.Max(0.5, Math.Min(10, 0.3 + range * 0.097));

    /// <summary>Closing faster than Safe x this is the red SLOW DOWN.</summary>
    public const double RedFactor = 1.6;
    /// <summary>The dampener brakes with all the thrust there is above its limit x this (well under the red).</summary>
    public const float OverFactor = 1.25f;

    /// <summary>The dampener's Auto speed limit at a range: the safe speed the assist advises, never above your own limit.</summary>
    public static double AutoSpeed(double range, double max) => Math.Min(max, SafeSpeed(range));

    /// <summary>
    /// The dampener's Auto acceleration limit at a range: 0.3 x the safe speed per second (3 m/s2 at 100 m, about 0.6 at
    /// 20 m, 0.15 in the last metres), never above your own limit. That is about three times what slowing down along the
    /// safe-speed curve needs, so it keeps up as the limit drops.
    /// </summary>
    public static double AutoAccel(double range, double max) => Math.Min(max, Math.Max(0.15, 0.3 * SafeSpeed(range)));

    /// <param name="me">your connector's frame</param>
    /// <param name="targetPos">the target connector's connection point</param>
    /// <param name="targetAxis">the target connector's outward axis (unit)</param>
    /// <param name="relVel">your velocity minus the target's at those points (m/s)</param>
    public static DockReading Read(DockFrame me, V3 targetPos, V3 targetAxis, V3 relVel)
    {
        var r = new DockReading { HasTarget = true };
        var d = targetPos - me.Pos;
        r.Range = d.Length;
        r.Ahead = V3.Dot(d, me.Fwd);
        r.OffRight = V3.Dot(d, me.Right);
        r.OffUp = V3.Dot(d, me.Up);
        // closing: the rate the range shrinks; along the line between the points (straight ahead when they touch)
        var line = r.Range > 1e-3 ? d * (1 / r.Range) : me.Fwd;
        r.Closing = V3.Dot(relVel, line);
        r.DriftRight = V3.Dot(relVel, me.Right);
        r.DriftUp = V3.Dot(relVel, me.Up);
        double cos = Math.Max(-1, Math.Min(1, V3.Dot(Unit(me.Fwd), Unit(targetAxis * -1))));
        r.Misalign = Math.Acos(cos) * 180 / Math.PI;
        r.Safe = SafeSpeed(r.Range);
        r.SpeedLevel = r.Closing <= r.Safe ? 0 : r.Closing <= r.Safe * RedFactor ? 1 : 2;
        r.InLockRange = r.Range <= LockDistance && r.Misalign <= LockAngle;
        return r;
    }

    private static V3 Unit(V3 v) { double l = v.Length; return l > 1e-12 ? v * (1 / l) : new V3(0, 0, 0); }

    /// <summary>The one thing to do next, in the overlay's words.</summary>
    public static string Advice(DockReading r)
    {
        if (r == null || !r.HasTarget) return "NO DOCK IN RANGE";
        if (r.Locked) return "LOCKED";
        if (r.Connectable || r.InLockRange) return "READY - LOCK";
        if (r.SpeedLevel == 2) return "SLOW DOWN";
        if (r.Ahead < 0) return "TARGET BEHIND - BACK OFF";
        double side = Math.Sqrt(r.OffRight * r.OffRight + r.OffUp * r.OffUp);
        // turn first while far out, then slide sideways onto the axis, then come straight in
        if (r.Misalign > 10 && r.Range > 15) return "TURN TO FACE";
        if (side > Math.Max(0.4, Math.Min(3, r.Range * 0.05))) return "SLIDE " + Slide(r);
        if (r.Misalign > LockAngle * 0.5) return "SQUARE UP";
        if (r.SpeedLevel == 1) return "EASE OFF";
        return r.Closing < 0.05 ? "COME IN" : "ON APPROACH";
    }

    /// <summary>The docking phase for the status badge, with a level: 0 good, 1 caution, 2 warning, 3 normal.</summary>
    public static (string Name, int Level) Phase(DockReading r)
    {
        if (r == null || !r.HasTarget) return ("NO DOCK", 1);
        if (r.Locked) return ("LOCKED", 0);
        if (r.Connectable || r.InLockRange) return ("LOCK READY", 0);
        if (r.SpeedLevel == 2) return ("TOO FAST", 2);
        if (r.Ahead < 0) return ("OVERSHOT", 2);
        double side = Math.Sqrt(r.OffRight * r.OffRight + r.OffUp * r.OffUp);
        if (r.Misalign > 10 && r.Range > 15) return ("TURN", 1);
        if (side > Math.Max(0.4, Math.Min(3, r.Range * 0.05))) return ("LINE UP", 1);
        if (r.Range < 5) return ("FINAL", 3);
        return ("APPROACH", 3);
    }

    /// <summary>Seconds until contact at the present closing speed, or -1 when not closing.</summary>
    public static double TimeToContact(DockReading r) => r == null || !r.HasTarget || r.Closing < 0.02 ? -1 : r.Range / r.Closing;

    /// <summary>Half the size of the approach tunnel's gate at d metres out from the dock: tight at the dock, wider out.</summary>
    public static double GateHalf(double gridSize, double d) => Math.Max(0.6, gridSize * 0.5) + d * 0.08;

    /// <summary>Which way to translate to put the target on the crosshair, e.g. "RIGHT 1.2 M, UP 0.4 M".</summary>
    public static string Slide(DockReading r)
    {
        var parts = new List<string>();
        if (Math.Abs(r.OffRight) >= 0.1) parts.Add((r.OffRight > 0 ? "RIGHT " : "LEFT ") + M(Math.Abs(r.OffRight)));
        if (Math.Abs(r.OffUp) >= 0.1) parts.Add((r.OffUp > 0 ? "UP " : "DOWN ") + M(Math.Abs(r.OffUp)));
        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }

    public static string M(double m) => (m >= 100 ? m.ToString("0", CultureInfo.InvariantCulture) : m.ToString("0.0", CultureInfo.InvariantCulture)) + " M";
    public static string Mps(double v) => v.ToString(Math.Abs(v) >= 10 ? "0" : "0.0", CultureInfo.InvariantCulture) + " M/S";
}

/// <summary>
/// The full-screen dock camera's overlay: a video-feed look (scanlines, vignette, a flicker band, REC and timestamp)
/// with the guidance on top (crosshair, target brackets, approach corridor, readout strip). Pixels; k = scale (1 at 1080p).
/// </summary>
public static class DockCamRenderer
{
    public static void Draw(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        float k = s.Scale > 0 ? s.Scale : h / 1080f;
        if (s.Synthetic)
        {
            // a drawn view: dark ground, then the wireframes (dock face brightest)
            c.Rect(0, 0, w, h, HudColor.Black.WithAlpha(0.92f));
            c.Rect(0, 0, w, h, t.Background.WithAlpha(0.5f));
            float lt = Math.Max(1, 1.5f * k);
            foreach (var e in s.Edges)
                Line(c, e.X1, e.Y1, e.X2, e.Y2, e.Kind == 1 ? lt * 1.5f : lt, e.Kind == 1 ? t.Main : e.Kind == 0 ? t.Dim.WithAlpha(0.9f) : t.Dim.WithAlpha(0.45f));
        }
        Feed(c, t, s, w, h, k);
        float cx = w / 2, cy = h / 2;
        var good = t.Good; var warn = t.Warn; var bad = t.Bad;
        var main = t.Main;

        // corridor: rings out along the target's axis, faint and smaller further out
        if (r != null && r.HasTarget)
            foreach (var p in s.Corridor)
            {
                float size = (float)Math.Max(6 * k, Math.Min(26 * k, 300 * k / Math.Max(1, p.D + r.Range * 0.25)));
                c.Shape(HudShape.Ring, p.X - size / 2, p.Y - size / 2, size, size, t.Dim.WithAlpha(0.9f));
            }

        // crosshair = your connector's axis
        var cross = r != null && (r.InLockRange || r.Connectable || r.Locked) ? good : main;
        float gap = 10 * k, arm = 26 * k, th = Math.Max(1, 2 * k);
        c.Rect(cx - gap - arm, cy - th / 2, arm, th, cross);
        c.Rect(cx + gap, cy - th / 2, arm, th, cross);
        c.Rect(cx - th / 2, cy - gap - arm, th, arm, cross);
        c.Rect(cx - th / 2, cy + gap, th, arm, cross);
        c.Shape(HudShape.Ring, cx - 4 * k, cy - 4 * k, 8 * k, 8 * k, cross);

        if (r != null && r.HasTarget)
        {
            var lvl = r.Locked || r.Connectable || r.InLockRange ? good : r.SpeedLevel == 2 ? bad : r.SpeedLevel == 1 ? warn : main;
            if (s.Target.HasValue)
            {
                var (tx, ty) = s.Target.Value;
                // the box shrinks with range but never below a readable size
                float bs = (float)Math.Max(24 * k, Math.Min(240 * k, 2600 * k / Math.Max(1, r.Range)));
                Brackets(c, tx - bs / 2, ty - bs / 2, bs, bs, Math.Max(1, 2 * k), lvl, k);
                // a line from the crosshair to the target when you're off the axis
                if (Math.Abs(tx - cx) + Math.Abs(ty - cy) > 30 * k) Line(c, cx, cy, tx, ty, Math.Max(1, k), lvl.WithAlpha(0.45f));
                c.Text(DockGuide.M(r.Range), tx + bs / 2 + 6 * k, ty - bs / 2, 16 * k, lvl, TextAlign.Left, true);
                c.Text(r.Target, tx + bs / 2 + 6 * k, ty - bs / 2 + 18 * k, 12 * k, t.Text.WithAlpha(0.8f), TextAlign.Left, true);
            }
            else if (s.OffScreen.HasValue)
            {
                var (dx, dy) = s.OffScreen.Value;
                float ex = cx + dx * (w / 2 - 60 * k), ey = cy + dy * (h / 2 - 60 * k);
                float ang = (float)Math.Atan2(dy, dx) + (float)(Math.PI / 2);
                c.Shape(HudShape.Triangle, ex - 14 * k, ey - 14 * k, 28 * k, 28 * k, lvl, ang);
                c.Text(DockGuide.M(r.Range), ex, ey + 18 * k, 14 * k, lvl, TextAlign.Center, true);
            }
        }

        Strip(c, t, r, s, w, h, k);
    }

    /// <summary>The video-feed look: vignette, scanlines, a rolling flicker band, frame corners, REC and timestamp.</summary>
    private static void Feed(ICanvas c, DockTheme t, DockScreen s, float w, float h, float k)
    {
        var black = HudColor.Black;
        float m = (s.Compact ? 10 : 28) * k;
        // vignette: darker towards the edges
        for (int i = 0; i < (s.Compact ? 3 : 6); i++)
        {
            float a = 0.10f - i * 0.015f, bw = (i + 1) * 22 * k;
            c.Rect(0, 0, bw, h, black.WithAlpha(a)); c.Rect(w - bw, 0, bw, h, black.WithAlpha(a));
            c.Rect(0, 0, w, bw * 0.7f, black.WithAlpha(a)); c.Rect(0, h - bw * 0.7f, w, bw * 0.7f, black.WithAlpha(a));
        }
        // scanlines, a soft band rolling down the picture, and now and then a thin glitch line
        float step = Math.Max(3, (float)Math.Round(4 * k));
        var line = black.WithAlpha(0.13f);
        for (float y = 0; y < h; y += step) c.Rect(0, y, w, Math.Max(1, step / 3), line);
        float band = (float)((s.Time * 0.12) % 1.2 - 0.1) * h;
        c.Rect(0, band, w, 40 * k, t.Text.WithAlpha(0.025f));
        int tick = (int)(s.Time * 7);
        if (tick % 11 == 0) c.Rect(0, (float)((tick * 7919 % 997) / 997.0) * h, w, Math.Max(1, k), t.Text.WithAlpha(0.18f));

        // frame corners
        float len = (s.Compact ? 30 : 60) * k, th = Math.Max(1, 2 * k);
        var col = t.Main.WithAlpha(0.8f);
        Brackets(c, m, m, w - 2 * m, h - 2 * m, th, col, k, len);

        // REC (blinking) and the camera name, timestamp
        float px = 18 * k;
        bool on = ((int)(s.Time * 2)) % 2 == 0;
        if (on) c.Shape(HudShape.Circle, m + 18 * k, m + 18 * k, 14 * k, 14 * k, t.Bad);
        c.Text("REC", m + 38 * k, m + 14 * k, px, t.Text, TextAlign.Left, true);
        c.Text(s.Title, m + 100 * k, m + 14 * k, px, t.Main, TextAlign.Left, true);
        var ts = TimeSpan.FromSeconds(Math.Max(0, s.Time));
        c.Text("T+" + ((int)ts.TotalHours).ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00") + "." + (ts.Milliseconds / 100),
            w - m - 18 * k, m + 14 * k, px, t.Text, TextAlign.Right, true);
    }

    /// <summary>The readout strip along the bottom, and the advice line above it.</summary>
    private static void Strip(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h, float k)
    {
        bool small = s.Compact;
        float m = (small ? 10 : 28) * k, pad = (small ? 4 : 16) * k, sh = 58 * k, y = h - m - sh - pad;
        c.Rect(m + pad, y, w - 2 * m - 2 * pad, sh, t.Background.WithAlpha(0.55f));
        float lp = (small ? 15 : 12) * k, vp = (small ? 20 : 22) * k, ly = y + 6 * k, vy = y + 26 * k;
        if (r == null || !r.HasTarget)
        {
            c.Text(DockGuide.Advice(r), w / 2, y + 16 * k, vp, t.Warn, TextAlign.Center, true);
            if (r != null && r.Mine.Length > 0) c.Text("CAMERA " + r.Mine.ToUpperInvariant(), w / 2, y - 26 * k, 14 * k, t.Text.WithAlpha(0.8f), TextAlign.Center, true);
            return;
        }
        var speed = r.SpeedLevel == 2 ? t.Bad : r.SpeedLevel == 1 ? t.Warn : t.Good;
        var align = r.Misalign <= DockGuide.LockAngle * 0.5 ? t.Good : r.Misalign <= DockGuide.LockAngle ? t.Warn : t.Bad;
        double side = Math.Sqrt(r.OffRight * r.OffRight + r.OffUp * r.OffUp);
        var all = new (string L, string V, HudColor C)[]
        {
            ("RANGE", DockGuide.M(r.Range), t.Text),
            ("CLOSING", DockGuide.Mps(r.Closing), speed),
            ("SAFE", DockGuide.Mps(r.Safe), t.Text.WithAlpha(0.65f)),
            ("OFFSET", side < 0.1 ? "ON AXIS" : DockGuide.M(side), side < 0.4 ? t.Good : t.Text),
            ("DRIFT", DockGuide.Mps(Math.Sqrt(r.DriftRight * r.DriftRight + r.DriftUp * r.DriftUp)), t.Text),
            ("ALIGN", r.Misalign.ToString("0.0", CultureInfo.InvariantCulture) + "°", align),
        };
        // small screens: range, closing, offset, align
        var cells = small ? new[] { all[0], all[1], all[3], all[5] } : all;
        float inset = (small ? 10 : 32) * k;
        float cw = (w - 2 * m - 2 * inset) / cells.Length, x = m + inset;
        foreach (var cell in cells)
        {
            c.Text(cell.L, x, ly, lp, t.Main.WithAlpha(0.85f), TextAlign.Left, true);
            c.Text(cell.V, x, vy, vp, cell.C, TextAlign.Left, true);
            x += cw;
        }
        string advice = DockGuide.Advice(r);
        var ac = r.Locked || r.Connectable || r.InLockRange ? t.Good : r.SpeedLevel == 2 ? t.Bad : r.SpeedLevel == 1 ? t.Warn : t.Text;
        float ap = 24 * k, aw = c.TextWidth(advice, ap, true) + 28 * k;
        if (aw > w - 2 * m) { ap *= (w - 2 * m) / aw; aw = w - 2 * m; } // narrow screens: smaller advice text
        c.Rect(w / 2 - aw / 2, y - ap - 18 * k, aw, ap + 10 * k, t.Background.WithAlpha(0.55f));
        c.Text(advice, w / 2, y - ap - 13 * k, ap, ac, TextAlign.Center, true);
        c.Text(r.Mine.ToUpperInvariant() + "  >  " + r.Target.ToUpperInvariant() + (r.TargetGrid.Length > 0 && !small ? "  (" + r.TargetGrid.ToUpperInvariant() + ")" : ""),
            m + (small ? 8 : 32) * k, m + 40 * k, 14 * k, t.Text.WithAlpha(0.8f), TextAlign.Left, true);
    }

    /// <summary>Four corner brackets round a box.</summary>
    public static void Brackets(ICanvas c, float x, float y, float w, float h, float th, HudColor col, float k, float len = 0)
    {
        float l = len > 0 ? len : Math.Max(6 * k, Math.Min(w, h) * 0.28f);
        c.Rect(x, y, l, th, col); c.Rect(x, y, th, l, col);
        c.Rect(x + w - l, y, l, th, col); c.Rect(x + w - th, y, th, l, col);
        c.Rect(x, y + h - th, l, th, col); c.Rect(x, y + h - l, th, l, col);
        c.Rect(x + w - l, y + h - th, l, th, col); c.Rect(x + w - th, y + h - l, th, l, col);
    }

    /// <summary>A straight line (a thin rotated rectangle).</summary>
    public static void Line(ICanvas c, float x1, float y1, float x2, float y2, float th, HudColor col)
    {
        float dx = x2 - x1, dy = y2 - y1, len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.5f) return;
        c.Rect((x1 + x2) / 2 - len / 2, (y1 + y2) / 2 - th / 2, len, th, col, (float)Math.Atan2(dy, dx));
    }
}
