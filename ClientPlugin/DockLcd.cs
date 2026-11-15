using System;
using System.Collections.Generic;
using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Utils;
using VRageMath;

namespace ProtogenDocking;

/// <summary>
/// Adds "DOCK - Docking Assist" to the LCD Script list (any LCD, cockpit, seat or modded screen). Its id keeps its old
/// "ScriptHud:dock.view" name, so screens already set to it keep working.
/// The script runs on your PC only: other players see a blank screen.
/// </summary>
internal static class DockLcd
{
    public const string ScriptId = "ScriptHud:dock.view", ScriptName = "DOCK - Docking Assist";

    private static readonly AccessTools.FieldRef<MyTextSurfaceScriptFactory, Dictionary<string, MyTextSurfaceScriptFactory.ScriptInfo>> Scripts =
        AccessTools.FieldRefAccess<MyTextSurfaceScriptFactory, Dictionary<string, MyTextSurfaceScriptFactory.ScriptInfo>>("m_scripts");

    private static MyTextSurfaceScriptFactory registeredIn;

    public static void Register()
    {
        var factory = MyTextSurfaceScriptFactory.Instance;
        if (factory == null) return;
        try
        {
            Scripts(factory)[ScriptId] = new MyTextSurfaceScriptFactory.ScriptInfo
            {
                Id = ScriptId, DisplayName = MyStringId.GetOrCompute(ScriptName), Type = typeof(DockAssistScript)
            };
            registeredIn = factory;
        }
        catch (Exception e) { Plugin.Log.Throttled("couldn't add the Docking Assist LCD script: " + e.Message); }
    }

    /// <summary>Puts the script back if something rebuilt or cleared the list (another plugin may rewrite the list).</summary>
    public static void Update()
    {
        var factory = MyTextSurfaceScriptFactory.Instance;
        if (factory == null) return;
        try { if (registeredIn != factory || !Scripts(factory).ContainsKey(ScriptId)) Register(); } catch { }
    }

    /// <summary>The game rebuilds its LCD script list when a world loads; add ours right after.</summary>
    [HarmonyPatch(typeof(MyTextSurfaceScriptFactory), nameof(MyTextSurfaceScriptFactory.LoadScripts))]
    internal static class LoadScriptsPatch
    {
        private static void Postfix() => Register();
    }
}

/// <summary>
/// One screen showing the Docking Assist (redrawn every 10 ticks: it moves with the ships). Its view box shows the live
/// dock camera picture when it can (DockLcdFeed), with the assist drawn over it.
/// </summary>
public class DockAssistScript : MyTSSCommon
{
    private readonly SpriteListCanvas canvas = new SpriteListCanvas();
    private readonly List<MySprite> sprites = new List<MySprite>();
    private readonly DockScreen screen = new DockScreen();
    private int runs, flip;
    private string customData;

    /// <summary>When the game last ran this script (DockClock.Now): it stops when the screen is out of range or off.</summary>
    public double LastRun;

    public DockAssistScript(IMyTextSurface surface, IMyCubeBlock block, Vector2 size) : base(surface, block, size) { }

    public override ScriptUpdate NeedsUpdate => ScriptUpdate.Update10;

    public override void Run()
    {
        try
        {
            base.Run();
            LastRun = DockClock.Now;
            // the quality preset: performance redraws every other run
            if (runs++ % Math.Max(1, DockConfig.Current.LcdEvery) != 0) return;
            Redraw(false);
        }
        catch (Exception e) { Plugin.Log.Throttled("Docking Assist LCD script failed: " + e.Message); }
    }

    /// <summary>Draws the screen; forced = between the game's runs (DockLcdFeed.Tick), so it's put on the screen now.</summary>
    internal void Redraw(bool forced)
    {
        var cube = m_block as MyCubeBlock;
        if (cube == null) return;
        var panel = m_surface as MyTextPanelComponent;
        var view = new RectangleF((m_surface.TextureSize - m_surface.SurfaceSize) / 2, m_surface.SurfaceSize);
        // this LCD's own look: a DockStyle=<style> line in its Custom Data (read again only when it changes)
        string cd = (m_block as IMyTerminalBlock)?.CustomData ?? "";
        if (!ReferenceEquals(cd, customData)) { customData = cd; screen.Style = DockStyles.FromCustomData(cd) ?? ""; }
        screen.Place = "lcd";
        DockScreens.Layout(screen, view.Width, view.Height);
        string feed = DockLcdFeed.For(cube, panel?.Area ?? 0, screen.ViewW, screen.ViewH);
        screen.LiveView = feed != null;
        sprites.Clear();
        DockScreens.Draw(canvas.Begin(sprites, view.Position, m_surface), cube, view.Width, view.Height, screen);
        if (feed != null)
        {
            // the picture first, under the assist. Its colour flips by one step each time: the game only redraws a screen
            // whose sprites changed, and the picture changes without its sprite changing.
            var centre = view.Position + new Vector2(screen.ViewX + screen.ViewW / 2, screen.ViewY + screen.ViewH / 2);
            var tint = (flip++ & 1) == 0 ? Color.White : new Color(255, 255, 254);
            sprites.Insert(0, new MySprite(SpriteType.TEXTURE, feed, centre, new Vector2(screen.ViewW, screen.ViewH), tint, null, TextAlignment.CENTER));
        }
        using (var frame = m_surface.DrawFrame()) frame.AddRange(sprites);
        DockLcdFeed.Live(this, feed != null);
        if (forced) DockLcdFeed.ForceRender(panel);
        else DockLcdFeed.Drew(this);
    }

    public override void Dispose()
    {
        DockLcdFeed.Gone(this);
        base.Dispose();
    }
}
