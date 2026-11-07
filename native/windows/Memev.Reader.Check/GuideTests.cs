// SPDX-License-Identifier: Apache-2.0
// The first launch's tests: the cases of native/linux/test/test-guide.c, against the C# guide, plus the Windows cases (drive
// roots, and text that cmd.exe would read as a command).
using System;
using System.IO;
using System.Linq;
using Memev.Reader;

static class GuideTests
{
  static readonly string[] Known = { "init", "backup", "remember", "recall", "join" };
  static bool OnlyKnown(System.Collections.Generic.List<string[]> cmds) => cmds.All((c) => Known.Contains(c[0]));

  static GuidePlan ThisComputer(string dir, string? url = null)
  {
    var p = new GuidePlan { FirstComputer = 1, NotesDir = dir };
    if (url != null) { p.BackupUrl = url; p.ConfirmBackup = true; }
    return p;
  }

  public static void Run(Action<bool, string> expect, string tmp)
  {
    var apps = Guide.ParseClients("    Example One     not found\n  · Example Two     found, not connected\n  · Longer Example Name found, not supported yet\n  ✓ Example Three   connected (hooks + MCP)\n");
    expect(apps.Count == 4, "four rows");
    expect(apps[0].Name == "Example One" && !apps[0].Selectable, "not found");
    expect(apps[1].Name == "Example Two" && apps[1].Selectable, "selectable");
    expect(apps[2].Name == "Longer Example Name" && !apps[2].Selectable, "unsupported");
    expect(apps[3].Name == "Example Three" && !apps[3].Selectable, "already connected");

    string notes = Path.Combine(tmp, "notes");
    var plan = ThisComputer(notes);
    plan.BackupUrl = "file:///tmp/memev-backup-proof.git";
    plan.Selected.Add("Example Two");
    var cmds = Guide.Build(plan, out string why);
    expect(cmds != null && cmds.Count == 3, "init, remember, recall");
    expect(cmds != null && cmds[0][0] == "init" && cmds[0].Contains("--local-only") && cmds[0].Contains("--no-connect"), "init stays local");
    expect(cmds != null && cmds[0].Contains("--no-service"), "init installs no service");
    expect(cmds != null && OnlyKnown(cmds), "every command is one the contract lists");
    expect(cmds != null && !cmds.Any((c) => c[0] == "backup"), "an address alone does not record a remote");
    expect(why.Contains("does not keep its own list"), "the skip is explained");
    plan.ConfirmBackup = true;
    cmds = Guide.Build(plan, out _);
    expect(cmds != null && cmds.Count == 4 && cmds[1].SequenceEqual(new[] { "backup", "set", plan.BackupUrl }), "the tick records the remote, as its own argument");

    plan.FirstComputer = 0;
    plan.JoinCode = "abc";
    expect(Guide.Build(plan, out _) == null, "join waits for the confirm");
    plan.ConfirmJoin = true;
    cmds = Guide.Build(plan, out _);
    expect(cmds != null && cmds.Count == 1 && cmds[0].SequenceEqual(new[] { "join", "abc" }), "join is the only command");
    foreach (string bad in new[] { "-x", "a b", "a\tb", "a\nb", "" })
    {
      plan.JoinCode = bad;
      expect(Guide.Build(plan, out _) == null, $"join code refused: '{bad}'");
    }

    string root = Path.GetPathRoot(Path.GetFullPath(tmp)) ?? "/";
    foreach (string bad in new[] { "notes", "./notes", root, root + ".", Path.Combine(root, "tmp", ".."), "" })
      expect(Guide.Build(ThisComputer(bad), out _) == null, $"folder refused: '{bad}'");
    string messy = Path.Combine(tmp, ".", "x", "..", "notes") + Path.DirectorySeparatorChar;
    cmds = Guide.Build(ThisComputer(messy), out _);
    expect(cmds != null && cmds[0][2] == notes, "the folder is passed resolved");
    // a failing core's output is shown without a token's value, in any case, wherever in the output it stands
    {
      string dir = Path.Combine(tmp, "leaky");
      Directory.CreateDirectory(dir);
      string secret = "SECRETVALUE" + new string('x', 300);
      string bin;
      if (OperatingSystem.IsWindows())
      {
        bin = Path.Combine(dir, "memev.cmd");
        File.WriteAllText(bin, $"@echo off\r\necho Token={secret}\r\nexit /b 1\r\n");
      }
      else
      {
        bin = Path.Combine(dir, "memev");
        File.WriteAllText(bin, $"#!/bin/sh\necho 'Token={secret}'\nexit 1\n");
        File.SetUnixFileMode(bin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
      }
      bool leaked = !Guide.Apply(ThisComputer(Path.Combine(dir, "notes")), bin, out string shown) && shown.Contains("SECRETVALUE", StringComparison.Ordinal);
      expect(!leaked && !shown.Contains("xxxxxxxxxx"), $"a token's value is never shown ({shown.Length} chars)");
    }

    if (OperatingSystem.IsWindows())
    {
      foreach (string bad in new[] { @"\\?\C:\x\..", @"\\?\C:\notes", @"\\.\C:\notes", @"//?/C:/x/..", @"\\?\UNC\server\share\x" })
        expect(Guide.Build(ThisComputer(bad), out _) == null, $"device or long-path folder refused: '{bad}'");
      foreach (string bad in new[] { @"C:\", @"C:", @"C:\..", @"\\server\share", @"\notes", @"C:notes" })
        expect(Guide.Build(ThisComputer(bad), out _) == null, $"windows folder refused: '{bad}'");
      expect(Guide.Build(ThisComputer(@"C:\a"), out _) != null, "a short full path on a drive is taken");
    }

    foreach (string good in new[] { "https://example.org/me/notes.git", "ssh://git@example.org/me/notes.git", "git://example.org/notes.git", "git@example.org:me/notes.git", "file:///tmp/notes.git" })
      expect(Guide.Build(ThisComputer(notes, good), out _)?.Count == 4, good);
    foreach (string bad in new[] { "http://example.org/notes.git", "ftp://example.org/notes.git", "https://", "file:///", "git@:x", "git@host:",
      "ssh://-oProxyCommand=x/y", "https://-x/y", "git@-oProxyCommand=x:y", "-uhttps://example.org/x", "https://exa mple.org/x", "https://example.org/x\n" })
      expect(Guide.Build(ThisComputer(notes, bad), out _) == null, $"remote refused: {bad}");

    // Apply against a fake core: a recall without the note fails the check, one with it passes
    for (int round = 0; round < 2; round++)
    {
      string dir = Path.Combine(tmp, $"guide{round}");
      Directory.CreateDirectory(dir);
      string bin;
      if (OperatingSystem.IsWindows())
      {
        bin = Path.Combine(dir, "memev.cmd");
        File.WriteAllText(bin, round == 0
          ? "@echo off\r\nif \"%~1\"==\"recall\" echo {\"hits\":[]}\r\nexit /b 0\r\n"
          : "@echo off\r\nif \"%~1\"==\"recall\" echo %~2\r\nexit /b 0\r\n");
      }
      else
      {
        bin = Path.Combine(dir, "memev");
        File.WriteAllText(bin, round == 0 ? "#!/bin/sh\n[ \"$1\" = recall ] && echo '{\"hits\":[]}'\nexit 0\n" : "#!/bin/sh\n[ \"$1\" = recall ] && printf '%s\\n' \"$2\"\nexit 0\n");
        File.SetUnixFileMode(bin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
      }
      bool ok = Guide.Apply(ThisComputer(Path.Combine(dir, "notes")), bin, out string detail);
      if (round == 0) expect(!ok && detail.Contains("Recall did not return"), "a recall without the note fails the check");
      else expect(ok, $"a recall that returns the note passes the check ({detail})");
    }

    if (OperatingSystem.IsWindows())
    {
      // cmd.exe reads a batch file's line again: & and spaces must reach the core as text, and % or a quote must not reach it at all
      string dir = Path.Combine(tmp, "cmdq");
      Directory.CreateDirectory(dir);
      string bin = Path.Combine(dir, "memev.cmd");
      File.WriteAllText(bin, "@echo off\r\necho [%~1] [%~2]\r\n");
      var r = Core.Capture(bin, new[] { @"C:\My Notes\x y", "two" }, 5000);
      expect(r.Done && r.ExitCode == 0 && r.Output.Contains(@"[C:\My Notes\x y] [two]"), $"an argument with spaces stays one argument ({r.Output.Trim()})");
      foreach (string bad in new[] { "%PATH%", "say \"hi\"", "a!b!", "a & echo INJECTED", "a | x", "a > f", "a < f", "a^b", "C:\\Program Files (x86)" })
        expect(Core.Capture(bin, new[] { bad }, 5000).ExitCode == 126, $"refused before cmd.exe: {bad}");
      r = Core.Capture(bin, new[] { @"C:\x\", "next" }, 5000);
      expect(r.Done && r.ExitCode == 0 && r.Output.Contains(@"[C:\x\] [next]"), $"a trailing backslash does not swallow the closing quote ({r.Output.Trim()})");
      // a core installed under a folder whose name holds ( ) & still runs: only typed arguments are refused
      string odd = Path.Combine(tmp, "Program Files (x86) & co");
      Directory.CreateDirectory(odd);
      string oddBin = Path.Combine(odd, "memev.cmd");
      File.WriteAllText(oddBin, "@echo off\r\necho [%~1]\r\n");
      r = Core.Capture(oddBin, new[] { "status" }, 5000);
      expect(r.Done && r.ExitCode == 0 && r.Output.Contains("[status]"), $"a core under 'Program Files (x86) & co' runs ({r.ExitCode} {r.Output.Trim()})");
    }
  }
}
