using System.ComponentModel;

namespace ModbusExt.ProfileEditor;

sealed class MainForm : Form
{
    // Toolbar
	readonly ComboBox _instances = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
	readonly Button _refresh = Btn("Refresh");
	readonly Button _save    = Btn("Save to XAE", false);
	readonly Button _revert  = Btn("Revert", false);
	readonly Button _undo    = Btn("Undo last save", false);
	readonly Button _preview = Btn("Preview code", false);
	readonly Button _addRow  = Btn("Add point");
	readonly Button _delRow  = Btn("Delete point");
	readonly Button _up      = Btn("Up");
	readonly Button _down    = Btn("Down");

    // Profile settings
    readonly TextBox       _modelName  = new() { Width = 150 };
    readonly ComboBox      _wordOrder  = MakeCombo(Enums.WordOrders[1..]);
    readonly ComboBox      _addressing = MakeCombo(Enums.Addressing);
    readonly NumericUpDown _maxRegs    = new() { Minimum = 1, Maximum = 125, Value = 125, Width = 55 };
    readonly NumericUpDown _maxGap     = new() { Minimum = 0, Maximum = 125, Value = 8, Width = 55 };
    readonly CheckBox      _fc6        = new() { Text = "FC6 only", AutoSize = true };
    readonly CheckBox      _fc16       = new() { Text = "FC16 only", AutoSize = true };
    readonly CheckBox      _swap       = new() { Text = "String byte swap", AutoSize = true };
    readonly TextBox       _slowPoll   = new() { Width = 70 };

    // Main areas
    readonly ListBox _profiles = new() { Dock = DockStyle.Left, Width = 300 };
    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
    };
    readonly ListBox _issues = new() { Dock = DockStyle.Bottom, Height = 110 };
    readonly BindingList<PointRow> _rows = new();

    List<(string Name, object Obj)> _dtes = new();
    List<ProfilePou> _found = new();
    XaeSession? _session;
    ProfileModel? _profile;
    ProfilePou? _current;
    XaeSession.PouTexts? _undoTexts;
    bool _loading;

    public MainForm()
    {
        Text = "ModbusExt Profile Editor";
        Width = 1400; Height = 850;

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = false };
        bar.Controls.AddRange(new Control[] { _instances, _refresh, Gap(), _save, _revert, _undo, _preview, Gap(), _addRow, _delRow, _up, _down });

        var settings = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = false };
        settings.Controls.AddRange(new Control[]
        {
            Lbl("Model"), _modelName, Lbl("Word order"), _wordOrder, Lbl("Addressing"), _addressing,
            Lbl("Max regs/read"), _maxRegs, Lbl("Max gap"), _maxGap, _fc6, _fc16, _swap, Lbl("Slow poll"), _slowPoll
        });

        BuildGrid();
        Controls.Add(_grid);
        Controls.Add(_issues);
        Controls.Add(_profiles);
        Controls.Add(settings);
        Controls.Add(bar);

        _refresh.Click += (_, _) => Guard(RefreshInstances);
        _instances.SelectedIndexChanged += (_, _) => Guard(Connect);
        _profiles.SelectedIndexChanged += (_, _) => Guard(LoadSelected);
        _revert.Click  += (_, _) => Guard(LoadSelected);
        _undo.Click    += (_, _) => Guard(UndoSave);
        _preview.Click += (_, _) => Guard(Preview);
        _save.Click    += (_, _) => Guard(Save);
        _addRow.Click  += (_, _) => { _rows.Add(new PointRow { Name = "NewPoint", Address = 40001 }); ValidateProfile(); };
        _delRow.Click  += (_, _) => { if (_grid.CurrentRow != null) { _rows.RemoveAt(_grid.CurrentRow.Index); ValidateProfile(); } };
        _up.Click      += (_, _) => MoveRow(-1);
        _down.Click    += (_, _) => MoveRow(+1);

        _grid.CellValueChanged += (_, _) => { if (!_loading) Guard(ValidateProfile); };
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.DataError += (_, e) => { e.ThrowException = false; };

        _modelName.TextChanged += (_, _) => SettingsChanged();
        _slowPoll.TextChanged  += (_, _) => SettingsChanged();
        _wordOrder.SelectedIndexChanged  += (_, _) => SettingsChanged();
        _addressing.SelectedIndexChanged += (_, _) => SettingsChanged();
        _maxRegs.ValueChanged += (_, _) => SettingsChanged();
        _maxGap.ValueChanged  += (_, _) => SettingsChanged();
        foreach (var cb in new[] { _fc6, _fc16, _swap }) cb.CheckedChanged += (_, _) => SettingsChanged();

        Load += (_, _) => Guard(RefreshInstances);
    }

    void BuildGrid()
    {
        _grid.Columns.AddRange(
            Col("Name", 110), Col("Address", 75), Combo("Type", Enums.Types, 95), Combo("Region", Enums.Regions, 115),
            Col("RawMin", 65), Col("RawMax", 65), Col("EngMin", 65), Col("EngMax", 65),
            Combo("Poll", Enums.Polls, 60), Col("FailValue", 70), Col("Regs", 45), Col("Bit", 40),
            Combo("WordOrder", Enums.WordOrders, 80), Col("Comment", 260));
        _grid.DataSource = _rows;
    }

    // ---- XAE ----

    void RefreshInstances()
    {
        _dtes = RunningObjects.ListDte();
        _instances.Items.Clear();
        foreach (var d in _dtes) _instances.Items.Add(d.Name);
        if (_dtes.Count > 0) _instances.SelectedIndex = 0;
        else Text = "ModbusExt Profile Editor — no running XAE found";
    }

    void Connect()
    {
        _session = new XaeSession(_dtes[_instances.SelectedIndex].Obj);
        Text = $"ModbusExt Profile Editor — {_session.SolutionName}";
        _found = _session.FindProfiles();
        _profiles.Items.Clear();
        foreach (var p in _found) _profiles.Items.Add($"{p.PlcName} / {p.Name}");
        if (_found.Count == 0) _issues.Items.Add("No profiles found in the open solution.");
    }

    void LoadSelected()
    {
        if (_profiles.SelectedIndex < 0) return;
        _current = _found[_profiles.SelectedIndex];
        var texts = XaeSession.Read(_current);
        _profile = ProfileParser.Parse(texts.Declaration, texts.Register);

        _loading = true;
        _modelName.Text          = _profile.Model;
        _wordOrder.SelectedItem  = _profile.WordOrder;
        _addressing.SelectedItem = _profile.Addressing;
        _maxRegs.Value           = Math.Clamp(_profile.MaxRegsPerRead, 1u, 125u);
        _maxGap.Value            = Math.Clamp(_profile.MaxGap, 0u, 125u);
        _fc6.Checked   = _profile.Fc6Only;
        _fc16.Checked  = _profile.Fc16Only;
        _swap.Checked  = _profile.StringByteSwap;
        _slowPoll.Text = _profile.SlowPoll;
        _rows.Clear();
        foreach (var p in _profile.Points) _rows.Add(p);
        _loading = false;

        _revert.Enabled = _preview.Enabled = true;
        ValidateProfile();
        foreach (var w in _profile.Warnings) _issues.Items.Add("Note: " + w);
    }

    void Save()
    {
        if (_profile == null || _current == null) return;
        ValidateProfile();
        if (!_save.Enabled) return;
        _undoTexts = XaeSession.Read(_current);
        XaeSession.Write(_current, ProfileWriter.Declaration(_profile), ProfileWriter.Body(), ProfileWriter.Register(_profile));
        _undo.Enabled = true;
        LoadSelected();                                  // Re-read from XAE: what you see is what landed
        _issues.Items.Insert(0, "Saved to XAE. Build the PLC project to compile it.");
    }

    void UndoSave()
    {
        if (_undoTexts == null || _current == null) return;
        XaeSession.Write(_current, _undoTexts.Declaration, _undoTexts.Body, _undoTexts.Register ?? "");
        _undoTexts = null;
        _undo.Enabled = false;
        LoadSelected();
    }

    // ---- Editing ----

    void SettingsChanged()
    {
        if (_loading || _profile == null) return;
        _profile.Model          = _modelName.Text.Trim();
        _profile.WordOrder      = (string)_wordOrder.SelectedItem!;
        _profile.Addressing     = (string)_addressing.SelectedItem!;
        _profile.MaxRegsPerRead = (uint)_maxRegs.Value;
        _profile.MaxGap         = (uint)_maxGap.Value;
        _profile.Fc6Only        = _fc6.Checked;
        _profile.Fc16Only       = _fc16.Checked;
        _profile.StringByteSwap = _swap.Checked;
        _profile.SlowPoll       = _slowPoll.Text.Trim();
        ValidateProfile();
    }

    void ValidateProfile()
    {
        if (_profile == null) return;
        _profile.Points.Clear();
        _profile.Points.AddRange(_rows);
        var issues = ProfileValidator.Validate(_profile);

        foreach (DataGridViewRow r in _grid.Rows) { r.ErrorText = ""; foreach (DataGridViewCell c in r.Cells) c.ErrorText = ""; }
        _issues.Items.Clear();
        foreach (var i in issues)
        {
            _issues.Items.Add(i.Row < 0 ? i.Message : $"Row {i.Row + 1} ({_rows[i.Row].Name}) {i.Column}: {i.Message}");
            if (i.Row < 0 || i.Row >= _grid.Rows.Count) continue;
            var row = _grid.Rows[i.Row];
            if (i.Column != null && _grid.Columns.Contains(i.Column)) row.Cells[i.Column].ErrorText = i.Message;
            else row.ErrorText = i.Message;
        }
        _issues.ForeColor = issues.Count == 0 ? Color.DarkGreen : Color.Firebrick;
        if (issues.Count == 0) _issues.Items.Add($"OK: {_rows.Count} points, ready to save.");
        _save.Enabled = issues.Count == 0 && _current != null;
    }

    void MoveRow(int delta)
    {
        if (_grid.CurrentRow == null) return;
        int i = _grid.CurrentRow.Index, j = i + delta;
        if (j < 0 || j >= _rows.Count) return;
        (_rows[i], _rows[j]) = (_rows[j], _rows[i]);
        _grid.CurrentCell = _grid.Rows[j].Cells[0];
        ValidateProfile();
    }

    void Preview()
    {
        if (_profile == null) return;
        ValidateProfile();
        var f = new Form { Text = $"Generated: {_profile.FbName}", Width = 950, Height = 700 };
        var t = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = false,
            ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 10),
            Text = ProfileWriter.Declaration(_profile)
                 + "\r\n(* ---- Body ---- *)\r\n" + ProfileWriter.Body()
                 + "\r\n(* ---- Register ---- *)\r\n" + ProfileWriter.Register(_profile)
        };
        f.Controls.Add(t);
        f.Show(this);
    }

    // ---- Helpers ----

    static DataGridViewColumn Col(string prop, int w) => new DataGridViewTextBoxColumn
    {
        Name = prop, DataPropertyName = prop, HeaderText = prop, Width = w,
        DefaultCellStyle = new DataGridViewCellStyle { NullValue = "", DataSourceNullValue = null }
    };

    static DataGridViewColumn Combo(string prop, string[] items, int w)
    {
        var c = new DataGridViewComboBoxColumn { Name = prop, DataPropertyName = prop, HeaderText = prop, Width = w, FlatStyle = FlatStyle.Flat };
        c.Items.AddRange(items);
        return c;
    }

    static ComboBox MakeCombo(string[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        c.Items.AddRange(items);
        c.SelectedIndex = 0;
        return c;
    }

    static Label   Lbl(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(8, 8, 2, 0) };
    static Control Gap()         => new Label { Width = 16 };

    static void Guard(Action a)
    {
        try { a(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "XAE", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
	
	static Button Btn(string text, bool enabled = true) => new() { Text = text, AutoSize = true, Enabled = enabled };
}