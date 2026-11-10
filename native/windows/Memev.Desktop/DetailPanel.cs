// SPDX-License-Identifier: Apache-2.0
// SH-21: the 400 px detail panel on the right, as native/macos/src/DetailPanel.swift draws it. Parts in order: title and close,
// the sample tag, the state, why, up to two buttons, facts, groups, and TECHNICAL pinned at the foot when technical detail is on.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Memev.Desktop;

sealed record DetailFact(string Name, string Value);

sealed record DetailRow(string Title, string Sub = "", string Figure = "", GlyphKind? Glyph = null);

sealed record DetailGroup(string Label, IList<DetailRow> Rows);

sealed record DetailButton(string Label, Action Press);

sealed class DetailModel
{
  /// What was clicked; a second click on the same key closes the panel.
  public required string Key;
  public required string Title;
  public bool Sample;
  public (GlyphKind Kind, string Word)? State;
  public string Why = "";
  public List<DetailButton> Buttons = new();
  public List<DetailFact> Facts = new();
  public List<DetailGroup> Groups = new();
  public string Plan = "";
  public string Source = "";
}

sealed class DetailPanel : Border
{
  readonly StackPanel content = new() { Margin = new Thickness(24) };
  readonly StackPanel tech = new() { Margin = new Thickness(24, 16, 24, 24) };
  public readonly MButton Close = new("✕", ButtonStyleKind.Ghost);
  public DetailModel? Model { get; private set; }
  public Action? OnClose;
  public TextBlock? TitleLabel;

  public DetailPanel()
  {
    Width = 400;
    SetResourceReference(BackgroundProperty, "panel");
    AutomationProperties.SetName(Close, "Close details");
    Close.ToolTip = "Close details";
    Close.OnPress = () => OnClose?.Invoke();
    var dock = new DockPanel { LastChildFill = true };
    DockPanel.SetDock(tech, Dock.Bottom);
    dock.Children.Add(tech);
    dock.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
    Child = dock;
  }

  public void Show(DetailModel m, bool technical)
  {
    Model = m;
    content.Children.Clear();
    tech.Children.Clear();
    const double width = 352;
    var title = Ui.Text(m.Title, TypeStyle.PanelTitle, "text", 300);
    TitleLabel = title;
    if (Close.Parent is Panel p) p.Children.Remove(Close);
    Close.Margin = new Thickness(0);
    Ui.Put(content, Ui.Row(Array.Empty<UIElement>(), title, new UIElement[] { Close }, 12), 24);
    if (m.Sample) Ui.Put(content, Parts.SampleTag(), 24);
    if (m.State is (GlyphKind k, string word))
      Ui.Put(content, Ui.H(8, new Glyph(k), Ui.Text(word, TypeStyle.BodyMedium, k == GlyphKind.Bad ? "bad" : "text")), 24);
    if (m.Why.Length > 0) Ui.Put(content, Ui.Text(m.Why, TypeStyle.Body, "sub", width), 24);
    if (m.Buttons.Count > 0)
      Ui.Put(content, Ui.H(8, m.Buttons.Take(2).Select((b) => (UIElement)new MButton(b.Label, ButtonStyleKind.Secondary, onPress: b.Press)).ToArray()), 24);
    if (m.Facts.Count > 0)
    {
      var facts = new StackPanel();
      foreach (DetailFact f in m.Facts)
      {
        var name = Ui.Text(f.Name, TypeStyle.Body, "sub");
        name.Width = 120;
        var row = Ui.Row(new UIElement[] { name }, Ui.Text(f.Value, TypeStyle.Body, "text", width - 132), Array.Empty<UIElement>(), 12);
        row.MinHeight = 36;
        facts.Children.Add(row);
      }
      Ui.Put(content, facts, 24);
    }
    foreach (DetailGroup g in m.Groups)
    {
      var block = Ui.V(4, Parts.SectionLabel(g.Label));
      foreach (DetailRow r in g.Rows) Ui.Put(block, new RowView(r.Glyph, r.Title, r.Sub, figure: r.Figure, minHeight: 36, wrap: 300), 4);
      Ui.Put(content, block, 24);
    }
    bool showTech = technical && (m.Plan.Length > 0 || m.Source.Length > 0);
    tech.Visibility = showTech ? Visibility.Visible : Visibility.Collapsed;
    if (showTech)
    {
      string text = string.Join("\n", new[] { m.Plan, m.Source }.Where((s) => s.Length > 0));
      var copy = new MButton("Copy", ButtonStyleKind.Secondary, onPress: () => { try { Clipboard.SetText(text); } catch (System.Runtime.InteropServices.ExternalException) { } });
      tech.Children.Add(Ui.Hairline("rule"));
      Ui.Put(tech, Ui.Row(new UIElement[] { Ui.Text("TECHNICAL", TypeStyle.Label, "sub") }, null, new UIElement[] { copy }, 12), 8);
      if (m.Plan.Length > 0) Ui.Put(tech, Ui.Text(m.Plan, TypeStyle.CodeBlock, "dim", width), 8);
      if (m.Source.Length > 0) Ui.Put(tech, Ui.Text(m.Source, TypeStyle.CodeBlock, "dim", width), 8);
    }
  }
}
