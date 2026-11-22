using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Sandbox.Game.World;
using VRage.FileSystem;

namespace ProtogenDocking;

/// <summary>
/// The settings files, reloaded when they change on disk; and the theme in use.
///
/// Global settings live in DockingSolutions.ini. A ship can have its own: Ships\(ship name).ini, used while you sit in
/// a seat on a ship of that name (ships with the same name share it). Back on global, the ship's file is renamed to
/// .ini.off, so its settings come back if you pick that ship again. The dock camera's spots per connector always stay in
/// the global file.
/// </summary>
internal static class DockConfig
{
    public const string FolderName = "ProtogenDockingSolutions", FileName = "DockingSolutions.ini";
    private static DockSettings global = new DockSettings();
    private static DockSettings ship;          // this ship's own settings, while it has some
    private static string shipName;            // the ship you sit in (null: not in a seat)
    private static bool loaded;
    private static DateTime lastWrite, shipWrite;
    private static long nextCheck;

    /// <summary>A theme handed over by another plugin or mod (API "SetTheme"), used while ThemeSource is "auto".</summary>
    public static DockTheme External;
    public static int Version { get; private set; } = 1;

    public static DockSettings Current { get { EnsureLoaded(); return ship ?? global; } }
    public static DockTheme Theme => Current.ThemeSource == "auto" && External != null ? External : Current.Theme;

    /// <summary>The ship you sit in, by name (null when you're not in a seat).</summary>
    public static string ShipName => shipName;
    /// <summary>The settings in use are this ship's own.</summary>
    public static bool PerShip => ship != null;

    /// <summary>Storage\ProtogenDockingSolutions under the game's user data folder (which follows -appdata); null until the game has set it.</summary>
    public static string Folder => string.IsNullOrEmpty(MyFileSystem.UserDataPath) ? null : Path.Combine(MyFileSystem.UserDataPath, "Storage", FolderName);
    public static string FilePath => Folder == null ? null : Path.Combine(Folder, FileName);
    public static string ShipsFolder => Folder == null ? null : Path.Combine(Folder, "Ships");

    /// <summary>The ship's settings file: its name with characters a file name can't have replaced.</summary>
    public static string ShipPath(string name)
    {
        if (ShipsFolder == null) return null;
        string f = ShipFileName(name);
        return f == null ? null : Path.Combine(ShipsFolder, f + ".ini");
    }

    internal static string ShipFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var sb = new StringBuilder();
        var bad = Path.GetInvalidFileNameChars();
        foreach (char c in name.Trim()) sb.Append(Array.IndexOf(bad, c) >= 0 ? '_' : c);
        string f = sb.ToString().TrimEnd('.', ' ');
        if (f.Length > 80) f = f.Substring(0, 80);
        return f.Length == 0 ? null : f;
    }

    public static void EnsureLoaded()
    {
        if (loaded || string.IsNullOrEmpty(MyFileSystem.UserDataPath)) return;
        loaded = true;
        if (dirty) Save();   // changes made before the folder was known
        else if (File.Exists(FilePath)) Reload();
        else { global = new DockSettings(); Save(); }
    }

    /// <summary>Every frame: follows the ship you sit in; checks the files about once a second.</summary>
    public static void Poll()
    {
        EnsureLoaded();
        if (!loaded) return;
        FollowShip();
        long now = Stopwatch.GetTimestamp();
        if (now < nextCheck) return;
        nextCheck = now + Stopwatch.Frequency;
        try
        {
            if (dirty) return;
            var t = File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath) : DateTime.MinValue;
            if (t != lastWrite) Reload();
            string sp = ShipPath(shipName);
            if (sp == null) return;
            if (ship != null)
            {
                if (!File.Exists(sp)) { ship = null; Version++; }       // the file was deleted: back on global
                else if (File.GetLastWriteTimeUtc(sp) != shipWrite) LoadShip();
            }
            else if (File.Exists(sp)) LoadShip();                     // the file was put there by hand
        }
        catch { }
    }

    private static void FollowShip()
    {
        string name = null;
        try { name = (MySession.Static?.ControlledEntity as Sandbox.Game.Entities.MyShipController)?.CubeGrid?.DisplayName; } catch { }
        if (string.IsNullOrWhiteSpace(name)) name = null;
        if (name == shipName) return;
        Flush();
        shipName = name;
        ship = null;
        string sp = ShipPath(name);
        if (sp != null && File.Exists(sp)) LoadShip();
        Version++;
    }

    private static void LoadShip()
    {
        string sp = ShipPath(shipName);
        try
        {
            var s = DockSettings.Parse(File.ReadAllText(sp));
            s.DockCamOffsets = global.DockCamOffsets;
            ship = s;
            shipWrite = File.GetLastWriteTimeUtc(sp);
        }
        catch (Exception e) { Plugin.Log.Write("couldn't read " + sp + ": " + e.Message); }
        Version++;
    }

    public static void Reload()
    {
        try
        {
            global = DockSettings.Parse(File.ReadAllText(FilePath));
            lastWrite = File.GetLastWriteTimeUtc(FilePath);
            if (ship != null) ship.DockCamOffsets = global.DockCamOffsets;
        }
        catch (Exception e) { Plugin.Log.Write("couldn't read " + FilePath + ": " + e.Message); }
        Version++;
    }

    private static bool dirty;

    /// <summary>Applies a change and saves it; save: false keeps it in memory until <see cref="Flush"/> (the settings window's sliders).</summary>
    public static void Change(Action<DockSettings> change, bool save = true)
    {
        EnsureLoaded();
        var s = Current.Copy();
        change(s);
        if (ship != null)
        {
            // the dock camera's spots stay global
            if (s.DockCamOffsets != global.DockCamOffsets)
            {
                var g = global.Copy();
                g.DockCamOffsets = s.DockCamOffsets;
                global = g;
                SaveFile(FilePath, global, ref lastWrite);
            }
            ship = s;
        }
        else global = s;
        if (save) Save(); else dirty = true;
        Version++;
    }

    /// <summary>Writes changes held in memory, if any.</summary>
    public static void Flush() { if (dirty) Save(); }

    /// <summary>Every setting back to its default (in the settings in use), except the dock camera's spots per connector.</summary>
    public static void ResetDefaults(bool save = true)
    {
        EnsureLoaded();
        var s = new DockSettings { DockCamOffsets = global.DockCamOffsets };
        if (ship != null) ship = s; else global = s;
        if (save) Save(); else dirty = true;
        Version++;
    }

    /// <summary>
    /// The ship you sit in gets its own settings: the ones it had before (if it was put back on global), else a copy of
    /// the global ones.
    /// </summary>
    public static string UseShip()
    {
        EnsureLoaded();
        string sp = ShipPath(shipName);
        if (sp == null) return "Sit in a seat on the ship first.";
        if (ship != null) return "\"" + shipName + "\" already has its own settings.";
        Flush();
        try
        {
            Directory.CreateDirectory(ShipsFolder);
            if (!File.Exists(sp) && File.Exists(sp + ".off")) File.Move(sp + ".off", sp);
        }
        catch (Exception e) { Plugin.Log.Write("couldn't restore " + sp + ": " + e.Message); }
        if (File.Exists(sp))
        {
            LoadShip();
            return "\"" + shipName + "\" uses its own settings again (the ones it had).";
        }
        ship = global.Copy();
        Save();
        Version++;
        return "\"" + shipName + "\" has its own settings now, starting from the global ones.";
    }

    /// <summary>The ship you sit in goes back to the global settings; its own are kept aside (.ini.off) for later.</summary>
    public static string UseGlobal()
    {
        EnsureLoaded();
        if (ship == null) return shipName == null ? "Using the global settings." : "\"" + shipName + "\" uses the global settings.";
        Flush();
        string sp = ShipPath(shipName);
        try
        {
            if (File.Exists(sp + ".off")) File.Delete(sp + ".off");
            File.Move(sp, sp + ".off");
        }
        catch (Exception e) { Plugin.Log.Write("couldn't put " + sp + " aside: " + e.Message); }
        ship = null;
        Version++;
        return "\"" + shipName + "\" uses the global settings (its own are kept for later).";
    }

    /// <summary>/dock ship [save | global]</summary>
    public static string ShipCommand(string arg)
    {
        switch ((arg ?? "").ToLowerInvariant())
        {
            case "save": case "own": case "this": case "on": return UseShip();
            case "global": case "all": case "off": return UseGlobal();
            default:
                return shipName == null ? "Using the global settings (not in a seat). /dock ship save | global"
                    : "\"" + shipName + "\" uses " + (ship != null ? "its own settings" : "the global settings") + ". /dock ship save | global";
        }
    }

    private static void Save()
    {
        // nowhere to save yet: keep the change in memory, EnsureLoaded saves it once the game's user data folder is known
        if (Folder == null) { dirty = true; return; }
        dirty = false;
        if (ship != null) SaveFile(ShipPath(shipName), ship, ref shipWrite);
        else SaveFile(FilePath, global, ref lastWrite);
    }

    private static void SaveFile(string path, DockSettings s, ref DateTime written)
    {
        if (path == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, s.Write());
            written = File.GetLastWriteTimeUtc(path);
        }
        catch (Exception e) { Plugin.Log.Write("couldn't save " + path + ": " + e.Message); }
    }

    public static bool HasTag(string name, string tag) =>
        !string.IsNullOrEmpty(tag) && (name ?? "").IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
}

/// <summary>Clocks: real seconds, and game seconds (stop while paused).</summary>
internal static class DockClock
{
    public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    public static double GameNow => MySession.Static?.ElapsedGameTime.TotalSeconds ?? Now;
}
