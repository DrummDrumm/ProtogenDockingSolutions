using System;
using static ProtogenDocking.StyleKit;

namespace ProtogenDocking;

/// <summary>
/// "Vector Bracket": amber on near-black, cut-corner panels, corner brackets, an octagon offset scope.
///
///   full screen:  [ [DOCKING ASSIST]  LIVE  ------------------------  connector  >>  dock ]
///                 [ live picture (full width)                                     [PHASE] ]
///                 [ [ next step ] [ offset ] [ align / roll ] [ range ] [ closing ]       ]
///
///   PiP / screens: the picture fills it; title and phase tags on top, a panel along the bottom with the scope, the
///   next step and the numbers (tall screens put the next step on an amber bar under them).
/// </summary>
public static class BracketStyle
{
    public static readonly DockTheme Palette = new DockTheme
    {
        Main = new HudColor(255, 176, 58),
        Dim = new HudColor(122, 96, 60),
        Good = new HudColor(132, 232, 152),
        Warn = new HudColor(255, 226, 110),
        Bad = new HudColor(255, 86, 64),
        Text = new HudColor(255, 241, 218),
        Background = new HudColor(6, 5, 4),
        Font = "White",
        NumberFont = "White",
    };

    private static readonly HudColor Label = new HudColor(217, 161, 87);
    private static readonly HudColor Faint = new HudColor(168, 138, 98);

    private static ViewLook Look(DockTheme t) => new ViewLook
    {
        Line = 1, Face = 2.4f, Cross = HudColor.White, CrossGap = 7, CrossArm = 14, Bracket = 2.4f,
        Outline = HudColor.White.WithAlpha(0.35f), Tunnel = t.Main, Chevron = t.Text,
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

    private static Box FullView(float w, float h, out Box panels)
    {
        float u = UFull(w, h), m = 24 * u, top = 72 * u;
        panels = new Box(m, h - m - 178 * u, w - 2 * m, 178 * u);
        return new Box(m, top, w - 2 * m, panels.Y - 16 * u - top);
    }

    public static void Draw(ICanvas c, DockReading r, DockScreen s, float w, float h)
    {
        if (s.Full) DrawFull(c, Palette, r, s, w, h);
        else DrawCompact(c, Palette, r, s, w, h);
    }

    private static HudColor PhaseCol(DockTheme t, int level) => level == 3 ? t.Text : Level(t, level);

    private static string PairShort(DockReading r) =>
        r != null && r.HasTarget ? r.Mine.ToUpperInvariant() + "  »  " + r.Target.ToUpperInvariant() : Pair(r);

    /// <summary>L-shaped brackets in the corners of a box (top only when bottom is false).</summary>
    private static void CornerL(ICanvas c, Box b, float lx, float ly, float th, HudColor col, bool bottom)
    {
        c.Rect(b.X, b.Y, lx, th, col); c.Rect(b.X, b.Y, th, ly, col);
        c.Rect(b.R - lx, b.Y, lx, th, col); c.Rect(b.R - th, b.Y, th, ly, col);
        if (!bottom) return;
        c.Rect(b.X, b.B - th, lx, th, col); c.Rect(b.X, b.B - ly, th, ly, col);
        c.Rect(b.R - lx, b.B - th, lx, th, col); c.Rect(b.R - th, b.B - ly, th, ly, col);
    }

    /// <summary>A cut-corner panel with the amber rule along its top.</summary>
    private static void Panel(ICanvas c, DockTheme t, Box b, float u, float alpha)
    {
        float cut = 14 * u;
        CutPanel(c, b, cut, t.Background.WithAlpha(alpha));
        CutPanel(c, b, cut, t.Main.WithAlpha(0.07f));
        c.Rect(b.X + cut, b.Y, b.W - cut, Math.Max(1.5f, 2 * u), t.Main);
    }

    // ------------------------------------------------------------------ full screen

    private static void DrawFull(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        float u = UFull(w, h), m = 24 * u, hair = Math.Max(1, u);
        var v = FullView(w, h, out var pb);
        var (phase, level) = DockGuide.Phase(r);
        bool has = r != null && r.HasTarget;

        Backing(c, s, v, w, h, t.Background, 0.97f);
        if (!s.LiveView) c.Rect(v.X, v.Y, v.W, v.H, HudColor.Black.WithAlpha(0.6f));
        ViewLines(c, t, r, s, v, u, Level(t, level), Look(t));
        CornerL(c, new Box(v.X + 4 * u, v.Y + 4 * u, v.W - 8 * u, v.H - 8 * u), 90 * u, 72 * u, Math.Max(1.5f, 2 * u), t.Main, true);

        // phase tag in the picture's top right
        float pp = 16 * u, lp = 13 * u, padX = 14 * u, padY = 8 * u;
        float pw = c.TextWidth(phase, pp, StyleKit.Mono), lw = c.TextWidth("PHASE", lp, StyleKit.Mono);
        var tag = new Box(v.R - 24 * u - (pw + lw + 10 * u + 2 * padX), v.Y + 20 * u, pw + lw + 10 * u + 2 * padX, pp + 2 * padY);
        CutPanel(c, tag, 8 * u, t.Background.WithAlpha(0.75f));
        c.Text("PHASE", tag.X + padX, tag.Y + padY + (pp - lp) * 0.7f, lp, Label, TextAlign.Left, StyleKit.Mono);
        c.Text(phase, tag.R - padX, tag.Y + padY, pp, PhaseCol(t, level).WithAlpha(level == 3 || level == 1 || Blink(s) ? 1f : 0.55f), TextAlign.Right, StyleKit.Mono);

        // header
        float hy = 18 * u, hh = 40 * u;
        float bw = CutBadge(c, "DOCKING ASSIST", m, hy + (hh - 31 * u) / 2, 15 * u, 18 * u, 8 * u, t.Main, t.Background, false, 8 * u);
        float x = m + bw + 24 * u;
        if (s.LiveView)
        {
            float sq = 9 * u;
            c.Rect(x, hy + (hh - sq) / 2, sq, sq, t.Main);
            c.Text("LIVE", x + sq + 8 * u, hy + (hh - 13 * u) / 2, 13 * u, t.Main, TextAlign.Left, StyleKit.Mono);
            x += sq + 8 * u + c.TextWidth("LIVE", 13 * u, StyleKit.Mono) + 24 * u;
        }
        float pairPx = 18 * u;
        string pair = Fit(c, PairShort(r), pairPx, Math.Max(80 * u, (w - m - x) * 0.6f));
        float pairW = c.TextWidth(pair, pairPx, StyleKit.Mono);
        c.Text(pair, w - m, hy + (hh - pairPx) / 2, pairPx, t.Text, TextAlign.Right, StyleKit.Mono);
        float lineW = w - m - pairW - 24 * u - x;
        if (lineW > 20 * u)
        {
            int fn = 16;
            for (int i = 0; i < fn; i++) c.Rect(x + lineW * i / fn, hy + hh / 2, lineW / fn + 0.5f, hair, t.Main.WithAlpha(0.6f * (1 - (i + 0.5f) / fn)));
        }

        // the panels: next step, offset, align / roll, range, closing
        float gap = 14 * u, w1 = 560 * u, w2 = 330 * u, w3 = 230 * u;
        float flex = (pb.W - w1 - w2 - w3 - 4 * gap) / 2;
        var p1 = new Box(pb.X, pb.Y, w1, pb.H);
        var p2 = new Box(p1.R + gap, pb.Y, w2, pb.H);
        var p3 = new Box(p2.R + gap, pb.Y, w3, pb.H);
        var p4 = new Box(p3.R + gap, pb.Y, flex, pb.H);
        var p5 = new Box(p4.R + gap, pb.Y, flex, pb.H);
        foreach (var p in new[] { p1, p2, p3, p4, p5 }) Panel(c, t, p, u, s.LiveView ? 0.0f : 0.5f);

        float ip = 22 * u, top = 18 * u;
        // next step
        c.Text("NEXT STEP", p1.X + ip, p1.Y + top, 13 * u, t.Main, TextAlign.Left, StyleKit.Mono);
        string advice = DockGuide.Advice(r);
        float ap = FitPx(c, advice, 32 * u, p1.W - 2 * ip, 12);
        c.Text(advice, p1.X + ip, p1.CY - ap / 2, ap, level == 3 ? t.Text : Level(t, level), TextAlign.Left, StyleKit.Mono);
        string eta = Eta(r);
        c.Text(eta.Length > 0 ? eta : Fit(c, Pair(r), 13 * u, p1.W - 2 * ip), p1.X + ip, p1.B - top - 13 * u, 13 * u, Faint, TextAlign.Left, StyleKit.Mono);

        // offset
        float sc = Math.Min(146 * u, p2.H - 24 * u);
        Scope(c, t, r, p2.X + 18 * u, p2.CY - sc / 2 + u, sc, u, true);
        float ox = p2.X + 18 * u + sc + 16 * u;
        double side = Side(r);
        string off = !has ? "--" : side < 0.1 ? "ON AXIS" : DockGuide.M(side);
        c.Text("OFFSET", ox, p2.CY - 42 * u, 13 * u, Label, TextAlign.Left, StyleKit.Mono);
        ValUnit(c, off, ox, p2.CY - 22 * u, FitPx(c, off, 36 * u, p2.R - ox - 12 * u, 12), 18 * u, !has ? t.Dim : side <= DockGuide.LockDistance ? t.Good : t.Text, t.Text, false);
        c.Text(Fit(c, Offsets(r).Replace("   ", "  ·  "), 13 * u, p2.R - ox - 10 * u), ox, p2.CY + 22 * u, 13 * u, Faint, TextAlign.Left, StyleKit.Mono);

        // align / roll
        float ry = p3.CY - 32 * u - 7 * u;
        c.Text("ALIGN", p3.X + ip, ry + 14 * u, 14 * u, Label, TextAlign.Left, StyleKit.Mono);
        c.Text(has ? Deg(r.Misalign) : "--", p3.R - ip, ry, 32 * u, has ? AlignColor(t, r) : t.Dim, TextAlign.Right, StyleKit.Mono);
        ry += 32 * u + 14 * u;
        c.Text("ROLL", p3.X + ip, ry + 14 * u, 14 * u, Label, TextAlign.Left, StyleKit.Mono);
        c.Text(s.Roll >= 0 ? Deg(s.Roll) : "--", p3.R - ip, ry, 32 * u, s.Roll >= 0 ? (s.Roll < 2 ? t.Text : RollColor(t, s)) : t.Dim, TextAlign.Right, StyleKit.Mono);

        // range
        c.Text("RANGE", p4.X + ip, p4.Y + top + 18 * u, 14 * u, Label, TextAlign.Left, StyleKit.Mono);
        ValUnit(c, has ? DockGuide.M(r.Range) : "--", p4.R - ip, p4.Y + top, 40 * u, 18 * u, has ? t.Text : t.Dim, t.Text, true);
        var rb = new Box(p4.X + ip, p4.B - top - 13 * u - 10 * u - 18 * u, p4.W - 2 * ip, 18 * u);
        int n = 12;
        SegBar(c, rb, n, RangeLit(r, n), 4 * u, false, has && r.Range < 5 ? t.Good : t.Main, t.Main.WithAlpha(0.18f));
        foreach (var (mm, lab) in new[] { (1.0, "1"), (5.0, "5"), (20.0, "20"), (100.0, "100 M") })
        {
            float tx = rb.X + rb.W * RangeAt(mm);
            bool last = lab.Length > 3;
            c.Text(lab, last ? Math.Min(tx + 12 * u, rb.R) : tx, p4.B - top - 13 * u, 12 * u, Faint, last ? TextAlign.Right : TextAlign.Center, StyleKit.Mono);
        }

        // closing
        var vc = SpeedColor(t, r);
        c.Text("CLOSING", p5.X + ip, p5.Y + top + 18 * u, 14 * u, Label, TextAlign.Left, StyleKit.Mono);
        string sp = SpeedText(r);
        ValUnit(c, sp, p5.R - ip, p5.Y + top, FitPx(c, sp, 40 * u, p5.W * 0.62f, 12), 18 * u, has ? vc : t.Dim, t.Text, true);
        var cb = new Box(p5.X + ip, rb.Y, p5.W - 2 * ip, 18 * u);
        int cn = 10, lit = 0;
        float safeX = -1;
        if (has)
        {
            var (_, safe, now) = SpeedScale(r);
            lit = (int)Math.Round(cn * now);
            safeX = cb.X + cb.W * safe;
        }
        SegBar(c, cb, cn, lit, 4 * u, false, vc, t.Main.WithAlpha(0.18f));
        if (safeX >= 0)
        {
            c.Rect(safeX - 1.5f * u, cb.Y - 6 * u, Math.Max(2, 3 * u), cb.H + 12 * u, HudColor.White);
            c.Text("SAFE " + DockGuide.Mps(r.Safe), p5.X + ip, p5.B - top - 12 * u, 12 * u, t.Text, TextAlign.Left, StyleKit.Mono);
        }
    }

    // ------------------------------------------------------------------ PiP and screens

    private static void DrawCompact(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        bool tall = h > w * 0.8f, has = r != null && r.HasTarget;
        float u = UCompact(w, h, tall), pad = 14 * u;
        var v = new Box(0, 0, w, h);
        var (phase, level) = DockGuide.Phase(r);

        if (!s.LiveView) c.Rect(0, 0, w, h, t.Background.WithAlpha(0.97f));
        ViewLines(c, t, r, s, v, u, Level(t, level), Look(t));
        CornerL(c, new Box(8 * u, 8 * u, w - 16 * u, h - 16 * u), 36 * u, 28 * u, Math.Max(1.5f, 2 * u), t.Main, false);

        // title and phase tags
        float tp = Math.Max(7, 12 * u), tpx = 12 * u, tpy = 5 * u, ty = 10 * u;
        float pw = CutBadge(c, phase, w - 10 * u, ty, tp, tpx, tpy, t.Background.WithAlpha(0.8f), PhaseCol(t, level).WithAlpha(level == 3 || level == 1 || Blink(s) ? 1f : 0.55f), true, 8 * u);
        string title = "DOCK ASSIST";
        float sq = Math.Max(3, 7 * u), live = s.LiveView ? 8 * u + sq + 6 * u + c.TextWidth("LIVE", tp, StyleKit.Mono) : 0;
        float tw = c.TextWidth(title, tp, StyleKit.Mono) + 2 * tpx + live;
        if (10 * u + tw > w - pw - 24 * u) live = 0;
        tw = c.TextWidth(title, tp, StyleKit.Mono) + 2 * tpx + live;
        CutPanel(c, new Box(10 * u, ty, tw, tp + 2 * tpy), 8 * u, t.Main);
        c.Text(title, 10 * u + tpx, ty + tpy, tp, t.Background, TextAlign.Left, StyleKit.Mono);
        if (live > 0)
        {
            float lx = 10 * u + tpx + c.TextWidth(title, tp, StyleKit.Mono) + 8 * u;
            c.Rect(lx, ty + tpy + (tp - sq) / 2, sq, sq, t.Background);
            c.Text("LIVE", lx + sq + 6 * u, ty + tpy, tp, t.Background, TextAlign.Left, StyleKit.Mono);
        }

        // the bottom panel
        float ph = (tall ? 166 : 96) * u, py = h - ph;
        c.Rect(0, py, w, ph, t.Background.WithAlpha(tall ? 0.88f : 0.86f));
        c.Rect(0, py, w, Math.Max(1.5f, 2 * u), t.Main);

        var vc = SpeedColor(t, r);
        string rv = has ? DockGuide.M(r.Range).Replace(" M", "") : "--";
        string cv = SpeedText(r).Replace(" M/S", "");
        float big = Math.Max(10, (tall ? 32 : 28) * u), sl = Math.Max(7, 11 * u), sm = Math.Max(7, 12 * u);
        string rl = tall ? "RANGE M" : "RANGE", cl = has ? "M/S  ·  " + DockGuide.Mps(r.Safe).Replace(" M/S", "") : "M/S";
        float cW = Math.Max(c.TextWidth(cv, big, StyleKit.Mono), c.TextWidth(cl, sl, StyleKit.Mono));
        float rW = Math.Max(c.TextWidth(rv, big, StyleKit.Mono), c.TextWidth(rl, sl, StyleKit.Mono));
        float cx2 = w - pad, rx2 = cx2 - cW - 16 * u;

        float rowY = py + (tall ? 12 * u : 0), rowH = tall ? 84 * u : ph;
        float sz = (tall ? 84 : 72) * u;
        Scope(c, t, r, pad, rowY + (rowH - sz) / 2, sz, u, false);
        float colY = rowY + (rowH - sl - big - 2 * u) / 2;
        c.Text(cl, cx2, colY, sl, Label, TextAlign.Right, StyleKit.Mono);
        c.Text(cv, cx2, colY + sl + 2 * u, FitPx(c, cv, big, w * 0.3f, 9), has ? vc : t.Dim, TextAlign.Right, StyleKit.Mono);
        c.Text(rl, rx2, colY, sl, Label, TextAlign.Right, StyleKit.Mono);
        c.Text(rv, rx2, colY + sl + 2 * u, big, has ? t.Text : t.Dim, TextAlign.Right, StyleKit.Mono);

        float mx = pad + sz + 16 * u, room = rx2 - rW - 16 * u - mx;
        double side = Side(r);
        string off = !has ? "--" : side < 0.1 ? "0 M" : DockGuide.M(side);
        string al = "ALIGN " + (has ? Deg(r.Misalign) : "--") + "  ·  ROLL ";
        string roll = s.Roll >= 0 ? Deg(s.Roll) : "--";
        var rollCol = s.Roll >= 2 ? RollColor(t, s) : t.Main;
        string advice = DockGuide.Advice(r);
        var advCol = level == 3 ? t.Text : Level(t, level);
        if (!tall)
        {
            if (room > 40 * u)
            {
                float ap = FitPx(c, advice, Math.Max(8, 19 * u), room, 7);
                float my = py + (ph - ap - 4 * u - sm) / 2;
                c.Text(advice, mx, my, ap, advCol, TextAlign.Left, StyleKit.Mono);
                Line2(c, al, roll, "  ·  OFF " + off, mx, my + ap + 4 * u, sm, room, Label, rollCol);
            }
        }
        else
        {
            if (room > 40 * u)
            {
                float my = rowY + (rowH - sm - 18 * u - 6 * u - sm) / 2;
                c.Text("OFFSET", mx, my + (18 * u - sm) * 0.7f, sm, Label, TextAlign.Left, StyleKit.Mono);
                float ow = c.TextWidth("OFFSET ", sm, StyleKit.Mono);
                c.Text(off, mx + ow, my, FitPx(c, off, 18 * u, room - ow, 8), !has ? t.Dim : side <= DockGuide.LockDistance ? t.Good : t.Text, TextAlign.Left, StyleKit.Mono);
                Line2(c, al, roll, "", mx, my + 18 * u + 6 * u, sm, room, Label, rollCol);
            }
            // the next step on an amber bar
            float bh = 34 * u;
            var bar = new Box(pad, h - 14 * u - bh, w - 2 * pad, bh);
            CutPanel(c, bar, 8 * u, level == 3 ? t.Main : Level(t, level));
            string nt = "NEXT  »  " + advice;
            float np = FitPx(c, nt, Math.Max(8, 18 * u), bar.W - 24 * u, 7);
            c.Text(nt, bar.X + 12 * u, bar.Y + (bh - np) / 2, np, t.Background, TextAlign.Left, StyleKit.Mono);
        }
    }

    /// <summary>"ALIGN 2.1° · ROLL " + the roll in its own colour + the rest, cut to the width.</summary>
    private static void Line2(ICanvas c, string a, string roll, string rest, float x, float y, float px, float room, HudColor col, HudColor rollCol)
    {
        float aw = c.TextWidth(a, px, StyleKit.Mono), rw = c.TextWidth(roll, px, StyleKit.Mono);
        if (aw + rw > room) { c.Text(Fit(c, a + roll, px, room), x, y, px, col, TextAlign.Left, StyleKit.Mono); return; }
        c.Text(a, x, y, px, col, TextAlign.Left, StyleKit.Mono);
        c.Text(roll, x + aw, y, px, rollCol, TextAlign.Left, StyleKit.Mono);
        if (rest.Length > 0 && room - aw - rw > 30) c.Text(Fit(c, rest, px, room - aw - rw), x + aw + rw, y, px, col, TextAlign.Left, StyleKit.Mono);
    }

    // ------------------------------------------------------------------ the offset scope

    /// <summary>An octagon with the dock's offset across your axis (log scale, 10 m at the edge) as a diamond.</summary>
    private static void Scope(ICanvas c, DockTheme t, DockReading r, float x, float y, float size, float u, bool detailed)
    {
        bool has = r != null && r.HasTarget;
        float hair = Math.Max(1, u), cx = x + size / 2, cy = y + size / 2, R = size * 0.47f;
        Octagon(c, cx, cy, R, hair, t.Main.WithAlpha(detailed ? 0.6f : 0.7f));
        if (detailed) Octagon(c, cx, cy, Map(2.0, R), hair, t.Main.WithAlpha(0.3f));
        c.Rect(cx - R, cy - hair / 2, 2 * R, hair, t.Main.WithAlpha(0.25f));
        c.Rect(cx - hair / 2, cy - R, hair, 2 * R, t.Main.WithAlpha(0.25f));
        if (!has) return;
        var (px, py) = Spot(r.OffRight, r.OffUp, cx, cy, R);
        double side = Side(r);
        var dc = side <= DockGuide.LockDistance ? t.Good : side < 2 ? t.Text : t.Warn;
        float d = Math.Max(5, size * 0.08f);
        c.Rect(px - d / 2, py - d / 2, d, d, dc, (float)(Math.PI / 4));
    }
}
