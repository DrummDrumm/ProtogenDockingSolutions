using System;
using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using VRageMath;

namespace ProtogenDocking;

/// <summary>
/// Two things done to the seat's move and rotate input while you dock, both on your PC only:
///
/// Steer from the dock camera: while the dock camera's picture is up (full screen, or the PiP, each with its own
/// setting), the input is turned so it is relative to where the camera looks. W flies the way the camera faces, A/D slide
/// left and right in its picture, the mouse turns the ship about the camera's own axes.
///
/// Docking dampener: while docking (the dock camera or PiP up, a dock picked, not yet connected), a key you hold asks
/// for a speed instead of full thrust: up to the speed limit along that way, relative to the dock's ship or station, never
/// pushing harder than the acceleration limit, and holding against gravity on that axis. Only the axes you press are
/// touched: let go and the game's own dampeners (if on) stop the ship as they always do.
///
/// How: a prefix on MyShipController.MoveAndRotate(move, rotation, roll), the call the game makes each frame with your
/// input. The values are changed before the game uses them. Nothing extra is sent: the game syncs these values as
/// always, they just say something else.
/// </summary>
[HarmonyPatch(typeof(MyShipController), nameof(MyShipController.MoveAndRotate), new[] { typeof(Vector3), typeof(Vector2), typeof(float) })]
// before AutoDock's prefix on the same method: an autopilot that replaces the input always has the last word
[HarmonyBefore("AutoDock")]
internal static class DockSteer
{
    // MyShipController.MoveAndRotate(): torque = -(ClampToSphere(rotation / 20, 1), roll * 0.2) in the seat's frame
    private const float RotScale = 20f, RollScale = 0.2f;
    /// <summary>How quickly the dampener closes the gap to the speed asked for, in seconds.</summary>
    private const float Tau = 0.4f;

    /// <summary>The dampener is holding the ship's speed right now.</summary>
    public static bool Damping { get; private set; }

    private static void Prefix(MyShipController __instance, ref Vector3 moveIndicator, ref Vector2 rotationIndicator, ref float rollIndicator)
    {
        if (!ReferenceEquals(MySession.Static?.ControlledEntity, __instance)) return;
        Damping = false;
        try
        {
            if (PlayGuard.Blocked || !(__instance is MyCockpit cockpit) || cockpit.CubeGrid?.Physics == null) return;
            if (CameraFrame(cockpit, out var cam))
            {
                // camera frame -> seat frame (both rotations only)
                Matrix r = (Matrix)(cam * MatrixD.Transpose(cockpit.WorldMatrix)).GetOrientation();
                moveIndicator = Vector3.TransformNormal(moveIndicator, r);
                var v = Vector2.ClampToSphere(rotationIndicator / RotScale, 1f);
                var n = Vector3.TransformNormal(new Vector3(v.X, v.Y, rollIndicator * RollScale), r);
                rotationIndicator = new Vector2(n.X, n.Y) * RotScale;
                rollIndicator = MathHelper.Clamp(n.Z, -1f, 1f) / RollScale;
            }
            Dampen(cockpit, ref moveIndicator);
        }
        catch (Exception e) { Plugin.Log.Throttled("dock steering failed: " + e.Message); }
    }

    /// <summary>The camera whose picture you're looking at, if steering from it is switched on.</summary>
    private static bool CameraFrame(MyCockpit cockpit, out MatrixD world)
    {
        world = MatrixD.Identity;
        var s = DockConfig.Current;
        // full screen: the camera view, or the Docking Assist with the live picture
        // only while docking: back to the seat's own controls once connected, with the view off, or out of range
        var t = Docking(cockpit);
        if (t == null) return false;
        if (DockCam.Showing) return SteerFor(false) && DockCam.FrameOf(cockpit, out world);
        if (!SteerFor(true) || !PipUp) return false;
        return t.FrameFor(cockpit, out world);
    }

    /// <summary>Steering from that view is on: Auto (unless switched off for this dock), else its own toggle.</summary>
    internal static bool SteerFor(bool pip)
    {
        var s = DockConfig.Current;
        if (s.SteerAuto) return !autoOff && (!pip || s.SteerAutoPip);
        return pip ? s.SteerPip : s.SteerFull;
    }

    /// <summary>With Auto: steering switched off by hand for this dock (the key or the toolbar); on again next dock.</summary>
    private static bool autoOff;
    private static bool wasDocking, wasSteering, steeredThisDock;

    /// <summary>You're docking right now (the view up, a dock in range, not connected).</summary>
    internal static bool IsDocking => wasDocking;

    /// <summary>
    /// Every frame from Plugin.Update: notices docking start and end. Auto: a warning on screen when steering from the
    /// camera takes over. Not Auto: the steer toggles switch themselves off when docking ends.
    /// </summary>
    public static void Update()
    {
        try
        {
            var cockpit = MySession.Static?.ControlledEntity as MyCockpit;
            bool docking = cockpit != null && Docking(cockpit) != null;
            var s = DockConfig.Current;
            // steering from the camera takes over right now (the view that's up steers): Auto warns each time
            bool steering = docking && (DockCam.Showing ? SteerFor(false) : PipUp && SteerFor(true));
            if (steering && !wasSteering && s.SteerAuto)
                DockCam.Say("DOCKING - CONTROLS NOW FOLLOW THE DOCK CAMERA: W flies the way it looks." +
                            (string.IsNullOrEmpty(s.SteerKey) ? "" : " " + s.SteerKey + " for the seat's controls."), 5000);
            if (steering) steeredThisDock = true;
            wasSteering = steering;
            if (docking == wasDocking) return;
            wasDocking = docking;
            if (docking) return;
            bool steered = steeredThisDock;
            steeredThisDock = false;
            if (s.SteerAuto)
            {
                if (steered) DockCam.Say("Docking ended: normal cockpit controls.", 3000);
                autoOff = false;
            }
            else if (s.SteerFull || s.SteerPip)
            {
                DockConfig.Change(x => { x.SteerFull = false; x.SteerPip = false; });
                DockCam.Say("Docking ended: steering from the camera off, normal cockpit controls.", 3000);
            }
        }
        catch (Exception e) { Plugin.Log.Throttled("docking start / end failed: " + e.Message); }
    }

    /// <summary>With Auto: the key / toolbar switches steering off or back on for this dock.</summary>
    internal static string ToggleAuto()
    {
        autoOff = !autoOff;
        return autoOff ? "Steering from the camera off for this dock: the seat's controls. Auto again next dock."
                       : "Steering from the camera on: W flies the way the camera looks.";
    }

    /// <summary>Docking: the dock camera or PiP up, a dock in range and picked, not yet connected. The tracked pair, else null.</summary>
    internal static DockTrack Docking(MyCockpit cockpit)
    {
        if (cockpit?.CubeGrid == null || !(DockCam.Active || PipUp) || !DockPip.InRange) return null;
        var t = DockTrack.For(cockpit.CubeGrid);
        return t?.Mine == null || t.Target == null || t.Target.Closed || t.Mine.Connected ? null : t;
    }

    internal static bool PipUp => DockPip.Preview == null && DockPip.Shown.Width > 0;

    private static void Dampen(MyCockpit cockpit, ref Vector3 move)
    {
        var s = DockConfig.Current;
        if (!s.Damp || !cockpit.ControlThrusters) return;
        // only the axes you're pressing; the others stay at 0, so the game's own dampeners (if on) work them as always
        if (move.LengthSquared() < 1e-4f) return;
        // within the pop-up range of a dock (a view up or not), not paused after undocking (DampUndock); the pair
        // picked for the dock camera / PiP while docking, else the closest one
        if (!DockPip.DampInRange || DockPip.DampPaused) return;
        var t = Docking(cockpit);
        var mineC = t?.Mine ?? DockPip.DampMine;
        var dockC = t?.Target ?? DockPip.DampDock;
        if (mineC == null || dockC == null || mineC.Closed || dockC.Closed || mineC.Connected) return;
        var grid = cockpit.CubeGrid;
        var thrust = grid.EntityThrustComponent;
        if (thrust == null) return;
        float mass = cockpit.CalculateShipMass().PhysicalMass;
        if (mass <= 0) return;

        // speeds relative to the dock's ship or station
        Vector3 refVel = dockC.CubeGrid?.Physics?.LinearVelocity ?? Vector3.Zero;
        Vector3 vel = grid.Physics.LinearVelocity - refVel;
        Vector3 gravity = MyGravityProviderSystem.CalculateTotalGravityInPoint(grid.Physics.CenterOfMassWorld);

        // the limits: yours, or with Auto, tighter as the dock gets closer (the assist's safe speed for the range)
        float speedLimit = s.DampSpeed, accelLimit = s.DampAccel;
        if (s.DampAuto)
        {
            double range = Vector3D.Distance(mineC.ConstraintPositionWorld(), dockC.ConstraintPositionWorld());
            speedLimit = (float)DockGuide.AutoSpeed(range, speedLimit);
            accelLimit = (float)DockGuide.AutoAccel(range, accelLimit);
        }

        // the speed asked for, in the seat's frame: held = up to the limit along that way
        Vector3 want = move * speedLimit;
        if (want.Length() > speedLimit) want = Vector3.Normalize(want) * speedLimit;
        var seatWorld = (Matrix)cockpit.WorldMatrix.GetOrientation();
        var toSeat = Matrix.Transpose(seatWorld);
        Vector3 v = Vector3.TransformNormal(vel, toSeat);
        Vector3 g = Vector3.TransformNormal(gravity, toSeat);
        cockpit.Orientation.GetMatrix(out Matrix seatInGrid);
        // the game's own dampeners still work a pressed axis, at (1 - how hard it's pressed); what they push is allowed
        // for below. They stop you against the relative dampening target (if one is set), else against space.
        bool gameDamp = thrust.DampenersEnabled;
        Vector3 dampRef = cockpit.RelativeDampeningEntity?.Physics?.LinearVelocity ?? Vector3.Zero;
        Vector3 dv = Vector3.TransformNormal(dampRef - grid.Physics.LinearVelocity, toSeat);

        var result = Vector3.Zero;
        for (int i = 0; i < 3; i++)
        {
            float input = move.GetDim(i);
            if (Math.Abs(input) < 0.01f) continue;
            float vi = v.GetDim(i);
            float accel = (want.GetDim(i) - vi) / Tau;
            // going faster than the limit (it came on while you were faster): back to the limit with all the thrust
            // there is; the acceleration limit applies from there on
            // (well under the assist's red SLOW DOWN at 1.6 x the safe speed)
            bool over = Math.Abs(vi) > speedLimit * DockGuide.OverFactor + 0.05f;
            if (!over) accel = MathHelper.Clamp(accel, -accelLimit, accelLimit);
            float force = (accel - g.GetDim(i)) * mass;
            // the thrust there is each way along this axis
            var axis = Vector3.Zero;
            axis.SetDim(i, 1);
            float plus = PushThrust(thrust, Base6Directions.GetDirection(Vector3.TransformNormal(axis, seatInGrid)));
            float minus = PushThrust(thrust, Base6Directions.GetDirection(Vector3.TransformNormal(-axis, seatInGrid)));
            // what the game's dampeners push at 0 input (MyEntityThrustComponent.ComputeBaseThrust:
            // (target - (v + g/2)) / 0.5 * mass, scaled by 1 - |input|)
            float d = gameDamp ? (dv.GetDim(i) / 0.5f - g.GetDim(i)) * mass : 0;
            // input c gives c * thrust + (1 - |c|) * d; solve for the push wanted
            float c = force >= d ? (plus - d > 0 ? (force - d) / (plus - d) : 1)
                                 : (minus + d > 0 ? (force - d) / (minus + d) : -1);
            result.SetDim(i, MathHelper.Clamp(c, -1f, 1f));
        }
        move = result;
        Damping = true;
    }

    /// <summary>
    /// The thrust there is to push the grid towards dir. GetMaxThrustInDirection's Right/Left/Up/Down are the way the
    /// grid is pushed, but its Forward is the thrust pushing towards +Z (backwards) and its Backward the thrust pushing
    /// towards -Z (forwards): MyEntityThrustComponent returns TotalMaxPositiveThrust.Z for Forward.
    /// </summary>
    private static float PushThrust(MyEntityThrustComponent thrust, Base6Directions.Direction dir)
    {
        if (dir == Base6Directions.Direction.Forward) dir = Base6Directions.Direction.Backward;
        else if (dir == Base6Directions.Direction.Backward) dir = Base6Directions.Direction.Forward;
        return thrust.GetMaxThrustInDirection(dir);
    }

}
