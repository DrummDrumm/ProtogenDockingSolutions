using System;
using System.Globalization;

namespace ProtogenDocking;

public enum TextAlign { Left, Center, Right }

public enum HudShape { Circle, Ring, Triangle, RightTriangle }

/// <summary>Where the docking pictures are drawn: pixels, top-left origin, x right, y down.</summary>
public interface ICanvas
{
    /// <summary>Filled rectangle; x,y = top-left; rotation (radians) is around the centre.</summary>
    void Rect(float x, float y, float w, float h, HudColor color, float rotation = 0);
    void Shape(HudShape shape, float x, float y, float w, float h, HudColor color, float rotation = 0);
    /// <summary>Text with its top at y; px = line height in pixels.</summary>
    void Text(string text, float x, float y, float px, HudColor color, TextAlign align, bool mono);
    float TextWidth(string text, float px, bool mono);
}

/// <summary>An RGBA colour that doesn't depend on the game, so the drawing code can be tested off the game.</summary>
public readonly struct HudColor : IEquatable<HudColor>
{
    public readonly byte R, G, B, A;

    public HudColor(int r, int g, int b, int a = 255)
    {
        R = Clamp(r); G = Clamp(g); B = Clamp(b); A = Clamp(a);
    }

    public static readonly HudColor White = new HudColor(255, 255, 255);
    public static readonly HudColor Black = new HudColor(0, 0, 0);

    public HudColor WithAlpha(float alpha) => new HudColor(R, G, B, (int)Math.Round(A * Math.Max(0f, Math.Min(1f, alpha))));

    /// <summary>"r,g,b" or "r,g,b,a" (0-255), or "#rrggbb".</summary>
    public static bool TryParse(string text, out HudColor color)
    {
        color = White;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        if (text.StartsWith("#") && text.Length == 7 && uint.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
        {
            color = new HudColor((int)(v >> 16) & 255, (int)(v >> 8) & 255, (int)v & 255);
            return true;
        }
        var parts = text.Split(',');
        if (parts.Length != 3 && parts.Length != 4) return false;
        var n = new[] { 0, 0, 0, 255 };
        for (int i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i])) return false;
        color = new HudColor(n[0], n[1], n[2], n[3]);
        return true;
    }

    public override string ToString() => A == 255
        ? string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", R, G, B)
        : string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", R, G, B, A);

    public bool Equals(HudColor other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object obj) => obj is HudColor c && Equals(c);
    public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;

    private static byte Clamp(int v) => (byte)Math.Max(0, Math.Min(255, v));
}

/// <summary>A world vector (metres) without the game's types.</summary>
public struct V3
{
    public double X, Y, Z;
    public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
    public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator *(V3 a, double k) => new V3(a.X * k, a.Y * k, a.Z * k);
    public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
}

/// <summary>
/// The colours and fonts the docking pictures use. Default: Protogen's amber. Settable in the settings file
/// (/dock theme), and by another plugin or mod through the API.
/// </summary>
public sealed class DockTheme
{
    public HudColor Main = new HudColor(255, 176, 0);
    public HudColor Dim = new HudColor(120, 84, 20);
    public HudColor Good = new HudColor(120, 220, 90);
    public HudColor Warn = new HudColor(255, 190, 40);
    public HudColor Bad = new HudColor(240, 60, 50);
    public HudColor Text = new HudColor(255, 226, 170);
    public HudColor Background = new HudColor(10, 8, 4);
    public string Font = "White";
    public string NumberFont = "Monospace";

    public static readonly string[] ColorNames = { "Main", "Dim", "Good", "Warn", "Bad", "Text", "Background" };

    public DockTheme Copy() => (DockTheme)MemberwiseClone();

    public HudColor Get(string name) => name switch
    {
        "Main" => Main, "Dim" => Dim, "Good" => Good, "Warn" => Warn, "Bad" => Bad, "Text" => Text, "Background" => Background, _ => Main,
    };

    public bool Set(string name, HudColor c)
    {
        switch (name)
        {
            case "Main": Main = c; return true;
            case "Dim": Dim = c; return true;
            case "Good": Good = c; return true;
            case "Warn": Warn = c; return true;
            case "Bad": Bad = c; return true;
            case "Text": Text = c; return true;
            case "Background": Background = c; return true;
            default: return false;
        }
    }

    /// <summary>A few looks for /dock theme.</summary>
    public static DockTheme Preset(string name)
    {
        switch ((name ?? "").ToLowerInvariant())
        {
            case "amber": return new DockTheme();
            case "cyan":
                return new DockTheme
                {
                    Main = new HudColor(60, 200, 255), Dim = new HudColor(30, 95, 125), Good = new HudColor(60, 220, 140), Warn = new HudColor(250, 190, 50),
                    Bad = new HudColor(240, 70, 60), Text = new HudColor(200, 240, 255), Background = new HudColor(2, 12, 20),
                };
            case "green":
                return new DockTheme
                {
                    Main = new HudColor(90, 255, 120), Dim = new HudColor(40, 120, 55), Good = new HudColor(150, 255, 150), Warn = new HudColor(240, 220, 60),
                    Bad = new HudColor(255, 80, 60), Text = new HudColor(200, 255, 205), Background = new HudColor(2, 14, 4),
                };
            case "white":
                return new DockTheme
                {
                    Main = new HudColor(235, 240, 245), Dim = new HudColor(110, 118, 128), Good = new HudColor(110, 220, 120), Warn = new HudColor(250, 200, 60),
                    Bad = new HudColor(240, 70, 60), Text = new HudColor(245, 248, 250), Background = new HudColor(8, 10, 14),
                };
            default: return null;
        }
    }
}
