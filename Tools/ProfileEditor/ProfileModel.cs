namespace ModbusExt.ProfileEditor;

sealed class PointRow
{
    string _name = "", _comment = "";
    public string  Name      { get => _name;    set => _name    = value ?? ""; }
    public uint    Address   { get; set; }
    public string  Type      { get; set; } = "Auto";
    public string  Region    { get; set; } = "Auto";
    public string  WordOrder { get; set; } = "Inherit";
    public string  Poll      { get; set; } = "Fast";
    public double? RawMin    { get; set; }
    public double? RawMax    { get; set; }
    public double? EngMin    { get; set; }
    public double? EngMax    { get; set; }
	public double? Gain      { get; set; }      // eng = raw * Gain + Offset; null = not used
    public double? Offset    { get; set; }
    public double? FailValue { get; set; }      // null = no fail-over value
    public uint?   Regs      { get; set; }
    public byte?   Bit       { get; set; }
    public string  Comment   { get => _comment; set => _comment = value ?? ""; }
	public bool   HasRange  => RawMin.HasValue || RawMax.HasValue || EngMin.HasValue || EngMax.HasValue;
    public bool   HasGain   => Gain.HasValue || Offset.HasValue;
    public string ScaleMark => HasGain ? "×" : HasRange ? "↔" : "";
    public string ScalingSummary =>
        HasGain  ? $"× {Gain} {(Offset is double o && o < 0 ? "−" : "+")} {Math.Abs(Offset ?? 0)}" :
        HasRange ? $"raw {RawMin}..{RawMax} → {EngMin}..{EngMax}" : "no scaling";
}

sealed class ProfileModel
{
    public string FbName         { get; set; } = "";
    public string Model          { get; set; } = "";
    public string WordOrder      { get; set; } = "ABCD";
    public string Addressing     { get; set; } = "Modicon";
    public uint   MaxRegsPerRead { get; set; } = 125;
    public uint   MaxGap         { get; set; } = 8;
    public bool   Fc6Only        { get; set; }
    public bool   Fc16Only       { get; set; }
    public bool   StringByteSwap { get; set; }
    public string SlowPoll       { get; set; } = "T#5S";
    public List<PointRow> Points { get; } = new();
    public List<string> Warnings { get; } = new();
}

static class Enums
{
    public static readonly string[] Types      = { "Auto", "Boolean", "Int16", "UInt16", "Int32", "UInt32", "Real32", "Int64", "UInt64", "Real64", "RegisterBit", "Text", "Raw" };
    public static readonly string[] Regions    = { "Auto", "Coil", "DiscreteInput", "InputRegister", "HoldingRegister" };
    public static readonly string[] WordOrders = { "Inherit", "ABCD", "CDAB", "BADC", "DCBA" };
    public static readonly string[] Polls      = { "Fast", "Slow", "Off" };
    public static readonly string[] Addressing = { "Modicon", "Offset0", "Offset1" };
}