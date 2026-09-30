using System.Text.RegularExpressions;

namespace ModbusExt.ProfileEditor;

sealed record Issue(int Row, string? Column, string Message);   // Row -1 = profile level

static class ProfileValidator
{
    public const uint MaxStrRegs = 16;   // MB_STR_LEN / 2
    public const uint MaxRawRegs = 8;    // MB_RAW_WORDS

    static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "BOOL","BYTE","WORD","DWORD","LWORD","SINT","INT","DINT","LINT","USINT","UINT","UDINT","ULINT","REAL","LREAL",
        "STRING","WSTRING","TIME","DATE","TOD","DT","BIT","ARRAY","OF","TO","AT","VAR","END_VAR","IF","THEN","ELSE","ELSIF",
        "END_IF","CASE","END_CASE","FOR","END_FOR","WHILE","END_WHILE","REPEAT","UNTIL","END_REPEAT","RETURN","EXIT",
        "CONTINUE","AND","OR","XOR","NOT","MOD","TRUE","FALSE","POINTER","REFERENCE","FUNCTION","FUNCTION_BLOCK","PROGRAM",
        "METHOD","PROPERTY","INTERFACE","EXTENDS","IMPLEMENTS","SUPER","THIS","ADD","SUB","MUL","DIV","ABS","MAX","MIN",
        "SEL","MUX","LIMIT","SHL","SHR","ROL","ROR","S","R","LD","ST","JMP","CAL","RET","IN","PT","ET","Q","CLK"
    };

    public static List<Issue> Validate(ProfileModel m)
    {
        var issues = new List<Issue>();
        if (string.IsNullOrWhiteSpace(m.Model)) issues.Add(new(-1, null, "Model name is empty."));
        else if (m.Model.Length > 32)          issues.Add(new(-1, null, "Model name longer than 32 characters."));
        if (!Regex.IsMatch(m.SlowPoll.Trim(), @"^T#\d+(ms|s|m|h|d)$", RegexOptions.IgnoreCase))
            issues.Add(new(-1, null, "Slow poll must be a TIME literal such as T#5S or T#500MS."));
        if (!Regex.IsMatch(m.FbName, @"^[A-Za-z_][A-Za-z0-9_]*$")) issues.Add(new(-1, null, "FB name is not a valid identifier."));
        if (m.Points.Count == 0) issues.Add(new(-1, null, "No points."));

        var resolved = new List<(int Row, string Region, uint Offset, uint Count)>();
        for (int i = 0; i < m.Points.Count; i++)
        {
            var p = m.Points[i];

            if (!Regex.IsMatch(p.Name, @"^[A-Za-z_][A-Za-z0-9_]*$") || p.Name.Contains("__"))
                issues.Add(new(i, "Name", "Not a valid IEC identifier."));
            else if (Reserved.Contains(p.Name)) issues.Add(new(i, "Name", "Reserved word."));
            for (int j = 0; j < i; j++)
                if (m.Points[j].Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new(i, "Name", $"Duplicate of row {j + 1}."));

            // Region and 0-based offset
            string region = p.Region; uint offset = 0; bool addrOk = true;
            switch (m.Addressing)
            {
                case "Modicon":
                    var (r, off) = Modicon(p.Address);
                    if (r == null) { issues.Add(new(i, "Address", "Not a Modicon reference: 1-9999, 10001-19999, 30001-39999, 40001-49999, or the 6-digit forms.")); addrOk = false; }
                    else if (p.Region != "Auto" && p.Region != r) { issues.Add(new(i, "Region", $"The address says {r}.")); addrOk = false; }
                    else { region = r; offset = off; }
                    break;
                case "Offset0":
                    if (p.Region == "Auto") { issues.Add(new(i, "Region", "Region required with offset addressing.")); addrOk = false; }
                    if (p.Address > 65535)  { issues.Add(new(i, "Address", "0..65535.")); addrOk = false; }
                    offset = p.Address;
                    break;
                case "Offset1":
                    if (p.Region == "Auto") { issues.Add(new(i, "Region", "Region required with offset addressing.")); addrOk = false; }
                    if (p.Address < 1 || p.Address > 65536) { issues.Add(new(i, "Address", "1..65536.")); addrOk = false; }
                    offset = p.Address == 0 ? 0 : p.Address - 1;
                    break;
            }
            bool bits = region is "Coil" or "DiscreteInput";

            // Type and register count
            string type = p.Type == "Auto" ? (bits ? "Boolean" : "UInt16") : p.Type;
            uint count = 1;
            switch (type)
            {
                case "Boolean":
                    if (!bits && addrOk) issues.Add(new(i, "Type", "Boolean only on coils and discrete inputs."));
                    break;
                case "Int16": case "UInt16":
                    if (bits) issues.Add(new(i, "Type", "Integer type on a bit region."));
                    break;
                case "RegisterBit":
                    if (bits) issues.Add(new(i, "Type", "RegisterBit on a bit region."));
                    if (p.Bit is null or > 15) issues.Add(new(i, "Bit", "Bit 0..15 required."));
                    break;
                case "Int32": case "UInt32": case "Real32":
                    if (bits) issues.Add(new(i, "Type", "32-bit type on a bit region."));
                    count = 2;
                    break;
                case "Int64": case "UInt64": case "Real64":
                    if (bits) issues.Add(new(i, "Type", "64-bit type on a bit region."));
                    count = 4;
                    break;
                case "Text":
                    if (bits) issues.Add(new(i, "Type", "Text on a bit region."));
                    if (p.Regs is null or < 1 or > MaxStrRegs) issues.Add(new(i, "Regs", $"Regs 1..{MaxStrRegs} required.")); else count = p.Regs.Value;
                    break;
                case "Raw":
                    if (bits) issues.Add(new(i, "Type", "Raw on a bit region."));
                    if (p.Regs is null or < 1 or > MaxRawRegs) issues.Add(new(i, "Regs", $"Regs 1..{MaxRawRegs} required.")); else count = p.Regs.Value;
                    break;
            }
            if (type is not ("Text" or "Raw") && p.Regs != null) issues.Add(new(i, "Regs", "Only Text and Raw use Regs."));
            if (type != "RegisterBit" && p.Bit != null)          issues.Add(new(i, "Bit", "Only RegisterBit uses Bit."));
            if (!bits && count > m.MaxRegsPerRead)               issues.Add(new(i, "Type", $"Wider than max regs per read ({m.MaxRegsPerRead})."));
            if ((ulong)offset + count > 65536)                   issues.Add(new(i, "Address", "Runs past the end of the address space."));

            // Scaling and fail value
                        // Scaling and fail value
            bool anyScale = p.RawMin.HasValue || p.RawMax.HasValue || p.EngMin.HasValue || p.EngMax.HasValue;
            bool gainForm = p.Gain.HasValue || p.Offset.HasValue;
            if (anyScale && gainForm)
                issues.Add(new(i, "Gain", "Use either Gain/Offset or the RawMin..EngMax range, not both."));
            if (gainForm && (p.Gain is null or 0))
                issues.Add(new(i, "Gain", "Gain required and non-zero (use 1 for offset only)."));
            if (anyScale)
            {
                if (!(p.RawMin.HasValue && p.RawMax.HasValue && p.EngMin.HasValue && p.EngMax.HasValue))
                    issues.Add(new(i, "RawMin", "Scaling needs all four: RawMin, RawMax, EngMin, EngMax."));
                else
                {
                    if (p.RawMin == p.RawMax) issues.Add(new(i, "RawMax", "RawMin and RawMax must differ."));
                    if (p.EngMin == p.EngMax) issues.Add(new(i, "EngMax", "EngMin and EngMax must differ."));
                }
            }
            if ((anyScale || gainForm) && (type is "Boolean" or "RegisterBit" or "Text" or "Raw"))
                issues.Add(new(i, anyScale ? "RawMin" : "Gain", $"No scaling on {type}."));
            if (p.FailValue.HasValue && (type is "Text" or "Raw"))
                issues.Add(new(i, "FailValue", $"No fail value on {type}."));
            if (addrOk) resolved.Add((i, region, offset, count));
        }

        for (int a = 0; a < resolved.Count; a++)
            for (int b = a + 1; b < resolved.Count; b++)
            {
                var x = resolved[a]; var y = resolved[b];
                if (x.Region == y.Region && x.Offset < y.Offset + y.Count && y.Offset < x.Offset + x.Count)
                    issues.Add(new(y.Row, "Address", $"Overlaps row {x.Row + 1} ({m.Points[x.Row].Name})."));
            }
        return issues;
    }

    public static (string? Region, uint Offset) Modicon(uint a) => a switch
    {
        >= 1      and <= 9999   => ("Coil",            a - 1),
        >= 10001  and <= 19999  => ("DiscreteInput",   a - 10001),
        >= 30001  and <= 39999  => ("InputRegister",   a - 30001),
        >= 40001  and <= 49999  => ("HoldingRegister", a - 40001),
        >= 100001 and <= 165536 => ("DiscreteInput",   a - 100001),
        >= 300001 and <= 365536 => ("InputRegister",   a - 300001),
        >= 400001 and <= 465536 => ("HoldingRegister", a - 400001),
        _ => (null, 0)
    };
}