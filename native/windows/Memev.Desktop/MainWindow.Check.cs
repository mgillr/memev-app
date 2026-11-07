// SPDX-License-Identifier: Apache-2.0
// MEMEV_SELF_CHECK=1: the window prints what it shows and exits, with the same line meanings as the macOS self-check
// (native/macos/test/self-check.sh): primary=, amber_max=, amber_ring=, sheet_ring=, pips=, reads=6, screen=, and the rest.
// It waits for the six first reads and every screen's read, so what it prints does not depend on which answer came first.
// MEMEV_SELF_CHECK_START=1 presses Start Memev's sheet instead; MEMEV_SELF_CHECK_GUIDE_STEP=connect|import|lead shows that step
// of the first launch. It opens a real window for a few seconds, so it runs in CI and is not meant for a person's own screen.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Memev.Desktop;

sealed partial class MainWindow
{
  static void Out(string line) => Program.Out(line);

  async Task Settle()
  {
    UpdateLayout();
    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    UpdateLayout();
  }

  async Task SelfCheck()
  {
    int code;
    try { code = await SelfCheckRun(); }
    catch (Exception e) { Out($"error={e.GetType().Name}: {e.Message}"); code = 2; }
    Environment.Exit(code);
  }

  async Task<int> SelfCheckRun()
  {
    await firstReads.Task;
    await Settle();
    if (Environment.GetEnvironmentVariable("MEMEV_SELF_CHECK_START") != null) return await CheckStart();
    string? step = Environment.GetEnvironmentVariable("MEMEV_SELF_CHECK_GUIDE_STEP");
    if (step != null) return await CheckGuideStep(step);
    if (inGuide)
    {
      int count = AmberRule.VisibleAmber(shell);
      Out($"guide={guideTitle}");
      Out($"primary_label={primary.Text}");
      Out($"primary={count}");
      Out($"visible={(IsVisible ? 1 : 0)}");
      Out($"reads={readsDone}");
      return guideTitle == "Is this your first computer?" && primary.Text == "Continue" && count == 1 && IsVisible && readsDone == 6 ? 0 : 1;
    }

    var nodes = Ui.All(this).ToList();
    int primaries = AmberRule.VisibleAmber(shell);
    bool ring = AmberRule.Ringed(shell);
    int focusable = nodes.OfType<ButtonBase>().Count((b) => Ui.Shown(b) && b.Focusable && b.IsEnabled);
    var (health, overview) = Sentences();
    bool match = healthButton.Sentence.Text == health && Screen("overview").Header.Sentence.Text == overview;
    bool accel = InputBindings.OfType<KeyBinding>().Any((k) => k.Key == Key.R && k.Modifiers == ModifierKeys.Control && k.Command == RefreshCommand);
    int w = (int)MinWidth, h = (int)MinHeight, sw = (int)Math.Round(sidebar.ActualWidth);
    Out($"health={healthButton.Sentence.Text}");
    Out($"overview={Screen("overview").Header.Sentence.Text}");
    Out($"star={glance.NorthStar}");
    Out($"attention={attentionRows}");
    Out($"checks={checkRows}");
    Out($"primary={primaries}");
    Out($"minw={w}");
    Out($"minh={h}");
    Out($"sidebar={sw}");
    Out($"focusable={focusable}");
    Out($"accel={(accel ? 1 : 0)}");
    Out($"match={(match ? 1 : 0)}");
    Out($"theme={Theme.Current.Name}");
    Out($"visible={(IsVisible ? 1 : 0)}");
    Out($"amber_ring={(ring ? 1 : 0)}");
    foreach (string line in Fonts.Report()) Out(line);
    Out($"reads={readsDone}");
    Out($"shown_ms={shownMs}");
    var ps = Pips();
    Out($"pips={healthButton.Pips.Children.Count}");
    foreach (Pip p in ps) Out($"pip={p.Name}:{p.State}");

    // Every screen's read, then every screen and tab, with the most amber buttons a person can see on any one of them (SH-20).
    foreach (string id in DataScreens) await LoadPage(id);
    await LoadScan();
    int most = 0;
    var texts = new List<string>();
    foreach (ScreenDef d in Screens.Nav.Append(Screens.Health))
    {
      Open(d.Id);
      var tabList = Tabs(d.Id);
      Out($"screen={d.Id}|{Screen(d.Id).Header.Title.Text}|{string.Join(",", tabList.Select((t) => t.Label))}");
      foreach (TabItemDef t in tabList.Count == 0 ? new List<TabItemDef> { new() { Id = "", Label = "" } } : tabList)
      {
        if (t.Id.Length > 0) SelectTab(t.Id);
        await Settle();
        int n = AmberRule.VisibleAmber(shell);
        most = Math.Max(most, n);
        if (n != 1 && !d.NoPrimary) Out($"amber_on={d.Id}/{t.Id}:{n}");
        if (!AmberRule.Ringed(shell)) { ring = false; Out($"ring_missing={d.Id}/{t.Id}"); }
        texts.AddRange(Ui.All(this).OfType<TextBlock>().Where((x) => Ui.Shown(x)).Select((x) => x.Text ?? ""));
      }
    }
    Out($"amber_max={most}");

    // The three screens of section 5, read against the core.
    PageData bots = pageData.GetValueOrDefault("bots") ?? new PageData();
    Out($"connect_read={(bots.AppsRead ? "ok" : "fail")}");
    Out($"connect_apps={bots.Apps.Count}");
    Out($"connect_ticked={Ticked().Count}");
    Out($"connect_connected={bots.Apps.Count((a) => a.Connected)}");
    Out($"connect_manual={bots.Apps.Count((a) => a.Manual)}");
    Out($"connect_notfound={bots.NotFound}");
    Out($"agents_read={(bots.RunnersRead ? "ok" : "fail")}");
    Out($"agents_rows={bots.Runners.Count}");

    // A confirmation sheet has no amber button and a red main button; a form sheet has one amber button with its ring.
    bool sheetsOk = true;
    async Task<(int Amber, string Main, bool Ring)> Sheet(string name, Action open)
    {
      open();
      await Settle();
      int a = AmberRule.VisibleAmber(sheet);
      bool r = AmberRule.Ringed(sheet);
      string main = sheet.MainButton == null ? "none" : sheet.MainButton.Kind == ButtonStyleKind.Danger ? "red" : sheet.MainButton.Amber ? "amber" : "plain";
      Out($"sheet={name}|{sheet.Model?.Title}|{sheet.MainButton?.Text ?? ""}|amber={a}|main={main}");
      texts.AddRange(Ui.All(sheet).OfType<TextBlock>().Select((x) => x.Text ?? ""));
      sheet.Dismiss();
      return (a, main, r);
    }
    var stop = await Sheet("stop", () => Present(Confirms.Sheet(ConfirmKind.Stop, "", (x) => x.Dismiss())));
    Out($"confirm_amber={stop.Amber}");
    Out($"confirm_main={stop.Main}");
    var start = await Sheet("start", StartSheet);
    Out($"start_sheet=Start Memev?|Start Memev");
    Out($"sheet_amber={start.Amber}");
    Out($"sheet_ring={(start.Ring ? 1 : 0)}");
    sheetsOk &= stop.Amber == 0 && stop.Main == "red" && start.Amber == 1 && start.Ring;
    if (Ticked().Count > 0) { var c = await Sheet("connect", ConnectSheet); sheetsOk &= c.Amber == 1 && c.Ring; }
    if (bots.Apps.FirstOrDefault((a) => a.Connected) is AppItem con) { var c = await Sheet("disconnect", () => DisconnectSheet(con)); sheetsOk &= c.Amber == 0 && c.Main == "red"; }
    var ag = await Sheet("agents", AgentsSheet);
    sheetsOk &= ag.Amber == 1 && ag.Ring;
    if (bots.Runners.FirstOrDefault((r) => r.Enabled) is RunnerRow rr) { var c = await Sheet("agents_stop", () => StopAgentsSheet(rr.Seat)); sheetsOk &= c.Amber == 0 && c.Main == "red"; }
    var imp = await Sheet("import", ImportSheet);
    sheetsOk &= imp.Amber == 0 && imp.Main == "none";
    Out($"import_read={(scanRead ? "ok" : "fail")}");
    Out($"import_sources={scanSources.Count}");
    Out($"import_marked={ImportTicked().Count}");
    if (ImportTicked().Count > 0) { var ic = await Sheet("import_confirm", () => ConfirmImport(ImportTicked(), null)); sheetsOk &= ic.Amber == 1 && ic.Ring; }
    var undo = await Sheet("import_undo", () => UndoSheet("import-1"));
    sheetsOk &= undo.Amber == 0 && undo.Main == "red";
    foreach (ConfirmKind k in Enum.GetValues<ConfirmKind>())
    {
      Present(Confirms.Sheet(k, "it", (x) => x.Dismiss()));
      await Settle();
      if (AmberRule.VisibleAmber(sheet) != 0 || sheet.MainButton?.Kind != ButtonStyleKind.Danger) { sheetsOk = false; Out($"confirm_bad={k}"); }
      sheet.Dismiss();
    }
    Out($"confirms={Enum.GetValues<ConfirmKind>().Length}");
    Out($"sheets_ok={(sheetsOk ? 1 : 0)}");

    texts.AddRange(Ui.All(this).OfType<TextBlock>().Select((x) => x.Text ?? ""));
    bool secret = texts.Any((t) => t.Contains("should-not-appear") || t.Contains("vault-marker"));
    bool leak = texts.Any((t) => t.Contains(@"\Users\", StringComparison.OrdinalIgnoreCase) || t.Contains("/Users/") || t.Contains("AppData", StringComparison.OrdinalIgnoreCase) || t.Contains("/var/folders/"));
    Out($"secret={(secret ? "present" : "absent")}");
    Out($"leak={(leak ? "present" : "absent")}");
    bool shellOk = most <= 1 && ps.Count == 14 && healthButton.Pips.Children.Count == 14 && sheetsOk;
    bool ok = match && primaries == 1 && ring && readsDone == 6 && w >= 1024 && h >= 700 && sw == 240 && focusable >= 9 && accel && !secret && !leak && IsVisible && shellOk;
    Out($"verdict={(ok ? "pass" : "fail")}");
    return ok ? 0 : 1;
  }

  /// MEMEV_SELF_CHECK_START: opens Start Memev's sheet, presses its main button, and prints what the core answered and what the
  /// window shows once it has read the core again.
  async Task<int> CheckStart()
  {
    StartSheet();
    await Settle();
    Out($"start_sheet={sheet.Model?.Title}|{sheet.MainButton?.Text}");
    Out($"start_before={(glance.DaemonUp ? "running" : "stopped")}");
    Out($"start_banner_before={(banner.Visibility == Visibility.Visible ? "shown" : "hidden")}");
    var done = new TaskCompletionSource<int>();
    onStartDone = (c) => done.TrySetResult(c);
    sheet.MainButton?.OnPress?.Invoke();
    int code = await done.Task;
    await Settle();
    Out($"start_rc={code}");
    Out($"start_result={sheet.Model?.Title}");
    Out($"start_status={statusLine.Text}");
    Out($"start_after={(glance.DaemonUp ? "running" : "stopped")}");
    Out($"start_banner={(banner.Visibility == Visibility.Visible ? "shown" : "hidden")}");
    return 0;
  }

  /// MEMEV_SELF_CHECK_GUIDE_STEP: one of the three steps after the note check, drawn against the core as it is.
  async Task<int> CheckGuideStep(string step)
  {
    int s = step switch { "connect" => StepConnect, "import" => StepImport, _ => StepLead };
    plan.FirstComputer = 1;
    GuideShow(s);
    for (int i = 0; i < 600 && guideBusy; i++) await Task.Delay(100);
    await Settle();
    int amber = AmberRule.VisibleAmber(shell);
    Out($"guide={guideTitle}");
    Out($"primary_label={primary.Text}");
    Out($"primary={amber}");
    Out($"skip={(Ui.Shown(skip) ? 1 : 0)}");
    Out($"guide_rows={Ui.All(guideRows).OfType<RowView>().Count()}");
    var texts = Ui.All(this).OfType<TextBlock>().Select((x) => x.Text ?? "").ToList();
    bool leak = texts.Any((t) => t.Contains("should-not-appear") || t.Contains(@"\Users\", StringComparison.OrdinalIgnoreCase) || t.Contains("AppData", StringComparison.OrdinalIgnoreCase));
    Out($"leak={(leak ? "present" : "absent")}");
    return amber == 1 && Ui.Shown(skip) && !leak ? 0 : 1;
  }
}
