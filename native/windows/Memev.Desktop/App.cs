// SPDX-License-Identifier: Apache-2.0
// The Windows window, first slice: the same window as native/linux and native/macos. It reads a Memev core on this computer
// through Memev.Reader and shows the health sentence, the seven menu entries, Help and support, and the overview sentence.
// It writes nothing and sends nothing beyond loopback. MEMEV_SELF_CHECK=1 prints what it shows and exits.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Memev.Reader;

namespace Memev.Desktop;

static class Program
{
  // A window program has no console; when stdout is redirected (a test, CI), lines still reach it through the handle.
  static readonly Lazy<StreamWriter> stdout = new(() => new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
  public static void Out(string line) => stdout.Value.WriteLine(line);

  [STAThread]
  static int Main(string[] args)
  {
    if (args.Length > 0 && args[0] == "--version")
    {
      Out("memev-desktop 0.0.1-slice1");
      return 0;
    }
    // An installed app carries its own core: use it unless MEMEV_BIN names another.
    string bundled = System.IO.Path.Combine(AppContext.BaseDirectory, "resources", "bin", "memev.cmd");
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MEMEV_BIN")) && File.Exists(bundled))
      Environment.SetEnvironmentVariable("MEMEV_BIN", bundled);
    var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
    return app.Run(new MainWindow());
  }
}

sealed record NavItem(string Id, string Label, int Kind, string? Later);

sealed class Theme
{
  public required string Name;
  public required Color Back, Text, Dim, Sub, NavText, Selected, Rule, Bad, Warn;
  public static readonly Color Amber = Color.FromRgb(0xF2, 0xB5, 0x44);
  public static readonly Color Ink = Color.FromRgb(0x0A, 0x0D, 0x12);
  static Color C(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
  public static readonly Theme Dark = new() { Name = "dark", Back = C(0x0A0D12), Text = C(0xE8ECF2), Dim = C(0x7F8997), Sub = C(0xA4ADBA), NavText = C(0xA4ADBA), Selected = C(0x181E27), Rule = C(0x374252), Bad = C(0xFF8A7A), Warn = C(0xF2B544) };
  public static readonly Theme Light = new() { Name = "light", Back = C(0xF6F7F9), Text = C(0x10151C), Dim = C(0x5F6978), Sub = C(0x46505E), NavText = C(0x46505E), Selected = C(0xEDEFF3), Rule = C(0xC3CAD4), Bad = C(0xB3261E), Warn = C(0x8A5600) };

  // MEMEV_THEME=dark or light; otherwise this computer's app setting
  public static Theme Pick()
  {
    switch (Environment.GetEnvironmentVariable("MEMEV_THEME"))
    {
      case "dark": return Dark;
      case "light": return Light;
    }
    try
    {
      using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
      return key?.GetValue("AppsUseLightTheme") is int v && v == 0 ? Dark : Light;
    }
    catch (System.Security.SecurityException) { return Light; }
  }
}

sealed class MainWindow : Window
{
  static readonly NavItem[] Nav =
  {
    new("overview", "Overview", 0, null),
    new("operations", "Operations", 2, "Not in this slice. Live agent panes are not drawn here."),
    new("projects", "Projects", 2, "Not in this slice. No project list is loaded yet."),
    new("bots", "Bots and agents", 2, "Not in this slice. The roster is not loaded yet."),
    new("memory", "Memory", 2, "Not in this slice. Search stays on the memev command until a later slice."),
    new("checked", "Checked work", 2, "Not in this slice. Receipts are not listed yet."),
    new("settings", "Settings", 2, "Not in this slice. The theme follows this computer."),
  };

  public static readonly RoutedCommand RefreshCommand = new("Refresh", typeof(MainWindow));

  readonly Theme theme = Theme.Pick();
  readonly bool selfCheck = Environment.GetEnvironmentVariable("MEMEV_SELF_CHECK") != null;
  readonly Border sidebar;
  readonly TextBlock healthLink = new() { TextWrapping = TextWrapping.Wrap, Text = "Reading Memev." };
  readonly Button healthButton;
  readonly List<Button> navButtons = new();
  readonly TextBlock pageTitle = new() { Text = "Executive overview", FontSize = 28, FontWeight = FontWeights.Medium };
  readonly Button primary;
  readonly TextBlock overviewSentence = new() { FontSize = 16, TextWrapping = TextWrapping.Wrap };
  readonly TextBlock northStar = new() { TextWrapping = TextWrapping.Wrap };
  readonly StackPanel attention = new();
  readonly TextBlock healthSentence = new() { FontSize = 16, TextWrapping = TextWrapping.Wrap };
  readonly StackPanel checks = new();
  readonly TextBlock laterBody = new() { FontSize = 16, TextWrapping = TextWrapping.Wrap };
  readonly StackPanel overviewPage, healthPage, laterPage, guidePage;
  readonly TextBlock primaryLabel = new() { Text = "Refresh", Foreground = new SolidColorBrush(Theme.Ink), FontWeight = FontWeights.Medium };
  readonly Button back;
  Button helpButton = null!;
  readonly TextBlock guideText = new() { FontSize = 16, TextWrapping = TextWrapping.Wrap, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left };
  readonly TextBlock guideDetail = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
  readonly TextBox guideEntry = new() { MinWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(6, 4, 6, 4) };
  readonly StackPanel guideChoices = new() { Margin = new Thickness(0, 12, 0, 0) };
  readonly StackPanel guideApps = new() { Margin = new Thickness(0, 12, 0, 0) };
  readonly CheckBox guideBackup = new() { Content = "Record this remote, and push my notes to it", Margin = new Thickness(0, 8, 0, 0) };
  Button? choiceThis, choiceOther;
  bool inGuide, guideBusy;
  int guideStep;
  GuidePlan plan = new();
  List<GuideApp> apps = new();
  Glance glance = new();
  int attentionRows;
  bool loading;

  SolidColorBrush B(Color c) => new(c);

  public MainWindow()
  {
    Title = "Memev";
    MinWidth = 1024;
    MinHeight = 700;
    Width = 1120;
    Height = 760;
    Background = B(theme.Back);
    Foreground = B(theme.Text);

    // the sidebar is 240 wide in all, with its padding inside that width
    var top = new StackPanel();
    top.Children.Add(new TextBlock { Text = "Memev", FontSize = 20, FontWeight = FontWeights.Medium, Foreground = B(theme.Text), Margin = new Thickness(8, 0, 0, 8) });
    healthButton = NavButton(healthLink, (_, _) => ShowHealth());
    healthLink.Foreground = B(theme.NavText);
    top.Children.Add(healthButton);
    top.Children.Add(new Border { Height = 1, Background = B(theme.Rule), Margin = new Thickness(0, 8, 0, 8) });
    for (int i = 0; i < Nav.Length; i++)
    {
      int index = i;
      var b = NavButton(new TextBlock { Text = Nav[i].Label, Foreground = B(theme.NavText) }, (_, _) => ShowPage(index));
      navButtons.Add(b);
      top.Children.Add(b);
    }
    var help = NavButton(new TextBlock { Text = "Help and support", Foreground = B(theme.NavText) }, (_, _) => ShowHelp());
    helpButton = help;
    var column = new DockPanel { LastChildFill = false };
    DockPanel.SetDock(help, Dock.Bottom);
    column.Children.Add(help);
    DockPanel.SetDock(top, Dock.Top);
    column.Children.Add(top);
    sidebar = new Border { Width = 240, Padding = new Thickness(12, 16, 12, 16), Background = B(theme.Back), Child = column };

    primary = new Button
    {
      Content = primaryLabel,
      Background = new SolidColorBrush(Theme.Amber), BorderThickness = new Thickness(0), Padding = new Thickness(16, 6, 16, 6),
      HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Tag = "primary",
    };
    primary.Click += (_, _) => { if (inGuide) GuideNext(); else Reload(); };
    var header = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
    DockPanel.SetDock(primary, Dock.Right);
    header.Children.Add(primary);
    back = NavButton(new TextBlock { Text = "Back", Foreground = B(theme.NavText) }, (_, _) => GuideBack());
    back.HorizontalAlignment = HorizontalAlignment.Left;
    back.Visibility = Visibility.Collapsed;
    back.Margin = new Thickness(0, 0, 12, 0);
    DockPanel.SetDock(back, Dock.Left);
    header.Children.Add(back);
    pageTitle.Foreground = B(theme.Text);
    header.Children.Add(pageTitle);

    overviewSentence.Foreground = B(theme.Sub);
    northStar.Foreground = B(theme.Text);
    overviewPage = new StackPanel();
    overviewPage.Children.Add(overviewSentence);
    overviewPage.Children.Add(Spaced(northStar, 8));
    overviewPage.Children.Add(SectionHead("STATUS"));
    overviewPage.Children.Add(attention);
    overviewPage.Children.Add(Spaced(new TextBlock { Text = "Charts are a later slice. The lines above are the core's answers, read just now. No sample figures are shown.", FontSize = 12, Foreground = B(theme.Dim), TextWrapping = TextWrapping.Wrap }, 16));

    healthSentence.Foreground = B(theme.Sub);
    healthPage = new StackPanel { Visibility = Visibility.Collapsed };
    healthPage.Children.Add(healthSentence);
    healthPage.Children.Add(SectionHead("CHECKS"));
    healthPage.Children.Add(checks);

    laterBody.Foreground = B(theme.Sub);
    laterPage = new StackPanel { Visibility = Visibility.Collapsed };
    laterPage.Children.Add(laterBody);

    var page = new StackPanel { Margin = new Thickness(36, 24, 36, 24) };
    page.Children.Add(header);
    page.Children.Add(overviewPage);
    page.Children.Add(healthPage);
    page.Children.Add(laterPage);
    guideText.Foreground = B(theme.Sub);
    guideDetail.Foreground = B(theme.Warn);
    guideEntry.Background = B(theme.Selected);
    guideEntry.Foreground = B(theme.Text);
    guideBackup.Foreground = B(theme.Text);
    choiceThis = NavButton(new TextBlock { Text = "This computer", Foreground = B(theme.NavText) }, (_, _) => Pick(1));
    choiceOther = NavButton(new TextBlock { Text = "Another computer", Foreground = B(theme.NavText) }, (_, _) => Pick(0));
    guideChoices.Children.Add(choiceThis);
    guideChoices.Children.Add(choiceOther);
    guidePage = new StackPanel { Visibility = Visibility.Collapsed };
    foreach (UIElement e in new UIElement[] { guideText, guideChoices, guideEntry, guideApps, guideBackup, guideDetail }) guidePage.Children.Add(e);
    page.Children.Add(guidePage);

    var root = new Grid();
    root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
    root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
    root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    Grid.SetColumn(sidebar, 0);
    var edge = new Rectangle { Fill = B(theme.Rule) };
    Grid.SetColumn(edge, 1);
    var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    Grid.SetColumn(scroll, 2);
    root.Children.Add(sidebar);
    root.Children.Add(edge);
    root.Children.Add(scroll);
    Content = root;

    CommandBindings.Add(new CommandBinding(RefreshCommand, (_, _) => { if (!inGuide) Reload(); }));
    InputBindings.Add(new KeyBinding(RefreshCommand, Key.R, ModifierKeys.Control));

    glance = Read.Load();
    Fill();
    ShowPage(0);
    if (WantGuide() && glance.HaveBinary) GuideShow(0);
    ContentRendered += (_, _) =>
    {
      if (selfCheck) { Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ReportAndQuit)); return; }
      int ms = int.TryParse(Environment.GetEnvironmentVariable("MEMEV_POLL_MS"), out int v) ? v : 5000;
      var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(200, ms)) };
      timer.Tick += (_, _) => { if (!inGuide) Reload(); };
      timer.Start();
    };
  }

  Button NavButton(TextBlock label, RoutedEventHandler onClick)
  {
    var b = new Button
    {
      Content = label, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6),
      HorizontalContentAlignment = HorizontalAlignment.Left, HorizontalAlignment = HorizontalAlignment.Stretch, Cursor = Cursors.Hand,
    };
    b.Click += onClick;
    return b;
  }

  static FrameworkElement Spaced(FrameworkElement e, double top) { e.Margin = new Thickness(0, top, 0, 0); return e; }

  FrameworkElement SectionHead(string text)
  {
    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 8) };
    row.Children.Add(new Rectangle { Width = 3, Height = 12, Fill = new SolidColorBrush(Theme.Amber), Margin = new Thickness(0, 0, 8, 0) });
    row.Children.Add(new TextBlock { Text = text, FontSize = 12, Foreground = B(theme.Dim) });
    return row;
  }

  TextBlock CheckRow(Check c)
  {
    var t = new TextBlock { Text = $"{c.Title}: {c.Status}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2), Foreground = B(theme.Text) };
    if (c.Status == "fail") t.Foreground = B(theme.Bad);
    else if (c.Status != "ok") t.Foreground = B(theme.Warn);
    return t;
  }

  TextBlock Line(string text) => new() { Text = text, Foreground = B(theme.Sub), TextWrapping = TextWrapping.Wrap };

  void Fill()
  {
    string health = Read.Health(glance);
    healthLink.Text = health;
    healthSentence.Text = health;
    overviewSentence.Text = Read.Overview(glance);
    northStar.Text = glance.NorthStar;
    northStar.Visibility = glance.NorthStar.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    attention.Children.Clear();
    attentionRows = 0;
    if (!glance.DaemonUp) attention.Children.Add(Line("Nothing to list while Memev is not running."));
    else if (glance.Look == 0) attention.Children.Add(Line("Nothing needs you."));
    else
      foreach (Check c in glance.Checks.Where((c) => c.Status != "ok")) { attention.Children.Add(CheckRow(c)); attentionRows++; }

    checks.Children.Clear();
    if (!glance.DaemonUp || glance.Checks.Count == 0) checks.Children.Add(Line("No checks were returned."));
    else foreach (Check c in glance.Checks) checks.Children.Add(CheckRow(c));
  }

  // the core is read off the window's thread, and the window is filled on it
  void Reload()
  {
    if (loading) return;
    loading = true;
    Task.Run(Read.Load).ContinueWith((t) =>
    {
      loading = false;
      if (t.Status == TaskStatus.RanToCompletion) { glance = t.Result; Fill(); }
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  void Select(int index)
  {
    for (int i = 0; i < navButtons.Count; i++) navButtons[i].Background = i == index ? B(theme.Selected) : Brushes.Transparent;
  }

  void Page(StackPanel visible)
  {
    foreach (StackPanel p in new[] { overviewPage, healthPage, laterPage, guidePage }) p.Visibility = p == visible ? Visibility.Visible : Visibility.Collapsed;
  }

  void ShowPage(int index)
  {
    Select(index);
    NavItem item = Nav[index];
    if (item.Kind == 0) { pageTitle.Text = "Executive overview"; Page(overviewPage); }
    else { pageTitle.Text = item.Label; laterBody.Text = item.Later ?? ""; Page(laterPage); }
  }

  // Health is not a menu row; the link under the wordmark opens it
  void ShowHealth() { Select(-1); pageTitle.Text = "Health"; Page(healthPage); }

  void ShowHelp() { Select(-1); pageTitle.Text = "Help and support"; laterBody.Text = "Help and support is not in this slice. This app sends nothing."; Page(laterPage); }

  // ---- the first launch, as native/linux/src/ui.c runs it

  // MEMEV_GUIDE=off skips it, MEMEV_GUIDE=1 shows it; otherwise it shows until it has been finished once on this computer
  static string MarkerPath()
  {
    string? cfg = Environment.GetEnvironmentVariable("MEMEV_CONFIG_DIR");
    string dir = !string.IsNullOrEmpty(cfg) ? cfg : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "memev");
    return System.IO.Path.Combine(dir, "desktop-first-launch-done");
  }

  static bool WantGuide()
  {
    string? flag = Environment.GetEnvironmentVariable("MEMEV_GUIDE");
    if (flag == "off") return false;
    if (flag == "1") return true;
    return !File.Exists(MarkerPath());
  }

  void Pick(int which)
  {
    plan.FirstComputer = which;
    if (choiceThis != null) choiceThis.Background = which == 1 ? B(theme.Selected) : Brushes.Transparent;
    if (choiceOther != null) choiceOther.Background = which == 0 ? B(theme.Selected) : Brushes.Transparent;
  }

  void SetNav(bool on) { foreach (Button b in navButtons) b.IsEnabled = on; healthButton.IsEnabled = on; helpButton.IsEnabled = on; }

  void GuideShow(int step)
  {
    inGuide = true;
    guideStep = step;
    SetNav(false);
    Select(-1);
    Page(guidePage);
    back.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
    foreach (UIElement e in new UIElement[] { guideEntry, guideChoices, guideApps, guideBackup }) e.Visibility = Visibility.Collapsed;
    guideDetail.Text = "";
    primaryLabel.Text = "Continue";
    if (step == 0)
    {
      pageTitle.Text = "Is this your first computer?";
      guideText.Text = "Nothing is created, connected or sent on this step.";
      guideChoices.Visibility = Visibility.Visible;
    }
    else if (step == 1 && plan.FirstComputer == 0)
    {
      pageTitle.Text = "Join another computer";
      guideText.Text = "The join runs only when you press the button. It contacts the other computer.";
      guideEntry.Visibility = Visibility.Visible;
      primaryLabel.Text = "Join the other computer";
    }
    else if (step == 1)
    {
      pageTitle.Text = "Where should the notes live?";
      guideText.Text = "Type a full path. The folder is created only when you continue, and only on this computer.";
      guideEntry.Visibility = Visibility.Visible;
    }
    else if (step == 2)
    {
      pageTitle.Text = "Which apps should Memev know?";
      guideText.Text = "The list is the core's own client list. A tick is remembered here. Nothing is connected: that list has no id, and this window does not keep its own list.";
      LoadApps();
      guideApps.Visibility = Visibility.Visible;
    }
    else if (step == 3)
    {
      pageTitle.Text = "Back up to your own git remote?";
      guideText.Text = "Leave this blank to skip it. Nothing is recorded unless you tick the line. If you record a remote, Memev pushes your notes to it: when it starts, every 5 minutes by default, and when it stops.";
      guideEntry.Visibility = Visibility.Visible;
      guideBackup.Visibility = Visibility.Visible;
      guideBackup.IsChecked = false;
    }
    else
    {
      pageTitle.Text = "Check that a note can be saved";
      guideText.Text = "This uses the core on this computer. It does not use sample figures.";
      primaryLabel.Text = "Check the notes";
    }
  }

  // The client list is read off the window's thread (it can take seconds), then the rows are drawn on it.
  void LoadApps()
  {
    if (apps.Count > 0 || guideBusy) return;
    guideApps.Children.Clear();
    string? bin = Core.ResolveBin();
    if (bin == null) { guideApps.Children.Add(Line("The client list did not parse.")); return; }
    guideBusy = true;
    primary.IsEnabled = false;
    guideApps.Children.Add(Line("Reading the core's client list."));
    Task.Run(() => { var r = Core.Capture(bin, new[] { "clients" }, 10000); return r.Done && r.ExitCode == 0 ? Guide.ParseClients(r.Output) : new List<GuideApp>(); })
      .ContinueWith((t) =>
      {
        guideBusy = false;
        primary.IsEnabled = true;
        apps = t.Status == TaskStatus.RanToCompletion ? t.Result : new List<GuideApp>();
        guideApps.Children.Clear();
        if (apps.Count == 0) { guideApps.Children.Add(Line("The client list did not parse.")); return; }
        foreach (GuideApp app in apps)
          guideApps.Children.Add(new CheckBox { Content = $"{app.Name}: {app.State}", IsChecked = false, IsEnabled = app.Selectable, Tag = app.Name, Foreground = B(theme.Text), Margin = new Thickness(0, 2, 0, 2) });
      }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  void Collect()
  {
    string text = guideEntry.Text ?? "";
    if (guideStep == 1 && plan.FirstComputer == 0) { plan.JoinCode = text; plan.ConfirmJoin = true; }
    else if (guideStep == 1) plan.NotesDir = text;
    else if (guideStep == 2)
    {
      plan.Selected.Clear();
      foreach (CheckBox c in guideApps.Children.OfType<CheckBox>()) if (c.IsChecked == true && c.Tag is string name) plan.Selected.Add(name);
    }
    else if (guideStep == 3) { plan.BackupUrl = text; plan.ConfirmBackup = guideBackup.IsChecked == true; }
  }

  // The commands run off the window's thread, so Join and the check do not freeze the window while the core works.
  void RunApply(string failed)
  {
    string? bin = Core.ResolveBin();
    if (bin == null) { guideDetail.Text = failed; if (guideStep == 1) plan.ConfirmJoin = false; return; }
    guideBusy = true;
    primary.IsEnabled = false;
    back.IsEnabled = false;
    GuidePlan snapshot = plan;
    Task.Run(() => (ok: Guide.Apply(snapshot, bin, out string detail), detail)).ContinueWith((t) =>
    {
      guideBusy = false;
      primary.IsEnabled = true;
      back.IsEnabled = true;
      var (ok, detail) = t.Status == TaskStatus.RanToCompletion ? t.Result : (false, "");
      if (!ok) { guideDetail.Text = detail.Length > 0 ? detail : failed; if (guideStep == 1) plan.ConfirmJoin = false; return; }
      if (detail.Length > 0) guideDetail.Text = detail;
      GuideFinish();
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  void GuideNext()
  {
    if (guideBusy) return;
    if (guideStep == 0)
    {
      if (plan.FirstComputer < 0) { guideDetail.Text = "Choose this computer or another one."; return; }
      guideEntry.Text = "";
      GuideShow(1);
      return;
    }
    Collect();
    if (guideStep == 1 && plan.FirstComputer == 0) { RunApply("Join did not run."); return; }
    if (guideStep == 1)
    {
      if (Guide.Build(plan, out string why) == null) { guideDetail.Text = why.Length > 0 ? why : "That folder cannot be used."; return; }
      GuideShow(2);
      return;
    }
    if (guideStep == 2)
    {
      Guide.Build(plan, out string why);
      guideEntry.Text = "";
      GuideShow(3);
      if (why.Length > 0) guideDetail.Text = why;
      return;
    }
    if (guideStep == 3)
    {
      if (Guide.Build(plan, out string why) == null) { guideDetail.Text = why.Length > 0 ? why : "That remote cannot be recorded."; return; }
      GuideShow(4);
      return;
    }
    RunApply("The check did not pass.");
  }

  void GuideBack()
  {
    if (!inGuide || guideStep <= 0 || guideBusy) return;
    if (guideStep == 1) plan.ConfirmJoin = false;
    GuideShow(guideStep - 1);
  }

  void GuideFinish()
  {
    inGuide = false;
    back.Visibility = Visibility.Collapsed;
    primaryLabel.Text = "Refresh";
    SetNav(true);
    try { string m = MarkerPath(); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(m)!); File.WriteAllText(m, "ok\n"); }
    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
    ShowPage(0);
    Reload();
  }

  static IEnumerable<DependencyObject> All(DependencyObject root)
  {
    yield return root;
    foreach (object child in LogicalTreeHelper.GetChildren(root))
      if (child is DependencyObject d) foreach (DependencyObject x in All(d)) yield return x;
  }

  void ReportAndQuit()
  {
    if (inGuide)
    {
      int count = All(this).OfType<Button>().Count((b) => (b.Tag as string) == "primary" && b.IsVisible);
      Program.Out($"guide={pageTitle.Text}");
      Program.Out($"primary_label={primaryLabel.Text}");
      Program.Out($"primary={count}");
      Environment.Exit(pageTitle.Text == "Is this your first computer?" && primaryLabel.Text == "Continue" && count == 1 ? 0 : 1);
    }
    var nodes = All(this).ToList();
    int primaries = nodes.OfType<Button>().Count((b) => (b.Tag as string) == "primary" && b.IsVisible);
    int focusable = nodes.OfType<Button>().Count((b) => b.IsVisible && b.Focusable && b.IsEnabled);
    var texts = nodes.OfType<TextBlock>().Select((t) => t.Text ?? "").ToList();
    bool secret = texts.Any((t) => t.Contains("should-not-appear") || t.Contains("vault-marker"));
    bool accel = InputBindings.OfType<KeyBinding>().Any((k) => k.Key == Key.R && k.Modifiers == ModifierKeys.Control && k.Command == RefreshCommand);
    string health = Read.Health(glance), overview = Read.Overview(glance);
    bool match = healthLink.Text == health && overviewSentence.Text == overview;
    int w = (int)MinWidth, h = (int)MinHeight, sw = (int)Math.Round(sidebar.ActualWidth);
    Program.Out($"health={healthLink.Text}");
    Program.Out($"overview={overviewSentence.Text}");
    Program.Out($"star={glance.NorthStar}");
    Program.Out($"attention={attentionRows}");
    Program.Out($"checks={checks.Children.Count}");
    Program.Out($"primary={primaries}");
    Program.Out($"minw={w}");
    Program.Out($"minh={h}");
    Program.Out($"sidebar={sw}");
    Program.Out($"focusable={focusable}");
    Program.Out($"accel={(accel ? 1 : 0)}");
    Program.Out($"match={(match ? 1 : 0)}");
    Program.Out($"secret={(secret ? "present" : "absent")}");
    Program.Out($"visible={(IsVisible ? 1 : 0)}");
    Program.Out($"theme={theme.Name}");
    bool ok = match && primaries == 1 && w >= 1024 && h >= 700 && sw == 240 && focusable >= 9 && accel && !secret && IsVisible;
    Environment.Exit(ok ? 0 : 1);
  }
}
