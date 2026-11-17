using System;
using Sandbox.Game.Gui;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.Input;

namespace ProtogenDocking;

/// <summary>
/// Server rules for client plugins (e.g. SDX2): the plugin must switch itself off while you're in admin mode or AFK.
/// While <see cref="Blocked"/> is set the dock camera stops, the picture-in-picture and its overlay go, Docking Assist
/// screens show only a "paused" line (no live picture), the /dock commands, toolbar actions and API calls do nothing, and
/// no extra scene is rendered. You turn the dock camera back on yourself once the reason has gone; the screens come back
/// on their own.
///  - Admin mode: creative tools, any admin-menu toggle (invulnerable, show players, use terminals...), the admin menu
///    open, or the spectator camera.
///  - AFK, in multiplayer only: no key, mouse button, mouse movement, wheel or controller input for 30 s (the game's
///    own AFK time, MySessionComponentTrash.CONST_TIME_TO_AFK). Single player has no AFK pause.
/// The settings window (Ctrl+Alt+D) still opens.
/// </summary>
internal static class PlayGuard
{
    public const double AfkSeconds = 30;
    private static double lastInput = -1;

    /// <summary>Why the plugin is off right now ("admin mode", "spectator camera", "AFK"...), or null when it may run.</summary>
    public static string Reason { get; private set; }
    public static bool Blocked => Reason != null;

    /// <summary>The message for a command or action tried while paused.</summary>
    public static string Paused => "Docking Solutions is paused (" + Reason + ").";

    public static void Reset() { lastInput = -1; Reason = null; axisSeen = null; }

    /// <summary>Every update, before anything else (Plugin.Update). True when it has just paused.</summary>
    public static bool Update(double now)
    {
        string was = Reason;
        Reason = Check(now);
        if (Reason != null && was == null) { Plugin.Log.Write("paused: " + Reason); return true; }
        if (Reason == null && was != null) Plugin.Log.Write("running again");
        return false;
    }

    private static string Check(double now)
    {
        var s = MySession.Static;
        if (s == null) { lastInput = -1; return null; }
        try
        {
            var input = MyInput.Static;
            if (lastInput < 0 || input != null && (input.IsAnyKeyPress() || input.IsAnyNewMousePressed() || input.IsAnyMousePressed()
                || input.GetMouseX() != 0 || input.GetMouseY() != 0 || input.DeltaMouseScrollWheelValue() != 0 || ControllerUsed(input)))
                lastInput = now;
            ulong me = Sync.MyId;
            if (s.CreativeToolsEnabled(me)) return "admin creative tools";
            if (s.AdminSettings != AdminSettingsEnum.None) return "admin mode";
            if (MyScreenManager.IsScreenOfTypeOpen(typeof(MyGuiScreenAdminMenu))) return "admin menu";
            if (s.IsCameraUserAnySpectator()) return "spectator camera";
            // AFK only counts in multiplayer: it's a server rule, and offline it would just stop the dock camera on a slow,
            // hands-off approach (the admin checks above apply everywhere)
            if (Sync.MultiplayerActive && now - lastInput > AfkSeconds) return "AFK";
            return null;
        }
        catch (Exception e)
        {
            Plugin.Log.Throttled("admin/AFK check failed, pausing: " + e.Message);
            return "admin/AFK check failed";
        }
    }

    private static readonly MyJoystickAxesEnum[] Axes = (MyJoystickAxesEnum[])Enum.GetValues(typeof(MyJoystickAxesEnum));
    private static readonly MyJoystickButtonsEnum[] Buttons = (MyJoystickButtonsEnum[])Enum.GetValues(typeof(MyJoystickButtonsEnum));
    private static float[] axisSeen;

    /// <summary>
    /// A controller button held, or a stick, trigger or slider moved more than 5% since it was last seen. Not the game's
    /// IsJoystickIdle: that wants every axis and both sliders centred, so a pad without sliders (an Xbox pad reads them
    /// as 0) or a throttle left off-centre never counts as idle, and AFK never comes.
    /// </summary>
    private static bool ControllerUsed(IMyInput input)
    {
        if (!input.IsJoystickConnected()) { axisSeen = null; return false; }
        bool used = false;
        foreach (var b in Buttons)
            if (b != MyJoystickButtonsEnum.None && input.IsJoystickButtonPressed(b)) { used = true; break; }
        if (axisSeen == null) { axisSeen = new float[Axes.Length]; for (int i = 0; i < axisSeen.Length; i++) axisSeen[i] = float.NaN; }
        for (int i = 0; i < Axes.Length; i++)
        {
            if (Axes[i] == MyJoystickAxesEnum.None) continue;
            float v = input.GetJoystickAxisStateForGameplay(Axes[i]);
            if (float.IsNaN(axisSeen[i])) { axisSeen[i] = v; continue; }
            if (Math.Abs(v - axisSeen[i]) > 0.05f) { axisSeen[i] = v; used = true; }
        }
        return used;
    }
}
