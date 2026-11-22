using System;

namespace ProtogenDocking;

/// <summary>A key binding as text, "Ctrl+Alt+K": modifiers then one key name (VRage.Input.MyKeys). Empty = no key.</summary>
public static class DockKeys
{
    public static bool Parse(string text, out bool ctrl, out bool alt, out bool shift, out string key)
    {
        ctrl = alt = shift = false;
        key = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (var raw in text.Split('+'))
        {
            string p = raw.Trim();
            if (p.Length == 0) continue;
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase)) ctrl = true;
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
            else if (key == null) key = p;
            else return false; // two keys
        }
        return key != null;
    }

    public static string Format(bool ctrl, bool alt, bool shift, string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        return (ctrl ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") + key;
    }

    /// <summary>The same binding written the usual way, or "" when it isn't one.</summary>
    public static string Norm(string text) => Parse(text, out bool c, out bool a, out bool s, out string k) ? Format(c, a, s, k) : "";
}
