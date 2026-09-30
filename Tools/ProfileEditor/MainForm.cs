using System.ComponentModel;

namespace ModbusExt.ProfileEditor;

sealed class MainForm : Form
{
    // Toolbar
    readonly FlowLayoutPanel _bar = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
    readonly ComboBox _instances = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly Button   _refresh    = Btn("Refresh");
    readonly Button   _new        = Btn("New profile", false);
    readonly Button   _save       = Btn("Save to XAE", false);
    readonly Button   _revert     = Btn("Revert", false);
    readonly Button   _undo       = Btn("Undo last save", false);
    readonly Button   _preview    = Btn("Preview code", false);
    readonly CheckBox _buildAfter = new() { Text = "Build after save", AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
    readonly Button   _addRow     = Btn("Add point");
    readonly Button   _delRow     = Btn("Delete point");
    readonly Button   _up         = Btn("Up");
    readonly Button   _down       = Btn("Down");
    readonly Button   _import     = Btn("Import CSV…", false);
    readonly Button   _paste      = Btn("Paste table", false);
    readonly Button   _export     = Btn("Export CSV…", false);

    // Profile settings
    readonly FlowLayoutPanel _settings = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
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
        Width = 1500; Height = 850;

        _bar.Controls.AddRange(new Control[]
        {
            _instances, _refresh, Gap(), _new, _save, _revert, _undo, _preview, _buildAfter, Gap(),
            _addRow, _delRow, _up, _down, Gap(), _import, _paste, _export
        });
        _settings.Controls.AddRange(new Control[]
        {
            Lbl("Model"), _modelName, Lbl("Word order"), _wordOrder, Lbl("Addressing"), _addressing,
            Lbl("Max regs/read"), _maxRegs, Lbl("Max gap"), _maxGap, _fc6, _fc16, _swap, Lbl("Slow poll"), _slowPoll
        });

        BuildGrid();
        Controls.Add(_grid);
        Controls.Add(_issues);
        Controls.Add(_profiles);
        Controls.Add(_settings);
        Controls.Add(_bar);

        _refresh.Click += (_, _) => Guard(RefreshInstances);
        _instances.SelectedIndexChanged += (_, _) => Guard(Connect);
        _profiles.SelectedIndexChanged += (_, _) => Guard(LoadSelected);
        _new.Click     += (_, _) => Guard(NewProfile);
        _save.Click    += (_, _) => Guard(Save);
        _revert.Click  += (_, _) => Guard(LoadSelected);
        _undo.Click    += (_, _) => Guard(UndoSave);
        _preview.Click += (_, _) => Guard(Preview);
        _addRow.Click  += (_, _) => { if (_profile != null) { _rows.Add(new PointRow { Name = "NewPoint", Address = 40001 }); ValidateProfile(); } };
        _delRow.Click  += (_, _) => { if (_grid.CurrentRow != null) { _rows.RemoveAt(_grid.CurrentRow.Index); ValidateProfile(); } };
        _up.Click      += (_, _) => MoveRow(-1);
        _down.Click    += (_, _) => MoveRow(+1);
        _import.Click  += (_, _) => Guard(ImportCsv);
        _paste.Click   += (_, _) => Guard(PasteTable);
        _export.Click  += (_, _) => Guard(ExportCsv);

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

        ClearEditor();
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
        ClearEditor();
        _session = new XaeSession(_dtes[_instances.SelectedIndex].Obj);
        Text = $"ModbusExt Profile Editor — {_session.SolutionName}";
        _found = _session.FindProfiles();
        _profiles.Items.Clear();
        foreach (var p in _found) _profiles.Items.Add($"{p.PlcName} / {p.Name}");
        _new.Enabled = true;
        if (_found.Count == 0) _issues.Items.Add("No profiles found in the open solution. Use New profile.");
    }

    void ClearEditor()
    {
        _loading = true;
        _current = null;
        _profile = null;
        _rows.Clear();
        _modelName.Text = "";
        _slowPoll.Text  = "";
        _loading = false;
        _issues.Items.Clear();
        _grid.Enabled = _settings.Enabled = false;
        _save.Enabled = _revert.Enabled = _preview.Enabled = _import.Enabled = _paste.Enabled = _export.Enabled = false;
    }

    void LoadSelected()
    {
        if (_profiles.SelectedIndex < 0) { ClearEditor(); return; }
        _grid.Enabled = _settings.Enabled = true;
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

        _revert.Enabled = _preview.Enabled = _import.Enabled = _paste.Enabled = _export.Enabled = true;
        ValidateProfile();
        foreach (var w in _profile.Warnings) _issues.Items.Add("Note: " + w);
    }

    void Save()
    {
        if (_profile == null || _current == null || _session == null) return;
        ValidateProfile();
        if (!_save.Enabled) return;
        _undoTexts = XaeSession.Read(_current);
        XaeSession.Write(_current, ProfileWriter.Declaration(_profile), ProfileWriter.Body(), ProfileWriter.Register(_profile));
        _undo.Enabled = true;
        LoadSelected();                                  // Re-read from XAE: what you see is what landed
        _issues.Items.Insert(0, "Saved to XAE.");
        if (_buildAfter.Checked) _session.BuildSolution();
    }

    void UndoSave()
    {
        if (_undoTexts == null || _current == null) return;
        XaeSession.Write(_current, _undoTexts.Declaration, _undoTexts.Body, _undoTexts.Register ?? "");
        _undoTexts = null;
        _undo.Enabled = false;
        LoadSelected();
    }

    void NewProfile()
    {
        if (_session == null) return;
        var plcs = _session.PlcProjects();
        if (plcs.Count == 0) throw new InvalidOperationException("No PLC projects in the solution.");

        using var dlg = new Form
        {
            Text = "New profile", Width = 440, Height = 200, FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false
        };
        var plc   = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        plc.Items.AddRange(plcs.ToArray());
        plc.SelectedIndex = 0;
        var model = new TextBox { Width = 280 };
        var fb    = new TextBox { Width = 280, Text = "FB_Mb_" };
        model.TextChanged += (_, _) => fb.Text = "FB_Mb_" + new string(model.Text.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        var ok = new Button { Text = "Create", DialogResult = DialogResult.OK, AutoSize = true };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
        table.Controls.Add(Lbl("PLC project"), 0, 0); table.Controls.Add(plc, 1, 0);
        table.Controls.Add(Lbl("Model"), 0, 1);       table.Controls.Add(model, 1, 1);
        table.Controls.Add(Lbl("FB name"), 0, 2);     table.Controls.Add(fb, 1, 2);
        table.Controls.Add(ok, 1, 3);
        dlg.Controls.Add(table);
        dlg.AcceptButton = ok;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var m = new ProfileModel { FbName = fb.Text.Trim(), Model = model.Text.Trim() };
        m.Points.Add(new PointRow { Name = "Point1", Address = 40001, Comment = "replace me" });
        var issues = ProfileValidator.Validate(m);
        if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues.Select(i => i.Message)));
        if (_found.Any(p => p.Name.Equals(m.FbName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{m.FbName} already exists.");

        var pou = _session.CreateProfile((string)plc.SelectedItem!, m.FbName,
                                         ProfileWriter.Declaration(m), ProfileWriter.Body(), ProfileWriter.Register(m));
        _found.Add(pou);
        _profiles.Items.Add($"{pou.PlcName} / {pou.Name}");
        _profiles.SelectedIndex = _profiles.Items.Count - 1;
    }

    // ---- Import / export ----

    void ImportCsv()
    {
        if (_profile == null) return;
        using var dlg = new OpenFileDialog { Filter = "CSV files|*.csv|All files|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        MergeRows(CsvTable.Read(File.ReadAllText(dlg.FileName), ','));
    }

    void PasteTable()
    {
        if (_profile == null) return;
        string text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Clipboard is empty. Copy a table with a header row from Excel first.");
        MergeRows(CsvTable.Read(text, text.Contains('\t') ? '\t' : ','));
    }

    void MergeRows(List<PointRow> rows)
    {
        var r = MessageBox.Show($"{rows.Count} rows read.\n\nYes = replace the current points\nNo = append to them",
                                "Import", MessageBoxButtons.YesNoCancel);
        if (r == DialogResult.Cancel) return;
        if (r == DialogResult.Yes) _rows.Clear();
        foreach (var p in rows) _rows.Add(p);
        ValidateProfile();
    }

    void ExportCsv()
    {
        if (_profile == null) return;
        using var dlg = new SaveFileDialog { Filter = "CSV files|*.csv", FileName = $"{_profile.FbName}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(dlg.FileName, CsvTable.Write(_rows));
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

    static Button  Btn(string text, bool enabled = true) => new() { Text = text, AutoSize = true, Enabled = enabled };
    static Label   Lbl(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(8, 8, 2, 0) };
    static Control Gap()         => new Label { Width = 16 };

    static void Guard(Action a)
    {
        try { a(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "XAE", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
