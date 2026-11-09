// SPDX-License-Identifier: Apache-2.0
// The guided first launch: this computer or another, where the notes live (or the join code), the backup remote, and the check
// that a note comes back, through Memev.Reader's Guide (native/linux/src/guide.c in C#). Then the three steps of
// native/REV2-SCREENS.md section 5, in order, each with Skip: connect your AI apps, bring in your chat history, choose the
// lead. Nothing runs before the person presses a button, and each change runs only after its sheet.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memev.Reader;

namespace Memev.Desktop;

sealed partial class MainWindow
{
  string guideTitle = "";
  int guideStep;
  bool guideBusy;
  GuidePlan plan = new();
  readonly MButton skip = new("Skip", ButtonStyleKind.Secondary);
  readonly MButton back = new("Back", ButtonStyleKind.Ghost);
  readonly StackPanel guideBody = new();
  readonly TextBlock guideText = Ui.Text("", TypeStyle.Sentence, "sub", 640);
  readonly TextBlock guideDetail = Ui.Text("", TypeStyle.Body, "warn", 640);
  readonly TextBox guideEntry = new() { Width = 520, Height = 32, HorizontalAlignment = HorizontalAlignment.Left };
  readonly CheckBox guideBackup = new() { Content = "Record this remote, and push my notes to it" };
  readonly StackPanel guideChoices = new() { Orientation = Orientation.Horizontal };
  readonly StackPanel guideRows = new();
  NavButton? choiceThis, choiceOther;
  List<SeatRow> leadSeats = new();
  string leadPick = "";
  List<ConnectRow> guideConnect = new();
  GuideFlow guideFlow = new();
  public const int StepConnect = 4, StepImport = 5, StepLead = 6;

  static string MarkerPath() => Path.Combine(ConfigDir(), "desktop-first-launch-done");

  /// MEMEV_GUIDE=off skips it, MEMEV_GUIDE=1 shows it; otherwise it shows until it has been finished once on this computer.
  static bool WantGuide()
  {
    string? flag = Environment.GetEnvironmentVariable("MEMEV_GUIDE");
    if (flag == "off") return false;
    if (flag == "1") return true;
    return !File.Exists(MarkerPath());
  }

  void BuildGuideView()
  {
    if (guideBody.Children.Count > 0) return;
    foreach (Control c in new Control[] { guideEntry })
    {
      c.FontFamily = Fonts.Sans;
      c.FontSize = 14;
      c.SetResourceReference(BackgroundProperty, "page");
      c.SetResourceReference(ForegroundProperty, "text");
      c.SetResourceReference(BorderBrushProperty, "ctl");
      c.VerticalContentAlignment = VerticalAlignment.Center;
      c.Padding = new Thickness(8, 0, 8, 0);
    }
    guideBackup.SetResourceReference(ForegroundProperty, "text");
    guideBackup.FontFamily = Fonts.Sans;
    choiceThis = new NavButton("This is my first computer") { MinWidth = 260, OnPress = () => Pick(1) };
    choiceOther = new NavButton("I already use Memev on another computer") { MinWidth = 260 };
    choiceOther.OnPress = () => Pick(0);
    Ui.Add(guideChoices, 12, choiceThis, choiceOther);
    back.OnPress = GuideBack;
    skip.OnPress = GuideSkip;
    AutomationProperties.SetName(guideEntry, "Your answer");
    foreach (UIElement e in new UIElement[] { guideText, guideChoices, guideEntry, guideBackup, guideRows, guideDetail, back }) Ui.Put(guideBody, e, 16);
  }

  void Pick(int which)
  {
    plan.FirstComputer = which;
    if (choiceThis != null) choiceThis.Selected = which == 1;
    if (choiceOther != null) choiceOther.Selected = which == 0;
  }

  List<MButton> GuideActions() => guideStep >= StepConnect ? new List<MButton> { skip, primary } : new List<MButton> { primary };

  void LeaveGuide() { if (inGuide) inGuide = false; }

  void GuideShow(int step)
  {
    Guide.Log($"show step {step} (was {guideStep}, busy={guideBusy})");
    BuildGuideView();
    inGuide = true;
    guideStep = step;
    current = "guide";
    currentPage = -1;
    CloseDetail();
    var s = Screen("guide");
    s.Content.Children.Clear();
    if (guideBody.Parent is Panel p) p.Children.Remove(guideBody);
    s.Content.Children.Add(guideBody);
    s.TabViews = new();
    foreach (UIElement e in new UIElement[] { guideEntry, guideChoices, guideBackup, guideRows }) e.Visibility = Visibility.Collapsed;
    back.Visibility = step > 0 && step < StepConnect ? Visibility.Visible : Visibility.Collapsed;
    guideDetail.Text = "";
    primary.Text = "Continue";
    primary.IsEnabled = true;
    switch (step)
    {
      case 0:
        guideTitle = "Is this your first computer?";
        guideText.Text = "Nothing is created, connected or sent on this step.";
        guideChoices.Visibility = Visibility.Visible;
        break;
      case 1 when plan.FirstComputer == 0:
        guideTitle = "Join another computer";
        guideText.Text = "Type the one-time code your first computer shows. The join runs only when you press the button. It contacts the other computer.";
        guideEntry.Visibility = Visibility.Visible;
        primary.Text = "Join the other computer";
        break;
      case 1:
        guideTitle = "Where should the notes live?";
        guideText.Text = "Type a full path. The folder is created only when you continue, and only on this computer.";
        guideEntry.Visibility = Visibility.Visible;
        if (guideEntry.Text.Length == 0) guideEntry.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "memev");
        break;
      case 2:
        guideTitle = "Back up to your own git remote?";
        guideText.Text = "Leave this blank to skip it. Nothing is recorded unless you tick the line. If you record a remote, Memev pushes your notes to it: when it starts, every 5 minutes by default, and when it stops.";
        guideEntry.Visibility = Visibility.Visible;
        guideBackup.Visibility = Visibility.Visible;
        guideBackup.IsChecked = false;
        break;
      case 3:
        guideTitle = "Check that a note can be saved";
        guideText.Text = "This creates the notes folder, starts Memev on this computer, saves one note and reads it back. It does not use sample figures.";
        primary.Text = "Check the notes";
        break;
      case StepConnect:
        guideTitle = "Connect your AI apps";
        guideText.Text = "Memev found these apps on this computer. The ticked ones are connected when you press the button, after a sheet that names them. Apps Memev cannot connect by itself show the step to do by hand.";
        guideRows.Visibility = Visibility.Visible;
        GuideLoadApps();
        break;
      case StepImport:
        guideTitle = "Bring in your existing AI chat history?";
        guideText.Text = "Memev found the history your AI apps keep on this computer. The ticked ones are brought in after one sheet that confirms them; it runs in the background, and an import can be undone. For a ChatGPT or Claude export file, use Memory, Search, Import later.";
        guideRows.Visibility = Visibility.Visible;
        GuideLoadScan();
        break;
      case StepLead:
        guideTitle = "Which app should lead?";
        guideText.Text = "The lead hands tasks to the others and is the one you talk to. You can change it later.";
        guideRows.Visibility = Visibility.Visible;
        GuideLoadSeats();
        break;
    }
    Render();
  }

  /// Every app the core found (discover --json, decided by GuideModel), read off the window's thread: auto apps ticked, one-step
  /// apps with their step and a Copy button, the rest found and not connectable.
  void GuideLoadApps()
  {
    guideFlow = new GuideFlow();
    guideRows.Children.Clear();
    guideRows.Children.Add(Note("Reading the apps on this computer."));
    primary.IsEnabled = false;
    guideBusy = true;
    Task.Run(Pages.Discovered).ContinueWith((t) =>
    {
      guideBusy = false;
      var (rows, read) = t.Status == TaskStatus.RanToCompletion ? t.Result : (new List<ConnectRow>(), false);
      guideConnect = rows;
      Guide.Log($"connect step: read={read} found={rows.Count} ticked={rows.Count((r) => r.Ticked)}");
      guideRows.Children.Clear();
      var list = new ListBoxView();
      if (!read) list.Add(new RowView(null, "The list of AI apps did not answer.", "Press Skip, and connect apps later from AI apps."));
      else if (rows.Count == 0) list.Add(new RowView(null, "No AI apps were found on this computer.", "Install one, then connect it later from AI apps."));
      foreach (ConnectRow row in rows)
      {
        UIElement lead;
        MButton? copy = null;
        if (row.HasTick)
        {
          var box = new CheckBox { IsChecked = row.Ticked, IsEnabled = row.Enabled, VerticalAlignment = VerticalAlignment.Center };
          AutomationProperties.SetName(box, row.App.Name);
          ConnectRow r = row;
          box.Checked += (_, _) => { r.Ticked = true; GuideConnectLabel(); };
          box.Unchecked += (_, _) => { r.Ticked = false; GuideConnectLabel(); };
          lead = box;
        }
        else
        {
          lead = new Border { Width = 16 };
          string text = row.Copy;
          if (text.Length > 0) copy = new MButton("Copy", ButtonStyleKind.Secondary, onPress: () => { try { Clipboard.SetText(text); statusLine.Text = "Copied."; } catch (System.Runtime.InteropServices.ExternalException) { statusLine.Text = "The clipboard is busy; try again."; } });
        }
        list.Add(new RowView(row.App.Automation == "auto" ? null : GlyphKind.Off, row.App.Name, row.State, technical ? $"memev connect {row.App.Id}" : "", button: copy, tech: technical, lead: lead));
      }
      guideRows.Children.Add(list);
      if (guideStep == StepConnect) GuideConnectLabel();
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  void GuideConnectLabel()
  {
    int n = (GuideModel.ConnectArgs(guideConnect)?.Length ?? 2) - 2;
    primary.Text = n == 0 ? "Continue" : n == 1 ? "Connect 1 app" : $"Connect {n} apps";
    primary.IsEnabled = !guideFlow.Connecting;
    EnforceAmber();
    Render();
  }

  /// One sheet that names the ticked apps; the connect runs only after it, and the guide moves on only when it has ended. A
  /// result that is not all connected lists each app's outcome and seat, the doctor's fix text, and Fix the safe ones.
  void GuideConnectSheet(string[] args)
  {
    var names = guideConnect.Where((r) => args.Contains(r.App.Id)).Select((r) => r.App.Name).ToList();
    Present(new SheetModel
    {
      Title = names.Count == 1 ? $"Connect {names[0]}?" : $"Connect {names.Count} AI apps?",
      Note = "Memev adds itself to the settings of each app below and keeps a copy of the old settings first. Each app gets its own seat on the message bus. You can disconnect any of them later.",
      Rows = names.Select((n) => new DetailRow(n, "", Glyph: GlyphKind.Ok)).ToList(),
      Source = "memev " + string.Join(' ', args), Main = "Connect",
      OnMain = (h) =>
      {
        guideFlow.ConnectStarted();
        GuideConnectLabel();
        h.Status.Text = "Connecting. This can take a few seconds.";
        if (h.MainButton != null) h.MainButton.IsEnabled = false;
        Task.Run(() =>
        {
          var (code, output) = Pages.GuideRun(args);
          var (apps, _) = GuideModel.ParseConnect(output);
          var fixes = apps.Any((a) => !a.Ok) || code != 0 ? GuideModel.ParseDoctor(Pages.GuideRun(new[] { "doctor", "--json" }).Output) : new List<FixItem>();
          return (code, output, apps, fixes);
        }).ContinueWith((t) =>
        {
          var (code, output, apps, fixes) = t.Status == TaskStatus.RanToCompletion ? t.Result : (-1, "", new List<ConnectOutcome>(), new List<FixItem>());
          Guide.Log($"connect: exit={code} apps={string.Join(",", apps.Select((a) => $"{a.Id}={a.Status}"))} fixes={fixes.Count}");
          guideFlow.ConnectEnded();
          GuideConnectResult(apps, fixes, code, output);
          GuideShow(StepImport);
        }, TaskScheduler.FromCurrentSynchronizationContext());
      },
    });
  }

  void GuideConnectResult(List<ConnectOutcome> apps, List<FixItem> fixes, int code, string output)
  {
    var failed = apps.Where((a) => !a.Ok).Select((a) => a.Id).ToList();
    var rows = apps.Select((a) => new DetailRow(guideConnect.FirstOrDefault((r) => r.App.Id == a.Id)?.App.Name ?? a.Id,
      a.Ok ? (a.Seat.Length > 0 ? $"connected · seat {a.Seat}" : "connected") : $"not connected{(a.Detail.Length > 0 ? " · " + a.Detail : "")}",
      Glyph: a.Ok ? GlyphKind.Ok : GlyphKind.Warn)).ToList();
    rows.AddRange(GuideModel.FixLines(fixes).Select((l) => new DetailRow("To fix", l, Glyph: GlyphKind.Warn)));
    bool allOk = code == 0 && failed.Count == 0 && apps.Count > 0;
    var m = new SheetModel
    {
      Title = allOk ? "Connected" : "Not every app connected",
      Note = allOk ? "Open a new session in each app: it starts with your memory." : "The apps below that did not connect say why. Fix the safe ones runs the fixes that change nothing of yours, then tries those apps once more.",
      Rows = rows, Source = "memev connect --json · memev doctor --json", Main = "Close",
    };
    if (!allOk && failed.Count > 0 && GuideModel.OfferSafeFix(fixes))
    {
      m.Second = "Fix the safe ones";
      m.OnSecond = (h) =>
      {
        h.Status.Text = "Fixing, then trying again.";
        foreach (MButton b in h.OtherButtons) b.IsEnabled = false;
        Task.Run(() => GuideModel.FixAndRetry(Pages.GuideRun, failed)).ContinueWith((t) =>
        {
          var retry = t.Status == TaskStatus.RanToCompletion ? t.Result.Retry : new List<ConnectOutcome>();
          Guide.Log($"fix the safe ones: retry={string.Join(",", retry.Select((a) => $"{a.Id}={a.Status}"))}");
          var fixesAfter = retry.Any((a) => !a.Ok) ? GuideModel.ParseDoctor(Pages.GuideRun(new[] { "doctor", "--json" }).Output) : new List<FixItem>();
          GuideConnectResult(retry, fixesAfter, retry.All((a) => a.Ok) && retry.Count > 0 ? 0 : 1, "");
        }, TaskScheduler.FromCurrentSynchronizationContext());
      };
    }
    Present(m);
    if (apps.Count == 0 && output.Length > 0) sheet.ShowOutput(Pages.Answer(output));
    guideDetail.Text = allOk ? "" : string.Join("\n", GuideModel.FixLines(fixes).Take(3));
  }

  /// The lead step reads the roster with Memev running (it starts Memev when it is not): each seat registered at connect is a
  /// choice, and with none the step offers Retry.
  void GuideLoadSeats()
  {
    guideRows.Children.Clear();
    guideRows.Children.Add(Note("Starting Memev if it is not running, then reading the seats on the message bus."));
    primary.IsEnabled = false;
    guideBusy = true;
    Task.Run(() => GuideModel.LoadLead(Pages.GuideRun)).ContinueWith((t) =>
    {
      guideBusy = false;
      var lc = t.Status == TaskStatus.RanToCompletion ? t.Result : new LeadChoice();
      Guide.Log($"lead step: read={lc.Read} started={lc.Started} seats={lc.Seats.Count}");
      leadSeats = lc.Seats.Select((x) => new SeatRow(x.Seat, x.Client, x.State, x.Lead)).ToList();
      guideRows.Children.Clear();
      var list = new ListBoxView();
      if (lc.Retry)
      {
        var retry = new MButton("Retry", ButtonStyleKind.Secondary, onPress: GuideLoadSeats);
        list.Add(new RowView(GlyphKind.Off, lc.Read ? "No app has a seat yet." : "The message bus did not answer.", "A connected app gets its seat when it connects, or on its first session. Press Retry, or skip this and choose the lead later with memev bus operator.", button: retry));
      }
      leadPick = leadSeats.FirstOrDefault((x) => x.Lead)?.Seat ?? leadSeats.FirstOrDefault()?.Seat ?? "";
      foreach (SeatRow seat in leadSeats)
      {
        var radio = new RadioButton { GroupName = "lead", IsChecked = seat.Seat == leadPick, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(radio, seat.Seat);
        string name = seat.Seat;
        radio.Checked += (_, _) => { leadPick = name; GuideLeadLabel(); };
        list.Add(new RowView(null, seat.Seat, string.Join(" · ", new[] { seat.Client, seat.State, seat.Lead ? "the lead now" : "" }.Where((x) => x.Length > 0)), lead: radio));
      }
      guideRows.Children.Add(list);
      GuideLeadLabel();
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  void GuideLeadLabel()
  {
    primary.Text = leadPick.Length == 0 ? "Finish" : $"Make {leadPick} the lead";
    primary.IsEnabled = true;
    Render();
  }

  void Collect()
  {
    string text = guideEntry.Text ?? "";
    if (guideStep == 1 && plan.FirstComputer == 0) { plan.JoinCode = text; plan.ConfirmJoin = true; }
    else if (guideStep == 1) plan.NotesDir = text;
    else if (guideStep == 2) { plan.BackupUrl = text; plan.ConfirmBackup = guideBackup.IsChecked == true; }
    plan.Selected.Clear();
    string? port = Environment.GetEnvironmentVariable("MEMEV_GUIDE_PORT");
    if (int.TryParse(port, out int n) && n > 0 && n < 65536) plan.Port = n;
  }

  // The commands run off the window's thread, so Join and the check do not freeze the window while the core works.
  void RunApply(string failed)
  {
    string? bin = Core.ResolveBin();
    if (bin == null) { guideDetail.Text = failed; if (guideStep == 1) plan.ConfirmJoin = false; return; }
    guideBusy = true;
    primary.IsEnabled = false;
    back.IsEnabled = false;
    guideDetail.Text = "Working. This can take a minute.";
    GuidePlan snapshot = plan;
    Task.Run(() => (ok: Guide.Apply(snapshot, bin, out string detail), detail)).ContinueWith((t) =>
    {
      guideBusy = false;
      primary.IsEnabled = true;
      back.IsEnabled = true;
      var (ok, d) = t.Status == TaskStatus.RanToCompletion ? t.Result : (false, "");
      Guide.Log($"apply result: ok={ok} task={t.Status}{(t.Exception != null ? " error=" + t.Exception.GetBaseException().GetType().Name + ": " + t.Exception.GetBaseException().Message : "")} detail={d.Replace("\n", " | ")}");
      if (!ok) { guideDetail.Text = d.Length > 0 ? Paths.Show(d) : failed; if (guideStep == 1) plan.ConfirmJoin = false; return; }
      guideDetail.Text = "";
      Reload();
      // A computer that joined another one's memory skips the three questions: its memory lives on the other computer.
      if (plan.FirstComputer == 0) GuideFinish(); else GuideShow(StepConnect);
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  void GuideNext()
  {
    Guide.Log($"continue pressed on step {guideStep} (busy={guideBusy})");
    if (guideBusy) return;
    switch (guideStep)
    {
      case 0:
        if (plan.FirstComputer < 0) { guideDetail.Text = "Choose this computer or another one."; return; }
        guideEntry.Text = "";
        GuideShow(1);
        return;
      case 1:
        Collect();
        if (plan.FirstComputer == 0) { RunApply("Join did not run."); return; }
        if (Guide.Build(plan, out string why1) == null) { guideDetail.Text = why1.Length > 0 ? why1 : "That folder cannot be used."; return; }
        guideEntry.Text = "";
        GuideShow(2);
        return;
      case 2:
        Collect();
        if (Guide.Build(plan, out string why2) == null) { guideDetail.Text = why2.Length > 0 ? why2 : "That remote cannot be recorded."; return; }
        GuideShow(3);
        return;
      case 3:
        RunApply("The check did not pass.");
        return;
      case StepConnect:
        // the import step shows only when the connect run has ended (GuideConnectSheet moves on when it ends)
        if (guideFlow.Connecting) { guideDetail.Text = "Connecting. The next step shows when it is done."; return; }
        if (GuideModel.ConnectArgs(guideConnect) is string[] connectArgs) { GuideConnectSheet(connectArgs); return; }
        guideFlow.Next();
        GuideShow(StepImport);
        return;
      case StepImport:
        if (ImportTicked().Count > 0) ConfirmImport(ImportTicked(), () => GuideShow(StepLead));
        else GuideShow(StepLead);
        return;
      default:
        if (leadPick.Length == 0) { GuideFinish(); return; }
        // the import stops Memev while it runs, and choosing the lead needs Memev running
        if (importRunning) { guideDetail.Text = "The import is still running. Choose the lead when it is done, or skip this and choose it later."; return; }
        string seat = leadPick;
        GuideFinish();
        Present(new SheetModel
        {
          Title = $"Make {seat} the lead?",
          Note = "The lead hands tasks to the other apps and is the one you talk to. Only this computer can choose it, and you can change it later.",
          Source = $"memev bus operator {seat}", Main = "Make it the lead",
          OnMain = (h) => RunInSheet(h, $"Making {seat} the lead.", new[] { "bus", "operator", seat }, 30000, (code, answer) =>
          {
            Result(code == 0 ? $"{seat} leads" : "The lead was not changed", code, answer, $"{seat} hands out tasks to the others now.", $"memev bus operator {seat}");
            Reload();
          }),
        });
        return;
    }
  }

  void GuideSkip()
  {
    if (guideBusy || guideStep < StepConnect) return;
    if (guideStep == StepLead) GuideFinish(); else GuideShow(guideStep + 1);
  }

  void GuideBack()
  {
    if (!inGuide || guideStep <= 0 || guideStep >= StepConnect || guideBusy) return;
    if (guideStep == 1) plan.ConfirmJoin = false;
    GuideShow(guideStep - 1);
  }

  void GuideFinish()
  {
    inGuide = false;
    primary.Text = "Continue";
    if (!selfCheck)
      try { string m = MarkerPath(); Directory.CreateDirectory(Path.GetDirectoryName(m)!); File.WriteAllText(m, "ok\n"); }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
    Open("overview");
    Reload();
  }
}
