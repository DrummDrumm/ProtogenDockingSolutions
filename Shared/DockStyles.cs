using System;
using System.Globalization;

namespace ProtogenDocking;

/// <summary>
/// The Docking Assist's looks (/dock style). Each style has two layouts: the full-screen dock camera's (Full: a big live
/// picture with slim readouts) and the compact one for the PiP and screens (the picture fills the screen, the readouts
/// sit over it). "classic" is the original DockAssistRenderer. Styles bring their own colours and draw all text in the
/// proportional font, thin and small, instead of the block monospace.
/// </summary>
public static class DockStyles
{
    public const string Default = "holo";

    /// <summary>The styles /dock style cycles through, in order.</summary>
    public static readonly string[] Names = { "holo", "tactical", "bracket", "glass", "radial", "classic" };

    public static string Norm(string v)
    {
        v = (v ?? "").Trim().ToLowerInvariant();
        if (v == "hololine" || v == "holo-line" || v == "line") v = "holo";
        if (v == "original" || v == "old") v = "classic";
        if (v == "tac" || v == "phosphor" || v == "green") v = "tactical";
        if (v == "vector" || v == "brackets" || v == "amber") v = "bracket";
        if (v == "mint" || v == "cards") v = "glass";
        if (v == "ring" || v == "reticle" || v == "hud") v = "radial";
        return Array.IndexOf(Names, v) >= 0 ? v : null;
    }

    /// <summary>The places that can have their own look (/dock style full | pip | lcd ...).</summary>
    public static readonly string[] Places = { "full", "pip", "lcd" };

    public static string PlaceName(string place) => place == "full" ? "full screen" : place == "pip" ? "PiP" : place == "lcd" ? "LCDs" : "everywhere";

    /// <summary>A place's own setting ("" = follows AssistStyle).</summary>
    public static string PlaceSetting(DockSettings set, string place) =>
        place == "full" ? set.FullStyle : place == "pip" ? set.PipStyle : place == "lcd" ? set.LcdStyle : "";

    /// <summary>The look for a screen: its own (an LCD's Custom Data), else its place's setting, else AssistStyle.</summary>
    public static string For(DockSettings set, string place, string own) =>
        Norm(own) ?? Norm(PlaceSetting(set, place)) ?? Norm(set.AssistStyle) ?? Default;

    /// <summary>
    /// The look an LCD's Custom Data asks for: a line "DockStyle=radial" (or "Dock Style: radial"; any case). Null when
    /// there's none or it names no style.
    /// </summary>
    public static string FromCustomData(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        foreach (var raw in text.Split('\n'))
        {
            string line = raw.Trim();
            int eq = line.IndexOfAny(new[] { '=', ':' });
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Replace(" ", "").Replace("_", "").ToLowerInvariant();
            if (key == "dockstyle" || key == "assiststyle") return Norm(line.Substring(eq + 1));
        }
        return null;
    }

    public static string Next(string v)
    {
        int i = Array.IndexOf(Names, Norm(v) ?? Default);
        return Names[(i + 1) % Names.Length];
    }

    public static string Title(string v) => (Norm(v) ?? Default) switch
    {
        "holo" => "Holo Line",
        "tactical" => "Tactical Phosphor",
        "bracket" => "Vector Bracket",
        "glass" => "Glass",
        "radial" => "Radial HUD",
        _ => "Classic",
    };

    /// <summary>Four letters for the toolbar slot.</summary>
    public static string Short(string v) { string n = Norm(v) ?? Default; return char.ToUpperInvariant(n[0]) + n.Substring(1, Math.Min(3, n.Length - 1)); }

    /// <summary>The colours a style draws with; classic uses the theme it's given.</summary>
    public static DockTheme Palette(string style, DockTheme theme)
    {
        switch (Norm(style) ?? Default)
        {
            case "holo": return HoloStyle.Palette;
            case "tactical": return TacticalStyle.Palette;
            case "bracket": return BracketStyle.Palette;
            case "glass": return GlassStyle.Palette;
            case "radial": return RadialStyle.Palette;
            default: return theme;
        }
    }

    /// <summary>Where the parts go (sets s.View*, which the lens and the live picture use).</summary>
    public static void Layout(string style, DockScreen s, float w, float h)
    {
        switch (Norm(style) ?? Default)
        {
            case "holo": HoloStyle.Layout(s, w, h); break;
            case "tactical": TacticalStyle.Layout(s, w, h); break;
            case "bracket": BracketStyle.Layout(s, w, h); break;
            case "glass": GlassStyle.Layout(s, w, h); break;
            case "radial": RadialStyle.Layout(s, w, h); break;
            default: DockAssistRenderer.Layout(s, w, h); break;
        }
    }

    public static void Draw(string style, ICanvas c, DockTheme theme, DockReading r, DockScreen s, float w, float h)
    {
        switch (Norm(style) ?? Default)
        {
            case "holo": HoloStyle.Draw(c, r, s, w, h); break;
            case "tactical": TacticalStyle.Draw(c, r, s, w, h); break;
            case "bracket": BracketStyle.Draw(c, r, s, w, h); break;
            case "glass": GlassStyle.Draw(c, r, s, w, h); break;
            case "radial": RadialStyle.Draw(c, r, s, w, h); break;
            default: DockAssistRenderer.Draw(c, theme, r, s, w, h); break;
        }
    }
}

/// <summary>Drawing bits the styles share: thin rings, fades, corner ticks, badges, the view's lines and the readouts' numbers.</summary>
public static class StyleKit
{
    public struct Box
    {
        public float X, Y, W, H;
        public Box(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float R => X + W;
        public float B => Y + H;
        public float CX => X + W / 2;
        public float CY => Y + H / 2;
    }

    public const bool Mono = false;

    /// <summary>A thin circle of short segments (the ring sprite's line grows with its size).</summary>
    public static void Ring(ICanvas c, float cx, float cy, float r, float th, HudColor col, int n = 0)
    {
        if (r <= 0) return;
        if (n <= 0) n = Math.Max(24, Math.Min(96, (int)(r / 2.5f)));
        float dl = (float)(2 * Math.PI * r / n) * 1.08f;
        for (int i = 0; i < n; i++)
        {
            double a = i * 2 * Math.PI / n;
            c.Rect(cx + (float)Math.Cos(a) * r - dl / 2, cy + (float)Math.Sin(a) * r - th / 2, dl, th, col, (float)(a + Math.PI / 2));
        }
    }

    /// <summary>A vertical fade: alpha a0 at the top to a1 at the bottom, in strips.</summary>
    public static void Fade(ICanvas c, float x, float y, float w, float h, HudColor col, float a0, float a1, int strips = 10)
    {
        if (h <= 0 || w <= 0) return;
        float sh = h / strips;
        for (int i = 0; i < strips; i++)
        {
            float a = a0 + (a1 - a0) * (i + 0.5f) / strips;
            c.Rect(x, y + i * sh, w, sh + 0.5f, col.WithAlpha(a));
        }
    }

    /// <summary>A one-pixel frame.</summary>
    public static void Frame(ICanvas c, Box b, float th, HudColor col)
    {
        c.Rect(b.X, b.Y, b.W, th, col);
        c.Rect(b.X, b.B - th, b.W, th, col);
        c.Rect(b.X, b.Y, th, b.H, col);
        c.Rect(b.R - th, b.Y, th, b.H, col);
    }

    /// <summary>L-shaped ticks in the four corners, inset by `inset`.</summary>
    public static void Corners(ICanvas c, Box b, float inset, float len, float th, HudColor col)
    {
        float x0 = b.X + inset, y0 = b.Y + inset, x1 = b.R - inset, y1 = b.B - inset;
        c.Rect(x0, y0, len, th, col); c.Rect(x0, y0, th, len, col);
        c.Rect(x1 - len, y0, len, th, col); c.Rect(x1 - th, y0, th, len, col);
        c.Rect(x0, y1 - th, len, th, col); c.Rect(x0, y1 - len, th, len, col);
        c.Rect(x1 - len, y1 - th, len, th, col); c.Rect(x1 - th, y1 - len, th, len, col);
    }

    /// <summary>A filled badge with its text; (x, y) is its top-left, or top-right when right is set. Returns its width.</summary>
    public static float Badge(ICanvas c, string text, float x, float y, float px, float padX, float padY, HudColor bg, HudColor fg, bool right)
    {
        float tw = c.TextWidth(text, px, Mono), bw = tw + 2 * padX, bh = px + 2 * padY;
        float bx = right ? x - bw : x;
        c.Rect(bx, y, bw, bh, bg);
        c.Text(text, bx + bw / 2, y + padY, px, fg, TextAlign.Center, Mono);
        return bw;
    }

    /// <summary>Text cut to a width with "...".</summary>
    public static string Fit(ICanvas c, string text, float px, float width)
    {
        if (string.IsNullOrEmpty(text) || c.TextWidth(text, px, Mono) <= width) return text;
        while (text.Length > 4 && c.TextWidth(text + "...", px, Mono) > width) text = text.Substring(0, text.Length - 1);
        return text + "...";
    }

    /// <summary>The largest size up to px at which the text fits the width.</summary>
    public static float FitPx(ICanvas c, string text, float px, float width, float min)
    {
        float tw = c.TextWidth(text ?? "", px, Mono);
        return tw > width && tw > 0 ? Math.Max(min, px * width / tw) : px;
    }

    public static bool Blink(DockScreen s, double hz = 2) => ((int)(s.Time * hz)) % 2 == 0;

    public static HudColor Level(DockTheme t, int level) => level == 0 ? t.Good : level == 1 ? t.Warn : level == 2 ? t.Bad : t.Main;

    public static string Deg(double d) => d.ToString(d < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + "°";

    /// <summary>Which way the dock sits off your axis: "R 0.28  U 0.19".</summary>
    public static string Offsets(DockReading r)
    {
        if (r == null || !r.HasTarget) return "";
        string f(double v) => Math.Abs(v).ToString(Math.Abs(v) >= 10 ? "0" : "0.0#", CultureInfo.InvariantCulture);
        return (r.OffRight < 0 ? "L " : "R ") + f(r.OffRight) + "   " + (r.OffUp < 0 ? "D " : "U ") + f(r.OffUp);
    }

    public static double Side(DockReading r) => r == null ? 0 : Math.Sqrt(r.OffRight * r.OffRight + r.OffUp * r.OffUp);

    public static HudColor AlignColor(DockTheme t, DockReading r) =>
        r == null || !r.HasTarget ? t.Dim : r.Misalign <= DockGuide.LockAngle * 0.5 ? t.Good : r.Misalign <= DockGuide.LockAngle ? t.Warn : t.Bad;

    public static HudColor RollColor(DockTheme t, DockScreen s) => s.Roll < 0 ? t.Text : s.Roll < 2 ? t.Good : s.Roll < 8 ? t.Warn : t.Bad;

    public static HudColor SpeedColor(DockTheme t, DockReading r) =>
        r == null || !r.HasTarget ? t.Text : r.Closing < -0.05 ? t.Warn : r.SpeedLevel == 2 ? t.Bad : r.SpeedLevel == 1 ? t.Warn : t.Good;

    public static string SpeedText(DockReading r) =>
        r == null || !r.HasTarget ? "--" : r.Closing < -0.05 ? "OPENING " + DockGuide.Mps(-r.Closing) : DockGuide.Mps(r.Closing);

    /// <summary>Range on the classic log scale (0 at the dock, 1 at 200 m).</summary>
    public static float RangeAt(double m) => (float)Math.Min(1, Math.Log(1 + Math.Max(0, m)) / Math.Log(1 + 200));

    /// <summary>The closing-speed scale's top, and where the safe speed sits on it.</summary>
    public static (double Top, float Safe, float Now) SpeedScale(DockReading r)
    {
        double top = Math.Max(r.Safe * 2, Math.Max(0.5, r.Closing * 1.1));
        return (top, (float)(r.Safe / top), (float)Math.Max(0, Math.Min(1, r.Closing / top)));
    }

    /// <summary>Metres off the axis to scope radius: log scale, 10 m (and more) at the edge.</summary>
    public static float Map(double m, float R) => (float)(Math.Min(1, Math.Log(1 + Math.Abs(m) / 0.3) / Math.Log(1 + 10 / 0.3)) * R);

    public static (float, float) Spot(double right, double up, float cx, float cy, float R)
    {
        double l = Math.Sqrt(right * right + up * up);
        if (l < 1e-4) return (cx, cy);
        float rr = Map(l, R);
        return (cx + (float)(right / l) * rr, cy - (float)(up / l) * rr);
    }

    public static bool Inside(Box b, float x, float y) => x >= b.X && x <= b.R && y >= b.Y && y <= b.B;

    /// <summary>A line cut to the box (screens can't clip, so lines are cut here).</summary>
    public static void Clipped(ICanvas c, Box b, float x1, float y1, float x2, float y2, float th, HudColor col)
    {
        float t0 = 0, t1 = 1, dx = x2 - x1, dy = y2 - y1;
        if (!Cut(-dx, x1 - b.X, ref t0, ref t1) || !Cut(dx, b.R - x1, ref t0, ref t1) ||
            !Cut(-dy, y1 - b.Y, ref t0, ref t1) || !Cut(dy, b.B - y1, ref t0, ref t1)) return;
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

    /// <summary>How the view's lines look in a style.</summary>
    public sealed class ViewLook
    {
        public float Line = 1;          // the thin lines' width (px at scale 1)
        public float Face = 1.8f;       // the dock face and your square
        public HudColor Cross = HudColor.White;
        public float CrossGap = 8, CrossArm = 16;
        public float Bracket = 2;       // the target's corner brackets
        public HudColor Outline;        // the target ship's outline
        public HudColor Tunnel;         // the tunnel ahead of you (fades with distance)
        public HudColor Chevron;
    }

    /// <summary>
    /// The view's lines over the picture (or the drawn view): the target's outline and dock face, your connector's square,
    /// the approach tunnel, your connector's axis, the target's brackets and range, the way to slide, the final ring.
    /// </summary>
    public static void ViewLines(ICanvas c, DockTheme t, DockReading r, DockScreen s, Box v, float u, HudColor pc, ViewLook look)
    {
        float lt = Math.Max(1, look.Line * u), ft = Math.Max(1, look.Face * u);
        float cx = v.CX, cy = v.CY;
        var mineCol = RollColor(t, s);
        foreach (var e in s.Edges)
        {
            var col = e.Kind == 3 ? mineCol : e.Kind == 1 ? t.Main : e.Kind == 0 ? look.Outline : look.Outline.WithAlpha(0.5f);
            Clipped(c, v, e.X1, e.Y1, e.X2, e.Y2, e.Kind == 1 || e.Kind == 3 ? ft : lt, col);
        }
        if (r != null && r.HasTarget)
            foreach (var g in s.Tunnel)
            {
                bool ahead = g.D <= r.Range + 0.5;
                float a = ahead ? (float)(0.75 - 0.5 * Math.Min(1, g.D / 150)) : 0.1f;
                if (g.Rail) a *= 0.6f;
                Clipped(c, v, g.X1, g.Y1, g.X2, g.Y2, lt, (ahead ? look.Tunnel : t.Dim).WithAlpha(a));
            }

        // your connector's axis: a thin open cross
        var cross = r != null && (r.InLockRange || r.Connectable || r.Locked) ? t.Good : look.Cross;
        float gap = look.CrossGap * u, arm = look.CrossArm * u, th = Math.Max(1, 1.5f * u);
        c.Rect(cx - gap - arm, cy - th / 2, arm, th, cross);
        c.Rect(cx + gap, cy - th / 2, arm, th, cross);
        c.Rect(cx - th / 2, cy - gap - arm, th, arm, cross);
        c.Rect(cx - th / 2, cy + gap, th, arm, cross);

        if (r == null || !r.HasTarget)
        {
            string msg = r == null || r.Mine.Length == 0 ? "NO CONNECTOR" : "SEARCHING FOR A DOCK";
            if (Blink(s, 1)) c.Text(msg, cx, cy + gap + arm + 8 * u, Math.Max(9, 15 * u), t.Warn, TextAlign.Center, Mono);
            return;
        }

        if (r.Range < 5 && !r.Locked)
        {
            float rr = 30 * u;
            int n = 32, lit = (int)Math.Round(n * (1 - r.Range / 5));
            var rc = r.Misalign <= DockGuide.LockAngle ? t.Good : t.Warn;
            for (int i = 0; i < n; i++)
            {
                double a = -Math.PI / 2 + i * 2 * Math.PI / n;
                float d = Math.Max(2, 3 * u);
                c.Rect(cx + (float)Math.Cos(a) * rr - d / 2, cy + (float)Math.Sin(a) * rr - d / 2, d, d, i < lit ? rc : t.Dim.WithAlpha(0.3f), (float)a);
            }
        }
        if (r.InLockRange || r.Connectable)
            Ring(c, cx, cy, (Blink(s, 3) ? 40 : 46) * u, Math.Max(1.5f, 2 * u), t.Good, 40);

        var lvl = r.Locked || r.Connectable || r.InLockRange ? t.Good : r.SpeedLevel == 2 ? t.Bad : r.SpeedLevel == 1 ? t.Warn : t.Main;
        if (s.Target.HasValue && Inside(v, s.Target.Value.X, s.Target.Value.Y))
        {
            var (tx, ty) = s.Target.Value;
            float bs = (float)Math.Max(16 * u, Math.Min(90 * u, 1400 * u / Math.Max(1, r.Range)));
            DockCamRenderer.Brackets(c, tx - bs / 2, ty - bs / 2, bs, bs, Math.Max(1, look.Bracket * u), lvl, u);
            if (Math.Abs(tx - cx) + Math.Abs(ty - cy) > 20 * u) Clipped(c, v, cx, cy, tx, ty, Math.Max(1, u), lvl.WithAlpha(0.45f));
            float lp = Math.Max(9, 13 * u);
            bool left = tx + bs / 2 + 70 * u > v.R;
            c.Text(DockGuide.M(r.Range), left ? tx - bs / 2 - 5 * u : tx + bs / 2 + 5 * u, ty - bs / 2, lp, lvl, left ? TextAlign.Right : TextAlign.Left, Mono);
        }
        else
        {
            var (dx, dy) = s.OffScreen ?? Dir(s, cx, cy);
            float ex = cx + dx * (v.W / 2 - 18 * u), ey = cy + dy * (v.H / 2 - 18 * u);
            float ang = (float)Math.Atan2(dy, dx) + (float)(Math.PI / 2);
            c.Shape(HudShape.Triangle, ex - 10 * u, ey - 10 * u, 20 * u, 20 * u, lvl, ang);
        }

        double side = Side(r);
        if (!r.Locked && !r.InLockRange && side > Math.Max(0.4, Math.Min(3, r.Range * 0.05)) && r.Ahead > 0)
        {
            float ux = (float)(r.OffRight / side), uy = (float)(-r.OffUp / side);
            float ang = (float)Math.Atan2(uy, ux) + (float)(Math.PI / 2);
            int step = (int)(s.Time * 4) % 3;
            float reach = Math.Min(v.W, v.H) * 0.5f - 16 * u;
            for (int i = 0; i < 3; i++)
            {
                float d = reach * (0.45f + 0.18f * i), sz = (9 + 2 * i) * u;
                c.Shape(HudShape.Triangle, cx + ux * d - sz / 2, cy + uy * d - sz / 2, sz, sz, look.Chevron.WithAlpha(i == step ? 0.95f : 0.3f), ang);
            }
        }
    }

    private static (float, float) Dir(DockScreen s, float cx, float cy)
    {
        if (!s.Target.HasValue) return (0, 1);
        float dx = s.Target.Value.X - cx, dy = s.Target.Value.Y - cy, l = (float)Math.Sqrt(dx * dx + dy * dy);
        return l < 1e-3f ? (0, 1) : (dx / l, dy / l);
    }

    /// <summary>The backing for the parts of a screen outside the live picture (or the whole screen when there's none).</summary>
    public static void Backing(ICanvas c, DockScreen s, Box view, float w, float h, HudColor bg, float alpha)
    {
        if (!s.LiveView) { c.Rect(0, 0, w, h, bg.WithAlpha(alpha)); return; }
        foreach (var (bx, by, bw, bh) in new[] { (0f, 0f, w, view.Y), (0f, view.B, w, h - view.B), (0f, view.Y, view.X, view.H), (view.R, view.Y, w - view.R, view.H) })
            if (bw > 0 && bh > 0) c.Rect(bx, by, bw, bh, bg.WithAlpha(alpha));
    }

    /// <summary>The pair of connectors: "NOSE > PORT 3 (TYCHO YARD)".</summary>
    public static string Pair(DockReading r) =>
        r == null || r.Mine.Length == 0 ? "NO CONNECTOR ON THIS SHIP"
        : r.HasTarget ? r.Mine.ToUpperInvariant() + "  >  " + r.Target.ToUpperInvariant() + (r.TargetGrid.Length > 0 ? "  (" + r.TargetGrid.ToUpperInvariant() + ")" : "")
        : r.Mine.ToUpperInvariant() + "  >  NO DOCK IN RANGE";

    public static string Eta(DockReading r)
    {
        double ttc = DockGuide.TimeToContact(r);
        if (ttc < 0 || r.Locked) return "";
        return ttc >= 600 ? "CONTACT > 10 MIN" : "CONTACT " + ttc.ToString(ttc < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + " S";
    }

    /// <summary>
    /// A panel with its top-left and bottom-right corners cut off (cut = the corner's size). The corner triangles are
    /// right-triangle sprites, the rest three bands that don't overlap, so a see-through fill stays even.
    /// </summary>
    public static void CutPanel(ICanvas c, Box b, float cut, HudColor fill)
    {
        cut = Math.Min(cut, Math.Min(b.W, b.H) / 2);
        if (cut < 1) { c.Rect(b.X, b.Y, b.W, b.H, fill); return; }
        c.Rect(b.X + cut, b.Y, b.W - cut, cut, fill);
        c.Rect(b.X, b.Y + cut, b.W, b.H - 2 * cut, fill);
        c.Rect(b.X, b.B - cut, b.W - cut, cut, fill);
        // the sprite's right angle is bottom-left; turned a quarter back it's bottom-right, a quarter on top-left
        c.Shape(HudShape.RightTriangle, b.X, b.Y, cut, cut, fill, (float)(-Math.PI / 2));
        c.Shape(HudShape.RightTriangle, b.R - cut, b.B - cut, cut, cut, fill, (float)(Math.PI / 2));
    }

    /// <summary>A badge on a cut-corner panel; (x, y) is its top-left, or top-right when right is set. Returns its width.</summary>
    public static float CutBadge(ICanvas c, string text, float x, float y, float px, float padX, float padY, HudColor bg, HudColor fg, bool right, float cut)
    {
        float bw = c.TextWidth(text, px, Mono) + 2 * padX, bh = px + 2 * padY;
        float bx = right ? x - bw : x;
        CutPanel(c, new Box(bx, y, bw, bh), cut, bg);
        c.Text(text, bx + bw / 2, y + padY, px, fg, TextAlign.Center, Mono);
        return bw;
    }

    /// <summary>A badge drawn as an outline with its text; (x, y) is its top-left, or top-right when right is set. Returns its width.</summary>
    public static float OutlineBadge(ICanvas c, string text, float x, float y, float px, float padX, float padY, float th, HudColor line, HudColor fg, bool right)
    {
        float bw = c.TextWidth(text, px, Mono) + 2 * padX, bh = px + 2 * padY;
        float bx = right ? x - bw : x;
        Frame(c, new Box(bx, y, bw, bh), th, line);
        c.Text(text, bx + bw / 2, y + padY, px, fg, TextAlign.Center, Mono);
        return bw;
    }

    /// <summary>
    /// A bar of n segments, lit up to `lit`; vertical bars fill from the bottom. The segment at `mark` (if 0 or more) is
    /// drawn in markCol, lit or not.
    /// </summary>
    public static void SegBar(ICanvas c, Box b, int n, int lit, float gap, bool vertical, HudColor on, HudColor off, int mark = -1, HudColor markCol = default)
    {
        if (n <= 0) return;
        float len = vertical ? b.H : b.W, seg = (len - gap * (n - 1)) / n;
        if (seg <= 0.5f) { seg = len / n; gap = 0; }
        for (int i = 0; i < n; i++)
        {
            var col = i == mark ? markCol : i < lit ? on : off;
            if (vertical) c.Rect(b.X, b.B - (i + 1) * seg - i * gap, b.W, seg, col);
            else c.Rect(b.X + i * (seg + gap), b.Y, seg, b.H, col);
        }
    }

    /// <summary>An octagon's outline around (cx, cy), r to the flat sides.</summary>
    public static void Octagon(ICanvas c, float cx, float cy, float r, float th, HudColor col)
    {
        float k = r * 0.64f;
        var p = new[] { (-k, -r), (k, -r), (r, -k), (r, k), (k, r), (-k, r), (-r, k), (-r, -k) };
        for (int i = 0; i < 8; i++)
        {
            var (x1, y1) = p[i]; var (x2, y2) = p[(i + 1) % 8];
            DockCamRenderer.Line(c, cx + x1, cy + y1, cx + x2, cy + y2, th, col);
        }
    }

    /// <summary>A dashed line (screens can't draw dashes, so it's short pieces).</summary>
    public static void Dashed(ICanvas c, float x1, float y1, float x2, float y2, float th, float dash, float gap, HudColor col)
    {
        float dx = x2 - x1, dy = y2 - y1, len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len < 1 || dash <= 0) return;
        float ux = dx / len, uy = dy / len;
        for (float d = 0; d < len; d += dash + gap)
        {
            float e = Math.Min(len, d + dash);
            DockCamRenderer.Line(c, x1 + ux * d, y1 + uy * d, x1 + ux * e, y1 + uy * e, th, col);
        }
    }

    /// <summary>"42.6 M" as a big number and a small unit; returns the width. Right-aligned at x when right is set.</summary>
    public static float ValUnit(ICanvas c, string text, float x, float y, float big, float small, HudColor valCol, HudColor unitCol, bool right)
    {
        int sp = text.LastIndexOf(' ');
        string val = sp > 0 ? text.Substring(0, sp) : text, unit = sp > 0 ? " " + text.Substring(sp + 1) : "";
        float vw = c.TextWidth(val, big, Mono), uw = unit.Length > 0 ? c.TextWidth(unit, small, Mono) : 0;
        float x0 = right ? x - vw - uw : x;
        c.Text(val, x0, y, big, valCol, TextAlign.Left, Mono);
        if (unit.Length > 0) c.Text(unit, x0 + vw, y + (big - small) * 0.78f, small, unitCol, TextAlign.Left, Mono);
        return vw + uw;
    }

    /// <summary>
    /// A card with all four corners cut a little (reads as rounded at screen sizes); border (if its alpha isn't 0) is a
    /// thin line round it, diagonals included.
    /// </summary>
    public static void Card(ICanvas c, Box b, float cut, HudColor fill, HudColor border, float th)
    {
        cut = Math.Min(cut, Math.Min(b.W, b.H) / 2);
        if (cut < 1) { c.Rect(b.X, b.Y, b.W, b.H, fill); if (border.A > 0) Frame(c, b, th, border); return; }
        c.Rect(b.X + cut, b.Y, b.W - 2 * cut, cut, fill);
        c.Rect(b.X, b.Y + cut, b.W, b.H - 2 * cut, fill);
        c.Rect(b.X + cut, b.B - cut, b.W - 2 * cut, cut, fill);
        // the sprite's right angle is bottom-left; each quarter turn moves it a corner clockwise
        c.Shape(HudShape.RightTriangle, b.X, b.Y, cut, cut, fill, (float)(-Math.PI / 2));      // bottom-right filled
        c.Shape(HudShape.RightTriangle, b.R - cut, b.Y, cut, cut, fill, 0);                     // bottom-left filled
        c.Shape(HudShape.RightTriangle, b.X, b.B - cut, cut, cut, fill, (float)Math.PI);        // top-right filled
        c.Shape(HudShape.RightTriangle, b.R - cut, b.B - cut, cut, cut, fill, (float)(Math.PI / 2)); // top-left filled
        if (border.A == 0) return;
        c.Rect(b.X + cut, b.Y, b.W - 2 * cut, th, border);
        c.Rect(b.X + cut, b.B - th, b.W - 2 * cut, th, border);
        c.Rect(b.X, b.Y + cut, th, b.H - 2 * cut, border);
        c.Rect(b.R - th, b.Y + cut, th, b.H - 2 * cut, border);
        float o = th / 2;
        DockCamRenderer.Line(c, b.X + o, b.Y + cut, b.X + cut, b.Y + o, th, border);
        DockCamRenderer.Line(c, b.R - cut, b.Y + o, b.R - o, b.Y + cut, th, border);
        DockCamRenderer.Line(c, b.X + o, b.B - cut, b.X + cut, b.B - o, th, border);
        DockCamRenderer.Line(c, b.R - cut, b.B - o, b.R - o, b.B - cut, th, border);
    }

    /// <summary>A horizontal fade: alpha a0 at the left to a1 at the right, in strips.</summary>
    public static void HFade(ICanvas c, float x, float y, float w, float h, HudColor col, float a0, float a1, int strips = 10)
    {
        if (h <= 0 || w <= 0) return;
        float sw = w / strips;
        for (int i = 0; i < strips; i++)
        {
            float a = a0 + (a1 - a0) * (i + 0.5f) / strips;
            c.Rect(x + i * sw, y, sw + 0.5f, h, col.WithAlpha(a));
        }
    }

    /// <summary>An arc from angle a0 to a1 (radians, 0 = right, clockwise on the screen), th thick, in short pieces.</summary>
    public static void Arc(ICanvas c, float cx, float cy, float r, double a0, double a1, float th, HudColor col)
    {
        double span = a1 - a0;
        if (Math.Abs(span) < 1e-4 || r <= 0) return;
        int n = Math.Max(3, Math.Min(90, (int)(Math.Abs(span) * r / 5)));
        float dl = (float)(Math.Abs(span) * r / n) * 1.12f;
        for (int i = 0; i < n; i++)
        {
            double a = a0 + span * (i + 0.5) / n;
            c.Rect(cx + (float)Math.Cos(a) * r - dl / 2, cy + (float)Math.Sin(a) * r - th / 2, dl, th, col, (float)(a + Math.PI / 2));
        }
    }

    /// <summary>A dark vignette round the edges (fades in from each side).</summary>
    public static void Vignette(ICanvas c, float w, float h, float depth, float alpha)
    {
        float dv = h * depth, dh = w * depth * 0.8f;
        Fade(c, 0, 0, w, dv, HudColor.Black, alpha, 0, 8);
        Fade(c, 0, h - dv, w, dv, HudColor.Black, 0, alpha, 8);
        HFade(c, 0, 0, dh, h, HudColor.Black, alpha, 0, 8);
        HFade(c, w - dh, 0, dh, h, HudColor.Black, 0, alpha, 8);
    }

    /// <summary>How many of n segments the range lights (log scale, 200 m full).</summary>
    public static int RangeLit(DockReading r, int n) => r == null || !r.HasTarget ? 0 : Math.Max(1, (int)Math.Round(n * RangeAt(r.Range)));
}
