// SPDX-License-Identifier: Apache-2.0
// The reader's tests: the cases of native/linux/test/test-read.c, run against the C# reader. They need no window and no
// package, so they run on Windows in CI and on any computer with .NET. A fixture core is written as a .cmd file on Windows and
// as a shell script elsewhere.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Memev.Reader;

static class Program
{
  static int failed;

  static void Expect(bool ok, string what)
  {
    if (ok) return;
    Console.Error.WriteLine($"not ok: {what}");
    failed++;
  }

  static string MakeCore(string dir, string? statusBody, string? doctorBody, string? cardBody, int exitCode = 0)
  {
    Directory.CreateDirectory(dir);
    if (statusBody != null) File.WriteAllText(Path.Combine(dir, "status.json"), statusBody + "\n");
    if (doctorBody != null) File.WriteAllText(Path.Combine(dir, "doctor.json"), doctorBody + "\n");
    if (cardBody != null) File.WriteAllText(Path.Combine(dir, "card.json"), cardBody + "\n");
    if (OperatingSystem.IsWindows())
    {
      string bin = Path.Combine(dir, "memev.cmd");
      File.WriteAllText(bin, exitCode != 0
        ? $"@echo off\r\nexit /b {exitCode}\r\n"
        : "@echo off\r\nif \"%~1\"==\"status\" goto show\r\nif \"%~1\"==\"doctor\" goto show\r\nif \"%~1\"==\"card\" goto show\r\nexit /b 2\r\n:show\r\ntype \"%~dp0%~1.json\"\r\n");
      return bin;
    }
    else
    {
      string bin = Path.Combine(dir, "memev");
      File.WriteAllText(bin, exitCode != 0
        ? $"#!/bin/sh\nexit {exitCode}\n"
        : "#!/bin/sh\nd=$(CDPATH= cd -- \"$(dirname \"$0\")\" && pwd)\ncase \"$1\" in\n  status|doctor|card) cat \"$d/$1.json\" ;;\n  *) exit 2 ;;\nesac\n");
      File.SetUnixFileMode(bin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
      return bin;
    }
  }

  static void TestJson()
  {
    const string doc = "{\"version\":\"0.1.0\",\"index\":{\"notes\":3},\"skip\":\"{not}\",\"name\":\"b\\\"c\"}";
    Expect(Json.TryGet(doc, "version", out string v) && v == "0.1.0", "version");
    Expect(Json.TryGet(doc, "index.notes", out string n) && n == "3", "notes");
    Expect(Json.TryGet(doc, "name", out string name) && name == "b\"c", "escape");
    Expect(!Json.TryGet(doc, "missing", out _), "missing");
    Expect(!Json.TryGet("{\"a\":null}", "a", out _), "null");
    Expect(Json.TryGet("{\"ok\":true}", "ok", out string b) && b == "true", "bool");
    Expect(Json.TryGet("{\"s\":\"a\\nb\"}", "s", out string nl) && nl == "a b", "a line break becomes a space");
    Expect(Json.TryGet("{\"s\":\"x\\ud800y\"}", "s", out string lone) && lone == "x?y", "a lone surrogate escape reads as '?' and does not throw");
    Expect(Json.TryGet("{\"s\":\"caf\\u00e9\"}", "s", out string u) && u == "caf?", "a \\u escape reads as '?', as in read.c");
    Expect(Json.TryGet("{\"a\":\"first\",\"a\":\"second\"}", "a", out string dup) && dup == "first", "a duplicate key gives the first value, as in read.c");
  }

  static void TestUrls()
  {
    Expect(Loopback.TryParse("http://127.0.0.1:7787/healthz", out string h, out int p, out string path) && h == "127.0.0.1" && p == 7787 && path == "/healthz", "v4");
    Expect(Loopback.TryParse("http://localhost/healthz", out h, out p, out _) && h == "localhost" && p == 80, "localhost");
    Expect(Loopback.TryParse("http://[::1]:7787/healthz", out h, out p, out _) && h == "::1" && p == 7787, "v6");
    string[] bad = { "https://127.0.0.1/healthz", "http://10.1.2.3/healthz", "http://192.0.2.1/healthz", "http://127.0.0.1:7787@192.0.2.1/",
      "http://localhost.example/healthz", "http://127.0.0.1:0/healthz", "http://127.0.0.1:99999/healthz", "file:///tmp/x", "" };
    foreach (string u in bad) Expect(!Loopback.TryParse(u, out _, out _, out _), u);
    // the refusal is timed after a warm-up call, so a cold start does not count; a connect attempt would take the whole 500 ms
    Http.Get("http://192.0.2.2/healthz", 500);
    var sw = Stopwatch.StartNew();
    Expect(Http.Get("http://192.0.2.1/healthz", 500) == null, "reject public-looking test address");
    Expect(sw.ElapsedMilliseconds < 400, $"reject did not wait on a connect ({sw.ElapsedMilliseconds} ms)");
  }

  static TcpListener Serve(out Thread server)
  {
    var l = new TcpListener(IPAddress.Loopback, 0);
    l.Start();
    server = new Thread(() =>
    {
      try
      {
        using TcpClient c = l.AcceptTcpClient();
        using NetworkStream s = c.GetStream();
        _ = s.ReadAtLeast(new byte[1024], 1, throwOnEndOfStream: false); // the request line is enough; its content is not checked
        const string payload = "{\"ok\":true,\"version\":\"9.9.9\"}";
        byte[] resp = Encoding.ASCII.GetBytes($"HTTP/1.0 200 OK\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n{payload}");
        s.Write(resp, 0, resp.Length);
      }
      catch (SocketException) { }
      finally { l.Stop(); }
    }) { IsBackground = true };
    server.Start();
    return l;
  }

  static void TestHttp()
  {
    var l = Serve(out Thread t);
    int port = ((IPEndPoint)l.LocalEndpoint).Port;
    string? body = Http.Get($"http://127.0.0.1:{port}/healthz", 2000);
    Expect(body != null, "http get");
    Expect(body != null && Json.TryGet(body, "version", out string v) && v == "9.9.9", "http version");
    t.Join(2000);
  }

  static void TestGlance(string tmp)
  {
    string dir = Path.Combine(tmp, "core");
    string bin = MakeCore(dir,
      "{\"version\":\"0.1.0\",\"vault\":\"/tmp/vault-marker\",\"index\":{\"notes\":3},\"token\":\"should-not-appear\"}",
      "[{\"id\":\"node\",\"title\":\"Node\",\"status\":\"ok\"},{\"id\":\"backup\",\"title\":\"Backup\",\"status\":\"warn\",\"token\":\"should-not-appear\"}]",
      "{\"project\":\"demo\",\"north_star\":\"Ship the window\",\"token\":\"should-not-appear\"}");
    Environment.SetEnvironmentVariable("MEMEV_BIN", bin);
    Environment.SetEnvironmentVariable("MEMEV_HEALTH_URL", "off");
    Glance g = Read.Load();
    Expect(g.HaveBinary, "binary");
    Expect(g.DaemonUp, "up");
    Expect(g.Version == "0.1.0", "glance version");
    Expect(g.Notes == 3, "glance notes");
    Expect(g.Project == "demo", "project");
    Expect(g.NorthStar == "Ship the window", "star");
    Expect(g.ChecksOk == 1 && g.ChecksWarn == 1 && g.CheckCount == 2, "checks");
    string overview = Read.Overview(g);
    Expect(overview == "Memev 0.1.0 is running for demo. 3 notes are in the index. 1 check to look at.", overview);
    string health = Read.Health(g);
    Expect(health == "1 check to look at.", health);
    Expect(!overview.Contains("should-not-appear") && !health.Contains("should-not-appear"), "token hidden");
    Expect(!overview.Contains("vault-marker"), "path hidden");

    File.WriteAllText(Path.Combine(dir, "status.json"), "{\"version\":\"0.1.0\",\"index\":{\"notes\":5}}\n");
    g = Read.Load();
    Expect(Read.Overview(g).Contains("5 notes are in the index."), "reload sees the new count");
    File.WriteAllText(Path.Combine(dir, "card.json"), "{\"project\":\"/tmp/vault-marker\"}\n");
    g = Read.Load();
    Expect(g.Project.Length == 0, "path project is not shown");
    File.WriteAllText(Path.Combine(dir, "doctor.json"), "{\"checks\":[{\"id\":\"x\",\"status\":\"FAIL\"},{\"title\":\"\",\"status\":\"fail\"}]}\n");
    g = Read.Load();
    Expect(g.CheckCount == 2 && g.ChecksWarn == 1 && g.ChecksFail == 1, "checks under a key, and a status that is not lower case counts as warn");
    Expect(g.Checks.Count == 2 && g.Checks[0].Status == "unknown" && g.Checks[0].Title == "x" && g.Checks[1].Title == "Check", "status word and title fallbacks");
    File.WriteAllText(Path.Combine(dir, "card.json"), "{\"project\":\"" + new string('\u6f22', 30) + "\"}\n"); // 30 characters, 90 UTF-8 bytes
    g = Read.Load();
    Expect(g.Project.Length == 0, "a project name over 79 bytes is not shown, though it is under 79 characters");
  }

  // A core that exits while a child it started keeps stdout and stderr open: the read ends at its deadline, as read.c's does.
  static void TestHeldOpen(string tmp)
  {
    string dir = Path.Combine(tmp, "held");
    Directory.CreateDirectory(dir);
    string bin;
    if (OperatingSystem.IsWindows())
    {
      bin = Path.Combine(dir, "memev.cmd");
      File.WriteAllText(bin, "@echo off\r\nstart \"\" /b powershell -NoProfile -Command \"Start-Sleep -Seconds 12\"\r\necho {}\r\n");
    }
    else
    {
      bin = Path.Combine(dir, "memev");
      File.WriteAllText(bin, "#!/bin/sh\nsleep 12 &\necho '{}'\n");
      File.SetUnixFileMode(bin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    var sw = Stopwatch.StartNew();
    Core.Result r = Core.Capture(bin, new[] { "status", "--json" }, 3000);
    long ms = sw.ElapsedMilliseconds;
    Expect(!r.Done, "a read whose output is held open ends as out of time");
    Expect(ms < 4500, $"a read whose output is held open ends near its 3000 ms deadline, not later ({ms} ms)");
  }

  static void TestHealthOnly(string tmp)
  {
    string bin = MakeCore(Path.Combine(tmp, "down"), null, null, null, exitCode: 1);
    var l = Serve(out Thread t);
    int port = ((IPEndPoint)l.LocalEndpoint).Port;
    Environment.SetEnvironmentVariable("MEMEV_BIN", bin);
    Environment.SetEnvironmentVariable("MEMEV_HEALTH_URL", $"http://127.0.0.1:{port}/healthz");
    Glance g = Read.Load();
    t.Join(2000);
    Expect(g.DaemonUp && g.Version == "9.9.9", "healthz alone marks the core up");
    Expect(g.HaveBinary, "down binary still counts as present");
    Expect(Read.Overview(g) == "Memev 9.9.9 is running.", Read.Overview(g));
  }

  static void TestMissing(string tmp)
  {
    Environment.SetEnvironmentVariable("MEMEV_BIN", Path.Combine(tmp, "memev-does-not-exist"));
    Environment.SetEnvironmentVariable("MEMEV_HEALTH_URL", "off");
    Glance g = Read.Load();
    Expect(!g.HaveBinary && !g.DaemonUp, "missing");
    Expect(Read.Overview(g) == "This app only reads a Free Core install. It does not install one.", Read.Overview(g));
    Expect(Read.Health(g) == "Memev is not on this computer yet.", Read.Health(g));
  }

  // `proof <memev> <notes folder>`: the first launch against a real core, as native/linux/src/proof.c. A remote address is
  // given without the tick, so the caller can check that none was recorded.
  static int Proof(string bin, string notes)
  {
    var clients = Core.Capture(bin, new[] { "clients" }, 30000);
    if (!clients.Done || clients.ExitCode != 0) { Console.Error.WriteLine("clients command failed"); return 1; }
    var apps = Guide.ParseClients(clients.Output);
    if (apps.Count < 1) { Console.Error.WriteLine("the client list did not parse"); return 1; }
    Console.WriteLine($"clients={apps.Count} selectable={apps.Count((a) => a.Selectable)}");
    var plan = new GuidePlan { FirstComputer = 1, NotesDir = notes, BackupUrl = "file:///C:/memev-proof-not-recorded.git", ConfirmBackup = false };
    if (int.TryParse(Environment.GetEnvironmentVariable("MEMEV_GUIDE_PORT"), out int port)) plan.Port = port;
    bool ok = Guide.Apply(plan, bin, out string detail);
    if (!ok) { Console.Error.WriteLine($"proof failed: {detail}"); Core.Capture(bin, new[] { "stop" }, 15000); return 1; }
    Environment.SetEnvironmentVariable("MEMEV_BIN", bin);
    Environment.SetEnvironmentVariable("MEMEV_HEALTH_URL", "off");
    Glance g = Read.Load();
    string sentence = Read.Overview(g);
    Console.WriteLine($"overview={sentence}");
    Console.WriteLine($"notes={g.Notes}");
    Core.Capture(bin, new[] { "stop" }, 15000);
    if (!g.DaemonUp || !sentence.Contains("is running") || sentence.Contains("not running")) { Console.Error.WriteLine("overview did not come from a running core"); return 1; }
    if (g.Notes < 1) { Console.Error.WriteLine("the core reported no notes"); return 1; }
    Console.WriteLine("proof=ok");
    return 0;
  }

  static int Main(string[] args)
  {
    if (args.Length == 3 && args[0] == "proof") return Proof(args[1], args[2]);
    string tmp = Path.Combine(Path.GetTempPath(), "memev-read-" + Guid.NewGuid().ToString("N").Substring(0, 8));
    Directory.CreateDirectory(tmp);
    // one guide case: 'ok <name>' only after its own checks passed; an unknown name is an error
    if (args.Length > 0 && args[0] == "--case")
    {
      try
      {
        if (args.Length != 2 || !GuideCases.Names.Contains(args[1])) { Console.Error.WriteLine($"unknown case; the cases are {string.Join(", ", GuideCases.Names)}"); return 2; }
        GuideCases.Run(args[1], Expect, tmp);
        if (failed > 0) { Console.Error.WriteLine($"{failed} check(s) of {args[1]} failed"); return 1; }
        Console.WriteLine($"ok {args[1]}");
        return 0;
      }
      finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
    }
    if (args.Length > 0) { Console.Error.WriteLine("usage: Memev.Reader.Check [--case <name> | proof <memev> <notes folder>]"); Directory.Delete(tmp, true); return 2; }
    try
    {
      foreach (string name in GuideCases.Names) GuideCases.Run(name, Expect, tmp);
      TestJson();
      TestUrls();
      TestHttp();
      TestGlance(tmp);
      TestHealthOnly(tmp);
      TestHeldOpen(tmp);
      TestMissing(tmp);
      GuideTests.Run(Expect, tmp);
      QrTests.Run(Expect);
    }
    finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
    if (failed > 0) { Console.Error.WriteLine($"{failed} reader test(s) failed"); return 1; }
    Console.WriteLine("read tests passed");
    return 0;
  }
}
