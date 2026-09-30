using System.Globalization;
using System.Text;

namespace ModbusExt.ProfileEditor;

static class CsvTable
{
    static readonly string[] Header = { "Name", "Address", "Type", "Region", "WordOrder", "Poll", "RawMin", "RawMax", "EngMin", "EngMax", "FailValue", "Regs", "Bit", "Comment" };

    public static string Write(IEnumerable<PointRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Header));
        foreach (var p in rows)
            sb.AppendLine(string.Join(",", new[]
            {
                p.Name, p.Address.ToString(), p.Type, p.Region, p.WordOrder, p.Poll,
                N(p.RawMin), N(p.RawMax), N(p.EngMin), N(p.EngMax), N(p.FailValue),
                p.Regs?.ToString() ?? "", p.Bit?.ToString() ?? "", Quote(p.Comment)
            }));
        return sb.ToString();
    }

    public static List<PointRow> Read(string text, char sep)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count < 2) throw new FormatException("Need a header line and at least one data line.");
        var head = Split(lines[0], sep).Select(Canon).ToList();
        if (!head.Contains("name")) throw new FormatException("No 'Name' column in the header.");

        var rows = new List<PointRow>();
        foreach (var line in lines.Skip(1))
        {
            var cells = Split(line, sep);
            var p = new PointRow();
            for (int i = 0; i < head.Count && i < cells.Count; i++)
            {
                string v = cells[i].Trim();
                if (v.Length == 0) continue;
                switch (head[i])
                {
                    case "name":                       p.Name = v; break;
                    case "address" or "addr" or "register" or "reg": p.Address = uint.Parse(v, CultureInfo.InvariantCulture); break;
                    case "type" or "datatype":         p.Type = Match(v, Enums.Types); break;
                    case "region":                     p.Region = Match(v, Enums.Regions); break;
                    case "wordorder" or "order":       p.WordOrder = Match(v, Enums.WordOrders); break;
                    case "poll":                       p.Poll = Match(v, Enums.Polls); break;
                    case "rawmin":                     p.RawMin = D(v); break;
                    case "rawmax":                     p.RawMax = D(v); break;
                    case "engmin":                     p.EngMin = D(v); break;
                    case "engmax":                     p.EngMax = D(v); break;
                    case "failvalue" or "fail":        p.FailValue = D(v); break;
                    case "regs" or "nregs":            p.Regs = uint.Parse(v, CultureInfo.InvariantCulture); break;
                    case "bit" or "nbit":              p.Bit = byte.Parse(v, CultureInfo.InvariantCulture); break;
                    case "comment" or "description":   p.Comment = v; break;
                }
            }
            rows.Add(p);
        }
        return rows;
    }

    static string Canon(string h) => h.Trim().Trim('"').Replace(" ", "").Replace("_", "").ToLowerInvariant();
    static string Match(string v, string[] items) => items.FirstOrDefault(i => i.Equals(v, StringComparison.OrdinalIgnoreCase)) ?? v;
    static double D(string v) => double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
    static string N(double? d) => d?.ToString("R", CultureInfo.InvariantCulture) ?? "";
    static string Quote(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    static List<string> Split(string line, char sep)
    {
        var cells = new List<string>(); var sb = new StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (q && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = !q; }
            else if (c == sep && !q) { cells.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        cells.Add(sb.ToString());
        return cells;
    }
}
