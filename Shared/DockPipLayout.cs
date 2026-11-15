using System;
using System.Globalization;

namespace ProtogenDocking;

/// <summary>
/// Quality and performance presets for the whole plugin: how often and how sharp the picture-in-picture dock camera
/// renders, whether it keeps SSAO and bloom, how often the Docking Assist screens redraw and how often docks are looked for.
/// </summary>
public sealed class DockQuality
{
    public string Name, Note;
    /// <summary>PiP pictures a second, the PiP picture's tallest height (px), SSAO + bloom in the PiP.</summary>
    public int PipFps, PipRes;
    public bool PipEffects;
    /// <summary>Docking Assist screens redraw every n-th script run (runs are 6 a second).</summary>
    public int LcdEvery;
    /// <summary>Seconds between looking for docks.</summary>
    public double ScanEvery;
    /// <summary>Live camera picture on Docking Assist LCDs: pictures a second (6 = the LCDs' own rate) and tallest height (px).</summary>
    public int LcdFps, LcdRes;

    public const string Custom = "custom";

    public static readonly DockQuality[] Presets =
    {
        new DockQuality { Name = "performance", PipFps = 15, PipRes = 270, PipEffects = false, LcdEvery = 2, ScanEvery = 3, LcdFps = 3, LcdRes = 180, Note = "PiP 15 fps at up to 270p, plain lighting; screens 3/s, LCD camera 3/s; docks looked for every 3 s" },
        new DockQuality { Name = "balanced", PipFps = 30, PipRes = 450, PipEffects = false, LcdEvery = 1, ScanEvery = 1.5, LcdFps = 10, LcdRes = 270, Note = "PiP 30 fps at up to 450p, plain lighting; screens 6/s, LCD camera 10/s" },
        new DockQuality { Name = "quality", PipFps = 45, PipRes = 720, PipEffects = true, LcdEvery = 1, ScanEvery = 1.5, LcdFps = 20, LcdRes = 360, Note = "PiP 45 fps at up to 720p with SSAO and bloom; screens 6/s, LCD camera 20/s" },
        new DockQuality { Name = "ultra", PipFps = 60, PipRes = 1080, PipEffects = true, LcdEvery = 1, ScanEvery = 1, LcdFps = 30, LcdRes = 512, Note = "PiP 60 fps at up to 1080p with SSAO and bloom; LCD camera 30/s; docks looked for every second" },
    };

    public static DockQuality Find(string name)
    {
        foreach (var p in Presets) if (p.Name.Equals(name ?? "", StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }

    /// <summary>The preset after this one (custom goes to the first).</summary>
    public static DockQuality Next(string name)
    {
        for (int i = 0; i < Presets.Length; i++)
            if (Presets[i].Name.Equals(name ?? "", StringComparison.OrdinalIgnoreCase)) return Presets[(i + 1) % Presets.Length];
        return Presets[0];
    }

    public void ApplyTo(DockSettings s)
    {
        s.Quality = Name;
        s.PipFps = PipFps; s.PipRes = PipRes; s.PipEffects = PipEffects; s.LcdEvery = LcdEvery; s.ScanEvery = ScanEvery;
        s.LcdFps = LcdFps; s.LcdRes = LcdRes;
    }
}

/// <summary>Where the picture-in-picture window sits on screen: a corner, margins from it, a height and a shape.</summary>
public static class DockPipLayout
{
    public static readonly string[] Corners = { "tl", "tr", "br", "bl" };
    public const float MinSize = 10, MaxSize = 80, MinAspect = 0.5f, MaxAspect = 3f;

    public static string CornerName(string c) =>
        c == "tl" ? "top left" : c == "tr" ? "top right" : c == "bl" ? "bottom left" : "bottom right";

    public static string NormCorner(string c)
    {
        c = (c ?? "").Trim().ToLowerInvariant().Replace("-", "").Replace(" ", "");
        switch (c)
        {
            case "tl": case "topleft": case "lt": return "tl";
            case "tr": case "topright": case "rt": return "tr";
            case "bl": case "bottomleft": case "lb": return "bl";
            case "br": case "bottomright": case "rb": return "br";
            default: return null;
        }
    }

    public static string NextCorner(string c)
    {
        int i = Array.IndexOf(Corners, NormCorner(c) ?? "tr");
        return Corners[(i + 1) % Corners.Length];
    }

    /// <summary>The window in pixels on a sw x sh screen (from its top-left), kept on screen.</summary>
    public static (float X, float Y, float W, float H) Rect(DockSettings s, float sw, float sh)
    {
        float h = sh * Clamp(s.PipSize, MinSize, MaxSize) / 100f;
        float w = h * Clamp(s.PipAspect, MinAspect, MaxAspect);
        if (w > sw * 0.98f) { h *= sw * 0.98f / w; w = sw * 0.98f; }
        float mx = sw * Clamp(s.PipX, 0, 100) / 100f, my = sh * Clamp(s.PipY, 0, 100) / 100f;
        string c = NormCorner(s.PipCorner) ?? "tr";
        float x = c[1] == 'l' ? mx : sw - w - mx;
        float y = c[0] == 't' ? my : sh - h - my;
        x = Clamp(x, 0, Math.Max(0, sw - w));
        y = Clamp(y, 0, Math.Max(0, sh - h));
        return (x, y, w, h);
    }

    /// <summary>A window dragged to x, y (its top-left, pixels): anchored to the nearest corner, with margins from it.</summary>
    public static void Place(DockSettings s, float x, float y, float w, float h, float sw, float sh)
    {
        x = Clamp(x, 0, Math.Max(0, sw - w));
        y = Clamp(y, 0, Math.Max(0, sh - h));
        bool left = x + w / 2 < sw / 2, top = y + h / 2 < sh / 2;
        s.PipCorner = (top ? "t" : "b") + (left ? "l" : "r");
        s.PipX = Round1((left ? x : sw - w - x) / sw * 100);
        s.PipY = Round1((top ? y : sh - h - y) / sh * 100);
    }

    /// <summary>"16:9", "4:3", "1:1", "square", "wide" or a number (width / height).</summary>
    public static bool TryAspect(string text, out float aspect)
    {
        aspect = 0;
        string t = (text ?? "").Trim().ToLowerInvariant();
        if (t == "square") { aspect = 1; return true; }
        if (t == "wide") { aspect = 16f / 9; return true; }
        if (t == "tv") { aspect = 4f / 3; return true; }
        int c = t.IndexOfAny(new[] { ':', 'x', '/' });
        if (c > 0 && float.TryParse(t.Substring(0, c), NumberStyles.Float, CultureInfo.InvariantCulture, out float a)
                  && float.TryParse(t.Substring(c + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float b) && a > 0 && b > 0)
            aspect = a / b;
        else if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out aspect)) return false;
        if (aspect <= 0) return false;
        aspect = Clamp(aspect, MinAspect, MaxAspect);
        return true;
    }

    public static string AspectText(float a)
    {
        foreach (var (n, v) in new[] { ("16:9", 16f / 9), ("4:3", 4f / 3), ("1:1", 1f), ("3:2", 1.5f), ("21:9", 21f / 9), ("16:10", 1.6f) })
            if (Math.Abs(a - v) < 0.01f) return n;
        return a.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    private static float Round1(float v) => (float)Math.Round(v, 1);
}
