using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Definitions;
using VRage.Game.GUI.TextPanel;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace ProtogenDocking;

/// <summary>
/// Draws as LCD sprites (MySprite) into a list: the one picture format every screen takes. The LCD script adds them to its
/// frame, the full-screen dock camera draws them on the HUD (HudSprites), and other plugins and mods get them through
/// the API and put them where they like. Text sizes come from the surface when there is one, else from the font itself.
/// </summary>
internal sealed class SpriteListCanvas : ICanvas
{
    private readonly StringBuilder sb = new StringBuilder();
    private static readonly Dictionary<string, float> rawHeights = new Dictionary<string, float>();
    private readonly Dictionary<string, float> surfaceHeights = new Dictionary<string, float>();
    public List<MySprite> List;
    public IMyTextSurface Surface;
    public Vector2 Offset;
    public DockTheme Theme;

    public SpriteListCanvas Begin(List<MySprite> list, Vector2 offset, IMyTextSurface surface = null, DockTheme theme = null)
    {
        List = list; Offset = offset; Theme = theme ?? DockConfig.Theme;
        if (Surface != surface) { Surface = surface; surfaceHeights.Clear(); }
        return this;
    }

    public void Rect(float x, float y, float w, float h, HudColor color, float rotation = 0) => Tex("SquareSimple", x, y, w, h, color, rotation);

    public void Shape(HudShape shape, float x, float y, float w, float h, HudColor color, float rotation = 0) =>
        Tex(shape == HudShape.Circle ? "Circle" : shape == HudShape.Ring ? "CircleHollow" : shape == HudShape.RightTriangle ? "RightTriangle" : "Triangle", x, y, w, h, color, rotation);

    public void Text(string text, float x, float y, float px, HudColor color, TextAlign align, bool mono)
    {
        if (color.A == 0 || string.IsNullOrEmpty(text)) return;
        string font = Font(mono);
        var s = MySprite.CreateText(text, font, C(color), Scale(font, px),
            align == TextAlign.Left ? TextAlignment.LEFT : align == TextAlign.Center ? TextAlignment.CENTER : TextAlignment.RIGHT);
        s.Position = new Vector2(x, y) + Offset;
        List.Add(s);
    }

    public float TextWidth(string text, float px, bool mono)
    {
        string font = Font(mono);
        float scale = Scale(font, px);
        if (Surface != null) { sb.Clear().Append(text); return Surface.MeasureStringInPixels(sb, font, scale).X; }
        return MyFontDefinition.MeasureStringRaw(font, text, scale).X;
    }

    private string Font(bool mono) => mono ? Theme.NumberFont : Theme.Font;

    private void Tex(string id, float x, float y, float w, float h, HudColor color, float rot)
    {
        if (color.A == 0 || w <= 0 || h <= 0) return;
        List.Add(new MySprite(SpriteType.TEXTURE, id, new Vector2(x + w / 2, y + h / 2) + Offset, new Vector2(w, h), C(color), null, TextAlignment.CENTER, rot));
    }

    private float Scale(string font, float px)
    {
        if (Surface != null)
        {
            if (!surfaceHeights.TryGetValue(font, out var lh))
            {
                sb.Clear().Append("Ag");
                lh = Surface.MeasureStringInPixels(sb, font, 1f).Y;
                if (lh <= 0) lh = 30f;
                surfaceHeights[font] = lh;
            }
            return px / lh;
        }
        return px / RawHeight(font);
    }

    public static float RawHeight(string font)
    {
        if (!rawHeights.TryGetValue(font, out var lh))
        {
            try { lh = MyFontDefinition.MeasureStringRaw(font, "Ag", 1f).Y; } catch { lh = 0; }
            if (lh <= 0) lh = 30f;
            rawHeights[font] = lh;
        }
        return lh;
    }

    private static Color C(HudColor c) => new Color(c.R, c.G, c.B, c.A);
}

/// <summary>Draws a list of LCD sprites on the HUD (screen pixels), as made by SpriteListCanvas without a surface.</summary>
internal static class HudSprites
{
    private const string Blank = "Textures\\GUI\\Blank.dds";

    private static readonly Dictionary<string, string> textures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["SquareSimple"] = Blank,
        ["Circle"] = "Textures\\Sprites\\Circle.dds",
        ["CircleHollow"] = "Textures\\Sprites\\Circle_Hollow.dds",
        ["Triangle"] = "Textures\\Sprites\\Triangle.dds",
        ["RightTriangle"] = "Textures\\Sprites\\Right_Triangle.dds",
    };

    public static void Draw(List<MySprite> sprites, Vector2 at)
    {
        foreach (var s in sprites)
        {
            var col = s.Color ?? Color.White;
            var pre = new Color(col.R * col.A / 255, col.G * col.A / 255, col.B * col.A / 255, col.A);
            var pos = (s.Position ?? Vector2.Zero) + at;
            if (s.Type == SpriteType.TEXT)
            {
                string font = s.FontId ?? "White";
                float scale = s.RotationOrScale;
                float x = pos.X;
                if (s.Alignment != TextAlignment.LEFT)
                {
                    float w = MyFontDefinition.MeasureStringRaw(font, s.Data, scale).X;
                    x -= s.Alignment == TextAlignment.CENTER ? w / 2 : w;
                }
                int fid = (int)MyStringHash.GetOrCompute(font);
                var p = new Vector2((float)Math.Round(x), (float)Math.Round(pos.Y));
                if (col.A > 40) MyRenderProxy.DrawString(fid, p + Vector2.One, new Color(0, 0, 0, (int)(col.A * 0.7f)), s.Data, scale, float.PositiveInfinity, true);
                MyRenderProxy.DrawString(fid, p, pre, s.Data, scale, float.PositiveInfinity, true);
            }
            else if (s.Type == SpriteType.TEXTURE)
            {
                if (!textures.TryGetValue(s.Data ?? "", out var tex)) continue;
                var size = s.Size ?? Vector2.One;
                var r = new RectangleF(pos.X - size.X / 2, pos.Y - size.Y / 2, size.X, size.Y);
                MyRenderProxy.DrawSprite(tex, ref r, null, pre, s.RotationOrScale, true, false);
            }
        }
    }
}
