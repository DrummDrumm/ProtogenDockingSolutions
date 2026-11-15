using System;
using System.Drawing;
using System.IO;
using ProtogenDocking;

internal static partial class Program
{
    /// <summary>Pictures of each Docking Assist style: full screen, PiP and a square LCD, over a sample camera picture.</summary>
    private static void StyleTests(string root)
    {
        var me = new DockFrame { Pos = new V3(0, 0, 0), Fwd = new V3(0, 0, 1), Up = new V3(0, 1, 0), Right = new V3(1, 0, 0) };
        var far = DockGuide.Read(me, new V3(-0.28, 0.19, 42.6), new V3(0, 0, -1), new V3(0, 0, 1.8));
        var final = DockGuide.Read(me, new V3(0.15, 0.05, 2.2), new V3(0, 0, -1), new V3(0, 0, 0.6));
        string feedFile = Path.Combine(root, "Tests", "feed-sample.jpg");
        using var feed = File.Exists(feedFile) ? Image.FromFile(feedFile) : null;

        foreach (var style in DockStyles.Names)
        {
            if (style == "classic") continue;
            foreach (var (W, H, full, tag, rd) in new[] {
                         (1920, 1080, true, "full", far), (640, 360, false, "pip", far), (512, 512, false, "lcd", far),
                         (512, 512, false, "lcd-final", final), (512, 256, false, "couch-nodock", (DockReading)null) })
            {
                string name = "preview-" + style + "-" + tag + ".png";
                try
                {
                    using var bmp = new Bitmap(W, H);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        GdiCanvas.Setup(g);
                        g.Clear(Color.Black);
                        var s = new DockScreen { Time = 12.3, Synthetic = true, Full = full, Style = style, LiveView = feed != null, Title = "DOCKING ASSIST" };
                        DockStyles.Layout(style, s, W, H);
                        if (feed != null) Cover(g, feed, new RectangleF(s.ViewX, s.ViewY, s.ViewW, s.ViewH));
                        var th = DockStyles.Palette(style, new DockTheme());
                        var gc = new GdiCanvas { G = g, Theme = th };
                        var rr = rd ?? new DockReading { HasTarget = false, Mine = "Connector Nose [Dock]" };
                        if (rd != null) Scene(s, rd, me);
                        DockStyles.Draw(style, gc, th, rr, s, W, H);
                    }
                    bmp.Save(Path.Combine(root, "Tests", name));
                    Check(true, "style " + style + ": picture -> Tests/" + name);
                }
                catch (Exception x) { Check(false, "style " + style + " " + tag + ": picture failed: " + x.Message); }
            }
        }

        var rec = new RecordingCanvas();
        var sn = new DockScreen { Time = 1, Full = true, Style = "holo" };
        DockStyles.Layout("holo", sn, 1920, 1080);
        Check(sn.ViewW > 1920 * 0.7f && sn.ViewH > 1080 * 0.8f, "holo: full-screen picture is most of the screen (" + sn.ViewW + " x " + sn.ViewH + ")");
        DockStyles.Draw("holo", rec, HoloStyle.Palette, new DockReading { HasTarget = false, Mine = "A" }, sn, 1920, 1080);
        Check(rec.Texts.Exists(t => t.Contains("NO DOCK")), "holo: no dock in range says so");
        var sc = new DockScreen { Style = "holo" };
        DockStyles.Layout("holo", sc, 512, 512);
        Check(sc.ViewW == 512 && sc.ViewH == 512, "holo: on screens the picture fills the screen");

        foreach (var st in new[] { "tactical", "bracket", "glass", "radial" })
        {
            var sf = new DockScreen { Time = 1, Full = true, Style = st };
            DockStyles.Layout(st, sf, 1920, 1080);
            Check(sf.ViewW > 1920 * 0.8f && sf.ViewH > 1080 * 0.7f, st + ": full-screen picture is most of the screen (" + sf.ViewW + " x " + sf.ViewH + ")");
            var rc = new RecordingCanvas();
            DockStyles.Draw(st, rc, DockStyles.Palette(st, new DockTheme()), new DockReading { HasTarget = false, Mine = "A" }, sf, 1920, 1080);
            Check(rc.Texts.Exists(x => x.Contains("NO DOCK")), st + ": no dock in range says so");
            var sl = new DockScreen { Style = st };
            DockStyles.Layout(st, sl, 512, 512);
            Check(sl.ViewW == 512 && sl.ViewH == 512, st + ": on screens the picture fills the screen");
            Check(DockStyles.Norm(st) == st, st + ": is a style name");
        }
        // per-place looks: own (Custom Data) > place setting > AssistStyle
        var ps = new DockSettings { AssistStyle = "tactical", PipStyle = "glass", LcdStyle = "radial" };
        Check(DockStyles.For(ps, "full", null) == "tactical" && DockStyles.For(ps, "pip", "") == "glass" && DockStyles.For(ps, "lcd", null) == "radial"
              && DockStyles.For(ps, "lcd", "bracket") == "bracket" && DockStyles.For(ps, "", null) == "tactical" && DockStyles.For(ps, "lcd", "nonsense") == "radial",
            "style: a screen's own look beats its place's, which beats AssistStyle");
        var back = DockSettings.Parse(ps.Write());
        Check(back.AssistStyle == "tactical" && back.FullStyle == "" && back.PipStyle == "glass" && back.LcdStyle == "radial", "style: place looks are saved and read back");
        Check(DockSettings.Parse("PipStyle=junk").PipStyle == "" && DockSettings.Parse("FullStyle=Bracket").FullStyle == "bracket", "style: a place look that's no style follows AssistStyle");
        Check(DockStyles.FromCustomData("[Dock]\nDockStyle=Radial\n") == "radial" && DockStyles.FromCustomData("dock style : glass") == "glass"
              && DockStyles.FromCustomData("Something=1\r\nDock_Style= tac \r\n") == "tactical" && DockStyles.FromCustomData("DockStyle=nope") == null
              && DockStyles.FromCustomData("") == null && DockStyles.FromCustomData("no style here") == null,
            "style: an LCD's Custom Data DockStyle= line is read");
        Check(DockStyles.Next("holo") == "tactical" && DockStyles.Next("classic") == "holo", "style: next cycles holo > tactical > bracket > glass > radial > classic");
    }

    private static void Cover(Graphics g, Image img, RectangleF box)
    {
        float s = Math.Max(box.Width / img.Width, box.Height / img.Height);
        float sw = box.Width / s, sh = box.Height / s;
        var src = new RectangleF((img.Width - sw) / 2, (img.Height - sh) / 2, sw, sh);
        g.DrawImage(img, box, src, GraphicsUnit.Pixel);
    }

    /// <summary>The dock's outline, face, tunnel and your square on the screen's lens (as DockScreens does in the game).</summary>
    private static void Scene(DockScreen s, DockReading rd, DockFrame me)
    {
        var l = new DockLens(me, s.ViewW, s.ViewH) { OffsetX = s.ViewX, OffsetY = s.ViewY };
        rd.Mine = "Connector Nose [Dock]"; rd.Target = "Docking Port 3"; rd.TargetGrid = "Tycho Yard";
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
        double ra = 0.8 * Math.PI / 180, h2 = 0.3;
        var mo = new V3(me.Pos.X, me.Pos.Y, me.Pos.Z + 2);
        var mu = new V3(Math.Cos(ra), Math.Sin(ra), 0); var mv = new V3(-Math.Sin(ra), Math.Cos(ra), 0);
        var mq = new[] { mo + (mu + mv) * h2, mo + (mu - mv) * h2, mo - (mu + mv) * h2, mo - (mu - mv) * h2 };
        for (int i = 0; i < 4; i++) if (l.Segment(mq[i], mq[(i + 1) % 4], out var m3)) s.Edges.Add((m3.X1, m3.Y1, m3.X2, m3.Y2, 3));
        s.Roll = 0.8;
        s.Target = l.Point(tp);
        if (s.Target == null) s.OffScreen = l.Direction(tp);
    }
}
