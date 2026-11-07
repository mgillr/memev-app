// SPDX-License-Identifier: Apache-2.0
// Import history on the trial core's own list (REV2-SCREENS.md 5.2): `import scan --json` finds every source, each shown with a
// tick (pre-ticked when the core marks it), its count, size and dates; one sheet confirms the chosen ones; then
// `import --source ID ... --yes` runs in the background with its progress shown, because a large history takes minutes.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Memev.Desktop;

/// One source `import scan --json` lists.
sealed record ScanSource(string Id, string App, string Kind, string Where, int? Count, long Bytes, string First, string Last, bool Marked, int Projects, string Unit);

static partial class ImportScan
{
  /// The sources on this computer. The scan imports nothing, needs no vault and does not stop Memev.
  public static (List<ScanSource> Sources, bool Read) Read()
  {
    var list = new List<ScanSource>();
    if (CoreCall.Json(new[] { "import", "scan", "--json" }, 120000) is not System.Text.Json.Nodes.JsonObject o || o["sources"] is not System.Text.Json.Nodes.JsonArray a) return (list, false);
    foreach (var s in a.OfType<System.Text.Json.Nodes.JsonObject>())
    {
      string id = J.Str(s["id"]);
      if (id.Length == 0 || id.Length > 400) continue;
      string count = J.Str(s["count"]);
      int? n = int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out int c) ? c : null;
      long bytes = long.TryParse(J.Str(s["bytes"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out long b) ? b : 0;
      string kind = J.Str(s["kind"]);
      string unit = J.Str(s["unit"]);
      if (unit.Length == 0) unit = kind == "files" ? "files" : kind == "export" ? "conversations" : "sessions";
      list.Add(new ScanSource(id, J.Str(s["app"]) is { Length: > 0 } app ? app : id, kind, Paths.Show(J.Str(s["path"])), n, bytes, Day(J.Str(s["first"])), Day(J.Str(s["last"])),
                              J.True(s["marked"]), J.Arr(s["projects"]).Count, unit));
    }
    return (list, true);
  }

  static string Day(string iso) => DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime d) ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "";

  public static string Size(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.#} GB" : b >= 1L << 20 ? $"{b / (double)(1L << 20):0.#} MB" : b >= 1024 ? $"{b / 1024} KB" : $"{b} bytes";

  public static string Line(ScanSource s)
  {
    var parts = new List<string> { s.Count is int n ? $"{n.ToString("N0", CultureInfo.InvariantCulture)} {(n == 1 ? s.Unit.TrimEnd('s') : s.Unit)}" : "not counted yet", Size(s.Bytes) };
    if (s.First.Length > 0 && s.Last.Length > 0) parts.Add(s.First == s.Last ? s.First : $"{s.First} to {s.Last}");
    if (s.Projects > 1) parts.Add($"{s.Projects} projects");
    if (s.Where.Length > 0) parts.Add(s.Where);
    return string.Join(" · ", parts);
  }

  [GeneratedRegex(@"Imported (\d+) item\(s\) as (\S+)")]
  public static partial Regex ImportedLine();
}

sealed partial class MainWindow
{
  List<ScanSource> scanSources = new();
  bool scanRead, importRunning;
  readonly Dictionary<string, bool> importTicks = new();

  Task LoadScan()
  {
    return Task.Run(ImportScan.Read).ContinueWith((t) =>
    {
      (scanSources, scanRead) = t.Status == TaskStatus.RanToCompletion ? t.Result : (new List<ScanSource>(), false);

      importTicks.Clear();
      foreach (ScanSource s in scanSources) importTicks[s.Id] = s.Marked;
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  List<ScanSource> ImportTicked() => scanSources.Where((s) => importTicks.GetValueOrDefault(s.Id)).ToList();

  string ImportLabel()
  {
    int n = ImportTicked().Count;
    return n == 1 ? "Import 1 source" : $"Import {n} sources";
  }

  /// The sources with their ticks; changed calls back when a tick changes.
  ListBoxView ScanList(Action changed)
  {
    var list = new ListBoxView();
    if (!scanRead) list.Add(new RowView(null, "The list of history on this computer did not answer."));
    else if (scanSources.Count == 0) list.Add(new RowView(GlyphKind.Off, "No chat history was found on this computer."));
    foreach (ScanSource s in scanSources)
    {
      var box = new CheckBox { IsChecked = importTicks.GetValueOrDefault(s.Id), VerticalAlignment = VerticalAlignment.Center };
      AutomationProperties.SetName(box, s.App);
      string id = s.Id;
      box.Checked += (_, _) => { importTicks[id] = true; changed(); };
      box.Unchecked += (_, _) => { importTicks[id] = false; changed(); };
      list.Add(new RowView(null, s.App, ImportScan.Line(s), technical ? Paths.Show(s.Id) : "", tech: technical, lead: box));
    }
    return list;
  }

  /// From Memory, Search, Import: the sources in a sheet, then the confirmation.
  void ScanSheet()
  {
    Present(new SheetModel { Title = "Bring in your AI chat history", Note = "Reading what your AI apps keep on this computer. Nothing is imported yet.", Source = "memev import scan --json" });
    LoadScan().ContinueWith((_) =>
    {
      if (!sheet.IsOpen || sheet.Model?.Title != "Bring in your AI chat history") return;
      MButton? main = null;
      var list = ScanList(() => { if (main != null) { main.Text = ImportLabel(); main.IsEnabled = ImportTicked().Count > 0; } });
      Present(new SheetModel
      {
        Title = "Bring in your AI chat history",
        Note = "Tick what to bring in. The ticked ones are small enough to bring in at once; a large history takes longer. Each import can be undone.",
        Fields = { list }, Source = "memev import scan --json", Main = ImportLabel(),
        OnMain = (h) => { if (ImportTicked().Count > 0) ConfirmImport(ImportTicked(), null); },
      });
      main = sheet.MainButton;
      if (main != null) main.IsEnabled = ImportTicked().Count > 0;
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  /// The one confirmation sheet: what is brought in, and what happens while it runs.
  void ConfirmImport(List<ScanSource> chosen, Action? started)
  {
    string[] args = new[] { "import" }.Concat(chosen.SelectMany((s) => new[] { "--source", s.Id })).Append("--yes").ToArray();
    Present(new SheetModel
    {
      Title = chosen.Count == 1 ? $"Import {chosen[0].App}?" : $"Import {chosen.Count} sources?",
      Note = "Memev reads these and writes one note per conversation into your memory. Nothing leaves this computer and no AI model is used. Memev stops while it imports and starts again when it is done; a large history can take a few minutes. You can undo the import later.",
      Rows = chosen.Select((s) => new DetailRow(s.App, ImportScan.Line(s), Glyph: GlyphKind.Ok)).ToList(),
      Source = "memev import --source <id> ... --yes", Main = "Import",
      OnMain = (h) => { StartImport(args, chosen.Count); started?.Invoke(); },
    });
  }

  /// Runs the import off the window's thread. The sheet shows a moving bar and the time so far; closing it leaves the import
  /// running, and the top bar's status line keeps the count. The core prints no progress of its own, so no share is shown.
  void StartImport(string[] args, int sources)
  {
    if (importRunning) return;
    importRunning = true;
    var bar = new ProgressBar { IsIndeterminate = true, Height = 4 };
    bar.SetResourceReference(ForegroundProperty, "accent");
    bar.SetResourceReference(BackgroundProperty, "raise");
    bar.BorderThickness = new Thickness(0);
    var elapsed = Ui.Text("Started just now.", TypeStyle.Body, "sub");
    Present(new SheetModel
    {
      Title = "Importing your history",
      Note = $"Memev is bringing in {(sources == 1 ? "1 source" : $"{sources} sources")}. You can close this and keep working; the top bar shows when it is done.",
      Fields = { bar, elapsed }, Source = "memev " + string.Join(' ', args.Select((a) => a.Contains(' ') ? $"\"{a}\"" : a)),
    });
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    timer.Tick += (_, _) =>
    {
      string t = clock.Elapsed.TotalMinutes >= 1 ? $"{(int)clock.Elapsed.TotalMinutes} min {clock.Elapsed.Seconds} s" : $"{clock.Elapsed.Seconds} s";
      elapsed.Text = $"Running for {t}.";
      statusLine.Text = $"Importing your history · {t}";
    };
    timer.Start();
    Task.Run(() => CoreCall.Run(args, 60 * 60 * 1000)).ContinueWith((t) =>
    {
      timer.Stop();
      importRunning = false;
      var (code, output) = t.Status == TaskStatus.RanToCompletion ? t.Result : (-1, "");
      string answer = code == 126 ? "" : Pages.Answer(output);
      var m = ImportScan.ImportedLine().Match(output);
      string title = code != 0 ? "The import did not finish" : m.Success ? $"Imported {m.Groups[1].Value} items" : "Nothing new to bring in";
      statusLine.Text = title + ".";
      bool showing = !sheet.IsOpen || sheet.Model?.Title == "Importing your history";
      if (showing)
      {
        string note = code == 0 ? (m.Success ? "It is in your memory now. Undo takes it all back out." : "Everything found was already in your memory.") : code == 126 ? Refused : $"The core stopped with code {code}. Its answer is below.";
        string id = m.Success ? m.Groups[2].Value : "";
        Present(new SheetModel { Title = title, Note = note, Source = "memev import --source ... --yes", Main = "Close", Second = id.Length > 0 ? "Undo this import" : null, OnSecond = (_) => UndoSheet(id) });
        sheet.ShowOutput(answer);
      }
      Reload();
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  // ---- the setup step

  void GuideLoadScan()
  {
    guideRows.Children.Clear();
    guideRows.Children.Add(Note("Reading what your AI apps keep on this computer."));
    primary.IsEnabled = false;
    guideBusy = true;
    LoadScan().ContinueWith((_) =>
    {
      guideBusy = false;
      guideRows.Children.Clear();
      guideRows.Children.Add(ScanList(GuideImportLabel));
      GuideImportLabel();
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  void GuideImportLabel()
  {
    primary.Text = ImportTicked().Count == 0 ? "Continue" : ImportLabel();
    primary.IsEnabled = true;
    if (inGuide) Render();
  }
}
