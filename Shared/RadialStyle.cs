using System;
using static ProtogenDocking.StyleKit;

namespace ProtogenDocking;

/// <summary>
/// "Radial HUD": the live picture fills every screen behind a ring reticle round your connector's axis. The left arc
/// fills with the range, the right arc with the closing speed (an orange tick for the safe speed); the dock's offset is a
/// dot inside the ring, your roll an orange marker on top. The numbers sit beside the ring, the rest under it.
///
///   full screen:  DOCKING ASSIST . LIVE                                 connector -> dock
///                                          ( APPROACH )
///                         RANGE   (  ring reticle  )   CLOSING
///                         42.6                            1.8
///                              OFFSET 0.34 M   ALIGN 2.1°   ROLL 0.8°
///                                     [ NEXT  next step ]
///
///   PiP: the numbers beside the ring and a line of readouts above the next-step bar. Square screens: the ring a
///   little high, the numbers under its sides, the readouts centred under it.
/// </summary>
public static class RadialStyle
{
    public static readonly DockTheme Palette = new DockTheme
    {
        Main = new HudColor(143, 233, 255),
        Dim = new HudColor(70, 110, 125),
        Good = new HudColor(130, 255, 180),
        Warn = new HudColor(255, 157, 74),
        Bad = new HudColor(255, 84, 72),
        Text = new HudColor(255, 255, 255),
        Background = new HudColor(0, 0, 0),
        Font = "White",
        NumberFont = "White",
    };

    private static readonly HudColor Label = new HudColor(159, 220, 239);

    private static ViewLook Look(DockTheme t) => new ViewLook
    {
        Line = 1, Face = 2.5f, Cross = HudColor.White, CrossGap = 8, CrossArm = 14, Bracket = 1.6f,
        Outline = t.Main.WithAlpha(0.18f), Tunnel = t.Main, Chevron = t.Warn,
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

    // ------------------------------------------------------------------ the ring

    /// <summary>
    /// The reticle round (cx, cy): k = pixels per mockup unit (the ring is 300 units, the arcs 340). Range on the left
    /// arc (filling up from the bottom), closing speed on the right one, roll ticks and marker on top, offset dot inside.
    /// </summary>
    private static void Reticle(ICanvas c, DockTheme t, DockReading r, DockScreen s, float cx, float cy, float k, float u, bool detailed)
    {
        bool has = r != null && r.HasTarget;
        float hair = Math.Max(1, u), R = 300 * k, A = 340 * k, at = Math.Max(2.5f, (detailed ? 10 : 5.5f) * u);
        Ring(c, cx, cy, R, hair, t.Main.WithAlpha(detailed ? 0.3f : 0.35f));
        if (detailed)
        {
            // dashed inner ring
            int n = 40;
            for (int i = 0; i < n; i += 1)
            {
                double a0 = i * 2 * Math.PI / n, a1 = a0 + 2 * Math.PI / n * 0.3;
                Arc(c, cx, cy, 150 * k, a0, a1, hair, t.Main.WithAlpha(0.18f));
            }
        }

        // range: left arc, 135° (bottom left) up through 180° to 225° (top left)
        double d45 = Math.PI / 4;
        Arc(c, cx, cy, A, Math.PI - d45, Math.PI + d45, at, t.Main.WithAlpha(0.2f));
        if (has)
        {
            float f = RangeAt(r.Range);
            Arc(c, cx, cy, A, Math.PI - d45, Math.PI - d45 + 2 * d45 * f, at, r.Range < 5 ? t.Good : t.Main);
        }
        // closing: right arc, 45° (bottom right) up through 0° to -45° (top right)
        Arc(c, cx, cy, A, -d45, d45, at, t.Main.WithAlpha(0.2f));
        if (has)
        {
            var (_, safe, now) = SpeedScale(r);
            var vc = SpeedColor(t, r);
            if (now > 0) Arc(c, cx, cy, A, d45 - 2 * d45 * now, d45, at, vc.Equals(t.Good) ? t.Main : vc);
            double sa = d45 - 2 * d45 * safe;
            float r0 = A - at * 1.6f - 6 * k, r1 = A + at * 1.6f + 6 * k;
            DockCamRenderer.Line(c, cx + (float)Math.Cos(sa) * r0, cy + (float)Math.Sin(sa) * r0, cx + (float)Math.Cos(sa) * r1, cy + (float)Math.Sin(sa) * r1, Math.Max(2, (detailed ? 4 : 3) * u), t.Warn);
        }

        // roll: ticks every 10° on top, white at 0, the roll as an orange marker outside
        float tl = Math.Max(4, 16 * k);
        for (int i = -2; i <= 2; i++)
        {
            if (!detailed && i != 0) continue;
            double a = -Math.PI / 2 + i * 10 * Math.PI / 180;
            DockCamRenderer.Line(c, cx + (float)Math.Cos(a) * R, cy + (float)Math.Sin(a) * R, cx + (float)Math.Cos(a) * (R - tl), cy + (float)Math.Sin(a) * (R - tl),
                Math.Max(1, (i == 0 ? 2 : 1) * u), i == 0 ? HudColor.White : t.Main.WithAlpha(0.6f));
        }
        if (s.Roll >= 0.5)
        {
            double a = -Math.PI / 2 + Math.Min(60, s.Roll * 2) * Math.PI / 180;
            float mk = Math.Max(6, (detailed ? 18 : 24) * k + 4 * u);
            float mr = R + mk * 0.75f;
            c.Shape(HudShape.Triangle, cx + (float)Math.Cos(a) * mr - mk / 2, cy + (float)Math.Sin(a) * mr - mk / 2, mk, mk, RollColor(t, s).Equals(t.Good) ? t.Good : t.Warn, (float)(a - Math.PI / 2));
        }

        if (!has) return;
        var (px, py) = Spot(r.OffRight, r.OffUp, cx, cy, R);
        double side = Side(r);
        var dc = side <= DockGuide.LockDistance ? t.Good : side < 2 ? t.Main : t.Warn;
        if (Math.Abs(px - cx) + Math.Abs(py - cy) > 8 * u) Dashed(c, cx, cy, px, py, hair, 4 * u, 4 * u, dc.WithAlpha(0.8f));
        float d = Math.Max(6, (detailed ? 14 : 28) * k + 2 * u);
        c.Shape(HudShape.Circle, px - d / 2, py - d / 2, d, d, dc);
    }

    private static void PhasePill(ICanvas c, DockTheme t, string phase, int level, DockScreen s, float cx, float y, float px, float u, bool center, float rightX = 0)
    {
        float bw = c.TextWidth(phase, px, StyleKit.Mono) + 2 * (center ? 22 : 12) * u, bh = px + 2 * (center ? 6 : 4) * u;
        float bx = center ? cx - bw / 2 : rightX - bw;
        var col = (level == 3 ? t.Main : Level(t, level)).WithAlpha(level == 3 || level == 1 || Blink(s) ? 1f : 0.55f);
        Card(c, new Box(bx, y, bw, bh), bh * 0.3f, HudColor.Black.WithAlpha(0.55f), col, Math.Max(1, u));
        c.Text(phase, bx + bw / 2, y + (bh - px) / 2, px, level == 3 ? t.Text : col, TextAlign.Center, StyleKit.Mono);
    }

    /// <summary>Label-number pairs on one line ("OFFSET 0.34 M   ALIGN 2.1°   ROLL 0.8°"); returns the width.</summary>
    private static float Readouts(ICanvas c, DockTheme t, DockReading r, DockScreen s, float x, float y, float lp, float np, float gap, bool center, bool shortLabels, bool measureOnly = false)
    {
        bool has = r != null && r.HasTarget;
        double side = Side(r);
        string off = !has ? "--" : side < 0.1 ? "0" : DockGuide.M(side);
        if (shortLabels && has) off = off.Replace(" M", "");
        var parts = new (string L, string V, HudColor C)[]
        {
            (shortLabels ? "OFF " : "OFFSET ", off, !has ? t.Dim : side <= DockGuide.LockDistance ? t.Good : t.Text),
            ("ALIGN ", has ? Deg(r.Misalign) : "--", has && r.Misalign > DockGuide.LockAngle * 0.5 ? AlignColor(t, r) : t.Text),
            ("ROLL ", s.Roll >= 0 ? Deg(s.Roll) : "--", s.Roll >= 2 ? RollColor(t, s) : t.Text),
        };
        float total = 0;
        foreach (var p in parts) total += c.TextWidth(p.L, lp, StyleKit.Mono) + c.TextWidth(p.V, np, StyleKit.Mono);
        total += gap * (parts.Length - 1);
        if (measureOnly) return total;
        float px = center ? x - total / 2 : x;
        foreach (var p in parts)
        {
            c.Text(p.L, px, y + (np - lp) * 0.75f, lp, Label, TextAlign.Left, StyleKit.Mono);
            px += c.TextWidth(p.L, lp, StyleKit.Mono);
            c.Text(p.V, px, y, np, p.C, TextAlign.Left, StyleKit.Mono);
            px += c.TextWidth(p.V, np, StyleKit.Mono) + gap;
        }
        return total;
    }

    // ------------------------------------------------------------------ full screen

    private static void DrawFull(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        float u = UFull(w, h), hair = Math.Max(1, u);
        var v = new Box(0, 0, w, h);
        var (phase, level) = DockGuide.Phase(r);
        bool has = r != null && r.HasTarget;
        float cx = w / 2, cy = h / 2, k = u;

        if (!s.LiveView) c.Rect(0, 0, w, h, t.Background.WithAlpha(0.97f));
        else Vignette(c, w, h, 0.28f, 0.55f);
        ViewLines(c, t, r, s, v, u, Level(t, level), Look(t));
        Reticle(c, t, r, s, cx, cy, k, u, true);

        // header
        float tp = 20 * u, lp = 14 * u;
        c.Text("DOCKING ASSIST", 32 * u, 26 * u, tp, t.Text, TextAlign.Left, StyleKit.Mono);
        if (s.LiveView)
        {
            float lx = 32 * u + c.TextWidth("DOCKING ASSIST", tp, StyleKit.Mono) + 16 * u, dot = 8 * u;
            c.Shape(HudShape.Circle, lx, 26 * u + tp / 2 - dot / 2, dot, dot, t.Main);
            c.Text("LIVE", lx + dot + 8 * u, 26 * u + (tp - lp) / 2, lp, t.Main, TextAlign.Left, StyleKit.Mono);
        }
        string pair = has ? r.Mine.ToUpperInvariant() + "  >  " + r.Target.ToUpperInvariant() : Pair(r);
        c.Text(Fit(c, pair, 16 * u, w * 0.45f), w - 32 * u, 28 * u, 16 * u, t.Text, TextAlign.Right, StyleKit.Mono);

        PhasePill(c, t, phase, level, s, cx, cy - 300 * k - 72 * u, 15 * u, u, true);

        // the numbers beside the ring
        float ny = cy - 70 * u, big = 54 * u;
        float lx2 = cx - 380 * k;
        c.Text("RANGE", lx2, ny, lp, Label, TextAlign.Right, StyleKit.Mono);
        c.Text(has ? DockGuide.M(r.Range).Replace(" M", "") : "--", lx2, ny + lp + 4 * u, big, has ? t.Text : t.Dim, TextAlign.Right, StyleKit.Mono);
        c.Text("METRES", lx2, ny + lp + big + 8 * u, lp, Label, TextAlign.Right, StyleKit.Mono);
        float rx2 = cx + 380 * k;
        var vc = SpeedColor(t, r);
        string sp = SpeedText(r).Replace(" M/S", "");
        c.Text("CLOSING", rx2, ny, lp, Label, TextAlign.Left, StyleKit.Mono);
        c.Text(sp, rx2, ny + lp + 4 * u, FitPx(c, sp, big, w - rx2 - 32 * u, 14), has ? (vc.Equals(t.Good) ? t.Text : vc) : t.Dim, TextAlign.Left, StyleKit.Mono);
        float my = ny + lp + big + 8 * u;
        c.Text("M/S", rx2, my, lp, Label, TextAlign.Left, StyleKit.Mono);
        if (has) c.Text("·  SAFE " + DockGuide.Mps(r.Safe).Replace(" M/S", ""), rx2 + c.TextWidth("M/S  ", lp, StyleKit.Mono), my, lp, t.Warn, TextAlign.Left, StyleKit.Mono);

        // readouts under the ring
        Readouts(c, t, r, s, cx, cy + 316 * k, lp, 26 * u, 34 * u, true, false);

        // the next step
        string advice = DockGuide.Advice(r), eta = Eta(r);
        float ap = 24 * u, np = 14 * u, nw = c.TextWidth("NEXT", np, StyleKit.Mono);
        float ew = eta.Length > 0 ? c.TextWidth(eta, np, StyleKit.Mono) + 24 * u : 0;
        float bw = Math.Min(w - 64 * u, nw + 16 * u + c.TextWidth(advice, ap, StyleKit.Mono) + ew + 60 * u), bh = 48 * u;
        var nb = new Box(cx - bw / 2, h - 34 * u - bh, bw, bh);
        c.Rect(nb.X, nb.Y, nb.W, nb.H, HudColor.Black.WithAlpha(0.55f));
        c.Rect(nb.X, nb.Y, nb.W, hair, t.Main.WithAlpha(0.6f));
        c.Rect(nb.X, nb.B - hair, nb.W, hair, t.Main.WithAlpha(0.6f));
        c.Text("NEXT", nb.X + 30 * u, nb.CY - np / 2, np, t.Main, TextAlign.Left, StyleKit.Mono);
        float ax = nb.X + 30 * u + nw + 16 * u;
        float apx = FitPx(c, advice, ap, nb.R - 30 * u - ew - ax, 10);
        c.Text(advice, ax, nb.CY - apx / 2, apx, level == 3 ? t.Text : Level(t, level), TextAlign.Left, StyleKit.Mono);
        if (eta.Length > 0) c.Text(eta, nb.R - 30 * u, nb.CY - np / 2, np, Label, TextAlign.Right, StyleKit.Mono);
    }

    // ------------------------------------------------------------------ PiP and screens

    private static void DrawCompact(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        bool tall = h > w * 0.8f, has = r != null && r.HasTarget;
        float u = UCompact(w, h, tall), hair = Math.Max(1, u);
        var v = new Box(0, 0, w, h);
        var (phase, level) = DockGuide.Phase(r);

        // the next-step bar sets how much room the ring has
        float ap = Math.Max(8, 16 * u), np = Math.Max(7, 12 * u), nh = ap + (tall ? 16 : 14) * u, ny = h - nh;
        float k = tall ? Math.Min(w, h) / 1080f : Math.Min(h / 950f, w / 1700f);
        float cx = w / 2, cy = tall ? h * 0.46f : h / 2;

        if (!s.LiveView) c.Rect(0, 0, w, h, t.Background.WithAlpha(0.97f));
        else Vignette(c, w, h, tall ? 0.34f : 0.3f, 0.62f);
        ViewLines(c, t, r, s, v, u, Level(t, level), Look(t));
        Reticle(c, t, r, s, cx, cy, k, u, false);

        // title and phase
        float tp = Math.Max(8, 14 * u), lp = Math.Max(7, 12 * u);
        c.Text("DOCK ASSIST", 12 * u, 10 * u, tp, t.Text, TextAlign.Left, StyleKit.Mono);
        float pw = c.TextWidth(phase, lp, StyleKit.Mono) + 24 * u;
        PhasePill(c, t, phase, level, s, 0, 9 * u, lp, u, false, w - 12 * u);
        if (s.LiveView)
        {
            float lx = 12 * u + c.TextWidth("DOCK ASSIST", tp, StyleKit.Mono) + 10 * u, dot = Math.Max(3, 6 * u);
            if (lx + dot + 6 * u + c.TextWidth("LIVE", lp, StyleKit.Mono) < w - 12 * u - pw - 10 * u)
            {
                c.Shape(HudShape.Circle, lx, 10 * u + tp / 2 - dot / 2, dot, dot, t.Main);
                c.Text("LIVE", lx + dot + 6 * u, 10 * u + (tp - lp) / 2, lp, t.Main, TextAlign.Left, StyleKit.Mono);
            }
        }

        // the numbers
        float big = Math.Max(10, 36 * u), unit = Math.Max(7, 14 * u);
        string rv = has ? DockGuide.M(r.Range) : "--";
        var vc = SpeedColor(t, r);
        string cv = SpeedText(r);
        var cvCol = has ? (vc.Equals(t.Good) ? t.Text : vc) : t.Dim;
        string safe = has ? DockGuide.Mps(r.Safe).Replace(" M/S", "") : "";
        if (!tall)
        {
            float ny2 = cy - 40 * u;
            float lx = cx - 340 * k - 28 * u, rx = cx + 340 * k + 28 * u;
            c.Text("RANGE", lx, ny2, lp, Label, TextAlign.Right, StyleKit.Mono);
            c.Text(rv.Replace(" M", ""), lx, ny2 + lp + 2 * u, FitPx(c, rv.Replace(" M", ""), big, lx - 8 * u, 9), has ? t.Text : t.Dim, TextAlign.Right, StyleKit.Mono);
            c.Text("M", lx, ny2 + lp + big + 4 * u, lp, Label, TextAlign.Right, StyleKit.Mono);
            string cvn = cv.Replace(" M/S", "");
            c.Text("CLOSING", rx, ny2, lp, Label, TextAlign.Left, StyleKit.Mono);
            c.Text(cvn, rx, ny2 + lp + 2 * u, FitPx(c, cvn, big, w - rx - 8 * u, 9), cvCol, TextAlign.Left, StyleKit.Mono);
            c.Text("M/S", rx, ny2 + lp + big + 4 * u, lp, Label, TextAlign.Left, StyleKit.Mono);
            if (has) c.Text("·  " + safe, rx + c.TextWidth("M/S  ", lp, StyleKit.Mono), ny2 + lp + big + 4 * u, lp, t.Warn, TextAlign.Left, StyleKit.Mono);

            float rp = Math.Max(8, 16 * u);
            float need = Readouts(c, t, r, s, 0, 0, lp, rp, 14 * u, false, true, true);
            if (need < w - 28 * u) Readouts(c, t, r, s, 14 * u, ny - 10 * u - rp, lp, rp, 14 * u, false, true);
        }
        else
        {
            float ny2 = cy + 300 * k * 0.66f;
            c.Text("RANGE", 12 * u, ny2, lp, Label, TextAlign.Left, StyleKit.Mono);
            ValUnit(c, rv, 12 * u, ny2 + lp + 2 * u, big, unit, has ? t.Text : t.Dim, t.Text, false);
            float cl = c.TextWidth("CLOSING" + (has ? "  ·  " : ""), lp, StyleKit.Mono), sw = has ? c.TextWidth(safe, lp, StyleKit.Mono) : 0;
            c.Text("CLOSING" + (has ? "  ·  " : ""), w - 12 * u - sw, ny2, lp, Label, TextAlign.Right, StyleKit.Mono);
            if (has) c.Text(safe, w - 12 * u, ny2, lp, t.Warn, TextAlign.Right, StyleKit.Mono);
            ValUnit(c, cv, w - 12 * u, ny2 + lp + 2 * u, big, unit, cvCol, t.Text, true);

            float rp = Math.Max(8, 17 * u);
            float ry = Math.Min(cy + 300 * k * 1.35f, ny - 10 * u - rp);
            float need = Readouts(c, t, r, s, 0, 0, lp, rp, 14 * u, false, false, true);
            if (need < w - 24 * u) Readouts(c, t, r, s, w / 2, ry, lp, rp, 14 * u, true, false);
            else Readouts(c, t, r, s, w / 2, ry, lp, rp, 10 * u, true, true);
        }

        // the next step
        c.Rect(0, ny, w, nh, HudColor.Black.WithAlpha(tall ? 0.7f : 0.65f));
        c.Rect(0, ny, w, hair, t.Main.WithAlpha(0.6f));
        c.Text("NEXT", 14 * u, ny + (nh - np) / 2, np, t.Main, TextAlign.Left, StyleKit.Mono);
        float ax = 14 * u + c.TextWidth("NEXT", np, StyleKit.Mono) + 10 * u;
        string advice = DockGuide.Advice(r);
        float apx = FitPx(c, advice, ap, w - 14 * u - ax, 7);
        c.Text(advice, ax, ny + (nh - apx) / 2, apx, level == 3 ? t.Text : Level(t, level), TextAlign.Left, StyleKit.Mono);
    }
}
