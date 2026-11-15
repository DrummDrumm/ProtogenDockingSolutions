using System;
using static ProtogenDocking.StyleKit;

namespace ProtogenDocking;

/// <summary>
/// "Glass": the live picture fills every screen; the readouts float on dark see-through cards with soft corners, mint
/// accents.
///
///   full screen:  [ (o) Docking Assist  LIVE ]   [ connector -> dock ]            [ APPROACH ]
///                                       live picture, full screen
///                 [ offset scope card ]       ( NEXT  next step )        [ range / closing card ]
///
///   PiP / screens: title and phase on top, a small scope card bottom left, the numbers bottom right (side by side on
///   wide screens, stacked on tall ones), the next step on a pill along the bottom.
/// </summary>
public static class GlassStyle
{
    public static readonly DockTheme Palette = new DockTheme
    {
        Main = new HudColor(94, 242, 200),
        Dim = new HudColor(90, 110, 120),
        Good = new HudColor(140, 255, 170),
        Warn = new HudColor(255, 207, 110),
        Bad = new HudColor(255, 100, 90),
        Text = new HudColor(255, 255, 255),
        Background = new HudColor(8, 14, 20),
        Font = "White",
        NumberFont = "White",
    };

    private static readonly HudColor Label = new HudColor(169, 188, 198);
    private static readonly HudColor Ink = new HudColor(4, 32, 26);

    private static ViewLook Look(DockTheme t) => new ViewLook
    {
        Line = 1, Face = 2, Cross = HudColor.White, CrossGap = 9, CrossArm = 12, Bracket = 1.6f,
        Outline = HudColor.White.WithAlpha(0.3f), Tunnel = t.Main, Chevron = t.Warn,
    };

    private static float UFull(float w, float h) => Math.Max(0.3f, Math.Min(w / 1920f, h / 1080f));

    private static float UCompact(float w, float h, bool tall) =>
        Math.Max(0.35f, tall ? Math.Min(w / 520f, h / 520f) : Math.Min(w / 620f, h / 350f));

    public static void Layout(DockScreen s, float w, float h)
    {
        s.ViewX = 0; s.ViewY = 0; s.ViewW = w; s.ViewH = h;
    }

    public static void Draw(ICanvas c, DockReading r, DockScreen s, float w, float h)
    {
        if (s.Full) DrawFull(c, Palette, r, s, w, h);
        else DrawCompact(c, Palette, r, s, w, h);
    }

    private static HudColor CardFill(DockTheme t) => t.Background.WithAlpha(0.66f);
    private static readonly HudColor Edge = HudColor.White.WithAlpha(0.14f);

    private static void GlassCard(ICanvas c, DockTheme t, Box b, float u, float radius = 12) =>
        Card(c, b, radius * u, CardFill(t), Edge, Math.Max(1, u));

    private static HudColor PhaseFill(DockTheme t, int level, DockScreen s) =>
        (level == 3 ? t.Main : Level(t, level)).WithAlpha(level == 3 || level == 1 || Blink(s) ? 1f : 0.6f);

    // ------------------------------------------------------------------ full screen

    private static void DrawFull(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        float u = UFull(w, h), m = 28 * u, hair = Math.Max(1, u);
        var v = new Box(0, 0, w, h);
        var (phase, level) = DockGuide.Phase(r);
        bool has = r != null && r.HasTarget;

        if (!s.LiveView) c.Rect(0, 0, w, h, t.Background.WithAlpha(0.97f));
        ViewLines(c, t, r, s, v, u, Level(t, level), Look(t));

        // top: title, the pair, the phase
        float ty = 24 * u, th = 46 * u;
        float tp = 18 * u, lp = 14 * u, dot = 9 * u;
        float tw = dot + 14 * u + c.TextWidth("DOCKING ASSIST", tp, StyleKit.Mono) + (s.LiveView ? 14 * u + c.TextWidth("LIVE", lp, StyleKit.Mono) : 0);
        var tc = new Box(m, ty, tw + 40 * u, th);
        GlassCard(c, t, tc, u, 9);
        c.Shape(HudShape.Circle, tc.X + 20 * u, tc.CY - dot / 2, dot, dot, s.LiveView ? t.Main : t.Dim);
        float x = tc.X + 20 * u + dot + 14 * u;
        c.Text("DOCKING ASSIST", x, tc.CY - tp / 2, tp, t.Text, TextAlign.Left, StyleKit.Mono);
        if (s.LiveView) c.Text("LIVE", x + c.TextWidth("DOCKING ASSIST", tp, StyleKit.Mono) + 14 * u, tc.CY - lp / 2, lp, t.Main, TextAlign.Left, StyleKit.Mono);

        float pp = 16 * u;
        float phw = c.TextWidth(phase, pp, StyleKit.Mono) + 44 * u;
        var pcard = new Box(w - m - phw, ty, phw, th);
        Card(c, pcard, 9 * u, PhaseFill(t, level, s), default, 0);
        c.Text(phase, pcard.CX, pcard.CY - pp / 2, pp, Ink, TextAlign.Center, StyleKit.Mono);

        float room = Math.Min(w - 2 * Math.Max(tc.R, w - pcard.X) - 40 * u, 900 * u);
        if (room > 120 * u)
        {
            string pair = has ? r.Mine.ToUpperInvariant() + "   >   " + r.Target.ToUpperInvariant() : Pair(r);
            pair = Fit(c, pair, pp, room - 44 * u);
            float pw = c.TextWidth(pair, pp, StyleKit.Mono) + 44 * u;
            var pc2 = new Box(w / 2 - pw / 2, ty, pw, th);
            GlassCard(c, t, pc2, u, 9);
            c.Text(pair, pc2.CX, pc2.CY - pp / 2, pp, t.Text, TextAlign.Center, StyleKit.Mono);
        }

        // bottom left: the offset scope
        float sw = 300 * u, sh = 18 * u + 13 * u + 12 * u + 200 * u + 12 * u + 30 * u + 18 * u;
        var oc = new Box(m, h - m - sh, sw, sh);
        GlassCard(c, t, oc, u, 10);
        float ip = 18 * u, y = oc.Y + ip;
        c.Text("OFFSET", oc.X + ip, y, 13 * u, Label, TextAlign.Left, StyleKit.Mono);
        if (has) c.Text(Offsets(r).Replace("   ", "  ·  "), oc.R - ip, y, 13 * u, Label, TextAlign.Right, StyleKit.Mono);
        y += 13 * u + 12 * u;
        Scope(c, t, r, s, oc.CX, y + 100 * u, 88 * u, u, true);
        y += 200 * u + 12 * u;
        double side = Side(r);
        string off = !has ? "--" : side < 0.1 ? "ON AXIS" : DockGuide.M(side);
        ValUnit(c, off, oc.X + ip, y, 30 * u, 15 * u, !has ? t.Dim : side <= DockGuide.LockDistance ? t.Good : t.Text, Label, false);
        AlignRoll(c, t, r, s, oc.R - ip, y + 12 * u, 14 * u, 16 * u, true);

        // bottom right: range and closing
        float rw = 340 * u, rp = 20 * u, rh = rp + 34 * u + 8 * u + 4 * u + 20 * u + 34 * u + 8 * u + 4 * u + rp;
        var rc = new Box(w - m - rw, h - m - rh, rw, rh);
        GlassCard(c, t, rc, u, 10);
        y = rc.Y + rp;
        c.Text("RANGE", rc.X + rp, y + 14 * u, 13 * u, Label, TextAlign.Left, StyleKit.Mono);
        ValUnit(c, has ? DockGuide.M(r.Range) : "--", rc.R - rp, y, 34 * u, 15 * u, has ? t.Text : t.Dim, Label, true);
        y += 34 * u + 8 * u;
        Bar(c, t, new Box(rc.X + rp, y, rc.W - 2 * rp, Math.Max(2, 4 * u)), has ? RangeAt(r.Range) : 0, has && r.Range < 5 ? t.Good : t.Main, -1);
        y += 4 * u + 20 * u;
        var vc = SpeedColor(t, r);
        c.Text(has ? "CLOSING  ·  SAFE " + DockGuide.Mps(r.Safe).Replace(" M/S", "") : "CLOSING", rc.X + rp, y + 14 * u, 13 * u, Label, TextAlign.Left, StyleKit.Mono);
        string sp = SpeedText(r);
        ValUnit(c, sp, rc.R - rp, y, FitPx(c, sp, 34 * u, rc.W * 0.5f, 10), 15 * u, has ? (vc.Equals(t.Good) ? t.Text : vc) : t.Dim, Label, true);
        y += 34 * u + 8 * u;
        float now = 0, safe = -1;
        if (has) { var sc = SpeedScale(r); now = sc.Now; safe = sc.Safe; }
        Bar(c, t, new Box(rc.X + rp, y, rc.W - 2 * rp, Math.Max(2, 4 * u)), now, vc, safe);

        // bottom centre: the next step on a pill
        string advice = DockGuide.Advice(r);
        string eta = Eta(r);
        float ap = 22 * u, np = 14 * u;
        float maxW = rc.X - oc.R - 48 * u;
        float aw = c.TextWidth(advice, ap, StyleKit.Mono), nw = c.TextWidth("NEXT", np, StyleKit.Mono);
        float ew = eta.Length > 0 ? c.TextWidth(eta, np, StyleKit.Mono) + 20 * u : 0;
        float pillW = Math.Min(maxW, nw + 16 * u + aw + ew + 56 * u);
        if (pillW < 200 * u) return;
        float apx = FitPx(c, advice, ap, pillW - 56 * u - nw - 16 * u - ew, 10);
        float pillH = 52 * u;
        var pill = new Box(w / 2 - pillW / 2, h - m - pillH, pillW, pillH);
        Card(c, pill, pillH * 0.24f, CardFill(t), Edge, hair);
        c.Text("NEXT", pill.X + 28 * u, pill.CY - np / 2, np, t.Main, TextAlign.Left, StyleKit.Mono);
        c.Text(advice, pill.X + 28 * u + nw + 16 * u, pill.CY - apx / 2, apx, level == 3 ? t.Text : Level(t, level), TextAlign.Left, StyleKit.Mono);
        if (eta.Length > 0) c.Text(eta, pill.R - 28 * u, pill.CY - np / 2, np, Label, TextAlign.Right, StyleKit.Mono);
    }

    /// <summary>A thin track with a fill; the safe mark (0-1, or -1 for none) in amber.</summary>
    private static void Bar(ICanvas c, DockTheme t, Box b, float f, HudColor col, float mark)
    {
        c.Rect(b.X, b.Y, b.W, b.H, HudColor.White.WithAlpha(0.14f));
        if (f > 0) c.Rect(b.X, b.Y, b.W * Math.Min(1, f), b.H, col);
        if (mark >= 0) c.Rect(b.X + b.W * mark - b.H * 0.25f, b.Y - b.H * 1.25f, Math.Max(2, b.H * 0.5f), b.H * 3.5f, t.Warn);
    }

    /// <summary>"ALIGN 2.1° · ROLL 0.8°" with the numbers bigger and the roll in its colour; right-aligned at x when right is set.</summary>
    private static float AlignRoll(ICanvas c, DockTheme t, DockReading r, DockScreen s, float x, float y, float lp, float np, bool right)
    {
        bool has = r != null && r.HasTarget;
        string a = has ? Deg(r.Misalign) : "--", ro = s.Roll >= 0 ? Deg(s.Roll) : "--";
        var parts = new (string T, float P, HudColor C)[]
        {
            ("ALIGN ", lp, Label), (a, np, has && r.Misalign > DockGuide.LockAngle * 0.5 ? AlignColor(t, r) : t.Text),
            ("  ·  ROLL ", lp, Label), (ro, np, s.Roll >= 2 ? RollColor(t, s) : t.Text),
        };
        float total = 0;
        foreach (var p in parts) total += c.TextWidth(p.T, p.P, StyleKit.Mono);
        float px = right ? x - total : x;
        foreach (var p in parts)
        {
            c.Text(p.T, px, y + (np - p.P) * 0.78f, p.P, p.C, TextAlign.Left, StyleKit.Mono);
            px += c.TextWidth(p.T, p.P, StyleKit.Mono);
        }
        return total;
    }

    // ------------------------------------------------------------------ PiP and screens

    private static void DrawCompact(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        bool tall = h > w * 0.8f, has = r != null && r.HasTarget;
        float u = UCompact(w, h, tall), m = 10 * u;
        var v = new Box(0, 0, w, h);
        var (phase, level) = DockGuide.Phase(r);

        if (!s.LiveView) c.Rect(0, 0, w, h, t.Background.WithAlpha(0.97f));
        ViewLines(c, t, r, s, v, u, Level(t, level), Look(t));

        // title and phase
        float tp = Math.Max(8, (tall ? 15 : 14) * u), pp = Math.Max(7, (tall ? 13 : 12) * u), dot = Math.Max(3, 7 * u);
        float th = tp + 12 * u;
        float phw = c.TextWidth(phase, pp, StyleKit.Mono) + 24 * u;
        var pcard = new Box(w - m - phw, m, phw, th);
        Card(c, pcard, 6 * u, PhaseFill(t, level, s), default, 0);
        c.Text(phase, pcard.CX, pcard.CY - pp / 2, pp, Ink, TextAlign.Center, StyleKit.Mono);
        float tw = dot + 8 * u + c.TextWidth("DOCK ASSIST", tp, StyleKit.Mono) + 24 * u;
        if (m + tw < pcard.X - 8 * u)
        {
            var tc = new Box(m, m, tw, th);
            GlassCard(c, t, tc, u, 6);
            c.Shape(HudShape.Circle, tc.X + 12 * u, tc.CY - dot / 2, dot, dot, s.LiveView ? t.Main : t.Dim);
            c.Text("DOCK ASSIST", tc.X + 12 * u + dot + 8 * u, tc.CY - tp / 2, tp, t.Text, TextAlign.Left, StyleKit.Mono);
        }

        // the next step on a pill along the bottom
        float ap = Math.Max(8, (tall ? 16 : 15) * u), np = Math.Max(7, 12 * u), pillH = ap + 16 * u;
        var pill = new Box(m, h - m - pillH, w - 2 * m, pillH);
        Card(c, pill, pillH * 0.24f, CardFill(t), Edge, Math.Max(1, u));
        c.Text("NEXT", pill.X + 14 * u, pill.CY - np / 2, np, t.Main, TextAlign.Left, StyleKit.Mono);
        float ax = pill.X + 14 * u + c.TextWidth("NEXT", np, StyleKit.Mono) + 10 * u;
        string advice = DockGuide.Advice(r);
        float apx = FitPx(c, advice, ap, pill.R - 14 * u - ax, 7);
        c.Text(advice, ax, pill.CY - apx / 2, apx, level == 3 ? t.Text : Level(t, level), TextAlign.Left, StyleKit.Mono);

        float by = pill.Y - 10 * u;   // the cards' bottom

        // scope card, bottom left
        float cw = (tall ? 136 : 108) * u, ss = (tall ? 96 : 70) * u, sp = (tall ? 10 : 8) * u, lp = Math.Max(7, (tall ? 14 : 13) * u);
        float ch = sp + ss + 4 * u + lp + sp;
        var sc = new Box(m, by - ch, cw, ch);
        GlassCard(c, t, sc, u, 7);
        Scope(c, t, r, s, sc.CX, sc.Y + sp + ss / 2, ss * 0.44f, u, false);
        string a = has ? Deg(r.Misalign) : "--", ro = s.Roll >= 0 ? Deg(s.Roll) : "--";
        float aw = c.TextWidth(a + "  ·  ", lp, StyleKit.Mono), rw = c.TextWidth(ro, lp, StyleKit.Mono);
        float lx = sc.CX - (aw + rw) / 2, ly = sc.Y + sp + ss + 4 * u;
        c.Text(a + "  ·  ", lx, ly, lp, has && r.Misalign > DockGuide.LockAngle * 0.5 ? AlignColor(t, r) : t.Text, TextAlign.Left, StyleKit.Mono);
        c.Text(ro, lx + aw, ly, lp, s.Roll >= 2 ? RollColor(t, s) : t.Text, TextAlign.Left, StyleKit.Mono);

        // numbers card, bottom right
        float big = Math.Max(10, (tall ? 32 : 26) * u), unit = Math.Max(7, (tall ? 13 : 12) * u), sl = Math.Max(7, (tall ? 12 : 11) * u);
        string rv = has ? DockGuide.M(r.Range) : "--", cv = SpeedText(r);
        string cl = has ? "CLOSING / " + DockGuide.Mps(r.Safe).Replace(" M/S", "") : "CLOSING";
        var vc = SpeedColor(t, r);
        var cvCol = has ? (vc.Equals(t.Good) ? t.Text : vc) : t.Dim;
        float rW = Math.Max(Width(c, rv, big, unit), c.TextWidth("RANGE", sl, StyleKit.Mono));
        float cW = Math.Max(Width(c, cv, big, unit), c.TextWidth(cl, sl, StyleKit.Mono));
        float px2 = (tall ? 16 : 14) * u, py2 = (tall ? 12 : 10) * u;
        Box nc;
        if (tall)
        {
            float nw = Math.Max(rW, cW) + 2 * px2, nh = 2 * (sl + 2 * u + big) + 8 * u + 2 * py2;
            nc = new Box(w - m - nw, by - nh, nw, nh);
        }
        else
        {
            float nw = rW + cW + 18 * u + 2 * px2, nh = sl + 2 * u + big + 2 * py2;
            nc = new Box(w - m - nw, by - nh, nw, nh);
        }
        if (nc.X < sc.R + 8 * u) return;
        GlassCard(c, t, nc, u, 7);
        float x1 = nc.X + px2, y1 = nc.Y + py2;
        c.Text("RANGE", x1, y1, sl, Label, TextAlign.Left, StyleKit.Mono);
        ValUnit(c, rv, x1, y1 + sl + 2 * u, big, unit, has ? t.Text : t.Dim, Label, false);
        float x2 = tall ? x1 : x1 + rW + 18 * u, y2 = tall ? y1 + sl + 2 * u + big + 8 * u : y1;
        c.Text(cl, x2, y2, sl, Label, TextAlign.Left, StyleKit.Mono);
        ValUnit(c, cv, x2, y2 + sl + 2 * u, big, unit, cvCol, Label, false);
    }

    private static float Width(ICanvas c, string text, float big, float small)
    {
        int sp = text.LastIndexOf(' ');
        return sp > 0 ? c.TextWidth(text.Substring(0, sp), big, StyleKit.Mono) + c.TextWidth(" " + text.Substring(sp + 1), small, StyleKit.Mono) : c.TextWidth(text, big, StyleKit.Mono);
    }

    // ------------------------------------------------------------------ the offset scope

    /// <summary>Thin white rings with the dock's offset across your axis (log scale, 10 m at the edge) as a soft mint dot.</summary>
    private static void Scope(ICanvas c, DockTheme t, DockReading r, DockScreen s, float cx, float cy, float R, float u, bool detailed)
    {
        bool has = r != null && r.HasTarget;
        float hair = Math.Max(1, u);
        Ring(c, cx, cy, R, hair, HudColor.White.WithAlpha(detailed ? 0.28f : 0.35f));
        if (detailed) Ring(c, cx, cy, R / 2, hair, HudColor.White.WithAlpha(0.16f));
        float ext = R * 48f / 44f;
        c.Rect(cx - ext, cy - hair / 2, 2 * ext, hair, HudColor.White.WithAlpha(detailed ? 0.14f : 0.18f));
        c.Rect(cx - hair / 2, cy - ext, hair, 2 * ext, HudColor.White.WithAlpha(detailed ? 0.14f : 0.18f));
        if (s.Roll >= 0.5)
        {
            double a = -Math.PI / 2 + Math.Min(60, s.Roll * 2) * Math.PI / 180;
            float mk = Math.Max(4, 7 * u);
            c.Shape(HudShape.Triangle, cx + (float)Math.Cos(a) * (R + mk * 0.7f) - mk / 2, cy + (float)Math.Sin(a) * (R + mk * 0.7f) - mk / 2, mk, mk, RollColor(t, s), (float)(a - Math.PI / 2));
        }
        if (!has) return;
        var (px, py) = Spot(r.OffRight, r.OffUp, cx, cy, R);
        double side = Side(r);
        var dc = side <= DockGuide.LockDistance ? t.Good : side < 2 ? t.Main : t.Warn;
        if (detailed)
        {
            float halo = R * 18f / 44f;
            c.Shape(HudShape.Circle, px - halo / 2, py - halo / 2, halo, halo, dc.WithAlpha(0.22f));
        }
        float d = Math.Max(4, R * (detailed ? 7f : 12f) / 44f);
        c.Shape(HudShape.Circle, px - d / 2, py - d / 2, d, d, dc);
    }
}
