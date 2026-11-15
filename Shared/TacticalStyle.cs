using System;
using static ProtogenDocking.StyleKit;

namespace ProtogenDocking;

/// <summary>
/// "Tactical Phosphor": green phosphor on black, boxed readouts, segmented bar gauges, filled tags.
///
///   full screen:  [ DOCK/ASSIST [LIVE]  connector > dock             PHASE [APPROACH] ]
///                 [ offset grid  | live picture                                         ]
///                 [ align  roll  |                                                      ]
///                 [ range  close |                                                      ]
///                 [ [NEXT] next step                                           contact  ]
///
///   PiP / screens: the picture fills it; a title bar on top, range and closing bars down the left, the numbers in a
///   box (top right on wide screens; bottom right with a small offset grid on tall ones), the next step along the bottom.
/// </summary>
public static class TacticalStyle
{
    public static readonly DockTheme Palette = new DockTheme
    {
        Main = new HudColor(125, 255, 168),
        Dim = new HudColor(58, 118, 80),
        Good = new HudColor(150, 255, 220),
        Warn = new HudColor(255, 193, 77),
        Bad = new HudColor(255, 90, 72),
        Text = new HudColor(216, 255, 228),
        Background = new HudColor(2, 6, 4),
        Font = "White",
        NumberFont = "White",
    };

    private static readonly HudColor Label = new HudColor(111, 216, 148);

    private static ViewLook Look(DockTheme t) => new ViewLook
    {
        Line = 1, Face = 2, Cross = t.Text, CrossGap = 6, CrossArm = 14, Bracket = 1.6f,
        Outline = t.Warn.WithAlpha(0.55f), Tunnel = t.Main, Chevron = t.Warn,
    };

    // ------------------------------------------------------------------ layout

    private static float UFull(float w, float h) => Math.Max(0.3f, Math.Min(w / 1920f, h / 1080f));

    private static float UCompact(float w, float h, bool tall) =>
        Math.Max(0.35f, tall ? Math.Min(w / 520f, h / 520f) : Math.Min(w / 620f, h / 350f));

    public static void Layout(DockScreen s, float w, float h)
    {
        var v = s.Full ? FullView(w, h, out _) : new Box(0, 0, w, h);
        s.ViewX = v.X; s.ViewY = v.Y; s.ViewW = v.W; s.ViewH = v.H;
    }

    private static Box FullView(float w, float h, out Box rail)
    {
        float u = UFull(w, h), m = 24 * u, top = 66 * u, foot = 82 * u;
        rail = new Box(m, top, 232 * u, h - foot - 16 * u - top);
        float vx = rail.R + 18 * u;
        return new Box(vx, top, w - m - vx, rail.H);
    }

    public static void Draw(ICanvas c, DockReading r, DockScreen s, float w, float h)
    {
        if (s.Full) DrawFull(c, Palette, r, s, w, h);
        else DrawCompact(c, Palette, r, s, w, h);
    }

    private static HudColor PhaseCol(DockTheme t, int level) => level == 3 ? t.Main : Level(t, level);

    // ------------------------------------------------------------------ full screen

    private static void DrawFull(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        float u = UFull(w, h), m = 24 * u, hair = Math.Max(1, u);
        var v = FullView(w, h, out var rail);
        var (phase, level) = DockGuide.Phase(r);
        var pc = PhaseCol(t, level);
        bool has = r != null && r.HasTarget;

        Backing(c, s, v, w, h, t.Background, 0.97f);
        if (!s.LiveView) c.Rect(v.X, v.Y, v.W, v.H, HudColor.Black.WithAlpha(0.6f));
        ViewLines(c, t, r, s, v, u, Level(t, level), Look(t));
        Frame(c, v, hair, t.Main.WithAlpha(0.4f));
        // edge ticks at the quarters
        for (int i = 1; i <= 3; i++)
        {
            float l = (i == 2 ? 14 : 9) * u;
            c.Rect(v.X, v.Y + v.H * i / 4, l, hair, t.Main.WithAlpha(0.6f));
            c.Rect(v.R - l, v.Y + v.H * i / 4, l, hair, t.Main.WithAlpha(0.6f));
            c.Rect(v.X + v.W * i / 4, v.Y, hair, l, t.Main.WithAlpha(0.6f));
            c.Rect(v.X + v.W * i / 4, v.B - l, hair, l, t.Main.WithAlpha(0.6f));
        }
        float cp = 14 * u;
        c.Text("CAM  /  " + (s.LiveView ? "LIVE FEED" : "NO FEED"), v.X + 14 * u, v.Y + 10 * u, cp, Label, TextAlign.Left, StyleKit.Mono);
        if (has && s.Edges.Exists(e => e.Kind == 0))
        {
            float tw = c.TextWidth("TARGET HULL", cp, StyleKit.Mono), sq = 10 * u;
            c.Text("TARGET HULL", v.R - 14 * u, v.Y + 10 * u, cp, t.Warn, TextAlign.Right, StyleKit.Mono);
            Frame(c, new Box(v.R - 14 * u - tw - sq - 8 * u, v.Y + 10 * u + (cp - sq) / 2, sq, sq), hair, t.Warn);
        }

        // header
        float hy = 16 * u, hh = 34 * u;
        c.Rect(m, hy + hh, w - 2 * m, hair, t.Main.WithAlpha(0.35f));
        float tp = 20 * u, ty = hy + (hh - tp) / 2;
        c.Text("DOCK/ASSIST", m, ty, tp, t.Text, TextAlign.Left, StyleKit.Mono);
        float x = m + c.TextWidth("DOCK/ASSIST", tp, StyleKit.Mono) + 20 * u;
        if (s.LiveView) x += Badge(c, "LIVE", x, hy + (hh - 19 * u) / 2, 15 * u, 8 * u, 2 * u, t.Main, t.Background, false) + 20 * u;
        float bw = OutlineBadge(c, phase, w - m, hy + (hh - 24 * u) / 2, 18 * u, 12 * u, 3 * u, hair, pc.WithAlpha(level == 3 || level == 1 || Blink(s) ? 1f : 0.55f), t.Text, true);
        float px = w - m - bw - 14 * u;
        c.Text("PHASE", px, hy + (hh - 15 * u) / 2, 15 * u, Label, TextAlign.Right, StyleKit.Mono);
        float pairRoom = px - c.TextWidth("PHASE", 15 * u, StyleKit.Mono) - 30 * u - x;
        if (pairRoom > 60 * u) c.Text(Fit(c, Pair(r), 15 * u, pairRoom), x, hy + (hh - 15 * u) / 2, 15 * u, Label, TextAlign.Left, StyleKit.Mono);

        Rail(c, t, r, s, rail, u);

        // footer: the next step
        float fy = h - 82 * u, fh = 62 * u;
        c.Rect(m, fy, w - 2 * m, hair, t.Main.WithAlpha(0.35f));
        float nb = Badge(c, "NEXT", m, fy + (fh - 20 * u) / 2 + 2 * u, 16 * u, 10 * u, 2 * u, t.Main, t.Background, false);
        float ax = m + nb + 18 * u;
        string eta = Eta(r);
        float ew = eta.Length > 0 ? c.TextWidth(eta, 15 * u, StyleKit.Mono) + 24 * u : 0;
        string advice = DockGuide.Advice(r);
        float ap = FitPx(c, advice, 24 * u, w - m - ew - ax, 10);
        c.Text(advice, ax, fy + (fh - ap) / 2 + 2 * u, ap, level == 3 ? t.Text : Level(t, level), TextAlign.Left, StyleKit.Mono);
        if (eta.Length > 0) c.Text(eta, w - m, fy + (fh - 15 * u) / 2 + 2 * u, 15 * u, Label, TextAlign.Right, StyleKit.Mono);
    }

    /// <summary>The left rail: offset grid, align and roll boxes, range and closing bar gauges.</summary>
    private static void Rail(ICanvas c, DockTheme t, DockReading r, DockScreen s, Box b, float u)
    {
        bool has = r != null && r.HasTarget;
        float hair = Math.Max(1, u), pad = 10 * u, y = b.Y;
        var box = t.Main.WithAlpha(0.3f);

        // offset
        float sc = b.W - 2 * pad;
        var ob = new Box(b.X, y, b.W, pad + 13 * u + 6 * u + sc + 6 * u + 30 * u + pad);
        c.Rect(ob.X, ob.Y, ob.W, ob.H, t.Background.WithAlpha(0.5f));
        Frame(c, ob, hair, box);
        c.Text("OFFSET", ob.X + pad, y + pad, 13 * u, Label, TextAlign.Left, StyleKit.Mono);
        if (has) c.Text(Offsets(r), ob.R - pad, y + pad, 13 * u, t.Text, TextAlign.Right, StyleKit.Mono);
        Scope(c, t, r, ob.X + pad, y + pad + 19 * u, sc, u, true);
        double side = Side(r);
        string off = !has ? "--" : side < 0.1 ? "ON AXIS" : DockGuide.M(side);
        ValUnit(c, off, ob.X + pad, ob.B - pad - 30 * u, 30 * u, 15 * u, !has ? t.Dim : side <= DockGuide.LockDistance ? t.Good : t.Text, Label, false);
        y = ob.B + 18 * u;

        // align / roll
        float half = (b.W - 10 * u) / 2, bh = 8 * u + 12 * u + 4 * u + 24 * u + 8 * u;
        var rc = s.Roll >= 2 ? RollColor(t, s) : box;
        var ac = has && r.Misalign > DockGuide.LockAngle * 0.5 ? AlignColor(t, r) : box;
        Cell(c, t, new Box(b.X, y, half, bh), "ALIGN", has ? Deg(r.Misalign) : "--", ac, has ? AlignColor(t, r) : t.Dim, u);
        Cell(c, t, new Box(b.X + half + 10 * u, y, half, bh), "ROLL", s.Roll >= 0 ? Deg(s.Roll) : "--", rc, s.Roll >= 0 ? RollColor(t, s) : t.Dim, u);
        y += bh + 18 * u;

        // range and closing gauges
        float gh = b.B - y;
        var rb = new Box(b.X, y, half, gh);
        var cb = new Box(b.X + half + 10 * u, y, half, gh);
        foreach (var g in new[] { rb, cb })
        {
            c.Rect(g.X, g.Y, g.W, g.H, t.Background.WithAlpha(0.5f));
            Frame(c, g, hair, box);
        }
        int n = 16;
        var rcol = has && r.Range < 5 ? t.Good : t.Main;
        c.Text("RANGE", rb.X + pad, y + pad, 12 * u, Label, TextAlign.Left, StyleKit.Mono);
        c.Text(has ? DockGuide.M(r.Range).Replace(" M", "") : "--", rb.X + pad, y + pad + 20 * u, 24 * u, t.Text, TextAlign.Left, StyleKit.Mono);
        var barR = new Box(rb.X + pad, y + pad + 52 * u, rb.W - 2 * pad, gh - 2 * pad - 52 * u - 20 * u);
        SegBar(c, barR, n, RangeLit(r, n), 3 * u, true, rcol, t.Main.WithAlpha(0.14f));
        c.Text("M  /  200 MAX", rb.X + pad, rb.B - pad - 11 * u, 11 * u, Label, TextAlign.Left, StyleKit.Mono);

        var vc = SpeedColor(t, r);
        c.Text("CLOSING", cb.X + pad, y + pad, 12 * u, Label, TextAlign.Left, StyleKit.Mono);
        string sp = SpeedText(r).Replace(" M/S", "");
        c.Text(sp, cb.X + pad, y + pad + 20 * u, FitPx(c, sp, 24 * u, cb.W - 2 * pad, 9), has ? vc : t.Dim, TextAlign.Left, StyleKit.Mono);
        var barC = new Box(cb.X + pad, barR.Y, cb.W - 2 * pad, barR.H);
        int lit = 0, mark = -1;
        if (has)
        {
            var (_, safe, now) = SpeedScale(r);
            lit = (int)Math.Round(n * now);
            mark = Math.Min(n - 1, (int)(n * safe));
        }
        SegBar(c, barC, n, lit, 3 * u, true, vc, t.Main.WithAlpha(0.14f), mark, t.Warn);
        c.Text(has ? "M/S  /  SAFE " + DockGuide.Mps(r.Safe).Replace(" M/S", "") : "M/S", cb.X + pad, cb.B - pad - 11 * u, 11 * u, t.Warn, TextAlign.Left, StyleKit.Mono);
    }

    private static void Cell(ICanvas c, DockTheme t, Box b, string label, string value, HudColor line, HudColor valCol, float u)
    {
        c.Rect(b.X, b.Y, b.W, b.H, t.Background.WithAlpha(0.5f));
        Frame(c, b, Math.Max(1, u), line);
        c.Text(label, b.X + 10 * u, b.Y + 8 * u, 12 * u, line.A == 255 ? line : Label, TextAlign.Left, StyleKit.Mono);
        c.Text(value, b.X + 10 * u, b.Y + 24 * u, 24 * u, valCol, TextAlign.Left, StyleKit.Mono);
    }

    // ------------------------------------------------------------------ PiP and screens

    private static void DrawCompact(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        bool tall = h > w * 0.8f, has = r != null && r.HasTarget;
        float u = UCompact(w, h, tall), hair = Math.Max(1, u), pad = 10 * u;
        var v = new Box(0, 0, w, h);
        var (phase, level) = DockGuide.Phase(r);
        var pc = PhaseCol(t, level);
        var line = t.Main.WithAlpha(0.4f);

        if (!s.LiveView) c.Rect(0, 0, w, h, t.Background.WithAlpha(0.97f));
        ViewLines(c, t, r, s, v, u, Level(t, level), Look(t));
        Frame(c, v, hair, t.Main.WithAlpha(0.5f));

        // title bar
        float th = (tall ? 32 : 30) * u;
        c.Rect(0, 0, w, th, t.Background.WithAlpha(0.85f));
        c.Rect(0, th, w, hair, line);
        float tp = Math.Max(8, (tall ? 16 : 15) * u);
        float bp = Math.Max(7, 14 * u);
        float bw = OutlineBadge(c, phase, w - pad, (th - bp - 2 * u) / 2, bp, 8 * u, u, hair, pc.WithAlpha(level == 3 || level == 1 || Blink(s) ? 1f : 0.55f), t.Text, true);
        c.Text("DOCK/ASSIST", pad, (th - tp) / 2, tp, t.Text, TextAlign.Left, StyleKit.Mono);
        float lx = pad + c.TextWidth("DOCK/ASSIST", tp, StyleKit.Mono) + 10 * u, lp = Math.Max(7, 12 * u);
        if (s.LiveView && lx + c.TextWidth("LIVE", lp, StyleKit.Mono) + 12 * u < w - bw - 2 * pad)
            Badge(c, "LIVE", lx, (th - lp - 2 * u) / 2, lp, 6 * u, u, t.Main, t.Background, false);

        // the next step
        float nh = (tall ? 42 : 40) * u, ny = h - nh;
        c.Rect(0, ny, w, nh, t.Background.WithAlpha(0.88f));
        c.Rect(0, ny, w, hair, line);
        float nb = Badge(c, "NEXT", pad, ny + (nh - lp - 2 * u) / 2, lp, 6 * u, u, t.Main, t.Background, false);
        float ax = pad + nb + 10 * u;
        string advice = DockGuide.Advice(r);
        float ap = FitPx(c, advice, Math.Max(8, (tall ? 16 : 15) * u), w - pad - ax, 7);
        c.Text(advice, ax, ny + (nh - ap) / 2, ap, level == 3 ? t.Text : Level(t, level), TextAlign.Left, StyleKit.Mono);

        // range and closing bars down the left
        float cw = (tall ? 92 : 78) * u, cy = th + hair, ch = ny - cy;
        c.Rect(0, cy, cw, ch, t.Background.WithAlpha(0.8f));
        c.Rect(cw, cy, hair, ch, t.Main.WithAlpha(0.35f));
        float gpad = (tall ? 10 : 8) * u, gap = (tall ? 8 : 6) * u, bwid = (cw - 2 * 8 * u - gap) / 2, sl = Math.Max(7, (tall ? 11 : 10) * u);
        int n = tall ? 12 : 10;
        var barR = new Box(8 * u, cy + gpad + sl + 4 * u, bwid, ch - 2 * gpad - sl - 4 * u);
        var barC = new Box(8 * u + bwid + gap, barR.Y, bwid, barR.H);
        c.Text("RNG", barR.CX, cy + gpad, sl, Label, TextAlign.Center, StyleKit.Mono);
        c.Text("CLS", barC.CX, cy + gpad, sl, Label, TextAlign.Center, StyleKit.Mono);
        float sg = (tall ? 3 : 2) * u;
        SegBar(c, barR, n, RangeLit(r, n), sg, true, has && r.Range < 5 ? t.Good : t.Main, t.Main.WithAlpha(0.14f));
        var vc = SpeedColor(t, r);
        int lit = 0, mark = -1;
        if (has)
        {
            var (_, safe, now) = SpeedScale(r);
            lit = (int)Math.Round(n * now);
            mark = Math.Min(n - 1, (int)(n * safe));
        }
        SegBar(c, barC, n, lit, sg, true, vc, t.Main.WithAlpha(0.14f), mark, t.Warn);

        // the numbers in a box
        float big = Math.Max(10, (tall ? 30 : 24) * u), unit = Math.Max(7, (tall ? 13 : 12) * u), sm = Math.Max(7, (tall ? 14 : 13) * u);
        double side = Side(r);
        string l1 = has ? "OFF " + (side < 0.1 ? "0" : DockGuide.M(side).Replace(" M", "")) + "  /  AL " + Deg(r.Misalign) : "OFF --  /  AL --";
        string l2 = (s.Roll >= 0 ? "ROLL " + Deg(s.Roll) : "ROLL --") + (tall && has ? "  /  SAFE " + DockGuide.Mps(r.Safe).Replace(" M/S", "") : "");
        string rv = has ? DockGuide.M(r.Range) : "--", cv = SpeedText(r);
        float bx2 = 8 * u, by2 = 6 * u;
        float inner = Math.Max(Math.Max(Measure(c, rv, big, unit), Measure(c, cv, big, unit)), Math.Max(c.TextWidth(l1, sm, StyleKit.Mono), c.TextWidth(l2, sm, StyleKit.Mono)));
        inner = Math.Min(inner, w - cw - 30 * u);
        float boxW = inner + 2 * (tall ? 10 : 8) * u, boxH = 2 * big + 2 * sm + by2 * 2 + 6 * u;
        var nb2 = new Box(w - bx2 - boxW, tall ? ny - 10 * u - boxH : th + 8 * u, boxW, boxH);
        c.Rect(nb2.X, nb2.Y, nb2.W, nb2.H, t.Background.WithAlpha(tall ? 0.8f : 0.75f));
        Frame(c, nb2, hair, t.Main.WithAlpha(0.3f));
        float rx = nb2.R - (tall ? 10 : 8) * u, yy = nb2.Y + by2;
        ValUnit(c, rv, rx, yy, big, unit, has ? t.Text : t.Dim, Label, true); yy += big + 2 * u;
        ValUnit(c, cv, rx, yy, big, unit, has ? vc : t.Dim, Label, true); yy += big + 2 * u;
        c.Text(Fit(c, l1, sm, inner), rx, yy, sm, has && r.Misalign > DockGuide.LockAngle ? AlignColor(t, r) : Label, TextAlign.Right, StyleKit.Mono); yy += sm + 2 * u;
        c.Text(Fit(c, l2, sm, inner), rx, yy, sm, s.Roll >= 2 ? RollColor(t, s) : t.Warn, TextAlign.Right, StyleKit.Mono);

        // tall screens: a small offset grid beside the bars
        if (tall)
        {
            float sz = 104 * u, sx = cw + 12 * u;
            if (sx + sz < nb2.X - 8 * u) Scope(c, t, r, sx, ny - 10 * u - sz, sz, u, false);
        }
    }

    private static float Measure(ICanvas c, string text, float big, float small)
    {
        int sp = text.LastIndexOf(' ');
        return sp > 0 ? c.TextWidth(text.Substring(0, sp), big, StyleKit.Mono) + c.TextWidth(" " + text.Substring(sp + 1), small, StyleKit.Mono) : c.TextWidth(text, big, StyleKit.Mono);
    }

    // ------------------------------------------------------------------ the offset grid

    /// <summary>
    /// A square grid with the dock's offset across your axis (log scale, 10 m at the edge) as a square, dashed lines
    /// through it to the edges; detailed adds the quarter grid and the lock zone.
    /// </summary>
    private static void Scope(ICanvas c, DockTheme t, DockReading r, float x, float y, float size, float u, bool detailed)
    {
        bool has = r != null && r.HasTarget;
        float hair = Math.Max(1, u), cx = x + size / 2, cy = y + size / 2, R = size / 2 - (detailed ? 4 * u : 0);
        var b = new Box(cx - R, cy - R, 2 * R, 2 * R);
        if (!detailed)
        {
            c.Rect(x, y, size, size, t.Background.WithAlpha(0.75f));
            Frame(c, new Box(x, y, size, size), hair, t.Main.WithAlpha(0.3f));
        }
        else
        {
            Frame(c, b, hair, t.Main.WithAlpha(0.45f));
            c.Rect(b.X, cy - R / 2, b.W, hair, t.Main.WithAlpha(0.12f));
            c.Rect(b.X, cy + R / 2, b.W, hair, t.Main.WithAlpha(0.12f));
            c.Rect(cx - R / 2, b.Y, hair, b.H, t.Main.WithAlpha(0.12f));
            c.Rect(cx + R / 2, b.Y, hair, b.H, t.Main.WithAlpha(0.12f));
            float lz = Map(DockGuide.LockDistance, R);
            c.Rect(cx - lz, cy - lz, 2 * lz, 2 * lz, t.Good.WithAlpha(0.14f));
        }
        c.Rect(b.X, cy - hair / 2, b.W, hair, t.Main.WithAlpha(0.35f));
        c.Rect(cx - hair / 2, b.Y, hair, b.H, t.Main.WithAlpha(0.35f));
        if (!has) return;

        var (px, py) = Spot(r.OffRight, r.OffUp, cx, cy, R);
        double side = Side(r);
        var dc = side <= DockGuide.LockDistance ? t.Good : side < 2 ? t.Text : t.Warn;
        float dash = Math.Max(2, 2 * size / 100), gap = Math.Max(2, 3 * size / 100);
        Dashed(c, px, b.Y, px, b.B, hair, dash, gap, t.Text.WithAlpha(0.5f));
        Dashed(c, b.X, py, b.R, py, hair, dash, gap, t.Text.WithAlpha(0.5f));
        float m = Math.Max(6, size * (detailed ? 0.08f : 0.1f));
        Frame(c, new Box(px - m / 2, py - m / 2, m, m), Math.Max(1.5f, 2 * u), dc);
        if (detailed)
        {
            var (fx, fy) = Spot(r.OffRight - r.DriftRight * 3, r.OffUp - r.DriftUp * 3, cx, cy, R);
            if (Math.Abs(fx - px) + Math.Abs(fy - py) > 3 * u)
            {
                DockCamRenderer.Line(c, px, py, fx, fy, hair, t.Text.WithAlpha(0.5f));
                float e = Math.Max(3, 4 * u);
                c.Rect(fx - e / 2, fy - e / 2, e, e, t.Text.WithAlpha(0.7f));
            }
        }
    }
}
