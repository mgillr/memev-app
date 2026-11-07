// SPDX-License-Identifier: Apache-2.0
// Each rev2 screen's menu entry, title, tabs and "What is this?" words (native/REV2-SCREENS.md sections 2 and 5), and the fixed
// text of its "Coming later" rows, as native/macos/src/Screens.swift holds them. A sentence is either filled from the core's
// answer or says plainly what is not built yet. No sample figure is shown as real.
using System.Collections.Generic;
using System.Linq;

namespace Memev.Desktop;

sealed class ScreenDef
{
  public required string Id;
  public required string Menu;
  public required string Title;
  public TabItemDef[] Tabs = System.Array.Empty<TabItemDef>();
  /// The sentence for each tab ("" for a screen with no tabs) until the core's answer is in.
  public Dictionary<string, string> Sentences = new();
  /// The words "What is this?" explains, each with a plain sentence.
  public (string Word, string Plain)[] Words = System.Array.Empty<(string, string)>();
  public bool NoPrimary;
}

static class Screens
{
  static TabItemDef T(string id, string label) => new() { Id = id, Label = label };

  static readonly (string, string) WLead = ("Lead", "The AI app that hands out tasks to the others and is the one you talk to.");
  static readonly (string, string) WChecked = ("Checked", "A task counts as checked only when its check passes, not when a bot says it is done.");

  public static readonly ScreenDef[] Nav =
  {
    new() { Id = "overview", Menu = "Overview", Title = "Executive overview", Sentences = new() { [""] = "Reading Memev." }, Words = new[]
    {
      WChecked,
      ("Needs you", "Something only you can decide or fix. Everything else carries on without you."),
      ("Gone quiet", "A bot or agent that has not reported in for a while."),
      WLead,
    } },
    new() { Id = "operations", Menu = "Operations", Title = "Operations", Tabs = new[] { T("workspace", "Workspace"), T("messages", "Messages") },
      Sentences = new()
      {
        ["workspace"] = "The lead's workspace is not built yet. It waits for the core's message filters.",
        ["messages"] = "Messages are not shown here yet. They wait for the core's message filters.",
      },
      Words = new[]
      {
        ("Lead agent", "The AI app that hands out tasks to the others and is the one you talk to."),
        ("Message bus", "How your bots and agents pass messages to each other through Memev on this computer."),
        ("Background runner", "Runs a bot's work while its app is closed."),
        ("When the lead answers", "The lead answers only when its app is open or its background runner is on."),
        ("Where else this conversation is", "The same messages, as your AI apps and the terminal see them."),
        ("Optimised", "A choice of model and effort made for each task. Not built yet."),
        ("Output", "Something a bot produced, such as a page or a report. The file stays in your project folder."),
      }, NoPrimary = true },
    new() { Id = "projects", Menu = "Projects", Title = "Projects", Sentences = new() { [""] = "Reading Memev." }, Words = new[]
    {
      ("Project", "A piece of work with its own plan, tasks and checks."),
      ("Gate", "A point in the plan that has to pass before the next stage starts."),
      WChecked,
      ("Out of date", "A check passed, then what it checked changed. The pass no longer counts until the check runs again."),
    } },
    new() { Id = "bots", Menu = "Bots and agents", Title = "Bots and agents",
      Tabs = new[] { T("team", "Team"), T("connect", "Connect"), T("agents", "Background agents"), T("access", "Access") },
      Sentences = new() { ["team"] = "Reading Memev.", ["connect"] = "Reading Memev.", ["agents"] = "Reading Memev.", ["access"] = "Access keys are not listed here yet." },
      Words = new[]
      {
        ("Bot or agent", "An AI app connected to your memory, with its own seat on the message bus."),
        WLead,
        ("Background agent", "A seat of one of your apps that takes unattended turns when the lead posts it a task, up to a daily limit you set."),
        ("Access key", "What lets one bot reach your memory. Each bot has its own, and you can revoke it."),
        ("Secure link", "The private route that lets AI apps on the web and your other computers reach this one. Off until you switch it on."),
      } },
    new() { Id = "chats", Menu = "Chats", Title = "Chats", Sentences = new() { [""] = "Reading Memev." }, Words = new[]
    {
      ("Chat channel", "A phone chat that reaches your lead: Telegram, WhatsApp, or the z.ai route through Discord or Lark. Each is off until you turn it on."),
      ("Pairing", "Telling Memev which one person, or which one chat, may reach the lead. Anyone else gets no answer."),
      ("Linked device", "How WhatsApp lets a computer use your account, as WhatsApp on a computer does. You can remove it on your phone at any time."),
    } },
    new() { Id = "memory", Menu = "Memory", Title = "Memory",
      Tabs = new[] { T("search", "Search"), T("review", "Review"), T("browse", "Browse"), T("history", "History"), T("health", "Health") },
      Sentences = new()
      {
        ["search"] = "Search everything your AI apps have saved.",
        ["review"] = "Reading Memev.",
        ["browse"] = "Browsing your memories by kind is not built yet.",
        ["history"] = "Every change to your memory is recorded and kept.",
        ["health"] = "Memory health is not measured in this window yet.",
      },
      Words = new[]
      {
        ("Memory", "A note one of your AI apps saved, kept as plain text on this computer."),
        ("Review", "Memories Memev is unsure about. Keep what is still true, drop what is not."),
        ("Claim", "One statement a memory makes, with where it came from."),
        ("Import", "Bringing in chat history you already have. Everything is previewed first, and each import can be undone."),
      } },
    new() { Id = "checked", Menu = "Checked work", Title = "Checked work", Sentences = new() { [""] = "Reading Memev." }, Words = new[]
    {
      ("Contract", "What a piece of work must do, written down before the work starts."),
      ("Check", "A test that shows the contract is met."),
      ("Receipt", "The record that a check passed, kept with the work."),
    } },
    new() { Id = "settings", Menu = "Settings", Title = "Settings",
      Tabs = new[] { T("general", "General"), T("plan", "Plan and computers"), T("storage", "Storage and backup"), T("advanced", "Advanced"), T("help", "Help and support") },
      Sentences = new()
      {
        ["general"] = "",
        ["plan"] = "Reading Memev.",
        ["storage"] = "Reading Memev.",
        ["advanced"] = "For developers, and for actions you rarely need. Each risky action asks first.",
        ["help"] = "Nothing is sent from Memev until you send it.",
      },
      Words = new[]
      {
        ("Plan", "What your licence covers: how many computers, and which features."),
        ("Backup", "A copy of your notes in a private store that you own."),
        ("Technical detail", "Shows the commands and the core's own words behind each screen."),
      } },
  };

  public static readonly ScreenDef Health = new() { Id = "health", Menu = "Health", Title = "Health", Sentences = new() { [""] = "Reading Memev." }, Words = new[]
  {
    ("Check", "One test Memev runs on itself, such as whether the search index is up to date."),
    ("Off by choice", "Something you have left off, such as web access for cloud AI. It is not a fault."),
  } };

  /// A project, opened from the project list. Its title is the project's name.
  public static ScreenDef Project(string name) => new()
  {
    Id = "project", Menu = name, Title = name,
    Tabs = new[] { T("overview", "Overview"), T("plan", "Plan"), T("work", "Work"), T("risks", "Risks and decisions"), T("results", "Results"), T("outputs", "Outputs") },
    Sentences = new()
    {
      ["overview"] = "",
      ["plan"] = "Gates and milestones are not built yet. Nothing in the core reads them.",
      ["work"] = "Tasks are not listed here yet. They wait for the core's work status.",
      ["risks"] = "Risks and decisions are not built yet.",
      ["results"] = "Check results by project are not shown here yet.",
      ["outputs"] = "Outputs are not built yet. Today a finished task records only a fingerprint of what it produced.",
    },
    Words = Nav[2].Words,
  };

  /// Why a tab shows no live rows yet, in one sentence that names what it waits for.
  public static readonly Dictionary<string, string> NotBuilt = new()
  {
    ["operations/workspace"] = "The live agent panes need the core's bus filters (SO-N-15), which are not on main yet. Bots and agents, Team shows the team today.",
    ["operations/messages"] = "Reading messages without marking them read needs the core's unread peek (SO-N-15), which is not on main yet.",
    ["bots/access"] = "The core's key list is text only, so this window does not show it yet. The memev token command lists keys in the terminal.",
    ["memory/browse"] = "The core has no list of memories by kind or by date yet. Search finds any memory by its words.",
    ["memory/history"] = "The change record is kept by the core; this window does not show it yet.",
    ["memory/health"] = "Speed, reach and integrity figures have no command in the core yet, so nothing is measured here.",
    ["settings/advanced"] = "Each of these actions needs a confirmation and a command this window may run. They are not built yet; the memev command does each in the terminal.",
    ["settings/help"] = "Email, problem reports and feature requests are not built yet. Nothing is sent from this window.",
    ["project/plan"] = "Milestones and gates have no reader in the core yet.",
    ["project/work"] = "The task list waits for the core's work status in this window.",
    ["project/risks"] = "No risks or decisions are recorded by the core yet.",
    ["project/results"] = "Results per project wait for the core's contract status in this window.",
    ["project/outputs"] = "Listing, previewing and versioning outputs are planned (SO-A-06 C2 to C5).",
  };

  public static readonly Dictionary<string, LaterItem[]> LaterItems = new()
  {
    ["overview"] = new LaterItem[]
    {
      new("Runs by day", "a week of activity at a glance",
          "The runner keeps the last 1,000 run times for each bot, but its status reports today only. Showing earlier days needs a small addition.", "NOT SCOPED · a small addition to memev runner status", "no code on main"),
      new("Business figures from your bots", "enquiries, replies and orders, each naming the bot that reported it",
          "Bots do not report business figures to Memev yet.", "NOT SCOPED · needs a contract for a metric feed", "no code on main"),
      new("Decisions waiting for you", "a bot asks, you decide, the work continues either way",
          "Work status has an approvals field, but nothing creates an approval yet.", "PLANNED · SO-A-05, SO-C-A AC04", "no code on main"),
      new("This week against last, and cost", "tasks checked week on week, and what the AI cost",
          "Not available yet. Memev keeps no figures from last week and records no spend.", "NOT SCOPED · needs a contract", "no code on main"),
      new("Ask across the business", "a question over everything your bots did, answered with its sources",
          "Planned, with no code on main yet.", "PLANNED · business inference", "no code on main"),
    },
    ["operations"] = new LaterItem[]
    {
      new("Model, effort and usage", "your own choice per agent, with a daily limit",
          "Nothing on main records tokens or cost; the runner stores one command per bot.", "DRAFT · MU-1", "no code on main"),
      new("Outputs and the preview canvas", "a list of what each task produced, with preview, versions and save a copy",
          "Today a finished task records only a fingerprint of what it produced.", "PLANNED · SO-A-06 C2 to C5, SO-C-C AC03", "no code on main"),
      new("Each agent's own terminal", "what a bot types and prints, live",
          "This needs a session-host contract with its own security review.", "NOT SCOPED · needs a contract", "no code on main"),
      new("A lead that knows what you are looking at", "the lead sees the project, task or output you have selected",
          "Planned, with no code on main yet.", "PLANNED · SO-A-08 C2", "no code on main"),
      new("Ask across the business", "a question over everything your bots did, answered with its sources",
          "Planned, with no code on main yet.", "PLANNED · business inference", "no code on main"),
      new("Goal to plan", "describe an outcome; the lead drafts the brief and the plan for your approval",
          "The planner is not on main.", "PLANNED · SO-C-A AC03, SO-REQ-045", "planner absent"),
    },
    ["operations/messages"] = new LaterItem[]
    {
      new("One timeline of messages and work events", "filtered by agent, task, time and kind",
          "Each entry would link to its task or decision.", "PLANNED · SO-C-B AC02", "no code on main"),
    },
    ["bots"] = new LaterItem[]
    {
      new("Pause all bots", "stop every bot's work at once",
          "Today work pause and runner off do this one project or bot at a time.", "NOT SCOPED · needs a contract", "memev work pause <project> · memev runner off <seat>"),
      new("Richer states", "what each bot is doing, not only whether it is live",
          "Planned.", "PLANNED · SO-C-B AC01", "no code on main"),
      new("Capability records per agent", "what each agent is good at",
          "Planned.", "PLANNED · SO-05-B", "no code on main"),
      new("A project lead separate from the overall lead", "one lead per project",
          "Planned.", "PLANNED · SO-L-A", "no code on main"),
    },
    ["projects"] = new LaterItem[]
    {
      new("Goal to plan", "describe an outcome in plain words; Memev drafts the brief and the plan for your approval",
          "The planner is not on main.", "PLANNED · SO-C-A AC03, SO-REQ-045", "planner absent"),
    },
    ["memory"] = new LaterItem[]
    {
      new("Findings", "something an AI noticed that stays short of a fact until confirmed",
          "The finding model is built; nothing reads it yet.", "PLANNED", "no reader on main"),
    },
    ["settings"] = new LaterItem[]
    {
      new("Notifications and a morning summary", "what needs you, which bot went quiet, what was checked overnight",
          "Planned.", "PLANNED · SO-C-C AC05", "no code on main"),
    },
    ["health"] = new LaterItem[]
    {
      new("A guided repair page for every check", "what went wrong, and the one button that fixes it",
          "Today the memev doctor command repairs what it can in the terminal.", "PLANNED · SO-C-C AC04", "memev doctor --fix --yes"),
    },
  };

}
