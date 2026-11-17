using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRageMath;
using IngameSurface = Sandbox.ModAPI.Ingame.IMyTextSurface;

namespace ProtogenDocking;

/// <summary>
/// The API for other plugins and mods, over the game's mod messages (works the same from a plugin or a client-side mod
/// script). On load, and whenever someone sends <see cref="Request"/> on <see cref="Channel"/>, this sends a
/// Dictionary&lt;string, Delegate&gt; on the same channel. Only types a mod may use cross over: VRageMath, MyTuple,
/// MySprite, IMyCubeBlock/IMyCubeGrid, the in-game IMyTextSurface and plain collections.
///
/// The ready-made client is Api/DockingSolutionsApi.cs (copy it into your mod or plugin). Bump <see cref="Version"/>
/// only for breaking changes; new entries can be added freely (clients check for the key).
/// </summary>
internal static class DockApi
{
    /// <summary>"PROTODOC".</summary>
    public const long Channel = 0x50524F544F444F43;
    public const string Request = "ProtogenDocking.Request";
    public const int Version = 1;

    private static Dictionary<string, Delegate> table;
    private static bool loaded;
    private static readonly SpriteListCanvas canvas = new SpriteListCanvas();
    private static readonly DockScreen scratch = new DockScreen();

    public static void Load()
    {
        if (loaded) return;
        try
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(Channel, OnMessage);
            loaded = true;
            Announce();
        }
        catch (Exception e) { Plugin.Log.Write("API failed to start: " + e.Message); }
    }

    public static void Unload()
    {
        if (!loaded) return;
        try { MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, OnMessage); } catch { }
        loaded = false;
    }

    /// <summary>Hands the API to everyone listening (again; clients take the latest).</summary>
    public static void Announce()
    {
        if (!loaded) return;
        try { MyAPIGateway.Utilities.SendModMessage(Channel, Table()); } catch (Exception e) { Plugin.Log.Throttled("API announce failed: " + e.Message); }
    }

    private static void OnMessage(object m)
    {
        if (m is string s && s == Request) Announce();
    }

    private static Dictionary<string, Delegate> Table() => table ??= new Dictionary<string, Delegate>
    {
        ["Version"] = new Func<int>(() => Version),
        ["Active"] = new Func<bool>(() => DockCam.Active),
        ["Showing"] = new Func<bool>(() => DockCam.Showing),
        ["Command"] = new Func<string, string>(Command),
        ["Describe"] = new Func<string>(DockCam.Describe),
        ["Pair"] = new Func<IMyCubeGrid, MyTuple<IMyCubeBlock, IMyCubeBlock>>(Pair),
        ["Reading"] = new Func<IMyCubeBlock, Dictionary<string, object>>(Reading),
        ["CameraFrame"] = new Func<IMyCubeBlock, MyTuple<bool, MatrixD, IMyCubeGrid>>(CameraFrame),
        ["LensFovY"] = new Func<float, float, float>(DockScreens.LensFovY),
        ["AssistViewBox"] = new Func<float, float, Vector4>(AssistViewBox),
        ["NewScreen"] = new Func<object>(() => new DockScreen()),
        ["DrawAssist"] = new Action<IMyCubeBlock, Vector4, bool, object, List<MySprite>, IngameSurface>(DrawAssist),
        ["DrawOverlay"] = new Action<IMyCubeBlock, Vector4, object, List<MySprite>, IngameSurface>(DrawOverlay),
        ["SetTheme"] = new Action<Dictionary<string, object>>(SetTheme),
        ["GetOffset"] = new Func<IMyCubeBlock, Vector3D>(GetOffset),
        ["SetOffset"] = new Action<IMyCubeBlock, Vector3D>(SetOffset),
    };

    private static string Command(string text) =>
        DockCommand.Run((text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

    private static DockTrack Track(IMyCubeBlock block) => DockTrack.For(block?.CubeGrid as MyCubeGrid);

    private static MyTuple<IMyCubeBlock, IMyCubeBlock> Pair(IMyCubeGrid grid)
    {
        var t = DockTrack.For(grid as MyCubeGrid);
        return new MyTuple<IMyCubeBlock, IMyCubeBlock>(t?.Mine, t?.Target);
    }

    /// <summary>The dock camera's spot for a screen or seat on this ship: on your connector's face, looking out.</summary>
    private static bool Frame(IMyCubeBlock block, out DockTrack t, out MatrixD world)
    {
        world = MatrixD.Identity;
        t = null;
        if (PlayGuard.Blocked) return false; // admin mode or AFK: nothing for other plugins either
        t = Track(block);
        return t?.Mine != null && block is MyCubeBlock cube && t.FrameFor(cube, out world);
    }

    private static MyTuple<bool, MatrixD, IMyCubeGrid> CameraFrame(IMyCubeBlock block)
    {
        try
        {
            if (Frame(block, out var t, out var world)) return new MyTuple<bool, MatrixD, IMyCubeGrid>(true, world, t.Mine.CubeGrid);
        }
        catch (Exception e) { Plugin.Log.Throttled("API CameraFrame failed: " + e.Message); }
        return new MyTuple<bool, MatrixD, IMyCubeGrid>(false, MatrixD.Identity, null);
    }

    private static Dictionary<string, object> Reading(IMyCubeBlock block)
    {
        var d = new Dictionary<string, object>();
        try
        {
            if (!Frame(block, out var t, out var world)) { d["HasTarget"] = false; d["Mine"] = ""; return d; }
            var r = t.Read(world);
            d["HasTarget"] = r.HasTarget;
            d["Range"] = r.Range;
            d["Ahead"] = r.Ahead;
            d["OffRight"] = r.OffRight;
            d["OffUp"] = r.OffUp;
            d["Closing"] = r.Closing;
            d["DriftRight"] = r.DriftRight;
            d["DriftUp"] = r.DriftUp;
            d["Misalign"] = r.Misalign;
            d["Safe"] = r.Safe;
            d["SpeedLevel"] = r.SpeedLevel;
            d["InLockRange"] = r.InLockRange;
            d["Connectable"] = r.Connectable;
            d["Locked"] = r.Locked;
            d["Mine"] = r.Mine;
            d["Target"] = r.Target;
            d["TargetGrid"] = r.TargetGrid;
            d["Advice"] = DockGuide.Advice(r);
        }
        catch (Exception e) { Plugin.Log.Throttled("API Reading failed: " + e.Message); }
        return d;
    }

    private static Vector4 AssistViewBox(float w, float h)
    {
        DockScreens.Layout(scratch, w, h);
        return new Vector4(scratch.ViewX, scratch.ViewY, scratch.ViewW, scratch.ViewH);
    }

    /// <summary>The Docking Assist for a screen on a ship into "sprites": rect = x, y (where on the surface), width, height.</summary>
    private static void DrawAssist(IMyCubeBlock block, Vector4 rect, bool live, object state, List<MySprite> sprites, IngameSurface surface)
    {
        if (sprites == null || PlayGuard.Blocked || !(block is MyCubeBlock cube)) return;
        try
        {
            var s = state as DockScreen ?? scratch;
            s.LiveView = live;
            DockScreens.Draw(canvas.Begin(sprites, new Vector2(rect.X, rect.Y), surface), cube, rect.Z, rect.W, s);
        }
        catch (Exception e) { Plugin.Log.Throttled("API DrawAssist failed: " + e.Message); }
    }

    /// <summary>The dock camera's guidance (crosshair, brackets, corridor, readouts) to lay over a live feed rendered with LensFovY.</summary>
    private static void DrawOverlay(IMyCubeBlock block, Vector4 rect, object state, List<MySprite> sprites, IngameSurface surface)
    {
        if (sprites == null || PlayGuard.Blocked || !(block is MyCubeBlock cube)) return;
        try { DockScreens.DrawFeed(canvas.Begin(sprites, new Vector2(rect.X, rect.Y), surface), cube, rect.Z, rect.W, state as DockScreen ?? scratch); }
        catch (Exception e) { Plugin.Log.Throttled("API DrawOverlay failed: " + e.Message); }
    }

    /// <summary>
    /// Colours (VRageMath.Color, Vector4 0-1, or "r,g,b") by name: Main, Dim, Good, Warn, Bad, Text, Background; and Font,
    /// NumberFont. Used while the settings' ThemeSource is "auto". null = back to the settings file's theme.
    /// </summary>
    private static void SetTheme(Dictionary<string, object> values)
    {
        if (values == null) { DockConfig.External = null; return; }
        var t = DockConfig.External?.Copy() ?? DockConfig.Current.Theme.Copy();
        foreach (var kv in values)
        {
            if (kv.Key == "Font" && kv.Value is string f && f.Length > 0) { t.Font = f; continue; }
            if (kv.Key == "NumberFont" && kv.Value is string nf && nf.Length > 0) { t.NumberFont = nf; continue; }
            HudColor c;
            if (kv.Value is Color col) c = new HudColor(col.R, col.G, col.B, col.A);
            else if (kv.Value is Vector4 v) c = new HudColor((int)(v.X * 255), (int)(v.Y * 255), (int)(v.Z * 255), (int)(v.W * 255));
            else if (!(kv.Value is string str) || !HudColor.TryParse(str, out c)) continue;
            t.Set(kv.Key, c);
        }
        DockConfig.External = t;
    }

    private static Vector3D GetOffset(IMyCubeBlock connector)
    {
        if (connector == null) return Vector3D.Zero;
        var o = DockTrack.Offset(connector.EntityId);
        return new Vector3D(o.Forward, o.Up, o.Right);
    }

    private static void SetOffset(IMyCubeBlock connector, Vector3D o)
    {
        if (connector is MyShipConnector) DockTrack.SetOffset(connector.EntityId, o.X, o.Y, o.Z);
    }
}
