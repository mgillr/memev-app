// SPDX-License-Identifier: Apache-2.0
// The rev2 colour tokens and type (native/REV2-SCREENS.md 1.12 and 1.14), with the exact hex of the mockup's latest values, the
// same as native/macos/src/Theme.swift. Each token is a brush in the application's resources under its own name, and every part
// of the window takes its colours from there by reference, so a change of theme repaints the whole window at once.
// Fonts: Space Grotesk for everything and IBM Plex Mono for labels, commands and machine text. Both are under the SIL Open Font
// License and are built into the program from native/macos/fonts, each checked against its pinned SHA-256 at build time
// (Memev.Desktop.csproj). When they cannot be loaded the window uses Segoe UI and Consolas at the same sizes and weights.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Memev.Desktop;

sealed class Theme
{
  public required string Name;
  public required bool Dark;
  public required Dictionary<string, Color> Tokens;
  public required Color Scrim;
  public required double ShadowOpacity, ShadowRadius, ShadowY;

  static Color C(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

  public static readonly Theme DarkTokens = new()
  {
    Name = "dark", Dark = true,
    Tokens = new()
    {
      ["page"] = C(0x080B10), ["panel"] = C(0x121822), ["raise"] = C(0x1B222D), ["line"] = C(0x2B3543), ["rule"] = C(0x374252),
      ["ctl"] = C(0x6B7789), ["text"] = C(0xE8ECF2), ["sub"] = C(0xA4ADBA), ["dim"] = C(0x8A94A2), ["accent"] = C(0xF2B544),
      ["accentFill"] = C(0xF2B544), ["onAccent"] = C(0x0A0D12), ["accentRing"] = C(0xF2B544), ["good"] = C(0x5FD08F),
      ["warn"] = C(0xF2B544), ["bad"] = C(0xFF8A7A), ["series"] = C(0x7F8B9A), ["sample"] = C(0x5C6877),
    },
    Scrim = Color.FromArgb((byte)Math.Round(0.62 * 255), 4, 6, 9), ShadowOpacity = 0.5, ShadowRadius = 48, ShadowY = 16,
  };

  public static readonly Theme LightTokens = new()
  {
    Name = "light", Dark = false,
    Tokens = new()
    {
      ["page"] = C(0xEFF1F5), ["panel"] = C(0xFFFFFF), ["raise"] = C(0xE8EBF0), ["line"] = C(0xD6DBE3), ["rule"] = C(0xC3CAD4),
      ["ctl"] = C(0x7C8797), ["text"] = C(0x10151C), ["sub"] = C(0x46505E), ["dim"] = C(0x5F6978), ["accent"] = C(0x8A5600),
      ["accentFill"] = C(0xF2B544), ["onAccent"] = C(0x0A0D12), ["accentRing"] = C(0x8A5600), ["good"] = C(0x1D6B43),
      ["warn"] = C(0x8A5600), ["bad"] = C(0xB3261E), ["series"] = C(0x8590A0), ["sample"] = C(0xA3ADB9),
    },
    Scrim = Color.FromArgb((byte)Math.Round(0.32 * 255), 16, 21, 28), ShadowOpacity = 0.10, ShadowRadius = 24, ShadowY = 8,
  };

  // Agent panes and the Source view use fixed dark colours in both themes (1.12).
  public static readonly Color PaneHeader = C(0x1B222C), PaneHeaderText = C(0xE6E9EE), PaneState = C(0xB7BEC8), PaneControl = C(0x141A22);
  public static readonly Color PaneBody = C(0x0B0F14), PaneBodyText = C(0xD3D8DF), PaneBorder = C(0x2A323D);

  /// The theme in use.
  public static Theme Current = DarkTokens;

  public Color this[string token] => Tokens[token];

  /// Puts every token in the application's resources as a brush; everything that refers to a token repaints.
  public static void Apply(Theme t)
  {
    Current = t;
    var res = Application.Current.Resources;
    foreach (var kv in t.Tokens)
    {
      var b = new SolidColorBrush(kv.Value);
      b.Freeze();
      res[kv.Key] = b;
    }
    var scrim = new SolidColorBrush(t.Scrim);
    scrim.Freeze();
    res["scrim"] = scrim;
  }

  /// MEMEV_THEME forces a theme; otherwise the app's own setting; otherwise this computer's app theme (SH-24).
  public static Theme Pick(string appearance)
  {
    switch (Environment.GetEnvironmentVariable("MEMEV_THEME") ?? appearance)
    {
      case "dark": return DarkTokens;
      case "light": return LightTokens;
    }
    return SystemDark() ? DarkTokens : LightTokens;
  }

  /// Windows' "Choose your app mode": AppsUseLightTheme 0 is dark. Light when it cannot be read.
  public static bool SystemDark()
  {
    if (!OperatingSystem.IsWindows()) return false;
    try
    {
      using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
      return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
    }
    catch (System.Security.SecurityException) { return false; }
  }
}

/// The type scale of 1.14: size, line height, weight, tracking (WPF has no tracking; it is kept for the record) and family.
sealed record TypeStyle(double Size, double Line, int Weight, double Tracking, bool Mono)
{
  public static readonly TypeStyle PageTitle = new(28, 36, 500, -0.56, false);
  public static readonly TypeStyle PanelTitle = new(20, 28, 500, -0.2, false);
  public static readonly TypeStyle Sentence = new(16, 24, 400, 0, false);
  public static readonly TypeStyle ChartTitle = new(16, 24, 500, 0, false);
  public static readonly TypeStyle Wordmark = new(16, 24, 600, 0, false);
  public static readonly TypeStyle Body = new(14, 20, 400, 0, false);
  public static readonly TypeStyle BodyMedium = new(14, 20, 500, 0, false);
  public static readonly TypeStyle Small = new(12, 16, 400, 0, false);
  public static readonly TypeStyle SmallMedium = new(12, 16, 500, 0, false);
  public static readonly TypeStyle Tag = new(12, 20, 500, 0, false);
  public static readonly TypeStyle Label = new(12, 16, 500, 0.72, true); // 0.06em at 12 px
  public static readonly TypeStyle Code = new(12, 16, 400, 0, true);
  public static readonly TypeStyle CodeBlock = new(12, 20, 400, 0, true);

  public FontFamily Family => Mono ? Fonts.Mono : Fonts.Sans;
  public FontWeight FontWeight => FontWeight.FromOpenTypeWeight(Weight);
}

/// The design's two families, built into the program as resources. Each falls back to the system font on its own.
static class Fonts
{
  public const string SansName = "Space Grotesk";
  public const string MonoName = "IBM Plex Mono";
  public static FontFamily Sans { get; private set; } = new("Segoe UI");
  public static FontFamily Mono { get; private set; } = new("Consolas");
  public static bool SansBundled { get; private set; }
  public static bool MonoBundled { get; private set; }

  /// Finds the built-in fonts. MEMEV_FONTS=off stands in for fonts that cannot be loaded, so the fallback can be tested.
  public static void Load()
  {
    SansBundled = MonoBundled = false;
    Sans = new FontFamily("Segoe UI");
    Mono = new FontFamily("Consolas");
    if (Environment.GetEnvironmentVariable("MEMEV_FONTS") == "off") return;
    try
    {
      var found = System.Windows.Media.Fonts.GetFontFamilies(new Uri("pack://application:,,,/fonts/")).ToList();
      FontFamily? Named(string n) => found.FirstOrDefault((f) => f.FamilyNames.Values.Contains(n) || f.Source.EndsWith("#" + n, StringComparison.Ordinal));
      // A family counts only when a face of it can be built, not when WPF would quietly stand another font in.
      if (Named(SansName) is FontFamily s && Face(s, 400) is GlyphTypeface gs && gs.FamilyNames.Values.Contains(SansName)) { Sans = s; SansBundled = true; }
      if (Named(MonoName) is FontFamily m && Face(m, 400) is GlyphTypeface gm && gm.FamilyNames.Values.Contains(MonoName)) { Mono = m; MonoBundled = true; }
    }
    catch (Exception e) when (e is UriFormatException || e is System.IO.IOException || e is ArgumentException || e is NotSupportedException || e is InvalidOperationException) { }
  }

  public static GlyphTypeface? Face(FontFamily f, int weight)
  {
    var t = new Typeface(f, FontStyles.Normal, FontWeight.FromOpenTypeWeight(weight), FontStretches.Normal);
    return t.TryGetGlyphTypeface(out GlyphTypeface g) ? g : null;
  }

  static string En(IDictionary<System.Globalization.CultureInfo, string> names) =>
    names.FirstOrDefault((kv) => kv.Key.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase)).Value ?? names.Values.FirstOrDefault() ?? "";

  /// The family a style resolves to, as the font file names itself.
  public static string FamilyOf(TypeStyle s) => Face(s.Family, s.Weight) is GlyphTypeface g ? En(g.FamilyNames) : s.Family.Source;

  /// The weight WPF gives a face asked for at this weight (a variable font may answer with one weight for all).
  public static int WeightOf(TypeStyle s) => Face(s.Family, s.Weight) is GlyphTypeface g ? g.Weight.ToOpenTypeWeight() : 0;

  public static string FaceOf(TypeStyle s) => Face(s.Family, s.Weight) is GlyphTypeface g ? En(g.FaceNames) : "";

  /// The lines --fonts prints and the self-check reads.
  public static IEnumerable<string> Report()
  {
    yield return $"font_sans={FamilyOf(TypeStyle.Body)}";
    yield return $"font_mono={FamilyOf(TypeStyle.Label)}";
    yield return $"font_weights={WeightOf(TypeStyle.Body)},{WeightOf(TypeStyle.BodyMedium)},{WeightOf(TypeStyle.Wordmark)}";
    yield return $"font_mono_faces={FaceOf(TypeStyle.Code)},{FaceOf(TypeStyle.Label)}";
  }
}
