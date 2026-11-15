using System;
using System.Globalization;

namespace ProtogenDocking;

/// <summary>
/// The Docking Assist screen (the "DOCK - Docking Assist" LCD script): a drawn approach view with the approach tunnel,
/// an offset scope, range and closing-speed gauges, a phase badge and the next step. Screens can't show a real camera
/// picture, so everything is drawn to read at a glance. Pixels; k = scale (1 at about 360 px tall).
///
///   wide screens:  [ header                         ]      square / tall:  [ header           ]
///                  [ view (tunnel)   | scope        ]                      [ view (tunnel)    ]
///                  [                 | range gauge  ]                      [ scope | gauges   ]
///                  [                 | speed gauge  ]                      [ next step        ]
///                  [ next step                      ]
/// </summary>
public static class DockAssistRenderer
{
    private struct Box { public float X, Y, W, H; public Box(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; } }

    private static float K(DockScreen s, float h) => s.Scale > 0 ? s.Scale : h / 360f;
    private static float Px(float v, float k, float min) => Math.Max(min, v * k);

    /// <summary>Where the parts go. Sets s.View* (the drawn view), which the screen's lens needs before it projects anything.</summary>
    public static void Layout(DockScreen s, float w, float h)
    {
        Parts(s, w, h, out var view, out _, out _, out _, out _);
        s.ViewX = view.X; s.ViewY = view.Y; s.ViewW = view.W; s.ViewH = view.H;
    }

    private static void Parts(DockScreen s, float w, float h, out Box view, out Box side, out Box header, out Box footer, out bool wide)
    {
        float k = K(s, h), m = Math.Max(4, 8 * k), gap = Math.Max(3, 6 * k);
        float hh = Px(15, k, 12) + Px(11, k, 9) + 8 * k;
        float fh = Px(18, k, 12) + 14 * k;
        header = new Box(m, m, w - 2 * m, hh);
        footer = new Box(m, h - m - fh, w - 2 * m, fh);
        float y0 = m + hh + gap, bodyH = footer.Y - gap - y0;
        wide = w > h * 1.25f;
        if (wide)
        {
            float vw = (w - 2 * m - gap) * 0.58f;
            view = new Box(m, y0, vw, bodyH);
            side = new Box(m + vw + gap, y0, w - 2 * m - vw - gap, bodyH);
        }
        else
        {
            float vh = (bodyH - gap) * 0.6f;
            view = new Box(m, y0, w - 2 * m, vh);
            side = new Box(m, y0 + vh + gap, w - 2 * m, bodyH - vh - gap);
        }
    }

    public static void Draw(ICanvas c, DockTheme t, DockReading r, DockScreen s, float w, float h)
    {
        float k = K(s, h);
        Parts(s, w, h, out var view, out var side, out var header, out var footer, out bool wide);
        var (phase, level) = DockGuide.Phase(r);
        var pc = Level(t, level);

        if (s.LiveView)
        {
            // the live picture shows through the view box: the backing goes round it
            float vb = view.Y + view.H;
            foreach (var (bx, by, bw, bh) in new[] { (0f, 0f, w, view.Y), (0f, vb, w, h - vb), (0f, view.Y, view.X, view.H), (view.X + view.W, view.Y, w - view.X - view.W, view.H) })
            {
                if (bw <= 0 || bh <= 0) continue;
                c.Rect(bx, by, bw, bh, HudColor.Black.WithAlpha(0.94f));
                c.Rect(bx, by, bw, bh, t.Background.WithAlpha(0.5f));
            }
        }
        else
        {
            c.Rect(0, 0, w, h, HudColor.Black.WithAlpha(0.94f));
            c.Rect(0, 0, w, h, t.Background.WithAlpha(0.5f));
        }

        Header(c, t, r, s, header, k, phase, pc, level);
        View(c, t, r, s, view, k, pc);
        if (wide)
        {
            float sc = Math.Min(side.W, side.H * 0.58f);
            Scope(c, t, r, s, new Box(side.X + (side.W - sc) / 2, side.Y, sc, sc), k);
            float gy = side.Y + sc + Math.Max(3, 5 * k);
            Gauges(c, t, r, new Box(side.X, gy, side.W, side.Y + side.H - gy), k);
        }
        else
        {
            float sc = Math.Min(side.H, side.W * 0.42f);
            Scope(c, t, r, s, new Box(side.X, side.Y, sc, sc), k);
            float gx = side.X + sc + Math.Max(4, 10 * k);
            Gauges(c, t, r, new Box(gx, side.Y, side.X + side.W - gx, side.H), k);
        }
        Footer(c, t, r, footer, k, pc, level);
    }

    private static HudColor Level(DockTheme t, int level) => level == 0 ? t.Good : level == 1 ? t.Warn : level == 2 ? t.Bad : t.Main;

    private static bool Blink(DockScreen s, double hz = 2) => ((int)(s.Time * hz)) % 2 == 0;

    // ------------------------------------------------------------------ header and footer

    private static void Header(ICanvas c, DockTheme t, DockReading r, DockScreen s, Box b, float k, string phase, HudColor pc, int level)
    {
        float tp = Px(15, k, 12), sp = Px(11, k, 9);
        c.Text(s.Title, b.X + 2 * k, b.Y, tp, t.Main, TextAlign.Left, true);
        // the phase badge, top right; blinks when it wants you to act
        float bp = Px(13, k, 11), bw = c.TextWidth(phase, bp, true) + 16 * k, bh = bp + 6 * k;
        float bx = b.X + b.W - bw;
        bool lit = level == 3 || level == 1 || Blink(s);
        c.Rect(bx, b.Y - 2 * k, bw, bh, pc.WithAlpha(lit ? 0.28f : 0.1f));
        DockCamRenderer.Brackets(c, bx, b.Y - 2 * k, bw, bh, Math.Max(1, 1.5f * k), pc, k, Math.Min(bw, bh) * 0.35f);
        c.Text(phase, bx + bw / 2, b.Y + 1 * k, bp, lit ? pc : pc.WithAlpha(0.6f), TextAlign.Center, true);
        // the pair
        string pair = r == null || r.Mine.Length == 0 ? "NO CONNECTOR ON THIS SHIP"
            : r.HasTarget ? r.Mine.ToUpperInvariant() + "  >  " + r.Target.ToUpperInvariant() + (r.TargetGrid.Length > 0 ? "  (" + r.TargetGrid.ToUpperInvariant() + ")" : "")
            : r.Mine.ToUpperInvariant() + "  >  NO DOCK IN RANGE";
        c.Text(Fit(c, pair, sp, b.W - 4 * k), b.X + 2 * k, b.Y + tp + 4 * k, sp, t.Text.WithAlpha(0.75f), TextAlign.Left, true);
        c.Rect(b.X, b.Y + b.H - Math.Max(1, k), b.W, Math.Max(1, k), t.Main.WithAlpha(0.35f));
    }

    private static void Footer(ICanvas c, DockTheme t, DockReading r, Box b, float k, HudColor pc, int level)
    {
        c.Rect(b.X, b.Y, b.W, b.H, pc.WithAlpha(0.16f));
        c.Rect(b.X, b.Y, Math.Max(2, 4 * k), b.H, pc);
        string eta = "";
        double ttc = DockGuide.TimeToContact(r);
        if (ttc >= 0 && !r.Locked) eta = ttc >= 600 ? "CONTACT > 10 MIN" : "CONTACT " + ttc.ToString(ttc < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + " S";
        float ep = Px(11, k, 9), ew = eta.Length > 0 ? c.TextWidth(eta, ep, true) + 10 * k : 0;
        string advice = DockGuide.Advice(r);
        float ap = Px(18, k, 12), room = b.W - 14 * k - ew;
        float tw = c.TextWidth(advice, ap, true);
        if (tw > room) ap = Math.Max(8, ap * room / tw);
        c.Text(advice, b.X + 10 * k, b.Y + (b.H - ap) / 2, ap, level == 3 ? t.Text : pc, TextAlign.Left, true);
        if (ew > 0) c.Text(eta, b.X + b.W - 6 * k, b.Y + (b.H - ep) / 2, ep, t.Text.WithAlpha(0.8f), TextAlign.Right, true);
    }

    // ------------------------------------------------------------------ the drawn view

    private static void View(ICanvas c, DockTheme t, DockReading r, DockScreen s, Box v, float k, HudColor pc)
    {
        if (!s.LiveView) c.Rect(v.X, v.Y, v.W, v.H, HudColor.Black.WithAlpha(0.55f));
        float lt = Math.Max(1, 1.3f * k);
        float cx = v.X + v.W / 2, cy = v.Y + v.H / 2;

        // the dock ship's outline and the dock face
        // kind 3: your connector's own square, green when it's rolled the same as the dock's
        var mineCol = s.Roll < 0 ? t.Text : s.Roll < 2 ? t.Good : s.Roll < 8 ? t.Warn : t.Bad;
        foreach (var e in s.Edges)
            Clipped(c, v, e.X1, e.Y1, e.X2, e.Y2, e.Kind == 1 || e.Kind == 3 ? lt * 1.6f : lt,
                    e.Kind == 3 ? mineCol : e.Kind == 1 ? t.Main : e.Kind == 0 ? t.Dim.WithAlpha(0.8f) : t.Dim.WithAlpha(0.4f));

        // the approach tunnel: gates between you and the dock bright (nearest the dock brightest), the ones behind you faint
        if (r != null && r.HasTarget)
            foreach (var g in s.Tunnel)
            {
                bool ahead = g.D <= r.Range + 0.5;
                float a = ahead ? (float)(0.85 - 0.5 * Math.Min(1, g.D / 150)) : 0.12f;
                if (g.Rail) a *= 0.55f;
                Clipped(c, v, g.X1, g.Y1, g.X2, g.Y2, g.Rail ? Math.Max(1, k) : lt, (ahead ? pc : t.Dim).WithAlpha(a));
            }

        // your connector's axis
        var cross = r != null && (r.InLockRange || r.Connectable || r.Locked) ? t.Good : t.Main;
        float gap = 7 * k, arm = 18 * k, th = Math.Max(1, 2 * k);
        c.Rect(cx - gap - arm, cy - th / 2, arm, th, cross);
        c.Rect(cx + gap, cy - th / 2, arm, th, cross);
        c.Rect(cx - th / 2, cy - gap - arm, th, arm, cross);
        c.Rect(cx - th / 2, cy + gap, th, arm, cross);

        if (r == null || !r.HasTarget)
        {
            string msg = r == null || r.Mine.Length == 0 ? "NO CONNECTOR" : "SEARCHING FOR A DOCK";
            float mp = Px(14, k, 10);
            if (Blink(s, 1)) c.Text(msg, cx, cy + arm + gap + 6 * k, mp, t.Warn, TextAlign.Center, true);
            DockCamRenderer.Brackets(c, v.X, v.Y, v.W, v.H, Math.Max(1, 1.5f * k), t.Main.WithAlpha(0.6f), k, 16 * k);
            return;
        }

        // final approach: a ring round the crosshair that fills as you close the last 5 m
        if (r.Range < 5 && !r.Locked)
        {
            float rr = 30 * k;
            int n = 24, lit = (int)Math.Round(n * (1 - r.Range / 5));
            var rc = r.Misalign <= DockGuide.LockAngle ? t.Good : t.Warn;
            for (int i = 0; i < n; i++)
            {
                double a = -Math.PI / 2 + i * 2 * Math.PI / n;
                float d = Math.Max(2, 3.5f * k);
                c.Rect(cx + (float)Math.Cos(a) * rr - d / 2, cy + (float)Math.Sin(a) * rr - d / 2, d, d, i < lit ? rc : t.Dim.WithAlpha(0.35f), (float)a);
            }
        }
        if (r.InLockRange || r.Connectable)
            Dashes(c, cx, cy, (Blink(s, 3) ? 40 : 46) * k, 20, Math.Max(2, 2.5f * k), 0.6f, t.Good);

        var lvl = r.Locked || r.Connectable || r.InLockRange ? t.Good : r.SpeedLevel == 2 ? t.Bad : r.SpeedLevel == 1 ? t.Warn : t.Main;
        if (s.Target.HasValue && Inside(v, s.Target.Value.X, s.Target.Value.Y))
        {
            var (tx, ty) = s.Target.Value;
            float bs = (float)Math.Max(16 * k, Math.Min(90 * k, 1400 * k / Math.Max(1, r.Range)));
            DockCamRenderer.Brackets(c, tx - bs / 2, ty - bs / 2, bs, bs, Math.Max(1, 2 * k), lvl, k);
            if (Math.Abs(tx - cx) + Math.Abs(ty - cy) > 20 * k) Clipped(c, v, cx, cy, tx, ty, Math.Max(1, k), lvl.WithAlpha(0.5f));
            float lp = Px(11, k, 9);
            bool left = tx + bs / 2 + 70 * k > v.X + v.W;
            c.Text(DockGuide.M(r.Range), left ? tx - bs / 2 - 4 * k : tx + bs / 2 + 4 * k, ty - bs / 2, lp, lvl, left ? TextAlign.Right : TextAlign.Left, true);
        }
        else
        {
            // off the view: an arrow on the edge towards it
            var (dx, dy) = s.OffScreen ?? Dir(s, cx, cy);
            float ex = cx + dx * (v.W / 2 - 18 * k), ey = cy + dy * (v.H / 2 - 18 * k);
            float ang = (float)Math.Atan2(dy, dx) + (float)(Math.PI / 2);
            c.Shape(HudShape.Triangle, ex - 10 * k, ey - 10 * k, 20 * k, 20 * k, lvl, ang);
        }

        // which way to slide: chevrons running out from the centre
        double side = Math.Sqrt(r.OffRight * r.OffRight + r.OffUp * r.OffUp);
        if (!r.Locked && !r.InLockRange && side > Math.Max(0.4, Math.Min(3, r.Range * 0.05)) && r.Ahead > 0)
        {
            float ux = (float)(r.OffRight / side), uy = (float)(-r.OffUp / side);
            float ang = (float)Math.Atan2(uy, ux) + (float)(Math.PI / 2);
            int step = (int)(s.Time * 4) % 3;
            float reach = Math.Min(v.W, v.H) * 0.5f - 16 * k;
            for (int i = 0; i < 3; i++)
            {
                float d = reach * (0.45f + 0.18f * i), sz = (10 + 2 * i) * k;
                c.Shape(HudShape.Triangle, cx + ux * d - sz / 2, cy + uy * d - sz / 2, sz, sz, t.Warn.WithAlpha(i == step ? 0.95f : 0.3f), ang);
            }
        }

        DockCamRenderer.Brackets(c, v.X, v.Y, v.W, v.H, Math.Max(1, 1.5f * k), t.Main.WithAlpha(0.6f), k, 16 * k);
    }

    private static (float, float) Dir(DockScreen s, float cx, float cy)
    {
        if (!s.Target.HasValue) return (0, 1);
        float dx = s.Target.Value.X - cx, dy = s.Target.Value.Y - cy, l = (float)Math.Sqrt(dx * dx + dy * dy);
        return l < 1e-3f ? (0, 1) : (dx / l, dy / l);
    }

    // ------------------------------------------------------------------ the offset scope

    /// <summary>Where the dock sits across your connector's axis, on a log scale (lock zone, 0.5 m, 2 m and 10 m rings),
    /// with where it'll be in 3 s at the present drift, and the outer ring coloured by how square you face it.</summary>
    private static void Scope(ICanvas c, DockTheme t, DockReading r, DockScreen s, Box b, float k)
    {
        float cx = b.X + b.W / 2, cy = b.Y + b.H / 2, lp = Px(10, k, 9);
        float R = b.W / 2 - lp - 4 * k;
        c.Rect(b.X, b.Y, b.W, b.H, HudColor.Black.WithAlpha(0.45f));
        var align = r == null || !r.HasTarget ? t.Dim : r.Misalign <= DockGuide.LockAngle * 0.5 ? t.Good : r.Misalign <= DockGuide.LockAngle ? t.Warn : t.Bad;
        // outer ring: how square you face the dock (dashes go out as it gets worse)
        int dashes = 36, litDashes = r == null || !r.HasTarget ? 0 : (int)Math.Round(dashes * Math.Max(0, 1 - r.Misalign / 90));
        for (int i = 0; i < dashes; i++)
        {
            double a = -Math.PI / 2 + i * 2 * Math.PI / dashes;
            float dl = (float)(2 * Math.PI * R / dashes * 0.6), dw = Math.Max(2, 3 * k);
            c.Rect(cx + (float)Math.Cos(a) * R - dl / 2, cy + (float)Math.Sin(a) * R - dw / 2, dl, dw, i < litDashes ? align : t.Dim.WithAlpha(0.35f), (float)(a + Math.PI / 2));
        }
        foreach (double m in new[] { 0.5, 2.0 })
            Dashes(c, cx, cy, Map(m, R), 28, Math.Max(1, 1.5f * k), 0.35f, t.Dim.WithAlpha(0.7f));
        float lz = Map(DockGuide.LockDistance, R);
        c.Shape(HudShape.Circle, cx - lz, cy - lz, 2 * lz, 2 * lz, t.Good.WithAlpha(0.18f));
        float th = Math.Max(1, k);
        c.Rect(cx - R, cy - th / 2, 2 * R, th, t.Dim.WithAlpha(0.45f));
        c.Rect(cx - th / 2, cy - R, th, 2 * R, t.Dim.WithAlpha(0.45f));
        c.Text("OFFSET", b.X + 3 * k, b.Y + 2 * k, lp, t.Main.WithAlpha(0.85f), TextAlign.Left, true);
        if (r == null || !r.HasTarget) { c.Text("--", cx, cy + R * 0.35f, lp, t.Dim, TextAlign.Center, true); return; }

        double side = Math.Sqrt(r.OffRight * r.OffRight + r.OffUp * r.OffUp);
        c.Text(side < 0.1 ? "ON AXIS" : DockGuide.M(side), b.X + b.W - 3 * k, b.Y + 2 * k, lp, side < 0.4 ? t.Good : t.Text, TextAlign.Right, true);
        c.Text("ALIGN " + r.Misalign.ToString("0.0", CultureInfo.InvariantCulture) + "°" + (s.Roll >= 0 ? "  ROLL " + s.Roll.ToString("0", CultureInfo.InvariantCulture) + "°" : ""),
               cx, b.Y + b.H - lp - 2 * k, lp, s.Roll >= 8 && r.Misalign < 22 ? t.Warn : align, TextAlign.Center, true);

        var (px, py) = Spot(r.OffRight, r.OffUp, cx, cy, R);
        // where the dock will be in 3 s: it moves against your drift
        var (fx, fy) = Spot(r.OffRight - r.DriftRight * 3, r.OffUp - r.DriftUp * 3, cx, cy, R);
        if (Math.Abs(fx - px) + Math.Abs(fy - py) > 2 * k)
        {
            DockCamRenderer.Line(c, px, py, fx, fy, Math.Max(1, 1.5f * k), t.Text.WithAlpha(0.6f));
            float e = Math.Max(3, 5 * k);
            c.Rect(fx - e / 2, fy - e / 2, e, e, t.Text.WithAlpha(0.7f), (float)(Math.PI / 4));
        }
        var dc = side <= DockGuide.LockDistance ? t.Good : side < 2 ? t.Warn : t.Bad;
        float d = Math.Max(5, 10 * k);
        c.Shape(HudShape.Circle, px - d / 2, py - d / 2, d, d, dc);
        if (side > 10 && Blink(s)) Dashes(c, px, py, d, 8, Math.Max(1, 1.5f * k), 0.5f, dc); // off the scale
    }

    /// <summary>A circle of n short dashes (thin, at any size; the ring sprite's line grows with its size).</summary>
    private static void Dashes(ICanvas c, float cx, float cy, float r, int n, float th, float fill, HudColor col)
    {
        float dl = (float)(2 * Math.PI * r / n * fill);
        for (int i = 0; i < n; i++)
        {
            double a = i * 2 * Math.PI / n;
            c.Rect(cx + (float)Math.Cos(a) * r - dl / 2, cy + (float)Math.Sin(a) * r - th / 2, dl, th, col, (float)(a + Math.PI / 2));
        }
    }

    /// <summary>Metres off the axis to scope radius: log scale, 10 m (and more) at the edge.</summary>
    private static float Map(double m, float R) => (float)(Math.Min(1, Math.Log(1 + Math.Abs(m) / 0.3) / Math.Log(1 + 10 / 0.3)) * R);

    private static (float, float) Spot(double right, double up, float cx, float cy, float R)
    {
        double l = Math.Sqrt(right * right + up * up);
        if (l < 1e-4) return (cx, cy);
        float rr = Map(l, R);
        return (cx + (float)(right / l) * rr, cy - (float)(up / l) * rr);
    }

    // ------------------------------------------------------------------ the gauges

    private static void Gauges(ICanvas c, DockTheme t, DockReading r, Box b, float k)
    {
        float gh = b.H / 2;
        RangeGauge(c, t, r, new Box(b.X, b.Y, b.W, gh), k);
        SpeedGauge(c, t, r, new Box(b.X, b.Y + gh, b.W, gh), k);
    }

    /// <summary>Label and value over a bar; returns the bar's box.</summary>
    private static Box GaugeFrame(ICanvas c, DockTheme t, Box b, float k, string label, string value, HudColor vc)
    {
        float lp = Px(10, k, 9), vp = Px(14, k, 12);
        c.Text(label, b.X, b.Y + (vp - lp), lp, t.Main.WithAlpha(0.85f), TextAlign.Left, true);
        c.Text(value, b.X + b.W, b.Y, vp, vc, TextAlign.Right, true);
        float by = b.Y + vp + 4 * k, bh = Math.Max(4, Math.Min(12 * k, b.Y + b.H - by - 4 * k));
        var bar = new Box(b.X, by, b.W, bh);
        c.Rect(bar.X, bar.Y, bar.W, bar.H, t.Dim.WithAlpha(0.25f));
        return bar;
    }

    private static readonly double[] RangeTicks = { 1, 5, 20, 50, 100 };
    private const double RangeTop = 200;
    private static float RangeAt(double m) => (float)(Math.Log(1 + Math.Max(0, m)) / Math.Log(1 + RangeTop));

    /// <summary>Range on a log scale, the dock at the left: the bar shrinks towards it as you come in.</summary>
    private static void RangeGauge(ICanvas c, DockTheme t, DockReading r, Box b, float k)
    {
        bool has = r != null && r.HasTarget;
        var bar = GaugeFrame(c, t, b, k, "RANGE", has ? DockGuide.M(r.Range) : "--", t.Text);
        foreach (double m in RangeTicks)
            c.Rect(bar.X + bar.W * RangeAt(m) - Math.Max(1, k) / 2, bar.Y, Math.Max(1, k), bar.H, t.Background.WithAlpha(0.9f));
        c.Rect(bar.X, bar.Y, Math.Max(2, 3 * k), bar.H, t.Good); // the dock
        if (!has) return;
        float f = Math.Min(1, RangeAt(r.Range));
        var col = r.Range < 5 ? t.Good : t.Main;
        c.Rect(bar.X, bar.Y + bar.H * 0.25f, bar.W * f, bar.H * 0.5f, col.WithAlpha(0.75f));
        float mx = bar.X + bar.W * f, sz = Math.Max(6, 9 * k);
        c.Shape(HudShape.Triangle, mx - sz / 2, bar.Y + bar.H, sz, sz, col);
    }

    /// <summary>Closing speed against the comfortable one for this range: green up to it, amber, then red.</summary>
    private static void SpeedGauge(ICanvas c, DockTheme t, DockReading r, Box b, float k)
    {
        bool has = r != null && r.HasTarget;
        if (!has) { GaugeFrame(c, t, b, k, "CLOSING", "--", t.Text); return; }
        bool opening = r.Closing < -0.05;
        var vc = opening ? t.Warn : r.SpeedLevel == 2 ? t.Bad : r.SpeedLevel == 1 ? t.Warn : t.Good;
        string value = opening ? "OPENING " + DockGuide.Mps(-r.Closing) : DockGuide.Mps(r.Closing);
        var bar = GaugeFrame(c, t, b, k, "CLOSING / SAFE " + DockGuide.Mps(r.Safe), value, vc);
        double top = Math.Max(r.Safe * 2, Math.Max(0.5, r.Closing * 1.1));
        float g = (float)(r.Safe / top), a = (float)(r.Safe * 1.6 / top);
        c.Rect(bar.X, bar.Y, bar.W * g, bar.H, t.Good.WithAlpha(0.3f));
        c.Rect(bar.X + bar.W * g, bar.Y, bar.W * (a - g), bar.H, t.Warn.WithAlpha(0.3f));
        c.Rect(bar.X + bar.W * a, bar.Y, bar.W * (1 - a), bar.H, t.Bad.WithAlpha(0.3f));
        float f = (float)Math.Max(0, Math.Min(1, r.Closing / top));
        c.Rect(bar.X, bar.Y + bar.H * 0.25f, bar.W * f, bar.H * 0.5f, vc);
        float mx = bar.X + bar.W * f, sz = Math.Max(6, 9 * k);
        c.Shape(HudShape.Triangle, mx - sz / 2, bar.Y + bar.H, sz, sz, vc);
    }

    // ------------------------------------------------------------------ helpers

    private static bool Inside(Box b, float x, float y) => x >= b.X && x <= b.X + b.W && y >= b.Y && y <= b.Y + b.H;

    /// <summary>A line cut to the box (screens can't clip, so lines are cut here).</summary>
    private static void Clipped(ICanvas c, Box b, float x1, float y1, float x2, float y2, float th, HudColor col)
    {
        float t0 = 0, t1 = 1, dx = x2 - x1, dy = y2 - y1;
        if (!Cut(-dx, x1 - b.X, ref t0, ref t1) || !Cut(dx, b.X + b.W - x1, ref t0, ref t1) ||
            !Cut(-dy, y1 - b.Y, ref t0, ref t1) || !Cut(dy, b.Y + b.H - y1, ref t0, ref t1)) return;
        DockCamRenderer.Line(c, x1 + dx * t0, y1 + dy * t0, x1 + dx * t1, y1 + dy * t1, th, col);
    }

    private static bool Cut(float p, float q, ref float t0, ref float t1)
    {
        if (Math.Abs(p) < 1e-6f) return q >= 0;
        float u = q / p;
        if (p < 0) { if (u > t1) return false; if (u > t0) t0 = u; }
        else { if (u < t0) return false; if (u < t1) t1 = u; }
        return true;
    }

    private static string Fit(ICanvas c, string text, float px, float width)
    {
        if (c.TextWidth(text, px, true) <= width) return text;
        while (text.Length > 4 && c.TextWidth(text + "...", px, true) > width) text = text.Substring(0, text.Length - 1);
        return text + "...";
    }
}
