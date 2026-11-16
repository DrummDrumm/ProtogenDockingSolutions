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

/// <summary>The settings file, reloaded when it changes on disk; and the theme in use.</summary>
internal static class DockConfig
{
    public const string FolderName = "ProtogenDockingSolutions", FileName = "DockingSolutions.ini";
    private static DockSettings current = new DockSettings();
    private static bool loaded;
    private static DateTime lastWrite;
    private static long nextCheck;

    /// <summary>A theme handed over by another plugin or mod (API "SetTheme"), used while ThemeSource is "auto".</summary>
    public static DockTheme External;
    public static int Version { get; private set; } = 1;

    public static DockSettings Current { get { EnsureLoaded(); return current; } }
    public static DockTheme Theme => Current.ThemeSource == "auto" && External != null ? External : Current.Theme;

    public static string Folder => Path.Combine(string.IsNullOrEmpty(MyFileSystem.UserDataPath)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceEngineers") : MyFileSystem.UserDataPath, "Storage", FolderName);
    public static string FilePath => Path.Combine(Folder, FileName);

    public static void EnsureLoaded()
    {
        if (loaded || string.IsNullOrEmpty(MyFileSystem.UserDataPath)) return;
        loaded = true;
        if (File.Exists(FilePath)) Reload();
        else { current = new DockSettings(); Save(); }
    }

    /// <summary>Checks the file about once a second.</summary>
    public static void Poll()
    {
        EnsureLoaded();
        if (!loaded) return;
        long now = Stopwatch.GetTimestamp();
        if (now < nextCheck) return;
        nextCheck = now + Stopwatch.Frequency;
        try
        {
            var t = File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath) : DateTime.MinValue;
            if (t != lastWrite && !dirty) Reload();
        }
        catch { }
    }

    public static void Reload()
    {
        try
        {
            current = DockSettings.Parse(File.ReadAllText(FilePath));
            lastWrite = File.GetLastWriteTimeUtc(FilePath);
        }
        catch (Exception e) { Plugin.Log.Write("couldn't read " + FilePath + ": " + e.Message); }
        Version++;
    }

    private static bool dirty;

    /// <summary>Applies a change and saves it; save: false keeps it in memory until <see cref="Flush"/> (the settings window's sliders).</summary>
    public static void Change(Action<DockSettings> change, bool save = true)
    {
        EnsureLoaded();
        var s = current.Copy();
        change(s);
        current = s;
        if (save) Save(); else dirty = true;
        Version++;
    }

    /// <summary>Writes changes held in memory, if any.</summary>
    public static void Flush() { if (dirty) Save(); }

    /// <summary>Every setting back to its default, except the dock camera's spots per connector.</summary>
    public static void ResetDefaults(bool save = true)
    {
        EnsureLoaded();
        current = new DockSettings { DockCamOffsets = current.DockCamOffsets };
        if (save) Save(); else dirty = true;
        Version++;
    }

    private static void Save()
    {
        dirty = false;
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, current.Write());
            lastWrite = File.GetLastWriteTimeUtc(FilePath);
        }
        catch (Exception e) { Plugin.Log.Write("couldn't save " + FilePath + ": " + e.Message); }
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
