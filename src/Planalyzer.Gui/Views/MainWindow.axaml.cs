using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Planalyzer.Comparison;
using Planalyzer.Core.Analysis;
using Planalyzer.Core.Parsing;
using Planalyzer.Execution;

namespace Planalyzer.Gui.Views;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<MultiDbRow> _multiDbRows = new();
    private readonly ObservableCollection<QueryRevision> _historyRows = new();
    private readonly List<string> _analyzeFiles = new();
    private string? _cmpEstFile;
    private string? _cmpActFile;

    public MainWindow()
    {
        InitializeComponent();

        // Analyze tab
        AnalyzePickButton.Click += AnalyzePick_Click;
        AnalyzeRunButton.Click += AnalyzeRun_Click;

        // Compare tab
        CmpEstButton.Click += async (_, _) => await CmpPick(true);
        CmpActButton.Click += async (_, _) => await CmpPick(false);
        CmpRunButton.Click += CmpRun_Click;

        // Multi-DB tab
        MdGrid.ItemsSource = _multiDbRows;
        MdAddButton.Click += MdAdd_Click;
        MdClearButton.Click += (_, _) => _multiDbRows.Clear();
        MdRunButton.Click += MdRun_Click;

        // Live tab
        LiveHistoryGrid.ItemsSource = _historyRows;
        LiveRunButton.Click += LiveRun_Click;
        LiveRefreshHistoryButton.Click += (_, _) => RefreshHistory();
        LiveLoadRevisionButton.Click += LiveLoadRevision_Click;
        LiveRollbackButton.Click += LiveRollback_Click;
    }

    // ---------- ANALYZE ----------
    private async void AnalyzePick_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Seleziona uno o più file .sqlplan",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Showplan XML") { Patterns = new[] { "*.sqlplan", "*.xml" } },
                FilePickerFileTypes.All
            }
        });
        if (files.Count == 0) return;

        _analyzeFiles.Clear();
        foreach (var f in files) _analyzeFiles.Add(f.Path.LocalPath);
        AnalyzeFileLabel.Text = string.Join(", ", _analyzeFiles.Select(System.IO.Path.GetFileName));
    }

    private void AnalyzeRun_Click(object? sender, RoutedEventArgs e)
    {
        if (_analyzeFiles.Count == 0) { AnalyzeOutput.Text = "Seleziona almeno un file."; return; }
        var level = (AnalyzeLevelCombo.SelectedIndex == 1) ? AudienceLevel.Expert : AudienceLevel.Beginner;
        var sb = new StringBuilder();
        foreach (var path in _analyzeFiles)
        {
            try
            {
                var plan = ShowplanParser.ParseFile(path);
                var rep = PlanAnalyzer.Analyze(plan);
                sb.AppendLine(TextRenderer.Render(rep, level));
                sb.AppendLine(new string('-', 80));
            }
            catch (Exception ex)
            {
                sb.AppendLine($"ERRORE su {path}: {ex.Message}");
            }
        }
        AnalyzeOutput.Text = sb.ToString();
    }

    // ---------- COMPARE ----------
    private async Task CmpPick(bool estimated)
    {
        var f = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = estimated ? "Piano stimato" : "Piano effettivo",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Showplan XML") { Patterns = new[] { "*.sqlplan", "*.xml" } } }
        });
        if (f.Count == 0) return;
        var p = f[0].Path.LocalPath;
        if (estimated) { _cmpEstFile = p; CmpEstLabel.Text = System.IO.Path.GetFileName(p); }
        else           { _cmpActFile = p; CmpActLabel.Text = System.IO.Path.GetFileName(p); }
    }

    private void CmpRun_Click(object? sender, RoutedEventArgs e)
    {
        if (_cmpEstFile is null || _cmpActFile is null)
        {
            CmpOutput.Text = "Seleziona entrambi i piani.";
            return;
        }
        try
        {
            var pe = ShowplanParser.ParseFile(_cmpEstFile);
            var pa = ShowplanParser.ParseFile(_cmpActFile);
            var rep = EstimatedVsActual.Compare(pe, pa);
            CmpOutput.Text = EstimatedVsActual.Render(rep);
        }
        catch (Exception ex) { CmpOutput.Text = "ERRORE: " + ex.Message; }
    }

    // ---------- MULTI-DB ----------
    private async void MdAdd_Click(object? sender, RoutedEventArgs e)
    {
        var f = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Aggiungi piano",
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("Showplan XML") { Patterns = new[] { "*.sqlplan", "*.xml" } } }
        });
        foreach (var x in f)
            _multiDbRows.Add(new MultiDbRow
            {
                Label = System.IO.Path.GetFileNameWithoutExtension(x.Path.LocalPath),
                Path = x.Path.LocalPath,
            });
    }

    private void MdRun_Click(object? sender, RoutedEventArgs e)
    {
        if (_multiDbRows.Count < 2) { MdOutput.Text = "Servono almeno 2 piani."; return; }
        try
        {
            var variants = _multiDbRows
                .Select(r => new DbVariant(r.Label, ShowplanParser.ParseFile(r.Path)))
                .ToList();
            var rep = MultiDbComparer.Compare(variants);
            MdOutput.Text = MultiDbComparer.Render(rep);
        }
        catch (Exception ex) { MdOutput.Text = "ERRORE: " + ex.Message; }
    }

    // ---------- LIVE ----------
    private async void LiveRun_Click(object? sender, RoutedEventArgs e)
    {
        var conn = LiveConn.Text?.Trim();
        var slug = LiveSlug.Text?.Trim();
        var sql = LiveSql.Text;
        var dbPath = string.IsNullOrWhiteSpace(LiveHistoryDb.Text) ? "planalyzer.db" : LiveHistoryDb.Text!.Trim();

        if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(sql))
        {
            LiveOutput.Text = "Compila connection string, slug e SQL.";
            return;
        }
        var mode = (LiveMode.SelectedIndex == 1) ? CaptureMode.EstimatedPlanOnly : CaptureMode.ActualPlan;
        var note = LiveNote.Text;

        LiveRunButton.IsEnabled = false;
        LiveOutput.Text = "Esecuzione in corso…";
        try
        {
            using var wb = new QueryWorkbench(conn, dbPath);
            var rev = await wb.TryRunAsync(slug!, sql!, note, mode, 120);
            var sb = new StringBuilder();
            sb.AppendLine($"Revision #{rev.RevisionNo} salvata.");
            sb.AppendLine($"  duration={rev.DurationMs}ms cpu={rev.CpuMs}ms logical={rev.LogicalReads} physical={rev.PhysicalReads} rows={rev.RowCount}");
            if (!string.IsNullOrEmpty(rev.Error)) sb.AppendLine("  ERROR: " + rev.Error);
            if (!string.IsNullOrEmpty(rev.PlanXml))
            {
                var plan = ShowplanParser.ParseString(rev.PlanXml);
                var rep = PlanAnalyzer.Analyze(plan);
                sb.AppendLine();
                sb.AppendLine(TextRenderer.Render(rep, AudienceLevel.Beginner));
            }
            LiveOutput.Text = sb.ToString();
            RefreshHistory();
        }
        catch (Exception ex) { LiveOutput.Text = "ERRORE: " + ex.Message; }
        finally { LiveRunButton.IsEnabled = true; }
    }

    private void RefreshHistory()
    {
        var slug = LiveSlug.Text?.Trim();
        var dbPath = string.IsNullOrWhiteSpace(LiveHistoryDb.Text) ? "planalyzer.db" : LiveHistoryDb.Text!.Trim();
        if (string.IsNullOrWhiteSpace(slug)) return;
        try
        {
            using var store = new QueryHistoryStore(dbPath);
            _historyRows.Clear();
            foreach (var r in store.List(slug!)) _historyRows.Add(r);
        }
        catch (Exception ex) { LiveOutput.Text = "ERRORE history: " + ex.Message; }
    }

    private void LiveLoadRevision_Click(object? sender, RoutedEventArgs e)
    {
        if (LiveHistoryGrid.SelectedItem is QueryRevision rev)
        {
            LiveSql.Text = rev.Sql;
            LiveNote.Text = $"derivata da rev #{rev.RevisionNo}";
        }
    }

    private void LiveRollback_Click(object? sender, RoutedEventArgs e)
    {
        if (LiveHistoryGrid.SelectedItem is not QueryRevision rev) return;
        var slug = LiveSlug.Text?.Trim();
        var dbPath = string.IsNullOrWhiteSpace(LiveHistoryDb.Text) ? "planalyzer.db" : LiveHistoryDb.Text!.Trim();
        if (string.IsNullOrWhiteSpace(slug)) return;
        try
        {
            using var wb = new QueryWorkbench(LiveConn.Text ?? "Server=.", dbPath);
            var newRev = wb.Rollback(slug!, rev.RevisionNo, "rollback da GUI");
            if (newRev is null) { LiveOutput.Text = "Rollback fallito."; return; }
            LiveOutput.Text = $"Creata revisione #{newRev.RevisionNo} (rollback a #{rev.RevisionNo}). Premi 'Esegui + analizza' per misurare di nuovo.";
            RefreshHistory();
        }
        catch (Exception ex) { LiveOutput.Text = "ERRORE: " + ex.Message; }
    }

    public sealed class MultiDbRow
    {
        public string Label { get; set; } = "";
        public string Path { get; set; } = "";
    }
}
