// SPDX-License-Identifier: Apache-2.0
// The live pages and the Operations lead prompt. Same rules as native/linux/src/ops.c: fixed argument
// lists, no shell, and no paths, tokens or raw JSON on the page.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Memev.Reader;

public static class Ops
{
  public const string Prompt = "Tell your lead agent what you want done, or ask a question";
  public const string Blank = "Nothing has been said yet. Tell your lead agent what you want done.";

  public sealed class Seat
  {
    public string Name = "";
    public string State = "";
    public string App = "";
    public bool Lead;
  }

  public sealed class Msg
  {
    public string From = "";
    public string To = "";
    public string Body = "";
  }

  public sealed class View
  {
    public string Lead = "";
    public string Sentence = "";
    public string Note = "";
    public List<Seat> Seats = new();
    public List<Msg> Messages = new();
  }

  public static bool SafeName(string? s)
  {
    if (string.IsNullOrEmpty(s) || s.Length >= 64) return false;
    foreach (char c in s)
    {
      bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.' || c == '@' || c == '+';
      if (!ok) return false;
    }
    return true;
  }

  static bool Banned(string s) => s.IndexOfAny("\"%!&|<>^()\r\n".ToCharArray()) >= 0;

  static bool HasPath(string s) =>
    s.Contains("://") || s.Contains("/home/") || s.Contains("/Users/") || s.Contains("/tmp/") || s.Contains("/var/") || s.Contains('\\');

  static string Flat(string s)
  {
    var b = new StringBuilder(s.Length);
    foreach (char c in s) b.Append(c < 32 || c == 127 ? ' ' : c);
    return b.ToString();
  }

  static string Words(string state) => state switch
  {
    "live" => "here now",
    "stale" => "not seen recently",
    "never joined" => "has not opened a session yet",
    "removed" => "removed",
    "" => "not seen",
    _ => state,
  };

  static string Kind(string kind) => kind switch
  {
    "host" => "this computer",
    "local-client" => "an app on this computer",
    "remote-computer" => "another computer",
    "cloud-connector" => "a web app",
    "" => "a computer",
    _ => kind,
  };

  static string Contract(string state) => state switch
  {
    "done" => "checked: its verify passed",
    "open" => "signed, not checked yet",
    "superseded" => "replaced by a newer version",
    "draft" => "a draft, not signed",
    "" => "unknown",
    _ => state,
  };

  static bool Field(JsonElement obj, string name, out string value)
  {
    value = "";
    if (!obj.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.String) return false;
    value = el.GetString() ?? "";
    return value.Length > 0;
  }

  public static bool ReadRoster(string json, View v)
  {
    v.Lead = "";
    v.Seats.Clear();
    try
    {
      using JsonDocument doc = JsonDocument.Parse(json);
      JsonElement root = doc.RootElement;
      if (Field(root, "operator", out string lead) && SafeName(lead)) v.Lead = lead;
      if (!root.TryGetProperty("seats", out JsonElement seats) || seats.ValueKind != JsonValueKind.Array) return false;
      foreach (JsonElement s in seats.EnumerateArray())
      {
        if (v.Seats.Count >= 16) break;
        if (!Field(s, "seat", out string name) || !SafeName(name)) continue;
        Field(s, "state", out string state);
        Field(s, "app", out string app);
        bool leadSeat = s.TryGetProperty("operator", out JsonElement flag) && flag.ValueKind == JsonValueKind.True;
        v.Seats.Add(new Seat { Name = name, State = Flat(state), App = SafeName(app) ? app : "", Lead = leadSeat });
      }
      return true;
    }
    catch (JsonException) { return false; }
  }

  public static bool ReadMessages(string json, View v)
  {
    v.Messages.Clear();
    try
    {
      using JsonDocument doc = JsonDocument.Parse(json);
      if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
      foreach (JsonElement m in doc.RootElement.EnumerateArray())
      {
        if (v.Messages.Count >= 40) break;
        if (!Field(m, "from", out string from) || !SafeName(from)) continue;
        if (!Field(m, "to", out string to) || !SafeName(to)) continue;
        if (!Field(m, "body", out string body) || HasPath(body) || Banned(body)) continue;
        v.Messages.Add(new Msg { From = from, To = to, Body = Flat(body) });
      }
      return true;
    }
    catch (JsonException) { return false; }
  }

  public static void Sentence(View v)
  {
    v.Sentence = v.Lead.Length > 0
      ? v.Lead + " leads. Tell it what you want done."
      : "No lead is set. Pick one, then tell it what you want done.";
  }

  public static View Load(Func<string[], Core.Result> run)
  {
    var v = new View();
    Core.Result roster = run(new[] { "bus", "roster", "--json" });
    if (!roster.Done || roster.ExitCode != 0 || !ReadRoster(roster.Output, v))
    {
      v.Note = "The roster could not be read.";
      v.Sentence = v.Note;
      return v;
    }
    Core.Result since = run(new[] { "bus", "since", "0", "--json" });
    if (!since.Done || since.ExitCode != 0 || !ReadMessages(since.Output, v))
      v.Note = "The message list could not be read.";
    Sentence(v);
    return v;
  }

  public static string[]? SendArgs(string? lead, string? text, out string why)
  {
    if (!SafeName(lead)) { why = "No lead is set. Pick one, then tell it what you want done."; return null; }
    if (string.IsNullOrWhiteSpace(text)) { why = "Write what you want done."; return null; }
    if (text.Length >= 400 || Banned(text) || HasPath(text)) { why = "That message was not sent."; return null; }
    why = "";
    return new[] { "bus", "send", lead!, "task", text };
  }

  public static string[]? LeadArgs(View v, string? seat, out string why)
  {
    if (!SafeName(seat) || v.Seats.Find((s) => s.Name == seat) == null)
    {
      why = SafeName(seat) ? "That seat is not on the roster." : "That seat cannot be used.";
      return null;
    }
    why = "";
    return new[] { "bus", "operator", seat! };
  }

  static void Line(StringBuilder b, string line)
  {
    if (line.Length == 0 || HasPath(line)) return;
    if (b.Length > 0) b.Append('\n');
    b.Append(line);
  }

  static bool Ran(Core.Result r) => r.Done && r.ExitCode == 0;

  static JsonElement Root(string json, out JsonDocument? doc)
  {
    try { doc = JsonDocument.Parse(json); return doc.RootElement; }
    catch (JsonException) { doc = null; return default; }
  }

  public static string Page(string which, string? query, Func<string[], Core.Result> run)
  {
    var b = new StringBuilder();
    if (which == "projects") Projects(b, run);
    else if (which == "bots") Bots(b, run);
    else if (which == "memory") Memory(b, query ?? "", run);
    else if (which == "checked") Checked(b, run);
    else if (which == "settings") Settings(b, run);
    else Line(b, "That page is not available.");
    return b.ToString();
  }

  static void Projects(StringBuilder b, Func<string[], Core.Result> run)
  {
    Line(b, "# CURRENT PROJECT");
    Core.Result r = run(new[] { "card", "--json" });
    JsonElement root = Root(r.Output, out JsonDocument? doc);
    using (doc)
    {
      if (!Ran(r) || doc == null) { Line(b, "The project did not answer."); return; }
      Field(root, "project", out string project);
      Field(root, "north_star", out string star);
      if (project.Length == 0 || HasPath(project)) { Line(b, "No project is set for this notes folder yet."); return; }
      Line(b, star.Length > 0 && !HasPath(star) ? project + " — " + star : project + " — No north star yet.");
    }
  }

  static void Bots(StringBuilder b, Func<string[], Core.Result> run)
  {
    Line(b, "# TEAM");
    View v = Load(run);
    if (v.Lead.Length == 0 && v.Seats.Count == 0 && v.Note.Length > 0) Line(b, v.Note);
    else
    {
      Line(b, v.Sentence);
      if (v.Note.Length > 0) Line(b, v.Note);
      if (v.Seats.Count == 0) Line(b, "No seats yet.");
      foreach (Seat s in v.Seats)
      {
        string words = Words(s.State);
        if (s.Lead || s.Name == v.Lead) Line(b, s.Name + " · the lead · " + words);
        else if (s.App.Length > 0 && s.App != s.Name) Line(b, s.Name + " · " + s.App + " · " + words);
        else Line(b, s.Name + " · " + words);
      }
    }
    Line(b, "# COMPUTERS AND APPS");
    Core.Result dev = run(new[] { "devices", "list", "--json" });
    JsonElement root = Root(dev.Output, out JsonDocument? doc);
    using (doc)
    {
      if (!Ran(dev) || doc == null || !root.TryGetProperty("devices", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        Line(b, "The device list did not answer.");
      else
      {
        int n = 0;
        foreach (JsonElement d in list.EnumerateArray())
        {
          Field(d, "label", out string label);
          Field(d, "kind", out string kind);
          Field(d, "status", out string status);
          string kindWords = Kind(kind);
          if (HasPath(label) || label == kindWords) label = "";
          string statusWord = status.Length > 0 ? status : "unknown";
          Line(b, label.Length > 0 ? label + " · " + kindWords + " · " + statusWord : kindWords + " · " + statusWord);
          n++;
        }
        if (n == 0) Line(b, "No computers or apps yet.");
      }
    }
    Line(b, "# APPS ON THIS COMPUTER");
    Core.Result clients = run(new[] { "clients" });
    if (!Ran(clients)) Line(b, "The app list did not answer.");
    else
    {
      int n = 0;
      foreach (string raw in clients.Output.Split('\n'))
      {
        string line = raw.Trim();
        if (line.Length == 0 || HasPath(line)) continue;
        Line(b, line);
        n++;
      }
      if (n == 0) Line(b, "The app list did not answer.");
    }
  }

  static bool WordOk(string w)
  {
    if (w.Length == 0 || w.Length > 40 || Banned(w) || HasPath(w)) return false;
    foreach (char c in w) if (c <= 32) return false;
    return true;
  }

  static void Memory(StringBuilder b, string query, Func<string[], Core.Result> run)
  {
    Line(b, "# SEARCH");
    string[] bits = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    if (bits.Length == 0) Line(b, "Type what you want to find.");
    else if (bits.Length > 8 || Array.Exists(bits, (w) => !WordOk(w))) Line(b, "That search was not sent.");
    else
    {
      var args = new List<string> { "recall" };
      args.AddRange(bits);
      args.Add("--json");
      Core.Result r = run(args.ToArray());
      JsonElement root = Root(r.Output, out JsonDocument? doc);
      using (doc)
      {
        if (!Ran(r) || doc == null || root.ValueKind != JsonValueKind.Array) Line(b, "The search did not answer.");
        else
        {
          int n = 0;
          foreach (JsonElement h in root.EnumerateArray())
          {
            Field(h, "heading", out string heading);
            Field(h, "snippet", out string snippet);
            Field(h, "project", out string project);
            if (heading.Length == 0 && snippet.Length == 0) continue;
            string title = heading.Length > 0 ? heading : "A note";
            Line(b, project.Length > 0 && !HasPath(project) ? title + " — " + snippet + " · " + project : title + " — " + snippet);
            n++;
          }
          if (n == 0) Line(b, "Nothing matched.");
        }
      }
    }
    Line(b, "# REVIEW");
    Core.Result review = run(new[] { "review", "list", "--json" });
    JsonElement inboxRoot = Root(review.Output, out JsonDocument? inboxDoc);
    using (inboxDoc)
    {
      if (!Ran(review) || inboxDoc == null || !inboxRoot.TryGetProperty("inbox", out JsonElement inbox) || inbox.ValueKind != JsonValueKind.Array)
        Line(b, "The review list did not answer.");
      else
      {
        int n = 0;
        foreach (JsonElement item in inbox.EnumerateArray())
        {
          if (!Field(item, "snippet", out string text)) Field(item, "text", out text);
          if (!Field(item, "why", out string why)) Field(item, "reason", out why);
          if (text.Length == 0) continue;
          Line(b, why.Length > 0 ? text + " — " + why : text);
          n++;
        }
        if (n == 0) Line(b, "Nothing is waiting for review.");
      }
    }
    Line(b, "# CLEAN-UP");
    Core.Result audit = run(new[] { "audit", "--json" });
    JsonElement auditRoot = Root(audit.Output, out JsonDocument? auditDoc);
    using (auditDoc)
    {
      if (!Ran(audit) || auditDoc == null) Line(b, "The clean-up report did not answer.");
      else
      {
        string memories = Num(auditRoot, "memories");
        string junk = Num(auditRoot, "likely_junk");
        Line(b, memories + " memories, " + junk + " likely junk");
        if (Field(auditRoot, "next", out string next) && !HasPath(next)) Line(b, next);
      }
    }
  }

  static string Num(JsonElement el, string name)
  {
    if (!el.TryGetProperty(name, out JsonElement v)) return "0";
    if (v.ValueKind == JsonValueKind.Null) return "";
    if (v.ValueKind == JsonValueKind.Number) return v.ToString();
    if (v.ValueKind == JsonValueKind.String) return v.GetString() ?? "0";
    return "0";
  }

  static void Checked(StringBuilder b, Func<string[], Core.Result> run)
  {
    Line(b, "# CONTRACTS");
    Core.Result r = run(new[] { "contract", "list" });
    JsonElement root = Root(r.Output, out JsonDocument? doc);
    using (doc)
    {
      if (!Ran(r) || doc == null || !root.TryGetProperty("contracts", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
      {
        Line(b, "The contract list did not answer.");
        return;
      }
      int n = 0;
      foreach (JsonElement c in list.EnumerateArray())
      {
        if (!Field(c, "title", out string title) || HasPath(title)) continue;
        Field(c, "state", out string state);
        Field(c, "version", out string version);
        Line(b, version.Length > 0 && version != "1" ? title + " (version " + version + ") — " + Contract(state) : title + " — " + Contract(state));
        n++;
      }
      if (n == 0) Line(b, "No contracts yet.");
    }
  }

  static void Settings(StringBuilder b, Func<string[], Core.Result> run)
  {
    Line(b, "# SETTINGS");
    Core.Result plan = run(new[] { "plan", "--json" });
    JsonElement planRoot = Root(plan.Output, out JsonDocument? planDoc);
    using (planDoc)
    {
      if (!Ran(plan) || planDoc == null) Line(b, "The plan did not answer.");
      else if (Field(planRoot, "plan", out string name) && !HasPath(name))
      {
        string used = Num(planRoot, "computers_used");
        string allowed = Num(planRoot, "computers_allowed");
        string conn = Num(planRoot, "connectors_counted");
        if (allowed.Length > 0 && allowed != "null")
          Line(b, "Plan: " + name + ". Computers: " + used + " of " + allowed + ". Connectors: " + conn + ".");
        else
          Line(b, "Plan: " + name + ". Computers: " + used + ". Connectors: " + conn + ".");
      }
    }
    Core.Result lic = run(new[] { "license", "status", "--json" });
    JsonElement licRoot = Root(lic.Output, out JsonDocument? licDoc);
    using (licDoc)
    {
      if (Ran(lic) && licDoc != null && Field(licRoot, "tier_label", out string tier) && !HasPath(tier))
      {
        if (!Field(licRoot, "state", out string state) && licRoot.TryGetProperty("license", out JsonElement nested))
          Field(nested, "state", out state);
        Field(licRoot, "reason", out string reason);
        if (reason.Length > 0 && !HasPath(reason)) Line(b, "Licence: " + tier + " · " + (state.Length > 0 ? state : "none") + " · " + reason);
        else Line(b, "Licence: " + tier + " · " + (state.Length > 0 ? state : "none"));
      }
    }
    Core.Result report = run(new[] { "report", "status", "--json" });
    JsonElement reportRoot = Root(report.Output, out JsonDocument? reportDoc);
    using (reportDoc)
    {
      if (Ran(report) && reportDoc != null)
      {
        bool on = reportRoot.TryGetProperty("enabled", out JsonElement en) && en.ValueKind == JsonValueKind.True;
        Line(b, "Anonymous report: " + (on ? "on" : "off"));
      }
    }
    Core.Result backup = run(new[] { "backup", "status" });
    if (Ran(backup)) Line(b, backup.Output.StartsWith("Backing up to") ? "Backup: on" : "Backup: not set up");
    Line(b, "Theme: follows this computer");
  }
}
