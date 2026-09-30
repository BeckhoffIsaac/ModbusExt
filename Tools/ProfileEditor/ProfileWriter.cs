using System.Globalization;
using System.Text;

namespace ModbusExt.ProfileEditor;

static class ProfileWriter
{
    public const string Marker = "(*@ModbusExt.Profile v1 - managed by ModbusExt Profile Editor. One point per line.*)";

    public static string Declaration(ProfileModel m)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Marker);
        sb.AppendLine($"FUNCTION_BLOCK {m.FbName} EXTENDS FB_ModbusDevice");
        sb.AppendLine("VAR_INPUT");
        int w = m.Points.Count == 0 ? 0 : m.Points.Max(p => p.Name.Length);
        foreach (var p in m.Points)
        {
            var line = $"\t{p.Name.PadRight(w)} : ST_MbPoint := ({Fields(p)});";
            if (p.Comment.Length > 0) line += "   // " + p.Comment;
            sb.AppendLine(line);
        }
        sb.AppendLine("END_VAR");
        return sb.ToString();
    }

    public static string Body() => "Register();\r\nSUPER^();\r\n";

    public static string Register(ProfileModel m)
    {
        var a = new List<string> { $"sModel := '{m.Model}'" };
        if (m.WordOrder != "ABCD")     a.Add($"eWordOrder := E_MbWordOrder.{m.WordOrder}");
        if (m.Addressing != "Modicon") a.Add($"eAddressing := E_MbAddressing.{m.Addressing}");
        if (m.MaxRegsPerRead != 125)   a.Add($"nMaxRegsPerRead := {m.MaxRegsPerRead}");
        if (m.MaxGap != 8)             a.Add($"nMaxGap := {m.MaxGap}");
        if (m.Fc6Only)                 a.Add("bFc6Only := TRUE");
        if (m.Fc16Only)                a.Add("bFc16Only := TRUE");
        if (m.StringByteSwap)          a.Add("bStringByteSwap := TRUE");
        if (!m.SlowPoll.Equals("T#5S", StringComparison.OrdinalIgnoreCase)) a.Add($"tSlowPoll := {m.SlowPoll}");

        var sb = new StringBuilder();
        sb.AppendLine($"Profile({string.Join(", ", a)});");
        foreach (var p in m.Points) sb.AppendLine($"AddPoint({p.Name}, '{p.Name}');");
        return sb.ToString();
    }

    static string Fields(PointRow p)
    {
        var f = new List<string> { $"nAddr := {p.Address}" };
        if (p.Region != "Auto")       f.Add($"eRegion := E_MbRegion.{p.Region}");
        if (p.Type != "Auto")         f.Add($"eType := E_MbType.{p.Type}");
        if (p.Regs is uint r)         f.Add($"nRegs := {r}");
        if (p.Bit is byte b)          f.Add($"nBit := {b}");
        if (p.WordOrder != "Inherit") f.Add($"eWordOrder := E_MbWordOrder.{p.WordOrder}");
        if (p.RawMin.HasValue || p.RawMax.HasValue || p.EngMin.HasValue || p.EngMax.HasValue)
        {
            f.Add($"fRawMin := {Num(p.RawMin ?? 0)}");
            f.Add($"fRawMax := {Num(p.RawMax ?? 0)}");
            f.Add($"fEngMin := {Num(p.EngMin ?? 0)}");
            f.Add($"fEngMax := {Num(p.EngMax ?? 0)}");
        }
		
		if (p.Gain.HasValue || p.Offset.HasValue)
        {
            f.Add($"fGain := {Num(p.Gain ?? 0)}");
            f.Add($"fOffset := {Num(p.Offset ?? 0)}");
        }
        if (p.Poll != "Fast")         f.Add($"ePoll := E_MbPoll.{p.Poll}");
        if (p.FailValue is double fv) { f.Add("bUseFailValue := TRUE"); f.Add($"fFailValue := {Num(fv)}"); }
        return string.Join(", ", f);
    }

    static string Num(double d)
    {
        var s = d.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('E') ? d.ToString("0.0###############E+0", CultureInfo.InvariantCulture) : s;
    }
}