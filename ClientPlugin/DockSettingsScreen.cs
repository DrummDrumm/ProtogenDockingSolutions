using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Sandbox;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace ProtogenDocking;

/// <summary>
/// The settings window: Pulsar's Config button for the plugin, or Ctrl+Alt+D in game. Changes apply at once and are
/// written to DockingSolutions.ini when it closes. The dock camera's spot per connector stays on /dock offset, and
/// the theme's single colours and fonts stay in the file.
/// </summary>
internal sealed class DockSettingsScreen : MyGuiScreenBase
{
    private const float RowH = 0.044f, Top = 0.03f;
    /// <summary>The window's background never goes more see-through than this, whatever the game's UI opacity is.</summary>
    private const float MinBackground = 0.95f;
    /// <summary>The dark shade behind the text, over the window's own background.</summary>
    private static readonly Vector4 Shade = new Vector4(0.02f, 0.03f, 0.04f, 0.82f);
    private const float ButtonW = 0.15f, ButtonH = 0.042f;
    private static DockSettingsScreen open;
    private float y, labelX, controlX, controlW;
    /// <summary>The Help tab is showing (kept while the game runs, so it opens where you left it).</summary>
    private static bool helpTab;

    public static bool IsOpen => open != null;

    public static void Open()
    {
        if (open != null) return;
        try
        {
            open = new DockSettingsScreen();
            open.Closed += (s, unloading) => { open = null; DockConfig.Flush(); };
            MyGuiSandbox.AddScreen(open);
        }
        catch (Exception e) { open = null; Plugin.Log.Write("can't open the settings window: " + e.Message); }
    }

    /// <summary>Ctrl+Alt+D: every frame from Plugin.Update. (Ctrl+Alt+/ is the SDX2 System Map's.)</summary>
    public static void CheckHotkey()
    {
        var input = MyInput.Static;
        if (input == null || open != null || !input.IsNewKeyPressed(MyKeys.D)) return;
        if (!input.IsAnyCtrlKeyPressed() || !input.IsAnyAltKeyPressed()) return;
        if (MyAPIGateway.Gui?.ChatEntryVisible ?? false) return;
        Open();
    }

    private DockSettingsScreen() : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.94f, 0.94f), false, null,
        Math.Max(MySandboxGame.Config.UIBkOpacity, MinBackground), Math.Max(MySandboxGame.Config.UIOpacity, MinBackground))
    {
        EnabledBackgroundFade = true;
        CloseButtonEnabled = true;
        RecreateControls(true);
    }

    public override string GetFriendlyName() => "ProtogenDockingSettings";

    private static void Set(Action<DockSettings> change) => DockConfig.Change(change, save: false);

    public override void RecreateControls(bool constructor)
    {
        base.RecreateControls(constructor);
        capturing = null;
        // a dark shade behind everything, so the text reads over a busy scene
        Controls.Add(new MyGuiControlPanel(Vector2.Zero, m_size.Value - new Vector2(0.012f, 0.012f), Shade, MyGuiConstants.BLANK_TEXTURE));
        var s = DockConfig.Current;
        var caption = AddCaption("Protogen Systems Docking Solutions settings");
        float top = caption.Position.Y + caption.Size.Y / 2 + Top;
        Tab("Settings", -0.44f, caption.Position.Y, !helpTab, false);
        Tab("Help", -0.335f, caption.Position.Y, helpTab, true);
        if (helpTab) { Help(top); return; }

        // ---- left column
        Column(-0.44f, -0.18f, 0.2f, top);
        Section("DOCK CAMERA");
        var modes = new[] { "cam", "assist" };
        Combo("Full screen shows", "The full-screen dock camera: the camera view with its guidance, or the Docking Assist with the live picture (/dock mode).",
            new[] { "Camera view", "Docking Assist" }, Math.Max(0, Array.IndexOf(modes, s.CamMode)), i => Set(x => x.CamMode = modes[i]));
        var titles = DockStyles.Names.Select(DockStyles.Title).ToList();
        Combo("Assist style", "The Docking Assist's look everywhere (/dock style).", titles,
            Math.Max(0, Array.IndexOf(DockStyles.Names, DockStyles.Norm(s.AssistStyle) ?? DockStyles.Default)), i => Set(x => x.AssistStyle = DockStyles.Names[i]));
        PlaceStyle("Full-screen style", "A look for the full-screen dock camera only.", s.FullStyle, v => Set(x => x.FullStyle = v));
        PlaceStyle("PiP style", "A look for the picture-in-picture window only.", s.PipStyle, v => Set(x => x.PipStyle = v));
        PlaceStyle("LCD style", "A look for Docking Assist LCDs only (an LCD's Custom Data line DockStyle=<style> beats it).", s.LcdStyle, v => Set(x => x.LcdStyle = v));

        Section("FLYING WHILE DOCKING");
        // Steer from the camera: off, by hand for one view or both (switches off when docking ends), or Auto
        int steer = s.SteerAuto ? (s.SteerAutoPip ? 4 : 5) : s.SteerFull && s.SteerPip ? 3 : s.SteerPip ? 2 : s.SteerFull ? 1 : 0;
        Combo("Steer from the camera", "While docking, W flies the way the dock camera looks and the mouse turns the ship about its axes.\n" +
            "Full screen / PiP / Both: switched on here, with the key or the toolbar, and off by itself when docking ends (connected, view off, or out of range).\n" +
            "Auto: on by itself as soon as a dock is in range with the dock camera or PiP up, off when docking ends. Your controls change without you pressing anything.\n" +
            "Auto, full screen only: the same, but only in the full-screen dock camera; the PiP never steers (/dock steer auto | autofull | manual).",
            new[] { "Off", "Full screen (this dock)", "PiP (this dock)", "Both (this dock)", "Auto while docking", "Auto, full screen only" }, steer, i =>
            {
                Set(x => { x.SteerAuto = i >= 4; if (i >= 4) x.SteerAutoPip = i == 4; x.SteerFull = i == 1 || i == 3; x.SteerPip = i == 2 || i == 3; });
                if ((i >= 4) != (steer >= 4)) RecreateControls(false); // the warning line comes and goes
            });
        if (s.SteerAuto)
        {
            Controls.Add(new MyGuiControlLabel(new Vector2(labelX + 0.01f, y - 0.012f),
                text: "! Warning: your controls change by themselves as soon as a dock is in range.", textScale: 0.65f,
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER) { ColorMask = new Vector4(1f, 0.55f, 0.3f, 1) });
            y += RowH * 0.45f;
        }
        KeyRow("Steer on / off key", "A key that switches steering from the camera for the view that's up (full screen, else the PiP; neither: both); with Auto, off and back on for this dock. " +
            "Click, then press the key with any Ctrl / Alt / Shift. Backspace or Delete = no key, Esc = keep it. The key still does its normal job in the game too, so pick a free one.");
        // all the way left = off; otherwise the speed limit
        // square-root scales, so the small values used for docking are easy to set and the large ones are still there
        Slider("Dampener speed limit", s.Damp ? ToPos(s.DampSpeed, DockSettings.DampSpeedMax) : 0, 0, 1,
            v => { float m = SpeedAt(v); Set(x => { x.Damp = m >= DockSettings.DampSpeedMin; if (x.Damp) x.DampSpeed = m; }); },
            v => { float m = SpeedAt(v); return m < DockSettings.DampSpeedMin ? "Off" : m.ToString("0.#", CultureInfo.InvariantCulture) + " m/s"; });
        Slider("Dampener acceleration", ToPos(s.DampAccel, DockSettings.DampAccelMax), 0, 1, v => Set(x => x.DampAccel = AccelAt(v)),
            v => AccelAt(v).ToString(AccelAt(v) < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + " m/s\u00b2");
        Check("Dampener auto (slower near the dock)", "The dampener's limits come down as the dock gets closer, to the safe speed the Docking Assist advises " +
            "(10 m/s at 100 m, about 2 m/s at 20 m, 0.5 m/s in the last metres), so you stay clear of its red SLOW DOWN. Never faster than the two limits above (/dock damp auto).",
            s.DampAuto, v => Set(x => { x.DampAuto = v; if (v) x.Damp = true; }));
        // after undocking: off for a while, or until out of range (the last notch), so it only works on the way in
        Slider("Dampener pause after undocking", s.DampUndock < 0 ? DockSettings.DampUndockMax + 5 : s.DampUndock, 0, DockSettings.DampUndockMax + 5,
            v => Set(x => x.DampUndock = v > DockSettings.DampUndockMax + 2.5f ? -1 : (float)Math.Round(v)),
            v => v > DockSettings.DampUndockMax + 2.5f ? "Until out of range" : Math.Round(v) < 0.5 ? "No pause" : Math.Round(v).ToString("0", CultureInfo.InvariantCulture) + " s");

        Section("QUALITY");
        var presets = DockQuality.Presets.Select(p => p.Name).ToList();
        int q = presets.IndexOf(s.Quality);
        var presetNames = DockQuality.Presets.Select(p => Cap(p.Name)).ToList();
        if (q < 0) { presetNames.Add("Custom"); q = presetNames.Count - 1; }
        Combo("Preset", string.Join("\n", DockQuality.Presets.Select(p => Cap(p.Name) + ": " + p.Note)), presetNames, q, i =>
        {
            if (i < DockQuality.Presets.Length) Set(DockQuality.Presets[i].ApplyTo);
        });
        Check("Live camera on LCDs", "Docking Assist LCDs show the live dock camera under the assist (off = the drawn view only, no rendering cost).",
            s.LcdFeed, v => Set(x => x.LcdFeed = v));

        // ---- right column
        Column(0.03f, 0.25f, 0.17f, top);
        Section("PICTURE-IN-PICTURE");
        Check("Show the PiP", "A small dock camera window while you fly (/dock pip).", s.PipOn, v => DockPip.Set(v));
        Combo("PiP shows", "The dock camera with its guidance, or the Docking Assist with the live picture in its view box.",
            new[] { "Dock camera", "Docking Assist" }, Math.Max(0, Array.IndexOf(modes, s.PipMode)), i => Set(x => x.PipMode = modes[i]));
        Check("Guidance on top", "The crosshair, brackets and readouts over the PiP picture.", s.PipOverlay, v => Set(x => x.PipOverlay = v));
        Check("Up by itself in 3rd person", "The PiP comes up while the full-screen dock camera is paused in third person.", s.PipAuto, v => Set(x => x.PipAuto = v));
        // all the way left = off; otherwise the range it pops up at
        Slider("Up by itself near a dock", s.PipNear ? Math.Min(DockSettings.NearMax, s.PipNearRange) : 0, 0, DockSettings.NearMax,
            v => { float m = (float)Math.Round(v / 5) * 5; Set(x => { x.PipNear = m >= DockSettings.NearMin; if (m >= DockSettings.NearMin) x.PipNearRange = m; }); },
            v => { float m = (float)Math.Round(v / 5) * 5; return m < DockSettings.NearMin ? "Off" : m.ToString("0", CultureInfo.InvariantCulture) + " m"; });
        // after docking: the dock camera and PiP off after this many seconds, or never (the last notch); LCD feeds stay
        Slider("Views off after docking", s.DockedOff < 0 ? DockSettings.DockedOffMax + 1 : s.DockedOff, 0, DockSettings.DockedOffMax + 1,
            v => Set(x => x.DockedOff = v > DockSettings.DockedOffMax + 0.5f ? -1 : (float)Math.Round(v)),
            v => v > DockSettings.DockedOffMax + 0.5f ? "Never (keep up)" : Math.Round(v) < 0.5 ? "Straight away" : Math.Round(v).ToString("0", CultureInfo.InvariantCulture) + " s");
        var corners = DockPipLayout.Corners;
        Combo("Corner", null, corners.Select(c => Cap(DockPipLayout.CornerName(c))).ToList(), Math.Max(0, Array.IndexOf(corners, s.PipCorner)),
            i => Set(x => x.PipCorner = corners[i]));
        Slider("Size", s.PipSize, DockPipLayout.MinSize, DockPipLayout.MaxSize, v => Set(x => x.PipSize = (float)Math.Round(v)),
            v => Math.Round(v).ToString("0", CultureInfo.InvariantCulture) + "%");
        var aspects = new List<(string, float)> { ("16:9", 16f / 9), ("4:3", 4f / 3), ("1:1", 1f), ("3:2", 1.5f), ("21:9", 21f / 9), ("16:10", 1.6f) };
        int a = aspects.FindIndex(t => Math.Abs(t.Item2 - s.PipAspect) < 0.01f);
        if (a < 0) { aspects.Add((DockPipLayout.AspectText(s.PipAspect), s.PipAspect)); a = aspects.Count - 1; }
        Combo("Shape", "Width : height.", aspects.Select(t => t.Item1).ToList(), a, i => Set(x => x.PipAspect = aspects[i].Item2));
        if (MySession.Static != null)
            Button("Move / resize with the mouse", "Opens the PiP editor over the game (/dock pip mouse).", () => { CloseScreen(); DockPip.Set(true); DockPip.Edit(); });

        Section("CONNECTORS");
        Text("Dock tag", "A connector with this in its name is picked first as yours.", s.DockTag, v => Set(x => x.DockTag = v));
        Text("Ignore tag", "A connector with this in its name is never used.", s.IgnoreTag, v => Set(x => x.IgnoreTag = v));
        Slider("Look for docks within", (float)s.TargetRange, 20, 5000, v => Set(x => x.TargetRange = RoundRange(v)),
            v => RoundRange(v).ToString("0", CultureInfo.InvariantCulture) + " m");

        Section("COLOURS");
        var themeKeys = new[] { "auto", "file", "amber", "cyan", "green", "white" };
        var themeNames = new[] { "Auto (from a plugin)", "From the file", "Amber", "Cyan", "Green", "White" };
        Combo("Colours", "auto follows a theme another plugin hands over; a preset replaces the file's colours (/dock theme).",
            themeNames, ThemeIndex(s, themeKeys), i => SetTheme(themeKeys[i]));

        // ---- bottom
        float bottom = m_size.Value.Y / 2 - 0.035f;
        string shipName = DockConfig.ShipName;
        var scope = new List<string> { "All ships (global)" };
        if (shipName != null) scope.Add("This ship: " + (shipName.Length > 28 ? shipName.Substring(0, 27) + "\u2026" : shipName));
        var who = new MyGuiControlCombobox(new Vector2(-0.44f, bottom), new Vector2(0.3f, ButtonH),
            toolTip: "Which settings this window changes. This ship: its own settings, used whenever you sit in a ship with this name (the dock camera's spots stay shared). " +
                     "All ships: the global settings; a ship put back on global keeps its own for later (/dock ship save | global).",
            originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM);
        for (int i = 0; i < scope.Count; i++) who.AddItem(i, scope[i], sort: false);
        who.SelectItemByKey(DockConfig.PerShip ? 1 : 0, sendEvent: false);
        who.ItemSelected += () => Guard(() =>
        {
            bool perShip = who.GetSelectedKey() == 1;
            if (perShip == DockConfig.PerShip) return;
            if (perShip) DockConfig.UseShip(); else DockConfig.UseGlobal();
            RecreateControls(false);
        });
        Controls.Add(new MyGuiControlLabel(new Vector2(-0.44f, bottom - ButtonH - 0.008f), text: "Settings for", textScale: 0.7f,
            originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM) { ColorMask = new Vector4(1f, 0.75f, 0.35f, 1) });
        Controls.Add(who);
        // Defaults and Close at the bottom right, clear of the Settings for box
        BottomButton("Close", 0.44f, bottom, null, () => CloseScreen());
        BottomButton("Defaults", 0.44f - ButtonW - 0.01f, bottom, "Every setting back to its default (the dock camera's spot per connector is kept).", ResetDefaults);
        Controls.Add(new MyGuiControlLabel(new Vector2(0.44f, bottom - ButtonH - 0.012f), text: "Ctrl+Alt+D opens this window in game. Changes apply at once.",
            textScale: 0.7f, originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM) { ColorMask = new Vector4(0.7f, 0.7f, 0.7f, 1) });
    }

    /// <summary>A key row waiting for a key press (null: none).</summary>
    private MyGuiControlButton capturing;

    private void KeyRow(string text, string tip)
    {
        Label(text, tip);
        string now = DockConfig.Current.SteerKey;
        MyGuiControlButton b = null;
        b = new MyGuiControlButton(new Vector2(controlX, y), text: new StringBuilder(string.IsNullOrEmpty(now) ? "None" : now), toolTip: tip,
            originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, onButtonClick: _ => Guard(() =>
            {
                capturing = b;
                b.Text = "Press a key...";
            }))
            { VisualStyle = MyGuiControlButtonStyleEnum.Rectangular, Size = new Vector2(controlW, 0.036f) };
        Controls.Add(b);
        y += RowH;
    }

    public override void HandleInput(bool receivedFocusInThisUpdate)
    {
        if (capturing != null)
        {
            var input = MyInput.Static;
            if (input.IsNewKeyPressed(MyKeys.Escape))
            {
                capturing.Text = string.IsNullOrEmpty(DockConfig.Current.SteerKey) ? "None" : DockConfig.Current.SteerKey;
                capturing = null;
                return; // Esc keeps the key and doesn't close the window
            }
            string key = input.IsNewKeyPressed(MyKeys.Back) || input.IsNewKeyPressed(MyKeys.Delete) ? "" : DockHotkey.Pressed();
            if (key != null)
            {
                Set(x => x.SteerKey = key);
                capturing.Text = key.Length == 0 ? "None" : key;
                capturing = null;
            }
            return;
        }
        base.HandleInput(receivedFocusInThisUpdate);
    }

    private void Tab(string text, float x, float y, bool selected, bool help)
    {
        var b = new MyGuiControlButton(new Vector2(x, y), text: new StringBuilder(text), toolTip: help ? "Brief instructions and explanations." : "The settings.",
            originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, onButtonClick: _ => Guard(() =>
            {
                if (helpTab == help) return;
                helpTab = help;
                RecreateControls(false);
            }))
            { VisualStyle = MyGuiControlButtonStyleEnum.Rectangular, Size = new Vector2(0.1f, 0.036f) };
        if (selected) b.ColorMask = new Vector4(1f, 0.75f, 0.35f, 1);
        Controls.Add(b);
    }

    /// <summary>The Help tab: the topics in a scrolling box, and Close.</summary>
    private void Help(float top)
    {
        float bottom = m_size.Value.Y / 2 - 0.035f;
        float h = bottom - ButtonH - 0.02f - top;
        var box = new MyGuiControlMultilineText(new Vector2(-0.44f, top), new Vector2(0.88f, h), textScale: 0.75f,
            textAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, drawScrollbarV: true, drawScrollbarH: false,
            textBoxAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
        box.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
        var heading = new Vector4(1f, 0.75f, 0.35f, 1);
        var body = new Vector4(0.9f, 0.9f, 0.9f, 1);
        bool first = true;
        foreach (var (title, text) in DockHelp.Topics)
        {
            if (!first) box.AppendLine();
            first = false;
            box.AppendText(title, "White", 0.8f, heading);
            box.AppendLine();
            foreach (var line in text.Split('\n'))
            {
                box.AppendText(line, "Blue", 0.72f, body);
                box.AppendLine();
            }
        }
        Controls.Add(box);
        BottomButton("Close", 0.44f, bottom, null, () => CloseScreen());
    }

    /// <summary>A bottom-row button, right edge at x.</summary>
    private void BottomButton(string text, float x, float bottom, string tip, Action click)
    {
        Controls.Add(new MyGuiControlButton(new Vector2(x, bottom), text: new StringBuilder(text), toolTip: tip,
            originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM, onButtonClick: b => Guard(click))
            { VisualStyle = MyGuiControlButtonStyleEnum.Rectangular, Size = new Vector2(ButtonW, ButtonH) });
    }

    private void ResetDefaults()
    {
        bool pipWasOn = DockConfig.Current.PipOn;
        DockConfig.ResetDefaults(save: false);
        if (pipWasOn) DockPip.Set(false);
        RecreateControls(false);
    }

    private static float ToPos(float value, float max) => (float)Math.Sqrt(Math.Max(0, value) / max);

    /// <summary>Speed limit at a slider position: 0.5 m/s steps up to 20, 1 up to 50, 5 above.</summary>
    private static float SpeedAt(float pos)
    {
        float v = pos * pos * DockSettings.DampSpeedMax;
        float step = v < 20 ? 0.5f : v < 50 ? 1 : 5;
        return (float)Math.Round(v / step) * step;
    }

    /// <summary>Acceleration limit at a slider position: 0.1 m/s2 steps up to 5, 0.5 up to 20, 1 above.</summary>
    private static float AccelAt(float pos)
    {
        float v = pos * pos * DockSettings.DampAccelMax;
        float step = v < 5 ? 0.1f : v < 20 ? 0.5f : 1;
        return Math.Max(DockSettings.DampAccelMin, (float)Math.Round(v / step) * step);
    }

    private static double RoundRange(float v) => v < 500 ? Math.Round(v / 10) * 10 : Math.Round(v / 50) * 50;

    private static int ThemeIndex(DockSettings s, string[] keys)
    {
        if (s.ThemeSource == "auto") return 0;
        for (int i = 2; i < keys.Length; i++)
        {
            var p = DockTheme.Preset(keys[i]);
            if (p != null && DockTheme.ColorNames.All(n => p.Get(n).Equals(s.Theme.Get(n)))) return i;
        }
        return 1;
    }

    private static void SetTheme(string key)
    {
        if (key == "auto") { Set(x => x.ThemeSource = "auto"); return; }
        if (key == "file") { Set(x => x.ThemeSource = "file"); return; }
        var p = DockTheme.Preset(key);
        if (p != null) Set(x => { var t = p.Copy(); t.Font = x.Theme.Font; t.NumberFont = x.Theme.NumberFont; x.Theme = t; x.ThemeSource = "file"; });
    }

    // ---- rows

    private void Column(float label, float control, float width, float top)
    {
        labelX = label; controlX = control; controlW = width; y = top;
    }

    private static void Guard(Action a)
    {
        try { a(); } catch (Exception e) { Plugin.Log.Throttled("setting failed: " + e.Message); }
    }

    private void Section(string title)
    {
        y += 0.01f;
        Controls.Add(new MyGuiControlLabel(new Vector2(labelX, y), text: title, textScale: 0.75f, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER)
            { ColorMask = new Vector4(1f, 0.75f, 0.35f, 1) });
        var sep = new MyGuiControlSeparatorList();
        sep.AddHorizontal(new Vector2(labelX, y + 0.018f), controlX + controlW - labelX);
        Controls.Add(sep);
        y += RowH * 0.8f;
    }

    private void Label(string text, string tip)
    {
        var l = new MyGuiControlLabel(new Vector2(labelX + 0.01f, y), text: text, textScale: 0.75f, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
        if (tip != null) l.SetToolTip(tip);
        Controls.Add(l);
    }

    private void Check(string text, string tip, bool value, Action<bool> set)
    {
        Label(text, tip);
        var c = new MyGuiControlCheckbox(new Vector2(controlX, y), toolTip: tip, isChecked: value, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
        c.IsCheckedChanged += cb => Guard(() => set(cb.IsChecked));
        Controls.Add(c);
        y += RowH;
    }

    private void Slider(string text, float value, float min, float max, Action<float> set, Func<float, string> show)
    {
        Label(text, null);
        float w = controlW - 0.05f;
        var sl = new MyGuiControlSlider(new Vector2(controlX, y), min, max, w, value, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
        sl.Value = value;
        var v = new MyGuiControlLabel(new Vector2(controlX + w + 0.008f, y), text: show(value), textScale: 0.75f, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
        sl.ValueChanged += x => { v.Text = show(x.Value); Guard(() => set(x.Value)); };
        Controls.Add(sl); Controls.Add(v);
        y += RowH;
    }

    private void Combo(string text, string tip, IList<string> items, int selected, Action<int> set)
    {
        Label(text, tip);
        var c = new MyGuiControlCombobox(new Vector2(controlX, y), new Vector2(controlW, 0.04f), toolTip: tip, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
        for (int i = 0; i < items.Count; i++) c.AddItem(i, items[i], sort: false);
        c.SelectItemByKey(selected, sendEvent: false);
        c.ItemSelected += () => Guard(() => set((int)c.GetSelectedKey()));
        Controls.Add(c);
        y += RowH;
    }

    /// <summary>A style for one place: "Same as main", then the styles.</summary>
    private void PlaceStyle(string text, string tip, string value, Action<string> set)
    {
        var items = new List<string> { "Same as main" };
        items.AddRange(DockStyles.Names.Select(DockStyles.Title));
        string n = DockStyles.Norm(value);
        Combo(text, tip, items, n == null ? 0 : Array.IndexOf(DockStyles.Names, n) + 1, i => set(i == 0 ? "" : DockStyles.Names[i - 1]));
    }

    private void Text(string text, string tip, string value, Action<string> set)
    {
        Label(text, tip);
        var t = new MyGuiControlTextbox(new Vector2(controlX, y), value ?? "", maxLength: 64, textScale: 0.75f)
            { OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, Size = new Vector2(controlW, 0.04f) };
        if (tip != null) t.SetToolTip(tip);
        t.TextChanged += box => Guard(() => set(box.Text.Trim()));
        Controls.Add(t);
        y += RowH;
    }

    private void Button(string text, string tip, Action click)
    {
        Controls.Add(new MyGuiControlButton(new Vector2(labelX + 0.01f, y), text: new StringBuilder(text), toolTip: tip,
            originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, onButtonClick: b => Guard(click))
            { VisualStyle = MyGuiControlButtonStyleEnum.Rectangular, Size = new Vector2(controlX + controlW - labelX - 0.01f, 0.04f) });
        y += RowH;
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
