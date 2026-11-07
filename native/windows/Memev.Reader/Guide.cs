// SPDX-License-Identifier: Apache-2.0
// The first launch of the Windows window: what native/linux/src/guide.c does, in C#. It asks this computer or another, where the
// notes live, which apps the core lists, and whether to record the person's own git remote. Only the commands in
// native/CLI-CONTRACT.md are built; no connect, push, github or cloud command, and nothing runs before the person continues.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Memev.Reader;

public sealed class GuideApp
{
  public string Name { get; init; } = "";
  public string State { get; init; } = "";
  public bool Selectable { get; init; }
}

public sealed class GuidePlan
{
  public int FirstComputer = -1; // 1 this computer, 0 another, -1 not chosen
  public string NotesDir = "";
  public int Port = 18799;
  public string JoinCode = "";
  public bool ConfirmJoin;
  public string BackupUrl = "";
  public bool ConfirmBackup;
  public List<string> Selected { get; } = new();
  public string Phrase = "The proof note is a blue window.";
}

public static class Guide
{
  static readonly string[] Suffixes =
  {
    "found, not supported yet", "found, disconnected by you", "found, not connected", "connected (hooks + MCP + instructions)",
    "connected (MCP + instructions)", "connected (hooks + MCP)", "not found",
  };

  // The core's `clients` lines: a marker, a name and a state. Names come only from that text.
  public static List<GuideApp> ParseClients(string text)
  {
    var apps = new List<GuideApp>();
    foreach (string raw in (text ?? "").Split('\n'))
    {
      if (apps.Count >= 32) break;
      string s = raw.TrimEnd('\r').TrimStart(' ', '\t');
      int i = 0;
      while (i < s.Length && !char.IsAsciiLetter(s[i])) i++; // the marker before the name may be more than one character
      s = s.Substring(i);
      string? matched = Suffixes.FirstOrDefault((x) => s.Length > x.Length && s.EndsWith(x, StringComparison.Ordinal));
      if (matched == null) continue;
      string name = s.Substring(0, s.Length - matched.Length).TrimEnd(' ');
      if (name.Length == 0 || name.Length >= 80) continue;
      apps.Add(new GuideApp { Name = name, State = matched, Selectable = matched == "found, not connected" });
    }
    return apps;
  }

  static bool HostOk(string h) => h.Length > 0 && h[0] != '-' && h[0] != '/';

  // A remote address the window will record: file:///, https://, ssh://, git:// or git@host:path, with no space or control
  // character, and a host that cannot be read as an option.
  public static bool UrlOk(string s)
  {
    if (s.Length < 8 || s.Length > 400 || s.Any((c) => c <= ' ' || c == 127)) return false;
    if (s.StartsWith("file:///", StringComparison.Ordinal)) return s.Length > 8;
    if (s.StartsWith("https://", StringComparison.Ordinal)) return HostOk(s.Substring(8));
    if (s.StartsWith("ssh://", StringComparison.Ordinal)) return HostOk(s.Substring(6));
    if (s.StartsWith("git://", StringComparison.Ordinal)) return HostOk(s.Substring(6));
    if (s.StartsWith("git@", StringComparison.Ordinal))
    {
      int colon = s.IndexOf(':', 4);
      return colon > 4 && HostOk(s.Substring(4)) && colon + 1 < s.Length;
    }
    return false;
  }

  static bool CodeOk(string s) => s.Length > 0 && s[0] != '-' && s.All((c) => c > ' ' && c != 127);

  // A full path, resolved as the system resolves it, that is not the root of a disk or a share. null when it cannot be used.
  public static string? NotesFolder(string s)
  {
    if (s.Length < 2 || s.Length > 400 || s.Any((c) => c < ' ' || c == 127) || !Path.IsPathFullyQualified(s)) return null;
    string full;
    try { full = Path.GetFullPath(s); } catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException) { return null; }
    string trimmed = Path.TrimEndingDirectorySeparator(full);
    string? root = Path.GetPathRoot(full);
    if (root == null || string.Equals(Path.TrimEndingDirectorySeparator(root), trimmed, StringComparison.OrdinalIgnoreCase)) return null;
    return trimmed;
  }

  // The commands that may run for this plan, in order, each without the program name. null and a reason when the plan is not ready.
  public static List<string[]>? Build(GuidePlan plan, out string why)
  {
    why = "";
    if (plan.FirstComputer < 0) { why = "Choose this computer or another one."; return null; }
    if (plan.FirstComputer == 0)
    {
      if (!plan.ConfirmJoin || !CodeOk(plan.JoinCode)) { why = "Joining waits until you enter a code and confirm. Nothing is sent before that."; return null; }
      return new List<string[]> { new[] { "join", plan.JoinCode } };
    }
    string? dir = NotesFolder(plan.NotesDir);
    if (dir == null) { why = "The notes folder has to be a full path, and not the root of a disk."; return null; }
    if (plan.Phrase.Length == 0) return null;
    int port = plan.Port > 0 && plan.Port < 65536 ? plan.Port : 18799;
    var cmds = new List<string[]> { new[] { "init", "--root", dir, "--local-only", "--no-connect", "--no-service", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture) } };
    if (plan.ConfirmBackup)
    {
      if (!UrlOk(plan.BackupUrl)) { why = "The remote address is not one this window will record."; return null; }
      cmds.Add(new[] { "backup", "set", plan.BackupUrl });
    }
    cmds.Add(new[] { "remember", plan.Phrase, "--force" });
    cmds.Add(new[] { "recall", plan.Phrase, "--json" });
    if (plan.Selected.Count > 0) why = "No app is connected. The client list has names, not the id the connect command needs, and this window does not keep its own list.";
    return cmds;
  }

  // A failed command's last output, with a line that names a token cut there, so its value is never shown.
  static string Scrub(string s)
  {
    string tail = s.Length > 180 ? s.Substring(s.Length - 180) : s;
    var lines = tail.Split('\n').Select((l) => { int t = l.IndexOf("token", StringComparison.Ordinal); return t >= 0 ? l.Substring(0, t) : l; });
    return new string(string.Join("\n", lines).Select((c) => c < ' ' && c != '\n' ? ' ' : c).ToArray());
  }

  // Run the plan against bin. true when every command ends well and, on this computer, the recall holds the saved phrase.
  public static bool Apply(GuidePlan plan, string bin, out string detail)
  {
    detail = "";
    var cmds = Build(plan, out string why);
    if (cmds == null) { detail = why.Length > 0 ? why : "The steps are not ready."; return false; }
    foreach (string[] args in cmds)
    {
      var r = Core.Capture(bin, args, args[0] == "init" ? 20000 : 10000);
      if (r.Done && r.ExitCode == 126) { detail = "That folder, address or code holds a character this window will not pass to the core: \" % ! & | < > ^ ( ). Choose one without it."; return false; }
      if (!r.Done || r.ExitCode != 0) { detail = $"The command {args[0]} stopped. {Scrub(r.Output)}".TrimEnd(); return false; }
      if (args[0] == "recall" && !r.Output.Contains(plan.Phrase, StringComparison.Ordinal)) { detail = "Recall did not return the note just saved."; return false; }
    }
    detail = why;
    return true;
  }
}
