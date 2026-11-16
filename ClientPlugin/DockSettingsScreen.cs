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
    private static DockSettingsScreen open;
    private float y, labelX, controlX, controlW;

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

    private DockSettingsScreen() : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.94f, 0.86f), false, null,
        MySandboxGame.Config.UIBkOpacity, MySandboxGame.Config.UIOpacity)
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
        var s = DockConfig.Current;
        var caption = AddCaption("Protogen Systems Docking Solutions settings");
        float top = caption.Position.Y + caption.Size.Y / 2 + Top;

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

        Section("COLOURS");
        var themeKeys = new[] { "auto", "file", "amber", "cyan", "green", "white" };
        var themeNames = new[] { "Another plugin's, else the file's", "The settings file's", "Amber", "Cyan", "Green", "White" };
        Combo("Colours", "auto follows a theme another plugin hands over; a preset replaces the file's colours (/dock theme).",
            themeNames, ThemeIndex(s, themeKeys), i => SetTheme(themeKeys[i]));

        // ---- right column
        Column(0.03f, 0.25f, 0.17f, top);
        Section("PICTURE-IN-PICTURE");
        Check("Show the PiP", "A small dock camera window while you fly (/dock pip).", s.PipOn, v => DockPip.Set(v));
        Combo("PiP shows", "The dock camera with its guidance, or the Docking Assist with the live picture in its view box.",
            new[] { "Dock camera", "Docking Assist" }, Math.Max(0, Array.IndexOf(modes, s.PipMode)), i => Set(x => x.PipMode = modes[i]));
        Check("Guidance on top", "The crosshair, brackets and readouts over the PiP picture.", s.PipOverlay, v => Set(x => x.PipOverlay = v));
        Check("Up by itself in 3rd person", "The PiP comes up while the full-screen dock camera is paused in third person.", s.PipAuto, v => Set(x => x.PipAuto = v));
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

        // ---- bottom
        float bottom = m_size.Value.Y / 2 - 0.035f;
        Controls.Add(new MyGuiControlButton(new Vector2(-0.1f, bottom), text: new StringBuilder("Defaults"),
            toolTip: "Every setting back to its default (the dock camera's spot per connector is kept).",
            originAlign: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM, onButtonClick: b => ResetDefaults()));
        Controls.Add(new MyGuiControlButton(new Vector2(0.1f, bottom), text: new StringBuilder("Close"),
            originAlign: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM, onButtonClick: b => CloseScreen()));
        Controls.Add(new MyGuiControlLabel(new Vector2(0, bottom - 0.065f), text: "Ctrl+Alt+D opens this window in game. Changes apply at once.",
            textScale: 0.7f, originAlign: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM) { ColorMask = new Vector4(0.7f, 0.7f, 0.7f, 1) });
    }

    private void ResetDefaults()
    {
        bool pipWasOn = DockConfig.Current.PipOn;
        DockConfig.ResetDefaults(save: false);
        if (pipWasOn) DockPip.Set(false);
        RecreateControls(false);
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
