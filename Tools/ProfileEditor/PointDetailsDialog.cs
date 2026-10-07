using System.Globalization;

namespace ModbusExt.ProfileEditor;

sealed class PointDetailsDialog : Form
{
    readonly PointRow _row;
    readonly ComboBox _region = Combo(Enums.Regions), _order = Combo(Enums.WordOrders);
    readonly TextBox _regs = Num(), _bit = Num(), _fail = Num();
    readonly CheckBox _useFail = new() { Text = "Use fail value", AutoSize = true };
    readonly RadioButton _none = new() { Text = "None", AutoSize = true, Checked = true };
    readonly RadioButton _range = new() { Text = "Range", AutoSize = true };
    readonly RadioButton _gainOff = new() { Text = "Gain + offset", AutoSize = true };
    readonly TextBox _rawMin = Num(), _rawMax = Num(), _engMin = Num(), _engMax = Num(), _gain = Num(), _offset = Num();

    public PointDetailsDialog(PointRow row)
    {
        _row = row;
        Text = $"Point details — {row.Name}";
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(12), Dock = DockStyle.Fill };
        int r = 0;
        Add(t, r++, "Region", _region);
        Add(t, r++, "Word order", _order);
        Add(t, r++, "Regs (Text/Raw)", _regs);
        Add(t, r++, "Bit (RegisterBit)", _bit);
        var failRow = new FlowLayoutPanel { AutoSize = true };
        failRow.Controls.AddRange(new Control[] { _useFail, _fail });
        Add(t, r++, "Fail value", failRow);

        var scaling = new GroupBox { Text = "Scaling", AutoSize = true, Padding = new Padding(8) };
        var s = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, Dock = DockStyle.Fill };
        var radios = new FlowLayoutPanel { AutoSize = true };
        radios.Controls.AddRange(new Control[] { _none, _range, _gainOff });
        s.Controls.Add(radios, 0, 0); s.SetColumnSpan(radios, 4);
        s.Controls.Add(Lbl("Raw min"), 0, 1); s.Controls.Add(_rawMin, 1, 1); s.Controls.Add(Lbl("Raw max"), 2, 1); s.Controls.Add(_rawMax, 3, 1);
        s.Controls.Add(Lbl("Eng min"), 0, 2); s.Controls.Add(_engMin, 1, 2); s.Controls.Add(Lbl("Eng max"), 2, 2); s.Controls.Add(_engMax, 3, 2);
        s.Controls.Add(Lbl("Gain"), 0, 3);    s.Controls.Add(_gain, 1, 3);   s.Controls.Add(Lbl("Offset"), 2, 3);  s.Controls.Add(_offset, 3, 3);
        scaling.Controls.Add(s);
        t.Controls.Add(scaling, 0, r); t.SetColumnSpan(scaling, 2); r++;

        var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        t.Controls.Add(buttons, 0, r); t.SetColumnSpan(buttons, 2);
        Controls.Add(t);
        AcceptButton = ok; CancelButton = cancel;

        foreach (var rb in new[] { _none, _range, _gainOff }) rb.CheckedChanged += (_, _) => EnableFields();
        _useFail.CheckedChanged += (_, _) => _fail.Enabled = _useFail.Checked;
        ok.Click += (_, e) => { if (!Apply()) DialogResult = DialogResult.None; };

        Load_();
    }

    void Load_()
    {
        _region.SelectedItem = _row.Region;
        _order.SelectedItem  = _row.WordOrder;
        _regs.Text = _row.Regs?.ToString() ?? "";
        _bit.Text  = _row.Bit?.ToString() ?? "";
        _useFail.Checked = _row.FailValue.HasValue;
        _fail.Text = S(_row.FailValue);
        _rawMin.Text = S(_row.RawMin); _rawMax.Text = S(_row.RawMax); _engMin.Text = S(_row.EngMin); _engMax.Text = S(_row.EngMax);
        _gain.Text = S(_row.Gain); _offset.Text = S(_row.Offset);
        if (_row.HasGain) _gainOff.Checked = true; else if (_row.HasRange) _range.Checked = true; else _none.Checked = true;
        EnableFields();
        _fail.Enabled = _useFail.Checked;
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
            _row.Region    = (string)_region.SelectedItem!;
            _row.WordOrder = (string)_order.SelectedItem!;
            _row.Regs      = string.IsNullOrWhiteSpace(_regs.Text) ? null : uint.Parse(_regs.Text, CultureInfo.InvariantCulture);
            _row.Bit       = string.IsNullOrWhiteSpace(_bit.Text) ? null : byte.Parse(_bit.Text, CultureInfo.InvariantCulture);
            _row.FailValue = _useFail.Checked ? (D(_fail.Text) ?? 0) : null;
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
            MessageBox.Show(this, "A number didn't parse: " + ex.Message, "Point details", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    static double? D(string s) => string.IsNullOrWhiteSpace(s) ? null : double.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
    static string  S(double? d) => d?.ToString("R", CultureInfo.InvariantCulture) ?? "";
    static TextBox Num() => new() { Width = 110 };
    static Label   Lbl(string t) => new() { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
    static ComboBox Combo(string[] items) { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 }; c.Items.AddRange(items); c.SelectedIndex = 0; return c; }
    static void Add(TableLayoutPanel t, int row, string label, Control c) { t.Controls.Add(Lbl(label), 0, row); t.Controls.Add(c, 1, row); }
}