// SPDX-License-Identifier: Apache-2.0
// The guide's connect, lead and fix cases, against a fixture core: a .cmd file on Windows and a shell script elsewhere, that
// writes each call to calls.log and answers from the JSON files beside it. `--case <name>` runs one case and prints
// `ok <name>` only after its own checks passed; test/guide-cases.test.mjs runs each case that way.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Memev.Reader;

static class GuideCases
{
  public static readonly string[] Names = { "connect-list", "connect-onestep", "connect-selected", "connect-result", "order", "lead-registered", "lead-retry", "fix-text", "fix-retry" };

  const string Discover = "[\n" +
    "{\"id\":\"claude-code\",\"name\":\"Claude Code\",\"found\":true,\"evidence\":\"path\",\"install_path\":\"\",\"automation\":\"auto\",\"step\":\"\"},\n" +
    "{\"id\":\"codex\",\"name\":\"Codex CLI\",\"found\":true,\"evidence\":\"folder\",\"install_path\":\"\",\"automation\":\"auto\",\"step\":\"\"},\n" +
    "{\"id\":\"cursor\",\"name\":\"Cursor\",\"found\":true,\"evidence\":\"registry\",\"install_path\":\"\",\"automation\":\"auto\",\"step\":\"\"},\n" +
    "{\"id\":\"grok-bot\",\"name\":\"Grok Bot\",\"found\":true,\"evidence\":\"registry\",\"install_path\":\"\",\"automation\":\"one-step\",\"step\":\"Add memev in Grok Bot, Settings, Connectors\"},\n" +
    "{\"id\":\"copilot-desktop\",\"name\":\"Microsoft Copilot\",\"found\":true,\"evidence\":\"registry\",\"install_path\":\"\",\"automation\":\"none\",\"step\":\"\"},\n" +
    "{\"id\":\"windsurf\",\"name\":\"Windsurf\",\"found\":false,\"evidence\":\"\",\"install_path\":\"\",\"automation\":\"auto\",\"step\":\"\"}\n]\n";

  const string Connected = "{\"apps\":[" +
    "{\"id\":\"claude-code\",\"status\":\"connected\",\"files\":[],\"backups\":[],\"seat\":\"claude-code@pc\",\"restart\":false}," +
    "{\"id\":\"cursor\",\"status\":\"failed\",\"files\":[],\"backups\":[],\"seat\":\"\",\"restart\":false,\"detail\":\"its settings file is read-only\"}" +
    "],\"operator\":\"\"}\n";

  const string Doctor = "[" +
    "{\"id\":\"node\",\"title\":\"Node.js 22 or later\",\"status\":\"ok\",\"detail\":\"22.11.0\",\"fix\":null}," +
    "{\"id\":\"state-perms\",\"title\":\"Memev's state folder is private\",\"status\":\"warn\",\"detail\":\"2 files are readable by others\",\"fix\":{\"desc\":\"restrict them to you (chmod 600/700)\",\"destructive\":false}}," +
    "{\"id\":\"client-cursor\",\"title\":\"Cursor's settings can be written\",\"status\":\"fail\",\"detail\":\"read-only\",\"fix\":{\"desc\":\"make the settings file writable again\",\"destructive\":true},\"related\":[\"cursor\"]}" +
    "]\n";

  const string RosterOne = "{\"operator\":null,\"live_minutes\":10,\"seats\":[{\"seat\":\"claude-code@pc\",\"client\":\"claude-code\",\"state\":\"registered\",\"last_seen\":null,\"claims\":[]},{\"seat\":\"old@pc\",\"client\":\"old\",\"state\":\"removed\",\"claims\":[]}]}\n";
  const string RosterNone = "{\"operator\":null,\"live_minutes\":10,\"seats\":[]}\n";

  sealed class Fixture
  {
    public readonly string Dir;
    public readonly string Bin;
    public Fixture(string tmp, string name, bool running)
    {
      Dir = Path.Combine(tmp, "core-" + name);
      Directory.CreateDirectory(Dir);
      File.WriteAllText(Path.Combine(Dir, "discover.json"), Discover);
      File.WriteAllText(Path.Combine(Dir, "connect.json"), Connected);
      File.WriteAllText(Path.Combine(Dir, "doctor.json"), Doctor);
      File.WriteAllText(Path.Combine(Dir, "roster.json"), RosterOne);
      if (running) File.WriteAllText(Path.Combine(Dir, "started"), "");
      if (OperatingSystem.IsWindows())
      {
        Bin = Path.Combine(Dir, "memev.cmd");
        File.WriteAllText(Bin, string.Join("\r\n", new[]
        {
          "@echo off", "echo %*>>\"%~dp0calls.log\"",
          "if \"%~1\"==\"status\" goto status", "if \"%~1\"==\"start\" goto start", "if \"%~1\"==\"discover\" goto show",
          "if \"%~1\"==\"connect\" goto show", "if \"%~1\"==\"doctor\" goto doctor", "if \"%~1\"==\"bus\" goto bus", "exit /b 2",
          ":status", "if not exist \"%~dp0started\" exit /b 1", "echo {\"running\":true}", "exit /b 0",
          ":start", "type nul>\"%~dp0started\"", "exit /b 0",
          ":show", "type \"%~dp0%~1.json\"", "exit /b 0",
          ":doctor", "if \"%~2\"==\"--fix\" exit /b 0", "type \"%~dp0doctor.json\"", "exit /b 0",
          ":bus", "type \"%~dp0roster.json\"", "exit /b 0", "",
        }));
      }
      else
      {
        Bin = Path.Combine(Dir, "memev");
        File.WriteAllText(Bin, "#!/bin/sh\nd=$(CDPATH= cd -- \"$(dirname \"$0\")\" && pwd)\necho \"$*\" >> \"$d/calls.log\"\n" +
          "case \"$1\" in\n" +
          "  status) [ -f \"$d/started\" ] || exit 1; echo '{\"running\":true}' ;;\n" +
          "  start) : > \"$d/started\" ;;\n" +
          "  discover|connect) cat \"$d/$1.json\" ;;\n" +
          "  doctor) [ \"$2\" = \"--fix\" ] && exit 0; cat \"$d/doctor.json\" ;;\n" +
          "  bus) cat \"$d/roster.json\" ;;\n" +
          "  *) exit 2 ;;\nesac\n");
        File.SetUnixFileMode(Bin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
      }
    }

    public (int Code, string Output) Run(string[] args)
    {
      var r = Core.Capture(Bin, args, 20000);
      return r.Done ? (r.ExitCode, r.Output) : (-1, "");
    }

    /// Each call the fixture got, with any quotes the Windows runner added taken out.
    public List<string> Calls()
    {
      string f = Path.Combine(Dir, "calls.log");
      return File.Exists(f) ? File.ReadAllLines(f).Select((l) => l.Replace("\"", "").Trim()).Where((l) => l.Length > 0).ToList() : new List<string>();
    }

    public List<ConnectRow> Rows() => GuideModel.Rows(GuideModel.ParseApps(Run(new[] { "discover", "--json" }).Output));
  }

  /// Runs one case. false when the name is not a case.
  public static bool Run(string name, Action<bool, string> expect, string tmp)
  {
    switch (name)
    {
      case "connect-list":
      {
        var f = new Fixture(tmp, name, true);
        var rows = f.Rows();
        expect(rows.Count == 5, $"{name}: the five apps found are listed, the one not found is not ({rows.Count})");
        expect(rows.Count((r) => r.Ticked) == 3 && rows.Where((r) => r.Ticked).All((r) => r.App.Automation == "auto"), $"{name}: only the three auto apps are ticked");
        var none = rows.FirstOrDefault((r) => r.App.Id == "copilot-desktop");
        expect(none != null && none.HasTick && !none.Ticked && !none.Enabled && none.State.Contains("cannot connect"), $"{name}: an app Memev cannot connect is shown, unticked and disabled");
        expect(f.Calls().FirstOrDefault() == "discover --json", $"{name}: the list comes from discover --json");
        // a core with no discover: the same list from onboarding --json
        var old = GuideModel.ParseApps("[{\"app\":\"claude-code\",\"status\":\"needs-manual\",\"detail\":\"memev connect claude-code\",\"step\":\"Claude Code\"},{\"app\":\"aider\",\"status\":\"unsupported\",\"detail\":\"not supported yet\"}]");
        expect(old.Count == 2 && old[0].Automation == "auto" && old[1].Automation == "none", $"{name}: onboarding --json still gives the list");
        var declined = GuideModel.Rows(GuideModel.ParseApps("[{\"app\":\"zed\",\"status\":\"skipped\",\"detail\":\"you disconnected it\"}]"));
        expect(declined.Count == 1 && declined[0].HasTick && !declined[0].Ticked && declined[0].Enabled, $"{name}: an app the person disconnected is listed unticked, and can be ticked");
        return true;
      }
      case "connect-onestep":
      {
        var rows = new Fixture(tmp, name, true).Rows();
        var one = rows.FirstOrDefault((r) => r.App.Id == "grok-bot");
        expect(one != null && !one.HasTick && !one.Ticked, $"{name}: a one-step app has no tick");
        expect(one != null && one.Copy == "Add memev in Grok Bot, Settings, Connectors" && one.State.Contains(one.Copy), $"{name}: it shows its step, and the Copy button copies it");
        return true;
      }
      case "connect-selected":
      {
        var f = new Fixture(tmp, name, true);
        var rows = f.Rows();
        rows.First((r) => r.App.Id == "codex").Ticked = false;
        // ticking a row that cannot be changed does nothing
        rows.First((r) => r.App.Id == "copilot-desktop").Ticked = true;
        string[]? args = GuideModel.ConnectArgs(rows);
        expect(args != null && args.SequenceEqual(new[] { "connect", "claude-code", "cursor", "--json" }), $"{name}: connect names exactly the two ticked auto apps ({(args == null ? "none" : string.Join(' ', args))})");
        if (args != null) f.Run(args);
        expect(f.Calls().LastOrDefault() == "connect claude-code cursor --json", $"{name}: the core got that command");
        foreach (var r in rows) r.Ticked = false;
        expect(GuideModel.ConnectArgs(rows) == null, $"{name}: nothing ticked runs no connect");
        return true;
      }
      case "connect-result":
      {
        var f = new Fixture(tmp, name, true);
        var (apps, _) = GuideModel.ParseConnect(f.Run(new[] { "connect", "claude-code", "cursor", "--json" }).Output);
        expect(apps.Count == 2, $"{name}: one outcome per app");
        expect(apps.Any((a) => a.Id == "claude-code" && a.Ok && a.Seat == "claude-code@pc"), $"{name}: a connected app shows its seat");
        expect(apps.Any((a) => a.Id == "cursor" && !a.Ok && a.Detail.Contains("read-only")), $"{name}: a failed app shows why");
        return true;
      }
      case "order":
      {
        var flow = new GuideFlow();
        flow.ConnectStarted();
        expect(!flow.Next() && flow.Step == GuideFlow.Connect, $"{name}: the import step does not show while connect runs");
        flow.ConnectEnded();
        expect(flow.Step == GuideFlow.Import, $"{name}: the import step shows when connect has ended");
        expect(flow.Next() && flow.Step == GuideFlow.Lead, $"{name}: then the lead step");
        var skip = new GuideFlow();
        expect(skip.Next() && skip.Step == GuideFlow.Import, $"{name}: with nothing ticked, Continue goes straight to import");
        return true;
      }
      case "lead-registered":
      {
        var f = new Fixture(tmp, name, false);
        var lc = GuideModel.LoadLead(f.Run);
        var calls = f.Calls();
        expect(lc.Started && calls.Contains("start") && calls.IndexOf("start") < calls.IndexOf("bus roster --json"), $"{name}: a stopped Memev is started before the roster is read");
        expect(lc.Read && lc.Seats.Count == 1 && lc.Seats[0].Seat == "claude-code@pc" && lc.Seats[0].State == "registered", $"{name}: a seat registered at connect is a choice; a removed one is not");
        expect(!lc.Retry, $"{name}: no Retry when there is a seat");
        var again = GuideModel.LoadLead(f.Run);
        expect(!again.Started, $"{name}: a running Memev is not started again");
        return true;
      }
      case "lead-retry":
      {
        var f = new Fixture(tmp, name, true);
        File.WriteAllText(Path.Combine(f.Dir, "roster.json"), RosterNone);
        var lc = GuideModel.LoadLead(f.Run);
        expect(lc.Read && lc.Retry, $"{name}: no seats offers Retry");
        File.WriteAllText(Path.Combine(f.Dir, "roster.json"), RosterOne);
        var retry = GuideModel.LoadLead(f.Run);
        expect(!retry.Retry && retry.Seats.Count == 1, $"{name}: Retry reads the roster again and shows the new seat");
        expect(f.Calls().Count((c) => c == "bus roster --json") == 2, $"{name}: each read is one roster call");
        return true;
      }
      case "fix-text":
      {
        var f = new Fixture(tmp, name, true);
        var items = GuideModel.ParseDoctor(f.Run(new[] { "doctor", "--json" }).Output);
        expect(items.Count == 2, $"{name}: every check that is not ok, and no ok one ({items.Count})");
        var lines = GuideModel.FixLines(items);
        expect(lines.Any((l) => l.Contains("restrict them to you")), $"{name}: a check's fix text is shown");
        expect(lines.Any((l) => l.Contains("make the settings file writable again") && l.EndsWith("(by hand)")), $"{name}: a fix that asks first says it is by hand");
        expect(GuideModel.OfferSafeFix(items), $"{name}: Fix the safe ones is offered");
        expect(items.Any((i) => i.Related.Contains("cursor")), $"{name}: a check names the app it concerns");
        expect(!GuideModel.OfferSafeFix(items.Where((i) => !i.Safe)), $"{name}: with no safe fix, the button is not offered");
        return true;
      }
      case "fix-retry":
      {
        var f = new Fixture(tmp, name, true);
        var (failed, _) = GuideModel.ParseConnect(f.Run(new[] { "connect", "claude-code", "cursor", "--json" }).Output);
        var ids = failed.Where((a) => !a.Ok).Select((a) => a.Id).ToList();
        int before = f.Calls().Count;
        var (ran, retry) = GuideModel.FixAndRetry(f.Run, ids);
        var calls = f.Calls().Skip(before).ToList();
        expect(calls.SequenceEqual(new[] { "doctor --fix --safe-only", "connect cursor --json" }), $"{name}: one safe fix, then one connect of the failed app ({string.Join(" ; ", calls)})");
        expect(ran.Count == 2 && retry.Count >= 1, $"{name}: the retry's outcome is read");
        return true;
      }
      default:
        return false;
    }
  }
}
