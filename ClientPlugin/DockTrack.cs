using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using IngameStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

namespace ProtogenDocking;

/// <summary>
/// Which of a ship's connectors is "yours" and which dock it's heading for, kept up to date: shared by the full-screen
/// dock camera (DockCam) and the "DOCK - Docking Assist" screens, so both show the same pair.
///
/// Works with any connector block, vanilla or modded (everything the game treats as a connector: MyShipConnector),
/// in connector mode. One with the ignore tag ([NoDock]) in its name is never used, as yours or as a dock.
///
/// Picks: your connector with the dock tag ([Dock]) in its name, else one the game says can connect, else the one nearest a connector
/// you could dock with. The target is the connector it can connect to, else the nearest working, free, same-size
/// connector that isn't an enemy's, within 300 m, facing you. Rescans every 1.5 s; screens re-pick your connector every 5 s.
/// </summary>
internal sealed class DockTrack
{
    /// <summary>How far to look for docks (setting TargetRange, 300 m by default).</summary>
    public static double TargetRange => DockConfig.Current.TargetRange;
    /// <summary>Seconds between looking for docks (the quality preset's ScanEvery).</summary>
    public static double Rescan => DockConfig.Current.ScanEvery;
    public const double Refind = 5, FaceOffset = 0.35, Forget = 30;

    public MyCubeGrid Grid;
    public MyShipConnector Mine, Target;
    public readonly List<MyShipConnector> MineList = new List<MyShipConnector>(), Targets = new List<MyShipConnector>();
    public int MineIndex, TargetIndex;
    /// <summary>Screens re-pick your connector now and then; the dock camera keeps the one you chose.</summary>
    public bool AutoPick = true;
    /// <summary>The ship and its mechanical subgrids (rotors, pistons, hinges).</summary>
    public readonly HashSet<long> Group = new HashSet<long>();
    private double nextScan, nextFind, lastUsed;
    private bool lastFreeOnly;
    private MyShipController seat;
    private double seatCheckedAt = -100;

    private static readonly Dictionary<long, DockTrack> tracks = new Dictionary<long, DockTrack>();

    public DockTrack(MyCubeGrid grid) { Grid = grid; }

    /// <summary>The tracker for a screen on this grid: the dock camera's while it's on this ship, else one of its own.</summary>
    public static DockTrack For(MyCubeGrid grid)
    {
        if (grid == null) return null;
        var cam = DockCam.Track;
        if (DockCam.Active && cam != null && (cam.Group.Contains(grid.EntityId) || cam.Grid == grid)) return cam;
        return Own(grid);
    }

    /// <summary>The screens' own tracker for a grid (even while the dock camera is on).</summary>
    public static DockTrack Own(MyCubeGrid grid)
    {
        if (grid == null) return null;
        if (!tracks.TryGetValue(grid.EntityId, out var t) || t.Grid != grid || grid.Closed) tracks[grid.EntityId] = t = new DockTrack(grid);
        t.lastUsed = DockClock.Now;
        t.Tick();
        return t;
    }

    /// <summary>Drops trackers no screen has asked for in a while.</summary>
    public static void Prune()
    {
        if (tracks.Count == 0) return;
        double now = DockClock.Now;
        foreach (var k in tracks.Where(p => now - p.Value.lastUsed > Forget || p.Value.Grid == null || p.Value.Grid.Closed).Select(p => p.Key).ToList()) tracks.Remove(k);
    }

    public static void Reset() => tracks.Clear();

    public void Tick()
    {
        double now = DockClock.Now;
        if (AutoPick && now >= nextFind && (Mine == null || Mine.Closed || !Mine.Connected))
        {
            nextFind = now + Refind;
            var keep = Mine;
            FindMine(false);
            // stay on the same connector unless a better one turns up (one marked [Dock], or one that can connect)
            Mine = keep != null && MineList.Contains(keep) && (Rank(MineList[0]) >= Rank(keep) || (!keep.Connected && MineList[0].Connected))
                ? keep : MineList.FirstOrDefault();
            MineIndex = Math.Max(0, MineList.IndexOf(Mine));
            nextScan = 0;
        }
        if (Mine != null && (Mine.Closed || !Mine.IsFunctional || Ignored(Mine))) { Mine = null; nextFind = 0; AutoPick = true; }
        if (now >= nextScan) Scan();
    }

    // ------------------------------------------------------------------ picking

    /// <summary>Your connectors (connector mode) on the ship and its mechanical subgrids, best first.</summary>
    public void FindMine(bool freeOnly)
    {
        lastFreeOnly = freeOnly;
        MineList.Clear();
        RefreshGroup();
        foreach (var g in GroupGrids())
            foreach (var c in g.GetFatBlocks().OfType<MyShipConnector>())
                if (c.IsFunctional && IsConnectorMode(c) && !Ignored(c) && (!freeOnly || !c.Connected)) MineList.Add(c);
        if (MineList.Count < 2) return;
        // nearest dock to each, read once
        var all = Candidates(Grid.PositionComp.WorldAABB.Center, Grid.PositionComp.WorldVolume.Radius + TargetRange, Group, null);
        double Best(MyShipConnector c)
        {
            var p = c.ConstraintPositionWorld();
            return all.Where(o => o.IsSmallConnector == c.IsSmallConnector).Select(o => Vector3D.Distance(p, o.ConstraintPositionWorld())).DefaultIfEmpty(1e9).Min();
        }
        var sorted = MineList.OrderBy(Rank).ThenBy(Best).ToList();
        MineList.Clear(); MineList.AddRange(sorted);
    }

    /// <summary>
    /// The closest pair of a free connector of yours (on the ship or its subgrids) and a dock it could go to, within range.
    /// Every free connector counts, not only the one picked as yours.
    /// </summary>
    public bool ClosestFree(double range, out MyShipConnector mine, out MyShipConnector dock, out double distance)
    {
        mine = dock = null;
        distance = double.MaxValue;
        if (Group.Count == 0) RefreshGroup();
        var free = new List<MyShipConnector>();
        foreach (var g in GroupGrids())
            foreach (var c in g.GetFatBlocks().OfType<MyShipConnector>())
                if (c.IsWorking && !c.Connected && IsConnectorMode(c) && !Ignored(c)) free.Add(c);
        if (free.Count == 0) return false;
        var docks = Candidates(Grid.PositionComp.WorldAABB.Center, Grid.PositionComp.WorldVolume.Radius + range, new HashSet<long>(Group) { Grid.EntityId }, null);
        foreach (var c in free)
        {
            var p = c.ConstraintPositionWorld();
            foreach (var o in docks)
            {
                if (o.IsSmallConnector != c.IsSmallConnector) continue;
                double d = Vector3D.Distance(p, o.ConstraintPositionWorld());
                if (d < distance) { distance = d; mine = c; dock = o; }
            }
        }
        return mine != null && distance <= range;
    }

    /// <summary>The connectors of yours (ship and subgrids) that are connected right now, into the list.</summary>
    public void ConnectedNow(List<MyShipConnector> into)
    {
        into.Clear();
        if (Group.Count == 0) RefreshGroup();
        foreach (var g in GroupGrids())
            foreach (var c in g.GetFatBlocks().OfType<MyShipConnector>())
                if (c.Connected && !Ignored(c)) into.Add(c);
    }

    /// <summary>Use this connector as yours and that dock as the target (when nobody picked by hand).</summary>
    public void Use(MyShipConnector mine, MyShipConnector dock)
    {
        if (!AutoPick || mine == null) return;
        if (Mine != mine)
        {
            Mine = mine;
            if (!MineList.Contains(mine)) MineList.Insert(0, mine);
            MineIndex = Math.Max(0, MineList.IndexOf(mine));
            nextFind = DockClock.Now + Refind;
            Scan();
        }
        if (dock != null && Target != dock && Targets.Contains(dock))
        {
            Target = dock;
            TargetIndex = Targets.IndexOf(dock);
        }
    }

    /// <summary>0 = the dock tag ([Dock]) in its name, 1 = connected, 2 = can connect, 3 = the rest.</summary>
    private static int Rank(MyShipConnector c)
    {
        if (DockConfig.HasTag(c.DisplayNameText, DockConfig.Current.DockTag)) return 0;
        var st = ((IMyShipConnector)c).Status;
        return st == IngameStatus.Connected ? 1 : st == IngameStatus.Connectable ? 2 : 3;
    }

    /// <summary>The ignore tag ([NoDock]) in its name: never used, as yours or as a dock.</summary>
    public static bool Ignored(MyShipConnector c) => DockConfig.HasTag(c.DisplayNameText, DockConfig.Current.IgnoreTag);

    private void RefreshGroup()
    {
        Group.Clear();
        var list = new List<IMyCubeGrid>();
        try { MyAPIGateway.GridGroups.GetGroup(Grid, GridLinkTypeEnum.Mechanical, list); } catch { }
        foreach (var g in list) Group.Add(g.EntityId);
        Group.Add(Grid.EntityId);
    }

    private IEnumerable<MyCubeGrid> GroupGrids()
    {
        foreach (long id in Group)
            if (MyEntities.TryGetEntityById(id, out var e) && e is MyCubeGrid g && !g.Closed) yield return g;
    }

    /// <summary>Takes another tracker's pair (the dock camera's), so the screens and the camera show the same connectors.</summary>
    public void Follow(DockTrack from)
    {
        if (from?.Mine == null || from == this) return;
        AutoPick = false;
        if (!MineList.Contains(from.Mine)) FindMine(false);
        Mine = from.Mine;
        MineIndex = Math.Max(0, MineList.IndexOf(Mine));
        Scan();
        if (from.Target != null && Targets.Contains(from.Target)) { Target = from.Target; TargetIndex = Targets.IndexOf(Target); }
    }

    /// <summary>
    /// Two directions across a connector's axis along its block's own sides, so a square drawn with them lines up with
    /// the blocks: when both connectors' squares sit square to each other, the ships are rolled the same way.
    /// </summary>
    public static void Across(MyShipConnector c, Vector3D axis, out Vector3D u, out Vector3D v)
    {
        var m = c.WorldMatrix;
        var pick = Math.Abs(Vector3D.Dot(m.Up, axis)) < 0.7 ? m.Up : m.Right;
        u = Vector3D.Normalize(pick - axis * Vector3D.Dot(pick, axis));
        v = Vector3D.Cross(axis, u);
    }

    public void NextMine()
    {
        // read the ship again first: connectors renamed (e.g. tagged [NoDock]), built or removed since the last pick
        FindMine(lastFreeOnly);
        if (MineList.Count == 0) return;
        int at = Mine != null ? MineList.IndexOf(Mine) : -1;
        MineIndex = (at + 1) % MineList.Count;
        Mine = MineList[MineIndex];
        nextScan = 0; Scan();
    }

    public void NextTarget()
    {
        if (Targets.Count < 2) return;
        TargetIndex = (TargetIndex + 1) % Targets.Count;
        Target = Targets[TargetIndex];
    }

    /// <summary>The docks you could connect to from your connector: nearest first, the one the game offers (or holds) on top.</summary>
    public void Scan()
    {
        nextScan = DockClock.Now + Rescan;
        if (Mine == null) { Target = null; Targets.Clear(); return; }
        if (Group.Count == 0) RefreshGroup();
        var keep = Target;
        var exclude = new HashSet<long>(Group) { Mine.CubeGrid.EntityId };
        var at = Mine.ConstraintPositionWorld();
        var found = Candidates(at, TargetRange, exclude, Mine);
        Targets.Clear();
        var offered = ((IMyShipConnector)Mine).OtherConnector as MyShipConnector;
        if (offered != null && !offered.Closed && !Ignored(offered)) Targets.Add(offered);
        Targets.AddRange(found.Where(c => c != offered).OrderBy(c => Score(c, at)));
        // stay on the one picked by hand while it's still there
        if (keep != null && Targets.Contains(keep) && keep != offered) Target = keep;
        else Target = Targets.FirstOrDefault();
        TargetIndex = Math.Max(0, Targets.IndexOf(Target));
    }

    /// <summary>Distance, with a heavy penalty for docks facing away (you'd have to go round).</summary>
    private static double Score(MyShipConnector c, Vector3D from)
    {
        var p = c.ConstraintPositionWorld();
        double d = Vector3D.Distance(p, from);
        return Vector3D.Dot(Axis(c), from - p) < 0 ? d + 1000 : d;
    }

    private static List<MyShipConnector> Candidates(Vector3D centre, double radius, HashSet<long> exclude, MyShipConnector sizeOf)
    {
        var list = new List<MyShipConnector>();
        var sphere = new BoundingSphereD(centre, radius);
        long me = MySession.Static?.LocalPlayerId ?? 0;
        List<IMyEntity> near;
        try { near = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere); } catch { return list; }
        foreach (var e in near)
        {
            if (!(e is MyCubeGrid g) || exclude.Contains(g.EntityId) || g.Physics == null || g.Closed) continue;
            foreach (var c in g.GetFatBlocks().OfType<MyShipConnector>())
            {
                if (!c.IsWorking || c.Connected || !IsConnectorMode(c) || Ignored(c)) continue;
                if (sizeOf != null && c.IsSmallConnector != sizeOf.IsSmallConnector) continue;
                try { if (c.GetUserRelationToOwner(me) == MyRelationsBetweenPlayerAndBlock.Enemies) continue; } catch { }
                if (Vector3D.DistanceSquared(c.ConstraintPositionWorld(), centre) > radius * radius) continue;
                list.Add(c);
            }
        }
        return list;
    }

    // ------------------------------------------------------------------ geometry

    /// <summary>A connector's outward connect axis in world space (the way its face points).</summary>
    /// <remarks>
    /// The game's own ConstraintAxisGridSpace is private: it's the block centre to the connection dummy, cut to its
    /// dominant axis, and ConstraintPositionWorld is that centre plus the same offset. So centre -> constraint point is the axis.
    /// </remarks>
    public static Vector3D Axis(MyShipConnector c)
    {
        var centreLocal = (Vector3D)((Vector3)(c.Max + c.Min) * c.CubeGrid.GridSize * 0.5f);
        var centre = Vector3D.Transform(centreLocal, c.CubeGrid.PositionComp.WorldMatrixRef);
        var a = c.ConstraintPositionWorld() - centre;
        return a.LengthSquared() > 1e-6 ? Vector3D.Normalize(a) : c.WorldMatrix.Forward;
    }

    private static System.Reflection.FieldInfo modeField;
    private static bool modeSearched;

    /// <summary>A connector in connector mode, not an ejector (its private m_connectorMode).</summary>
    public static bool IsConnectorMode(MyShipConnector c)
    {
        if (!modeSearched) { modeSearched = true; modeField = AccessTools.Field(typeof(MyShipConnector), "m_connectorMode"); }
        try { return modeField == null || modeField.GetValue(c)?.ToString() == "Connector"; } catch { return true; }
    }

    /// <summary>
    /// Which way is "up" for the view: the seat you sit in when it's on this ship, else the screen's own cockpit,
    /// else the ship's main cockpit (or any cockpit), else the grid's up.
    /// </summary>
    public Vector3D UpRef(MyCubeBlock screenBlock) => RefMatrix(screenBlock).Up;

    /// <summary>
    /// The seat (or grid) the view is kept upright to: its up, and its forward when the connector looks along its up.
    /// Never a screen's own matrix: a screen block (modded ones especially) can be mounted any way round, which would turn
    /// its picture away from the dock camera's.
    /// </summary>
    public MatrixD RefMatrix(MyCubeBlock screenBlock)
    {
        if (MySession.Static?.ControlledEntity is MyShipController me && !me.Closed && Group.Contains(me.CubeGrid.EntityId)) return me.WorldMatrix;
        if (screenBlock is MyShipController sc) return sc.WorldMatrix;
        double now = DockClock.Now;
        if ((seat == null || seat.Closed) && now - seatCheckedAt > 5)
        {
            seatCheckedAt = now;
            var all = GroupGrids().SelectMany(g => g.GetFatBlocks().OfType<MyCockpit>()).ToList();
            seat = all.FirstOrDefault(c => c.IsMainCockpit) ?? all.FirstOrDefault(c => c.CubeGrid == Grid) ?? all.FirstOrDefault();
        }
        return seat != null && !seat.Closed ? seat.WorldMatrix : Grid.WorldMatrix;
    }

    /// <summary>The view's camera for a screen (or seat): Frame, kept upright the same way as the dock camera's.</summary>
    public bool FrameFor(MyCubeBlock screenBlock, out MatrixD world)
    {
        var m = RefMatrix(screenBlock);
        return Frame(m.Up, m.Forward, out world);
    }

    /// <summary>The camera's world matrix: on your connector's face, looking out, kept upright to "up" (and "fwd" when looking along it).</summary>
    public bool Frame(Vector3D upRef, Vector3D fwdRef, out MatrixD world, double offset = FaceOffset)
    {
        world = MatrixD.Identity;
        if (Mine == null || Mine.Closed) return false;
        var fwd = Axis(Mine);
        // the connector's own camera spot (/dock offset): forward along its axis, then up and right in the view
        var o = Offset(Mine.EntityId);
        var pos = Mine.ConstraintPositionWorld() + fwd * (offset + o.Forward);
        Vector3D up = upRef - fwd * Vector3D.Dot(upRef, fwd);
        if (up.LengthSquared() < 0.05) up = fwdRef - fwd * Vector3D.Dot(fwdRef, fwd);
        if (up.LengthSquared() < 1e-6) up = Vector3D.CalculatePerpendicularVector(fwd);
        world = MatrixD.CreateWorld(pos, fwd, Vector3D.Normalize(up));
        if (o.Up != 0 || o.Right != 0) world.Translation += world.Up * o.Up + world.Right * o.Right;
        world.Translation = Unblocked(Mine.CubeGrid, Mine.ConstraintPositionWorld(), world.Translation);
        return true;
    }

    /// <summary>How far short of an obstacle the camera stops (metres), so it doesn't sit in a surface.</summary>
    private const double ObstacleMargin = 0.25;

    [ThreadStatic] private static List<MyPhysics.HitInfo> hits;

    /// <summary>
    /// The camera spot <paramref name="to"/>, pulled back to just before the first thing in the way along the line from the
    /// connector's face: another grid, an asteroid or a planet. Your own ship (and whatever is joined to it by rotors,
    /// pistons or hinges) doesn't count. Like the game's third-person camera, this keeps the view out of other ships and
    /// rock, so a moved dock camera can't look inside them.
    /// </summary>
    private static Vector3D Unblocked(MyCubeGrid own, Vector3D from, Vector3D to)
    {
        var line = to - from;
        double length = line.Length();
        if (length < 1e-3 || own == null) return to;
        var list = hits ??= new List<MyPhysics.HitInfo>();
        list.Clear();
        try { MyPhysics.CastRay(from, to, list, MyPhysics.CollisionLayers.CollisionLayerWithoutCharacter); }
        catch { return from; }
        double nearest = length;
        foreach (var hit in list)
        {
            var top = (hit.HkHitInfo.GetHitEntity() as MyEntity)?.GetTopMostParent();
            if (top is MyCubeGrid g && (g == own || MyCubeGridGroups.Static.Mechanical.HasSameGroup(g, own))) continue;
            nearest = Math.Min(nearest, Vector3D.Distance(from, hit.Position));
        }
        list.Clear();
        return nearest >= length ? to : from + line / length * Math.Max(0, nearest - ObstacleMargin);
    }

    // ------------------------------------------------------------------ the camera's spot, per connector

    private static string offsetsText;
    private static Dictionary<long, (double Forward, double Up, double Right)> offsets = new Dictionary<long, (double, double, double)>();

    private static Dictionary<long, (double Forward, double Up, double Right)> Offsets()
    {
        string text = DockConfig.Current.DockCamOffsets ?? "";
        if (text == offsetsText) return offsets;
        var map = new Dictionary<long, (double, double, double)>();
        foreach (var item in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var f = item.Split(':');
            // the same limits as /dock offset, also for spots typed into the settings file
            if (f.Length == 4 && long.TryParse(f[0], out long id) && Num(f[1], out double a) && Num(f[2], out double b) && Num(f[3], out double c))
                map[id] = DockSettings.ClampOffset(a, b, c);
        }
        offsetsText = text;
        offsets = map;
        return map;
    }

    private static bool Num(string s, out double v) =>
        double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);

    private static string Fmt(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>How far this connector's dock camera is moved from its face (metres: forward, up, right); 0s = not moved.</summary>
    public static (double Forward, double Up, double Right) Offset(long connectorId) =>
        Offsets().TryGetValue(connectorId, out var o) ? o : (0, 0, 0);

    public static string OffsetText((double Forward, double Up, double Right) o) =>
        Fmt(o.Forward) + " m forward, " + Fmt(o.Up) + " m up, " + Fmt(o.Right) + " m right";

    /// <summary>Saves a connector's camera spot (all 0 = back on its face). Forward -2..20 m, up and right -10..10 m.</summary>
    public static void SetOffset(long connectorId, double forward, double up, double right)
    {
        (forward, up, right) = DockSettings.ClampOffset(forward, up, right);
        var map = new Dictionary<long, (double Forward, double Up, double Right)>(Offsets());
        if (forward == 0 && up == 0 && right == 0) map.Remove(connectorId);
        else map[connectorId] = (forward, up, right);
        string text = string.Join(" ", map.Select(kv => kv.Key + ":" + Fmt(kv.Value.Forward) + ":" + Fmt(kv.Value.Up) + ":" + Fmt(kv.Value.Right)));
        DockConfig.Change(s => s.DockCamOffsets = text);
    }

    /// <summary>The guidance numbers for this frame.</summary>
    public DockReading Read(MatrixD meWorld)
    {
        var me = new DockFrame { Pos = V(meWorld.Translation), Fwd = V(meWorld.Forward), Up = V(meWorld.Up), Right = V(meWorld.Right) };
        DockReading r;
        if (Mine == null) return new DockReading { HasTarget = false };
        if (Target == null || Target.Closed) r = new DockReading { HasTarget = false };
        else
        {
            var tp = Target.ConstraintPositionWorld();
            var mp = Mine.ConstraintPositionWorld();
            Vector3D vMe = Mine.CubeGrid.Physics?.GetVelocityAtPoint(mp) ?? Vector3D.Zero;
            Vector3D vTarget = Target.CubeGrid.Physics?.GetVelocityAtPoint(tp) ?? Vector3D.Zero;
            // the frame sits FaceOffset out from the face; measure from the connection point itself
            me.Pos = V(mp);
            r = DockGuide.Read(me, V(tp), V(Axis(Target)), V(vMe - vTarget));
            r.Target = Name(Target);
            r.TargetGrid = Target.CubeGrid.DisplayName ?? "";
        }
        var st = ((IMyShipConnector)Mine).Status;
        r.Connectable = st == IngameStatus.Connectable;
        r.Locked = st == IngameStatus.Connected;
        r.Mine = Name(Mine);
        return r;
    }

    public static V3 V(Vector3D v) => new V3(v.X, v.Y, v.Z);

    public static string Name(MyShipConnector c) => c == null ? "?" : c.DisplayNameText ?? c.DisplayName ?? "Connector";
}
