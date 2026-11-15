using System;
using System.Drawing;
using System.IO;
using ProtogenDocking;

internal static partial class Program
{
    private static void DockTests(string root)
    {
        // your connector at the origin looking down +Z (right +X, up +Y); a dock 40 m ahead, 2 m right, 1 m low, facing you
        var me = new DockFrame { Pos = new V3(0, 0, 0), Fwd = new V3(0, 0, 1), Up = new V3(0, 1, 0), Right = new V3(1, 0, 0) };
        var r = DockGuide.Read(me, new V3(2, -1, 40), new V3(0, 0, -1), new V3(0.5, 0, 3));
        Check(Math.Abs(r.Range - Math.Sqrt(4 + 1 + 1600)) < 1e-6, "dock: range between connection points");
        Check(Math.Abs(r.OffRight - 2) < 1e-9 && Math.Abs(r.OffUp + 1) < 1e-9 && Math.Abs(r.Ahead - 40) < 1e-9, "dock: offset right 2, up -1, ahead 40");
        Check(r.Closing > 2.9 && r.Closing < 3.1, "dock: closing speed along the line (" + r.Closing.ToString("0.00") + ")");
        Check(Math.Abs(r.DriftRight - 0.5) < 1e-9, "dock: sideways drift");
        Check(r.Misalign < 1e-6, "dock: face to face = 0° off");
        Check(r.SpeedLevel == 0 && !r.InLockRange, "dock: 3 m/s at 40 m is fine, not in lock range");
        Check(DockGuide.Advice(r) == "SLIDE RIGHT 2.0 M, DOWN 1.0 M", "dock: advice slides onto the axis (" + DockGuide.Advice(r) + ")");

        // too fast close in
        var fast = DockGuide.Read(me, new V3(0, 0, 5), new V3(0, 0, -1), new V3(0, 0, 4));
        Check(fast.SpeedLevel == 2 && DockGuide.Advice(fast) == "SLOW DOWN", "dock: 4 m/s at 5 m says SLOW DOWN");

        // turned 90° away, far out
        var turned = DockGuide.Read(me, new V3(0, 0, 60), new V3(1, 0, 0), new V3(0, 0, 0));
        Check(Math.Abs(turned.Misalign - 90) < 1e-6 && DockGuide.Advice(turned) == "TURN TO FACE", "dock: 90° off far out says TURN TO FACE");

        // the game's lock test: within 0.59 m and 45°
        var close = DockGuide.Read(me, new V3(0.1, 0, 0.4), new V3(Math.Sin(0.5), 0, -Math.Cos(0.5)), new V3(0, 0, 0.2));
        Check(close.InLockRange && DockGuide.Advice(close) == "READY - LOCK", "dock: 0.41 m and 29° off is in lock range");
        var skew = DockGuide.Read(me, new V3(0, 0, 0.4), new V3(Math.Sin(1.0), 0, -Math.Cos(1.0)), new V3(0, 0, 0));
        Check(!skew.InLockRange, "dock: 57° off is not in lock range");

        // safe speed curve
        Check(Math.Abs(DockGuide.SafeSpeed(200) - 10) < 1e-9 && DockGuide.SafeSpeed(0.2) == 0.5 && DockGuide.SafeSpeed(20) > 2 && DockGuide.SafeSpeed(20) < 2.5, "dock: safe speed 10 m/s far, ~2 at 20 m, 0.5 at the end");
        Check(DockGuide.Advice(null) == "NO DOCK IN RANGE", "dock: no target");

        // picture: the overlay over a dark scene, a dock off to the right and low
        string png = Path.Combine(root, "Tests", "preview-dockcam.png");
        try
        {
            const int W = 1920, H = 1080;
            using var bmp = new Bitmap(W, H);
            using (var g = Graphics.FromImage(bmp))
            {
                GdiCanvas.Setup(g);
                g.Clear(Color.FromArgb(18, 20, 26));
                // a stand-in station face behind the overlay
                using (var b = new SolidBrush(Color.FromArgb(52, 56, 64))) g.FillRectangle(b, 980, 470, 380, 260);
                using (var b = new SolidBrush(Color.FromArgb(30, 32, 38))) g.FillEllipse(b, 1110, 560, 120, 120);
                foreach (var th in new[] { new DockTheme() })
                {
                    var gc = new GdiCanvas { G = g, Theme = th };
                    r.Mine = "Connector Nose [Dock]"; r.Target = "Docking Port 3"; r.TargetGrid = "Tycho Yard";
                    var s = new DockScreen { Time = 3723.4, Target = (1170, 620) };
                    s.Corridor.Add((1166, 616, 2)); s.Corridor.Add((1160, 610, 5)); s.Corridor.Add((1150, 600, 10)); s.Corridor.Add((1130, 585, 20));
                    DockCamRenderer.Draw(gc, th, r, s, W, H);
                }
            }
            bmp.Save(png);
            Check(true, "dock: picture -> Tests/preview-dockcam.png");
        }
        catch (Exception x) { Check(false, "dock: picture failed: " + x.Message); }

        // the screens' lens: straight ahead is the middle, right is right, up is up; lines behind are cut at the near plane
        var lens = new DockLens(me, 512, 256);
        var mid = lens.Point(new V3(0, 0, 10));
        Check(mid.HasValue && Math.Abs(mid.Value.X - 256) < 1e-3 && Math.Abs(mid.Value.Y - 128) < 1e-3, "dock lens: ahead = screen centre");
        var ur = lens.Point(new V3(1, 1, 10));
        Check(ur.HasValue && ur.Value.X > 256 && ur.Value.Y < 128, "dock lens: right and up = right and up on screen");
        Check(lens.Point(new V3(0, 0, -5)) == null, "dock lens: behind = not drawn");
        Check(lens.Segment(new V3(0, 0, -5), new V3(0, 0, 20), out var seg) && Math.Abs(seg.X1 - 256) < 1 && Math.Abs(seg.X2 - 256) < 1, "dock lens: a line through the camera is cut, not flipped");
        Check(!lens.Segment(new V3(0, 0, -5), new V3(1, 0, -9), out _), "dock lens: a line wholly behind is skipped");
        var dir = lens.Direction(new V3(-3, 0, -10));
        Check(dir.X < -0.99, "dock lens: off-screen arrow points left for a dock behind-left");

        // the docking phases for the badge
        Check(DockGuide.Phase(r).Name == "LINE UP" && DockGuide.Phase(fast).Name == "TOO FAST" && DockGuide.Phase(turned).Name == "TURN"
              && DockGuide.Phase(close).Name == "LOCK READY" && DockGuide.Phase(null).Name == "NO DOCK", "dock assist: phases (line up, too fast, turn, lock ready, no dock)");
        Check(Math.Abs(DockGuide.TimeToContact(r) - r.Range / r.Closing) < 1e-9 && DockGuide.TimeToContact(turned) < 0, "dock assist: time to contact, none when not closing");

        // pictures of the Docking Assist: a wide crash-couch screen and a square LCD, lining up far out and on final approach
        var final = DockGuide.Read(me, new V3(0.15, 0.05, 2.2), new V3(0, 0, -1), new V3(0, 0, 0.6));
        foreach (var (W, H, name, rd) in new[] { (512, 248, "preview-dockassist-couch.png", r), (512, 512, "preview-dockassist-lcd.png", r),
                                                 (512, 248, "preview-dockassist-couch-final.png", final), (512, 512, "preview-dockassist-lcd-nodock.png", (DockReading)null) })
        {
            string file = Path.Combine(root, "Tests", name);
            try
            {
                using var bmp = new Bitmap(W, H);
                using (var g = Graphics.FromImage(bmp))
                {
                    GdiCanvas.Setup(g);
                    g.Clear(Color.Black);
                    var th = new DockTheme();
                    var gc = new GdiCanvas { G = g, Theme = th };
                    float k = Math.Max(0.35f, Math.Min(H / 360f, W / 500f));
                    var s = new DockScreen { Time = 12.3, Synthetic = true, Scale = k, Compact = W / k < 1100, Title = "DOCKING ASSIST" };
                    DockAssistRenderer.Layout(s, W, H);
                    var l = new DockLens(me, s.ViewW, s.ViewH) { OffsetX = s.ViewX, OffsetY = s.ViewY };
                    var rr = rd ?? new DockReading { HasTarget = false, Mine = "Connector Nose [Dock]" };
                    if (rd != null)
                    {
                        rd.Mine = "Connector Nose [Dock]"; rd.Target = "Docking Port 3"; rd.TargetGrid = "Tycho Yard";
                        // the dock: 2 m right, 1 m low (or nearly dead ahead on final), facing back down -Z
                        var tp = new V3(me.Pos.X + rd.OffRight, me.Pos.Y + rd.OffUp, rd.Ahead);
                        var lo = new V3(tp.X - 15, tp.Y - 10, tp.Z); var hi = new V3(tp.X + 15, tp.Y + 10, tp.Z + 40);
                        var c8 = new V3[8];
                        for (int i = 0; i < 8; i++) c8[i] = new V3((i & 1) == 0 ? lo.X : hi.X, (i & 2) == 0 ? lo.Y : hi.Y, (i & 4) == 0 ? lo.Z : hi.Z);
                        for (int i = 0; i < 8; i++)
                            for (int bit = 1; bit < 8; bit <<= 1)
                                if ((i & bit) == 0 && l.Segment(c8[i], c8[i | bit], out var e)) s.Edges.Add((e.X1, e.Y1, e.X2, e.Y2, 0));
                        double f = 1;
                        foreach (var (a, b) in new[] { (new V3(-f, -f, 0), new V3(f, -f, 0)), (new V3(f, -f, 0), new V3(f, f, 0)), (new V3(f, f, 0), new V3(-f, f, 0)), (new V3(-f, f, 0), new V3(-f, -f, 0)) })
                            if (l.Segment(tp + a, tp + b, out var e)) s.Edges.Add((e.X1, e.Y1, e.X2, e.Y2, 1));
                        V3[] prev = null;
                        foreach (double d in DockGuide.CorridorMarks)
                        {
                            double gh = DockGuide.GateHalf(2.5, d);
                            var o = new V3(tp.X, tp.Y, tp.Z - d);
                            var q = new[] { o + new V3(gh, gh, 0), o + new V3(gh, -gh, 0), o + new V3(-gh, -gh, 0), o + new V3(-gh, gh, 0) };
                            for (int i = 0; i < 4; i++)
                            {
                                if (l.Segment(q[i], q[(i + 1) % 4], out var e)) s.Tunnel.Add((e.X1, e.Y1, e.X2, e.Y2, d, false));
                                if (prev != null && l.Segment(prev[i], q[i], out var e2)) s.Tunnel.Add((e2.X1, e2.Y1, e2.X2, e2.Y2, d, true));
                            }
                            prev = q;
                        }
                        // your connector's square, rolled 6 degrees from the dock's (amber, ROLL 6)
                        double ra = 6 * Math.PI / 180, h2 = 0.3;
                        var mo = new V3(me.Pos.X, me.Pos.Y, me.Pos.Z + 2);
                        var mu = new V3(Math.Cos(ra), Math.Sin(ra), 0); var mv = new V3(-Math.Sin(ra), Math.Cos(ra), 0);
                        var mq = new[] { mo + (mu + mv) * h2, mo + (mu - mv) * h2, mo - (mu + mv) * h2, mo - (mu - mv) * h2 };
                        for (int i = 0; i < 4; i++) if (l.Segment(mq[i], mq[(i + 1) % 4], out var me3)) s.Edges.Add((me3.X1, me3.Y1, me3.X2, me3.Y2, 3));
                        s.Roll = 6;
                        s.Target = l.Point(tp);
                        if (s.Target == null) s.OffScreen = l.Direction(tp);
                    }
                    DockAssistRenderer.Draw(gc, th, rr, s, W, H);
                }
                bmp.Save(file);
                Check(true, "dock assist: screen picture -> Tests/" + name);
            }
            catch (Exception x) { Check(false, "dock assist: screen picture failed: " + x.Message); }
        }

    }
}
