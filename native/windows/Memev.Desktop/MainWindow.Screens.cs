// SPDX-License-Identifier: Apache-2.0
// The screens' content: Overview and Health from the glance and the doctor, the data screens from their reads, the placeholder
// tabs that say what they wait for, and the three screens of native/REV2-SCREENS.md section 5 (Connect, Import history and
// Background agents) with their sheets.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memev.Reader;

namespace Memev.Desktop;

sealed partial class MainWindow
{
  // ---- building blocks

  /// A labelled block: the section label, the command when technical detail is on, then the body.
  StackPanel Block(string label, IEnumerable<UIElement> body, bool sample = false, string source = "", MButton? link = null)
  {
    var b = new StackPanel();
    Ui.Put(b, Parts.SectionLabel(label, sample, link), 12);
    if (technical && source.Length > 0) Ui.Put(b, Ui.Text(source, TypeStyle.Code, "dim"), 12);
    foreach (UIElement v in body) Ui.Put(b, v, 12);
    return b;
  }

  /// Adds a block to a screen's content with the spacing of 1.1: 48 px before a labelled section, 24 before an unlabelled one.
  static void Put(UIElement v, StackPanel s, bool labelled = true) => Ui.Put(s, v, labelled ? 48 : 24);

  static TextBlock Note(string text) => Ui.Text(text, TypeStyle.Body, "sub", 640);

  UIElement? LaterBlock(string key)
  {
    if (!Screens.LaterItems.TryGetValue(key, out LaterItem[]? items) || items.Length == 0) return null;
    return Later.Block(items, (it, row) => OpenDetail(new DetailModel { Key = $"later:{it.Title}", Title = it.Title, Sample = true, Why = it.Why, Plan = it.Plan, Source = it.Source }, row));
  }

  RowView CheckRow(string title, string status, MButton? button = null)
  {
    JsonObjectView e = new(DoctorEntry(title));
    string detailText = Paths.Show(e.Detail);
    var r = new RowView(Kind(status), title, detailText, technical ? $"memev doctor --json · {e.Id}" : "", button: button, tech: technical);
    r.OnPress = () =>
    {
      var facts = new List<DetailFact> { new("Check", title) };
      string fix = Paths.Show(e.Fix);
      if (fix.Length > 0) facts.Add(new("To fix", fix));
      string word = status == "ok" ? "Fine" : status == "fail" ? "Failed" : "Needs a look";
      OpenDetail(new DetailModel
      {
        Key = $"check:{title}", Title = title, State = (Kind(status), word),
        Why = detailText.Length == 0 ? "The core gave no more detail for this check." : detailText, Facts = facts, Source = "memev doctor --json",
      }, r);
    };
    return r;
  }

  /// The id, detail and fix of one doctor check; fix may be an object with a description.
  readonly struct JsonObjectView
  {
    public readonly string Id, Detail, Fix;
    public JsonObjectView(System.Text.Json.Nodes.JsonObject? o)
    {
      Id = J.Str(o?["id"]);
      Detail = J.Str(o?["detail"]);
      Fix = o?["fix"] is System.Text.Json.Nodes.JsonObject f ? J.Str(f["desc"]) : J.Str(o?["fix"]);
    }
  }

  /// Shows the first keep rows of a list and hides the rest with their dividers.
  void Collapse(ListBoxView list, int keep)
  {
    int n = 0;
    foreach (UIElement v in list.Stack.Children)
    {
      if (v is RowView) n++;
      v.Visibility = n > keep ? Visibility.Collapsed : Visibility.Visible;
    }
    EnforceAmber();
  }

  // ---- Overview and Health

  void BuildOverview(ScreenView s)
  {
    s.Content.Children.Clear();
    attentionRows = 0;
    if (glance.NorthStar.Length > 0) Put(Ui.Text(glance.NorthStar, TypeStyle.BodyMedium, "text", 640), s.Content, false);
    bool up = glance.DaemonUp;
    // OV-11: a running Memev with no notes yet offers to bring in what the person already has.
    if (glanceRead && up && glance.Notes == 0)
    {
      var imp = new MButton("Import what you already have", ButtonStyleKind.Secondary, 2, true, ImportSheet);
      Put(Ui.V(16, Note("Memory fills as you work in a connected app. You can also bring in the chat history you already have."), imp), s.Content, false);
    }
    var list = new ListBoxView();
    var rows = new List<RowView>();
    if (!glanceRead) list.Add(new RowView(null, "Reading Memev."));
    else if (!up) list.Add(new RowView(Stopped ? GlyphKind.Bad : GlyphKind.Off, "Nothing to list while Memev is not running."));
    else if (glance.Look == 0) list.Add(new RowView(GlyphKind.Ok, "All clear.", "Nothing needs you. Every check the core returned is fine."));
    else
      foreach (Check c in glance.Checks.Where((c) => c.Status != "ok"))
      {
        // SH-20: a row's "See why" may be the screen's one amber button; the rule picks the first.
        var why = new MButton("See why", ButtonStyleKind.Secondary, 2);
        var r = CheckRow(c.Title, c.Status, why);
        why.OnPress = () => r.OnPress?.Invoke();
        rows.Add(r);
        list.Add(r);
        attentionRows++;
      }
    MButton? link = null;
    if (rows.Count > 2)
    {
      var l = new MButton($"Show all ({rows.Count})", ButtonStyleKind.Link);
      l.OnPress = () => { bool showing = l.Text.StartsWith("Show fewer", StringComparison.Ordinal); l.Text = showing ? $"Show all ({rows.Count})" : "Show fewer"; Collapse(list, showing ? 2 : rows.Count); };
      link = l;
    }
    Put(Block("NEEDS YOU", new UIElement[] { list }, source: "memev doctor --json", link: link), s.Content);
    if (rows.Count > 2) Collapse(list, 2);
    Put(Block("TODAY", new UIElement[] { Note("Progress by project, bots now and runs today need the core's work status and runner status, which this window does not read yet. No sample figures are shown.") }), s.Content);
    if (LaterBlock("overview") is UIElement later) Put(later, s.Content, false);
  }

  void BuildHealth(ScreenView s)
  {
    s.Content.Children.Clear();
    checkRows = 0;
    bool up = glance.DaemonUp;
    if (!glanceRead)
    {
      var l = new ListBoxView();
      l.Add(new RowView(null, "Reading Memev."));
      Put(Block("CHECKS", new UIElement[] { l }, source: "memev doctor --json"), s.Content);
      var svc0 = new ListBoxView();
      svc0.Add(new RowView(null, "Reading Memev.", code: "memev status --json", tech: technical));
      Put(Block("MEMEV SERVICE", new UIElement[] { svc0 }, source: "memev status --json"), s.Content);
      return;
    }
    if (Stopped)
    {
      var start = new MButton("Start Memev", ButtonStyleKind.Secondary, 2, onPress: StartSheet);
      var l = new ListBoxView();
      l.Add(new RowView(GlyphKind.Bad, "Memev is not running", "start it and your apps share their memory again", button: start));
      Put(Block("TO LOOK AT", new UIElement[] { l }), s.Content);
    }
    if (!up || glance.Checks.Count == 0)
    {
      var l = new ListBoxView();
      l.Add(new RowView(GlyphKind.Off, "No checks were returned."));
      checkRows = 1;
      Put(Block("CHECKS", new UIElement[] { l }, source: "memev doctor --json"), s.Content);
    }
    else
    {
      var look = new List<RowView>();
      var fine = new List<RowView>();
      var fineNames = new List<string>();
      foreach (Check c in glance.Checks)
      {
        if (c.Status == "ok") { fine.Add(CheckRow(c.Title, c.Status)); fineNames.Add(c.Title.ToLowerInvariant()); }
        else look.Add(CheckRow(c.Title, c.Status));
      }
      checkRows = look.Count + fine.Count;
      if (look.Count > 0)
      {
        var l = new ListBoxView();
        foreach (RowView r in look) l.Add(r);
        Put(Block("TO LOOK AT", new UIElement[] { l }, source: "memev doctor --json"), s.Content);
      }
      if (fine.Count > 0)
      {
        var e = new ExpanderRow(fine.Count == 1 ? "1 check is fine" : $"{fine.Count} checks are fine", string.Join(" · ", fineNames.Take(7)));
        var l = new ListBoxView();
        foreach (RowView r in fine) l.Add(r);
        e.Body.Children.Add(l);
        e.OnToggle = EnforceAmber;
        Put(e, s.Content, look.Count == 0);
      }
      if (Pips().Any((p) => p.State == "off"))
      {
        var e = new ExpanderRow("Off by your choice (1)", "web access for cloud AI");
        var l = new ListBoxView();
        l.Add(new RowView(GlyphKind.Off, "Web access for cloud AI", "off until you switch it on", button: new MButton("Manage", ButtonStyleKind.Secondary, onPress: () => Open("bots", "connect"))));
        e.Body.Children.Add(l);
        e.OnToggle = EnforceAmber;
        Put(e, s.Content, false);
      }
    }
    var svc = new ListBoxView();
    svc.Add(new RowView(up ? GlyphKind.Ok : GlyphKind.Bad, up ? "Running" : "Stopped", up ? "the core's status answers" : "the core's status does not answer", "memev status --json", tech: technical));
    Put(Block("MEMEV SERVICE", new UIElement[] { svc }, source: "memev status --json"), s.Content);
    var help = new ExpanderRow("Get help", "Help and support");
    var hl = new ListBoxView();
    hl.Add(new RowView(null, "Help and support", "email, report a problem, ask for a feature", button: new MButton("Open", ButtonStyleKind.Secondary, onPress: () => Open("settings", "help"))));
    help.Body.Children.Add(hl);
    help.OnToggle = EnforceAmber;
    Put(help, s.Content, false);
    if (LaterBlock("health") is UIElement later) Put(later, s.Content, false);
  }

  // ---- data screens

  /// A data screen's content from what its read returned, each section under its tab.
  void BuildData(string id, ScreenView s, PageData? d)
  {
    var tabs = Def(id).Tabs;
    var keys = tabs.Length == 0 ? new[] { "" } : tabs.Select((t) => t.Id).ToArray();
    var views = keys.ToDictionary((k) => k, (k) => new StackPanel());
    if (id == "memory" && views.TryGetValue("search", out StackPanel? sv))
    {
      if (search.Parent is Panel p) p.Children.Remove(search);
      search.Margin = new Thickness(0);
      sv.Children.Add(search);
    }
    if (d != null)
    {
      foreach (PageSection sec in d.Sections)
      {
        StackPanel? v = views.GetValueOrDefault(sec.Tab) ?? views.GetValueOrDefault("");
        if (v == null) continue;
        var list = new ListBoxView();
        if (sec.Rows.Count == 0 && sec.Empty.Length > 0) list.Add(new RowView(null, sec.Empty));
        foreach (PageRow r in sec.Rows)
        {
          var row = new RowView(r.Tone == 2 ? GlyphKind.Bad : r.Tone == 1 ? GlyphKind.Warn : null, r.Title, r.Sub);
          row.OnPress = () => OpenDetail(new DetailModel
          {
            Key = $"row:{sec.Head}:{r.Title}", Title = r.Title, Why = r.Sub, Source = sec.Source,
            State = r.Tone == 0 ? null : (r.Tone == 2 ? GlyphKind.Bad : GlyphKind.Warn, r.Tone == 2 ? "Removed" : "Needs a look"),
          }, row);
          list.Add(row);
        }
        if (sec.Rows.Count > 0 || sec.Empty.Length > 0) Put(Block(sec.Head, new UIElement[] { list }, source: sec.Source), v);
      }
      if (id == "bots")
      {
        BuildConnect(views["connect"], d);
        BuildAgents(views["agents"], d);
      }
    }
    else
      foreach (string k in keys)
        if (!Screens.NotBuilt.ContainsKey($"{id}/{k}")) Put(Note("Reading Memev."), views[k], false);
    if (id == "memory" && d?.Sections.Any((x) => x.Head == "SEARCH") != true && views.TryGetValue("search", out StackPanel? sv2))
      Put(Note("Type words and press Enter to search everything your AI apps have saved."), sv2, false);
    if (id == "settings" && views.TryGetValue("general", out StackPanel? gv)) Put(SettingsControls(), gv);
    foreach (string k in keys)
    {
      if (Screens.NotBuilt.TryGetValue($"{id}/{k}", out string? why)) Put(Note(why), views[k], false);
      if (LaterBlock(k.Length == 0 ? id : $"{id}/{k}") is UIElement later) Put(later, views[k], false);
    }
    if (LaterBlock(id) is UIElement l2 && !keys.Contains("")) Put(l2, views[keys[0]], false);
    Install(views, s, id);
  }

  void BuildPlaceholder(string id, ScreenView s)
  {
    var tabs = Def(id).Tabs;
    var keys = tabs.Length == 0 ? new[] { "" } : tabs.Select((t) => t.Id).ToArray();
    var views = new Dictionary<string, StackPanel>();
    foreach (string k in keys)
    {
      var v = new StackPanel();
      if (id == "project" && k == "overview")
      {
        var l = new ListBoxView();
        l.Add(new RowView(null, "North star", glance.NorthStar.Length == 0 ? "No north star yet." : glance.NorthStar));
        Put(Block("THE PROJECT", new UIElement[] { l }, source: "memev card --json"), v);
      }
      if (Screens.NotBuilt.TryGetValue($"{id}/{k}", out string? why)) Put(Note(why), v, false);
      if (LaterBlock(k.Length == 0 ? id : $"{id}/{k}") is UIElement later) Put(later, v, false);
      views[k] = v;
    }
    if (LaterBlock(id) is UIElement l2 && keys.Length > 0) Put(l2, views[keys[0]], false);
    Install(views, s, id);
  }

  void Install(Dictionary<string, StackPanel> views, ScreenView s, string id)
  {
    s.Content.Children.Clear();
    s.TabViews = views;
    var order = Def(id).Tabs.Select((t) => t.Id).ToArray();
    foreach (string k in order.Length == 0 ? new[] { "" } : order)
      if (views.TryGetValue(k, out StackPanel? v)) s.Content.Children.Add(v);
  }

  /// Settings, General: Appearance and Technical detail, both settings of this app (SH-23, SH-24).
  UIElement SettingsControls()
  {
    var l = new ListBoxView();
    bool forced = Environment.GetEnvironmentVariable("MEMEV_THEME") != null;
    var choices = new StackPanel { Orientation = Orientation.Horizontal };
    foreach (var (key, label) in new[] { ("system", "This computer"), ("dark", "Dark"), ("light", "Light") })
    {
      var b = new MButton(label, appearance == key ? ButtonStyleKind.Secondary : ButtonStyleKind.Ghost, onPress: () => AppearanceChanged(key)) { IsEnabled = !forced };
      AutomationProperties.SetName(b, $"Appearance: {label}");
      Ui.Put(choices, b, 8);
    }
    l.Add(new RowView(null, "Appearance", "dark, light, or as this computer is set", button: choices));
    var sw = new MButton(technical ? "Switch off" : "Switch on", ButtonStyleKind.Secondary, onPress: () => TechnicalChanged(!technical));
    AutomationProperties.SetName(sw, "Technical detail");
    l.Add(new RowView(null, "Technical detail", technical ? "on · the commands and the core's own words behind each screen" : "off · show the commands and the core's own words behind each screen", button: sw));
    return Block("THIS APP", new UIElement[] { l });
  }

  // ---- 5.1 Connect: the apps the core found, with ticks

  readonly Dictionary<string, bool> ticks = new();

  void ResetTicks(string id)
  {
    if (id != "bots") return;
    ticks.Clear();
    foreach (AppItem a in pageData["bots"].Apps.Where((a) => a.Connectable || a.Declined)) ticks[a.Id] = a.Connectable;
  }

  List<AppItem> Ticked() => (pageData.GetValueOrDefault("bots")?.Apps ?? new()).Where((a) => ticks.GetValueOrDefault(a.Id)).ToList();

  MButton? connectButton;

  MButton? ConnectAction()
  {
    var d = pageData.GetValueOrDefault("bots");
    if (d == null || !d.Apps.Any((a) => a.Connectable || a.Declined)) return null;
    int n = Ticked().Count;
    connectButton ??= new MButton("", ButtonStyleKind.Secondary, 1, onPress: ConnectSheet);
    connectButton.Text = n == 1 ? "Connect 1 app" : $"Connect {n} apps";
    connectButton.IsEnabled = n > 0;
    return connectButton;
  }

  void BuildConnect(StackPanel v, PageData d)
  {
    var block = new List<UIElement>();
    var list = new ListBoxView();
    if (!d.AppsRead) list.Add(new RowView(null, "The list of AI apps did not answer."));
    else if (d.Apps.Count == 0) list.Add(new RowView(null, "No AI apps were found on this computer.", "Install one, then press Refresh."));
    foreach (AppItem a in d.Apps)
    {
      string state = a.Connected ? "connected" : a.Declined ? "you disconnected it" : a.Connectable ? "found · not connected" : a.Status == "unsupported" ? "found · not supported yet" : "found · needs one step by hand";
      string sub = a.Manual ? $"{state} · {a.Detail}" : a.Note.Length > 0 ? $"{state} · {a.Note}" : state;
      CheckBox? box = null;
      if (a.Connected || a.Connectable || a.Declined)
      {
        box = new CheckBox { IsChecked = a.Connected || ticks.GetValueOrDefault(a.Id), IsEnabled = !a.Connected, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(box, a.Name);
        string id = a.Id;
        box.Checked += (_, _) => { ticks[id] = true; if (inGuide) GuideConnectLabel(); Render(); };
        box.Unchecked += (_, _) => { ticks[id] = false; if (inGuide) GuideConnectLabel(); Render(); };
      }
      else box = null;
      MButton? disc = a.Connected ? new MButton("Disconnect", ButtonStyleKind.Secondary, onPress: () => DisconnectSheet(a)) : null;
      var lead = box ?? (UIElement)new Border { Width = 16 };
      var r = new RowView(a.Manual ? GlyphKind.Off : null, a.Name, sub, technical ? $"memev connect {a.Id}" : "", button: disc, tech: technical, lead: lead);
      r.OnPress = () => OpenDetail(new DetailModel
      {
        Key = $"app:{a.Id}", Title = a.Name, State = (a.Connected ? GlyphKind.Ok : a.Manual ? GlyphKind.Off : GlyphKind.Warn, state),
        Why = "Before it connects an app, Memev shows exactly what it will change, and keeps a copy of the app's old settings.",
        Facts = new List<DetailFact> { new("App", a.Name), new("State", state) }.Concat(a.Manual ? new[] { new DetailFact("By hand", a.Detail) } : Array.Empty<DetailFact>()).ToList(),
        Source = $"memev onboarding --json · memev connect {a.Id}",
      }, r);
      list.Add(r);
    }
    block.Add(list);
    if (d.NotFound >= 0) block.Add(Note(d.NotFound == 1 ? "1 more app Memev can connect is not on this computer." : $"{d.NotFound} more apps Memev can connect are not on this computer."));
    var b = Block("AI APPS ON THIS COMPUTER", block, source: "memev onboarding --json · memev clients");
    v.Children.Insert(0, b);
  }

  /// BC-S3: one sheet naming the apps, then connect with exactly their ids.
  void ConnectSheet()
  {
    var apps = Ticked();
    if (apps.Count == 0) return;
    string[] args = new[] { "connect" }.Concat(apps.Select((a) => a.Id)).ToArray();
    Present(new SheetModel
    {
      Title = apps.Count == 1 ? $"Connect {apps[0].Name}?" : $"Connect {apps.Count} AI apps?",
      Note = "Memev adds itself to the settings of each app below and keeps a copy of the old settings first. Each app gets its own seat on the message bus. You can disconnect any of them later.",
      Rows = apps.Select((a) => new DetailRow(a.Name, a.Declined ? "you disconnected it earlier; this connects it again" : "", Glyph: GlyphKind.Ok)).ToList(),
      Source = "memev " + string.Join(' ', args), Main = "Connect",
      OnMain = (h) => RunInSheet(h, "Connecting. This can take a few seconds.", args, 120000, (code, answer) =>
      {
        Result(code == 0 ? "Connected" : "Not every app connected", code, answer, "Open a new session in each app: it starts with your memory.", "memev " + string.Join(' ', args));
        _ = LoadPage("bots");
      }),
    });
  }

  void DisconnectSheet(AppItem a)
  {
    Present(Confirms.Sheet(ConfirmKind.Disconnect, a.Name, (h) => RunInSheet(h, $"Disconnecting {a.Name}.", new[] { "disconnect", a.Id }, 60000, (code, answer) =>
    {
      Result(code == 0 ? $"{a.Name} is disconnected" : $"{a.Name} was not disconnected", code, answer, "Its old settings are back. Memev connect brings it back at any time.", $"memev disconnect {a.Id}");
      _ = LoadPage("bots");
    })));
  }

  // ---- 5.3 Background agents

  MButton? agentsButton;

  MButton? AgentsAction()
  {
    if (pageData.GetValueOrDefault("bots") is not PageData d || !d.RunnersRead) return null;
    agentsButton ??= new MButton("Add background agents", ButtonStyleKind.Secondary, 1, onPress: AgentsSheet);
    return agentsButton;
  }

  void BuildAgents(StackPanel v, PageData d)
  {
    var list = new ListBoxView();
    if (!d.RunnersRead) list.Add(new RowView(null, "The runner status did not answer."));
    else if (d.Runners.Count == 0) list.Add(new RowView(GlyphKind.Off, "No background agent yet.", "Add some for a connected app; each one needs a daily limit."));
    foreach (RunnerRow r in d.Runners)
    {
      string sub = r.Enabled ? $"on · {r.Today} of {r.Max} runs today" : "off";
      MButton? stop = r.Enabled ? new MButton("Stop", ButtonStyleKind.Secondary, onPress: () => StopAgentsSheet(r.Seat)) : null;
      var row = new RowView(r.Enabled ? GlyphKind.Live : GlyphKind.Off, r.Seat, sub, r.Cmd, button: stop, tech: technical);
      row.OnPress = () => OpenDetail(new DetailModel
      {
        Key = $"runner:{r.Seat}", Title = r.Seat, State = (r.Enabled ? GlyphKind.Live : GlyphKind.Off, r.Enabled ? "On" : "Off"),
        Why = "It takes one unattended turn when the lead posts it a task, until the day's limit.",
        Facts = { new("Runs today", r.Today.ToString(System.Globalization.CultureInfo.InvariantCulture)), new("Most a day", r.Max.ToString(System.Globalization.CultureInfo.InvariantCulture)) },
        Source = "memev runner status --json",
      }, row);
      list.Add(row);
    }
    var b = Block("BACKGROUND AGENTS", new UIElement[] { list, Note("A background agent takes its turns while memev runner watch runs for its app. The trial core has no service that keeps that running yet.") }, source: "memev runner status --json");
    v.Children.Insert(0, b);
  }

  /// BB-S1: the app, how many agents and the daily limit, which is required.
  void AgentsSheet()
  {
    var apps = (pageData.GetValueOrDefault("bots")?.Apps ?? new()).Where((a) => a.Connected).ToList();
    var pick = new ComboBox { Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
    foreach (AppItem a in apps) pick.Items.Add(new ComboBoxItem { Content = a.Name, Tag = a.Id });
    if (pick.Items.Count > 0) pick.SelectedIndex = 0;
    AutomationProperties.SetName(pick, "App");
    var count = new TextBox { Text = "1", Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
    AutomationProperties.SetName(count, "How many agents");
    var limit = new TextBox { Text = "", Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
    AutomationProperties.SetName(limit, "Most runs a day");
    UIElement Field(string label, string hint, Control c) => Ui.V(4, Ui.Text(label, TypeStyle.BodyMedium, "text"), Ui.Text(hint, TypeStyle.Small, "sub"), c);
    var fields = new List<UIElement>
    {
      apps.Count == 0 ? Note("No app is connected yet. Connect one on the Connect tab first.") : Field("App", "one of the apps that are connected", pick),
      Field("How many agents", "from 1 to 20", count),
      Field("Most runs a day", "required: a whole number of 1 or more", limit),
    };
    Present(new SheetModel
    {
      Title = "Add background agents",
      Note = "Each agent takes one unattended turn when the lead posts it a task, until the day's limit.",
      Fields = fields, Source = "memev runner on <app> --agents N --max-runs-per-day M", Main = "Start",
      OnMain = (h) =>
      {
        string? app = (pick.SelectedItem as ComboBoxItem)?.Tag as string;
        if (app == null) { h.Status.Text = "Choose an app that is connected."; return; }
        if (!int.TryParse(count.Text.Trim(), out int n) || n < 1 || n > 20) { h.Status.Text = "How many agents: a whole number from 1 to 20."; return; }
        if (!int.TryParse(limit.Text.Trim(), out int m) || m < 1) { h.Status.Text = "Most runs a day is required: a whole number of 1 or more. Nothing was started."; return; }
        string[] args = { "runner", "on", app, "--agents", n.ToString(System.Globalization.CultureInfo.InvariantCulture), "--max-runs-per-day", m.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        RunInSheet(h, "Starting the background agents.", args, 30000, (code, answer) =>
        {
          Result(code == 0 ? "Background agents are on" : "The background agents did not start", code, answer, "They take their turns while memev runner watch runs for this app.", "memev " + string.Join(' ', args));
          _ = LoadPage("bots");
        });
      },
    });
  }

  void StopAgentsSheet(string seat)
  {
    Present(Confirms.Custom($"Stop the background agents for {seat}?", "It takes no more turns until you start it again. Work it already did is kept.", "Stop", $"memev runner off {seat}",
      (h) => RunInSheet(h, $"Stopping {seat}.", new[] { "runner", "off", seat }, 30000, (code, answer) =>
      {
        Result(code == 0 ? $"{seat} is off" : $"{seat} was not stopped", code, answer, "It takes no more turns.", $"memev runner off {seat}");
        _ = LoadPage("bots");
      })));
  }

  // ---- 5.2 Import history

  MButton? importButton;
  MButton importAction => importButton ??= new MButton("Import", ButtonStyleKind.Secondary, onPress: ImportSheet);

  /// MEM-S2: rows for AI app history, a chat export and earlier imports; no main button.
  void ImportSheet()
  {
    var rows = new ListBoxView();
    rows.Add(new RowView(null, "Your AI apps' history on this computer", "each app's sessions, with their count, size and dates; you pick what comes in",
      button: new MButton("Choose", ButtonStyleKind.Secondary, onPress: ScanSheet)));
    rows.Add(new RowView(null, "A chat export (.zip)", "from ChatGPT or Claude: their export file", button: new MButton("Choose", ButtonStyleKind.Secondary, onPress: ChooseExport)));
    rows.Add(new RowView(null, "Earlier imports", "each can be undone", button: new MButton("List", ButtonStyleKind.Secondary, onPress: ImportList)));
    Present(new SheetModel
    {
      Title = "Import",
      Note = "Nothing is kept until you confirm it, and each import can be undone.",
      Fields = { rows }, Source = "memev import scan --json · memev import --export FILE.zip --preview · memev import list",
    });
  }

  void ChooseExport()
  {
    var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Chat export (*.zip)|*.zip", CheckFileExists = true, Multiselect = false, Title = "Choose a ChatGPT or Claude export" };
    if (dlg.ShowDialog(this) != true) return;
    string f = dlg.FileName;
    if (!Pages.PlainFile(f))
    {
      sheet.Status.Text = "That file's name or folder holds a character this window will not pass to the core: \" % ! & | < > ^ ( ). Copy it to a plain name, such as export.zip, and choose that.";
      return;
    }
    ImportPreview(new[] { "--export", f }, "that export");
  }

  void ImportPreview(string[] what, string label)
  {
    string[] args = new[] { "import" }.Concat(what).Append("--preview").ToArray();
    RunInSheet(sheet, $"Looking at {label}. Memev stops for a moment and starts again.", args, 180000, (code, answer) =>
    {
      if (code != 0) { Result("The preview did not run", code, answer, "", "memev import ... --preview"); return; }
      bool nothing = answer.Contains("\n0 item(s) to import", StringComparison.Ordinal) || answer.StartsWith("0 item(s) to import", StringComparison.Ordinal);
      string[] keep = new[] { "import" }.Concat(what).Append("--yes").ToArray();
      Present(new SheetModel
      {
        Title = nothing ? "Nothing new to bring in" : $"Keep what Memev found in {label}?",
        Note = nothing ? "Memev found nothing that is not already in your memory." : "This is what Memev would bring in. Nothing is kept until you press Import. Memev stops for a moment while it imports, then starts again. You can undo the import later.",
        Source = "memev import ... --yes", Main = nothing ? null : "Import",
        OnMain = (h) => RunInSheet(h, "Importing.", keep, 300000, (c2, a2) =>
        {
          Result(c2 == 0 ? "Imported" : "The import did not finish", c2, a2, "It is in your memory now. Earlier imports in the Import sheet can undo it.", "memev import ... --yes");
          Reload();
        }),
      });
      sheet.ShowOutput(answer);
    });
  }

  void ImportList()
  {
    RunInSheet(sheet, "Reading the earlier imports. Memev stops for a moment and starts again.", new[] { "import", "list" }, 60000, (code, answer) =>
    {
      if (code != 0) { Result("The import list did not answer", code, answer, "", "memev import list"); return; }
      var list = Pages.ParseImports(answer);
      var rows = new ListBoxView();
      if (list.Count == 0) rows.Add(new RowView(null, "No imports yet."));
      foreach (ImportRow r in list)
        rows.Add(new RowView(null, r.Id, string.Join(" · ", new[] { r.Items, r.Where }.Where((x) => x.Length > 0)), button: new MButton("Undo", ButtonStyleKind.Secondary, onPress: () => UndoSheet(r.Id))));
      Present(new SheetModel { Title = "Earlier imports", Note = "Undo takes everything one import brought in back out of your memory.", Fields = { rows }, Source = "memev import list" });
    });
  }

  void UndoSheet(string id)
  {
    Present(Confirms.Custom($"Undo import {id}?", "Everything that import brought in leaves your memory.", "Undo import", $"memev import undo {id}",
      (h) => RunInSheet(h, "Undoing the import.", new[] { "import", "undo", id }, 120000, (code, answer) =>
      {
        Result(code == 0 ? "The import is undone" : "The import was not undone", code, answer, "What it brought in has left your memory.", $"memev import undo {id}");
        Reload();
      })));
  }
}
