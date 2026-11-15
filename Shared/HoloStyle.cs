using System;
using static ProtogenDocking.StyleKit;

namespace ProtogenDocking;

/// <summary>
/// "Holo Line": thin cyan lines, corner ticks, amber for what needs fixing.
///
///   full screen:  [ title . live       connector > dock         PHASE ]
///                 [ live picture (about 78% wide)     | offset scope  ]
///                 [                                   | align  roll   ]
///                 [                                   | range         ]
///                 [                                   | closing       ]
///                 [ NEXT >  next step                        contact  ]
///
///   PiP / screens: the picture fills it; title and phase across the top, scope, offsets, range and closing speed over
///   a dark fade at the bottom, the next step under them.
/// </summary>
public static class HoloStyle
{
    public static readonly DockTheme Palette = new DockTheme
    {
        Main = new HudColor(95, 225, 255),
        Dim = new HudColor(52, 120, 140),
        Good = new HudColor(96, 236, 170),
        Warn = new HudColor(255, 200, 97),
        Bad = new HudColor(255, 96, 84),
        Text = new HudColor(230, 251, 255),
        Background = new HudColor(2, 6, 10),
        Font = "White",
        NumberFont = "White",
    };

    private static readonly HudColor Label = new HudColor(143, 220, 240);

    private static ViewLook Look(DockTheme t) => new ViewLook
    {
        Line = 1, Face = 1.8f, Cross = HudColor.White, CrossGap = 7, CrossArm = 15, Bracket = 1.6f,
        Outline = t.Warn.WithAlpha(0.6f), Tunnel = t.Main, Chevron = t.Warn,
    };

    // ------------------------------------------------------------------ layout

    private static float UFull(float w, float h) => Math.Max(0.3f, Math.Min(w / 1920f, h / 1080f));
    private static float UCompact(float w, float h) => Math.Max(0.35f, Math.Min(w / 560f, h / 320f));

    public static void Layout(DockScreen s, float w, float h)
    {
        var v = s.Full ? FullView(w, h, out _) : new Box(0, 0, w, h);
        s.ViewX = v.X; s.ViewY = v.Y; s.ViewW = v.W; s.ViewH = v.H;
    }

    private static Box FullView(float w, float h, out Box rail)
    {
        float u = UFull(w, h), m = 28 * u, gap = 28 * u, top = 64 * u, bottom = 86 * u;
        float railW = Math.Min(336 * u, w * 0.24f);
        var v = new Box(m, top, w - 2 * m - gap - railW, h - top - bottom);
        rail = new Box(v.R + gap, top, railW, v.H);
        return v;
    }

    public static void Draw(ICanvas c, DockReading r, DockScreen s, float w, float h)
    {
        if (s.Full) DrawFull(c, Palette, r, s, w, h);
        else DrawCompact(c, Palette, r, s, w, h);
    }

    // ------------------------------------------------------------------ full screen

    private static void DrawFull(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        float u = UFull(w, h);
        var v = FullView(w, h, out var rail);
        var (phase, level) = DockGuide.Phase(r);
        var pc = Level(t, level);
        float hair = Math.Max(1, u);

        Backing(c, s, v, w, h, t.Background, 0.96f);
        if (!s.LiveView) c.Rect(v.X, v.Y, v.W, v.H, HudColor.Black.WithAlpha(0.6f));
        ViewLines(c, t, r, s, v, u, pc, Look(t));
        Frame(c, v, hair, t.Main.WithAlpha(0.3f));
        Corners(c, v, 10 * u, 22 * u, Math.Max(1.5f, 2 * u), t.Main);

        // header
        float hy = 16 * u, hp = 21 * u;
        c.Text("DOCKING ASSIST", 28 * u, hy + 4 * u, hp, t.Text, TextAlign.Left, StyleKit.Mono);
        float tx = 28 * u + c.TextWidth("DOCKING ASSIST", hp, StyleKit.Mono) + 14 * u;
        if (s.LiveView)
        {
            float d = 8 * u;
            c.Shape(HudShape.Circle, tx, hy + 4 * u + hp / 2 - d / 2, d, d, t.Main);
            c.Text("LIVE", tx + d + 8 * u, hy + 7 * u, 15 * u, t.Main, TextAlign.Left, StyleKit.Mono);
        }
        float bw = Badge(c, phase, w - 28 * u, hy, 16 * u, 18 * u, 6 * u, pc.WithAlpha(level == 3 || level == 1 || Blink(s) ? 1f : 0.55f), t.Background, true);
        float pairW = w - 2 * (28 * u) - 2 * Math.Max(bw, 380 * u) - 40 * u;
        string pair = Fit(c, Pair(r), 16 * u, Math.Max(100 * u, pairW));
        c.Text(pair, w / 2, hy + 7 * u, 16 * u, Label, TextAlign.Center, StyleKit.Mono);

        Rail(c, t, r, s, rail, u);

        // footer: the next step
        float fy = h - 72 * u;
        c.Rect(28 * u, fy, w - 56 * u, hair, t.Main.WithAlpha(0.28f));
        string next = "NEXT  >";
        float np = 15 * u, ap = 25 * u, ty = fy + 22 * u;
        c.Text(next, 28 * u, ty + (ap - np) / 2, np, t.Main, TextAlign.Left, StyleKit.Mono);
        float ax = 28 * u + c.TextWidth(next, np, StyleKit.Mono) + 18 * u;
        string eta = Eta(r);
        float ew = eta.Length > 0 ? c.TextWidth(eta, np, StyleKit.Mono) + 24 * u : 0;
        string advice = DockGuide.Advice(r);
        float apx = FitPx(c, advice, ap, w - 28 * u - ew - ax, 10);
        c.Text(advice, ax, ty + (ap - apx) / 2, apx, level == 3 ? t.Text : pc, TextAlign.Left, StyleKit.Mono);
        if (eta.Length > 0) c.Text(eta, w - 28 * u, ty + (ap - np) / 2, np, Label, TextAlign.Right, StyleKit.Mono);
    }

    /// <summary>The side rail: the offset scope and its numbers, align and roll, range and closing speed.</summary>
    private static void Rail(ICanvas c, DockTheme t, DockReading r, DockScreen s, Box b, float u)
    {
        bool has = r != null && r.HasTarget;
        float hair = Math.Max(1, u), lp = 14 * u, y = b.Y;
        c.Text("OFFSET", b.X, y, lp, Label, TextAlign.Left, StyleKit.Mono);
        if (has) c.Text(Offsets(r), b.R, y, lp, Label, TextAlign.Right, StyleKit.Mono);
        y += lp + 10 * u;
        float sc = Math.Min(b.W, b.H * 0.4f);
        Scope(c, t, r, s, b.X + (b.W - sc) / 2, y, sc, u, true);
        y += sc + 10 * u;
        double side = Side(r);
        c.Text(!has ? "--" : side < 0.1 ? "ON AXIS" : DockGuide.M(side), b.X, y, 36 * u, !has ? t.Dim : side <= DockGuide.LockDistance ? t.Good : t.Text, TextAlign.Left, StyleKit.Mono);
        y += 36 * u + 18 * u;

        c.Rect(b.X, y, b.W, hair, t.Main.WithAlpha(0.2f));
        y += 16 * u;
        float half = b.W / 2;
        c.Text("ALIGN", b.X, y, 13 * u, Label, TextAlign.Left, StyleKit.Mono);
        c.Text("ROLL", b.X + half, y, 13 * u, Label, TextAlign.Left, StyleKit.Mono);
        y += 17 * u;
        c.Text(has ? Deg(r.Misalign) : "--", b.X, y, 30 * u, has ? AlignColor(t, r) : t.Dim, TextAlign.Left, StyleKit.Mono);
        c.Text(s.Roll >= 0 ? Deg(s.Roll) : "--", b.X + half, y, 30 * u, s.Roll >= 0 ? RollColor(t, s) : t.Dim, TextAlign.Left, StyleKit.Mono);
        y += 30 * u + 18 * u;

        // range: a hairline with the log scale's ticks, filled to the range
        c.Rect(b.X, y, b.W, hair, t.Main.WithAlpha(0.2f));
        y += 16 * u;
        c.Text("RANGE", b.X, y + 12 * u, 13 * u, Label, TextAlign.Left, StyleKit.Mono);
        c.Text(has ? DockGuide.M(r.Range) : "--", b.R, y, 32 * u, t.Text, TextAlign.Right, StyleKit.Mono);
        y += 32 * u + 10 * u;
        c.Rect(b.X, y + 9 * u, b.W, hair, t.Main.WithAlpha(0.35f));
        foreach (var (m, lab) in new[] { (1.0, "1"), (5.0, "5"), (20.0, "20"), (100.0, "100") })
        {
            float x = b.X + b.W * RangeAt(m);
            c.Rect(x, y + 2 * u, hair, 8 * u, t.Main.WithAlpha(0.5f));
            c.Text(lab, x, y + 15 * u, 11 * u, t.Dim, TextAlign.Center, StyleKit.Mono);
        }
        if (has)
        {
            float f = RangeAt(r.Range);
            c.Rect(b.X, y + 8 * u, b.W * f, Math.Max(2, 2.5f * u), r.Range < 5 ? t.Good : t.Main);
            c.Shape(HudShape.Circle, b.X + b.W * f - 4 * u, y + 5.5f * u, 8 * u, 8 * u, r.Range < 5 ? t.Good : t.Main);
        }
        y += 34 * u + 10 * u;

        // closing speed: filled to the speed, the safe speed marked in amber
        c.Rect(b.X, y, b.W, hair, t.Main.WithAlpha(0.2f));
        y += 16 * u;
        c.Text("CLOSING", b.X, y + 12 * u, 13 * u, Label, TextAlign.Left, StyleKit.Mono);
        var vc = SpeedColor(t, r);
        string sp = SpeedText(r);
        c.Text(sp, b.R, y, FitPx(c, sp, 32 * u, b.W * 0.68f, 10), has ? vc : t.Dim, TextAlign.Right, StyleKit.Mono);
        y += 32 * u + 10 * u;
        c.Rect(b.X, y + 9 * u, b.W, hair, t.Main.WithAlpha(0.35f));
        if (has)
        {
            var (_, safe, now) = SpeedScale(r);
            c.Rect(b.X, y + 8 * u, b.W * now, Math.Max(2, 2.5f * u), vc);
            c.Rect(b.X + b.W * safe - u, y + 1 * u, Math.Max(2, 2 * u), 16 * u, t.Warn);
            c.Text("SAFE " + DockGuide.Mps(r.Safe), b.X, y + 22 * u, 12 * u, t.Warn, TextAlign.Left, StyleKit.Mono);
        }
    }

    // ------------------------------------------------------------------ PiP and screens

    private static void DrawCompact(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        float u = UCompact(w, h);
        var v = new Box(0, 0, w, h);
        bool tall = h > w * 0.8f, has = r != null && r.HasTarget;
        var (phase, level) = DockGuide.Phase(r);
        var pc = Level(t, level);
        float hair = Math.Max(1, u), pad = 12 * u;

        if (!s.LiveView) c.Rect(0, 0, w, h, t.Background.WithAlpha(0.97f));
        ViewLines(c, t, r, s, v, u, pc, Look(t));
        Frame(c, v, hair, t.Main.WithAlpha(0.4f));

        // top: title and phase over a fade
        float th = 46 * u;
        Fade(c, 0, 0, w, th, t.Background, 0.88f, 0f, 8);
        float tp = Math.Max(9, 15 * u);
        float bw = Badge(c, phase, w - pad, 9 * u, Math.Max(8, 13 * u), 10 * u, 4 * u, pc.WithAlpha(level == 3 || level == 1 || Blink(s) ? 1f : 0.55f), t.Background, true);
        string title = "DOCK ASSIST";
        c.Text(title, pad, 12 * u, tp, t.Text, TextAlign.Left, StyleKit.Mono);
        float lx0 = pad + c.TextWidth(title, tp, StyleKit.Mono) + 10 * u, dot = Math.Max(4, 6 * u);
        if (s.LiveView && lx0 + dot + 6 * u + c.TextWidth("LIVE", tp * 0.8f, StyleKit.Mono) < w - bw - 2 * pad)
        {
            c.Shape(HudShape.Circle, lx0, 12 * u + tp / 2 - dot / 2, dot, dot, t.Main);
            c.Text("LIVE", lx0 + dot + 6 * u, 12 * u + tp * 0.1f, tp * 0.8f, t.Main, TextAlign.Left, StyleKit.Mono);
        }

        // bottom: readouts over a fade
        float bh = (tall ? 220 : 136) * u;
        Fade(c, 0, h - bh, w, bh * 0.45f, t.Background, 0f, 0.86f, 8);
        c.Rect(0, h - bh * 0.55f, w, bh * 0.55f + 1, t.Background.WithAlpha(0.88f));

        float nextH = (tall ? 40 : 32) * u;
        float sz = (tall ? 120 : 92) * u, sy = h - nextH - 10 * u - sz;
        Scope(c, t, r, s, pad, sy, sz, u, false);

        float lx = pad + sz + 14 * u, small = Math.Max(8, 12 * u);
        string ar = (has ? "ALIGN " + Deg(r.Misalign) : "ALIGN --") + (s.Roll >= 0 ? "   ROLL " + Deg(s.Roll) : "");
        double side = Side(r);
        string off = !has ? "--" : side < 0.1 ? "ON AXIS" : DockGuide.M(side);
        var offCol = !has ? t.Dim : side <= DockGuide.LockDistance ? t.Good : t.Text;

        // the big numbers on the right
        string rv = has ? DockGuide.M(r.Range) : "--", cv = SpeedText(r);
        float big = (tall ? 34 : 30) * u;
        if (tall)
        {
            float ry = sy - 6 * u;
            c.Text("RANGE", w - pad, ry, small, Label, TextAlign.Right, StyleKit.Mono);
            c.Text(rv, w - pad, ry + small + 2 * u, big, t.Text, TextAlign.Right, StyleKit.Mono);
            float cy = ry + small + big + 10 * u;
            c.Text(has ? "CLOSING  /  SAFE " + DockGuide.Mps(r.Safe) : "CLOSING", w - pad, cy, small, has ? t.Warn : Label, TextAlign.Right, StyleKit.Mono);
            c.Text(cv, w - pad, cy + small + 2 * u, FitPx(c, cv, big, w * 0.42f, 9), has ? SpeedColor(t, r) : t.Dim, TextAlign.Right, StyleKit.Mono);

            float oy = sy + 8 * u;
            c.Text("OFFSET", lx, oy, small, Label, TextAlign.Left, StyleKit.Mono);
            c.Text(off, lx, oy + small + 3 * u, 22 * u, offCol, TextAlign.Left, StyleKit.Mono);
            c.Text(Offsets(r), lx, oy + small + 28 * u, small, Label, TextAlign.Left, StyleKit.Mono);
            c.Text(Fit(c, ar, small, w * 0.5f), lx, oy + 2 * small + 34 * u, small, s.Roll >= 8 ? t.Warn : Label, TextAlign.Left, StyleKit.Mono);
        }
        else
        {
            float ny = sy + sz - big - small - 6 * u;
            float cw = Math.Max(c.TextWidth(cv, big, StyleKit.Mono), c.TextWidth("CLOSING / 0.0 M/S", small, StyleKit.Mono));
            float cx2 = w - pad, rx = cx2 - cw - 22 * u;
            c.Text(has ? "CLOSING / " + DockGuide.Mps(r.Safe) : "CLOSING", cx2, ny, small, has ? t.Warn : Label, TextAlign.Right, StyleKit.Mono);
            c.Text(cv, cx2, ny + small + 2 * u, big, has ? SpeedColor(t, r) : t.Dim, TextAlign.Right, StyleKit.Mono);
            c.Text("RANGE", rx, ny, small, Label, TextAlign.Right, StyleKit.Mono);
            c.Text(rv, rx, ny + small + 2 * u, big, t.Text, TextAlign.Right, StyleKit.Mono);

            float room = rx - c.TextWidth(rv, big, StyleKit.Mono) - 14 * u - lx;
            if (room > 40 * u)
            {
                c.Text(Fit(c, ar, small, room), lx, ny, small, s.Roll >= 8 ? t.Warn : Label, TextAlign.Left, StyleKit.Mono);
                c.Text(off, lx, ny + small + 4 * u, FitPx(c, off, 20 * u, room, 8), offCol, TextAlign.Left, StyleKit.Mono);
            }
        }

        // the next step
        float fy = h - nextH;
        c.Rect(pad, fy, w - 2 * pad, hair, t.Main.WithAlpha(0.3f));
        float np = Math.Max(8, 12 * u), ap = Math.Max(9, (tall ? 17 : 15) * u);
        c.Text("NEXT", pad, fy + (nextH - np) / 2, np, t.Main, TextAlign.Left, StyleKit.Mono);
        float ax = pad + c.TextWidth("NEXT", np, StyleKit.Mono) + 10 * u;
        string advice = DockGuide.Advice(r);
        float apx = FitPx(c, advice, ap, w - pad - ax, 8);
        c.Text(advice, ax, fy + (nextH - apx) / 2, apx, level == 3 ? t.Text : pc, TextAlign.Left, StyleKit.Mono);
    }

    // ------------------------------------------------------------------ the offset scope

    /// <summary>
    /// Where the dock sits across your connector's axis (log scale, 10 m at the edge), its ring coloured by how square you
    /// face it, your roll as a marker on top; detailed adds the 0.5 m / 2 m rings, the lock zone and where it'll be in 3 s.
    /// </summary>
    private static void Scope(ICanvas c, DockTheme t, DockReading r, DockScreen s, float x, float y, float size, float u, bool detailed)
    {
        bool has = r != null && r.HasTarget;
        float cx = x + size / 2, cy = y + size / 2, R = size / 2 - Math.Max(2, 5 * u) - (detailed ? 6 * u : 0);
        float hair = Math.Max(1, u);
        if (!detailed) c.Shape(HudShape.Circle, cx - R, cy - R, 2 * R, 2 * R, t.Background.WithAlpha(0.55f));
        var ring = has ? AlignColor(t, r) : t.Main.WithAlpha(0.5f);
        Ring(c, cx, cy, R, Math.Max(1, 1.3f * u), has ? ring.WithAlpha(0.8f) : ring);
        if (detailed)
        {
            Ring(c, cx, cy, Map(2.0, R), hair, t.Main.WithAlpha(0.22f));
            Ring(c, cx, cy, Map(0.5, R), hair, t.Main.WithAlpha(0.22f));
            float lz = Map(DockGuide.LockDistance, R);
            c.Shape(HudShape.Circle, cx - lz, cy - lz, 2 * lz, 2 * lz, t.Good.WithAlpha(0.14f));
        }
        c.Rect(cx - R, cy - hair / 2, 2 * R, hair, t.Main.WithAlpha(0.2f));
        c.Rect(cx - hair / 2, cy - R, hair, 2 * R, t.Main.WithAlpha(0.2f));
        // roll: a marker on the ring off the top tick by the roll angle
        c.Rect(cx - hair / 2, cy - R, hair, Math.Max(3, 6 * u), HudColor.White);
        if (s.Roll >= 0.5)
        {
            double a = -Math.PI / 2 + s.Roll * Math.PI / 180 * 2;
            float m = Math.Max(5, 9 * u), mx = cx + (float)Math.Cos(a) * (R + m * 0.6f), my = cy + (float)Math.Sin(a) * (R + m * 0.6f);
            c.Shape(HudShape.Triangle, mx - m / 2, my - m / 2, m, m, RollColor(t, s), (float)(a - Math.PI / 2));
        }
        if (!has) return;

        var (px, py) = Spot(r.OffRight, r.OffUp, cx, cy, R);
        double side = Side(r);
        var dc = side <= DockGuide.LockDistance ? t.Good : side < 2 ? t.Main : t.Warn;
        if (detailed)
        {
            var (fx, fy) = Spot(r.OffRight - r.DriftRight * 3, r.OffUp - r.DriftUp * 3, cx, cy, R);
            if (Math.Abs(fx - px) + Math.Abs(fy - py) > 3 * u)
            {
                DockCamRenderer.Line(c, px, py, fx, fy, hair, t.Text.WithAlpha(0.5f));
                float e = Math.Max(3, 5 * u);
                c.Rect(fx - e / 2, fy - e / 2, e, e, t.Text.WithAlpha(0.7f), (float)(Math.PI / 4));
            }
            if (Math.Abs(px - cx) + Math.Abs(py - cy) > 6 * u) DockCamRenderer.Line(c, cx, cy, px, py, hair, dc.WithAlpha(0.5f));
            Ring(c, px, py, Math.Max(5, 10 * u), hair, dc.WithAlpha(0.5f), 20);
        }
        float d = Math.Max(5, (detailed ? 10 : 9) * u);
        c.Shape(HudShape.Circle, px - d / 2, py - d / 2, d, d, dc);
    }
}
