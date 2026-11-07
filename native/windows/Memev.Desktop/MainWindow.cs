// SPDX-License-Identifier: Apache-2.0
// The rev2 shell on Windows (native/REV2-SCREENS.md section 1), with the same structure as native/macos/src/App.swift: the
// sidebar with the health button and its 14 pips, the menu, Help and support and the foot line; the top bar; the stopped
// banner; each screen's header, tabs and content; the detail panel; sheets. The window shows at once with each part saying
// "Reading Memev."; the core is read off the window's thread and each part fills when its answer is in. It runs a command that
// changes something only after a sheet that names what changes (the owner's ruling D-1), and sends nothing off this computer.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Memev.Reader;

namespace Memev.Desktop;

/// One screen's view: its header and its content, kept while the window is open.
sealed class ScreenView
{
  public readonly StackPanel Root = new();
  public readonly PageHeader Header = new();
  public readonly StackPanel Content = new() { Margin = new Thickness(0, 40, 0, 0) };
  public readonly MButton Refresh;
  public Dictionary<string, StackPanel> TabViews = new();

  public ScreenView(MButton refresh)
  {
    Refresh = refresh;
    Root.Children.Add(Header);
    Root.Children.Add(Content);
  }
}

sealed record Pip(string Name, GlyphKind Kind, string State, string Tip); // state: fine, needs, fault, off, unread

sealed partial class MainWindow : Window
{
  public static readonly RoutedCommand RefreshCommand = new("Refresh", typeof(MainWindow));
  public static readonly RoutedCommand CommandFieldCommand = new("Search or run a command", typeof(MainWindow));

  readonly bool selfCheck = Environment.GetEnvironmentVariable("MEMEV_SELF_CHECK") != null;
  Glance glance = new();
  bool glanceRead, loading, starting;
  int readsDone, readsPending;
  readonly TaskCompletionSource firstReads = new();
  readonly DateTime launched = DateTime.UtcNow;
  int shownMs = -1;
  Action<int>? onStartDone;
  int attentionRows, checkRows;

  // What the core said, beyond the reader's glance.
  List<JsonObject> doctor = new();
  JsonObject? roster;
  string version = "", planLabel = "", projectName = "";
  readonly Dictionary<string, PageData> pageData = new();
  int pageLoad;

  // Navigation and this app's own settings.
  string current = "overview";
  int currentPage = -1;
  readonly Dictionary<string, string> tabFor = new();
  bool inGuide, technical;
  string appearance = "system";

  // The shell.
  readonly Border sidebar = Ui.Box("panel");
  readonly HealthButton healthButton = new();
  readonly List<NavButton> navButtons = new();
  readonly NavButton projectButton = new("", 28);
  readonly NavButton helpButton = new("Help and support");
  readonly TextBlock foot = Ui.Text("", TypeStyle.Small, "dim");
  readonly MButton crumbLink = new("Projects", ButtonStyleKind.Link);
  readonly TextBlock crumbSep = Ui.Text("›", TypeStyle.Body, "sub");
  readonly TextBlock crumb = Ui.Text("Overview", TypeStyle.Body, "text");
  readonly Border techChip = Ui.Box("raise", null, 4);
  readonly TextBlock statusLine = Ui.Text("", TypeStyle.Body, "sub");
  readonly TextBox command = new();
  readonly Border banner = Ui.Box("raise");
  MButton bannerStart = null!;
  readonly StackPanel pageStack = new() { Margin = new Thickness(36, 32, 36, 64) };
  readonly ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
  readonly DetailPanel detail = new();
  readonly Border detailEdge = Ui.Hairline("rule", true);
  readonly SheetHost sheet = new();
  RowView? selectedRow;
  readonly Dictionary<string, ScreenView> screens = new();
  /// Everything but the sheet: the amber rule for the window runs here, and a sheet keeps its own.
  readonly Grid shell = new();
  readonly TextBox search = new() { Width = 420, Height = 32, HorizontalAlignment = HorizontalAlignment.Left };
  readonly MButton primary = new("Continue", ButtonStyleKind.Secondary, 1);

  public MainWindow()
  {
    LoadSettings();
    Theme.Apply(Theme.Pick(appearance));
    Title = "Memev";
    MinWidth = 1024;
    MinHeight = 700;
    Width = 1280;
    Height = 800;
    WindowStartupLocation = WindowStartupLocation.CenterScreen;
    SetResourceReference(BackgroundProperty, "page");
    SetResourceReference(ForegroundProperty, "text");
    FontFamily = Fonts.Sans;

    BuildSidebar();
    var main = BuildMain();
    detail.Visibility = Visibility.Collapsed;
    detailEdge.Visibility = Visibility.Collapsed;
    detail.OnClose = CloseDetail;

    foreach (double w in new double[] { 240, 1, -1, 1, 400 })
      shell.ColumnDefinitions.Add(new ColumnDefinition { Width = w < 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
    var edge = Ui.Hairline("rule", true);
    var parts = new UIElement[] { sidebar, edge, main, detailEdge, detail };
    for (int i = 0; i < parts.Length; i++) { Grid.SetColumn(parts[i], i); shell.Children.Add(parts[i]); }
    var root = new Grid();
    root.Children.Add(shell);
    root.Children.Add(sheet);
    Content = root;

    primary.OnPress = PrimaryPressed;
    Screen("overview");
    Screen("health");
    Screen("guide");

    CommandBindings.Add(new CommandBinding(RefreshCommand, (_, _) => RefreshClicked()));
    InputBindings.Add(new KeyBinding(RefreshCommand, Key.R, ModifierKeys.Control));
    CommandBindings.Add(new CommandBinding(CommandFieldCommand, (_, _) => command.Focus()));
    InputBindings.Add(new KeyBinding(CommandFieldCommand, Key.K, ModifierKeys.Control));
    // Esc closes a sheet, then the detail panel (SH-21, 1.16).
    PreviewKeyDown += (_, e) =>
    {
      if (e.Key != Key.Escape) return;
      if (sheet.IsOpen) { sheet.Dismiss(); e.Handled = true; }
      else if (detail.Visibility == Visibility.Visible) { CloseDetail(); e.Handled = true; }
    };
    if (OperatingSystem.IsWindows())
      Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, _) => Dispatcher.BeginInvoke(new Action(() => { if (appearance == "system") Repaint(); }));

    Fill();
    if (WantGuide() && CoreCall.HaveBinary) GuideShow(0); else Open("overview");
    ContentRendered += (_, _) =>
    {
      if (shownMs < 0) shownMs = (int)(DateTime.UtcNow - launched).TotalMilliseconds;
      ReadFirst();
      if (selfCheck) { _ = SelfCheck(); return; }
      firstReads.Task.ContinueWith((_) =>
      {
        int ms = int.TryParse(Environment.GetEnvironmentVariable("MEMEV_POLL_MS"), out int v) ? v : 5000;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(200, ms)) };
        timer.Tick += (_, _) => { if (!inGuide) Reload(); };
        timer.Start();
      }, TaskScheduler.FromCurrentSynchronizationContext());
    };
  }

  // ---- this app's own settings, kept beside the first-launch marker

  static string ConfigDir()
  {
    string? cfg = Environment.GetEnvironmentVariable("MEMEV_CONFIG_DIR");
    return !string.IsNullOrEmpty(cfg) ? cfg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "memev");
  }

  static string SettingsPath() => Path.Combine(ConfigDir(), "desktop-settings.json");

  void LoadSettings()
  {
    try
    {
      if (JsonNode.Parse(File.ReadAllText(SettingsPath())) is JsonObject o)
      {
        string a = J.Str(o["appearance"]);
        if (a == "dark" || a == "light" || a == "system") appearance = a;
        technical = J.True(o["technical"]);
      }
    }
    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException) { }
  }

  void SaveSettings()
  {
    if (selfCheck) return;
    try
    {
      Directory.CreateDirectory(ConfigDir());
      File.WriteAllText(SettingsPath(), new JsonObject { ["appearance"] = appearance, ["technical"] = technical }.ToJsonString() + "\n");
    }
    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
  }

  void Repaint()
  {
    Theme.Apply(Theme.Pick(appearance));
    sheet.Paint();
    Render();
  }

  // ---- reading the core

  /// One read of the core off the window's thread; done runs on it with the answer.
  void ReadOff<T>(Func<T> work, Action<T> done)
  {
    readsPending++;
    Task.Run(work).ContinueWith((t) =>
    {
      if (t.Status == TaskStatus.RanToCompletion) done(t.Result);
      readsDone++;
      if (--readsPending == 0) firstReads.TrySetResult();
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  /// The six first reads: the glance (status, doctor and card through Memev.Reader), status, licence and plan, card, doctor and
  /// the roster. Each fills its own part of the window when it returns.
  void ReadFirst()
  {
    if (readsPending > 0 || readsDone > 0) return;
    loading = true;
    firstReads.Task.ContinueWith((_) => loading = false, TaskScheduler.FromCurrentSynchronizationContext());
    ReadOff(Read.Load, (g) => { glance = g; glanceRead = true; Fill(); });
    ReadOff(() => CoreCall.Json(new[] { "status", "--json" }) as JsonObject, (s) => { version = Plain(J.Str(s?["version"])); Fill(); });
    ReadOff(() => (CoreCall.Json(new[] { "license", "status", "--json" }) as JsonObject, CoreCall.Json(new[] { "plan", "--json" }) as JsonObject), (lp) =>
    {
      string tier = J.Str(lp.Item1?["tier_label"]);
      planLabel = tier.Length == 0 ? J.Str(lp.Item2?["plan"]) : tier;
      Fill();
    });
    ReadOff(() => CoreCall.Json(new[] { "card", "--json" }) as JsonObject, (c) =>
    {
      string p = J.Str(c?["project"]);
      projectName = p.Contains('/') || p.Contains('\\') ? "" : p;
      Fill();
    });
    ReadOff(ReadDoctor, (d) => { doctor = d; Fill(); });
    ReadOff(() => CoreCall.Json(new[] { "bus", "roster", "--json" }) as JsonObject, (r) => { roster = r; Fill(); });
  }

  static string Plain(string v) => v.Length <= 31 && v.All((c) => char.IsAsciiDigit(c) || c == '.') ? v : "";

  /// The doctor's checks with their ids, detail and fix, for the pips and the detail panel. The doctor prints an array, or an
  /// object with a checks array.
  static List<JsonObject> ReadDoctor()
  {
    JsonNode? j = CoreCall.Json(new[] { "doctor", "--json" });
    if (j is JsonObject o && o["checks"] is JsonArray a) return a.OfType<JsonObject>().ToList();
    return J.Objects(j).ToList();
  }

  /// The core is read off the window's thread; the window is filled on it.
  void Reload(Action? then = null)
  {
    if (loading) return;
    loading = true;
    Task.Run(() => (Read.Load(), ReadDoctor(), CoreCall.Json(new[] { "bus", "roster", "--json" }) as JsonObject)).ContinueWith((t) =>
    {
      loading = false;
      if (t.Status == TaskStatus.RanToCompletion) { (glance, doctor, roster) = t.Result; glanceRead = true; }
      Fill();
      then?.Invoke();
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  /// Reads the core again now, after a poll already under way has finished.
  void RefreshNow(Action then)
  {
    if (loading) { Task.Delay(200).ContinueWith((_) => RefreshNow(then), TaskScheduler.FromCurrentSynchronizationContext()); return; }
    Reload(then);
  }

  static readonly string[] DataScreens = { "projects", "bots", "memory", "checked", "settings" };

  /// A page reads the core off the window's thread; a newer load replaces an older one that ends later.
  Task LoadPage(string id)
  {
    int ticket = ++pageLoad;
    string query = search.Text ?? "";
    string theme = Environment.GetEnvironmentVariable("MEMEV_THEME") ?? (appearance == "system" ? "follows this computer" : appearance);
    string project = projectName;
    return Task.Run(() => id switch
    {
      "memory" => Pages.Memory(query),
      "checked" => Pages.Checked(),
      "bots" => Pages.Bots(),
      "projects" => Pages.Projects(project, ""),
      _ => Pages.Settings(theme),
    }).ContinueWith((t) =>
    {
      if (t.Status != TaskStatus.RanToCompletion) return;
      if (ticket != pageLoad && pageData.ContainsKey(id)) return;
      pageData[id] = t.Result;
      ResetTicks(id);
      BuildData(id, Screen(id), t.Result);
      if (current == id) Render();
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  bool Stopped => glance.HaveBinary && !glance.DaemonUp;

  (string Health, string Overview) Sentences() => !glanceRead ? ("Reading Memev.", "Reading Memev.") : (Read.Health(glance), Read.Overview(glance));

  JsonObject? DoctorEntry(string title) => doctor.FirstOrDefault((c) => J.Str(c["title"]) == title) ?? doctor.FirstOrDefault((c) => J.Str(c["id"]) == title);

  static GlyphKind Kind(string status) => status switch { "ok" => GlyphKind.Ok, "warn" => GlyphKind.Warn, "fail" => GlyphKind.Bad, _ => GlyphKind.Off };

  /// The 14 pips of 1.2, in order. A pip whose source the window does not read is hollow and says so in its title.
  List<Pip> Pips()
  {
    JsonObject? ById(string id) => doctor.FirstOrDefault((c) => J.Str(c["id"]) == id);
    Pip P(string name, string id)
    {
      if (ById(id) is not JsonObject c) return new Pip(name, GlyphKind.Off, "unread", $"{name} · not read yet");
      GlyphKind k = Kind(J.Str(c["status"]));
      return new Pip(name, k, k == GlyphKind.Ok ? "fine" : k == GlyphKind.Warn ? "needs" : k == GlyphKind.Bad ? "fault" : "unread", $"{name} · {Paths.Show(J.Str(c["detail"]))}");
    }
    var output = new List<Pip>();
    if (roster?["seats"] is JsonArray seats)
    {
      var named = seats.OfType<JsonObject>().Where((s) => J.Str(s["seat"]).Length > 0).ToList();
      int live = named.Count((s) => J.Str(s["state"]) == "live");
      string? quiet = named.Where((s) => J.Str(s["state"]) == "stale").Select((s) => J.Str(s["seat"])).FirstOrDefault();
      output.Add(quiet != null ? new Pip("Bots", GlyphKind.Warn, "needs", $"{quiet} quiet · {live} of {named.Count} live") : new Pip("Bots", GlyphKind.Ok, "fine", $"Bots · {live} of {named.Count} live"));
    }
    else output.Add(new Pip("Bots", GlyphKind.Off, "unread", "Bots · not read yet"));
    output.Add(P("Memory on", "vault"));
    if (ById("daemon") != null) output.Add(P("Running", "daemon"));
    else if (glance.HaveBinary && glanceRead)
    {
      // The doctor did not return its daemon check: the core's status answer says whether Memev is running.
      bool up = glance.DaemonUp;
      output.Add(new Pip("Running", up ? GlyphKind.Ok : GlyphKind.Bad, up ? "fine" : "fault", up ? "Running" : "Not running"));
    }
    else output.Add(new Pip("Running", GlyphKind.Off, "unread", "Running · not read yet"));
    output.Add(P("History", "oplog"));
    output.Add(P("Search", "index"));
    output.Add(P("Messages", "bus"));
    output.Add(P("Lead set", "bus-lead"));
    output.Add(P("Backup", "backup"));
    output.Add(P("Plan", "license"));
    output.Add(P("Notes safe", "secrets"));
    output.Add(new Pip("Shared", GlyphKind.Off, "unread", "Shared · not read yet: the core's ready check is not in this window"));
    output.Add(new Pip("Checks on", GlyphKind.Off, "unread", "Checks on · not read yet: the core's contracts status is not in this window"));
    output.Add(P("Update", "update"));
    if (ById("remote") is JsonObject r)
    {
      bool off = J.Str(r["status"]) == "ok" && J.Str(r["detail"]).ToLowerInvariant().Contains("off");
      output.Add(off ? new Pip("Web off", GlyphKind.Off, "off", "Web off") : P("Web access", "remote"));
    }
    else output.Add(new Pip("Web access", GlyphKind.Off, "unread", "Web access · not read yet"));
    return output;
  }

  int QuietBots => J.Objects(roster?["seats"]).Count((s) => J.Str(s["seat"]).Length > 0 && J.Str(s["state"]) == "stale");

  /// Fills everything that comes from the glance and the doctor: the health button, the Overview and Health screens.
  void Fill()
  {
    var (health, overview) = Sentences();
    healthButton.Sentence.Text = health;
    var ps = Pips();
    healthButton.Pips.Children.Clear();
    foreach (Pip p in ps) Ui.Put(healthButton.Pips, new Glyph(p.Kind, 6, true) { ToolTip = p.Tip }, 4);
    int fine = ps.Count((p) => p.State == "fine"), off = ps.Count((p) => p.State == "off");
    healthButton.GlyphView.Kind = !glanceRead ? GlyphKind.Off : Stopped ? GlyphKind.Bad : glance.Look > 0 ? GlyphKind.Warn : GlyphKind.Ok;
    AutomationProperties.SetName(healthButton, $"{health} {fine} fine, {off} off by choice. Open Health.");
    navButtons[3].NeedsYou = QuietBots > 0 && !Stopped;
    foot.Text = string.Join(" · ", new[] { planLabel, version.Length == 0 ? "" : $"v{version}" }.Where((x) => x.Length > 0));
    projectButton.Text = projectName;
    projectButton.Visibility = projectName.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

    var ov = Screen("overview");
    ov.Header.Sentence.Text = overview;
    BuildOverview(ov);
    var he = Screen("health");
    he.Header.Sentence.Text = health;
    BuildHealth(he);
    Render();
  }

  // ---- the shell

  void BuildSidebar()
  {
    var stack = new StackPanel { Margin = new Thickness(16, 20, 16, 16) };
    var word = Ui.Text("Memev", TypeStyle.Wordmark, "text");
    word.VerticalAlignment = VerticalAlignment.Center;
    stack.Children.Add(Ui.H(8, new Mark(), word));
    healthButton.OnPress = () => Open("health");
    healthButton.Margin = new Thickness(0, 16, 0, 0);
    stack.Children.Add(healthButton);
    for (int i = 0; i < Screens.Nav.Length; i++)
    {
      ScreenDef item = Screens.Nav[i];
      if (item.Id == "settings")
      {
        var d = Ui.Hairline("rule");
        d.Margin = new Thickness(0, 8, 0, 8);
        stack.Children.Add(d);
      }
      var b = new NavButton(item.Menu) { Margin = new Thickness(0, i == 0 ? 16 : 4, 0, 0) };
      int index = i;
      b.OnPress = () => Open(Screens.Nav[index].Id);
      navButtons.Add(b);
      stack.Children.Add(b);
      if (item.Id == "projects")
      {
        projectButton.Visibility = Visibility.Collapsed;
        projectButton.Margin = new Thickness(0, 4, 0, 0);
        projectButton.OnPress = () => { if (projectName.Length > 0) Open("project"); };
        stack.Children.Add(projectButton);
      }
    }
    var bottom = new StackPanel { Margin = new Thickness(16, 0, 16, 16), VerticalAlignment = VerticalAlignment.Bottom };
    helpButton.OnPress = () => Open("settings", "help");
    bottom.Children.Add(helpButton);
    foot.Margin = new Thickness(12, 12, 0, 0);
    bottom.Children.Add(foot);
    var dock = new DockPanel { LastChildFill = true };
    DockPanel.SetDock(bottom, Dock.Bottom);
    dock.Children.Add(bottom);
    dock.Children.Add(stack);
    sidebar.Width = 240;
    sidebar.Child = dock;
  }

  UIElement BuildMain()
  {
    var top = Ui.Box("panel");
    top.Height = 48;
    top.Padding = new Thickness(36, 0, 36, 0);
    crumbLink.OnPress = () => Open("projects");
    var chipText = Ui.Text("Technical detail on", TypeStyle.SmallMedium, "sub");
    chipText.VerticalAlignment = VerticalAlignment.Center;
    techChip.Child = chipText;
    techChip.Height = 20;
    techChip.Padding = new Thickness(8, 0, 8, 0);
    AutomationProperties.SetName(statusLine, "Status");
    statusLine.TextTrimming = TextTrimming.CharacterEllipsis;

    // SH-9: the command field, 280 by 32, with Ctrl K at its right.
    var field = Ui.Box("page", "ctl", 8);
    field.Width = 280;
    field.Height = 32;
    command.BorderThickness = new Thickness(0);
    command.Background = System.Windows.Media.Brushes.Transparent;
    command.SetResourceReference(ForegroundProperty, "text");
    command.SetResourceReference(TextBox.CaretBrushProperty, "text");
    command.FontFamily = Fonts.Sans;
    command.FontSize = 14;
    command.VerticalContentAlignment = VerticalAlignment.Center;
    AutomationProperties.SetName(command, "Search or run a command");
    var placeholder = Ui.Text("Search or run a command", TypeStyle.Body, "dim");
    placeholder.IsHitTestVisible = false;
    placeholder.VerticalAlignment = VerticalAlignment.Center;
    placeholder.Margin = new Thickness(2, 0, 0, 0);
    command.TextChanged += (_, _) => placeholder.Visibility = command.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    command.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommandEntered(); e.Handled = true; } };
    var key = Ui.Text("Ctrl K", TypeStyle.Small, "dim");
    key.VerticalAlignment = VerticalAlignment.Center;
    var inner = new Grid { Margin = new Thickness(10, 0, 10, 0) };
    inner.Children.Add(placeholder);
    inner.Children.Add(command);
    field.Child = Ui.Row(Array.Empty<UIElement>(), inner, new UIElement[] { key }, 8);
    foreach (FrameworkElement e in new FrameworkElement[] { crumbLink, crumbSep, crumb, techChip, statusLine }) e.VerticalAlignment = VerticalAlignment.Center;
    var bar = Ui.Row(new UIElement[] { crumbLink, crumbSep, crumb, techChip }, statusLine, new UIElement[] { field }, 8);
    techChip.Margin = new Thickness(16, 0, 0, 0);
    statusLine.Margin = new Thickness(16, 0, 0, 0);
    top.Child = bar;

    // SH-10: the stopped banner.
    var seeWhy = new MButton("See why it stopped", ButtonStyleKind.Secondary, onPress: () => Open("health"));
    bannerStart = new MButton("Start Memev", ButtonStyleKind.Secondary, 3, onPress: StartSheet);
    var words = Ui.V(2, Ui.Text("Memev is stopped.", TypeStyle.BodyMedium, "text"), Ui.Text("Your AI apps cannot reach their shared memory. Nothing is lost.", TypeStyle.Body, "sub"));
    words.VerticalAlignment = VerticalAlignment.Center;
    var bad = new Glyph(GlyphKind.Bad);
    banner.Child = Ui.Row(new UIElement[] { bad, words }, null, new UIElement[] { seeWhy, bannerStart }, 12);
    banner.Padding = new Thickness(36, 12, 36, 12);
    banner.Visibility = Visibility.Collapsed;

    scroll.Content = pageStack;
    search.FontFamily = Fonts.Sans;
    search.FontSize = 14;
    search.VerticalContentAlignment = VerticalAlignment.Center;
    search.Padding = new Thickness(8, 0, 8, 0);
    search.SetResourceReference(BackgroundProperty, "page");
    search.SetResourceReference(ForegroundProperty, "text");
    search.SetResourceReference(BorderBrushProperty, "ctl");
    search.SetResourceReference(TextBox.CaretBrushProperty, "text");
    AutomationProperties.SetName(search, "Search your notes");
    search.KeyDown += (_, e) => { if (e.Key == Key.Enter && current == "memory") { _ = LoadPage("memory"); e.Handled = true; } };

    var col = new DockPanel { LastChildFill = true };
    foreach (UIElement e in new UIElement[] { top, Ui.Hairline("rule"), banner }) { DockPanel.SetDock(e, Dock.Top); col.Children.Add(e); }
    col.Children.Add(scroll);
    return col;
  }

  /// A screen's view, made once and kept. The guide's screen holds the guide's steps and its one button.
  ScreenView Screen(string id)
  {
    if (screens.TryGetValue(id, out ScreenView? s)) return s;
    var refresh = id == "guide" ? primary : new MButton("Refresh", ButtonStyleKind.Secondary, 1, onPress: RefreshClicked);
    s = new ScreenView(refresh);
    s.Header.What.OnPress = OpenWhat;
    s.Header.Tabs.OnSelect = SelectTab;
    s.Root.Visibility = Visibility.Collapsed;
    screens[id] = s;
    pageStack.Children.Add(s.Root);
    return s;
  }

  ScreenDef Def(string id) => id switch
  {
    "health" => Screens.Health,
    "project" => Screens.Project(projectName),
    "guide" => new ScreenDef { Id = "guide", Menu = "", Title = "" },
    _ => Screens.Nav.FirstOrDefault((d) => d.Id == id) ?? Screens.Nav[0],
  };

  void Select(int index)
  {
    for (int i = 0; i < navButtons.Count; i++) navButtons[i].Selected = i == index;
    projectButton.Selected = false;
    helpButton.Selected = false;
    healthButton.Open = false;
  }

  void Open(string id, string? tab = null)
  {
    LeaveGuide();
    if (sheet.IsOpen) sheet.Dismiss();
    current = id;
    if (tab != null) tabFor[id] = tab;
    currentPage = Array.FindIndex(Screens.Nav, (d) => d.Id == id);
    CloseDetail();
    if (DataScreens.Contains(id))
    {
      BuildData(id, Screen(id), pageData.GetValueOrDefault(id));
      _ = LoadPage(id);
    }
    else if (id != "overview" && id != "health") BuildPlaceholder(id, Screen(id));
    Render();
  }

  void SelectTab(string t)
  {
    tabFor[current] = t;
    CloseDetail();
    Render();
  }

  /// The tab list of a screen, with live counts and needs-you diamonds where the core's answers give them.
  List<TabItemDef> Tabs(string id)
  {
    var list = Def(id).Tabs.Select((t) => new TabItemDef { Id = t.Id, Label = t.Label }).ToList();
    foreach (TabItemDef t in list)
    {
      if (id == "memory" && t.Id == "review" && pageData.GetValueOrDefault("memory")?.Counts.GetValueOrDefault("review", -1) is int n && n >= 0) t.Label = $"Review ({n})";
      if (id == "bots" && t.Id == "team") t.NeedsYou = QuietBots > 0 && !Stopped;
    }
    return list;
  }

  string Sentence(string id, string tab)
  {
    switch (id)
    {
      case "overview": return Sentences().Overview;
      case "health": return Sentences().Health;
      case "guide": return "";
      case "project" when tab == "overview": return glance.NorthStar.Length == 0 ? "No north star yet." : $"North star: {glance.NorthStar}";
      case "settings" when tab == "general":
        string theme = Theme.Current.Dark ? "Dark theme." : "Light theme.";
        return version.Length == 0 ? theme : $"{theme} Version {version}.";
    }
    if (pageData.GetValueOrDefault(id)?.Sentences.GetValueOrDefault(tab) is string s && s.Length > 0) return s;
    return Def(id).Sentences.GetValueOrDefault(tab, "");
  }

  /// The page actions of a screen and tab, at most two; the last one is the screen's amber candidate (SH-14, SH-20).
  List<MButton> Actions(string id, string tab)
  {
    var s = Screen(id);
    if (id == "guide") return GuideActions();
    var list = new List<MButton>();
    if (id == "memory" && tab == "search") list.Add(importAction);
    list.Add(s.Refresh);
    if (id == "bots" && tab == "connect" && ConnectAction() is MButton c) list.Add(c);
    if (id == "bots" && tab == "agents" && AgentsAction() is MButton a) list.Add(a);
    if (list.Count > 2) list.RemoveAt(0);
    return list;
  }

  /// Draws the current screen: header, tabs, the selected tab's content, the top bar and the banner, then the amber rule.
  void Render()
  {
    string id = current;
    ScreenDef d = Def(id);
    ScreenView s = Screen(id);
    var tabList = Tabs(id);
    string tab = tabFor.GetValueOrDefault(id) ?? tabList.FirstOrDefault()?.Id ?? "";
    if (id != "guide") s.Header.Configure(d.Title, Sentence(id, tab), tabList, tab, d.Words.Length > 0, Actions(id, tab));
    else s.Header.Configure(guideTitle, "", new List<TabItemDef>(), "", false, Actions(id, tab));
    foreach (var (k, v) in s.TabViews) v.Visibility = k == tab || (k == "" && tabList.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
    foreach (var (k, v) in screens) v.Root.Visibility = k == id ? Visibility.Visible : Visibility.Collapsed;

    // The menu: the selected entry, or none for Health, a project's Help and support and the guide.
    Select(currentPage);
    if (id == "health") healthButton.Open = true;
    if (id == "project") projectButton.Selected = true;
    if (id == "settings" && tab == "help") { Select(-1); helpButton.Selected = true; }

    // SH-6: the breadcrumb.
    bool inProject = id == "project";
    crumbLink.Visibility = inProject ? Visibility.Visible : Visibility.Collapsed;
    crumbSep.Visibility = inProject ? Visibility.Visible : Visibility.Collapsed;
    crumb.Text = id == "guide" ? "Setting up" : inProject ? projectName : d.Id == "health" ? "Health" : d.Menu;
    techChip.Visibility = technical ? Visibility.Visible : Visibility.Collapsed;
    banner.Visibility = Stopped && id != "guide" ? Visibility.Visible : Visibility.Collapsed;
    SetNav(!inGuide);
    EnforceAmber();
  }

  void EnforceAmber() => AmberRule.Enforce(shell, Stopped && current != "guide", Def(current).NoPrimary);

  void SetNav(bool on)
  {
    foreach (NavButton b in navButtons) b.IsEnabled = on;
    healthButton.IsEnabled = on;
    helpButton.IsEnabled = on;
    projectButton.IsEnabled = on;
    command.IsEnabled = on;
  }

  void RefreshClicked()
  {
    if (inGuide) return;
    Reload();
    if (DataScreens.Contains(current)) _ = LoadPage(current);
  }

  void PrimaryPressed() { if (inGuide) GuideNext(); else RefreshClicked(); }

  /// SH-9: goes to a screen, a tab or the project by name; anything else searches the notes. It never sends anything to a bot.
  void CommandEntered()
  {
    string q = command.Text.Trim();
    string low = q.ToLowerInvariant();
    if (low.Length == 0) return;
    command.Text = "";
    void Say(string s) => statusLine.Text = s;
    foreach (ScreenDef d in Screens.Nav.Append(Screens.Health))
      if (d.Menu.ToLowerInvariant() == low || d.Title.ToLowerInvariant() == low) { Open(d.Id); Say($"Opened {d.Menu}."); return; }
    if (low == "help" || low == "help and support") { Open("settings", "help"); Say("Opened Help and support."); return; }
    foreach (ScreenDef d in Screens.Nav)
      foreach (TabItemDef t in d.Tabs)
        if (t.Label.ToLowerInvariant() == low || $"{d.Menu} {t.Label}".ToLowerInvariant() == low) { Open(d.Id, t.Id); Say($"Opened {d.Menu}, {t.Label}."); return; }
    if (projectName.Length > 0 && projectName.ToLowerInvariant() == low) { Open("project"); Say($"Opened {projectName}."); return; }
    foreach (ScreenDef d in Screens.Nav.Append(Screens.Health))
      if (low.Length >= 3 && d.Menu.ToLowerInvariant().StartsWith(low, StringComparison.Ordinal)) { Open(d.Id); Say($"Opened {d.Menu}."); return; }
    search.Text = q;
    Open("memory", "search");
    Say($"Searching your notes for “{q}”.");
  }

  // ---- the detail panel

  void OpenDetail(DetailModel m, RowView? row)
  {
    if (detail.Visibility == Visibility.Visible && detail.Model?.Key == m.Key) { CloseDetail(); return; }
    if (selectedRow != null) selectedRow.Selected = false;
    selectedRow = row;
    if (row != null) row.Selected = true;
    detail.Show(m, technical);
    detail.Visibility = Visibility.Visible;
    detailEdge.Visibility = Visibility.Visible;
    detail.Close.Focus();
  }

  void CloseDetail()
  {
    if (detail.Visibility != Visibility.Visible) return;
    detail.Visibility = Visibility.Collapsed;
    detailEdge.Visibility = Visibility.Collapsed;
    if (selectedRow != null)
    {
      selectedRow.Selected = false;
      if (Ui.Shown(selectedRow)) selectedRow.Focus();
    }
    selectedRow = null;
  }

  void OpenWhat()
  {
    ScreenDef d = Def(current);
    OpenDetail(new DetailModel
    {
      Key = $"what:{current}", Title = "What is this?", Why = "The words on this screen, in plain terms.",
      Groups = { new DetailGroup("WORDS ON THIS SCREEN", d.Words.Select((w) => new DetailRow(w.Word, w.Plain)).ToList()) },
    }, null);
  }

  // ---- sheets that run the core

  void Present(SheetModel m)
  {
    sheet.Technical = technical;
    sheet.Present(m);
  }

  const string StartTitle = "Start Memev?";

  /// SH-10 under the owner's ruling D-1: the window starts Memev itself, after a sheet that names what changes.
  void StartSheet()
  {
    Present(new SheetModel
    {
      Title = StartTitle,
      Note = "This starts Memev's background service on this PC, so your AI apps can reach their shared memory again. It does not change your notes, and it does not make Memev start when you sign in.",
      Source = "memev start", Main = "Start Memev", OnMain = RunStart,
    });
  }

  /// Runs start off the window's thread, shows that it is starting, then the result, and reads the core again for the health state.
  void RunStart(SheetHost host)
  {
    if (starting) return;
    starting = true;
    host.Status.Text = "Starting Memev. This can take a few seconds.";
    if (host.MainButton != null) host.MainButton.IsEnabled = false;
    statusLine.Text = "Starting Memev.";
    bool have = CoreCall.HaveBinary;
    // The core waits for its service to answer before it returns; a first open of a large notes folder can take longer.
    Task.Run(() => CoreCall.Run(new[] { "start" }, 60000)).ContinueWith((t) =>
    {
      starting = false;
      int code = t.Status == TaskStatus.RanToCompletion ? t.Result.Code : -1;
      var (title, said) = code switch
      {
        0 => ("Memev is running", "Memev's background service is running on this PC. Your AI apps can reach their shared memory."),
        -1 when !have => ("Memev could not be run", "This window could not find the memev command it runs. Nothing was started."),
        -1 => ("Memev has not answered yet", "Memev did not answer within a minute. It may still be opening your notes; Health shows when it is running."),
        _ => ("Memev did not start", $"Memev did not start (the core stopped with code {code}). Health shows its checks."),
      };
      statusLine.Text = title + ".";
      if (host.IsOpen && host.Model?.Title == StartTitle) Present(new SheetModel { Title = title, Note = said, Source = "memev start", Main = "Close" });
      RefreshNow(() => onStartDone?.Invoke(code));
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  /// Runs one change after its sheet: the sheet says it is working, shows the core's answer, then calls done on this thread.
  void RunInSheet(SheetHost host, string working, string[] args, int timeoutMs, Action<int, string> done)
  {
    host.Status.Text = working;
    if (host.MainButton != null) host.MainButton.IsEnabled = false;
    foreach (MButton b in host.OtherButtons) b.IsEnabled = false;
    statusLine.Text = working;
    Task.Run(() => CoreCall.Run(args, timeoutMs)).ContinueWith((t) =>
    {
      var (code, output) = t.Status == TaskStatus.RanToCompletion ? t.Result : (-1, "");
      done(code, code == 126 ? "" : Pages.Answer(output));
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  static string Refused => "That holds a character this window will not pass to the core: \" % ! & | < > ^ ( ). Nothing was run.";

  /// The sheet that says what the core answered, with Close.
  void Result(string title, int code, string answer, string ok, string source)
  {
    string note = code == 0 ? ok : code == 126 ? Refused : code == -1 ? "The core did not answer in time. Nothing more was run." : $"The core stopped with code {code}. Its answer is below.";
    Present(new SheetModel { Title = title, Note = note, Source = source, Main = "Close" });
    sheet.ShowOutput(answer);
    statusLine.Text = title + ".";
  }

  // ---- this app's settings

  void AppearanceChanged(string a)
  {
    appearance = a;
    SaveSettings();
    Repaint();
    Open(current, tabFor.GetValueOrDefault(current));
  }

  void TechnicalChanged(bool on)
  {
    technical = on;
    SaveSettings();
    Fill();
    Open(current, tabFor.GetValueOrDefault(current));
    statusLine.Text = technical ? "Technical detail is on." : "Technical detail is off.";
  }
}
