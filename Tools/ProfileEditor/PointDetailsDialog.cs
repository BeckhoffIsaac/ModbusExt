using System.Globalization;

namespace ModbusExt.ProfileEditor;

sealed class PointDetailsDialog : Form
{
    readonly PointRow _row;
    readonly ComboBox _region = Combo(Enums.Regions), _order = Combo(Enums.WordOrders);
    readonly TextBox _regs = Num(), _bit = Num(), _fail = Num();
    readonly CheckBox _useFail = new() { Text = "Use fail value", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };

    public PointDetailsDialog(PointRow row)
    {
        _row = row;
        Text = $"Point details — {row.Name}";
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(12) };
        int r = 0;
        Add(t, r++, "Region", _region);
        Add(t, r++, "Word order", _order);
        Add(t, r++, "Regs (Text / Raw)", _regs);
        Add(t, r++, "Bit (RegisterBit)", _bit);
        var failRow = new FlowLayoutPanel { AutoSize = true };
        failRow.Controls.AddRange(new Control[] { _useFail, _fail });
        Add(t, r++, "Fail value", failRow);

        var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        t.Controls.Add(buttons, 0, r); t.SetColumnSpan(buttons, 2);
        Controls.Add(t);
        AcceptButton = ok; CancelButton = cancel;

        _useFail.CheckedChanged += (_, _) => _fail.Enabled = _useFail.Checked;
        ok.Click += (_, _) => { if (!Apply()) DialogResult = DialogResult.None; };

        _region.SelectedItem = row.Region;
        _order.SelectedItem  = row.WordOrder;
        _regs.Text = row.Regs?.ToString() ?? "";
        _bit.Text  = row.Bit?.ToString() ?? "";
        _useFail.Checked = row.FailValue.HasValue;
        _fail.Text = row.FailValue?.ToString("R", CultureInfo.InvariantCulture) ?? "";
        _fail.Enabled = _useFail.Checked;
    }

    bool Apply()
    {
        try
        {
            _row.Region    = (string)_region.SelectedItem!;
            _row.WordOrder = (string)_order.SelectedItem!;
            _row.Regs      = string.IsNullOrWhiteSpace(_regs.Text) ? null : uint.Parse(_regs.Text, CultureInfo.InvariantCulture);
            _row.Bit       = string.IsNullOrWhiteSpace(_bit.Text) ? null : byte.Parse(_bit.Text, CultureInfo.InvariantCulture);
            _row.FailValue = _useFail.Checked
                ? (string.IsNullOrWhiteSpace(_fail.Text) ? 0 : double.Parse(_fail.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture))
                : null;
            return true;
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, "A number didn't parse: " + ex.Message, "Point details", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    static TextBox  Num() => new() { Width = 110 };
    static Label    Lbl(string t) => new() { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
    static ComboBox Combo(string[] items) { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 }; c.Items.AddRange(items); c.SelectedIndex = 0; return c; }
    static void Add(TableLayoutPanel t, int row, string label, Control c) { t.Controls.Add(Lbl(label), 0, row); t.Controls.Add(c, 1, row); }
}