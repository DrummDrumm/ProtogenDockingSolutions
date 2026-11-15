using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Sandbox.Definitions;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Render11.Common;
using VRage.Render11.Resources;
using VRage.Utils;
using VRageMath;
using VRageRender;
using VRageRender.Messages;

namespace ProtogenDocking;

/// <summary>
/// The live dock camera picture inside the "DOCK - Docking Assist" LCD script's view box, with the assist drawn over it.
///
/// An LCD shows sprites, so the picture becomes one: the scene is rendered from the dock camera (the PiP's render,
/// DockPip.RenderScene) into a texture named ProtogenDockFeed_..., which is added as an LCD sprite. The script puts that
/// sprite in its view box and draws the assist on top. The game redraws a scripted LCD at most 6 times a second, so for
/// faster presets the feed screens are redrawn from the plugin's update as well (ForceRedraw).
/// All on this PC: other players see the plain Docking Assist (or nothing, without the plugin).
/// </summary>
internal static class DockLcdFeed
{
    // ------------------------------------------------------------------ game thread

    private sealed class Want
    {
        public string Name;          // the texture (and LCD sprite) name; holds the size, so a new size is a new texture
        public uint Actor;           // the connector's grid's render object
        public MatrixD Local;        // the camera relative to that grid
        public float Fov, Near, Far, FarFar;
        public int W, H;
        public long At;              // Stopwatch time of the last request
    }

    private static readonly ConcurrentDictionary<string, Want> wants = new ConcurrentDictionary<string, Want>();
    private static readonly ConcurrentDictionary<string, bool> rendered = new ConcurrentDictionary<string, bool>();
    private static readonly HashSet<string> sprites = new HashSet<string>();

    /// <summary>
    /// The sprite to put in a Docking Assist LCD's view box (viewW x viewH texture pixels), or null when there is no
    /// picture (yet): the setting is off, the screen is far away or out of view, or the renderer hooks failed.
    /// </summary>
    public static string For(MyCubeBlock block, int area, float viewW, float viewH)
    {
        try
        {
            var s = DockConfig.Current;
            if (!s.LcdFeed || !DockPip.Hooked || block?.CubeGrid == null || viewW < 16 || viewH < 16) return null;
            var cam = MySector.MainCamera;
            if (cam == null) return null;
            var box = block.PositionComp.WorldAABB;
            // the game stops drawing LCDs further away than this anyway; and nothing for a screen you can't see
            if (Vector3D.DistanceSquared(cam.Position, box.Center) > 150 * 150 || !cam.IsInFrustum(ref box)) return null;
            var t = DockTrack.For(block.CubeGrid);
            if (t?.Mine == null || !t.FrameFor(block, out var world)) return null;
            var grid = t.Mine.CubeGrid;
            uint actor = grid?.Render?.GetRenderObjectID() ?? uint.MaxValue;
            if (actor == uint.MaxValue) return null;

            int h = (int)Math.Min(viewH, Math.Max(120, s.LcdRes)) & ~1;
            int w = (int)Math.Round(h * viewW / viewH / 2) * 2;
            string key = block.EntityId + "_" + area;
            string name = "ProtogenDockFeed_" + key + "_" + w + "x" + h;
            Register(name);
            wants[key] = new Want
            {
                Name = name, Actor = actor, Local = world * MatrixD.Invert(grid.WorldMatrix),
                Fov = DockScreens.LensFovY(viewW, viewH), Near = cam.NearPlaneDistance, Far = cam.FarPlaneDistance, FarFar = cam.FarFarPlaneDistance,
                W = w, H = h, At = Stopwatch.GetTimestamp(),
            };
            return rendered.ContainsKey(name) ? name : null;
        }
        catch (Exception e)
        {
            Plugin.Log.Throttled("Docking Assist LCD camera failed: " + e.Message);
            return null;
        }
    }

    /// <summary>Makes the texture name usable as an LCD sprite (not listed in the LCD's image list).</summary>
    private static void Register(string name)
    {
        if (sprites.Contains(name)) return;
        var mgr = MyDefinitionManager.Static;
        var id = new MyDefinitionId(typeof(MyObjectBuilder_LCDTextureDefinition), name);
        var def = new MyLCDTextureDefinition { Id = id, TexturePath = name, SpritePath = name, Selectable = false, Public = false };
        var set = AccessTools.Field(typeof(MyDefinitionManagerBase), "m_definitions").GetValue(mgr) as MyDefinitionSet;
        set?.AddOrReplaceDefinition(def);
        var byIdField = set == null ? null : AccessTools.Field(set.GetType(), "m_definitionsById");
        if (byIdField?.GetValue(set) is System.Collections.IDictionary byId) byId[id] = def;
        sprites.Add(name);
    }

    // ---- redrawing feed screens faster than the game does (6 a second)

    private static readonly List<DockAssistScript> live = new List<DockAssistScript>();
    private static readonly Dictionary<DockAssistScript, double> lastDraw = new Dictionary<DockAssistScript, double>();

    public static void Live(DockAssistScript script, bool feed)
    {
        if (feed) { if (!live.Contains(script)) live.Add(script); }
        else Gone(script);
    }

    public static void Gone(DockAssistScript script)
    {
        live.Remove(script);
        lastDraw.Remove(script);
    }

    /// <summary>Every update: redraw feed screens between the game's own runs, at the preset's LCD camera rate.</summary>
    public static void Tick()
    {
        int fps = DockConfig.Current.LcdFps;
        if (live.Count == 0 || fps <= 6) return;
        double now = DockClock.Now, every = 1.0 / fps;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var sc = live[i];
            // the game stopped running it (screen out of range, off, or another script): stop too
            if (now - sc.LastRun > 0.5) { Gone(sc); continue; }
            if (lastDraw.TryGetValue(sc, out double at) && now - at < every) continue;
            lastDraw[sc] = now;
            sc.Redraw(true);
        }
    }

    /// <summary>World unloaded: forget its screens (its sprite definitions go with it; the textures follow in 2 s).</summary>
    public static void Reset()
    {
        live.Clear();
        lastDraw.Clear();
        wants.Clear();
        sprites.Clear();
        rendered.Clear();
    }

    public static void Drew(DockAssistScript script) => lastDraw[script] = DockClock.Now;

    private static readonly PropertyInfo renderProp = AccessTools.Property(typeof(MyTextPanelComponent), "Render");
    private static readonly AccessTools.FieldRef<MyTextPanelComponent, int> areaField = AccessTools.FieldRefAccess<MyTextPanelComponent, int>("m_area");
    private static readonly AccessTools.FieldRef<MyTextPanelComponent, Vector2I> sizeField = AccessTools.FieldRefAccess<MyTextPanelComponent, Vector2I>("m_textureSize");
    private static readonly AccessTools.FieldRef<MyTextPanelComponent, Vector2> aspectField = AccessTools.FieldRefAccess<MyTextPanelComponent, Vector2>("m_screenAspectRatio");
    private static readonly AccessTools.FieldRef<MyTextPanelComponent, List<MySprite>> layersField = AccessTools.FieldRefAccess<MyTextPanelComponent, List<MySprite>>("m_renderLayers");
    private static bool forceFailed;

    /// <summary>Has the screen draw the sprites it was just given now, rather than at its next 10-frame update.</summary>
    public static void ForceRender(MyTextPanelComponent panel)
    {
        if (forceFailed || panel == null) return;
        try
        {
            if (!(renderProp?.GetValue(panel) is MyRenderComponentScreenAreas render)) return;
            render.RenderSpritesToTexture(areaField(panel), layersField(panel), sizeField(panel), aspectField(panel), panel.ScriptBackgroundColor, panel.BackgroundAlpha);
        }
        catch (Exception e)
        {
            forceFailed = true;
            Plugin.Log.Write("Docking Assist LCD camera: faster screen redraws unavailable, staying at 6 a second: " + e.Message);
        }
    }

    // ------------------------------------------------------------------ render thread

    private static readonly Dictionary<string, IUserGeneratedTexture> textures = new Dictionary<string, IUserGeneratedTexture>();
    private static readonly Dictionary<string, long> renderedAt = new Dictionary<string, long>();
    private static readonly List<string> scratch = new List<string>();
    private static int next;

    /// <summary>After the main scene: render the one LCD feed most overdue (one a frame keeps the cost even).</summary>
    public static void RenderFrame(IRtvBindable mainTarget)
    {
        try
        {
            long now = Stopwatch.GetTimestamp();
            Cleanup(now);
            if (wants.IsEmpty) return;
            int fps = Math.Max(1, Math.Min(30, DockConfig.Current.LcdFps));
            long every = Stopwatch.Frequency / fps;
            scratch.Clear();
            foreach (var k in wants.Keys) scratch.Add(k);
            for (int i = 0; i < scratch.Count; i++)
            {
                var key = scratch[(next + i) % scratch.Count];
                if (!wants.TryGetValue(key, out var w)) continue;
                if (renderedAt.TryGetValue(w.Name, out long at) && now - at < every) continue;
                next = (next + i + 1) % Math.Max(1, scratch.Count);
                if (Render(w, mainTarget)) { renderedAt[w.Name] = now; rendered[w.Name] = true; }
                return;
            }
        }
        catch (Exception e)
        {
            // the whole trace once (where it broke), then the short message now and then
            if (!loggedTrace) { loggedTrace = true; Plugin.Log.Write("Docking Assist LCD camera render failed: " + e); }
            else Plugin.Log.Throttled("Docking Assist LCD camera render failed: " + e.Message);
        }
    }

    private static bool loggedTrace;

    private static bool Render(Want w, IRtvBindable mainTarget)
    {
        if (!textures.TryGetValue(w.Name, out var tex) || tex == null)
        {
            // the pixels must be handed over: without them the game only notes the size and never makes the GPU texture
            tex = MyManagers.FileTextures.CreateGeneratedTexture(w.Name, w.W, w.H, MyGeneratedTextureType.RGBA, false, new byte[w.W * w.H * 4], true);
            if (tex?.Resource == null)
            {
                if (tex != null) try { MyManagers.FileTextures.DestroyGeneratedTexture(w.Name); } catch { }
                return false;
            }
            textures[w.Name] = tex;
        }
        var format = mainTarget.Rtv.Description.Format;
        bool srgb = format.ToString().EndsWith("SRgb", StringComparison.Ordinal);
        var scene = MyManagers.RwTexturesPool.BorrowRtv("ProtogenDockFeed.Scene", w.W, w.H, format);
        var bytes = MyManagers.RwTexturesPool.BorrowRtv("ProtogenDockFeed.Copy", w.W, w.H, srgb ? Format.R8G8B8A8_UNorm_SRgb : Format.R8G8B8A8_UNorm);
        try
        {
            if (!DockPip.RenderScene(scene, w.Actor, w.Local, w.Fov, w.Near, w.Far, w.FarFar, w.W, w.H, false)) return false;
            // the picture as the screen shows it, fully opaque, then byte for byte into the sRGB sprite texture, so the
            // LCD reads it back as the colours you'd see on your monitor
            var rc = MyRender11.RC;
            rc.ClearRtv(bytes, new RawColor4(0, 0, 0, 1));
            rc.SetBlendState(MyBlendStateManager.BlendReplaceNoAlphaChannel);
            rc.SetInputLayout(null);
            rc.PixelShader.Set(MyCopyToRT.CopyPs);
            rc.SetRtv(bytes);
            rc.SetDepthStencilState(MyDepthStencilStateManager.IgnoreDepthStencil);
            rc.PixelShader.SetSamplers(0, MySamplerStateManager.StandardSamplers);
            rc.PixelShader.SetSrv(0, scene);
            MyScreenPass.DrawFullscreenQuad(rc, new MyViewport(w.W, w.H));
            rc.PixelShader.SetSrv(0, null);
            rc.SetRtvNull();
            rc.SetBlendState(null);
            rc.CopyResource(bytes, tex);
            return true;
        }
        finally
        {
            scene.Release();
            bytes.Release();
        }
    }

    /// <summary>Drops feeds nobody asked for in 2 s (screen gone, out of view, setting off) and textures of old sizes.</summary>
    private static void Cleanup(long now)
    {
        long stale = Stopwatch.Frequency * 2;
        foreach (var kv in wants)
            if (now - kv.Value.At > stale) wants.TryRemove(kv.Key, out _);
        if (textures.Count == 0) return;
        scratch.Clear();
        foreach (var name in textures.Keys)
        {
            bool used = false;
            foreach (var w in wants.Values) if (w.Name == name) { used = true; break; }
            if (!used) scratch.Add(name);
        }
        foreach (var name in scratch)
        {
            try { MyManagers.FileTextures.DestroyGeneratedTexture(name); } catch { }
            textures.Remove(name);
            renderedAt.Remove(name);
            rendered.TryRemove(name, out _);
        }
    }
}
