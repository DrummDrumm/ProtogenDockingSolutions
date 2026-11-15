using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRageMath;
using IngameSurface = Sandbox.ModAPI.Ingame.IMyTextSurface;

namespace ProtogenDocking.Api
{
    /// <summary>
    /// Client for Protogen Systems Docking Solutions. Copy this file into your mod (client-side scripts) or plugin; it uses
    /// only types the mod whitelist allows. Nothing here talks to the server: it all runs on the player's PC.
    ///
    /// Use:
    ///   var dock = new DockingSolutionsApi();
    ///   dock.Load(() => { /* ready */ });          // in LoadData / BeforeStart
    ///   ...
    ///   if (dock.IsReady) { var f = dock.CameraFrame(seat); if (f.Item1) { /* f.Item2 = the dock camera's world matrix */ } }
    ///   dock.Unload();                             // in UnloadData
    ///
    /// Picture: the dock camera sits on the ship's chosen connector face, looking out. To show a live picture, render a
    /// camera at CameraFrame(...) with the vertical field of view LensFovY(w, h); then DrawOverlay(...) lines up with it.
    /// Or draw the whole Docking Assist (drawn approach view, scope and gauges) with DrawAssist(...).
    /// The sprites are ordinary LCD sprites (MySprite): add them to an LCD frame, or draw them however you draw.
    /// </summary>
    public class DockingSolutionsApi
    {
        public const long Channel = 0x50524F544F444F43;
        public const string Request = "ProtogenDocking.Request";
        public const int SupportedVersion = 1;

        private Action onReady;
        private bool registered;

        public bool IsReady { get; private set; }
        public int Version { get; private set; }

        private Func<bool> active, showing;
        private Func<string, string> command;
        private Func<string> describe;
        private Func<IMyCubeGrid, MyTuple<IMyCubeBlock, IMyCubeBlock>> pair;
        private Func<IMyCubeBlock, Dictionary<string, object>> reading;
        private Func<IMyCubeBlock, MyTuple<bool, MatrixD, IMyCubeGrid>> cameraFrame;
        private Func<float, float, float> lensFovY;
        private Func<float, float, Vector4> assistViewBox;
        private Func<object> newScreen;
        private Action<IMyCubeBlock, Vector4, bool, object, List<MySprite>, IngameSurface> drawAssist;
        private Action<IMyCubeBlock, Vector4, object, List<MySprite>, IngameSurface> drawOverlay;
        private Action<Dictionary<string, object>> setTheme;
        private Func<IMyCubeBlock, Vector3D> getOffset;
        private Action<IMyCubeBlock, Vector3D> setOffset;

        /// <summary>Starts listening and asks Docking Solutions for its API. onReady runs (on the game thread) once it arrives.</summary>
        public void Load(Action onReady = null)
        {
            this.onReady = onReady;
            if (!registered)
            {
                MyAPIGateway.Utilities.RegisterMessageHandler(Channel, OnMessage);
                registered = true;
            }
            MyAPIGateway.Utilities.SendModMessage(Channel, Request);
        }

        public void Unload()
        {
            if (registered) MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, OnMessage);
            registered = false;
            IsReady = false;
            onReady = null;
        }

        private void OnMessage(object message)
        {
            var table = message as IReadOnlyDictionary<string, Delegate>;
            if (table == null) return;
            Delegate v;
            Version = table.TryGetValue("Version", out v) && v is Func<int> ? ((Func<int>)v)() : 0;
            if (Version < 1 || Version > SupportedVersion) return;
            Get(table, "Active", out active);
            Get(table, "Showing", out showing);
            Get(table, "Command", out command);
            Get(table, "Describe", out describe);
            Get(table, "Pair", out pair);
            Get(table, "Reading", out reading);
            Get(table, "CameraFrame", out cameraFrame);
            Get(table, "LensFovY", out lensFovY);
            Get(table, "AssistViewBox", out assistViewBox);
            Get(table, "NewScreen", out newScreen);
            Get(table, "DrawAssist", out drawAssist);
            Get(table, "DrawOverlay", out drawOverlay);
            Get(table, "SetTheme", out setTheme);
            Get(table, "GetOffset", out getOffset);
            Get(table, "SetOffset", out setOffset);
            bool first = !IsReady;
            IsReady = true;
            if (first && onReady != null) onReady();
        }

        private static void Get<T>(IReadOnlyDictionary<string, Delegate> table, string name, out T field) where T : class
        {
            Delegate d;
            field = table.TryGetValue(name, out d) ? d as T : null;
        }

        /// <summary>The full-screen dock camera is on (it may be paused in third person).</summary>
        public bool Active() { return active != null && active(); }

        /// <summary>The full-screen dock camera's view and overlay fill the screen now: HUD plugins may want to step aside.</summary>
        public bool Showing() { return showing != null && showing(); }

        /// <summary>Runs a /dock command ("on", "off", "next", "target", "map on", ...) and returns the message it would show.</summary>
        public string Command(string args) { return command != null ? command(args) : ""; }

        /// <summary>One line on what the dock camera is doing.</summary>
        public string Describe() { return describe != null ? describe() : ""; }

        /// <summary>The connector in use on this ship (Item1) and the dock it's lined up with (Item2); either may be null.</summary>
        public MyTuple<IMyCubeBlock, IMyCubeBlock> Pair(IMyCubeGrid grid)
        {
            return pair != null ? pair(grid) : new MyTuple<IMyCubeBlock, IMyCubeBlock>(null, null);
        }

        /// <summary>
        /// The docking figures for a screen or seat on a ship: HasTarget, Range, Ahead, OffRight, OffUp, Closing (m, m/s),
        /// DriftRight, DriftUp, Misalign (degrees), Safe (m/s), SpeedLevel (0 ok .. 2 too fast), InLockRange, Connectable,
        /// Locked (bool), Mine, Target, TargetGrid, Advice (string). Empty when not loaded.
        /// </summary>
        public Dictionary<string, object> Reading(IMyCubeBlock block)
        {
            return reading != null ? reading(block) : new Dictionary<string, object>();
        }

        /// <summary>
        /// The dock camera's spot for a screen or seat on a ship (Item1 false = no connector): its world matrix (Item2) on the
        /// connector's face looking out, and the grid it's on (Item3, to leave out of a render if you like).
        /// </summary>
        public MyTuple<bool, MatrixD, IMyCubeGrid> CameraFrame(IMyCubeBlock block)
        {
            return cameraFrame != null ? cameraFrame(block) : new MyTuple<bool, MatrixD, IMyCubeGrid>(false, MatrixD.Identity, null);
        }

        /// <summary>The vertical field of view (radians) to render a live feed with, on a w x h view, so the overlays line up.</summary>
        public float LensFovY(float w, float h) { return lensFovY != null ? lensFovY(w, h) : MathHelper.ToRadians(45); }

        /// <summary>Where the Docking Assist's view box sits on a w x h screen: x, y, width, height (put a live feed there).</summary>
        public Vector4 AssistViewBox(float w, float h) { return assistViewBox != null ? assistViewBox(w, h) : new Vector4(0, 0, w, h); }

        /// <summary>A screen's own drawing state (smoothing and blinking); keep one per screen and pass it back each draw.</summary>
        public object NewScreen() { return newScreen != null ? newScreen() : null; }

        /// <summary>
        /// The Docking Assist for "block"'s ship as LCD sprites, added to "sprites". rect = x, y, width, height on the surface.
        /// live = a live feed is drawn in the view box (AssistViewBox), so the drawn view leaves it clear. surface (optional)
        /// measures text exactly; without it, sizes are for HUD pixels.
        /// </summary>
        public void DrawAssist(IMyCubeBlock block, Vector4 rect, bool live, object screen, List<MySprite> sprites, IngameSurface surface = null)
        {
            if (drawAssist != null) drawAssist(block, rect, live, screen, sprites, surface);
        }

        /// <summary>The dock camera's guidance (crosshair, target brackets, approach corridor, readouts) for a feed rendered with LensFovY.</summary>
        public void DrawOverlay(IMyCubeBlock block, Vector4 rect, object screen, List<MySprite> sprites, IngameSurface surface = null)
        {
            if (drawOverlay != null) drawOverlay(block, rect, screen, sprites, surface);
        }

        /// <summary>
        /// Hands over your colours, so docking matches your HUD: Main, Dim, Good, Warn, Bad, Text, Background as
        /// VRageMath.Color (or Vector4 0-1, or "r,g,b"), and Font, NumberFont. null = back to the player's own. Applies while
        /// the player's ThemeSource setting is "auto" (the default).
        /// </summary>
        public void SetTheme(Dictionary<string, object> theme) { if (setTheme != null) setTheme(theme); }

        /// <summary>How far a connector's dock camera is moved from its face (X forward, Y up, Z right, metres).</summary>
        public Vector3D GetOffset(IMyCubeBlock connector) { return getOffset != null ? getOffset(connector) : Vector3D.Zero; }

        /// <summary>Moves (and saves) a connector's dock camera spot: X forward -2..20, Y up and Z right -10..10 metres.</summary>
        public void SetOffset(IMyCubeBlock connector, Vector3D offset) { if (setOffset != null) setOffset(connector, offset); }
    }
}
