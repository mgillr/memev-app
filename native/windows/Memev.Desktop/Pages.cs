// SPDX-License-Identifier: Apache-2.0
// The pages over the core's own commands, as native/macos/src/Pages.swift reads them, and the three screens of
// native/REV2-SCREENS.md section 5. Every read runs off the window's thread and fills its rows on it. Only the fields the
// map names are shown, and a path is shown only when it is the person's own, shortened with ~ (the owner's ruling D-4).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memev.Reader;

namespace Memev.Desktop;

/// Runs the core with a fixed argument list (native/CLI-CONTRACT.md). No shell; Memev.Reader quotes for a .cmd core.
static class CoreCall
{
  /// (exit code, stdout). -1 when the core could not be found or did not answer in time; 126 when an argument was refused.
  public static (int Code, string Output) Run(string[] args, int timeoutMs = 10000)
  {
    string? bin = Core.ResolveBin();
    if (bin == null) return (-1, "");
    var r = Core.Capture(bin, args, timeoutMs);
    return r.Done ? (r.ExitCode, r.Output) : (-1, "");
  }

  public static bool HaveBinary => Core.ResolveBin() != null;

  public static JsonNode? Json(string[] args, int timeoutMs = 10000)
  {
    var (code, output) = Run(args, timeoutMs);
    return code == 0 ? Parse(output) : null;
  }

  /// The first JSON value in the output, whatever the exit code: onboarding --json exits 1 when one of its items failed.
  public static JsonNode? JsonAnyCode(string[] args, int timeoutMs = 10000)
  {
    var (code, output) = Run(args, timeoutMs);
    return code >= 0 && code != 126 && code != 127 ? Parse(output) : null;
  }

  public static JsonNode? Parse(string text)
  {
    int i = 0;
    // a line of progress before the value is skipped: the value starts at the first line that opens with { or [
    foreach (string line in text.Split('\n'))
    {
      string t = line.TrimStart();
      if (t.StartsWith('{') || t.StartsWith('['))
      {
        try { return JsonNode.Parse(text.Substring(i)); } catch (JsonException) { return null; }
      }
      i += line.Length + 1;
    }
    return null;
  }
}

static class J
{
  public static string Str(JsonNode? v)
  {
    if (v is not JsonValue jv) return "";
    if (jv.TryGetValue(out string? s)) return (s ?? "").Replace("\r", " ").Replace("\n", " ");
    if (jv.TryGetValue(out bool b)) return b ? "true" : "false";
    if (jv.TryGetValue(out JsonElement e)) return e.ValueKind == JsonValueKind.Number ? e.GetRawText() : e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";
    return jv.ToJsonString();
  }

  public static bool True(JsonNode? v) => v is JsonValue jv && jv.TryGetValue(out bool b) && b;

  public static JsonArray Arr(JsonNode? v) => v as JsonArray ?? new JsonArray();

  public static IEnumerable<JsonObject> Objects(JsonNode? v) => Arr(v).OfType<JsonObject>();

  public static int Int(JsonNode? v) => int.TryParse(Str(v), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) ? n : 0;
}

/// What may be shown of a path (the owner's ruling D-4): the person's own paths, with the home folder written as ~. A token,
/// a key, the app's own settings or any other system path is never shown: a text that holds one is dropped.
static class Paths
{
  static string Home()
  {
    string h = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
    if (h.Length == 0) h = Environment.GetEnvironmentVariable("HOME") ?? "";
    return h.TrimEnd('\\', '/');
  }

  public static string Show(string s)
  {
    if (s.Length == 0) return s;
    string home = Home();
    string t = s;
    if (home.Length > 2) t = t.Replace(home, "~", StringComparison.OrdinalIgnoreCase).Replace(home.Replace('\\', '/'), "~", StringComparison.OrdinalIgnoreCase);
    string low = t.ToLowerInvariant();
    if (low.Contains("should-not-appear") || low.Contains("token") || low.Contains("secret") || low.Contains("appdata") || low.Contains(".memev")
        || low.Contains(".ssh") || low.Contains("\\program files") || low.Contains("\\windows\\") || low.Contains("/users/") || low.Contains("\\users\\")
        || low.Contains("/home/") || low.Contains("/private/") || low.Contains("/var/folders/") || low.Contains("/tmp/") || low.Contains("\\temp\\"))
      return "";
    // any other absolute path that is not under the person's own folder
    for (int i = 0; i + 2 < t.Length; i++)
      if (char.IsAsciiLetter(t[i]) && t[i + 1] == ':' && (t[i + 2] == '\\' || t[i + 2] == '/') && (i == 0 || !char.IsAsciiLetterOrDigit(t[i - 1]))) return "";
    if (t.Contains("\\\\", StringComparison.Ordinal)) return "";
    return t;
  }
}

/// One row of a page: a title line and an optional dimmer second line. Tone 0 plain, 1 warn, 2 bad.
sealed record PageRow(string Title, string Sub, int Tone);

sealed class PageSection
{
  public required string Head;
  public List<PageRow> Rows = new();
  public string Empty = "";
  public string Tab = "";
  public string Source = "";
}

/// An AI app the core found (5.1), from one item of onboarding --json.
sealed class AppItem
{
  public required string Id;
  public required string Name;
  public required string Status;
  public string Detail = "";
  public string Note = "";
  public bool Connected => Status == "added" || Status == "ready";
  public bool Declined => Status == "skipped";
  /// Found and supported, not connected: the core's own detail names the command that connects it.
  public bool Connectable => Status == "needs-manual" && Detail == $"memev connect {Id}";
  public bool Manual => Status == "unsupported" || (Status == "needs-manual" && !Connectable);
}

sealed record RunnerRow(string Seat, bool Enabled, int Max, int Today, string Cmd);

sealed record SeatRow(string Seat, string Client, string State, bool Lead);

sealed record ImportRow(string Id, string Items, string Where);

/// What one screen's read returned.
sealed class PageData
{
  public List<PageSection> Sections = new();
  public Dictionary<string, string> Sentences = new();
  public Dictionary<string, int> Counts = new();
  public List<AppItem> Apps = new();
  public bool AppsRead;
  public int NotFound = -1;
  public List<RunnerRow> Runners = new();
  public bool RunnersRead;
  public List<SeatRow> Seats = new();
}

static class Pages
{
  static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

  public static PageData Memory(string query)
  {
    var d = new PageData();
    const string recall = "memev recall <words> --json";
    if (query.Trim().Length > 0)
    {
      string[] words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      var hits = CoreCall.Json(new[] { "recall" }.Concat(words).Append("--json").ToArray()) as JsonArray;
      var sec = new PageSection { Head = "SEARCH", Empty = hits == null ? "The search did not answer." : "Nothing matched.", Tab = "search", Source = recall };
      if (hits != null)
        foreach (JsonObject h in hits.OfType<JsonObject>().Take(20))
        {
          string proj = J.Str(h["project"]);
          sec.Rows.Add(new PageRow(Paths.Show(J.Str(h["snippet"])), Paths.Show(J.Str(h["heading"])) + (proj.Length == 0 ? "" : $" · {proj}"), 0));
        }
      d.Sections.Add(sec);
    }
    const string review = "memev review list --json";
    if (CoreCall.Json(new[] { "review", "list", "--json" }) is JsonObject r && r["inbox"] is JsonArray inbox)
    {
      var sec = new PageSection { Head = "REVIEW", Empty = "Nothing is waiting for review.", Tab = "review", Source = review };
      foreach (JsonObject i in inbox.OfType<JsonObject>().Take(50))
        sec.Rows.Add(new PageRow(Paths.Show(J.Str(i["text"] ?? i["title"])), Paths.Show(J.Str(i["why"] ?? i["reason"] ?? i["flag"])), 1));
      d.Sections.Add(sec);
      int n = inbox.Count;
      d.Counts["review"] = n;
      d.Sentences["review"] = n == 0 ? "Nothing is waiting for review." : $"{Plural(n, "memory", "memories")} to review. Keep what is still true, drop what is not.";
      d.Sentences["search"] = n == 0 ? "Search everything your AI apps have saved." : $"Search everything your AI apps have saved. {n} to review.";
    }
    else d.Sections.Add(new PageSection { Head = "REVIEW", Empty = "The review list did not answer.", Tab = "review", Source = review });
    if (CoreCall.Json(new[] { "audit", "--json" }) is JsonObject a)
      d.Sections.Add(new PageSection { Head = "CLEAN-UP", Tab = "review", Source = "memev audit --json",
        Rows = { new PageRow($"{J.Str(a["memories"])} memories, {J.Str(a["likely_junk"])} likely junk", Paths.Show(J.Str(a["next"])), 0) } });
    else d.Sections.Add(new PageSection { Head = "CLEAN-UP", Empty = "The clean-up report did not answer.", Tab = "review", Source = "memev audit --json" });
    return d;
  }

  public static PageData Checked()
  {
    const string src = "memev contract list";
    if (CoreCall.Json(new[] { "contract", "list" }) is not JsonObject r || r["contracts"] is not JsonArray list)
      return new PageData { Sections = { new PageSection { Head = "CONTRACTS", Empty = "The contract list did not answer.", Source = src } }, Sentences = { [""] = "The contract list did not answer." } };
    // contracts.mjs contractList(): state is done (its verify passed, with a receipt), open (signed, not verified yet), superseded or draft
    var words = new Dictionary<string, string> { ["done"] = "checked: its verify passed", ["open"] = "signed, not checked yet", ["superseded"] = "replaced by a newer version", ["draft"] = "a draft, not signed" };
    var all = list.OfType<JsonObject>().ToList();
    int done = all.Count((c) => J.Str(c["state"]) == "done");
    var sec = new PageSection { Head = "CONTRACTS", Empty = "No contracts yet.", Source = src };
    foreach (JsonObject c in all)
    {
      string state = J.Str(c["state"]), v = J.Str(c["version"]);
      sec.Rows.Add(new PageRow(J.Str(c["title"]) + (v.Length == 0 || v == "1" ? "" : $" (version {v})"), words.GetValueOrDefault(state, state), state == "open" || state == "draft" ? 1 : 0));
    }
    return new PageData { Sections = { sec }, Sentences = { [""] = all.Count == 0 ? "No contracts yet." : $"{Plural(all.Count, "contract", "contracts")}. {done} checked." } };
  }

  /// The AI apps the core found, with their ids, from onboarding --json (REV2-SCREENS.md 5.1). The only output of the trial core
  /// that names an app's id; clients prints names only.
  public static (List<AppItem> Apps, bool Read) Apps()
  {
    var apps = new List<AppItem>();
    var items = CoreCall.JsonAnyCode(new[] { "onboarding", "--json" }, 60000) as JsonArray;
    if (items == null) return (apps, false);
    foreach (JsonObject it in items.OfType<JsonObject>())
    {
      string id = J.Str(it["app"]);
      if (id.Length == 0 || id.Length > 64 || id.Any((c) => !(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_' || c == '.'))) continue;
      string status = J.Str(it["status"]), step = J.Str(it["step"]), detail = Paths.Show(J.Str(it["detail"]));
      if (J.True(it["note"]))
      {
        // a connected app's one manual step: kept with that app
        if (apps.FirstOrDefault((a) => a.Id == id) is AppItem owner) owner.Note = detail;
        continue;
      }
      if (apps.Any((a) => a.Id == id)) continue;
      apps.Add(new AppItem { Id = id, Name = step.Length > 0 ? step : id, Status = status, Detail = detail });
    }
    return (apps, true);
  }

  /// The count of clients rows that read "not found" (BC-10). Text only.
  public static int NotFound()
  {
    var (code, text) = CoreCall.Run(new[] { "clients" });
    if (code != 0) return -1;
    return text.Split('\n').Count((l) => l.TrimEnd('\r').EndsWith("not found", StringComparison.Ordinal));
  }

  public static (List<RunnerRow> Rows, bool Read) Runners()
  {
    var rows = new List<RunnerRow>();
    if (CoreCall.Json(new[] { "runner", "status", "--json" }) is not JsonArray list) return (rows, false);
    foreach (JsonObject r in list.OfType<JsonObject>())
    {
      string seat = J.Str(r["seat"]);
      if (seat.Length == 0) continue;
      rows.Add(new RunnerRow(seat, J.True(r["enabled"]), J.Int(r["max_runs_per_day"]), J.Int(r["today"]), Paths.Show(J.Str(r["cmd"]))));
    }
    return (rows, true);
  }

  public static List<SeatRow> Seats(JsonObject? roster)
  {
    var list = new List<SeatRow>();
    foreach (JsonObject s in J.Objects(roster?["seats"]))
    {
      string seat = J.Str(s["seat"]), state = J.Str(s["state"]);
      if (seat.Length == 0 || state == "removed") continue;
      list.Add(new SeatRow(seat, J.Str(s["client"]), state, J.True(s["operator"])));
    }
    return list;
  }

  /// import list prints one line per import: its id, the item count and where it came from.
  public static List<ImportRow> ParseImports(string text)
  {
    var rows = new List<ImportRow>();
    foreach (string raw in text.Split('\n'))
    {
      string line = raw.TrimEnd('\r').Trim();
      if (line.Length == 0 || line.StartsWith("No imports", StringComparison.Ordinal)) continue;
      string[] parts = line.Split("  ", 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      if (parts.Length < 2 || parts[0].Any(char.IsWhiteSpace) || parts[0].Length > 80) continue;
      rows.Add(new ImportRow(parts[0], parts[1], parts.Length > 2 ? Paths.Show(parts[2]) : ""));
    }
    return rows;
  }

  public static PageData Bots()
  {
    var d = new PageData();
    const string rosterSrc = "memev bus roster --json";
    if (CoreCall.Json(new[] { "bus", "roster", "--json" }) is JsonObject r)
    {
      // bus.mjs roster(): a row is a seat, or a computer with no app yet (seat null); state is live, stale, never joined or removed
      var seats = J.Objects(r["seats"]).ToList();
      var states = new Dictionary<string, string> { ["live"] = "here now", ["stale"] = "not seen recently", ["never joined"] = "has not opened a session yet", ["removed"] = "removed" };
      var sec = new PageSection { Head = "TEAM", Empty = "No seats yet.", Tab = "team", Source = rosterSrc };
      if (J.Str(r["operator"]).Length == 0) sec.Rows.Add(new PageRow("No lead is set.", "Choose which AI leads and coordinates the others.", 1));
      foreach (JsonObject s in seats)
      {
        string seat = J.Str(s["seat"]), state = J.Str(s["state"]);
        string title = seat.Length == 0 ? J.Str(s["device_label"]) : seat;
        string sub = seat.Length == 0 ? "a computer with no AI app on it yet" : string.Join(" · ", new[] { J.True(s["operator"]) ? "the lead" : "", states.GetValueOrDefault(state, state) }.Where((x) => x.Length > 0));
        sec.Rows.Add(new PageRow(title, sub, state == "live" || seat.Length == 0 ? 0 : state == "removed" ? 2 : 1));
      }
      d.Sections.Add(sec);
      var named = seats.Where((s) => J.Str(s["seat"]).Length > 0).ToList();
      int live = named.Count((s) => J.Str(s["state"]) == "live");
      var quiet = named.Where((s) => J.Str(s["state"]) == "stale").Select((s) => J.Str(s["seat"])).ToList();
      string sentence = named.Count == 0 ? "No bots or agents have joined yet." : $"{Plural(named.Count, "bot or agent", "bots and agents")}. {live} live.";
      if (quiet.Count > 0) sentence += quiet.Count == 1 ? $" {quiet[0]} has not been seen recently." : $" {quiet.Count} have not been seen recently.";
      d.Sentences["team"] = sentence;
      d.Counts["quiet"] = quiet.Count;
      d.Seats = Seats(r);
    }
    else d.Sections.Add(new PageSection { Head = "TEAM", Empty = "The roster did not answer.", Tab = "team", Source = rosterSrc });

    var (apps, read) = Apps();
    d.Apps = apps;
    d.AppsRead = read;
    d.NotFound = NotFound();
    int found = apps.Count, connected = apps.Count((a) => a.Connected);
    d.Sentences["connect"] = !read ? "The list of AI apps did not answer." : found == 0 ? "No AI apps were found on this computer."
      : $"{connected} of {found} AI apps found on this computer {(connected == 1 ? "is" : "are")} connected.";

    const string devSrc = "memev devices list --json";
    if (CoreCall.Json(new[] { "devices", "list", "--json" }) is JsonObject dv && dv["devices"] is JsonArray devs)
    {
      // devices.mjs KINDS
      var kinds = new Dictionary<string, string> { ["host"] = "this computer", ["local-client"] = "an app on this computer", ["remote-computer"] = "another computer", ["cloud-connector"] = "a web app" };
      var sec = new PageSection { Head = "COMPUTERS AND APPS", Empty = "No computers or apps yet.", Tab = "connect", Source = devSrc };
      foreach (JsonObject x in devs.OfType<JsonObject>())
        sec.Rows.Add(new PageRow(J.Str(x["label"]), $"{kinds.GetValueOrDefault(J.Str(x["kind"]), J.Str(x["kind"]))} · {J.Str(x["status"])}", J.Str(x["status"]) == "active" ? 0 : 1));
      d.Sections.Add(sec);
    }
    else d.Sections.Add(new PageSection { Head = "COMPUTERS AND APPS", Empty = "The device list did not answer.", Tab = "connect", Source = devSrc });

    var (runners, rread) = Runners();
    d.Runners = runners;
    d.RunnersRead = rread;
    int on = runners.Count((x) => x.Enabled);
    d.Sentences["agents"] = !rread ? "The runner status did not answer." : on == 0 ? "No background agent is on." : $"{Plural(on, "background agent is", "background agents are")} on.";
    return d;
  }

  public static PageData Projects(string project, string star)
  {
    var sec = new PageSection { Head = "CURRENT PROJECT", Empty = "No project is set for this notes folder yet.", Source = "memev card --json" };
    if (CoreCall.Json(new[] { "card", "--json" }) is JsonObject c)
    {
      string p = J.Str(c["project"]), s = J.Str(c["north_star"]);
      if (p.Length > 0 && !p.Contains('/') && !p.Contains('\\')) sec.Rows.Add(new PageRow(p, s.Length == 0 ? "No north star yet." : Paths.Show(s), 0));
    }
    return new PageData { Sections = { sec }, Sentences = { [""] = sec.Rows.Count == 0 ? "No project is set for this notes folder yet." : $"1 project: {sec.Rows[0].Title}." } };
  }

  /// Settings, split by tab: General (the anonymous report and the theme), Plan and computers, Storage and backup.
  public static PageData Settings(string theme)
  {
    var d = new PageData();
    var general = new PageSection { Head = "GENERAL", Tab = "general", Source = "memev report status --json" };
    var plan = new PageSection { Head = "PLAN", Empty = "The plan did not answer.", Tab = "plan", Source = "memev plan --json · memev license status --json" };
    var backup = new PageSection { Head = "BACKUP", Empty = "The backup status did not answer.", Tab = "storage", Source = "memev backup status" };
    if (CoreCall.Json(new[] { "plan", "--json" }) is JsonObject p)
    {
      plan.Rows.Add(new PageRow($"Plan: {J.Str(p["plan"])}", $"{J.Str(p["computers_used"])} of {J.Str(p["computers_allowed"])} computers · {J.Str(p["connectors_counted"])} connectors", 0));
      d.Sentences["plan"] = $"{J.Str(p["plan"])}. {J.Str(p["computers_used"])} of {J.Str(p["computers_allowed"])} computers.";
    }
    if (CoreCall.Json(new[] { "license", "status", "--json" }) is JsonObject l)
    {
      var lic = l["license"] as JsonObject;
      plan.Rows.Add(new PageRow($"Licence: {J.Str(l["tier_label"])}", string.Join(" · ", new[] { J.Str(lic?["state"]), J.Str(lic?["reason"]) }.Where((x) => x.Length > 0)), 0));
    }
    if (CoreCall.Json(new[] { "report", "status", "--json" }) is JsonObject rs)
      general.Rows.Add(new PageRow($"Anonymous report: {(J.True(rs["enabled"]) ? "on" : "off")}", "", 0));
    general.Rows.Add(new PageRow($"Theme: {theme}", "", 0));
    var (code, b) = CoreCall.Run(new[] { "backup", "status" });
    if (code == 0)
    {
      bool on = b.Trim().StartsWith("Backing up to", StringComparison.Ordinal);
      backup.Rows.Add(new PageRow(on ? "Backup: on" : "Backup: not set up", "", on ? 0 : 1));
      d.Sentences["storage"] = on ? "Backup is on." : "Backup is not set up.";
    }
    d.Sections = new() { general, plan, backup };
    return d;
  }

  /// A core's answer for a sheet: each line with paths the person may not see dropped, and lines that name a token left out.
  public static string Answer(string output)
  {
    var lines = output.Replace("\r", "").Split('\n').Select((l) => l.TrimEnd()).Where((l) => l.Length > 0)
      .Select((l) => Paths.Show(l)).Where((l) => l.Length > 0).TakeLast(40);
    return string.Join("\n", lines);
  }

  /// The file a person picked, as the core is given it. Its name is checked by the caller's runner too.
  public static bool PlainFile(string path) => path.Length > 3 && File.Exists(path) && !path.Any((c) => "\"%!&|<>^()".Contains(c) || c < ' ');
}
