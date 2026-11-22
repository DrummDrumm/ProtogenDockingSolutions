using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;

namespace ProtogenDocking;

/// <summary>World hooks: the /dock chat command, the toolbar actions, the LCD script and the API.</summary>
[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
public class DockSession : MySessionComponentBase
{
    public override void LoadData()
    {
        DockApi.Load();
    }

    public override void BeforeStart()
    {
        DockConfig.EnsureLoaded();
        DockLcd.Register();
        try { DockActions.Register(); } catch (Exception e) { Plugin.Log.Write("toolbar actions failed: " + e.Message); }
        MyAPIGateway.Utilities.MessageEntered += OnMessage;
        DockApi.Announce();
    }

    protected override void UnloadData()
    {
        try { MyAPIGateway.Utilities.MessageEntered -= OnMessage; } catch { }
        DockActions.Unregister();
        DockApi.Unload();
        DockCam.Reset();
        DockLcdFeed.Reset();
        PlayGuard.Reset();
        DockConfig.External = null;
        base.UnloadData();
    }

    private static void OnMessage(string message, ref bool sendToOthers)
    {
        var parts = (message ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].Equals("/dock", StringComparison.OrdinalIgnoreCase)) return;
        sendToOthers = false;
        DockCam.Say(DockCommand.Run(parts.Skip(1).ToArray()), 4000);
    }
}

/// <summary>The /dock command (also open to other plugins and mods through the API's "Command").</summary>
internal static class DockCommand
{
    // Shown in the game's text window (DockCam.Say), one command per line.
    public const string Usage =
        "/dock on | off | view (1st / 3rd person)\n" +
        "/dock next (your connector) | target (the dock)\n" +
        "/dock mode cam | assist | next (full-screen view: camera / Docking Assist)\n" +
        "/dock style holo | tactical | bracket | glass | radial | classic | next (the Docking Assist's look)\n" +
        "/dock style full | pip | lcd <style> | next | same (a look for one place only; an LCD's Custom Data DockStyle=<style> beats it)\n" +
        "/dock offset <fwd> [<up> <right>] | reset (camera spot)\n" +
        "/dock steer on | off (move and rotate relative to the full-screen dock camera)\n" +
        "/dock damp on | off | speed <m/s> | accel <m/s2> (docking dampener)\n" +
        "/dock ship save | global (this ship's own settings / the global ones)\n" +
        "/dock pip (picture-in-picture; /dock pip help)\n" +
        "/dock lcdcam on | off (live camera on Docking Assist LCDs)\n" +
        "/dock quality performance | balanced | quality | ultra | next\n" +
        "/dock theme amber | cyan | green | white | auto | file\n" +
        "/dock tag <text> | ignore <text> | range <m> | map off | dockcam | on";

    public static string Run(string[] args)
    {
        if (PlayGuard.Blocked) return PlayGuard.Paused;
        if (args.Length == 0) return DockCam.Describe();
        string a = args[0].ToLowerInvariant();
        string rest = string.Join(" ", args.Skip(1));
        switch (a)
        {
            case "on": case "start": return DockCam.Active ? DockCam.Resume() : DockCam.Start();
            case "off": case "stop": return DockCam.Stop("Dock camera off.");
            case "toggle": return DockCam.Toggle();
            case "next": case "conn": return DockCam.NextMine();
            case "target": case "tgt": return DockCam.NextTarget();
            case "view": case "3rd": return DockCam.SwitchView();
            case "mode": return DockPip.CamMode(args.Length > 1 ? args[1].ToLowerInvariant() : "next");
            case "assist": return DockPip.CamMode("assist");
            case "style": return DockScreens.SetStyle(args.Length > 1 ? args[1].ToLowerInvariant() : null, args.Length > 2 ? args[2].ToLowerInvariant() : null);
            case "offset": case "pos": return DockCam.Offset(args.Skip(1).ToArray());
            case "reset": return DockCam.Offset(new[] { "reset" });
            case "theme":
                return Theme(args.Length > 1 ? args[1] : "");
            case "tag":
                if (rest.Length == 0) return "Dock tag: " + DockConfig.Current.DockTag + " (a connector with it in its name is picked first as yours). /dock tag <text>";
                DockConfig.Change(s => s.DockTag = rest);
                return "Connectors with " + rest + " in their name are picked first as yours.";
            case "ignore": case "notag":
                if (rest.Length == 0) return "Ignore tag: " + DockConfig.Current.IgnoreTag + " (a connector with it in its name is never used). /dock ignore <text>";
                DockConfig.Change(s => s.IgnoreTag = rest);
                return "Connectors with " + rest + " in their name are never used.";
            case "range":
                if (args.Length < 2 || !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double r))
                    return "Docks are looked for within " + DockConfig.Current.TargetRange.ToString("0", CultureInfo.InvariantCulture) + " m. /dock range <20-5000>";
                r = Math.Max(20, Math.Min(5000, r));
                DockConfig.Change(s => s.TargetRange = r);
                return "Docks are looked for within " + r.ToString("0", CultureInfo.InvariantCulture) + " m.";
            case "pip": return DockPip.Command(args.Skip(1).ToArray());
            case "steer": return DockCam.Steer(false, args.Length > 1 ? args[1].ToLowerInvariant() : null);
            case "damp": case "dampener": return DockCam.Damp(args.Skip(1).ToArray());
            case "ship": return DockConfig.ShipCommand(args.Length > 1 ? args[1] : null);
            case "lcdcam": case "lcdfeed":
            {
                bool on = args.Length < 2 ? !DockConfig.Current.LcdFeed : !(args[1].Equals("off", StringComparison.OrdinalIgnoreCase) || args[1] == "0");
                DockConfig.Change(x => x.LcdFeed = on);
                return on ? "Docking Assist LCDs show the live dock camera, with the assist over it." : "Docking Assist LCDs show the drawn view only.";
            }
            case "quality": case "preset": return Quality(args.Length > 1 ? args[1] : "");
            case "help": case "?": return Usage;
            default: return "Unknown: /dock " + args[0] + ". /dock help lists the commands.";
        }
    }

    /// <summary>The quality / performance preset ("next" steps through them, as the toolbar does).</summary>
    public static string Quality(string name)
    {
        var q = name.Equals("next", StringComparison.OrdinalIgnoreCase) ? DockQuality.Next(DockConfig.Current.Quality) : DockQuality.Find(name);
        if (q == null)
            return "Quality: " + DockConfig.Current.Quality + ". /dock quality " + string.Join(" | ", DockQuality.Presets.Select(p => p.Name)) + " | next";
        DockConfig.Change(q.ApplyTo);
        return "Docking quality: " + q.Name + " (" + q.Note + ").";
    }

    private static string Theme(string name)
    {
        string n = name.ToLowerInvariant();
        if (n == "auto")
        {
            DockConfig.Change(s => s.ThemeSource = "auto");
            return DockConfig.External != null ? "Docking colours follow the theme another plugin hands over." : "Docking colours: the settings file's, until a plugin hands over its theme.";
        }
        if (n == "file")
        {
            DockConfig.Change(s => s.ThemeSource = "file");
            return "Docking colours: always the settings file's (" + (DockConfig.FilePath ?? "Storage\\" + DockConfig.FolderName + "\\" + DockConfig.FileName) + ").";
        }
        var p = DockTheme.Preset(n);
        if (p == null) return "Docking colours: " + (DockConfig.Theme == DockConfig.External ? "another plugin's theme" : "the settings file's") + ". /dock theme amber | cyan | green | white | auto | file";
        DockConfig.Change(s => { var t = p.Copy(); t.Font = s.Theme.Font; t.NumberFont = s.Theme.NumberFont; s.Theme = t; s.ThemeSource = "file"; });
        return "Docking colours: " + n + ". /dock theme auto to follow another plugin's theme again.";
    }
}

/// <summary>
/// Docking toolbar actions on every cockpit and control block (vanilla or modded, anything that's a ship controller).
/// Added on this PC only; a slot is saved as the action's id, so the ids keep their old names and existing
/// toolbars keep working.
/// The actions are made again for each world. Mods wrap the action objects they're given (Build Info replaces Writer
/// with its own) and those wrappers die with their world. A kept action would call the last world's unloaded mod and
/// crash the game, e.g. Build Info's "Digi.Log accessed after it was unloaded" in the next world's toolbar update.
/// </summary>
internal static class DockActions
{
    private const string Icons = @"Textures\GUI\Icons\Actions\";
    private static readonly List<IMyTerminalAction> actions = new List<IMyTerminalAction>();
    private static bool registered;

    public static void Register()
    {
        if (registered) return;
        var tc = MyAPIGateway.TerminalControls;
        if (tc == null) return;
        if (actions.Count == 0)
        {
            Add(tc, "Protogen_DockCam", "Dock Camera: on / off (in third person: back to the dock camera)", "Toggle", () => DockCam.Say(DockCam.Toggle()),
                sb => sb.Append(DockCam.Active ? (DockCam.Suspended ? "3rd" : "Dock") : "Off"));
            Add(tc, "Protogen_DockCamNext", "Dock: next connector of yours (dock camera and screens)", "Increase", () => DockCam.Say(DockCam.NextMine()), sb => sb.Append("Conn"));
            Add(tc, "Protogen_DockCamTarget", "Dock: next dock to aim at (dock camera and screens)", "Reverse", () => DockCam.Say(DockCam.NextTarget()), sb => sb.Append("Tgt"));
            Add(tc, "Protogen_DockCamMode", "Dock Camera: camera view / Docking Assist (full screen)", "Toggle", () => DockCam.Say(DockPip.CamMode("next")),
                sb => sb.Append(DockConfig.Current.CamMode == "assist" ? "Asst" : "Cam"));
            Add(tc, "Protogen_DockStyle", "Docking Assist: next style", "Toggle", () => DockCam.Say(DockScreens.SetStyle("next")),
                sb => sb.Append(DockStyles.Short(DockConfig.Current.AssistStyle)));
            Add(tc, "Protogen_DockPip", "Dock Camera: picture-in-picture on / off", "Toggle", () => DockCam.Say(DockPip.Toggle()),
                sb => sb.Append(DockPip.On ? "PiP" : "Off"));
            Add(tc, "Protogen_DockPipCorner", "Dock PiP: next corner", "Reverse", () => DockCam.Say(DockPip.Command(new[] { "corner", "next" })),
                sb => sb.Append(DockConfig.Current.PipCorner.ToUpperInvariant()));
            Add(tc, "Protogen_DockPipMode", "Dock PiP: dock camera / Docking Assist", "Toggle", () => DockCam.Say(DockPip.ToggleMode()),
                sb => sb.Append(DockConfig.Current.PipMode == "assist" ? "Asst" : "Cam"));
            Add(tc, "Protogen_DockSteerView", "Dock Camera: steer from the camera on / off (the view that's up)", "Toggle", () => DockCam.Say(DockCam.SteerView()),
                sb => sb.Append(SteerShort()));
            Add(tc, "Protogen_DockSteer", "Dock Camera: steer from the full-screen camera on / off", "Toggle", () => DockCam.Say(DockCam.Steer(false, null)),
                sb => sb.Append(DockConfig.Current.SteerFull ? "Steer" : "Seat"));
            Add(tc, "Protogen_DockPipSteer", "Dock PiP: steer from the PiP camera on / off", "Toggle", () => DockCam.Say(DockCam.Steer(true, null)),
                sb => sb.Append(DockConfig.Current.SteerPip ? "Steer" : "Seat"));
            Add(tc, "Protogen_DockDamp", "Docking dampener on / off (speed and acceleration limits while docking)", "Toggle", () => DockCam.Say(DockCam.Damp(new string[0])),
                sb => sb.Append(DockConfig.Current.Damp ? "Damp" : "Off"));
            Add(tc, "Protogen_DockDampAuto", "Docking dampener Auto on / off (slower as the dock gets closer)", "Toggle", () => DockCam.Say(DockCam.Damp(new[] { "auto" })),
                sb => sb.Append(DockConfig.Current.Damp && DockConfig.Current.DampAuto ? "Auto" : "Fixed"));
            Add(tc, "Protogen_DockPipEdit", "Dock PiP: move / resize with the mouse", "Start", () => DockCam.Say(DockPip.Edit()), sb => sb.Append("Edit"));
            Add(tc, "Protogen_DockQuality", "Docking: next quality preset (performance / balanced / quality / ultra)", "Increase", () => DockCam.Say(DockCommand.Quality("next")),
                sb => sb.Append(QualityShort()));
        }
        tc.CustomActionGetter += AddActions;
        registered = true;
    }

    public static void Unregister()
    {
        if (!registered) return;
        try { MyAPIGateway.TerminalControls.CustomActionGetter -= AddActions; } catch { }
        actions.Clear(); // new objects next world: see the class summary
        registered = false;
    }

    private static string SteerShort()
    {
        var s = DockConfig.Current;
        if (s.SteerAuto) return DockSteer.IsDocking && DockSteer.SteerFor(false) ? "Steer" : "Auto";
        bool on = DockCam.Showing ? s.SteerFull : DockSteer.PipUp ? s.SteerPip : s.SteerFull || s.SteerPip;
        return on ? "Steer" : "Seat";
    }

    private static string QualityShort()
    {
        switch (DockConfig.Current.Quality)
        {
            case "performance": return "Perf";
            case "balanced": return "Bal";
            case "quality": return "Qual";
            case "ultra": return "Ultra";
            default: return "Cust";
        }
    }

    private static void AddActions(IMyTerminalBlock block, List<IMyTerminalAction> list)
    {
        if (block is IMyShipController) list.AddRange(actions);
    }

    private static void Add(IMyTerminalControls tc, string id, string name, string icon, Action run, Action<StringBuilder> writer)
    {
        var a = tc.CreateAction<IMyShipController>(id);
        a.Name = new StringBuilder(name);
        a.Icon = Icons + icon + ".dds";
        a.ValidForGroups = false;
        a.Enabled = b => true;
        a.Action = b =>
        {
            try { if (PlayGuard.Blocked) DockCam.Say(PlayGuard.Paused); else run(); }
            catch (Exception e) { Plugin.Log.Throttled("toolbar action failed: " + e.Message); }
        };
        a.Writer = (b, sb) => { try { writer(sb); } catch { } };
        actions.Add(a);
    }
}

/// <summary>The full-screen dock camera's overlay and the PiP window, drawn right after the game world on the main thread.</summary>
[HarmonyLib.HarmonyPatch(typeof(MyGuiScreenGamePlay), nameof(MyGuiScreenGamePlay.Draw))]
internal static class GamePlayDrawPatch
{
    private static void Postfix(bool __result)
    {
        if (!__result || PlayGuard.Blocked) return;
        if (DockCam.Showing) DockCam.Draw();
        DockPip.Draw();
    }
}
