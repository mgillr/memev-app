// SPDX-License-Identifier: Apache-2.0
// The decisions of the guide's last three steps, with no window: which AI apps are listed and ticked, which are connected,
// when the import step may show, how the lead step finds the seats, and what the guide says and runs when something is not
// done. Memev.Desktop only shows what this returns, so Memev.Reader.Check tests it on any computer against a fixture core.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memev.Reader;

/// One app the core found: from `discover --json` (id, name, found, evidence, install_path, automation, step), or, on a core
/// without discover, from `onboarding --json` (app, status, detail).
public sealed class FoundApp
{
  public string Id { get; init; } = "";
  public string Name { get; init; } = "";
  public bool Found { get; init; } = true;
  /// auto: Memev connects it; one-step: one step by hand, shown with its text; none: found, and Memev cannot connect it.
  public string Automation { get; init; } = "none";
  public string Step { get; init; } = "";
  public string Evidence { get; init; } = "";
  public bool Connected { get; init; }
  /// The person disconnected it earlier: listed with its tick off, and they may tick it again.
  public bool Declined { get; init; }
}

/// A row of the Connect step, as the window draws it.
public sealed class ConnectRow
{
  public required FoundApp App;
  public bool HasTick;      // auto apps only
  public bool Ticked;       // ticked by default when auto and not connected yet
  public bool Enabled;      // a tick the person may change
  public string Copy = "";  // the one-step text, for its Copy button
  public string State = "";
}

/// What connect --json said for one app.
public sealed class ConnectOutcome
{
  public string Id = "";
  public string Status = "";
  public string Seat = "";
  public string Detail = "";
  public bool Verified = true;
  /// configured: the app shares its settings file with another (ChatGPT desktop and Codex), so the core wrote them and no
  /// session has shown it loads them yet. Connected, not yet verified; never a failure.
  public bool Unverified => Status == "configured" || !Verified;
  public bool Ok => Status is "connected" or "added" or "ready" or "ok" or "already" or "configured";
  public string Shown => !Ok ? "not connected" : Unverified ? "configured · not yet seen to load it" : Status == "already" ? "already connected" : "connected";
}

public enum ConnectKind { AllConnected, SomeFailed, Unreported }

/// What one connect run came to, for the ids it was sent. All connected only when the core exited 0 and reported every id as
/// connected: a core that prints no outcome for an id it was sent (one that ignores --json and prints a text line) is
/// Unreported, never a success with an empty list.
public sealed class ConnectSummary
{
  public ConnectKind Kind;
  public List<string> Missing = new();       // sent, and no outcome came back
  public List<string> NotConnected = new();  // failed or missing: what Fix the safe ones tries again
  public string Title => Kind switch
  {
    ConnectKind.AllConnected => "Connected",
    ConnectKind.Unreported => "Memev did not report the result",
    _ => "Not every app connected",
  };
}

/// A doctor check that is not ok, with the text that says how to fix it.
public sealed class FixItem
{
  public string Id = "";
  public string Title = "";
  public string Status = "";
  public string Detail = "";
  public string FixText = "";
  public bool Safe;           // the core can fix it, and its fix is marked destructive false and is not by hand
  public bool Manual;         // a step for the person: Fix the safe ones skips it and shows its text
  public List<string> Related = new();
}

/// The seats the lead step offers, after Memev was started if it was not running.
public sealed class LeadChoice
{
  public bool Read;           // the roster answered
  public bool Started;        // this call ran start
  public List<(string Seat, string Client, string State, bool Lead)> Seats = new();
  public bool Retry => Seats.Count == 0;
}

/// The order of the guide's last steps. The import step shows only after the connect run has ended.
public sealed class GuideFlow
{
  public const int Connect = 4, Import = 5, Lead = 6;
  public int Step { get; private set; } = Connect;
  public bool Connecting { get; private set; }

  public void ConnectStarted() => Connecting = true;

  public void ConnectEnded()
  {
    Connecting = false;
    if (Step == Connect) Step = Import;
  }

  /// Continue on the Connect step with nothing ticked, or on the Import step.
  public bool Next()
  {
    if (Connecting) return false;
    if (Step == Connect) Step = Import;
    else if (Step == Import) Step = Lead;
    else return false;
    return true;
  }
}

/// Runs the core with a fixed argument list: (exit code, output). -1 when it did not answer.
public delegate (int Code, string Output) CoreRun(string[] args);

public static class GuideModel
{
  static bool IdOk(string id) => id.Length > 0 && id.Length <= 64 && id[0] != '-' && id.All((c) => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_' || c == '.');

  static string Str(JsonNode? n)
  {
    if (n is not JsonValue v) return "";
    if (v.TryGetValue(out string? s)) return s ?? "";
    if (v.TryGetValue(out bool b)) return b ? "true" : "false";
    return v.ToJsonString();
  }

  static bool True(JsonNode? n) => n is JsonValue v && v.TryGetValue(out bool b) && b;

  /// The first JSON value in the output; a line of progress before it is skipped. Several lines that each hold an object
  /// (one object per line) are read as an array.
  public static JsonNode? Parse(string text)
  {
    text ??= "";
    int at = -1, i = 0;
    foreach (string line in text.Split('\n'))
    {
      string t = line.TrimStart();
      if (t.StartsWith('{') || t.StartsWith('[')) { at = i; break; }
      i += line.Length + 1;
    }
    if (at < 0) return null;
    string rest = text.Substring(at);
    try { return JsonNode.Parse(rest); }
    catch (JsonException)
    {
      var arr = new JsonArray();
      foreach (string line in rest.Split('\n'))
      {
        string t = line.Trim();
        if (t.Length == 0) continue;
        try { if (JsonNode.Parse(t) is JsonObject o) arr.Add(o); else return null; } catch (JsonException) { return null; }
      }
      return arr.Count > 0 ? arr : null;
    }
  }

  static IEnumerable<JsonObject> Items(JsonNode? n, params string[] keys)
  {
    if (n is JsonArray a) return a.OfType<JsonObject>();
    if (n is JsonObject o)
    {
      foreach (string k in keys) if (o[k] is JsonArray inner) return inner.OfType<JsonObject>();
      // an object keyed by id
      return o.Where((kv) => kv.Value is JsonObject).Select((kv) => { var c = (JsonObject)kv.Value!.DeepClone(); if (c["id"] == null) c["id"] = kv.Key; return c; });
    }
    return Enumerable.Empty<JsonObject>();
  }

  /// `discover --json`, or `onboarding --json` from a core that has no discover.
  public static List<FoundApp> ParseApps(string text)
  {
    var apps = new List<FoundApp>();
    foreach (JsonObject it in Items(Parse(text), "apps", "clients", "items"))
    {
      if (apps.Count >= 128) break;
      string id = Str(it["id"]);
      if (id.Length == 0) id = Str(it["app"]);
      if (!IdOk(id) || apps.Any((a) => a.Id == id)) continue;
      FoundApp app;
      if (it["automation"] != null || it["found"] != null)
      {
        string auto = Str(it["automation"]);
        if (auto != "auto" && auto != "one-step") auto = "none";
        string status = Str(it["status"]);
        app = new FoundApp
        {
          Id = id, Name = Str(it["name"]) is { Length: > 0 } n ? n : id, Found = it["found"] == null || True(it["found"]),
          Automation = auto, Step = Str(it["step"]), Evidence = Str(it["evidence"]),
          Connected = True(it["connected"]) || status is "connected" or "added" or "ready",
          Declined = True(it["declined"]) || status == "skipped",
        };
      }
      else
      {
        if (True(it["note"])) continue; // a connected app's own manual note, not an app
        string status = Str(it["status"]), detail = Str(it["detail"]);
        bool connected = status is "added" or "ready";
        bool connectable = status == "needs-manual" && detail == $"memev connect {id}";
        string auto = connected || connectable || status == "skipped" ? "auto" : status == "needs-manual" ? "one-step" : "none";
        app = new FoundApp { Id = id, Name = Str(it["step"]) is { Length: > 0 } s ? s : id, Automation = auto, Step = auto == "one-step" ? detail : "", Connected = connected, Declined = status == "skipped" };
      }
      if (app.Found) apps.Add(app);
    }
    return apps;
  }

  /// discover --json says what is installed, not what is connected. The connection state the core does give is in
  /// onboarding --json: an app row (not a note) with status ready or added is connected, and skipped is one the person
  /// disconnected. Apps it does not name keep the state discover gave.
  public static List<FoundApp> MarkConnected(IEnumerable<FoundApp> apps, string onboardingText)
  {
    var state = new Dictionary<string, string>();
    foreach (JsonObject it in Items(Parse(onboardingText), "items"))
    {
      string id = Str(it["app"]);
      if (!IdOk(id) || True(it["note"]) || state.ContainsKey(id)) continue;
      state[id] = Str(it["status"]);
    }
    return apps.Select((a) => !state.TryGetValue(a.Id, out string? st) ? a : new FoundApp
    {
      Id = a.Id, Name = a.Name, Found = a.Found, Automation = a.Automation, Step = a.Step, Evidence = a.Evidence,
      Connected = a.Connected || st is "ready" or "added", Declined = a.Declined || st == "skipped",
    }).ToList();
  }

  /// Every app found, in the core's order: auto apps ticked (connected ones ticked and fixed), one-step apps with their step
  /// and no tick, the rest found and not connectable, with a tick that is off and cannot be changed.
  public static List<ConnectRow> Rows(IEnumerable<FoundApp> apps)
  {
    var rows = new List<ConnectRow>();
    foreach (FoundApp a in apps)
    {
      if (a.Automation == "auto")
        rows.Add(new ConnectRow { App = a, HasTick = true, Ticked = !a.Declined || a.Connected, Enabled = !a.Connected, State = a.Connected ? "connected" : a.Declined ? "found · you disconnected it" : "found · Memev connects it" });
      else if (a.Automation == "one-step")
        rows.Add(new ConnectRow { App = a, HasTick = false, Copy = a.Step, State = a.Step.Length > 0 ? $"found · one step by hand: {a.Step}" : "found · one step by hand" });
      else
        rows.Add(new ConnectRow { App = a, HasTick = true, Ticked = false, Enabled = false, State = "found · Memev cannot connect it" });
    }
    return rows;
  }

  /// The connect command for the ticked rows: only auto apps not connected yet, each id once. null when there is none, so
  /// the window never runs connect with no app.
  public static string[]? ConnectArgs(IEnumerable<ConnectRow> rows)
  {
    var ids = rows.Where((r) => r.HasTick && r.Enabled && r.Ticked && r.App.Automation == "auto" && !r.App.Connected && IdOk(r.App.Id))
      .Select((r) => r.App.Id).Distinct().ToList();
    return ids.Count == 0 ? null : new[] { "connect" }.Concat(ids).Append("--json").ToArray();
  }

  /// `connect --json`: each app's status and seat, and the lead (operator) when the core names one.
  public static (List<ConnectOutcome> Apps, string Operator) ParseConnect(string text)
  {
    var list = new List<ConnectOutcome>();
    JsonNode? n = Parse(text);
    string op = n is JsonObject o ? Str(o["operator"]) : "";
    if (n is JsonObject oo && oo["bus"] is JsonObject bus && op.Length == 0) op = Str(bus["operator"]);
    foreach (JsonObject it in Items(n, "apps", "clients", "results"))
    {
      string id = Str(it["id"]);
      if (id.Length == 0) id = Str(it["app"]);
      if (!IdOk(id) || list.Any((x) => x.Id == id)) continue;
      string seat = it["seat"] is JsonObject so ? Str(so["seat"]) : Str(it["seat"]);
      list.Add(new ConnectOutcome { Id = id, Status = Str(it["status"]), Seat = seat, Detail = Str(it["detail"]) is { Length: > 0 } d ? d : Str(it["reason"]) is { Length: > 0 } rr ? rr : Str(it["error"]), Verified = Str(it["verified"]) != "false" });
    }
    return (list, op);
  }

  public static ConnectSummary Summarize(IEnumerable<string> sent, int code, IEnumerable<ConnectOutcome> outcomes)
  {
    var ids = sent.Distinct().ToList();
    var byId = outcomes.GroupBy((o) => o.Id).ToDictionary((g) => g.Key, (g) => g.First());
    var sum = new ConnectSummary();
    sum.Missing = ids.Where((id) => !byId.ContainsKey(id)).ToList();
    var failed = ids.Where((id) => byId.TryGetValue(id, out var o) && !o.Ok).ToList();
    sum.NotConnected = failed.Concat(sum.Missing).ToList();
    sum.Kind = ids.Count == 0 || sum.Missing.Count > 0 ? ConnectKind.Unreported
      : failed.Count > 0 || code != 0 ? ConnectKind.SomeFailed : ConnectKind.AllConnected;
    return sum;
  }

  /// The guide's detail line under a connect result: empty when all connected; otherwise what happened and the doctor's
  /// first fix line, or where to look when the doctor named none.
  public static string ResultDetail(ConnectSummary sum, IEnumerable<FixItem> fixes)
  {
    if (sum.Kind == ConnectKind.AllConnected) return "";
    string first = FixLines(fixes).FirstOrDefault() ?? "Health shows Memev's checks.";
    string head = sum.Kind == ConnectKind.Unreported
      ? $"Memev did not report the result for {string.Join(", ", sum.Missing)}."
      : $"Not connected: {string.Join(", ", sum.NotConnected)}.";
    return $"{head} {first}";
  }

  /// `doctor --json`: every check that is not ok, with its fix text (the fix's desc, else its manual sentence, else its detail).
  public static List<FixItem> ParseDoctor(string text)
  {
    var list = new List<FixItem>();
    foreach (JsonObject c in Items(Parse(text), "checks"))
    {
      string status = Str(c["status"]);
      if (status == "ok" || status.Length == 0) continue;
      string fixText = c["fix"] is JsonObject f ? Str(f["desc"]) : "";
      if (fixText.Length == 0) fixText = Str(c["manual"]);
      if (fixText.Length == 0 && c["fix"] is JsonObject f2) fixText = Str(f2["manual"]);
      bool manual = c["fix"] is JsonObject fm ? True(fm["manual"]) || (fm["desc"] == null && fm["manual"] != null) : Str(c["manual"]).Length > 0;
      bool safe = !manual && c["fix"] is JsonObject f3 && f3["destructive"] is JsonValue dv && dv.TryGetValue(out bool destructive) && !destructive;
      var related = (c["related"] as JsonArray)?.Select(Str).Where(IdOk).ToList() ?? new List<string>();
      list.Add(new FixItem { Id = Str(c["id"]), Title = Str(c["title"]), Status = status, Detail = Str(c["detail"]), FixText = fixText.Length > 0 ? fixText : Str(c["detail"]), Safe = safe, Manual = manual, Related = related });
    }
    return list;
  }

  /// The lines the Result sheet and the guide's detail line show: each check that is not ok, with how to fix it.
  public static List<string> FixLines(IEnumerable<FixItem> items) =>
    items.Select((i) => i.Manual ? $"{(i.Title.Length > 0 ? i.Title : i.Id)}: by hand: {i.FixText}" : $"{(i.Title.Length > 0 ? i.Title : i.Id)}: {i.FixText}{(i.Safe ? "" : " (by hand)")}").ToList();

  /// The steps only the person can do, from checks whose fix is by hand: shown as steps, never run.
  public static List<string> ManualSteps(IEnumerable<FixItem> items) => items.Where((i) => i.Manual && i.FixText.Length > 0).Select((i) => i.FixText).Distinct().ToList();

  /// Whether Fix the safe ones is offered: at least one check the core can fix without asking.
  public static bool OfferSafeFix(IEnumerable<FixItem> items) => items.Any((i) => i.Safe);

  /// Fix the safe ones: doctor --fix --safe-only once, then connect once for the apps that did not connect. The commands run,
  /// in order, and the outcomes of the retry.
  public static (List<string[]> Ran, List<ConnectOutcome> Retry) FixAndRetry(CoreRun run, IEnumerable<string> failedIds)
  {
    var ran = new List<string[]>();
    var fix = new[] { "doctor", "--fix", "--safe-only" };
    ran.Add(fix);
    run(fix);
    var ids = failedIds.Where(IdOk).Distinct().ToList();
    if (ids.Count == 0) return (ran, new List<ConnectOutcome>());
    var again = new[] { "connect" }.Concat(ids).Append("--json").ToArray();
    ran.Add(again);
    var (_, output) = run(again);
    return (ran, ParseConnect(output).Apps);
  }

  /// The lead step: Memev must run for the roster to hold the seats connect registered, so a stopped Memev is started first
  /// (the setup guide's check step already said it starts Memev on this computer). Seats in state removed are left out.
  public static LeadChoice LoadLead(CoreRun run)
  {
    var lc = new LeadChoice();
    if (run(new[] { "status", "--json" }).Code != 0)
    {
      lc.Started = true;
      if (run(new[] { "start" }).Code != 0) return lc;
    }
    var (code, output) = run(new[] { "bus", "roster", "--json" });
    if (code != 0 || Parse(output) is not JsonObject r) return lc;
    lc.Read = true;
    string op = Str(r["operator"]);
    foreach (JsonObject s in Items(r["seats"]))
    {
      string seat = Str(s["seat"]), state = Str(s["state"]);
      if (seat.Length == 0 || state == "removed") continue;
      lc.Seats.Add((seat, Str(s["client"]), state, True(s["operator"]) || seat == op));
    }
    return lc;
  }
}
