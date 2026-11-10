// SPDX-License-Identifier: Apache-2.0
// The Windows window's entry point. --version prints the version; --fonts prints the fonts in use with no window, for the
// self-check. An installed app carries its own core in resources\bin, used unless MEMEV_BIN names another.
using System;
using System.IO;
using System.Windows;

namespace Memev.Desktop;

static class Program
{
  // A window program has no console; when stdout is redirected (a test, CI), lines still reach it through the handle.
  static readonly Lazy<StreamWriter> stdout = new(() => new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
  public static void Out(string line) => stdout.Value.WriteLine(line);

  [STAThread]
  static int Main(string[] args)
  {
    if (args.Length > 0 && args[0] == "--version")
    {
      Out("memev-desktop 0.0.1-slice1");
      return 0;
    }
    string bundled = Path.Combine(AppContext.BaseDirectory, "resources", "bin", "memev.cmd");
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MEMEV_BIN")) && File.Exists(bundled))
      Environment.SetEnvironmentVariable("MEMEV_BIN", bundled);
    var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
    // The design's fonts, found before any part is made; the system fonts stand in when they cannot be.
    Fonts.Load();
    if (args.Length > 0 && args[0] == "--fonts")
    {
      foreach (string line in Fonts.Report()) Out(line);
      return 0;
    }
    return app.Run(new MainWindow());
  }
}
