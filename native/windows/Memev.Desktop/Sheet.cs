// SPDX-License-Identifier: Apache-2.0
// SH-26 and 1.16, as native/macos/src/Sheet.swift draws them: a sheet is a focused form over the window, 600 px wide at most,
// radius 12, padding 24, 72 px from the top, over a scrim. Focus moves in, the rest of the window takes no clicks, Tab stays
// inside, Esc or a click outside closes it. In a sheet the main button is amber; in a confirmation both buttons look the same and
// the main one has a red ring and text.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Memev.Desktop;

sealed class SheetModel
{
  public required string Title;
  public bool Sample { get; set; }
  public string Note = "";
  public List<UIElement> Fields = new();
  public List<DetailRow> Rows = new();
  public string Notice = "";
  public string Source = "";
  /// The main button's label; null for a sheet of rows that has only Close (MEM-S2).
  public string? Main;
  public string? Second { get; set; }
  public bool Confirm;
  public Action<SheetHost> OnMain = (h) => h.Dismiss();
  public Action<SheetHost> OnSecond = (h) => h.Dismiss();
}

/// The confirmation sheets of 1.16. Each names what changes.
enum ConfirmKind { Revoke, Replace, Rmpc, Rmpath, Uninstall, Purge, Stop, Rotate, Compact, Disconnect, Drop, Cancel, Mark, Switchoff }

static class Confirms
{
  public static string Title(ConfirmKind k, string subject) => k switch
  {
    ConfirmKind.Revoke => $"Revoke the key for {subject}?",
    ConfirmKind.Replace => $"Replace the key for {subject}?",
    ConfirmKind.Rmpc => $"Remove {subject}?",
    ConfirmKind.Rmpath => "Remove the memev command from your PATH?",
    ConfirmKind.Uninstall => "Remove Memev?",
    ConfirmKind.Purge => "Also remove the runtime and launcher?",
    ConfirmKind.Stop => "Stop Memev?",
    ConfirmKind.Rotate => "Make a new web-access key?",
    ConfirmKind.Compact => "Tidy old history files?",
    ConfirmKind.Disconnect => $"Disconnect {subject}?",
    ConfirmKind.Drop => $"Drop “{subject}”?",
    ConfirmKind.Cancel => $"Cancel “{subject}”?",
    ConfirmKind.Mark => $"Mark “{subject}” as checked?",
    _ => "Switch off Checked work?",
  };

  public static string Note(ConfirmKind k) => k switch
  {
    ConfirmKind.Revoke => "It stops working at once on this PC. You can issue a new key at any time.",
    ConfirmKind.Replace => "The old key stops working, and its holder needs the new one.",
    ConfirmKind.Rmpc => "It stops reaching your memory, and one computer on your plan comes free. You can add it again later with a new code.",
    ConfirmKind.Rmpath => "Terminals and AI apps can no longer call memev by name until you add it back.",
    ConfirmKind.Uninstall => "This disconnects every app, stops Memev and removes the login service. Your notes stay in ~\\memev.",
    ConfirmKind.Purge => "This does everything Uninstall does, then removes the runtime and launcher as well. To use Memev again you would install it again.",
    ConfirmKind.Stop => "Your AI apps lose their shared memory until it starts again.",
    ConfirmKind.Rotate => "This cuts off anyone holding the old address.",
    ConfirmKind.Compact => "This frees very little disk space and stops Memev for a moment.",
    ConfirmKind.Disconnect => "It loses its shared memory until you connect it again.",
    ConfirmKind.Drop => "It leaves your memory and stops being recalled.",
    ConfirmKind.Cancel => "The task stops here. You give a reason, and it is kept with the task.",
    ConfirmKind.Mark => "The lead only, with evidence, on this computer.",
    _ => "Nothing is held or blocked while it is off.",
  };

  public static string Main(ConfirmKind k) => k switch
  {
    ConfirmKind.Revoke => "Revoke key",
    ConfirmKind.Replace => "Replace key",
    ConfirmKind.Rmpc => "Remove computer",
    ConfirmKind.Rmpath => "Remove command",
    ConfirmKind.Uninstall => "Uninstall",
    ConfirmKind.Purge => "Purge",
    ConfirmKind.Stop => "Stop Memev",
    ConfirmKind.Rotate => "Rotate key",
    ConfirmKind.Compact => "Compact",
    ConfirmKind.Disconnect => "Disconnect",
    ConfirmKind.Drop => "Drop",
    ConfirmKind.Cancel => "Cancel the task",
    ConfirmKind.Mark => "Mark as checked",
    _ => "Switch off",
  };

  public static SheetModel Sheet(ConfirmKind k, string subject, Action<SheetHost> onConfirm) =>
    new() { Title = Title(k, subject), Note = Note(k), Main = Main(k), Confirm = true, OnMain = onConfirm };

  /// A confirmation of the same look for an action the map's table does not list (the three screens of section 5).
  public static SheetModel Custom(string title, string note, string main, string source, Action<SheetHost> onConfirm) =>
    new() { Title = title, Note = note, Main = main, Confirm = true, Source = source, OnMain = onConfirm };
}

/// The scrim and the sheet over the window's content.
sealed class SheetHost : Grid
{
  readonly Border scrim = new();
  readonly Border panel = new();
  readonly StackPanel stack = new();
  public readonly TextBlock Status = Ui.Text("", TypeStyle.Small, "sub", 552);
  /// The core's answer to what the sheet ran, in mono, scrolling inside the sheet.
  public readonly TextBlock Output = Ui.Text("", TypeStyle.CodeBlock, "text", 552);
  readonly ScrollViewer outputScroll;
  public SheetModel? Model { get; private set; }
  public MButton? MainButton { get; private set; }
  public List<MButton> OtherButtons { get; } = new();
  IInputElement? returnFocus;
  public bool Technical;

  public SheetHost()
  {
    Visibility = Visibility.Collapsed;
    scrim.SetResourceReference(Border.BackgroundProperty, "scrim");
    scrim.MouseLeftButtonDown += (_, e) => { e.Handled = true; Dismiss(); };
    panel.SetResourceReference(Border.BackgroundProperty, "panel");
    panel.SetResourceReference(Border.BorderBrushProperty, "line");
    panel.BorderThickness = new Thickness(1);
    panel.CornerRadius = new CornerRadius(12);
    panel.Padding = new Thickness(24);
    panel.MaxWidth = 600;
    panel.Width = 600;
    panel.HorizontalAlignment = HorizontalAlignment.Center;
    panel.VerticalAlignment = VerticalAlignment.Top;
    panel.Margin = new Thickness(24, 72, 24, 24);
    Output.TextWrapping = TextWrapping.Wrap;
    outputScroll = new ScrollViewer { Content = Output, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    var scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    panel.Child = scroll;
    KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Cycle);
    KeyboardNavigation.SetControlTabNavigation(panel, KeyboardNavigationMode.Cycle);
    KeyboardNavigation.SetDirectionalNavigation(panel, KeyboardNavigationMode.Cycle);
    Children.Add(scrim);
    Children.Add(panel);
    Paint();
  }

  public void Paint()
  {
    Theme t = Theme.Current;
    panel.Effect = new DropShadowEffect { Color = Colors.Black, Opacity = t.ShadowOpacity, BlurRadius = t.ShadowRadius, ShadowDepth = t.ShadowY, Direction = 270 };
  }

  public bool IsOpen => Visibility == Visibility.Visible;

  public void Present(SheetModel m)
  {
    bool wasOpen = IsOpen;
    Model = m;
    stack.Children.Clear();
    OtherButtons.Clear();
    const double w = 552;
    Ui.Put(stack, Ui.Text(m.Title, TypeStyle.PanelTitle, "text", w), 16);
    if (m.Sample) Ui.Put(stack, Parts.SampleTag(), 16);
    if (m.Note.Length > 0) Ui.Put(stack, Ui.Text(m.Note, TypeStyle.Body, "sub", w), 16);
    foreach (UIElement f in m.Fields) { if (f is FrameworkElement fe && fe.Parent is Panel p) p.Children.Remove(f); Ui.Put(stack, f, 16); }
    foreach (DetailRow r in m.Rows) Ui.Put(stack, new RowView(r.Glyph, r.Title, r.Sub, figure: r.Figure, minHeight: 36, wrap: 480), 8);
    if (m.Notice.Length > 0)
    {
      var box = Ui.Box("raise", null, 8);
      box.Padding = new Thickness(12);
      box.Child = Ui.Text(m.Notice, TypeStyle.Body, "text", w - 24);
      Ui.Put(stack, box, 16);
    }
    if (m.Source.Length > 0 && Technical) Ui.Put(stack, Ui.Text(m.Source, TypeStyle.CodeBlock, "dim", w), 16);
    Status.Text = "";
    Output.Text = "";
    outputScroll.Visibility = Visibility.Collapsed;
    Status.Margin = new Thickness(0);
    outputScroll.Margin = new Thickness(0);
    Ui.Put(stack, Status, 16);
    Ui.Put(stack, outputScroll, 8);
    var buttons = new List<MButton>();
    var cancel = new MButton(m.Main == null ? "Close" : "Cancel", ButtonStyleKind.Secondary, onPress: Dismiss);
    buttons.Add(cancel);
    if (m.Second != null) { SheetModel mm = m; buttons.Add(new MButton(m.Second, ButtonStyleKind.Secondary, onPress: () => mm.OnSecond(this))); }
    MainButton = null;
    if (m.Main != null)
    {
      SheetModel mm = m;
      MainButton = new MButton(m.Main, m.Confirm ? ButtonStyleKind.Danger : ButtonStyleKind.Secondary, m.Confirm ? 0 : 3, onPress: () => mm.OnMain(this));
      buttons.Add(MainButton);
    }
    OtherButtons.AddRange(buttons.Where((b) => !ReferenceEquals(b, MainButton)));
    var row = Ui.H(8, buttons.ToArray());
    row.HorizontalAlignment = HorizontalAlignment.Right;
    Ui.Put(stack, row, 16);
    if (!wasOpen) returnFocus = Keyboard.FocusedElement;
    Visibility = Visibility.Visible;
    // A sheet has its own amber rule: its main button, never in a confirmation.
    AmberRule.Enforce(stack, false, m.Confirm);
    Dispatcher.BeginInvoke(new Action(() => { var first = Ui.All(stack).OfType<Control>().FirstOrDefault((c) => c.Focusable && c.IsEnabled && c is not ClickArea); first?.Focus(); }),
                           System.Windows.Threading.DispatcherPriority.Input);
  }

  /// Shows the core's answer under the status line.
  public void ShowOutput(string text)
  {
    Output.Text = text.Trim();
    outputScroll.Visibility = Output.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
  }

  public void Dismiss()
  {
    Visibility = Visibility.Collapsed;
    stack.Children.Clear();
    Model = null;
    MainButton = null;
    OtherButtons.Clear();
    returnFocus?.Focus();
    returnFocus = null;
  }

  public StackPanel Stack => stack;
}
