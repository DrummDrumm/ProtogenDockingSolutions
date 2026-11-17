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
    /// <summary>What the full-screen dock camera shows: "cam" = the camera view with its guidance; "assist" = the
    /// Docking Assist across the screen with the live picture in its view box.</summary>
    public string CamMode = "cam";
    /// <summary>The Docking Assist's look (DockStyles.Names): holo, ... or classic.</summary>
    public string AssistStyle = DockStyles.Default;
    /// <summary>The look in one place (full-screen dock camera, PiP, LCDs); "" = AssistStyle.</summary>
    public string FullStyle = "", PipStyle = "", LcdStyle = "";

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
        sb.AppendLine("; CamMode = what the full-screen dock camera shows: cam | assist (the Docking Assist with the live picture)");
        sb.AppendLine("CamMode=" + CamMode);
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
                case "CamMode": s.CamMode = NormPipMode(v) ?? "cam"; break;
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
