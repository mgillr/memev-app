// SPDX-License-Identifier: Apache-2.0
// Chats (REV2-SCREENS.md 5.6): talk with the lead from a phone, through the core's chat bridge. Each channel shows its state
// from `chat status --json`; set-up, pairing and linking run after their sheets, a token goes to the core in the environment and
// never as an argument, WhatsApp turns on only after the person ticks its risk line, and turning a channel off asks first.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Memev.Reader;

namespace Memev.Desktop;

/// One channel as `chat status --json` prints it.
sealed record ChatRow(string Channel, bool On, bool Running, string Seat, string LastIn, string LastOut, string Error, bool Enabled, bool Linked, bool SetUp, bool Paired, string Bot, bool Available);

static class Chats
{
  public const string WhatsAppWarning = "This uses an unofficial WhatsApp connection. WhatsApp's terms do not allow it, and WhatsApp could suspend this number. Memev only messages you, in your own chat.";

  public static (List<ChatRow> Rows, bool Read) Read()
  {
    var rows = new List<ChatRow>();
    if (CoreCall.Json(new[] { "chat", "status", "--json" }, 15000) is not JsonObject o || o["channels"] is not JsonArray a) return (rows, false);
    foreach (JsonObject c in a.OfType<JsonObject>())
    {
      string ch = J.Str(c["channel"]);
      if (ch.Length == 0) continue;
      rows.Add(new ChatRow(ch, J.True(c["on"]), J.True(c["running"]), J.Str(c["seat"]), Paths.Show(J.Str(c["last_in"])), Paths.Show(J.Str(c["last_out"])), Paths.Show(J.Str(c["error"])),
                           J.True(c["enabled"]), J.True(c["linked"]), J.True(c["set_up"]), J.True(c["paired"]), J.Str(c["bot"]), c["available"] is null || J.True(c["available"])));
    }
    return (rows, true);
  }

  public static string Name(string ch) => ch switch { "telegram" => "Telegram", "whatsapp" => "WhatsApp", "discord" => "Discord", "lark" => "Lark", _ => ch };

  public static string State(ChatRow c)
  {
    if (!c.On) return "off";
    string s = c.Channel == "whatsapp" ? (c.Linked ? "linked" : c.Enabled ? "on, not linked yet" : "not turned on") : c.Paired ? "paired" : c.SetUp ? "set up, not paired" : "on";
    if (c.Running) s += " · running";
    if (c.LastIn.Length > 0) s += $" · last in {c.LastIn}";
    if (c.LastOut.Length > 0) s += $" · last out {c.LastOut}";
    if (c.Error.Length > 0) s += $" · {c.Error}";
    return s;
  }

  public static GlyphKind Glyph(ChatRow c) => !c.On ? GlyphKind.Off : c.Error.Length > 0 ? GlyphKind.Warn : (c.Paired || c.Linked) && c.Running ? GlyphKind.Ok : GlyphKind.Live;

  /// A Discord id: a long number.
  public static bool Snowflake(string s) => s.Length >= 15 && s.Length <= 22 && s.All(char.IsAsciiDigit);

  /// The core's answer to a --json chat command: the last JSON object it printed.
  public static JsonObject? Answer(string output)
  {
    JsonObject? last = null;
    foreach (string line in output.Split('\n'))
    {
      string t = line.Trim();
      if (!t.StartsWith('{')) continue;
      try { if (JsonNode.Parse(t) is JsonObject o) last = o; } catch (JsonException) { }
    }
    return last;
  }

  /// A QR symbol as a picture: dark on white whatever the theme, with the four-module quiet zone, drawn with no smoothing.
  public static Image Picture(string text, double size = 240)
  {
    bool[,] m = Qr.Matrix(text);
    int n = m.GetLength(0), q = 4, w = n + 2 * q;
    var px = new byte[w * w];
    for (int i = 0; i < px.Length; i++) px[i] = 255;
    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) if (m[y, x]) px[(y + q) * w + x + q] = 0;
    var bmp = BitmapSource.Create(w, w, 96, 96, PixelFormats.Gray8, null, px, w);
    bmp.Freeze();
    var img = new Image { Source = bmp, Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Left, Tag = n };
    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
    AutomationProperties.SetName(img, "QR code");
    return img;
  }
}

sealed partial class MainWindow
{
  void BuildChats(StackPanel v, PageData d)
  {
    if (!d.ChatsRead) { Put(Block("CHATS", new UIElement[] { Note("The chat status did not answer.") }, source: "memev chat status --json"), v); return; }
    ChatRow Row(string ch) => d.Chats.FirstOrDefault((c) => c.Channel == ch) ?? new ChatRow(ch, false, false, "", "", "", "", false, false, false, false, "", true);
    RowView Line(ChatRow c, params MButton?[] buttons)
    {
      var bs = buttons.Where((b) => b != null).Cast<MButton>().ToArray();
      UIElement? group = bs.Length == 0 ? null : Ui.H(8, bs);
      var r = new RowView(Chats.Glyph(c), Chats.Name(c.Channel), Chats.State(c), technical && c.Seat.Length > 0 ? $"seat {c.Seat}" : "", button: group, tech: technical);
      r.OnPress = () => OpenDetail(new DetailModel
      {
        Key = $"chat:{c.Channel}", Title = Chats.Name(c.Channel), State = (Chats.Glyph(c), Chats.State(c)),
        Why = "What you write in this chat reaches your lead as a message to read, never as a command. Only the lead's answers to this chat come back.",
        Facts = { new("Seat", c.Seat.Length > 0 ? c.Seat : "none yet"), new("Running", c.Running ? "yes" : "no") },
        Source = "memev chat status --json",
      }, r);
      return r;
    }
    MButton Off(ChatRow c) => new("Turn off", ButtonStyleKind.Secondary, onPress: () => ChatOffSheet(c.Channel));

    var tg = Row("telegram");
    var tl = new ListBoxView();
    tl.Add(Line(tg, tg.SetUp ? null : new MButton("Set up", ButtonStyleKind.Secondary, onPress: TelegramSetupSheet),
                tg.SetUp ? new MButton(tg.Paired ? "Pair again" : "Pair", ButtonStyleKind.Secondary, onPress: TelegramPair) : null, tg.On ? Off(tg) : null));
    Put(Block("TELEGRAM", new UIElement[] { tl, Note("Recommended: Telegram's own bot route, within its terms. Only the one person who pairs is accepted.") }, source: "memev chat telegram setup | pair | disable --json"), v);

    var wa = Row("whatsapp");
    var warn = Ui.Box("raise", null, 8);
    warn.Padding = new Thickness(12);
    warn.Child = Ui.Text(Chats.WhatsAppWarning, TypeStyle.Body, "text", 616);
    var wl = new ListBoxView();
    wl.Add(Line(wa, wa.Enabled ? null : new MButton("Turn on", ButtonStyleKind.Secondary, onPress: WhatsAppOnSheet),
                wa.Enabled && !wa.Linked ? new MButton("Link", ButtonStyleKind.Secondary, onPress: WhatsAppLinkSheet) : null, wa.On ? Off(wa) : null));
    Put(Block("WHATSAPP", new UIElement[] { warn, wl }, source: "memev chat whatsapp enable --accept-risk | link | disable --json"), v);

    var dc = Row("discord");
    var lk = Row("lark");
    var zl = new ListBoxView();
    zl.Add(Line(dc, !dc.On && !lk.On ? new MButton("Set up", ButtonStyleKind.Secondary, onPress: DiscordSetupSheet) : null, dc.On ? Off(dc) : null));
    var lr = Line(lk, lk.On ? Off(lk) : null);
    if (!lk.On) lr.SubLabel.Text = "off · the core keeps Lark settings, but its connection is not built yet, so it is not set up here";
    zl.Add(lr);
    Put(Block("Z.AI (DISCORD OR LARK)", new UIElement[] { zl, Note("Discord and Lark are two routes for z.ai and share one seat, so only one is on at a time.") }, source: "memev chat discord setup | pair | disable --json · memev chat lark disable --json"), v);
    Put(Note("Every message from a phone reaches the lead as data to read, never as a command."), v, false);
  }

  // ---- sheets

  void ChatDone(string title, int code, string output, string ok)
  {
    var a = Chats.Answer(output);
    bool good = code == 0 && (a == null || a["ok"] is null || J.True(a["ok"]));
    string msg = a != null && !good ? Paths.Show(J.Str(a["message"])) : "";
    Present(new SheetModel { Title = good ? title : $"{title}: not done", Note = good ? ok : code == 126 ? Refused : msg.Length > 0 ? msg : code == -1 ? "The core did not answer in time." : $"The core stopped with code {code}.", Main = "Close" });
    _ = LoadPage("chats");
  }

  static PasswordBox Secret(string name)
  {
    var p = new PasswordBox { Width = 420, Height = 32, HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
    AutomationProperties.SetName(p, name);
    return p;
  }

  static TextBox Field(string name, double width = 320)
  {
    var t = new TextBox { Width = width, Height = 32, HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
    AutomationProperties.SetName(t, name);
    return t;
  }

  static UIElement Labelled(string label, string hint, Control c) => Ui.V(4, Ui.Text(label, TypeStyle.BodyMedium, "text"), Ui.Text(hint, TypeStyle.Small, "sub"), c);

  void TelegramSetupSheet()
  {
    var token = Secret("Bot token");
    Present(new SheetModel
    {
      Title = "Set up Telegram",
      Note = "In Telegram, open @BotFather, send /newbot and follow its steps. Paste the token it gives you. Memev checks it with Telegram and keeps it on this computer only.",
      Fields = { Labelled("Bot token", "hidden as you type; it never goes on a command line", token) },
      Source = "MEMEV_TELEGRAM_TOKEN=… memev chat telegram setup --json", Main = "Set up",
      OnMain = (h) =>
      {
        string t = token.Password.Trim();
        if (t.Length < 20 || t.Any(char.IsWhiteSpace)) { h.Status.Text = "Paste the whole token BotFather sent you."; return; }
        RunInSheet(h, "Checking the token with Telegram.", new[] { "chat", "telegram", "setup", "--json" }, 60000,
          (code, output) => ChatDone("Telegram is set up", code, output, "Next: Pair, to say which phone may reach your lead."), new Dictionary<string, string> { ["MEMEV_TELEGRAM_TOKEN"] = t }, raw: true);
      },
    });
  }

  void TelegramPair()
  {
    Present(new SheetModel { Title = "Pair your phone", Note = "Asking Memev for a one-time link.", Source = "memev chat telegram pair --json" });
    RunInSheet(sheet, "Making a one-time link.", new[] { "chat", "telegram", "pair", "--json" }, 60000, (code, output) =>
    {
      var a = Chats.Answer(output);
      string link = J.Str(a?["link"]);
      if (code != 0 || link.Length == 0 || !link.StartsWith("https://t.me/", StringComparison.Ordinal)) { ChatDone("Pair your phone", code == 0 ? 1 : code, output, ""); return; }
      ShowCodeSheet("Pair your phone", "Open it on your phone within 10 minutes and press Start. Only that one person is accepted, and only in their private chat with your bot.", link, true);
    }, raw: true);
  }

  /// A sheet with a QR code and its text; Copy copies a link, never a WhatsApp code.
  Image? ShowCodeSheet(string title, string note, string text, bool copy)
  {
    Image? img = null;
    var fields = new List<UIElement>();
    try { img = Chats.Picture(text); fields.Add(img); } catch (ArgumentException) { fields.Add(Note("This code is too long to draw here.")); }
    if (copy) fields.Add(Ui.Text(text, TypeStyle.CodeBlock, "text", 552));
    Present(new SheetModel
    {
      Title = title, Note = note, Fields = fields, Main = copy ? "Done" : null, Second = copy ? "Copy the link" : null,
      OnSecond = (h) => { try { Clipboard.SetText(text); h.Status.Text = "Copied."; } catch (System.Runtime.InteropServices.ExternalException) { h.Status.Text = "The clipboard is busy; try again."; } },
      OnMain = (h) => { h.Dismiss(); _ = LoadPage("chats"); },
    });
    return img;
  }

  void WhatsAppOnSheet()
  {
    var tick = new CheckBox { Content = "I understand that WhatsApp could suspend this number", IsChecked = false };
    tick.SetResourceReference(ForegroundProperty, "text");
    AutomationProperties.SetName(tick, "I understand that WhatsApp could suspend this number");
    var warn = Ui.Box("raise", null, 8);
    warn.Padding = new Thickness(12);
    warn.Child = Ui.Text(Chats.WhatsAppWarning, TypeStyle.Body, "text", 528);
    Present(new SheetModel
    {
      Title = "Turn on WhatsApp?",
      Note = "Memev installs the pinned WhatsApp library on this computer and links to your account as a linked device. It writes only to your own “Message yourself” chat, and reads only what you write there.",
      Fields = { warn, tick }, Source = "memev chat whatsapp enable --accept-risk --json", Main = "Turn on",
      OnMain = (h) =>
      {
        if (tick.IsChecked != true) { h.Status.Text = "Tick the line above first."; return; }
        RunInSheet(h, "Installing the WhatsApp connection. This can take a minute.", new[] { "chat", "whatsapp", "enable", "--accept-risk", "--json" }, 600000,
          (code, output) => ChatDone("WhatsApp is on", code, output, "Next: Link, and scan the code with your phone."), raw: true);
      },
    });
    if (sheet.MainButton is MButton main)
    {
      main.IsEnabled = false;
      tick.Checked += (_, _) => main.IsEnabled = true;
      tick.Unchecked += (_, _) => main.IsEnabled = false;
    }
  }

  CancellationTokenSource? linkCancel;

  void WhatsAppLinkSheet()
  {
    var phone = Field("Or link with your phone number", 240);
    Present(new SheetModel
    {
      Title = "Link WhatsApp",
      Note = "Memev shows a code for your phone. Leave the number empty for a QR code to scan; with your number, Memev shows an 8-character code to type in instead.",
      Fields = { Labelled("Or link with your phone number", "optional: the number with its country code, digits only", phone) },
      Source = "memev chat whatsapp link [--phone N] --json", Main = "Show the code",
      OnMain = (h) =>
      {
        string p = phone.Text.Trim().TrimStart('+');
        if (p.Length > 0 && (p.Length < 7 || p.Length > 15 || !p.All(char.IsAsciiDigit))) { h.Status.Text = "A phone number is digits only, with its country code."; return; }
        StartLink(p);
      },
    });
  }

  void StartLink(string phone)
  {
    linkCancel?.Cancel();
    var cancel = new CancellationTokenSource();
    linkCancel = cancel;
    var args = new List<string> { "chat", "whatsapp", "link", "--json" };
    if (phone.Length > 0) args.AddRange(new[] { "--phone", phone });
    var holder = Ui.V(12, Note("Waiting for the code."));
    Present(new SheetModel
    {
      Title = "Link WhatsApp",
      Note = "On your phone: WhatsApp > Settings > Linked devices > Link a device. On Android, Settings is under the ⋮ menu. The code changes every few seconds; this shows the newest.",
      Fields = { holder }, Source = "memev " + string.Join(' ', args),
    });
    bool linked = false;
    string? bin = Core.ResolveBin();
    if (bin == null) { holder.Children.Clear(); holder.Children.Add(Note("The memev command was not found.")); return; }
    Task.Run(() => Core.Stream(bin, args.ToArray(), 5 * 60 * 1000, (line) =>
    {
      var a = Chats.Answer(line);
      if (a == null) return;
      Dispatcher.BeginInvoke(new Action(() =>
      {
        if (cancel.IsCancellationRequested || !sheet.IsOpen || sheet.Model?.Title != "Link WhatsApp") { cancel.Cancel(); return; }
        holder.Children.Clear();
        if (J.True(a["linked"])) { linked = true; return; }
        if (J.Str(a["qr"]) is { Length: > 0 } qr) { try { holder.Children.Add(Chats.Picture(qr)); } catch (ArgumentException) { holder.Children.Add(Note("This code is too long to draw here.")); } }
        else if (J.Str(a["pairing_code"]) is { Length: > 0 } code) Ui.Put(holder, Ui.V(4, Note("Choose “Link with phone number instead”, then type:"), Ui.Text(code, TypeStyle.PanelTitle, "text")), 0);
        else if (a["ok"] is not null && !J.True(a["ok"])) holder.Children.Add(Note(Paths.Show(J.Str(a["message"]))));
      }));
    }, cancel.Token)).ContinueWith((t) =>
    {
      int code = t.Status == TaskStatus.RanToCompletion ? t.Result : -1;
      if (cancel.IsCancellationRequested && !linked) return;
      if (linked || code == 0) Present(new SheetModel { Title = "WhatsApp is linked", Note = "Write to yourself in WhatsApp (“Message yourself”) to reach your lead. Its answers appear in the same chat.", Main = "Close" });
      else Present(new SheetModel { Title = "WhatsApp was not linked", Note = code == -1 ? "No phone linked within five minutes. Press Link to try again." : $"The core stopped with code {code}.", Main = "Close" });
      _ = LoadPage("chats");
    }, TaskScheduler.FromCurrentSynchronizationContext());
  }

  void DiscordSetupSheet()
  {
    var token = Secret("Bot token");
    var guild = Field("Server id");
    var channel = Field("Channel id");
    var zai = Field("z.ai bot user id");
    var person = Field("Your user id");
    Present(new SheetModel
    {
      Title = "Set up the Discord route",
      Note = "Memev runs your own Discord bot in one private channel of your server, and you add the z.ai bot to that channel. Turn on Developer Mode in Discord (Settings > Advanced) and use Copy ID for each id. docs/CHAT.md in the core has the steps.",
      Fields =
      {
        Labelled("Bot token", "from the Developer Portal, with Message Content Intent on; hidden as you type", token),
        Labelled("Server id", "a long number", guild), Labelled("Channel id", "the private channel", channel),
        Labelled("z.ai bot user id", "a long number", zai), Labelled("Your user id", "optional", person),
      },
      Source = "MEMEV_DISCORD_TOKEN=… memev chat discord setup --json; memev chat discord pair --guild G --channel C --zai Z [--person P] --json", Main = "Set up",
      OnMain = (h) =>
      {
        string t = token.Password.Trim(), g = guild.Text.Trim(), c = channel.Text.Trim(), z = zai.Text.Trim(), p = person.Text.Trim();
        if (t.Length < 20 || t.Any(char.IsWhiteSpace)) { h.Status.Text = "Paste the whole bot token."; return; }
        if (!Chats.Snowflake(g) || !Chats.Snowflake(c) || !Chats.Snowflake(z) || (p.Length > 0 && !Chats.Snowflake(p))) { h.Status.Text = "Each id is a long number: use Copy ID in Discord."; return; }
        RunInSheet(h, "Checking the token with Discord.", new[] { "chat", "discord", "setup", "--json" }, 60000, (code, output) =>
        {
          var a = Chats.Answer(output);
          if (code != 0 || (a?["ok"] is not null && !J.True(a["ok"]))) { ChatDone("The Discord route", code == 0 ? 1 : code, output, ""); return; }
          var pair = new List<string> { "chat", "discord", "pair", "--guild", g, "--channel", c, "--zai", z };
          if (p.Length > 0) pair.AddRange(new[] { "--person", p });
          pair.Add("--json");
          RunInSheet(h, "Pairing the channel.", pair.ToArray(), 30000, (c2, o2) => ChatDone("The Discord route is set up", c2, o2, "The z.ai bot and you can reach your lead in that channel."), raw: true);
        }, new Dictionary<string, string> { ["MEMEV_DISCORD_TOKEN"] = t }, raw: true);
      },
    });
  }

  void ChatOffSheet(string channel)
  {
    string name = Chats.Name(channel);
    var (title, note) = channel switch
    {
      "telegram" => ("Turn off Telegram?", "Memev forgets the bot token and the paired person. Nobody reaches the lead through Telegram until you set it up again."),
      "whatsapp" => ("Turn off WhatsApp?", "Memev logs this computer out of WhatsApp and removes everything it kept here."),
      _ => ($"Turn off the {name} route?", "z.ai no longer reaches your lead through it, and its settings on this computer are removed."),
    };
    Present(Confirms.Custom(title, note, "Turn off", $"memev chat {channel} disable --json",
      (h) => RunInSheet(h, $"Turning off {name}.", new[] { "chat", channel, "disable", "--json" }, 60000,
        (code, output) => ChatDone($"{name} is off", code, output, "Everything it kept on this computer is removed."), raw: true)));
  }
}
