using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ModbusExt.ProfileEditor;

static class ProfileParser
{
    static readonly Regex Header   = new(@"FUNCTION_BLOCK\s+(\w+)\s+EXTENDS\s+FB_ModbusDevice", RegexOptions.IgnoreCase);
    static readonly Regex VarInput = new(@"VAR_INPUT(.*?)END_VAR", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    static readonly Regex BlockCmt = new(@"\(\*.*?\*\)", RegexOptions.Singleline);
    static readonly Regex Point    = new(@"(\w+)\s*:\s*ST_MbPoint\s*(?::=\s*\((.*?)\))?\s*;[ \t]*(?://[ \t]*([^\r\n]*))?",
                                         RegexOptions.Singleline | RegexOptions.IgnoreCase);
    static readonly Regex ProfileCall  = new(@"Profile\s*\((.*?)\)\s*;", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    static readonly Regex AddPointCall = new(@"AddPoint\s*\(\s*(\w+)\s*,\s*'([^']*)'\s*\)\s*;", RegexOptions.IgnoreCase);

    public static ProfileModel Parse(string declaration, string? registerBody)
    {
        var m = new ProfileModel();
        var h = Header.Match(declaration);
        if (!h.Success) throw new FormatException("Not a profile: no 'FUNCTION_BLOCK X EXTENDS FB_ModbusDevice' header.");
        m.FbName = h.Groups[1].Value;

        var vi = VarInput.Match(BlockCmt.Replace(declaration, ""));
        if (!vi.Success) throw new FormatException("No VAR_INPUT block.");
        foreach (Match p in Point.Matches(vi.Groups[1].Value))
        {
            var row = new PointRow { Name = p.Groups[1].Value, Comment = p.Groups[3].Value.Trim() };
            if (p.Groups[2].Success) ApplyFields(row, p.Groups[2].Value, m.Warnings);
            m.Points.Add(row);
        }

        if (registerBody == null) { m.Warnings.Add("No Register method; profile settings left at defaults."); return m; }
        var pc = ProfileCall.Match(BlockCmt.Replace(registerBody, ""));
        if (pc.Success) ApplyProfile(m, pc.Groups[1].Value);
        else m.Warnings.Add("No Profile(...) call in Register.");

        var added = AddPointCall.Matches(registerBody).Cast<Match>()
                                .Select(x => (Id: x.Groups[1].Value, Name: x.Groups[2].Value)).ToList();
        foreach (var row in m.Points)
        {
            var a = added.FirstOrDefault(x => x.Id.Equals(row.Name, StringComparison.OrdinalIgnoreCase));
            if (a.Id == null) m.Warnings.Add($"{row.Name}: declared but never registered (no AddPoint).");
            else if (a.Name != row.Name) m.Warnings.Add($"{row.Name}: registered as '{a.Name}'; normalized on save.");
        }
        foreach (var a in added)
            if (!m.Points.Any(r => r.Name.Equals(a.Id, StringComparison.OrdinalIgnoreCase)))
                m.Warnings.Add($"AddPoint({a.Id}): no matching declaration.");
        return m;
    }

    static void ApplyFields(PointRow row, string init, List<string> warnings)
    {
        bool useFail = false; double? failVal = null;
        foreach (var (key, val) in SplitNamedArgs(init))
        {
            switch (key.ToLowerInvariant())
            {
                case "naddr":         row.Address   = ParseUInt(val); break;
                case "etype":         row.Type      = EnumMember(val); break;
                case "eregion":       row.Region    = EnumMember(val); break;
                case "ewordorder":    row.WordOrder = EnumMember(val); break;
                case "epoll":         row.Poll      = EnumMember(val); break;
                case "frawmin":       row.RawMin    = ParseDouble(val); break;
                case "frawmax":       row.RawMax    = ParseDouble(val); break;
                case "fengmin":       row.EngMin    = ParseDouble(val); break;
                case "fengmax":       row.EngMax    = ParseDouble(val); break;
                case "busefailvalue": useFail       = ParseBool(val); break;
                case "ffailvalue":    failVal       = ParseDouble(val); break;
                case "nregs":         row.Regs      = ParseUInt(val); break;
                case "nbit":          row.Bit       = (byte)ParseUInt(val); break;
                default: warnings.Add($"{row.Name}: unknown field '{key}' ignored."); break;
            }
        }
        row.FailValue = useFail ? (failVal ?? 0.0) : null;
    }

    static void ApplyProfile(ProfileModel m, string args)
    {
        foreach (var (key, val) in SplitNamedArgs(args))
        {
            switch (key.ToLowerInvariant())
            {
                case "smodel":          m.Model          = val.Trim().Trim('\''); break;
                case "ewordorder":      m.WordOrder      = EnumMember(val); break;
                case "eaddressing":     m.Addressing     = EnumMember(val); break;
                case "nmaxregsperread": m.MaxRegsPerRead = ParseUInt(val); break;
                case "nmaxgap":         m.MaxGap         = ParseUInt(val); break;
                case "bfc6only":        m.Fc6Only        = ParseBool(val); break;
                case "bfc16only":       m.Fc16Only       = ParseBool(val); break;
                case "bstringbyteswap": m.StringByteSwap = ParseBool(val); break;
                case "tslowpoll":       m.SlowPoll       = val.Trim(); break;
                default: m.Warnings.Add($"Profile(): unknown argument '{key}' ignored."); break;
            }
        }
    }

    // Splits "a := 1, b := 'x,y', c := E_MbType.Real32" on top-level commas outside quotes
    static IEnumerable<(string Key, string Value)> SplitNamedArgs(string s)
    {
        var parts = new List<string>(); var sb = new StringBuilder(); int depth = 0; bool quoted = false;
        foreach (char c in s)
        {
            if (c == '\'') quoted = !quoted;
            if (!quoted && c == '(') depth++;
            if (!quoted && c == ')') depth--;
            if (c == ',' && depth == 0 && !quoted) { parts.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        parts.Add(sb.ToString());
        foreach (var p in parts)
        {
            int i = p.IndexOf(":=", StringComparison.Ordinal);
            if (i < 0) { if (p.Trim().Length > 0) yield return (p.Trim(), ""); continue; }
            yield return (p[..i].Trim(), p[(i + 2)..].Trim());
        }
    }

    static string EnumMember(string v) { v = v.Trim(); int i = v.LastIndexOf('.'); return i >= 0 ? v[(i + 1)..] : v; }
    static bool   ParseBool(string v)   => v.Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase);
    static double ParseDouble(string v) => double.Parse(v.Trim().Replace("_", ""), NumberStyles.Float, CultureInfo.InvariantCulture);
    static uint   ParseUInt(string v)
    {
        v = v.Trim().Replace("_", "");
        return v.StartsWith("16#", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(v[3..], 16)
            : uint.Parse(v, CultureInfo.InvariantCulture);
    }
}