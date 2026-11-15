using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using ProtogenDocking;

internal static partial class Program
{
    private static int fails;
    private static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what); if (!ok) fails++; }

    private static int Main()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        if (!Directory.Exists(Path.Combine(root, "Tests"))) root = Directory.GetCurrentDirectory();
        DockTests(root);
        StyleTests(root);
        SettingsTests();
        PipTests();
        DrawTests();
        Console.WriteLine(fails == 0 ? "All passed." : fails + " failed.");
        return fails == 0 ? 0 : 1;
    }

    private static void SettingsTests()
    {
        var d = new DockSettings();
        Check(d.DockTag == "[Dock]" && d.IgnoreTag == "[NoDock]" && d.TargetRange == 300 && d.ThemeSource == "auto", "settings: defaults");

        var s = new DockSettings { DockCamOffsets = "123:1.5:0:-0.25", DockTag = "<dock>", IgnoreTag = "(skip)", TargetRange = 750, ThemeSource = "file" };
        s.Theme = DockTheme.Preset("cyan");
        s.Theme.Font = "Monospace";
        var back = DockSettings.Parse(s.Write());
        Check(back.DockCamOffsets == "123:1.5:0:-0.25" && back.DockTag == "<dock>" && back.IgnoreTag == "(skip)"
              && back.TargetRange == 750 && back.ThemeSource == "file", "settings: written and read back");
        Check(DockTheme.ColorNames.All(n => back.Theme.Get(n).Equals(s.Theme.Get(n))) && back.Theme.Font == "Monospace", "settings: theme colours and font read back");

        // unknown lines are ignored
        var other = DockSettings.Parse("DockCamOffsets=9:2:0:0\nSomethingElse=1");
        Check(other.DockCamOffsets == "9:2:0:0" && other.DockTag == "[Dock]", "settings: unknown lines ignored");

        var clamp = DockSettings.Parse("TargetRange=99999\nThemeMain=#102030\nThemeBad=1,2,3,4");
        Check(clamp.TargetRange == 5000, "settings: range clamped");
        Check(clamp.Theme.Main.Equals(new HudColor(16, 32, 48)) && clamp.Theme.Bad.Equals(new HudColor(1, 2, 3, 4)), "settings: #rrggbb and r,g,b,a colours");

        Check(new[] { "amber", "cyan", "green", "white" }.All(n => DockTheme.Preset(n) != null) && DockTheme.Preset("nope") == null, "theme: presets");
    }

    private static void PipTests()
    {
        var d = new DockSettings();
        Check(d.Quality == "balanced" && !d.PipOn && d.PipCorner == "tr" && d.PipOverlay && d.PipFps == 30, "pip: defaults (off, top right, balanced)");

        // presets set their values; custom keeps what's written
        var q = DockSettings.Parse("Quality=performance\nPipFps=50");
        Check(q.PipFps == 15 && q.PipRes == 270 && q.LcdEvery == 2 && q.ScanEvery == 3, "quality: a named preset sets its values");
        var c = DockSettings.Parse("Quality=custom\nPipFps=50\nPipRes=600\nPipEffects=on\nLcdEvery=3");
        Check(c.Quality == "custom" && c.PipFps == 50 && c.PipRes == 600 && c.PipEffects && c.LcdEvery == 3, "quality: custom keeps the written values");
        var u = DockSettings.Parse("Quality=bogus\nPipFps=500\nPipRes=10");
        Check(u.Quality == "custom" && u.PipFps == 60 && u.PipRes == 180, "quality: unknown name = custom, values clamped");
        Check(DockQuality.Next("ultra").Name == "performance" && DockQuality.Next("custom").Name == "performance" && DockQuality.Presets.Length == 4, "quality: next preset wraps");
        Check(DockQuality.Presets.Zip(DockQuality.Presets.Skip(1), (a, b) => b.PipFps >= a.PipFps && b.PipRes >= a.PipRes).All(x => x), "quality: presets go up in fps and sharpness");
        Check(DockQuality.Presets.Zip(DockQuality.Presets.Skip(1), (a, b) => b.LcdFps >= a.LcdFps && b.LcdRes >= a.LcdRes).All(x => x)
              && d.LcdFeed && d.LcdFps == 10 && d.LcdRes == 270 && q.LcdFps == 3 && q.LcdRes == 180, "lcd camera: on by default, presets go up");
        var lc = DockSettings.Parse("Quality=custom\nLcdFeed=off\nLcdFps=99\nLcdRes=5");
        Check(!lc.LcdFeed && lc.LcdFps == 30 && lc.LcdRes == 120, "lcd camera: off and clamped values read back");
        Check(!DockSettings.Parse(new DockSettings { LcdFeed = false }.Write()).LcdFeed, "lcd camera: setting written and read back");

        // the PiP's mode and auto in third person
        var pd = new DockSettings();
        Check(pd.PipMode == "cam" && pd.PipAuto, "pip mode: dock camera and auto by default");
        var pm = DockSettings.Parse(new DockSettings { PipMode = "assist", PipAuto = false }.Write());
        Check(pm.PipMode == "assist" && !pm.PipAuto, "pip mode: written and read back");
        Check(DockSettings.Parse("PipMode=nonsense").PipMode == "cam" && DockSettings.NormPipMode("Docking") == "assist", "pip mode: unknown falls back, names accepted");
        Check(new DockSettings().CamMode == "cam" && DockSettings.Parse(new DockSettings { CamMode = "assist" }.Write()).CamMode == "assist", "dock cam mode: default and round trip");
        Check(new DockSettings().AssistStyle == "holo" && DockSettings.Parse(new DockSettings { AssistStyle = "classic" }.Write()).AssistStyle == "classic"
              && DockSettings.Parse("AssistStyle=nonsense").AssistStyle == "holo" && DockStyles.Next("radial") == "classic" && DockStyles.Short("classic") == "Clas",
              "assist style: holo by default, round trip, unknown falls back, next");

        // round trip
        var s = new DockSettings { PipOn = true, PipCorner = "bl", PipX = 3, PipY = 7.5f, PipSize = 40, PipAspect = 4f / 3, PipOverlay = false };
        DockQuality.Find("ultra").ApplyTo(s);
        var back = DockSettings.Parse(s.Write());
        Check(back.PipOn && back.PipCorner == "bl" && back.PipX == 3 && back.PipY == 7.5f && back.PipSize == 40 && Math.Abs(back.PipAspect - 4f / 3) < 0.001f && !back.PipOverlay
              && back.Quality == "ultra" && back.PipFps == 60 && back.PipEffects, "pip: settings written and read back");

        // the window on screen
        var l = new DockSettings { PipCorner = "tr", PipX = 2, PipY = 10, PipSize = 25, PipAspect = 16f / 9 };
        var (x, y, w, h) = DockPipLayout.Rect(l, 1920, 1080);
        Check(Math.Abs(h - 270) < 0.5f && Math.Abs(w - 480) < 0.5f && Math.Abs(x - (1920 - 480 - 38.4f)) < 0.5f && Math.Abs(y - 108) < 0.5f, "pip: top right rect");
        l.PipCorner = "bl";
        (x, y, w, h) = DockPipLayout.Rect(l, 1920, 1080);
        Check(Math.Abs(x - 38.4f) < 0.5f && Math.Abs(y - (1080 - 270 - 108)) < 0.5f, "pip: bottom left rect");
        var big = new DockSettings { PipSize = 80, PipAspect = 3, PipX = 50, PipY = 50 };
        (x, y, w, h) = DockPipLayout.Rect(big, 1280, 720);
        Check(x >= 0 && y >= 0 && x + w <= 1280.5f && y + h <= 720.5f, "pip: an oversized window stays on screen");

        // dragging anchors to the nearest corner and keeps the spot
        var drag = new DockSettings { PipSize = 25, PipAspect = 16f / 9 };
        DockPipLayout.Place(drag, 100, 700, 480, 270, 1920, 1080);
        var (dx, dy, _, _) = DockPipLayout.Rect(drag, 1920, 1080);
        Check(drag.PipCorner == "bl" && Math.Abs(dx - 100) < 2 && Math.Abs(dy - 700) < 2, "pip: dragged window anchors bottom left, same spot");
        DockPipLayout.Place(drag, 1300, 50, 480, 270, 1920, 1080);
        (dx, dy, _, _) = DockPipLayout.Rect(drag, 1920, 1080);
        Check(drag.PipCorner == "tr" && Math.Abs(dx - 1300) < 2 && Math.Abs(dy - 50) < 2, "pip: dragged window anchors top right, same spot");

        Check(DockPipLayout.TryAspect("16:9", out var a1) && Math.Abs(a1 - 16f / 9) < 0.001f && DockPipLayout.TryAspect("square", out var a2) && a2 == 1
              && DockPipLayout.TryAspect("10", out var a3) && a3 == 3 && !DockPipLayout.TryAspect("x", out _), "pip: aspect parsing");
        Check(DockPipLayout.NormCorner("Top-Left") == "tl" && DockPipLayout.NextCorner("br") == "bl" && DockPipLayout.NormCorner("middle") == null, "pip: corners");
        var r = new DockSettings { PipCorner = "bl", PipSize = 60 };
        r.ResetPipLayout();
        Check(r.PipCorner == "tr" && r.PipSize == 28, "pip: reset layout");
    }

    /// <summary>Both pictures draw something, and the view box stays inside the screen it's given.</summary>
    private static void DrawTests()
    {
        var me = new DockFrame { Pos = new V3(0, 0, 0), Fwd = new V3(0, 0, 1), Up = new V3(0, 1, 0), Right = new V3(1, 0, 0) };
        var r = DockGuide.Read(me, new V3(2, -1, 40), new V3(0, 0, -1), new V3(0.5, 0, 3));
        foreach (var (w, h) in new[] { (512f, 512f), (512f, 248f), (1024f, 200f) })
        {
            var rec = new RecordingCanvas();
            var s = new DockScreen { Time = 1, Synthetic = true, Title = "DOCKING ASSIST" };
            float k = Math.Max(0.35f, Math.Min(h / 360f, w / 500f));
            s.Scale = k; s.Compact = w / k < 1100;
            DockAssistRenderer.Layout(s, w, h);
            DockAssistRenderer.Draw(rec, new DockTheme(), r, s, w, h);
            Check(rec.Count > 20 && rec.Texts.Any(t => t.Contains("DOCKING ASSIST")), "draw: Docking Assist on " + w + "x" + h + " (" + rec.Count + " items)");
            Check(s.ViewW > 0 && s.ViewH > 0 && s.ViewX >= 0 && s.ViewY >= 0 && s.ViewX + s.ViewW <= w + 0.5f && s.ViewY + s.ViewH <= h + 0.5f, "draw: view box inside the screen on " + w + "x" + h);
        }
        var rc = new RecordingCanvas();
        DockCamRenderer.Draw(rc, new DockTheme(), r, new DockScreen { Time = 1, Target = (300, 200), Scale = 1, Compact = true }, 640, 400);
        Check(rc.Count > 10, "draw: dock camera overlay (" + rc.Count + " items)");
        var none = new RecordingCanvas();
        DockAssistRenderer.Draw(none, new DockTheme(), new DockReading { HasTarget = false }, new DockScreen { Scale = 1 }, 512, 512);
        Check(none.Texts.Any(t => t.IndexOf("NO DOCK", StringComparison.OrdinalIgnoreCase) >= 0), "draw: no dock in range says so");
    }
}

internal sealed class RecordingCanvas : ICanvas
{
    public int Count;
    public readonly List<string> Texts = new List<string>();
    public void Rect(float x, float y, float w, float h, HudColor color, float rotation = 0) => Count++;
    public void Shape(HudShape shape, float x, float y, float w, float h, HudColor color, float rotation = 0) => Count++;
    public void Text(string text, float x, float y, float px, HudColor color, TextAlign align, bool mono) { Count++; Texts.Add(text ?? ""); }
    public float TextWidth(string text, float px, bool mono) => (text ?? "").Length * px * 0.55f;
}

/// <summary>Draws the pictures with GDI+ for the preview images.</summary>
internal sealed class GdiCanvas : ICanvas
{
    public Graphics G;
    public DockTheme Theme;

    public static void Setup(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    private static Color C(HudColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    public void Rect(float x, float y, float w, float h, HudColor color, float rotation = 0)
    {
        using var b = new SolidBrush(C(color));
        var st = G.Save();
        G.TranslateTransform(x + w / 2, y + h / 2);
        G.RotateTransform(rotation * 180f / (float)Math.PI);
        G.FillRectangle(b, -w / 2, -h / 2, w, h);
        G.Restore(st);
    }

    public void Shape(HudShape shape, float x, float y, float w, float h, HudColor color, float rotation = 0)
    {
        var st = G.Save();
        G.TranslateTransform(x + w / 2, y + h / 2);
        G.RotateTransform(rotation * 180f / (float)Math.PI);
        using (var b = new SolidBrush(C(color)))
        {
            switch (shape)
            {
                case HudShape.Circle: G.FillEllipse(b, -w / 2, -h / 2, w, h); break;
                case HudShape.Ring: using (var p = new Pen(C(color), Math.Max(1, w / 16))) G.DrawEllipse(p, -w / 2, -h / 2, w, h); break;
                case HudShape.Triangle: G.FillPolygon(b, new[] { new PointF(0, -h / 2), new PointF(w / 2, h / 2), new PointF(-w / 2, h / 2) }); break;
                default: G.FillPolygon(b, new[] { new PointF(-w / 2, -h / 2), new PointF(-w / 2, h / 2), new PointF(w / 2, h / 2) }); break;
            }
        }
        G.Restore(st);
    }

    private static Font F(float px, bool mono) => new Font(mono ? "Consolas" : "Segoe UI", Math.Max(1, px * 0.72f), GraphicsUnit.Pixel);

    public void Text(string text, float x, float y, float px, HudColor color, TextAlign align, bool mono)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var f = F(px, mono);
        using var b = new SolidBrush(C(color));
        float w = G.MeasureString(text, f).Width;
        G.DrawString(text, f, b, align == TextAlign.Left ? x : align == TextAlign.Center ? x - w / 2 : x - w, y);
    }

    public float TextWidth(string text, float px, bool mono)
    {
        using var f = F(px, mono);
        return G.MeasureString(text ?? "", f).Width;
    }
}
