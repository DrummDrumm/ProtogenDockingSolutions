using System;
using HarmonyLib;
using VRage.Plugins;
using VRage.Utils;

namespace ProtogenDocking;

// ReSharper disable once UnusedType.Global
public class Plugin : IPlugin
{
    public const string Name = "Protogen Systems Docking Solutions";
    private Harmony harmony;

    [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Init(object gameInstance)
    {
        try
        {
            harmony = new Harmony("ProtogenDockingSolutions");
            harmony.PatchAll(typeof(Plugin).Assembly);
        }
        catch (Exception e) { Log.Write("patching failed, the Docking Assist LCD script and overlay won't load: " + e); }
        DockPip.Init();
    }

    public void Update()
    {
        try
        {
            DockSettingsScreen.CheckHotkey();
            DockConfig.Poll();
            if (PlayGuard.Update(DockClock.Now))
            {
                bool cam = DockCam.Active;
                DockCam.Stop("");
                DockPip.Clear();
                if (cam) DockCam.Say("Dock camera off. " + PlayGuard.Paused, 4000);
            }
            if (PlayGuard.Blocked) return;
            DockHotkey.Check();
            DockSteer.Update();
            DockLcd.Update();
            DockCam.Update();
            DockPip.Tick();
            if (Sandbox.Game.World.MySession.Static != null) DockLcdFeed.Tick();
        }
        catch (Exception e) { Log.Throttled("update failed: " + e.Message); }
    }

    /// <summary>Pulsar's Config button.</summary>
    public void OpenConfigDialog() => DockSettingsScreen.Open();

    public void Dispose()
    {
        try { harmony?.UnpatchAll("ProtogenDockingSolutions"); } catch { }
        DockPip.Dispose();
    }

    internal static class Log
    {
        private static DateTime next;
        public static void Write(string msg)
        {
            try { MyLog.Default?.WriteLineAndConsole("Docking Solutions: " + msg); } catch { }
        }
        public static void Throttled(string msg)
        {
            if (DateTime.UtcNow < next) return;
            next = DateTime.UtcNow.AddSeconds(10);
            Write(msg);
        }
    }
}
