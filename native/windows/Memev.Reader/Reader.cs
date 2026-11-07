// SPDX-License-Identifier: Apache-2.0
// The Windows window's reader. It does what native/linux/src/read.c does, in C#, because read.c is POSIX only:
// it runs `memev status --json`, `memev doctor --json` and `memev card --json`, then GET /healthz on loopback, keeps only the
// fields the window shows, and builds the same two sentences. It writes nothing, and it never resolves a host name.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Memev.Reader;

public sealed class Check
{
  public string Id { get; init; } = "";
  public string Title { get; init; } = "";
  public string Status { get; init; } = "";
}

public sealed class Glance
{
  public const int ChecksMax = 64;
  public bool HaveBinary;
  public bool DaemonUp;
  public string Version = "";
  public string Project = "";
  public string NorthStar = "";
  public int Notes = -1; // -1 when the core did not say
  public int ChecksOk;
  public int ChecksWarn;
  public int ChecksFail;
  public int CheckCount;
  public List<Check> Checks { get; } = new();
  public string Error = "";
  public int Look => ChecksFail + ChecksWarn;
}

public static class Json
{
  // A string, number or boolean at a dotted object key, as read.c's memev_json_get: null, objects and arrays are misses.
  public static bool TryGet(string json, string key, out string value)
  {
    value = "";
    JsonDocument doc;
    try { doc = JsonDocument.Parse(json); } catch (JsonException) { return false; }
    using (doc) return TryGet(doc.RootElement, key, out value);
  }

  public static bool TryGet(JsonElement root, string key, out string value)
  {
    value = "";
    JsonElement cur = root;
    foreach (string part in key.Split('.'))
    {
      if (part.Length == 0 || cur.ValueKind != JsonValueKind.Object || !TryGetFirst(cur, part, out JsonElement next)) return false;
      cur = next;
    }
    switch (cur.ValueKind)
    {
      case JsonValueKind.String:
        value = Decode(cur.GetRawText());
        return true;
      case JsonValueKind.Number: value = cur.GetRawText(); return true;
      case JsonValueKind.True: value = "true"; return true;
      case JsonValueKind.False: value = "false"; return true;
      default: return false;
    }
  }

  // The first property of that name, as read.c finds it; JsonElement.TryGetProperty would give the last one.
  public static bool TryGetFirst(JsonElement obj, string name, out JsonElement value)
  {
    foreach (JsonProperty p in obj.EnumerateObject())
      if (p.NameEquals(name)) { value = p.Value; return true; }
    value = default;
    return false;
  }

  // A JSON string token decoded as read.c's decode_scalar does, from its raw text, so no escape can throw: \n, \t, \r, \", \\ and
  // \/ as themselves, a \u escape as '?', any other escaped character as itself, and a line break as a space.
  static string Decode(string raw)
  {
    var sb = new StringBuilder();
    int end = raw.Length - 1; // raw holds its quotes
    for (int i = 1; i < end;)
    {
      char c;
      if (raw[i] == '\\')
      {
        i++;
        if (i >= end) break;
        char e = raw[i++];
        switch (e)
        {
          case 'n': c = '\n'; break;
          case 't': c = '\t'; break;
          case 'r': c = '\r'; break;
          case 'u': c = '?'; for (int k = 0; k < 4 && i < end; k++) i++; break;
          default: c = e; break;
        }
      }
      else c = raw[i++];
      sb.Append(c == '\n' || c == '\r' ? ' ' : c);
    }
    return sb.ToString();
  }
}

public static class Loopback
{
  // http only; the host is 127.0.0.1, localhost or [::1], written out, never looked up. As read.c's memev_loopback_url.
  public static bool TryParse(string? url, out string host, out int port, out string path)
  {
    host = ""; port = 0; path = "";
    if (url == null || !url.StartsWith("http://", StringComparison.Ordinal)) return false;
    string p = url.Substring(7);
    if (p.StartsWith("[", StringComparison.Ordinal))
    {
      if (!p.StartsWith("[::1]", StringComparison.Ordinal)) return false;
      host = "::1";
      p = p.Substring(5);
    }
    else
    {
      int end = p.Length;
      int slash = p.IndexOf('/');
      int colon = p.IndexOf(':');
      if (slash >= 0 && slash < end) end = slash;
      if (colon >= 0 && colon < end) end = colon;
      if (end == 0) return false;
      host = p.Substring(0, end);
      if (host != "127.0.0.1" && host != "localhost") return false;
      p = p.Substring(end);
    }
    if (p.StartsWith(":", StringComparison.Ordinal))
    {
      int i = 1;
      while (i < p.Length && char.IsAsciiDigit(p[i])) i++;
      if (i == 1 || !long.TryParse(p.AsSpan(1, i - 1), out long n) || n < 1 || n > 65535) return false;
      port = (int)n;
      p = p.Substring(i);
    }
    else port = 80;
    if (p.Length == 0) { path = "/"; return true; }
    if (!p.StartsWith("/", StringComparison.Ordinal) || p.Length >= 1024) return false;
    int hash = p.IndexOf('#');
    path = hash >= 0 ? p.Substring(0, hash) : p;
    return true;
  }
}

public static class Http
{
  // GET a loopback URL over a raw socket (no proxy, no name lookup). The body on HTTP 200, else null.
  public static string? Get(string url, int timeoutMs)
  {
    if (!Loopback.TryParse(url, out string host, out int port, out string path) || timeoutMs < 1) return null;
    IPAddress addr = host == "::1" ? IPAddress.IPv6Loopback : IPAddress.Loopback;
    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    try
    {
      using var sock = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
      using (var cts = new CancellationTokenSource(timeoutMs))
        sock.ConnectAsync(new IPEndPoint(addr, port), cts.Token).AsTask().GetAwaiter().GetResult();
      byte[] req = Encoding.ASCII.GetBytes($"GET {path} HTTP/1.0\r\nHost: {(host == "::1" ? "[::1]" : "127.0.0.1")}\r\nConnection: close\r\n\r\n");
      sock.SendTimeout = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
      sock.Send(req);
      var raw = new MemoryStream();
      byte[] buf = new byte[4096];
      for (;;)
      {
        int left = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
        if (left <= 0) return null;
        sock.ReceiveTimeout = left;
        int n = sock.Receive(buf);
        if (n == 0) break;
        if (raw.Length + n >= 16384) return null;
        raw.Write(buf, 0, n);
      }
      string text = Encoding.UTF8.GetString(raw.ToArray());
      int split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
      if (split < 0 || !text.StartsWith("HTTP/", StringComparison.Ordinal)) return null;
      string[] status = text.Substring(0, text.IndexOf('\r')).Split(' ');
      if (status.Length < 2 || status[1] != "200") return null;
      return text.Substring(split + 4);
    }
    catch (Exception e) when (e is SocketException || e is OperationCanceledException || e is IOException) { return null; }
  }
}

public static class Core
{
  static readonly bool Windows = OperatingSystem.IsWindows();

  static bool Executable(string path)
  {
    if (!File.Exists(path)) return false;
    if (OperatingSystem.IsWindows()) return true;
    return (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
  }

  // MEMEV_BIN, or memev on PATH (with PATHEXT on Windows, where npm installs memev.cmd). null when there is none.
  public static string? ResolveBin()
  {
    string? env = Environment.GetEnvironmentVariable("MEMEV_BIN");
    if (!string.IsNullOrEmpty(env)) return Executable(env) ? env : null;
    string? path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrEmpty(path)) return null;
    string[] exts = Windows ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries) : new[] { "" };
    foreach (string dir in path.Split(Path.PathSeparator))
    {
      if (dir.Length == 0) continue;
      foreach (string ext in exts)
      {
        string candidate = Path.Combine(dir, "memev" + ext.ToLowerInvariant());
        if (Executable(candidate)) return candidate;
      }
    }
    return null;
  }

  public sealed record Result(bool Done, int ExitCode, string Output);

  // Run bin with args, stdout captured (at most 256 KiB), stderr dropped. Done is false when it ran out of time, and it is then killed.
  public static Result Capture(string bin, string[] args, int timeoutMs)
  {
    var psi = new ProcessStartInfo { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    string ext = Path.GetExtension(bin).ToLowerInvariant();
    if (Windows && (ext == ".cmd" || ext == ".bat"))
    {
      // a batch file runs through cmd.exe; the arguments are fixed words of this program, never text from the core
      psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
      psi.Arguments = $"/d /s /c \"\"{bin}\" {string.Join(' ', args)}\"";
    }
    else
    {
      psi.FileName = bin;
      foreach (string a in args) psi.ArgumentList.Add(a);
    }
    Process p;
    try { p = Process.Start(psi) ?? throw new InvalidOperationException("no process"); }
    catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is IOException) { return new Result(true, 127, ""); }
    using (p)
    {
      // stderr is drained and dropped, as read.c sends it to /dev/null. Every wait below has the deadline: WaitForExit(int) does not
      // wait for the streams, so a grandchild that keeps stdout or stderr open cannot hold this past timeoutMs, as it cannot in read.c.
      p.ErrorDataReceived += (_, _) => { };
      p.BeginErrorReadLine();
      var outTask = ReadCapped(p.StandardOutput.BaseStream, 262143);
      var clock = Stopwatch.StartNew();
      if (p.WaitForExit(timeoutMs) && outTask.Wait(Math.Max(0, timeoutMs - (int)clock.ElapsedMilliseconds)))
        return new Result(true, p.ExitCode, outTask.Result);
      try { p.Kill(true); } catch (InvalidOperationException) { }
      p.WaitForExit(1000);
      return new Result(false, 128, "");
    }
  }

  static async Task<string> ReadCapped(Stream s, int cap)
  {
    var ms = new MemoryStream();
    byte[] buf = new byte[4096];
    int n;
    while ((n = await s.ReadAsync(buf).ConfigureAwait(false)) > 0)
    {
      int take = (int)Math.Min(n, Math.Max(0, cap - ms.Length));
      if (take > 0) ms.Write(buf, 0, take);
    }
    return Encoding.UTF8.GetString(ms.ToArray());
  }
}

public static class Read
{
  static bool PlainVersion(string s)
  {
    if (s.Length == 0 || s.Length > 31) return false;
    foreach (char c in s) if ((c < '0' || c > '9') && c != '.') return false;
    return true;
  }

  // cap counts UTF-8 bytes, as read.c's strlen does
  static bool PlainLabel(string s, int cap)
  {
    if (s.Length == 0 || Encoding.UTF8.GetByteCount(s) > cap) return false;
    foreach (char c in s) if (c < 32 || c == '/' || c == '\\') return false;
    return true;
  }

  static string StatusWord(string s)
  {
    int i = 0;
    while (i < s.Length && i < 12 && s[i] >= 'a' && s[i] <= 'z') i++;
    return i == 0 ? "unknown" : s.Substring(0, i);
  }

  static string Cut(string s, int max) => s.Length > max ? s.Substring(0, max) : s;

  static bool JsonValue(string s)
  {
    string t = s.TrimStart(' ', '\t', '\r', '\n');
    return t.StartsWith("{", StringComparison.Ordinal) || t.StartsWith("[", StringComparison.Ordinal);
  }

  static void LoadChecks(Glance g, string json)
  {
    JsonDocument doc;
    try { doc = JsonDocument.Parse(json); } catch (JsonException) { return; }
    using (doc)
    {
      JsonElement list = doc.RootElement;
      if (list.ValueKind == JsonValueKind.Object && Json.TryGetFirst(list, "checks", out JsonElement inner)) list = inner;
      if (list.ValueKind != JsonValueKind.Array) return;
      foreach (JsonElement item in list.EnumerateArray())
      {
        if (item.ValueKind != JsonValueKind.Object) return;
        if (item.GetRawText().Length >= 2048) continue; // read.c skips an entry it cannot hold, without counting it
        Json.TryGet(item, "id", out string id);
        Json.TryGet(item, "title", out string title);
        Json.TryGet(item, "status", out string status);
        id = Cut(id, 63); title = Cut(title, 127);
        string word = StatusWord(status);
        if (word == "ok") g.ChecksOk++; else if (word == "fail") g.ChecksFail++; else g.ChecksWarn++;
        if (g.Checks.Count < Glance.ChecksMax)
          g.Checks.Add(new Check { Id = id, Title = title.Length > 0 ? title : id.Length > 0 ? id : "Check", Status = word });
        g.CheckCount++;
      }
    }
  }

  // Read status, doctor and card from MEMEV_BIN or PATH, then loopback /healthz. MEMEV_HEALTH_URL=off skips the request. Writes nothing.
  public static Glance Load()
  {
    var g = new Glance();
    string? bin = Core.ResolveBin();
    if (bin == null) { g.Error = "memev was not found"; return g; }
    g.HaveBinary = true;
    var status = Core.Capture(bin, new[] { "status", "--json" }, 3000);
    if (status.Done && status.ExitCode == 0 && JsonValue(status.Output))
    {
      g.DaemonUp = true;
      if (Json.TryGet(status.Output, "version", out string v) && PlainVersion(v)) g.Version = v;
      if (Json.TryGet(status.Output, "index.notes", out string n))
      {
        int i = 0;
        bool neg = n.StartsWith("-", StringComparison.Ordinal);
        int start = neg || n.StartsWith("+", StringComparison.Ordinal) ? 1 : 0;
        for (i = start; i < n.Length && char.IsAsciiDigit(n[i]); i++) { }
        if (i > start && !neg && long.TryParse(n.AsSpan(start, i - start), out long notes) && notes < 1000000000L) g.Notes = (int)notes;
      }
    }
    var doctor = Core.Capture(bin, new[] { "doctor", "--json" }, 3000);
    if (doctor.Done && doctor.ExitCode == 0 && JsonValue(doctor.Output)) LoadChecks(g, doctor.Output);
    var card = Core.Capture(bin, new[] { "card", "--json" }, 3000);
    if (card.Done && card.ExitCode == 0 && JsonValue(card.Output))
    {
      if (Json.TryGet(card.Output, "project", out string project) && PlainLabel(project, 79)) g.Project = project;
      if (Json.TryGet(card.Output, "north_star", out string star) && PlainLabel(star, 159)) g.NorthStar = star;
    }
    string health = Environment.GetEnvironmentVariable("MEMEV_HEALTH_URL") ?? "http://127.0.0.1:7787/healthz";
    if (health != "off")
    {
      string? body = Http.Get(health, 500);
      if (body != null && Json.TryGet(body, "ok", out string ok) && ok == "true")
      {
        g.DaemonUp = true;
        if (g.Version.Length == 0 && Json.TryGet(body, "version", out string hv) && PlainVersion(hv)) g.Version = hv;
      }
    }
    return g;
  }

  public static string Overview(Glance g)
  {
    if (!g.HaveBinary) return "This app only reads a Free Core install. It does not install one.";
    if (!g.DaemonUp) return "Memev is not running. Start it on this computer, then press Refresh.";
    var s = new StringBuilder(g.Version.Length > 0 ? $"Memev {g.Version} is running" : "Memev is running");
    if (g.Project.Length > 0) s.Append(" for ").Append(g.Project);
    s.Append('.');
    if (g.Notes == 0) s.Append(" No notes are stored yet.");
    else if (g.Notes == 1) s.Append(" 1 note is in the index.");
    else if (g.Notes > 1) s.Append($" {g.Notes} notes are in the index.");
    if (g.Look == 1) s.Append(" 1 check to look at.");
    else if (g.Look > 1) s.Append($" {g.Look} checks to look at.");
    return s.ToString();
  }

  public static string Health(Glance g)
  {
    if (!g.HaveBinary) return "Memev is not on this computer yet.";
    if (!g.DaemonUp) return "Memev is not running.";
    if (g.CheckCount == 0) return "Memev is running. No checks were returned.";
    if (g.Look == 0) return $"All {g.CheckCount} checks are fine.";
    if (g.Look == 1) return "1 check to look at.";
    return $"{g.Look} checks to look at.";
  }
}
