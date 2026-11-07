// SPDX-License-Identifier: Apache-2.0
// The rev2 parts the screens are built from, as native/macos/src/Components.swift draws them: status glyphs, the mark, buttons
// with the one-amber rule, list boxes and rows, section labels, the header rule, tabs, the page header, expanders and the
// "Coming later" rows. Every colour is a reference to a token in Theme.cs, so a change of theme repaints them.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Memev.Desktop;

enum GlyphKind { Ok, Warn, Bad, Live, Off }

enum ButtonStyleKind { Secondary, Ghost, Danger, Link }

static class Ui
{
  public static void Bg(FrameworkElement e, string token)
  {
    if (e is Control c) c.SetResourceReference(Control.BackgroundProperty, token);
    else if (e is Border b) b.SetResourceReference(Border.BackgroundProperty, token);
    else if (e is Panel p) p.SetResourceReference(Panel.BackgroundProperty, token);
  }

  public static TextBlock Text(string text, TypeStyle s, string role, double wrap = 0)
  {
    var t = new TextBlock { Text = text };
    Style(t, s);
    t.SetResourceReference(TextBlock.ForegroundProperty, role);
    if (wrap > 0) { t.TextWrapping = TextWrapping.Wrap; t.MaxWidth = wrap; }
    return t;
  }

  public static void Style(TextBlock t, TypeStyle s)
  {
    t.FontFamily = s.Family;
    t.FontSize = s.Size;
    t.FontWeight = s.FontWeight;
    t.LineHeight = s.Line;
    t.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
  }

  public static Border Box(string fill, string? border = null, double radius = 0, double thickness = 1)
  {
    var b = new Border { CornerRadius = new CornerRadius(radius) };
    if (fill != "clear") b.SetResourceReference(Border.BackgroundProperty, fill);
    if (border != null) { b.SetResourceReference(Border.BorderBrushProperty, border); b.BorderThickness = new Thickness(thickness); }
    return b;
  }

  public static Border Hairline(string role = "rule", bool vertical = false)
  {
    var b = new Border();
    b.SetResourceReference(Border.BackgroundProperty, role);
    if (vertical) b.Width = 1; else b.Height = 1;
    return b;
  }

  public static StackPanel V(double spacing = 0, params UIElement[] kids) => Add(new StackPanel { Orientation = Orientation.Vertical }, spacing, kids);

  public static StackPanel H(double spacing = 8, params UIElement[] kids) => Add(new StackPanel { Orientation = Orientation.Horizontal }, spacing, kids);

  /// Adds children with a gap before each one but the first (StackPanel has no spacing of its own).
  public static StackPanel Add(StackPanel p, double spacing, params UIElement[] kids)
  {
    foreach (UIElement k in kids) Put(p, k, spacing);
    return p;
  }

  public static void Put(StackPanel p, UIElement k, double gap)
  {
    if (p.Children.Count > 0 && k is FrameworkElement f)
    {
      Thickness m = f.Margin;
      f.Margin = p.Orientation == Orientation.Vertical ? new Thickness(m.Left, m.Top + gap, m.Right, m.Bottom) : new Thickness(m.Left + gap, m.Top, m.Right, m.Bottom);
    }
    p.Children.Add(k);
  }

  /// A row of parts with one part that takes the rest of the width (a DockPanel, last child fills).
  public static DockPanel Row(IEnumerable<UIElement> left, UIElement? fill, IEnumerable<UIElement> right, double gap = 12)
  {
    var d = new DockPanel { LastChildFill = true };
    bool first = true;
    foreach (UIElement e in left) { DockPanel.SetDock(e, Dock.Left); Gap(e, first ? 0 : gap, 0); first = false; d.Children.Add(e); }
    var rs = right.ToList();
    for (int i = rs.Count - 1; i >= 0; i--) { DockPanel.SetDock(rs[i], Dock.Right); Gap(rs[i], i == 0 ? gap : gap, 0); d.Children.Add(rs[i]); }
    if (fill != null) { Gap(fill, first ? 0 : gap, 0); d.Children.Add(fill); }
    return d;
  }

  static void Gap(UIElement e, double left, double right)
  {
    if (e is FrameworkElement f) f.Margin = new Thickness(f.Margin.Left + left, f.Margin.Top, f.Margin.Right + right, f.Margin.Bottom);
  }

  public static IEnumerable<DependencyObject> All(DependencyObject root)
  {
    yield return root;
    var seen = new HashSet<DependencyObject> { root };
    foreach (object child in LogicalTreeHelper.GetChildren(root))
      if (child is DependencyObject d && seen.Add(d)) foreach (DependencyObject x in All(d)) yield return x;
  }

  /// Whether an element and every parent above it are set to show. It does not wait for layout.
  public static bool Shown(DependencyObject? e)
  {
    while (e != null)
    {
      if (e is UIElement u && u.Visibility != Visibility.Visible) return false;
      e = LogicalTreeHelper.GetParent(e) ?? (e is Visual v ? VisualTreeHelper.GetParent(v) : null);
    }
    return true;
  }

  static ControlTemplate? plain;

  /// A button drawn only as a border round its content, so the rev2 look is not covered by the system chrome.
  public static ControlTemplate PlainTemplate => plain ??= (ControlTemplate)XamlReader.Parse(
    "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ButtonBase'>" +
    "<Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' " +
    "CornerRadius='8' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'>" +
    "<ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' RecognizesAccessKey='False'/>" +
    "</Border></ControlTemplate>");
}

/// A status glyph (1.13): each state has its own shape as well as its colour. As a pip (1.2), fine is filled in ctl.
sealed class Glyph : Grid
{
  GlyphKind kind;
  readonly double size;
  readonly bool pip;

  public Glyph(GlyphKind kind, double size = 8, bool pip = false)
  {
    this.size = size;
    this.pip = pip;
    Width = size;
    Height = size;
    VerticalAlignment = VerticalAlignment.Center;
    Kind = kind;
  }

  public GlyphKind Kind
  {
    get => kind;
    set
    {
      kind = value;
      Children.Clear();
      Shape s;
      switch (value)
      {
        case GlyphKind.Ok:
          s = new Rectangle { RadiusX = pip ? 1 : 2, RadiusY = pip ? 1 : 2 };
          s.SetResourceReference(Shape.FillProperty, pip ? "ctl" : "good");
          break;
        case GlyphKind.Warn:
          s = new Polygon { Points = new PointCollection { new(size / 2, 0), new(size, size / 2), new(size / 2, size), new(0, size / 2) } };
          s.SetResourceReference(Shape.FillProperty, "warn");
          break;
        case GlyphKind.Bad:
          s = new Polygon { Points = new PointCollection { new(0, size), new(size, size), new(size / 2, 0) } };
          s.SetResourceReference(Shape.FillProperty, "bad");
          break;
        case GlyphKind.Live:
          s = new Ellipse();
          s.SetResourceReference(Shape.FillProperty, "series");
          break;
        default:
          s = new Rectangle { RadiusX = 1, RadiusY = 1, StrokeThickness = 1 };
          s.SetResourceReference(Shape.StrokeProperty, "ctl");
          break;
      }
      Children.Add(s);
    }
  }
}

/// The wordmark's mark: a circle in text crossed by an amber line, 28 px.
sealed class Mark : Grid
{
  public Mark()
  {
    Width = Height = 28;
    var c = new Ellipse { Margin = new Thickness(4), StrokeThickness = 2 };
    c.SetResourceReference(Shape.StrokeProperty, "text");
    var l = new Rectangle { Height = 2, Margin = new Thickness(1, 0, 1, 0), VerticalAlignment = VerticalAlignment.Center };
    l.SetResourceReference(Shape.FillProperty, "accent");
    Children.Add(c);
    Children.Add(l);
  }
}

/// A button in the rev2 look. It is never amber by itself: Rank marks it as a candidate, and AmberRule picks at most one visible
/// candidate in the window or a sheet to fill (SH-20). Rank 1 is a page action, 2 a row or empty-state button, 3 the stopped
/// banner's button or a sheet's main button. A filled button carries Tag "primary" and the 1 px accentRing edge.
sealed class MButton : Button
{
  readonly TextBlock label = new();
  ButtonStyleKind kind;
  public int Rank;
  public bool Large;
  public Action? OnPress;
  public bool Amber { get; private set; }

  public MButton(string text, ButtonStyleKind kind = ButtonStyleKind.Secondary, int rank = 0, bool large = false, Action? onPress = null)
  {
    this.kind = kind;
    Rank = rank;
    Large = large;
    OnPress = onPress;
    Template = Ui.PlainTemplate;
    Content = label;
    Text = text;
    Cursor = Cursors.Hand;
    VerticalAlignment = VerticalAlignment.Center;
    HorizontalAlignment = HorizontalAlignment.Left;
    VerticalContentAlignment = VerticalAlignment.Center;
    HorizontalContentAlignment = HorizontalAlignment.Center;
    if (kind != ButtonStyleKind.Link) Height = large ? 40 : 32;
    Padding = kind == ButtonStyleKind.Link ? new Thickness(0) : new Thickness(large ? 16 : 12, 0, large ? 16 : 12, 0);
    Click += (_, e) => { if (ReferenceEquals(e.OriginalSource, this)) { e.Handled = true; OnPress?.Invoke(); } };
    IsEnabledChanged += (_, _) => Opacity = IsEnabled ? 1 : 0.5;
    Paint();
  }

  public string Text
  {
    get => label.Text;
    set { label.Text = value; AutomationProperties.SetName(this, value); }
  }

  public ButtonStyleKind Kind { get => kind; set { kind = value; Paint(); } }

  public void SetAmber(bool on)
  {
    if (on == Amber && (Tag as string == "primary") == on) return;
    Amber = on;
    Tag = on ? "primary" : null;
    Paint();
  }

  void Paint()
  {
    string fg = "text";
    TypeStyle font = TypeStyle.BodyMedium;
    label.TextDecorations = null;
    BorderThickness = new Thickness(0);
    Background = Brushes.Transparent;
    if (Amber)
    {
      // The mockup's 1 px accentRing edge: in light mode the fill alone is too faint against white (WCAG 1.4.11).
      SetResourceReference(BackgroundProperty, "accentFill");
      SetResourceReference(BorderBrushProperty, "accentRing");
      BorderThickness = new Thickness(1);
      fg = "onAccent";
    }
    else
    {
      switch (kind)
      {
        case ButtonStyleKind.Secondary:
          SetResourceReference(BackgroundProperty, "raise");
          SetResourceReference(BorderBrushProperty, "line");
          BorderThickness = new Thickness(1);
          break;
        case ButtonStyleKind.Ghost:
          fg = "sub";
          break;
        case ButtonStyleKind.Danger:
          SetResourceReference(BackgroundProperty, "raise");
          SetResourceReference(BorderBrushProperty, "bad");
          BorderThickness = new Thickness(1);
          fg = "bad";
          break;
        case ButtonStyleKind.Link:
          fg = "sub";
          font = TypeStyle.Small;
          label.TextDecorations = TextDecorations.Underline;
          break;
      }
    }
    Ui.Style(label, font);
    label.SetResourceReference(TextBlock.ForegroundProperty, fg);
  }
}

/// SH-20: at most one filled amber button among the visible buttons under a root.
static class AmberRule
{
  /// In the stopped state only the banner's button may be amber. Otherwise a row or empty-state button wins, and the last page
  /// action is amber only when none is visible and the screen is not marked noPrimary.
  public static void Enforce(DependencyObject root, bool stopped, bool noPrimary)
  {
    var all = Ui.All(root).OfType<MButton>().Where((b) => b.Rank > 0).ToList();
    var shown = all.Where((b) => Ui.Shown(b)).ToList();
    MButton? winner = shown.FirstOrDefault((b) => b.Rank == 3);
    if (winner == null && !stopped) winner = shown.FirstOrDefault((b) => b.Rank == 2) ?? (noPrimary ? null : shown.LastOrDefault((b) => b.Rank == 1));
    foreach (MButton b in all) b.SetAmber(ReferenceEquals(b, winner));
  }

  /// The filled amber buttons a person can see under root.
  public static int VisibleAmber(DependencyObject root) => Ui.All(root).OfType<Button>().Count((b) => b.Tag as string == "primary" && Ui.Shown(b));

  /// Whether every filled amber button a person can see under root carries the 1 px accentRing edge of the current theme.
  public static bool Ringed(DependencyObject root)
  {
    Color want = Theme.Current["accentRing"];
    foreach (Button b in Ui.All(root).OfType<Button>().Where((b) => b.Tag as string == "primary" && Ui.Shown(b)))
    {
      if (b.BorderThickness != new Thickness(1) || b.BorderBrush is not SolidColorBrush s || s.Color != want) return false;
      if (b.Background is not SolidColorBrush f || f.Color != Theme.Current["accentFill"]) return false;
    }
    return true;
  }
}

/// A button that is a whole area (a row, the health button, an expander's head). A click on an inner button is that button's.
class ClickArea : Button
{
  public Action? OnPress;

  public ClickArea()
  {
    Template = Ui.PlainTemplate;
    Background = Brushes.Transparent;
    BorderThickness = new Thickness(0);
    HorizontalAlignment = HorizontalAlignment.Stretch;
    HorizontalContentAlignment = HorizontalAlignment.Stretch;
    VerticalContentAlignment = VerticalAlignment.Center;
    Cursor = Cursors.Hand;
    Click += (_, e) => { if (ReferenceEquals(e.OriginalSource, this)) OnPress?.Invoke(); };
  }
}

/// SH-2: the health button, a glyph and the core's health sentence, then the pip strip.
sealed class HealthButton : ClickArea
{
  public readonly Glyph GlyphView = new(GlyphKind.Off);
  public readonly TextBlock Sentence = Ui.Text("Reading Memev.", TypeStyle.Body, "text", 180);
  public readonly StackPanel Pips = new() { Orientation = Orientation.Horizontal };
  bool open;

  public HealthButton()
  {
    Padding = new Thickness(12, 8, 12, 8);
    GlyphView.Margin = new Thickness(0, 6, 0, 0);
    GlyphView.VerticalAlignment = VerticalAlignment.Top;
    var line = Ui.Row(new UIElement[] { GlyphView }, Sentence, Array.Empty<UIElement>(), 8);
    Content = Ui.V(8, line, Pips);
  }

  public bool Open { get => open; set { open = value; if (open) SetResourceReference(BackgroundProperty, "raise"); else Background = Brushes.Transparent; } }
}

/// A menu entry (SH-3): 32 px, radius 8; selected is on raise at weight 500 with a 2 px amber line at its left edge. A needs-you
/// entry carries the warn diamond. Indent is 28 for a project entry under Projects.
sealed class NavButton : ClickArea
{
  public readonly TextBlock Label;
  readonly Border edge = Ui.Box("accent");
  readonly Glyph diamond = new(GlyphKind.Warn);
  bool selected, needsYou;

  public NavButton(string text, double indent = 0)
  {
    Height = 32;
    Label = Ui.Text(text, TypeStyle.Body, "sub");
    edge.Width = 2;
    edge.HorizontalAlignment = HorizontalAlignment.Left;
    edge.Margin = new Thickness(-indent, 6, 0, 6);
    diamond.Visibility = Visibility.Collapsed;
    var row = Ui.Row(new UIElement[] { Label }, null, new UIElement[] { diamond }, 8);
    var g = new Grid();
    g.Children.Add(row);
    g.Children.Add(edge);
    Content = g;
    Padding = new Thickness(12 + indent, 0, 12, 0);
    AutomationProperties.SetName(this, text);
    Paint();
  }

  public string Text { get => Label.Text; set { Label.Text = value; AutomationProperties.SetName(this, value); } }
  public bool Selected { get => selected; set { selected = value; Paint(); } }
  public bool NeedsYou { get => needsYou; set { needsYou = value; diamond.Visibility = value ? Visibility.Visible : Visibility.Collapsed; } }

  void Paint()
  {
    if (selected) SetResourceReference(BackgroundProperty, "raise"); else Background = Brushes.Transparent;
    Ui.Style(Label, selected ? TypeStyle.BodyMedium : TypeStyle.Body);
    Label.SetResourceReference(TextBlock.ForegroundProperty, selected ? "text" : "sub");
    edge.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
  }
}

static class Parts
{
  /// SH-19: "Sample, not built yet", 20 px high on raise.
  public static Border SampleTag()
  {
    var b = Ui.Box("raise", null, 4);
    b.Height = 20;
    b.Padding = new Thickness(8, 0, 8, 0);
    b.HorizontalAlignment = HorizontalAlignment.Left;
    var t = Ui.Text("Sample, not built yet", TypeStyle.Tag, "sub");
    t.VerticalAlignment = VerticalAlignment.Center;
    b.Child = t;
    return b;
  }

  /// SH-17: a 16 by 2 px amber line, 12 px gap, the label in mono 12 in sub, the sample tag, a 1 px rule to the right edge,
  /// and a "Show all (n)" link when the list has more rows.
  public static DockPanel SectionLabel(string label, bool sample = false, MButton? link = null, double lineWidth = 16, string role = "sub")
  {
    var line = Ui.Box("accent");
    line.Width = lineWidth;
    line.Height = 2;
    line.VerticalAlignment = VerticalAlignment.Center;
    var text = Ui.Text(label, TypeStyle.Label, role);
    text.VerticalAlignment = VerticalAlignment.Center;
    var left = new List<UIElement> { line, text };
    if (sample) { var t = SampleTag(); left.Add(t); }
    var rule = Ui.Hairline("rule");
    rule.VerticalAlignment = VerticalAlignment.Center;
    var right = link != null ? new UIElement[] { link } : Array.Empty<UIElement>();
    return Ui.Row(left, rule, right, 12);
  }

  /// SH-15: on a screen with no tabs, a 48 px 2 px amber line, then a 1 px rule to the right edge.
  public static DockPanel HeaderRule()
  {
    var line = Ui.Box("accent");
    line.Width = 48;
    line.Height = 2;
    line.VerticalAlignment = VerticalAlignment.Bottom;
    var rule = Ui.Hairline("rule");
    rule.VerticalAlignment = VerticalAlignment.Bottom;
    return Ui.Row(new UIElement[] { line }, rule, Array.Empty<UIElement>(), 0);
  }
}

sealed class TabItemDef
{
  public required string Id;
  public required string Label;
  public bool NeedsYou;
}

/// SH-16: text-only tabs, 40 px high, 24 px apart, one 1 px rule under the strip; the selected tab is text at weight 500 with a
/// 2 px amber underline. A tab that needs the person carries the warn diamond.
sealed class TabStrip : Grid
{
  readonly StackPanel row = new() { Orientation = Orientation.Horizontal, Height = 40, VerticalAlignment = VerticalAlignment.Top };
  public readonly List<(string Id, ClickArea Button, TextBlock Label)> Buttons = new();
  public Action<string>? OnSelect;

  public TabStrip()
  {
    Height = 41;
    var rule = Ui.Hairline("rule");
    rule.VerticalAlignment = VerticalAlignment.Bottom;
    Children.Add(rule);
    Children.Add(row);
  }

  public void Set(IList<TabItemDef> tabs, string selected)
  {
    row.Children.Clear();
    Buttons.Clear();
    foreach (TabItemDef t in tabs)
    {
      bool on = t.Id == selected;
      var label = Ui.Text(t.Label, on ? TypeStyle.BodyMedium : TypeStyle.Body, on ? "text" : "sub");
      label.VerticalAlignment = VerticalAlignment.Center;
      var diamond = new Glyph(GlyphKind.Warn) { Visibility = t.NeedsYou ? Visibility.Visible : Visibility.Collapsed };
      var under = Ui.Box("accent");
      under.Height = 2;
      under.VerticalAlignment = VerticalAlignment.Bottom;
      under.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
      var g = new Grid { Height = 40 };
      g.Children.Add(Ui.H(6, label, diamond));
      g.Children.Add(under);
      var b = new ClickArea { Content = g, Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left };
      AutomationProperties.SetName(b, t.Label);
      string id = t.Id;
      b.OnPress = () => OnSelect?.Invoke(id);
      Ui.Put(row, b, 24);
      Buttons.Add((t.Id, b, label));
    }
  }

  public IEnumerable<string> Labels => Buttons.Select((b) => b.Label.Text);
}

/// SH-11 to SH-16: title, "What is this?", up to two page actions, the sentence, then the header rule or the tab strip.
sealed class PageHeader : StackPanel
{
  public readonly TextBlock Title = Ui.Text("", TypeStyle.PageTitle, "text");
  public readonly TextBlock Sentence = Ui.Text("", TypeStyle.Sentence, "sub", 640);
  public readonly MButton What = new("What is this?", ButtonStyleKind.Ghost);
  readonly StackPanel actions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
  readonly DockPanel rule = Parts.HeaderRule();
  public readonly TabStrip Tabs = new();

  public PageHeader()
  {
    Orientation = Orientation.Vertical;
    Title.TextTrimming = TextTrimming.CharacterEllipsis;
    var top = Ui.Row(Array.Empty<UIElement>(), Title, new UIElement[] { What, actions }, 12);
    Children.Add(top);
    Sentence.Margin = new Thickness(0, 8, 0, 0);
    Sentence.HorizontalAlignment = HorizontalAlignment.Left;
    Children.Add(Sentence);
    rule.Margin = new Thickness(0, 24, 0, 0);
    Tabs.Margin = new Thickness(0, 24, 0, 0);
    Children.Add(rule);
    Children.Add(Tabs);
  }

  /// Shows the tabs when there are any, else the header rule. At most two actions are kept.
  public void Configure(string title, string sentence, IList<TabItemDef> tabs, string selected, bool showWhat, IEnumerable<MButton> acts)
  {
    Title.Text = title;
    Sentence.Text = sentence;
    Sentence.Visibility = sentence.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    What.Visibility = showWhat ? Visibility.Visible : Visibility.Collapsed;
    actions.Children.Clear();
    foreach (MButton a in acts.Take(2))
    {
      if (a.Parent is Panel p) p.Children.Remove(a);
      a.Margin = new Thickness(0);
      Ui.Put(actions, a, 8);
    }
    rule.Visibility = tabs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    Tabs.Visibility = tabs.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    Tabs.Set(tabs, selected);
  }
}

/// A list on panel with a 1 px line round it, radius 12, padding 4 12; rows inside are split by a 1 px line (1.1).
sealed class ListBoxView : Border
{
  public readonly StackPanel Stack = new();

  public ListBoxView()
  {
    SetResourceReference(BackgroundProperty, "panel");
    SetResourceReference(BorderBrushProperty, "line");
    BorderThickness = new Thickness(1);
    CornerRadius = new CornerRadius(12);
    Padding = new Thickness(12, 4, 12, 4);
    Child = Stack;
  }

  public void Add(UIElement row)
  {
    if (Stack.Children.Count > 0) Stack.Children.Add(Ui.Hairline("line"));
    Stack.Children.Add(row);
  }

  public IEnumerable<RowView> Rows => Stack.Children.OfType<RowView>();
}

/// A list row: at least 44 px high (36 in the detail panel), padding 12, a glyph, a title, a second line, a command line in mono
/// when technical detail is on, a figure on the right and a button. A selected row is on raise with a 1 px inset text ring.
sealed class RowView : ClickArea
{
  public readonly TextBlock TitleLabel, SubLabel, CodeLabel;
  bool selected;

  public RowView(GlyphKind? glyph, string title, string sub = "", string code = "", string figure = "", UIElement? button = null,
                 double minHeight = 44, bool tech = false, double wrap = 0, UIElement? lead = null)
  {
    TitleLabel = Ui.Text(title, TypeStyle.BodyMedium, "text");
    SubLabel = Ui.Text(sub, TypeStyle.Small, "sub");
    CodeLabel = Ui.Text(code, TypeStyle.Code, "dim");
    foreach (TextBlock t in new[] { TitleLabel, SubLabel }) { t.TextWrapping = TextWrapping.Wrap; if (wrap > 0) t.MaxWidth = wrap; }
    SubLabel.Visibility = sub.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    CodeLabel.Visibility = code.Length == 0 || !tech ? Visibility.Collapsed : Visibility.Visible;
    var words = new StackPanel();
    words.Children.Add(TitleLabel);
    SubLabel.Margin = new Thickness(0, 2, 0, 0);
    CodeLabel.Margin = new Thickness(0, 2, 0, 0);
    words.Children.Add(SubLabel);
    words.Children.Add(CodeLabel);
    words.VerticalAlignment = VerticalAlignment.Center;
    var left = new List<UIElement>();
    if (lead != null) left.Add(lead);
    if (glyph is GlyphKind g) left.Add(new Glyph(g));
    var right = new List<UIElement>();
    if (figure.Length > 0)
    {
      var f = Ui.Text(figure, TypeStyle.Body, "text");
      Typography.SetNumeralAlignment(f, FontNumeralAlignment.Tabular);
      f.VerticalAlignment = VerticalAlignment.Center;
      right.Add(f);
    }
    if (button != null) right.Add(button);
    Content = Ui.Row(left, words, right, 12);
    Padding = new Thickness(8, 12, 8, 12);
    MinHeight = minHeight;
    AutomationProperties.SetName(this, title);
    Paint();
  }

  public bool Selected { get => selected; set { selected = value; Paint(); } }

  void Paint()
  {
    if (selected)
    {
      SetResourceReference(BackgroundProperty, "raise");
      SetResourceReference(BorderBrushProperty, "text");
      BorderThickness = new Thickness(1);
    }
    else { Background = Brushes.Transparent; BorderThickness = new Thickness(0); }
  }
}

/// SH-18: a 1 px rule above, a chevron, a title at weight 500 and a summary in sub. Its contents open 24 px indented.
sealed class ExpanderRow : StackPanel
{
  public readonly ClickArea Head = new();
  readonly TextBlock chevron = Ui.Text("›", TypeStyle.Body, "sub");
  public readonly StackPanel Body = new() { Margin = new Thickness(24, 12, 0, 0) };
  bool open;
  public Action? OnToggle;

  public ExpanderRow(string title, string summary)
  {
    chevron.RenderTransformOrigin = new Point(0.5, 0.5);
    chevron.Width = 12;
    chevron.TextAlignment = TextAlignment.Center;
    var t = Ui.Text(title, TypeStyle.BodyMedium, "text");
    var s = Ui.Text(summary, TypeStyle.Body, "sub");
    s.TextTrimming = TextTrimming.CharacterEllipsis;
    Head.Content = Ui.Row(new UIElement[] { chevron, t }, s, Array.Empty<UIElement>(), 12);
    Head.Padding = new Thickness(0, 12, 0, 4);
    AutomationProperties.SetName(Head, title);
    Head.OnPress = () => IsOpen = !IsOpen;
    Children.Add(Ui.Hairline("rule"));
    Children.Add(Head);
    Children.Add(Body);
    Body.Visibility = Visibility.Collapsed;
  }

  public bool IsOpen
  {
    get => open;
    set
    {
      open = value;
      chevron.RenderTransform = new RotateTransform(open ? 90 : 0);
      Body.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
      OnToggle?.Invoke();
    }
  }
}

/// One "Coming later" entry: fixed text that runs no command (SH-22).
sealed record LaterItem(string Title, string Sub, string Why, string Plan, string Source);

static class Later
{
  /// SH-22: an expander "Coming later (n)" with a list of hollow-glyph rows; each opens a panel tagged as a sample.
  public static ExpanderRow Block(IList<LaterItem> items, Action<LaterItem, RowView> open)
  {
    var e = new ExpanderRow($"Coming later ({items.Count})", "not built yet; each opens a sample of what is planned");
    var list = new ListBoxView();
    foreach (LaterItem it in items)
    {
      var r = new RowView(GlyphKind.Off, it.Title, it.Sub);
      r.OnPress = () => open(it, r);
      list.Add(r);
    }
    e.Body.Children.Add(list);
    return e;
  }
}
