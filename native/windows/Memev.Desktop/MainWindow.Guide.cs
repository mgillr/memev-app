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

  /// The apps the core found (onboarding --json), read off the window's thread, drawn with ticks.
  void GuideLoadApps()
  {
    guideRows.Children.Clear();
    guideRows.Children.Add(Note("Reading the apps on this computer."));
    primary.IsEnabled = false;
    guideBusy = true;
    Task.Run(Pages.Apps).ContinueWith((t) =>
    {
      guideBusy = false;
      var (apps, read) = t.Status == TaskStatus.RanToCompletion ? t.Result : (new List<AppItem>(), false);
      var d = pageData.GetValueOrDefault("bots") ?? new PageData();
      d.Apps = apps;
      d.AppsRead = read;
      pageData["bots"] = d;
      ResetTicks("bots");
      guideRows.Children.Clear();
      var shown = new StackPanel();
      BuildConnect(shown, d);
      guideRows.Children.Add(shown);
      if (guideStep == StepConnect) GuideConnectLabel();
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  void GuideConnectLabel()
  {
    int n = Ticked().Count;
    primary.Text = n == 0 ? "Continue" : n == 1 ? "Connect 1 app" : $"Connect {n} apps";
    primary.IsEnabled = true;
    EnforceAmber();
  }

  void GuideLoadSeats()
  {
    guideRows.Children.Clear();
    guideRows.Children.Add(Note("Reading the seats on the message bus."));
    primary.IsEnabled = false;
    guideBusy = true;
    Task.Run(() => CoreCall.JsonIfRunning(new[] { "bus", "roster", "--json" }) as System.Text.Json.Nodes.JsonObject).ContinueWith((t) =>
    {
      guideBusy = false;
      if (t.Status == TaskStatus.RanToCompletion && t.Result != null) roster = t.Result;
      leadSeats = Pages.Seats(roster);
      guideRows.Children.Clear();
      var list = new ListBoxView();
      if (leadSeats.Count == 0) list.Add(new RowView(GlyphKind.Off, "No app has a seat yet.", "Each connected app gets a seat on its first session. Skip this, and choose the lead later with memev bus operator."));
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
        if (Ticked().Count > 0) ConnectSheet();
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
