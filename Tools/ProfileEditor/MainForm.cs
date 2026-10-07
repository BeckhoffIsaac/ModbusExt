using System.ComponentModel;

namespace ModbusExt.ProfileEditor;

sealed class MainForm : Form
{
    // Toolbar
    readonly ToolStrip _tools = new() { GripStyle = ToolStripGripStyle.Hidden, RenderMode = ToolStripRenderMode.System, Padding = new Padding(6, 2, 6, 2) };
    readonly ToolStripDropDownButton _tsNew = new("New") { DisplayStyle = ToolStripItemDisplayStyle.Text };
    readonly ToolStripMenuItem _tsNewProfile = new("Profile…");
    readonly ToolStripMenuItem _tsNewChannel = new("Channel…") { Enabled = false };
    readonly ToolStripMenuItem _tsNewDevice  = new("Device…")  { Enabled = false };
    readonly ToolStripButton _tsSave = Tb("Save"), _tsRevert = Tb("Revert"), _tsUndo = Tb("Undo save");
    readonly ToolStripButton _tsAdd = Tb("Add point"), _tsDel = Tb("Delete"), _tsUp = Tb("Up"), _tsDown = Tb("Down");
    readonly ToolStripDropDownButton _tsMore = new("More") { DisplayStyle = ToolStripItemDisplayStyle.Text };
    readonly ToolStripMenuItem _tsPreview = new("Preview code"), _tsImport = new("Import CSV…"), _tsPaste = new("Paste table"), _tsExport = new("Export CSV…");
    readonly ToolStripButton _tsBuild = new("Build after save") { CheckOnClick = true, DisplayStyle = ToolStripItemDisplayStyle.Text };

    // Status bar
    readonly StatusStrip _status = new() { SizingGrip = false };
    readonly ToolStripStatusLabel _ssSolution = new("Not connected") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripStatusLabel _ssState = new("");
    readonly ToolStripDropDownButton _ssInstance = new("XAE") { DisplayStyle = ToolStripItemDisplayStyle.Text };

    // Navigation
    readonly SplitContainer _split = new() { Dock = DockStyle.Fill, SplitterDistance = 280, FixedPanel = FixedPanel.Panel1 };
    readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, BorderStyle = BorderStyle.None, ShowLines = false, FullRowSelect = true };
    readonly TreeNode _nProfiles = new("Profiles"), _nChannels = new("Channels"), _nDevices = new("Devices");

    // Profile card
    readonly TableLayoutPanel _card = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(8, 6, 8, 2) };
    readonly FlowLayoutPanel _basic = new() { AutoSize = true, WrapContents = true, Dock = DockStyle.Top };
    readonly FlowLayoutPanel _advanced = new() { AutoSize = true, WrapContents = true, Dock = DockStyle.Top, Visible = false };
    readonly LinkLabel _advToggle = new() { Text = "Advanced ▾", AutoSize = true, Margin = new Padding(8, 8, 0, 0), LinkBehavior = LinkBehavior.HoverUnderline };
    readonly TextBox       _modelName  = new() { Width = 180 };
    readonly ComboBox      _wordOrder  = MakeCombo(Enums.WordOrders[1..]);
    readonly ComboBox      _addressing = MakeCombo(Enums.Addressing);
    readonly NumericUpDown _maxRegs    = new() { Minimum = 1, Maximum = 125, Value = 125, Width = 60 };
    readonly NumericUpDown _maxGap     = new() { Minimum = 0, Maximum = 125, Value = 8, Width = 60 };
    readonly CheckBox      _fc6        = new() { Text = "FC6 only", AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
    readonly CheckBox      _fc16       = new() { Text = "FC16 only", AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
    readonly CheckBox      _swap       = new() { Text = "String byte swap", AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
    readonly TextBox       _slowPoll   = new() { Width = 80 };

    // Grid and issues
    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BorderStyle = BorderStyle.None,
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, BackgroundColor = Color.White,
        EnableHeadersVisualStyles = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        AllowUserToResizeRows = false
    };
    readonly Panel   _issuesPanel = new() { Dock = DockStyle.Bottom, Height = 120, Visible = false, Padding = new Padding(8, 4, 8, 4) };
    readonly ListBox _issues = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, ForeColor = Color.Firebrick };
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
        Font = new Font("Segoe UI", 10f);
        Width = 1400; Height = 850;
        BackColor = Color.White;

        // Toolbar
        _tsNew.DropDownItems.AddRange(new ToolStripItem[] { _tsNewProfile, _tsNewChannel, _tsNewDevice });
        _tsMore.DropDownItems.AddRange(new ToolStripItem[] { _tsPreview, _tsImport, _tsPaste, _tsExport });
        _tools.Items.AddRange(new ToolStripItem[]
        {
            _tsNew, _tsSave, _tsRevert, _tsUndo, new ToolStripSeparator(),
            _tsAdd, _tsDel, _tsUp, _tsDown, new ToolStripSeparator(),
            _tsMore, new ToolStripSeparator(), _tsBuild
        });

        // Status bar
        _status.Items.AddRange(new ToolStripItem[] { _ssSolution, _ssState, _ssInstance });

        // Tree
        _tree.Nodes.AddRange(new[] { _nProfiles, _nChannels, _nDevices });
        _split.Panel1.Controls.Add(_tree);
        _split.Panel1.Padding = new Padding(4);

        // Card
        _basic.Controls.AddRange(new Control[] { Lbl("Model"), _modelName, Lbl("Word order"), _wordOrder, Lbl("Addressing"), _addressing, _advToggle });
        _advanced.Controls.AddRange(new Control[]
        {
            Lbl("Max regs/read"), _maxRegs, Lbl("Max gap"), _maxGap, _fc6, _fc16, _swap, Lbl("Slow poll"), _slowPoll
        });
        _card.Controls.Add(_advanced);
        _card.Controls.Add(_basic);

        // Editor panel
        BuildGrid();
        _issuesPanel.Controls.Add(_issues);
        _split.Panel2.Controls.Add(_grid);
        _split.Panel2.Controls.Add(_issuesPanel);
        _split.Panel2.Controls.Add(_card);

        Controls.Add(_split);
        Controls.Add(_status);
        Controls.Add(_tools);

        // Wiring
        _tsNewProfile.Click += (_, _) => Guard(NewProfile);
        _tsSave.Click    += (_, _) => Guard(Save);
        _tsRevert.Click  += (_, _) => Guard(LoadCurrent);
        _tsUndo.Click    += (_, _) => Guard(UndoSave);
        _tsPreview.Click += (_, _) => Guard(Preview);
        _tsImport.Click  += (_, _) => Guard(ImportCsv);
        _tsPaste.Click   += (_, _) => Guard(PasteTable);
        _tsExport.Click  += (_, _) => Guard(ExportCsv);
        _tsAdd.Click     += (_, _) => { if (_profile != null) { _rows.Add(new PointRow { Name = "NewPoint", Address = 40001 }); ValidateProfile(); } };
        _tsDel.Click     += (_, _) => { if (_grid.CurrentRow != null) { _rows.RemoveAt(_grid.CurrentRow.Index); ValidateProfile(); } };
        _tsUp.Click      += (_, _) => MoveRow(-1);
        _tsDown.Click    += (_, _) => MoveRow(+1);
        _advToggle.LinkClicked += (_, _) => { _advanced.Visible = !_advanced.Visible; _advToggle.Text = _advanced.Visible ? "Advanced ▴" : "Advanced ▾"; };
        _tree.AfterSelect += (_, e) => Guard(() => { if (e.Node?.Tag is ProfilePou p) LoadProfile(p); else ClearEditor(); });

        _grid.CellValueChanged += (_, _) => { if (!_loading) Guard(ValidateProfile); };
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.DataError += (_, e) => { e.ThrowException = false; };
        _grid.CellContentClick += (_, e) => { if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "Details") Guard(() => EditDetails(e.RowIndex)); };
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ScaleMark")
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = _rows[e.RowIndex].ScalingSummary;
        };

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
        var hdr = new DataGridViewCellStyle { BackColor = Color.FromArgb(245, 245, 245), ForeColor = Color.FromArgb(60, 60, 60), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Padding = new Padding(4, 2, 4, 2) };
        _grid.ColumnHeadersDefaultCellStyle = hdr;
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(250, 250, 250) };
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        _grid.DefaultCellStyle.Padding = new Padding(2);

        var mark = new DataGridViewTextBoxColumn { Name = "ScaleMark", DataPropertyName = "ScaleMark", HeaderText = "Scale", Width = 52, ReadOnly = true };
        mark.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        mark.DefaultCellStyle.ForeColor = Color.FromArgb(0, 102, 204);
        var details = new DataGridViewButtonColumn { Name = "Details", HeaderText = "", Text = "…", UseColumnTextForButtonValue = true, Width = 36, FlatStyle = FlatStyle.Flat };
        var comment = Col("Comment", 200);
        comment.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        _grid.Columns.AddRange(Col("Name", 140), Col("Address", 90), Combo("Type", Enums.Types, 110), mark, Combo("Poll", Enums.Polls, 80), comment, details);
        _grid.DataSource = _rows;
    }

    // ---- XAE ----

    void RefreshInstances()
    {
        _dtes = RunningObjects.ListDte();
        _ssInstance.DropDownItems.Clear();
        foreach (var d in _dtes)
        {
            var item = new ToolStripMenuItem(d.Name) { Tag = d.Obj };
            item.Click += (_, _) => Guard(() => Connect(d.Obj));
            _ssInstance.DropDownItems.Add(item);
        }
        _ssInstance.DropDownItems.Add(new ToolStripSeparator());
        var refresh = new ToolStripMenuItem("Refresh instances");
        refresh.Click += (_, _) => Guard(RefreshInstances);
        _ssInstance.DropDownItems.Add(refresh);
        if (_dtes.Count > 0) Connect(_dtes[0].Obj);
        else _ssSolution.Text = "No running XAE found";
    }

    void Connect(object dte)
    {
        ClearEditor();
        _session = new XaeSession(dte);
        _ssSolution.Text = _session.SolutionName;
        _found = _session.FindProfiles();
        _nProfiles.Nodes.Clear();
        foreach (var p in _found) _nProfiles.Nodes.Add(new TreeNode($"{p.PlcName} / {p.Name}") { Tag = p });
        _tree.ExpandAll();
        _tsNew.Enabled = true;
        if (_found.Count == 0) SetState("No profiles in this solution — New › Profile…", false);
    }

    void ClearEditor()
    {
        _loading = true;
        _current = null; _profile = null;
        _rows.Clear();
        _modelName.Text = ""; _slowPoll.Text = "";
        _loading = false;
        _issues.Items.Clear(); _issuesPanel.Visible = false;
        _card.Enabled = _grid.Enabled = false;
        _tsSave.Enabled = _tsRevert.Enabled = _tsPreview.Enabled = _tsImport.Enabled = _tsPaste.Enabled = _tsExport.Enabled = false;
        _tsAdd.Enabled = _tsDel.Enabled = _tsUp.Enabled = _tsDown.Enabled = false;
        SetState("", true);
    }

    void LoadCurrent() { if (_current != null) LoadProfile(_current); }

    void LoadProfile(ProfilePou pou)
    {
        _current = pou;
        var texts = XaeSession.Read(pou);
        _profile = ProfileParser.Parse(texts.Declaration, texts.Register);

        _loading = true;
        _modelName.Text          = _profile.Model;
        _wordOrder.SelectedItem  = _profile.WordOrder;
        _addressing.SelectedItem = _profile.Addressing;
        _maxRegs.Value           = Math.Clamp(_profile.MaxRegsPerRead, 1u, 125u);
        _maxGap.Value            = Math.Clamp(_profile.MaxGap, 0u, 125u);
        _fc6.Checked = _profile.Fc6Only; _fc16.Checked = _profile.Fc16Only; _swap.Checked = _profile.StringByteSwap;
        _slowPoll.Text = _profile.SlowPoll;
        _rows.Clear();
        foreach (var p in _profile.Points) _rows.Add(p);
        _loading = false;

        _card.Enabled = _grid.Enabled = true;
        _tsRevert.Enabled = _tsPreview.Enabled = _tsImport.Enabled = _tsPaste.Enabled = _tsExport.Enabled = true;
        _tsAdd.Enabled = _tsDel.Enabled = _tsUp.Enabled = _tsDown.Enabled = true;
        ValidateProfile();
        foreach (var w in _profile.Warnings) { _issues.Items.Add("Note: " + w); _issuesPanel.Visible = true; }
    }

    void Save()
    {
        if (_profile == null || _current == null || _session == null) return;
        ValidateProfile();
        if (!_tsSave.Enabled) return;
        _undoTexts = XaeSession.Read(_current);
        XaeSession.Write(_current, ProfileWriter.Declaration(_profile), ProfileWriter.Body(), ProfileWriter.Register(_profile));
        _tsUndo.Enabled = true;
        LoadProfile(_current);
        SetState("Saved to XAE", true);
        if (_tsBuild.Checked) _session.BuildSolution();
    }

    void UndoSave()
    {
        if (_undoTexts == null || _current == null) return;
        XaeSession.Write(_current, _undoTexts.Declaration, _undoTexts.Body, _undoTexts.Register ?? "");
        _undoTexts = null;
        _tsUndo.Enabled = false;
        LoadProfile(_current);
    }

    void NewProfile()
    {
        if (_session == null) return;
        var plcs = _session.PlcProjects();
        if (plcs.Count == 0) throw new InvalidOperationException("No PLC projects in the solution.");

        using var dlg = new Form
        {
            Text = "New profile", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, Font = Font
        };
        var plc   = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        plc.Items.AddRange(plcs.ToArray()); plc.SelectedIndex = 0;
        var model = new TextBox { Width = 300 };
        var fb    = new TextBox { Width = 300, Text = "FB_Mb_" };
        model.TextChanged += (_, _) => fb.Text = "FB_Mb_" + new string(model.Text.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        var ok = new Button { Text = "Create", DialogResult = DialogResult.OK, AutoSize = true };
        var table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(12) };
        table.Controls.Add(Lbl("PLC project"), 0, 0); table.Controls.Add(plc, 1, 0);
        table.Controls.Add(Lbl("Model"), 0, 1);       table.Controls.Add(model, 1, 1);
        table.Controls.Add(Lbl("FB name"), 0, 2);     table.Controls.Add(fb, 1, 2);
        table.Controls.Add(ok, 1, 3);
        dlg.Controls.Add(table); dlg.AcceptButton = ok;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var m = new ProfileModel { FbName = fb.Text.Trim(), Model = model.Text.Trim() };
        m.Points.Add(new PointRow { Name = "Point1", Address = 40001, Comment = "replace me" });
        var issues = ProfileValidator.Validate(m);
        if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues.Select(i => i.Message)));
        if (_found.Any(p => p.Name.Equals(m.FbName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{m.FbName} already exists.");

        var pou = _session.CreateProfile((string)plc.SelectedItem!, m.FbName, ProfileWriter.Declaration(m), ProfileWriter.Body(), ProfileWriter.Register(m));
        _found.Add(pou);
        var node = new TreeNode($"{pou.PlcName} / {pou.Name}") { Tag = pou };
        _nProfiles.Nodes.Add(node);
        _tree.SelectedNode = node;
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
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Clipboard is empty. Copy a table with a header row from Excel first.");
        MergeRows(CsvTable.Read(text, text.Contains('\t') ? '\t' : ','));
    }

    void MergeRows(List<PointRow> rows)
    {
        var r = MessageBox.Show(this, $"{rows.Count} rows read.\n\nYes = replace the current points\nNo = append to them", "Import", MessageBoxButtons.YesNoCancel);
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

    void EditDetails(int rowIndex)
    {
        using var d = new PointDetailsDialog(_rows[rowIndex]);
        if (d.ShowDialog(this) != DialogResult.OK) return;
        _rows.ResetItem(rowIndex);
        ValidateProfile();
    }

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
            else row.ErrorText = i.Message;        // Columns hidden in the dialog mark the row
        }
        _issuesPanel.Visible = issues.Count > 0;
        SetState(issues.Count == 0 ? $"OK · {_rows.Count} points" : $"{issues.Count} issue{(issues.Count == 1 ? "" : "s")}", issues.Count == 0);
        _tsSave.Enabled = issues.Count == 0 && _current != null;
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
        var f = new Form { Text = $"Generated: {_profile.FbName}", Width = 950, Height = 700, Font = Font };
        f.Controls.Add(new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10),
            Text = ProfileWriter.Declaration(_profile) + "\r\n(* ---- Body ---- *)\r\n" + ProfileWriter.Body()
                 + "\r\n(* ---- Register ---- *)\r\n" + ProfileWriter.Register(_profile)
        });
        f.Show(this);
    }

    // ---- Helpers ----

    void SetState(string text, bool ok)
    {
        _ssState.Text = text;
        _ssState.ForeColor = ok ? Color.FromArgb(0, 120, 60) : Color.Firebrick;
    }

    static ToolStripButton Tb(string text) => new(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };

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
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        c.Items.AddRange(items);
        c.SelectedIndex = 0;
        return c;
    }

    static Label Lbl(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(8, 8, 2, 0), ForeColor = Color.FromArgb(80, 80, 80) };

    static void Guard(Action a)
    {
        try { a(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "XAE", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}