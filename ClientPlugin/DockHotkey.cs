using System;
using System.Linq;
using Sandbox.Game.Gui;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Input;

namespace ProtogenDocking;

/// <summary>
/// The steer-from-the-camera key (SteerKey, set in the settings window): switches steering for the view that is up
/// (full screen, else the PiP; with neither up, both). Only in the game's own view, not while typing or in a menu.
/// </summary>
internal static class DockHotkey
{
    /// <summary>Keys that can be bound: everything but the mouse buttons, the modifiers themselves, Windows and Escape.</summary>
    internal static readonly MyKeys[] Bindable = Enum.GetValues(typeof(MyKeys)).Cast<MyKeys>().Distinct()
        .Where(k => (byte)k > 7 && k != MyKeys.Escape && k != MyKeys.Shift && k != MyKeys.Control && k != MyKeys.Alt
                    && k != MyKeys.LeftShift && k != MyKeys.RightShift && k != MyKeys.LeftControl && k != MyKeys.RightControl
                    && k != MyKeys.LeftAlt && k != MyKeys.RightAlt && k != MyKeys.LeftWindows && k != MyKeys.RightWindows)
        .ToArray();

    private static string parsedFrom;
    private static bool ctrl, alt, shift;
    private static MyKeys key;

    /// <summary>Every frame from Plugin.Update.</summary>
    public static void Check()
    {
        var input = MyInput.Static;
        if (input == null || PlayGuard.Blocked) return;
        string text = DockConfig.Current.SteerKey;
        if (text != parsedFrom)
        {
            parsedFrom = text;
            key = MyKeys.None;
            if (DockKeys.Parse(text, out ctrl, out alt, out shift, out string name) && Enum.TryParse(name, true, out MyKeys k)) key = k;
        }
        if (key == MyKeys.None || !input.IsNewKeyPressed(key)) return;
        if (input.IsAnyCtrlKeyPressed() != ctrl || input.IsAnyAltKeyPressed() != alt || input.IsAnyShiftKeyPressed() != shift) return;
        if (!(MyScreenManager.GetScreenWithFocus() is MyGuiScreenGamePlay)) return;
        if (MyAPIGateway.Gui?.ChatEntryVisible ?? false) return;
        DockCam.Say(DockCam.SteerView());
    }

    /// <summary>The key pressed this frame (with modifiers) as a binding, for the settings window; null when none.</summary>
    public static string Pressed()
    {
        var input = MyInput.Static;
        if (input == null) return null;
        foreach (var k in Bindable)
            if (input.IsNewKeyPressed(k))
                return DockKeys.Format(input.IsAnyCtrlKeyPressed(), input.IsAnyAltKeyPressed(), input.IsAnyShiftKeyPressed(), k.ToString());
        return null;
    }
}
