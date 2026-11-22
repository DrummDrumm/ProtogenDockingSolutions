using System;
using System.Globalization;
using System.Text;

namespace ProtogenDocking;

/// <summary>Docking Solutions' settings (Storage\ProtogenDockingSolutions\DockingSolutions.ini, "Key=Value" lines).</summary>
public sealed class DockSettings
{
    /// <summary>The dock camera's spot per connector: "connectorId:forward:up:right" (metres), space-separated.</summary>
    public string DockCamOffsets = "";

    /// <summary>How far the dock camera may be moved from its connector's face: forward -2..20 m, up and right -10..10 m.</summary>
    public const double OffsetBackMax = 2, OffsetForwardMax = 20, OffsetSideMax = 10;

    /// <summary>A camera spot within <see cref="OffsetForwardMax"/> and the other limits; numbers that aren't finite count as 0.</summary>
    public static (double Forward, double Up, double Right) ClampOffset(double forward, double up, double right)
    {
        static double Clamp(double v, double min, double max) => double.IsNaN(v) || double.IsInfinity(v) ? 0 : Math.Max(min, Math.Min(max, v));
        return (Clamp(forward, -OffsetBackMax, OffsetForwardMax), Clamp(up, -OffsetSideMax, OffsetSideMax), Clamp(right, -OffsetSideMax, OffsetSideMax));
    }
    /// <summary>A connector with this in its name is picked first as yours.</summary>
    public string DockTag = "[Dock]";
    /// <summary>A connector with this in its name is never used (neither as yours nor as a dock).</summary>
    public string IgnoreTag = "[NoDock]";
    /// <summary>How far to look for docks (m).</summary>
    public double TargetRange = 300;
    /// <summary>"auto": another plugin's theme when one hands it over, else this file's.</summary>
    public string ThemeSource = "auto";
    public DockTheme Theme = new DockTheme();

    /// <summary>The quality preset (DockQuality), or "custom" when the values below were set one by one.</summary>
    public string Quality = "balanced";
    public int PipFps = 30, PipRes = 450, LcdEvery = 1;
    public bool PipEffects;
    public double ScanEvery = 1.5;
    /// <summary>The live dock camera picture inside Docking Assist LCDs: on/off, pictures a second, tallest height (px).</summary>
    public bool LcdFeed = true;
    public int LcdFps = 10, LcdRes = 270;

    /// <summary>The picture-in-picture dock camera: on, its corner, margins from it (% of the screen), height (% of the screen) and width / height.</summary>
    public bool PipOn;
    public string PipCorner = "tr";
    public float PipX = 1.5f, PipY = 12f, PipSize = 28f, PipAspect = 16f / 9;
    /// <summary>The dock camera's guidance (crosshair, brackets, readouts) over the PiP.</summary>
    public bool PipOverlay = true;
    /// <summary>What the PiP shows: "cam" = the dock camera picture with its guidance; "assist" = the Docking Assist
    /// (gauges, scope, next step) with the live picture in its view box.</summary>
    public string PipMode = "cam";
    /// <summary>The PiP comes up by itself while the full-screen dock camera is paused in third person.</summary>
    public bool PipAuto = true;
    /// <summary>The PiP comes up by itself while your connector is within PipNearRange metres of a dock (and not connected).</summary>
    public bool PipNear;
    public float PipNearRange = 50;
    /// <summary>
    /// Seconds after docking (your connector connects) before the dock camera and PiP switch off: 0 = straight away,
    /// up to DockedOffMax; -1 = never, they stay up on the docked pair. LCD feeds aren't affected.
    /// </summary>
    public float DockedOff = 3;
    public const float DockedOffMax = 60;
    /// <summary>What the full-screen dock camera shows: "cam" = the camera view with its guidance; "assist" = the
    /// Docking Assist across the screen with the live picture in its view box.</summary>
    public string CamMode = "cam";
    /// <summary>While the full-screen dock camera is up, your move and rotate input is relative to where it looks.</summary>
    public bool SteerFull;
    /// <summary>While the PiP is up, your move and rotate input is relative to where its camera looks.</summary>
    public bool SteerPip;
    /// <summary>A key that switches steering from the camera for the view that's up, e.g. "Ctrl+Alt+K" ("" = none).</summary>
    public string SteerKey = "";
    /// <summary>
    /// Steering from the camera switches itself on as soon as you're docking (a dock in range with the view up) and off
    /// when docking ends, with a warning on screen. Off: SteerFull / SteerPip are switched on by hand and switch
    /// themselves off when docking ends.
    /// </summary>
    public bool SteerAuto;
    /// <summary>With SteerAuto: the PiP steers by itself too (off = Auto only in the full-screen dock camera).</summary>
    public bool SteerAutoPip = true;
    /// <summary>Docking dampener: while docking, a key you hold asks for a speed (up to DampSpeed) and never pushes harder
    /// than DampAccel, relative to the dock's ship or station. Keys you don't press are left to the game.</summary>
    public bool Damp;
    public float DampSpeed = 5, DampAccel = 2;
    /// <summary>The dampener's limits come down as the dock gets closer, to the safe speed the assist advises.</summary>
    public bool DampAuto;
    /// <summary>
    /// After one of your connectors lets go (undocking), the dampener stays off for this many seconds (0 = no pause, up
    /// to DampUndockMax); -1 = until you're out of range of every dock, so it only works on the way in.
    /// </summary>
    public float DampUndock;
    public const float DampUndockMax = 120;
    public const float DampSpeedMin = 0.5f, DampSpeedMax = 200, DampAccelMin = 0.1f, DampAccelMax = 50;
    /// <summary>The Docking Assist's look (DockStyles.Names): holo, ... or classic.</summary>
    public string AssistStyle = DockStyles.Default;
    /// <summary>The look in one place (full-screen dock camera, PiP, LCDs); "" = AssistStyle.</summary>
    public string FullStyle = "", PipStyle = "", LcdStyle = "";

    /// <summary>The PiP pop-up range's limits, in metres.</summary>
    public const float NearMin = 5, NearMax = 300;

    public static string NormPipMode(string v)
    {
        switch ((v ?? "").Trim().ToLowerInvariant())
        {
            case "cam": case "camera": case "dockcam": return "cam";
            case "assist": case "dock": case "docking": return "assist";
            default: return null;
        }
    }

    /// <summary>The PiP window's place and shape back to the defaults.</summary>
    public void ResetPipLayout()
    {
        var d = new DockSettings();
        PipCorner = d.PipCorner; PipX = d.PipX; PipY = d.PipY; PipSize = d.PipSize; PipAspect = d.PipAspect;
    }

    public void CopyPipLayout(DockSettings from)
    {
        PipCorner = from.PipCorner; PipX = from.PipX; PipY = from.PipY; PipSize = from.PipSize; PipAspect = from.PipAspect;
    }

    public DockSettings Copy()
    {
        var s = (DockSettings)MemberwiseClone();
        s.Theme = Theme.Copy();
        return s;
    }

    public string Write()
    {
        var sb = new StringBuilder();
        sb.AppendLine("; Protogen Systems Docking Solutions. Edit in game with /dock, or here (reloaded when saved).");
        sb.AppendLine("DockCamOffsets=" + DockCamOffsets);
        sb.AppendLine("DockTag=" + DockTag);
        sb.AppendLine("IgnoreTag=" + IgnoreTag);
        sb.AppendLine("TargetRange=" + TargetRange.ToString("0", CultureInfo.InvariantCulture));
        sb.AppendLine("; ThemeSource=auto uses another plugin's theme when one hands it over; file = always the colours below");
        sb.AppendLine("ThemeSource=" + ThemeSource);
        foreach (var n in DockTheme.ColorNames) sb.AppendLine("Theme" + n + "=" + Theme.Get(n));
        sb.AppendLine("ThemeFont=" + Theme.Font);
        sb.AppendLine("ThemeNumberFont=" + Theme.NumberFont);
        sb.AppendLine("; Quality = performance | balanced | quality | ultra (sets the values below) | custom (uses them as written)");
        sb.AppendLine("Quality=" + Quality);
        sb.AppendLine("PipFps=" + PipFps.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("PipRes=" + PipRes.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("PipEffects=" + (PipEffects ? "on" : "off"));
        sb.AppendLine("LcdEvery=" + LcdEvery.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("ScanEvery=" + ScanEvery.ToString("0.##", CultureInfo.InvariantCulture));
        sb.AppendLine("LcdFps=" + LcdFps.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("LcdRes=" + LcdRes.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("; LcdFeed = the live dock camera picture inside Docking Assist LCDs (off = the drawn view only)");
        sb.AppendLine("LcdFeed=" + (LcdFeed ? "on" : "off"));
        sb.AppendLine("; Picture-in-picture dock camera: corner tl|tr|bl|br, margins and size in % of the screen, aspect = width / height");
        sb.AppendLine("PipOn=" + (PipOn ? "on" : "off"));
        sb.AppendLine("PipCorner=" + PipCorner);
        sb.AppendLine("PipX=" + F(PipX));
        sb.AppendLine("PipY=" + F(PipY));
        sb.AppendLine("PipSize=" + F(PipSize));
        sb.AppendLine("PipAspect=" + PipAspect.ToString("0.###", CultureInfo.InvariantCulture));
        sb.AppendLine("PipOverlay=" + (PipOverlay ? "on" : "off"));
        sb.AppendLine("; PipMode = cam (dock camera + guidance) | assist (the Docking Assist, live picture in its view box)");
        sb.AppendLine("PipMode=" + PipMode);
        sb.AppendLine("; PipAuto = the PiP comes up by itself while the full-screen dock camera is in third person");
        sb.AppendLine("PipAuto=" + (PipAuto ? "on" : "off"));
        sb.AppendLine("; PipNear = the PiP comes up by itself while your connector is within PipNearRange metres of a dock");
        sb.AppendLine("PipNear=" + (PipNear ? "on" : "off"));
        sb.AppendLine("PipNearRange=" + PipNearRange.ToString("0", CultureInfo.InvariantCulture));
        sb.AppendLine("; DockedOff = seconds after docking before the dock camera and PiP switch off (0-60), or never (they stay up)");
        sb.AppendLine("DockedOff=" + (DockedOff < 0 ? "never" : DockedOff.ToString("0", CultureInfo.InvariantCulture)));
        sb.AppendLine("; CamMode = what the full-screen dock camera shows: cam | assist (the Docking Assist with the live picture)");
        sb.AppendLine("CamMode=" + CamMode);
        sb.AppendLine("; SteerFull / SteerPip = while that view is up, move and rotate are relative to where the dock camera looks");
        sb.AppendLine("SteerFull=" + (SteerFull ? "on" : "off"));
        sb.AppendLine("SteerPip=" + (SteerPip ? "on" : "off"));
        sb.AppendLine("; SteerKey = a key that switches steering from the camera for the view that's up, e.g. Ctrl+Alt+K (empty = none)");
        sb.AppendLine("SteerKey=" + SteerKey);
        sb.AppendLine("; SteerAuto = steering from the camera switches on by itself as soon as a dock is in range with the view up (with a warning), and off when docking ends");
        sb.AppendLine("SteerAuto=" + (SteerAuto ? "on" : "off"));
        sb.AppendLine("; SteerAutoPip = with SteerAuto, the PiP steers by itself too (off = only the full-screen dock camera)");
        sb.AppendLine("SteerAutoPip=" + (SteerAutoPip ? "on" : "off"));
        sb.AppendLine("; Damp = docking dampener: while docking, the keys you press are held within DampSpeed (m/s, 0.5-200) and DampAccel (m/s2, 0.1-50)");
        sb.AppendLine("Damp=" + (Damp ? "on" : "off"));
        sb.AppendLine("DampSpeed=" + DampSpeed.ToString("0.##", CultureInfo.InvariantCulture));
        sb.AppendLine("DampAccel=" + DampAccel.ToString("0.##", CultureInfo.InvariantCulture));
        sb.AppendLine("; DampAuto = the dampener's limits come down as the dock gets closer, to the safe speed the assist advises (never above DampSpeed / DampAccel)");
        sb.AppendLine("DampAuto=" + (DampAuto ? "on" : "off"));
        sb.AppendLine("; DampUndock = seconds the dampener stays off after a connector lets go (0 = no pause, up to 120), or 'range' = until out of range");
        sb.AppendLine("DampUndock=" + (DampUndock < 0 ? "range" : DampUndock.ToString("0", CultureInfo.InvariantCulture)));
        sb.AppendLine("; AssistStyle = the Docking Assist's look: " + string.Join(" | ", DockStyles.Names));
        sb.AppendLine("AssistStyle=" + AssistStyle);
        sb.AppendLine("; FullStyle / PipStyle / LcdStyle = a look for the full-screen dock camera / the PiP / the LCDs only; empty = AssistStyle");
        sb.AppendLine("; (an LCD's Custom Data line DockStyle=<look> beats LcdStyle for that LCD)");
        sb.AppendLine("FullStyle=" + FullStyle);
        sb.AppendLine("PipStyle=" + PipStyle);
        sb.AppendLine("LcdStyle=" + LcdStyle);
        return sb.ToString();
    }

    private static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    public static bool TryBool(string v, out bool b)
    {
        switch ((v ?? "").Trim().ToLowerInvariant())
        {
            case "on": case "true": case "1": case "yes": b = true; return true;
            case "off": case "false": case "0": case "no": b = false; return true;
            default: b = false; return false;
        }
    }

    private static bool Num(string v, out float f) => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f);

    public static DockSettings Parse(string text)
    {
        var s = new DockSettings();
        foreach (var raw in (text ?? "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
            switch (k)
            {
                case "DockCamOffsets": s.DockCamOffsets = v; break;
                case "DockTag": s.DockTag = v; break;
                case "IgnoreTag": s.IgnoreTag = v; break;
                case "TargetRange":
                    if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double r)) s.TargetRange = Math.Max(20, Math.Min(5000, r));
                    break;
                case "ThemeSource": s.ThemeSource = v.Equals("file", StringComparison.OrdinalIgnoreCase) ? "file" : "auto"; break;
                case "ThemeFont": if (v.Length > 0) s.Theme.Font = v; break;
                case "ThemeNumberFont": if (v.Length > 0) s.Theme.NumberFont = v; break;
                case "Quality": s.Quality = DockQuality.Find(v)?.Name ?? DockQuality.Custom; break;
                case "PipFps": if (Num(v, out var fps)) s.PipFps = (int)DockPipLayout.Clamp(fps, 5, 60); break;
                case "PipRes": if (Num(v, out var res)) s.PipRes = (int)DockPipLayout.Clamp(res, 180, 1080); break;
                case "PipEffects": if (TryBool(v, out var fx)) s.PipEffects = fx; break;
                case "LcdEvery": if (Num(v, out var le)) s.LcdEvery = (int)DockPipLayout.Clamp(le, 1, 6); break;
                case "ScanEvery": if (Num(v, out var se)) s.ScanEvery = DockPipLayout.Clamp(se, 0.5f, 10); break;
                case "LcdFps": if (Num(v, out var lf)) s.LcdFps = (int)DockPipLayout.Clamp(lf, 1, 30); break;
                case "LcdRes": if (Num(v, out var lr)) s.LcdRes = (int)DockPipLayout.Clamp(lr, 120, 1024); break;
                case "LcdFeed": if (TryBool(v, out var lfd)) s.LcdFeed = lfd; break;
                case "PipOn": if (TryBool(v, out var on)) s.PipOn = on; break;
                case "PipCorner": s.PipCorner = DockPipLayout.NormCorner(v) ?? "tr"; break;
                case "PipX": if (Num(v, out var px)) s.PipX = DockPipLayout.Clamp(px, 0, 100); break;
                case "PipY": if (Num(v, out var py)) s.PipY = DockPipLayout.Clamp(py, 0, 100); break;
                case "PipSize": if (Num(v, out var sz)) s.PipSize = DockPipLayout.Clamp(sz, DockPipLayout.MinSize, DockPipLayout.MaxSize); break;
                case "PipAspect": if (DockPipLayout.TryAspect(v, out var asp)) s.PipAspect = asp; break;
                case "PipOverlay": if (TryBool(v, out var ov)) s.PipOverlay = ov; break;
                case "PipMode": s.PipMode = NormPipMode(v) ?? "cam"; break;
                case "PipAuto": if (TryBool(v, out var pa)) s.PipAuto = pa; break;
                case "PipNear": if (TryBool(v, out var pn)) s.PipNear = pn; break;
                case "PipNearRange": if (Num(v, out var pr)) s.PipNearRange = DockPipLayout.Clamp(pr, NearMin, NearMax); break;
                case "DockedOff":
                    if (v.Equals("never", StringComparison.OrdinalIgnoreCase) || v.Equals("keep", StringComparison.OrdinalIgnoreCase)) s.DockedOff = -1;
                    else if (Num(v, out var dof)) s.DockedOff = dof < 0 ? -1 : DockPipLayout.Clamp((float)Math.Round(dof), 0, DockedOffMax);
                    break;
                case "CamMode": s.CamMode = NormPipMode(v) ?? "cam"; break;
                case "SteerFull": if (TryBool(v, out var sf)) s.SteerFull = sf; break;
                case "SteerPip": if (TryBool(v, out var sp)) s.SteerPip = sp; break;
                case "SteerKey": s.SteerKey = DockKeys.Norm(v); break;
                case "SteerAuto": if (TryBool(v, out var sau)) s.SteerAuto = sau; break;
                case "SteerAutoPip": if (TryBool(v, out var sap)) s.SteerAutoPip = sap; break;
                case "Damp": if (TryBool(v, out var dm)) s.Damp = dm; break;
                case "DampAuto": if (TryBool(v, out var dau)) s.DampAuto = dau; break;
                case "DampUndock":
                    if (v.Equals("range", StringComparison.OrdinalIgnoreCase)) s.DampUndock = -1;
                    else if (Num(v, out var dun)) s.DampUndock = dun < 0 ? -1 : DockPipLayout.Clamp((float)Math.Round(dun), 0, DampUndockMax);
                    break;
                case "DampSpeed": if (Num(v, out var ds)) s.DampSpeed = DockPipLayout.Clamp(ds, DampSpeedMin, DampSpeedMax); break;
                case "DampAccel": if (Num(v, out var da)) s.DampAccel = DockPipLayout.Clamp(da, DampAccelMin, DampAccelMax); break;
                case "AssistStyle": s.AssistStyle = DockStyles.Norm(v) ?? DockStyles.Default; break;
                case "FullStyle": s.FullStyle = DockStyles.Norm(v) ?? ""; break;
                case "PipStyle": s.PipStyle = DockStyles.Norm(v) ?? ""; break;
                case "LcdStyle": s.LcdStyle = DockStyles.Norm(v) ?? ""; break;
                default:
                    if (k.StartsWith("Theme") && HudColor.TryParse(v, out var c)) s.Theme.Set(k.Substring(5), c);
                    break;
            }
        }
        // a named preset sets its values (Quality=custom keeps the ones written)
        DockQuality.Find(s.Quality)?.ApplyTo(s);
        return s;
    }
}
