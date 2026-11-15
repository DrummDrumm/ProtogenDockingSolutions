using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Game.GUI.TextPanel;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace ProtogenDocking;

/// <summary>
/// Moves and resizes the PiP window with the mouse: drag it anywhere (it anchors to the nearest corner), mouse wheel =
/// size, Shift + wheel = shape, right click = next corner, R = back to the default. The window shows live while you
/// edit; Save keeps it, Esc or Cancel puts it back.
/// </summary>
public sealed class DockPipEditor : MyGuiScreenBase
{
    private readonly DockSettings edit;
    private bool dragging, resizing;
    private Vector2 grab;
    private readonly SpriteListCanvas canvas = new SpriteListCanvas();
    private readonly List<MySprite> sprites = new List<MySprite>();
    private const float Handle = 18;

    public DockPipEditor() : base(new Vector2(0.5f, 0.5f), null, null, false, null, 0f, 0f)
    {
        EnabledBackgroundFade = false;
        CloseButtonEnabled = false;
        DrawMouseCursor = true;
        m_canShareInput = false;
        edit = DockConfig.Current.Copy();
        DockPip.Preview = edit;
        Closed += (s, unloading) => DockPip.Preview = null;
        float y = 0.44f;
        Button(-0.33f, y, "Save", Save);
        Button(-0.17f, y, "Cancel", () => CloseScreen());
        Button(-0.01f, y, "Reset", () => edit.ResetPipLayout());
        Button(0.15f, y, "Next corner", NextCorner);
    }

    public override string GetFriendlyName() => "DockingSolutionsPipEditor";

    private void Save()
    {
        DockConfig.Change(s => s.CopyPipLayout(edit));
        DockCam.Say("PiP saved: " + DockPipLayout.CornerName(edit.PipCorner) + ", " + edit.PipSize.ToString("0.#") + "% tall, " + DockPipLayout.AspectText(edit.PipAspect) + ".");
        CloseScreen();
    }

    private void NextCorner()
    {
        edit.PipCorner = DockPipLayout.NextCorner(edit.PipCorner);
    }

    public override void HandleInput(bool receivedFocusInThisUpdate)
    {
        var input = MyInput.Static;
        if (input == null) return;
        if (input.IsNewKeyPressed(MyKeys.Escape)) { CloseScreen(); return; }
        if (input.IsNewKeyPressed(MyKeys.Enter)) { Save(); return; }
        var full = MyGuiManager.GetFullscreenRectangle();
        var mouse = MyGuiManager.GetScreenCoordinateFromNormalizedCoordinate(MyGuiManager.MouseCursorPosition);
        var (x, y, w, h) = DockPipLayout.Rect(edit, full.Width, full.Height);
        var rect = new RectangleF(full.Left + x, full.Top + y, w, h);

        if (dragging || resizing)
        {
            if (!input.IsLeftMousePressed()) { dragging = resizing = false; return; }
            if (dragging)
            {
                var at = mouse - grab;
                DockPipLayout.Place(edit, at.X - full.Left, at.Y - full.Top, w, h, full.Width, full.Height);
            }
            else
            {
                // the handle on the corner away from the anchor: height follows the mouse, the shape stays
                string c = DockPipLayout.NormCorner(edit.PipCorner) ?? "tr";
                float newH = c[0] == 't' ? mouse.Y - rect.Y : rect.Bottom - mouse.Y;
                edit.PipSize = DockPipLayout.Clamp(newH / full.Height * 100, DockPipLayout.MinSize, DockPipLayout.MaxSize);
            }
            return;
        }

        bool inTray = MyGuiManager.MouseCursorPosition.Y > 0.9f;
        base.HandleInput(receivedFocusInThisUpdate);
        if (inTray) return;
        if (input.IsNewKeyPressed(MyKeys.R)) { edit.ResetPipLayout(); return; }
        if (input.IsNewRightMousePressed()) { NextCorner(); return; }
        int wheel = input.DeltaMouseScrollWheelValue();
        if (wheel != 0)
        {
            if (input.IsAnyShiftKeyPressed()) edit.PipAspect = DockPipLayout.Clamp((float)Math.Round(edit.PipAspect + Math.Sign(wheel) * 0.05f, 2), DockPipLayout.MinAspect, DockPipLayout.MaxAspect);
            else edit.PipSize = DockPipLayout.Clamp(edit.PipSize + Math.Sign(wheel) * 2, DockPipLayout.MinSize, DockPipLayout.MaxSize);
            return;
        }
        if (input.IsNewLeftMousePressed())
        {
            if (HandleRect(rect).Contains(mouse)) resizing = true;
            else if (rect.Contains(mouse)) { dragging = true; grab = mouse - rect.Position; }
        }
    }

    /// <summary>The resize handle: the window's corner facing the screen's middle.</summary>
    private RectangleF HandleRect(RectangleF r)
    {
        string c = DockPipLayout.NormCorner(edit.PipCorner) ?? "tr";
        float hx = c[1] == 'l' ? r.Right - Handle : r.X;
        float hy = c[0] == 't' ? r.Bottom - Handle : r.Y;
        return new RectangleF(hx, hy, Handle, Handle);
    }

    public override bool Draw()
    {
        try
        {
            var full = MyGuiManager.GetFullscreenRectangle();
            var (x, y, w, h) = DockPipLayout.Rect(edit, full.Width, full.Height);
            var theme = DockConfig.Theme;
            sprites.Clear();
            var c = canvas.Begin(sprites, Vector2.Zero, null, theme);
            // outline and resize handle over the live window
            var r = new RectangleF(x, y, w, h);
            var hr = HandleRect(r);
            var warn = theme.Warn;
            c.Rect(x - 3, y - 3, w + 6, 2, warn); c.Rect(x - 3, y + h + 1, w + 6, 2, warn);
            c.Rect(x - 3, y - 3, 2, h + 6, warn); c.Rect(x + w + 1, y - 3, 2, h + 6, warn);
            c.Rect(hr.X, hr.Y, hr.Width, hr.Height, warn);
            string info = DockPipLayout.CornerName(edit.PipCorner).ToUpperInvariant() + "   " + edit.PipSize.ToString("0.#") + "% TALL   " + DockPipLayout.AspectText(edit.PipAspect);
            c.Rect(full.Width / 2 - 360, 24, 720, 62, new HudColor(0, 0, 0, 170));
            c.Text("DOCK PIP: drag to move, drag the corner square or wheel to size, Shift + wheel = shape, right click = corner, R = reset", full.Width / 2, 32, 17, theme.Text, TextAlign.Center, false);
            c.Text(info + "   -   Enter saves, Esc cancels", full.Width / 2, 58, 17, theme.Main, TextAlign.Center, true);
            HudSprites.Draw(sprites, new Vector2(full.Left, full.Top));
        }
        catch (Exception e) { Plugin.Log.Throttled("PiP editor draw failed: " + e.Message); }
        return base.Draw();
    }

    private void Button(float x, float y, string text, Action click)
    {
        var b = new MyGuiControlButton(new Vector2(x, y), MyGuiControlButtonStyleEnum.Default, new Vector2(0.15f, 0.04f), null,
            MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, null, new StringBuilder(text), 0.7f,
            onButtonClick: _ => { try { click(); } catch (Exception e) { Plugin.Log.Write("PiP editor: " + e); } });
        Controls.Add(b);
    }
}
