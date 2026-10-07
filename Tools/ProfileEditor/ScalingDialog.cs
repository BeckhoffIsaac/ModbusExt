using System.Globalization;

namespace ModbusExt.ProfileEditor;

sealed class ScalingDialog : Form
{
    readonly PointRow _row;
    readonly RadioButton _none = new() { Text = "None", AutoSize = true };
    readonly RadioButton _range = new() { Text = "Range  (raw min..max → eng min..max)", AutoSize = true };
    readonly RadioButton _gainOff = new() { Text = "Gain + offset  (eng = raw × gain + offset)", AutoSize = true };
    readonly TextBox _rawMin = Num(), _rawMax = Num(), _engMin = Num(), _engMax = Num(), _gain = Num(), _offset = Num();

    public ScalingDialog(PointRow row)
    {
        _row = row;
        Text = $"Scaling — {row.Name}";
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var t = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, Padding = new Padding(12) };
        int r = 0;
        t.Controls.Add(_none, 0, r); t.SetColumnSpan(_none, 4); r++;
        t.Controls.Add(_range, 0, r); t.SetColumnSpan(_range, 4); r++;
        t.Controls.Add(Lbl("Raw min"), 0, r); t.Controls.Add(_rawMin, 1, r); t.Controls.Add(Lbl("Raw max"), 2, r); t.Controls.Add(_rawMax, 3, r); r++;
        t.Controls.Add(Lbl("Eng min"), 0, r); t.Controls.Add(_engMin, 1, r); t.Controls.Add(Lbl("Eng max"), 2, r); t.Controls.Add(_engMax, 3, r); r++;
        t.Controls.Add(_gainOff, 0, r); t.SetColumnSpan(_gainOff, 4); r++;
        t.Controls.Add(Lbl("Gain"), 0, r); t.Controls.Add(_gain, 1, r); t.Controls.Add(Lbl("Offset"), 2, r); t.Controls.Add(_offset, 3, r); r++;

        var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        t.Controls.Add(buttons, 0, r); t.SetColumnSpan(buttons, 4);
        Controls.Add(t);
        AcceptButton = ok; CancelButton = cancel;

        foreach (var rb in new[] { _none, _range, _gainOff }) rb.CheckedChanged += (_, _) => EnableFields();
        ok.Click += (_, _) => { if (!Apply()) DialogResult = DialogResult.None; };

        _rawMin.Text = S(row.RawMin); _rawMax.Text = S(row.RawMax); _engMin.Text = S(row.EngMin); _engMax.Text = S(row.EngMax);
        _gain.Text = S(row.Gain); _offset.Text = S(row.Offset);
        if (row.HasGain) _gainOff.Checked = true; else if (row.HasRange) _range.Checked = true; else _none.Checked = true;
        EnableFields();
    }

    void EnableFields()
    {
        foreach (var c in new[] { _rawMin, _rawMax, _engMin, _engMax }) c.Enabled = _range.Checked;
        foreach (var c in new[] { _gain, _offset }) c.Enabled = _gainOff.Checked;
    }

    bool Apply()
    {
        try
        {
            if (_range.Checked)
            {
                _row.RawMin = D(_rawMin.Text) ?? 0; _row.RawMax = D(_rawMax.Text) ?? 0;
                _row.EngMin = D(_engMin.Text) ?? 0; _row.EngMax = D(_engMax.Text) ?? 0;
                _row.Gain = _row.Offset = null;
            }
            else if (_gainOff.Checked)
            {
                _row.Gain = D(_gain.Text) ?? 0; _row.Offset = D(_offset.Text) ?? 0;
                _row.RawMin = _row.RawMax = _row.EngMin = _row.EngMax = null;
            }
            else
            {
                _row.RawMin = _row.RawMax = _row.EngMin = _row.EngMax = _row.Gain = _row.Offset = null;
            }
            return true;
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, "A number didn't parse: " + ex.Message, "Scaling", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    static double? D(string s) => string.IsNullOrWhiteSpace(s) ? null : double.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
    static string  S(double? d) => d?.ToString("R", CultureInfo.InvariantCulture) ?? "";
    static TextBox Num() => new() { Width = 110 };
    static Label   Lbl(string t) => new() { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(24, 6, 8, 0) };
}