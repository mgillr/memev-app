// SPDX-License-Identifier: Apache-2.0
// Each rev2 screen's menu entry, title, tabs and "What is this?" words (native/REV2-SCREENS.md section 2), and the fixed text of
// its "Coming later" rows. The mockup's sentences carry sample figures; here a sentence is either filled from the core's answer
// or says plainly what is not built yet. No sample figure is shown as real.
import AppKit

struct ScreenDef {
  let id: String
  let menu: String
  let title: String
  let tabs: [TabItem]
  /// The sentence for each tab ("" for a screen with no tabs) until the core's answer is in.
  let sentences: [String: String]
  /// The words "What is this?" explains, each with a plain sentence. The mockup's own wording is not in this repository; these
  /// sentences are this app's.
  let words: [(String, String)]
  var noPrimary = false
}

let W_LEAD = ("Lead", "The AI app that hands out tasks to the others and is the one you talk to.")
let W_CHECKED = ("Checked", "A task counts as checked only when its check passes, not when a bot says it is done.")

let NAV: [ScreenDef] = [
  ScreenDef(id: "overview", menu: "Overview", title: "Executive overview", tabs: [], sentences: ["": "Reading Memev."], words: [
    W_CHECKED,
    ("Needs you", "Something only you can decide or fix. Everything else carries on without you."),
    ("Gone quiet", "A bot or agent that has not reported in for a while."),
    W_LEAD,
  ]),
  ScreenDef(id: "operations", menu: "Operations", title: "Operations",
            tabs: [TabItem(id: "workspace", label: "Workspace"), TabItem(id: "messages", label: "Messages")],
            sentences: [
              "workspace": "The lead's workspace is not built yet. It waits for the core's message filters.",
              "messages": "Messages are not shown here yet. They wait for the core's message filters.",
            ],
            words: [
              ("Lead agent", "The AI app that hands out tasks to the others and is the one you talk to."),
              ("Message bus", "How your bots and agents pass messages to each other through Memev on this computer."),
              ("Background runner", "Runs a bot's work while its app is closed."),
              ("When the lead answers", "The lead answers only when its app is open or its background runner is on."),
              ("Where else this conversation is", "The same messages, as your AI apps and the terminal see them."),
              ("Optimised", "A choice of model and effort made for each task. Not built yet."),
              ("Output", "Something a bot produced, such as a page or a report. The file stays in your project folder."),
            ], noPrimary: true),
  ScreenDef(id: "projects", menu: "Projects", title: "Projects", tabs: [], sentences: ["": "Reading Memev."], words: [
    ("Project", "A piece of work with its own plan, tasks and checks."),
    ("Gate", "A point in the plan that has to pass before the next stage starts."),
    W_CHECKED,
    ("Out of date", "A check passed, then what it checked changed. The pass no longer counts until the check runs again."),
  ]),
  ScreenDef(id: "bots", menu: "Bots and agents", title: "Bots and agents",
            tabs: [TabItem(id: "team", label: "Team"), TabItem(id: "connect", label: "Connect"), TabItem(id: "access", label: "Access")],
            sentences: ["team": "Reading Memev.", "connect": "Reading Memev.", "access": "Access keys are not listed here yet."],
            words: [
              ("Bot or agent", "An AI app connected to your memory, with its own seat on the message bus."),
              W_LEAD,
              ("Access key", "What lets one bot reach your memory. Each bot has its own, and you can revoke it."),
              ("Secure link", "The private route that lets AI apps on the web and your other computers reach this one. Off until you switch it on."),
            ]),
  ScreenDef(id: "memory", menu: "Memory", title: "Memory",
            tabs: [TabItem(id: "search", label: "Search"), TabItem(id: "review", label: "Review"), TabItem(id: "browse", label: "Browse"),
                   TabItem(id: "history", label: "History"), TabItem(id: "health", label: "Health")],
            sentences: [
              "search": "Search everything your AI apps have saved.",
              "review": "Reading Memev.",
              "browse": "Browsing your memories by kind is not built yet.",
              "history": "Every change to your memory is recorded and kept.",
              "health": "Memory health is not measured in this window yet.",
            ],
            words: [
              ("Memory", "A note one of your AI apps saved, kept as plain text on this computer."),
              ("Review", "Memories Memev is unsure about. Keep what is still true, drop what is not."),
              ("Claim", "One statement a memory makes, with where it came from."),
            ]),
  ScreenDef(id: "checked", menu: "Checked work", title: "Checked work", tabs: [], sentences: ["": "Reading Memev."], words: [
    ("Contract", "What a piece of work must do, written down before the work starts."),
    ("Check", "A test that shows the contract is met."),
    ("Receipt", "The record that a check passed, kept with the work."),
  ]),
  ScreenDef(id: "settings", menu: "Settings", title: "Settings",
            tabs: [TabItem(id: "general", label: "General"), TabItem(id: "plan", label: "Plan and computers"),
                   TabItem(id: "storage", label: "Storage and backup"), TabItem(id: "advanced", label: "Advanced"),
                   TabItem(id: "help", label: "Help and support")],
            sentences: [
              "general": "",
              "plan": "Reading Memev.",
              "storage": "Reading Memev.",
              "advanced": "For developers, and for actions you rarely need. Each risky action asks first.",
              "help": "Nothing is sent from Memev until you send it.",
            ],
            words: [
              ("Plan", "What your licence covers: how many computers, and which features."),
              ("Backup", "A copy of your notes in a private store that you own."),
              ("Technical detail", "Shows the commands and the core's own words behind each screen."),
            ]),
]

let HEALTH = ScreenDef(id: "health", menu: "Health", title: "Health", tabs: [], sentences: ["": "Reading Memev."], words: [
  ("Check", "One test Memev runs on itself, such as whether the search index is up to date."),
  ("Off by choice", "Something you have left off, such as web access for cloud AI. It is not a fault."),
])

/// A project, opened from the project list. Its title is the project's name.
func projectDef(_ name: String) -> ScreenDef {
  ScreenDef(id: "project", menu: name, title: name,
            tabs: [TabItem(id: "overview", label: "Overview"), TabItem(id: "plan", label: "Plan"), TabItem(id: "work", label: "Work"),
                   TabItem(id: "risks", label: "Risks and decisions"), TabItem(id: "results", label: "Results"), TabItem(id: "outputs", label: "Outputs")],
            sentences: [
              "overview": "",
              "plan": "Gates and milestones are not built yet. Nothing in the core reads them.",
              "work": "Tasks are not listed here yet. They wait for the core's work status.",
              "risks": "Risks and decisions are not built yet.",
              "results": "Check results by project are not shown here yet.",
              "outputs": "Outputs are not built yet. Today a finished task records only a fingerprint of what it produced.",
            ],
            words: NAV[2].words)
}

/// Why a tab shows no live rows yet, in one sentence that names what it waits for.
let NOT_BUILT: [String: String] = [
  "operations/workspace": "The live agent panes need the core's bus filters (SO-N-15), which are not on main yet. Bots and agents, Team shows the team today.",
  "operations/messages": "Reading messages without marking them read needs the core's unread peek (SO-N-15), which is not on main yet.",
  "bots/access": "The core's key list is text only, so this window does not show it yet. The memev token command lists keys in the terminal.",
  "memory/browse": "The core has no list of memories by kind or by date yet. Search finds any memory by its words.",
  "memory/history": "The change record is kept by the core; this window does not show it yet.",
  "memory/health": "Speed, reach and integrity figures have no command in the core yet, so nothing is measured here.",
  "settings/advanced": "Each of these actions needs a confirmation and a command this window may run. They are not built yet; the memev command does each in the terminal.",
  "settings/help": "Email, problem reports and feature requests are not built yet. Nothing is sent from this window.",
  "project/plan": "Milestones and gates have no reader in the core yet.",
  "project/work": "The task list waits for the core's work status in this window.",
  "project/risks": "No risks or decisions are recorded by the core yet.",
  "project/results": "Results per project wait for the core's contract status in this window.",
  "project/outputs": "Listing, previewing and versioning outputs are planned (SO-A-06 C2 to C5).",
]

let LATER: [String: [LaterItem]] = [
  "overview": [
    LaterItem(title: "Runs by day", sub: "a week of activity at a glance",
              why: "The runner keeps the last 1,000 run times for each bot, but its status reports today only. Showing earlier days needs a small addition.",
              plan: "NOT SCOPED · a small addition to memev runner status", source: "no code on main"),
    LaterItem(title: "Business figures from your bots", sub: "enquiries, replies and orders, each naming the bot that reported it",
              why: "Bots do not report business figures to Memev yet.", plan: "NOT SCOPED · needs a contract for a metric feed", source: "no code on main"),
    LaterItem(title: "Decisions waiting for you", sub: "a bot asks, you decide, the work continues either way",
              why: "Work status has an approvals field, but nothing creates an approval yet.", plan: "PLANNED · SO-A-05, SO-C-A AC04", source: "no code on main"),
    LaterItem(title: "This week against last, and cost", sub: "tasks checked week on week, and what the AI cost",
              why: "Not available yet. Memev keeps no figures from last week and records no spend.", plan: "NOT SCOPED · needs a contract", source: "no code on main"),
    LaterItem(title: "Ask across the business", sub: "a question over everything your bots did, answered with its sources",
              why: "Planned, with no code on main yet.", plan: "PLANNED · business inference", source: "no code on main"),
  ],
  "operations": [
    LaterItem(title: "Model, effort and usage", sub: "your own choice per agent, with a daily limit",
              why: "Nothing on main records tokens or cost; the runner stores one command per bot.", plan: "DRAFT · MU-1", source: "no code on main"),
    LaterItem(title: "Outputs and the preview canvas", sub: "a list of what each task produced, with preview, versions and save a copy",
              why: "Today a finished task records only a fingerprint of what it produced.", plan: "PLANNED · SO-A-06 C2 to C5, SO-C-C AC03", source: "no code on main"),
    LaterItem(title: "Each agent's own terminal", sub: "what a bot types and prints, live",
              why: "This needs a session-host contract with its own security review.", plan: "NOT SCOPED · needs a contract", source: "no code on main"),
    LaterItem(title: "A lead that knows what you are looking at", sub: "the lead sees the project, task or output you have selected",
              why: "Planned, with no code on main yet.", plan: "PLANNED · SO-A-08 C2", source: "no code on main"),
    LaterItem(title: "Ask across the business", sub: "a question over everything your bots did, answered with its sources",
              why: "Planned, with no code on main yet.", plan: "PLANNED · business inference", source: "no code on main"),
    LaterItem(title: "Goal to plan", sub: "describe an outcome; the lead drafts the brief and the plan for your approval",
              why: "The planner is not on main.", plan: "PLANNED · SO-C-A AC03, SO-REQ-045", source: "planner absent"),
  ],
  "operations/messages": [
    LaterItem(title: "One timeline of messages and work events", sub: "filtered by agent, task, time and kind",
              why: "Each entry would link to its task or decision.", plan: "PLANNED · SO-C-B AC02", source: "no code on main"),
  ],
  "bots": [
    LaterItem(title: "Pause all bots", sub: "stop every bot's work at once", why: "Today work pause and runner off do this one project or bot at a time.",
              plan: "NOT SCOPED · needs a contract", source: "memev work pause <project> · memev runner off <seat>"),
    LaterItem(title: "Richer states", sub: "what each bot is doing, not only whether it is live", why: "Planned.", plan: "PLANNED · SO-C-B AC01", source: "no code on main"),
    LaterItem(title: "Capability records per agent", sub: "what each agent is good at", why: "Planned.", plan: "PLANNED · SO-05-B", source: "no code on main"),
    LaterItem(title: "A project lead separate from the overall lead", sub: "one lead per project", why: "Planned.", plan: "PLANNED · SO-L-A", source: "no code on main"),
  ],
  "projects": [
    LaterItem(title: "Goal to plan", sub: "describe an outcome in plain words; Memev drafts the brief and the plan for your approval",
              why: "The planner is not on main.", plan: "PLANNED · SO-C-A AC03, SO-REQ-045", source: "planner absent"),
  ],
  "memory": [
    LaterItem(title: "Findings", sub: "something an AI noticed that stays short of a fact until confirmed",
              why: "The finding model is built; nothing reads it yet.", plan: "PLANNED", source: "no reader on main"),
  ],
  "settings": [
    LaterItem(title: "Notifications and a morning summary", sub: "what needs you, which bot went quiet, what was checked overnight",
              why: "Planned.", plan: "PLANNED · SO-C-C AC05", source: "no code on main"),
  ],
  "health": [
    LaterItem(title: "A guided repair page for every check", sub: "what went wrong, and the one button that fixes it",
              why: "Today the memev doctor command repairs what it can in the terminal.", plan: "PLANNED · SO-C-C AC04", source: "memev doctor --fix --yes"),
  ],
]
