# Rev2 build map: every screen, tab and element, and what fills it

This map is for the native app. It follows rev2 exactly as it was built in the mockup.

Sources, all read-only:

- Mockup: `ui-mockups/rev2/canvas-src/src/` in the bus bundle `rebaseline-2026-11-05`. Files `tpl.html` and `logic1.js` to `logic5.js`. `build.sh` joins them into `project/Main.dc.html`. I checked that each source file appears unchanged in that built file. Citations such as `logic3.js:45` point into `src/`.
- Decisions: `rev2/05-decisions.md`. Later sections win over earlier ones. Where the decisions and the code differ, the code is newer, and this map says so.
- Core: `native/COVERAGE.md` in this repository (core functions, at memev `c0f47b1e`) and `ui-mockups/FREE-CORE-INVENTORY.md` (JSON fields, at memev `0ba15e33`).
- `native/CLI-CONTRACT.md` and `native/PAGES.md` in this repository, where they limit what the window may run.

How to read an element:

- **Src** is the core command or MCP tool that fills it, from COVERAGE.md, with the JSON field where the inventory names one. COVERAGE status is in brackets: in, next, waits.
- **Tag** is "Coming later" or "Sample, not built yet", as the mockup tags it, with the reason the mockup gives.
- **Act** is each action: the label, the command it runs, and **confirm** if it needs a confirmation.
- **Panel** is what the detail panel shows when the element is clicked.
- An element is one section, list row, chart, chart row group, field, button, sheet or window. Each has an id such as `OV-3`.
- "App" means the app does it itself, with no core command.
- "No flow drawn" means the mockup button only shows the status line `Mockup only: "<label>" changes nothing here.` (`logic5.js:214`, `say()` at `logic1.js:22`).

Confirmations. COVERAGE.md:5 says every action runs behind a confirmation that names what changes. The mockup draws a named confirmation sheet only for some actions (`logic5.js:113-128`). Below, **confirm (sheet "...")** means the mockup draws that sheet. **confirm (rule)** means the mockup draws no sheet, but COVERAGE.md:5 still requires one. Reads and navigation need none.

The licence boundary holds for this map. Nothing here describes the merge engine. The one row that names it (Settings, Plan and computers, Coming later) is shown as text only.

---

## 1. The shell

### 1.1 Layout and widths

- Window: sidebar on the left, then the main column, then the detail panel when open (`tpl.html:12-300`).
- Sidebar: 240 px wide, padding 20 16 16, on the `panel` colour, with a 1 px `rule` edge on its right (`tpl.html:13`). 05-decisions:17.
- Top bar: 48 px high, padding 0 36, on `panel`, with a 1 px `rule` edge below (`tpl.html:34`).
- Main area padding: 32 36 64. On Operations, Workspace: 16 36 16 (`logic5.js:217`, `:224`).
- Content width: no maximum. It fills the window with 36 px side padding (`logic5.js:224` `pageMax: 'none'`). This is the 6 Nov decision (05-decisions:155-157). It replaces the 1,120 px maximum at 05-decisions:17.
- Text paragraphs keep their own width: 640 px for the page sentence and notes (`tpl.html:51`, `:73`), 520 px in empty states (`tpl.html:84`).
- Detail panel: 400 px, sticky, full window height, scrolls inside, padding 24, 24 px between parts, on `panel` with a 1 px `rule` left edge (`tpl.html:277`).
- Sheet (a focused form over the window): 600 px wide at most, padding 24, radius 12, over a scrim, 72 px from the top (`tpl.html:303-305`).
- Command field: 280 px wide, 32 px high (`tpl.html:39`).
- Rows: at least 44 px high, padding 12 (`tpl.html:93`). Detail panel rows: at least 36 px (`tpl.html:283`, `:288`).
- Lists sit on `panel` with a 1 px `line` round them, radius 12, padding 4 12. Column headers sit on `raise` (`logic1.js:144-145`, `tpl.html:88-90`). This is the 6 Nov colour separation (05-decisions:159-161).
- Charts in a row are split by a 1 px `rule` with 24 px padding (`logic1.js:152`).
- Spacing between blocks: 40 px before the first, 48 px before a labelled section, 24 px before an unlabelled one, 24 px indent inside an open expander (`logic1.js:82-87`).
- Minimum native window: 1024 by 700 (`SLICE.md`, the first slice).

### 1.2 Sidebar, top to bottom (`tpl.html:13-31`, `logic5.js:194-203`)

- **SH-1 Wordmark**: the mark (a circle in `text` crossed by an `accent` line), 28 px, then "Memev" at 16/24 weight 600 (`tpl.html:14`).
- **SH-2 Health button**: two lines in one button, padding 8 12, radius 8. Its background is `raise` while Health is open (`tpl.html:15-20`, `logic5.js:220`). A click opens Health.
  - Line 1: a status glyph and one sentence (`logic5.js:218-220`). The sentence is "1 to look at: ops-bot quiet" (warn glyph) when something needs the person, "All working" (ok glyph) when nothing does, and "Memev is stopped" (bad glyph) when stopped.
  - Accessible name (`logic5.js:221`): "1 to look at: ops-bot quiet. 12 fine, 1 off by choice. Open Health.", "All working. 13 fine, 1 off by choice. Open Health." or "Memev is stopped. Open Health."
  - Line 2: the pip strip, 6 px squares, 4 px apart (`tpl.html:17-19`). The code draws **14 pips** (`logic5.js:202`). 05-decisions:14 and `tpl.html:18` say 13. The code wins: 13 checks plus one hollow "Web off". Each pip has a title: "ops-bot quiet · 4 of 5 live", "Memory on", "Running", "History", "Search", "Messages", "Lead set", "Backup", "Plan", "Notes safe", "Shared", "Checks on", "Update", "Web off".
  - Pip look (`logic5.js:203`): fine is filled `ctl`; needs you is `warn`, turned 45 degrees; fault is a `bad` triangle; off by choice is hollow with a `ctl` ring.
  - Src: `doctor --json` [in] (array of `{id, title, status ok|warn|fail, detail, fix}`), `status --json` [in], `bus roster --json` [in] (`seats[].state`, `operator`), `plan --json` [in]. Map: Running = `doctor` id `daemon`; History = `oplog`; Search = `index`; Messages = `bus`; Lead set = `bus-lead`; Backup = `backup`; Plan = `license`; Notes safe = `secrets`; Shared = `ready --json` `smoke` [next]; Checks on = `contracts status` (not in COVERAGE, see G-12); Update = `update`; Web off = `remote`; bot pip = `bus roster` states.
- **SH-3 Menu** (`logic5.js:194-201`, `tpl.html:21-26`). Buttons 32 px high, radius 8. The selected entry is on `raise`, weight 500, with a 2 px `accent` line at its left edge. In order:
  1. "Overview"
  2. "Operations"
  3. "Projects". Under it, one entry per project, indented 28 px: "Website relaunch", "Support inbox", "Spring campaign". These are hidden in first run (`logic5.js:200`). A project entry with something that needs the person carries the warn diamond, in the normal state only.
  4. "Bots and agents". It carries the warn diamond ("needs you") in the normal state (`logic5.js:194`).
  5. "Memory"
  6. "Checked work"
  7. a 1 px divider
  8. "Settings"
  - Src for project entries: `card --json` [in] `active_projects`. The diamond: `work status <project> --json` [next] `blocked`, `stale_evidence`.
  - Health and Install have no menu entry. Health opens from SH-2 and from the stopped banner. Install is the first-run wizard.
- **SH-4 "Help and support"**: at the sidebar foot, 32 px, on `raise` while open. It opens Settings, Help and support (`tpl.html:28`, `logic5.js:222`).
- **SH-5 Foot line**: "Personal, free · v0.1.0" or "Personal Plus · v0.1.0", 12/16 in `dim` (`tpl.html:29`, `logic5.js:223`). Src: `plan --json` [in] `plan`; `license status --json` [in] `tier_label`; version from `status --json` `version`.

### 1.3 Top bar (`tpl.html:34-40`)

- **SH-6 Breadcrumb** (`logic5.js:204-205`). A flat screen shows its name alone: "Overview", "Operations", "Projects", "Bots and agents", "Memory", "Checked work", "Settings", "Health". Inside a project it reads "Projects › Website relaunch". "Projects" is then a link back to the list. The current part is in `text`, the rest in `sub`.
- **SH-7 "Technical detail on"** chip: shown only when technical detail is on (`tpl.html:38`).
- **SH-8 Status line**: one line in `sub` that reports the result of the last action (`tpl.html:38`, role status). In the app it shows the core's answer to an action.
- **SH-9 Command field**: placeholder "Search or run a command", with "⌘K" at its right (`tpl.html:39`). The mockup gives it no behaviour. 03-interaction-model §2.6 describes it: go to any screen, project, view, bot, contract, note or setting by name, and run any action that a screen button runs. Src: app, over the same commands the buttons run; note search uses `recall --json` [in]. It is not tagged, so see D-3.

### 1.4 Stopped banner (`tpl.html:42-44`)

- **SH-10** Shown on every screen in the Stopped state. A `bad` triangle, then "Memev is stopped." and "Your AI apps cannot reach their shared memory. Nothing is lost." Two buttons: "See why it stopped" (opens Health) and "Start Memev" (the amber button).
  - Src: `status --json` [in] fails or `doctor --json` id `daemon` fails. The inventory names `whyNotRunning` (`daemon.mjs:55`), but no command prints it; "See why" shows the doctor result.
  - Act: "Start Memev" runs `start` [next], confirm (rule).

### 1.5 Page header (`tpl.html:48-65`)

- **SH-11 Title**: 28/36, weight 500, tracking -0.02em. One per screen.
- **SH-12 Sentence**: 16/24 in `sub`, 640 px wide at most. Every screen's sentence is given below.
- **SH-13 "What is this?"**: a ghost button at the right of the title, on every screen that has words to explain (`tpl.html:54`). It opens the detail panel titled "What is this?" with "The words on this screen, in plain terms." and one group "WORDS ON THIS SCREEN" (`logic5.js:180`). The words for each screen come from `HELP` (`logic2.js:46-55`) and are listed in each screen section below. App text, no core source.
- **SH-14 Page actions**: up to two buttons at the right of the title (`tpl.html:55`). The amber one follows SH-20.
- **SH-15 Header rule**: on a screen with no tabs, a 48 px 2 px `accent` line, then a 1 px `rule` to the right edge, 24 px under the header (`tpl.html:59`). On a screen with tabs the tab strip takes its place.
- **SH-16 Tabs**: text only, 40 px high, 24 px apart, one 1 px `rule` under the strip. The selected tab is `text`, weight 500, with a 2 px **`accent`** underline (`tpl.html:61-65`, `logic5.js:225`). This replaces 02-visual-system's "not amber" tab rule (05-decisions:59). A tab that needs the person carries the warn diamond, in the normal state only.

### 1.6 Section label and line rules (05-decisions:52-64)

- **SH-17 Section label** (`tpl.html:69-71`): a 16 by 2 px `accent` line, 12 px gap, the label in IBM Plex Mono 12/16, weight 500, tracking 0.06em, in `sub`, then the "Sample, not built yet" tag when the block has one, then a 1 px `rule` that runs to the right edge, then "Show all (n)" or "Show fewer" as an underlined link when the list has more rows.
- The same label form is used in detail panel groups (`tpl.html:286`), on Operations (`tpl.html:159`, `:170`, `:188`) and on install stages (`tpl.html:337`, 24 px line, label in `text`).
- Amber is used in three ways only: the thin 2 px brand line, the one filled button, and the needs-you diamond (05-decisions:64).
- **SH-18 Expander row** (`tpl.html:79-81`): a 1 px `rule` above, a "›" chevron that turns 90 degrees when open, a title at weight 500, then a summary in `sub`. Its contents open 24 px indented.
- **SH-19 "Sample, not built yet" tag**: 20 px high, padding 0 8, radius 4, on `raise`, 12/20 weight 500 in `sub` (`tpl.html:70`, `logic1.js:37`). Charts drawn from samples use the `sample` colour for their marks (`logic1.js:155`).

### 1.7 The one amber button rule (`logic5.js:177-179`)

- **SH-20** At most one filled amber button on a screen. The checker enforces it (05-decisions:139).
  - A row button marked primary is amber: "See why" (Overview), "Resolve" (project Risks and decisions), "Ask it" (Health), "Upgrade" (Plan and computers, free), "Start Memev" (Health, stopped).
  - The last page action is amber only when no row on the screen has an amber button, the screen is not stopped, and the page is not marked `noPrimary`. Operations and Operations, Messages are `noPrimary`.
  - In the Stopped state every row button turns secondary, and only the banner's "Start Memev" is amber.
  - The first button of an empty state is amber (`logic1.js:139`).
  - On Operations, Workspace the "Send" button is amber only while the prompt box has text (`logic1.js:117`).
  - In a sheet the main button is amber, except in a confirmation: there both buttons look the same, and the main one has a `bad` ring and `bad` text (`logic5.js:191`). The issue forms also make both buttons look the same.
  - In the install wizard each window has one amber button (`tpl.html:352`).
- Secondary buttons: on `raise`, 1 px `line`, `text`. Ghost buttons: no background, `sub`. All 32 px high, radius 8, 14 px weight 500. Large buttons (empty states, install): 40 px, padding 0 16 (`tpl.html:84`, `:351-352`).

### 1.8 The detail panel (`tpl.html:276-300`, `logic5.js:182-186`)

- **SH-21** Opens on the right when a row, chart, gate, lane or part with details is clicked. A second click on the same item closes it. Esc closes it (`logic5.js:228`). Focus moves to its title and returns on close (`logic5.js:244-249`). The clicked row shows as selected: `raise` background, 1 px inset `text` ring, radius 8 (`logic1.js:66`).
- Parts, in order:
  1. Title, 20/28 weight 500, and a "✕" close button, "Close details" (`tpl.html:278`).
  2. Tag, when the item is a sample: "Sample, not built yet" (`tpl.html:279`).
  3. State: a glyph and a state line; red text when the state is bad (`tpl.html:280`).
  4. Why: one sentence in `sub` (`tpl.html:281`).
  5. Buttons: up to two, secondary (`tpl.html:282`).
  6. Facts: rows of name (120 px, `sub`) and value (`tpl.html:283`).
  7. Groups: each with a section label and rows; a row may have a glyph, a second line, a figure on the right, a small bar, or a button (`tpl.html:284-296`).
  8. Terminal sample, for a bot: a dark box with name, role and lines (`tpl.html:297`). Always a sample.
  9. "TECHNICAL", only with technical detail on, pinned at the foot: a "Copy" button, the plan line (contract reference) and the source line (commands) in mono 12/20 (`tpl.html:298`).
- Button routing (`logic5.js:181-183`): "Open the project" opens Website relaunch. "Tell the lead" opens Operations, Workspace with the prompt box started as "About <title>: ". "Connect" opens the Connect an app sheet. "Revoke", "Replace with a new key", "Remove", "Disconnect" and "Cancel with a reason" open their confirmation sheets. Every other button has no flow drawn.

### 1.9 "Coming later" rows (`logic1.js:56`)

- **SH-22** An expander titled "Coming later (n)" with the summary "not built yet; each opens a sample of what is planned". Inside, a list with no label. Each row has a hollow glyph. Its detail panel always carries the "Sample, not built yet" tag, a why sentence, and with technical detail on a plan line ("PLANNED · <contract>", "NOT SCOPED · needs a contract" or "DRAFT · <spec>") and a source line ("no code on main" and similar).
- The app shows these rows as fixed text. They run no command.

### 1.10 Technical detail switch

- **SH-23** One switch, in Settings, General, "Technical detail" (`logic5.js:59`). App setting.
- When on:
  - the top bar shows "Technical detail on" (SH-7);
  - state words switch to the engine's words (`logic1.js:35-36`): Checked → VERIFIED, Being checked → VERIFYING, In progress → IN_PROGRESS, Blocked → BLOCKED, Ready → READY, gone quiet → stale, bot → seat;
  - rows that carry a command show it under their title in mono 12/16 `dim` (`tpl.html:96`, `logic1.js:65`);
  - charts show their source line under the chart (`tpl.html:151`);
  - the detail panel shows TECHNICAL (SH-21 part 9);
  - extra blocks appear: project Work "How bots take and hold tasks" (`logic3.js:65`), Memory Search "Advanced" (`logic4.js:96`), Checked work "Block a push while a contract is open" (`logic4.js:102`), Health "Self-test" and "Preflight" (`logic4.js:115`), Storage "Vault detail" (`logic5.js:27`);
  - the Operations prompt hint reads "bus_send · Enter to send" in place of "Enter to send" (`logic1.js:117`);
  - the installer shows the command under each window (`tpl.html:357`).

### 1.11 Theme

- **SH-24** Dark and light. Settings, General, "Appearance" switches it (`logic5.js:58`). `SLICE.md` says the native window follows the computer's theme unless set. App setting.

### 1.12 Colour tokens (`logic1.js:26-32`, the latest values)

| Token | Dark | Light | Used for |
|---|---|---|---|
| page (`bg`) | #080B10 | #EFF1F5 | the window ground, inputs |
| panel | #121822 | #FFFFFF | sidebar, top bar, lists, detail panel, sheets |
| raised (`raise`) | #1B222D | #E8EBF0 | selected rows, list headers, tags, secondary buttons |
| line | #2B3543 | #D6DBE3 | rows inside a list, borders of lists |
| rule | #374252 | #C3CAD4 | section lines, shell edges, tab strip, expanders |
| control (`ctl`) | #6B7789 | #7C8797 | input borders, fine pips, hollow glyphs |
| text | #E8ECF2 | #10151C | main text |
| sub | #A4ADBA | #46505E | secondary text, section labels |
| dim | #8A94A2 | #5F6978 | captions, commands, chart units |
| amber (`accent`) | #F2B544 | #8A5600 | the 2 px brand line, tab underline, rings |
| amber fill (`accentFill`) | #F2B544 | #F2B544 | the one filled button; its text is #0A0D12 (`onAccent`) |
| good | #5FD08F | #1D6B43 | ok glyph, checked bars |
| warn | #F2B544 | #8A5600 | needs-you diamond |
| bad | #FF8A7A | #B3261E | fault triangle, failed results, danger buttons |
| series | #7F8B9A | #8590A0 | live glyph, neutral bars |
| sample | #5C6877 | #A3ADB9 | marks of sample charts |

- 05-decisions:62 gives `line` as #252E3A dark and #DDE1E7 light. The 6 Nov colour change replaced those; the table holds the code's values.
- Agent panes and the Source view use fixed dark colours in both themes (`tpl.html:163`, `:197`): header #1B222C with text #E6E9EE and state #B7BEC8; control row #141A22; body #0B0F14 with text #D3D8DF; border #2A323D; a quiet bot's dot #F2B544 (diamond); a live bot's dot #8E98A6 (circle).
- Shadow and scrim: dark `0 16px 48px rgba(0,0,0,0.5)` over `rgba(4,6,9,0.62)`; light `0 8px 24px rgba(16,21,28,0.10)` over `rgba(16,21,28,0.32)`.

### 1.13 Status glyphs (`logic1.js:40-44`)

Each state has its own shape as well as its colour. 8 px.

- ok: a filled square, radius 2, `good`.
- warn (needs you): a square turned 45 degrees, `warn`.
- bad: a triangle, `bad`.
- live: a circle, `series`.
- off (not started, not built, off by choice): a hollow square with a `ctl` ring.

### 1.14 Type (`tpl.html` inline, 02-visual-system §3.4)

- Fonts: Space Grotesk for everything; IBM Plex Mono for section labels, commands and machine text, always 12 px.
- 28/36, weight 500, tracking -0.02em: page title.
- 20/28, weight 500, tracking -0.01em: detail panel title, sheet title, installer window title, preview heading.
- 16/24: page sentence (400 in `sub`), chart titles (500), chat messages, empty-state text, wordmark (600, the only 600).
- 14/20: body, rows, buttons (500), menu.
- 12/16: secondary lines, column headers (500), tags (500 on a 20 px line), captions. Mono 12/16 for one line and 12/18 or 12/20 for several.
- Numbers in columns use tabular figures (`tpl.html:107`, `:132`).

### 1.15 Mockup-only controls

- **SH-25** The dashed strip above the window is not part of the app (`tpl.html:2-9`, `logic5.js:207-213`): "View" (The app, Install steps), "Theme" (Dark, Light), "Plan" (Free, Paid), "Technical detail" (Off, On), "State" (Normal, First run, All clear, Stopped). It says "every figure is sample data". The app does not build this strip. The states it switches are real app states and are described per screen.

### 1.16 Sheets (`logic5.js:112-147`, `tpl.html:302-326`)

- A sheet is a focused form. Focus moves in, the rest of the window goes inert, Tab stays inside, Esc or a click outside closes it (`logic5.js:235-243`, `:228-230`). Parts: title, optional tag, note, fields, rows, a notice on `raise`, with technical detail on the source line, then a status line, "Cancel", an optional second button and the main button.
- **SH-26 Confirmation sheets** (`logic5.js:113-128`). Title, note, main button. Both buttons look the same; the main one is red.

| Id | Title | Note | Main button | Runs |
|---|---|---|---|---|
| revoke | "Revoke the key for <holder>?" | "It stops working at once on this Mac. You can issue a new key at any time." | "Revoke key" | `token revoke` [next] |
| replace | "Replace the key for <holder>?" | "The old key stops working, and its holder needs the new one." | "Replace key" | `token rotate` (not in COVERAGE's list, see G-24) |
| rmpc | "Remove <computer>?" | "It stops reaching your memory, and one computer on your plan comes free. You can add it again later with a new code." | "Remove computer" | `devices remove` [next] |
| rmpath | "Remove the memev command from your PATH?" | "Terminals and AI apps can no longer call memev by name until you add it back." | "Remove command" | `path remove` [next] |
| uninstall | "Remove Memev?" | "This disconnects every app, stops Memev and removes the login service. Your notes stay in ~/memev." | "Uninstall" | `uninstall` [next] |
| purge | "Also remove the runtime and launcher?" | "This does everything Uninstall does, then removes the runtime and launcher as well. To use Memev again you would install it again." | "Purge" | `uninstall --purge` [next] |
| stop | "Stop Memev?" | "Your AI apps lose their shared memory until it starts again." | "Stop Memev" | `stop` [next] |
| rotate | "Make a new web-access key?" | "This cuts off anyone holding the old address." | "Rotate key" | `cloud rotate-key` (`cloud` [next]) |
| compact | "Tidy old history files?" | "This frees very little disk space and stops Memev for a moment." | "Compact" | `compact` [next]; the core refuses while the daemon runs, so the app must stop, compact, start |
| disconnect | "Disconnect <app>?" | "It loses its shared memory until you connect it again." | "Disconnect" | `disconnect <app>` [next] |
| drop | "Drop "<memory>"?" | "It leaves your memory and stops being recalled." | "Drop" | `review drop` [next] |
| cancel | "Cancel "<task>"?" | "The task stops here. You give a reason, and it is kept with the task." | "Cancel the task" | `work cancel <project> <task> --reason` [next] |
| mark | "Mark "<task>" as checked?" | "The lead only, with evidence, on this computer." | "Mark as checked" | `memev_task_verify` [next], local only |
| switchoff | "Switch off Checked work?" | "Nothing is held or blocked while it is off." | "Switch off" | `contracts off` (not in COVERAGE, see G-12) |

- The form sheets (upgrade, addpc, connect, weblink, contract, save, import, bug, feature) are listed with the screens that open them.

---

## 2. Screens, in menu order

Sample data in the mockup: three projects (`logic2.js:3-7`), five bots with claude-code as lead (`logic2.js:10-16`), four needs-you items (`logic2.js:32-37`). The app shows live values in their place.

States drawn: Normal, First run, All clear (nothing needs you), Stopped. Where a screen changes by state, it says so.

Order: the seven menu entries first, then Health and the install wizard, which have no menu entry.

### 2.1 Overview (`logic3.js:3-32`)

- Menu "Overview". Breadcrumb "Overview". Title "Executive overview" (05-decisions:19).
- Sentence: "4 things need you. 3 projects are moving. 1 bot has gone quiet." All clear: "Nothing needs you. 3 projects are moving. Every bot has reported in." First run: "Memev is ready. Your apps share one memory." (`logic3.js:4`, `:20`)
  - Src: count of OV-1 items; projects from `card --json` [in] `active_projects`; bots from `bus roster --json` [in] `seats[].state`.
- What is this? words (`logic2.js:48`): "Checked", "Needs you", "Gone quiet", "Lead".
- No page actions. Header rule SH-15.

Normal and All clear:

- **OV-1 Section "NEEDS YOU"**, a list (`logic3.js:22-23`). Shows two rows, then "Show all (4)" / "Show fewer".
  - Src: across projects from `card --json` `active_projects`, each `work status <project> --json` [next]: `blocked`, `stale_evidence`, `approvals`; plus `bus roster --json` seats whose `state` is `stale`.
  - All clear: one row "All clear." with "Nothing is blocked, no check is out of date and every bot has reported in." and an ok glyph.
- **OV-2 Row "Website relaunch: the checkout page is blocked"**, second line "no payment test key", button "See why" (amber) (`logic2.js:33`).
  - Src: `work status <project> --json` `blocked[]` with the reason; `memev_project_status` task `blocked_reason`, `holder`.
  - Act: "See why" opens the panel. No command.
  - Panel: state bad "Blocked"; why "The checkout page cannot be checked until a payment test key arrives. The key was requested on 3 Nov. The work goes on against a mock."; buttons "Tell the lead" (to Operations) and "Open the project"; facts "Project", "Held by", "Checks" ("0 of 5"), "Last message". Technical: "memev work status --json per project (blocked, with the reason)".
  - Checks come from `memev_contract_status` [next] `passed`, total. Last message comes from `bus tail --json` [next] filtered by the holder seat.
- **OV-3 Row "ops-bot has gone quiet for 48 minutes"**, "last task: nightly stock sync", button "Ask it to report in" (`logic2.js:34`).
  - Src: `bus roster --json` seat `state` = `stale`, `minutes_since`, `last_message`, `claims`.
  - Act: the button opens the bot's panel.
  - Panel: the bot panel, BOT-P below.
- **OV-4 Row "Website relaunch: the header check is out of date"**, "the header changed after its check passed", button "Re-check now" (`logic2.js:35`).
  - Src: `work status <project> --json` `stale_evidence[]`.
  - Act: the button opens the panel. In the panel "Re-check now" runs `verify <id>` [next], confirm (rule).
  - Panel: state warn "Out of date" (technical: "stale evidence"); why "The check passed, then the header changed. The pass no longer counts until the check runs again."; facts "Project", "Task", "Passed", "Changed".
- **OV-5 Row "Support inbox: the reply template check is out of date"**, "the template changed after its check passed", button "Re-check now". Shown after "Show all (4)" (`logic2.js:36`). Same source and panel as OV-4, why "The reply template changed after its check passed."
- **OV-6 Section "TODAY"**, three charts side by side (`logic3.js:24`).
- **OV-7 Chart "Progress by project"**, unit "Tasks, by project", legend "Checked", "Reported done, not yet checked", "Not done". One row per project: a stacked bar and "14 of 31". Note under it: "A task counts as done only when its check passes, not when a bot says so." (`logic3.js:9-12`)
  - Src: `card --json` `active_projects`; per project `work status <project> --json` [next] `done`, `total`, and `tasks[].events.observed` against `events.verified` for the middle part.
  - Act: a row opens that project. The chart title opens the panel.
  - Panel: why "Click a bar to open that project. Checks passed, by project:"; group "CHECKS PASSED BY PROJECT", one row per project with "<status> · last checked <time> · <n> need you", a figure "6 of 17" and a small bar. Src `memev_contract_status` [next] `passed`, total, `last_verified`.
- **OV-8 Chart "Bots now"**, unit "Minutes since each bot was last seen". One row per bot with its glyph, a thin bar up to 60 minutes (warn colour for a quiet bot) and "gone quiet · 48 min" or "live · 2 min". All clear: every row "live". (`logic3.js:13-15`)
  - Src: `bus roster --json` [in] `seats[].seat`, `state`, `minutes_since`, `operator` (adds "· lead").
  - Act: a row opens Bots and agents, Team.
- **OV-9 Chart "Runs today"**, unit "Unattended runs today, by bot". One row per bot, most first, with the count. (`logic3.js:16-18`)
  - Src: `runner status --json` [next] per seat `today`.
  - Act: a row opens Bots and agents, Team.
- **OV-10 "Coming later (5)"** (`logic3.js:25-31`). Rows, each tagged sample:
  - "Runs by day", "a week of activity at a glance". Reason: "The runner keeps the last 1,000 run times for each bot, but its status reports today only. Showing earlier days needs a small addition." NOT SCOPED, a small addition to `memev runner status`.
  - "Business figures from your bots", "enquiries, replies and orders, each naming the bot that reported it". Panel group "SAMPLE FIGURES": Enquiries handled 128, Replies sent 412, Orders synced 37, Replies inside 1 hour 96%. NOT SCOPED, needs a contract for a metric feed.
  - "Decisions waiting for you", "a bot asks, you decide, the work continues either way". Panel group "SAMPLE DECISIONS" with two "Decide" rows. PLANNED SO-A-05, SO-C-A AC04; work status has an approvals field, nothing creates an approval yet.
  - "This week against last, and cost", "tasks checked week on week, and what the AI cost". "Not available yet. Memev keeps no figures from last week and records no spend." NOT SCOPED.
  - "Ask across the business", "a question over everything your bots did, answered with its sources". PLANNED, business inference intelligence, no code on main.

First run (`logic3.js:4-7`):

- **OV-11 Empty state**: "Start a project when you have work to hand out, or just carry on: your apps already remember." Buttons "Start a project" (amber) and "Import what you already have".
  - Src: `card --json` [in] `active_projects` is empty.
  - Act: "Start a project": no flow drawn and no command (G-1). "Import what you already have" opens the Import sheet (MEM-S2).
- **OV-12 Section "FINISH SETTING UP"**, a list (05-decisions owner choice 4):
  - "Let AI apps on the web reach your memory", "off until you choose · needed for ChatGPT, Claude and Grok on the web", button "Set up": opens Bots and agents, Connect.
  - "Connect a web AI app", "needs web access first", button "Connect": opens Bots and agents, Connect.
  - "Help improve Memev?", "an anonymous count of setup milestones, never your notes · off unless you say yes", button "Decide": opens Settings, General.
  - Src for the row states: `cloud status` [next] (text only), `plan --json` `connectors_counted`, `report status --json` [in] `asked`, `enabled`.

### 2.2 Operations (`logic5.js:74-109`, block drawn by `logic1.js:94-135`, `tpl.html:157-203`)

- Menu "Operations". Breadcrumb "Operations". Title "Operations".
- Tabs: "Workspace", "Messages" (05-decisions:45). There is no lead slice on other screens (05-decisions:34, :92).
- What is this? words (`logic2.js:47`): "Lead agent", "Message bus", "Background runner", "When the lead answers", "Where else this conversation is", "Optimised", "Output".

#### Tab: Workspace

- Sentence: "claude-code leads. 4 of 5 agents are live." First run: "claude-code leads. Nothing has been asked of it yet." (`logic5.js:74`)
  - Src: `bus roster --json` [in] `operator`, `seats[].state`.
- **OP-1 Page action "Change the lead"**, secondary (the page is `noPrimary`). No flow drawn.
  - Act: `bus operator <seat>` / `bus_set_operator` [next], local only, only with the person's own answer. Confirm (rule).
- The page fits the window: the thread and the outputs panel take the height left (`logic1.js:112-115`).
- **OP-2 Section "AGENTS AT WORK"**, label in amber (05-decisions:70). The tag "Sample, not built yet" shows only in the Terminal view. Controls on the label line (`tpl.html:159`):
  - **OP-3 View buttons** "Messages (works today)" and "Terminal output (sample)". App view switch.
  - **OP-4 "Pause" / "Resume"**: stops new lines arriving in the panes. Disabled in first run, when stopped, and in the Terminal view. App only; it changes nothing in the core.
  - **OP-5 "Hide agents" / "Show agents"**: hides the panes so the conversation takes their height. App only.
- **OP-6 Usage row**, group "Model and usage for all agents" (`tpl.html:160`): select "All agents" (Optimised, Strong model, Balanced model, Fast model), select "Daily limit" (No limit, A set amount, then stop and ask), the text "Used today: Not available yet", and the tag "Sample, not built yet".
  - Tag: sample. Reason in "Coming later" OP-14 "Model, effort and usage": DRAFT MU-1; nothing records tokens or cost.
  - Src: none (G-6).
- **OP-7 Agent panes**, one per bot, side by side, scroll sideways, 168 px high (`tpl.html:162-164`, `logic1.js:124-131`). The lead's pane is first, wider (1.4, at least 360 px) with a 2 px amber edge. Others at least 290 px.
  - Header: a dot (diamond for a quiet bot), the name ("claude-code · lead", "codex", "sales-bot", "support-bot", "ops-bot") and a state ("live · home page build", "gone quiet · 48 min").
  - Control row: for bots Memev starts itself (the lead, codex), a model select, an effort select ("Effort: auto", "Effort: high", "Effort: medium", "Effort: low"; disabled while Optimised) and "now strong · high" or "your choice". For the others: "Model is set in the bot’s own app".
  - Body, Messages view: one line per message, "16:02 → codex  Mock the payment provider for now.", "← " for received, "2 unread" or "1 unread, on hold" at the end. The newest line stays in view unless the person has scrolled up (`logic1.js:131`). First run: "(nothing yet)".
  - Body, Terminal view: rev1's terminal lines. Tagged sample.
  - Src, Messages view: `bus since SEQ --json` [next], filtered by sender and recipient in the app (05-decisions:90). COVERAGE says the panes wait for the core's `--from/--to` filters (SO-N-15) (G-7). Fields: `seq`, `ts`, `from`, `to`, `type`, `body`. Unread counts: `bus_unread` with peek [waits] (G-8). Pane state: `bus roster --json` `state`, `minutes_since`, and the held task from `memev_project_status` `holder`.
  - **Never `bus watch`.** It reads as that bot, marks its messages read and keeps a quiet bot showing live (05-decisions:90).
  - Src, Terminal view: none. Tag: sample. Reason: "Sample only: Memev cannot show a bot’s own terminal yet." Nothing on main starts, attaches to or streams a bot's session.
  - Model and effort selects: no source (G-6).
  - The mockup adds a sample line to each live pane every 2.5 seconds (`logic5.js:251-255`, `LIVE` at `:100-103`). The app instead polls `bus since` from the last `seq`.
- **OP-8 Pane note** under the panes: "Following each bot’s messages works today. The lines arriving every few seconds here are sample data." When stopped it adds "Memev is stopped, so no new lines arrive."; when paused, "Paused: no new lines until you resume." In the Terminal view: "Sample only: Memev cannot show a bot’s own terminal yet." With technical detail on: "memev bus since SEQ --json, filtered by sender and recipient in the app (small new work) · unread counts: bus_unread with peek · never memev bus watch, which marks messages read" (`logic1.js:118`). The app drops the sentence about sample lines.
- **OP-9 Panel "LEAD AGENT"**, label in amber, 2 px amber edge (`tpl.html:169-184`). Controls on its label line: select "Model for the lead agent", select "Effort for the lead agent", and "now strong · high". Src: none (G-6).
- **OP-10 Conversation thread** (role log), scrolls inside itself to the newest line (`tpl.html:171-179`, 05-decisions:35):
  - Your messages: on the right, in a bubble on `raise`, 16/24.
  - The lead's messages: on the left, "claude-code · lead · 15:33" above, then the text at 16/24. With technical detail on, the message line "#183 reply · info · plan" (sequence, type, about).
  - Step lines: mono, with a left rule, "→ codex   Mock the payment provider for now." and "← codex   Mocked. All five checkout checks run against the mock." These are what the lead sent to and heard from other bots.
  - Output cards: name, meta "sample output · diagram · from codex · draft", and "Open preview". Tagged sample in their meta text.
  - Blank: "Nothing has been said yet. Tell your lead agent what you want done. It hands the work to your other bots and answers here."
  - The mockup's own reply, "Understood. I will pass this to the right bot and report back here." marked "just now · sample reply", and the "…" writing line are mockup behaviour. The app shows only real bus messages.
  - Src: `bus since SEQ --json` [next] (or `bus tail --json`), the messages between the person's seat and the `operator` seat, and the operator's messages to other seats for step lines. Fields `seq`, `ts`, `from`, `to`, `type`, `body`, `ref`, `project`. Output cards: none beyond `artifact_hash`; they are tagged sample.
- **OP-11 Prompt box**, stays under the thread (`tpl.html:180-183`):
  - Text area "Message the lead agent", placeholder "Tell your lead agent what you want done, or ask a question", two lines at 16/24. Enter sends; Shift Enter is a new line.
  - Ready-made requests: "What needs me today?", "Status update on Website relaunch", "Check everything again". Each fills the box. App only.
  - Hint "Enter to send" (technical: "bus_send · Enter to send").
  - **"Send"**: amber when the box has text.
  - Act: `bus send <operator> request "<text>"` / `bus_send` [next], sent as the person's own seat (G-10). The mockup sends at once with no sheet. Confirm (rule) conflicts with a chat; see D-2.
  - "Tell the lead" in any detail panel lands here with "About <title>: " in the box (05-decisions:36). It never sends by itself.
- **OP-12 Panel "OUTPUTS"**, tag "Sample, not built yet" (`tpl.html:185-192`). Text: "Everything your bots produce is listed here, newest first. The files stay in your project folder on this computer. Pick one to preview it." Rows: "Home page · v3" ("web page · made by claude-code · today 16:12", "reviewed · 1 of 4 checks pass"), "Content audit report · v1" ("accepted · 3 of 3 checks pass"), "Checkout flow · v1" ("draft · 0 of 5 checks pass"). Empty in first run.
  - Tag: sample. Reason (05-decisions:49): today a finished task records only a hash of what it produced, `memev_project_status` `artifact_hash`. Listing, preview and versions are SO-A-06 C2 to C5 and SO-C-C AC03.
  - Src: none beyond `artifact_hash`.
- **OP-13 Preview canvas**, opens in the Outputs panel from a row or a card (`tpl.html:193-199`, `logic1.js:73-75`): "‹ All outputs", name and second line, the sample tag, view buttons "Preview" and "Source", "Save a copy...", "Show in folder", the preview body (a page in a sealed frame with no network access, 05-decisions:48), and a foot line with the check state and the file's place ("reviewed · 1 of 4 checks pass · projects/website/pages/home.html").
  - Tag: sample, same reason as OP-12.
  - Act: "Save a copy..." and "Show in folder": no flow drawn; app file actions once outputs exist.
- **OP-14 "Coming later (6)"** (`logic5.js:85-91`):
  - "Model, effort and usage", "Optimised, or your own choice per agent, with a daily limit · free on every plan". Facts "Plan: free on every plan", "The lead: stays on a stronger model by default". DRAFT MU-1. Nothing on main records tokens or cost; the runner stores one command per bot.
  - "Outputs and the preview canvas", "a list of what each task produced, with preview, versions and save a copy". PLANNED SO-A-06 C2 to C5, SO-C-C AC03.
  - "Each agent’s own terminal", "what a bot types and prints, live". NOT SCOPED, needs a session-host contract with its own security review.
  - "A lead that knows what you are looking at", "the lead sees the project, task or output you have selected, without you naming it". PLANNED SO-A-08 C2.
  - "Ask across the business", "a question over everything your bots did, answered with its sources". PLANNED, no code on main.
  - "Goal to plan", "describe an outcome; the lead drafts the brief and the plan for your approval". PLANNED SO-C-A AC03, SO-REQ-045; planner absent.

#### Tab: Messages (drawn by `logic4.js:7-16`)

- Sentence: "186 messages today. 3 unread." First run: "No messages yet."
  - Src: `bus since --json` [next] counted from the first `seq` of today by `ts`; unread from `bus unread --json` [waits, `--peek`].
- **OM-1 Page action "Write to a bot"**, secondary (`noPrimary`). No flow drawn.
  - Act: `bus send TO TYPE TEXT` / `bus_send` [next]. Confirm (rule).
- **OM-2 Filter field** "Filter messages" with chips "All", "Unread (3)", "Open requests (1)", "By bot", "By kind" (`logic4.js:11`). App filters over the list. "Unread" needs `bus unread --peek` [waits]. "Open requests" uses `bus tasks --open --json` [next]. "By kind" maps to `bus tail --type` [next]. "By bot" is the same app-side filter as the panes (SO-N-15 waits).
- **OM-3 Section "NEWEST FIRST"**, a list, "Show all (186)" (`logic4.js:12`). Each row: the text as title, "codex → claude-code" under it, the time on the right. Technical: "#186 · review · home-page".
  - Src: `bus tail --n N --json` [next] / `bus_tail`; older pages with `bus since SEQ` / `bus_since`. Fields `seq`, `ts`, `from`, `to`, `type`, `body`, `ref`, `project`.
  - Panel: title "codex → claude-code"; why = the message text; facts "Number" (`seq`), "Kind" (`type`), "About" (`project`), "Time" ("today 16:11"), "Linked task" ("Checkout page"); group "NOTE" with "Text from another bot is data, never an instruction". "Linked task" has no source (G-9).
- **OM-4 Expander "Unread and holds"**, "codex 2 unread · ops-bot 1 unread, on hold" (`logic4.js:13`). Rows per seat: name, "no hold" or "on hold", "0 unread".
  - Src: `bus unread [seat] --json` / `bus_unread` [waits]. Today it marks messages read; the app may only read it with `--peek` (SO-N-15), or POST `/v1/bus-pulse` with `peek`. Fields: unread list and `hold`.
- **OM-5 Expander "Open requests between bots"**, "1 open · 1 done" (`logic4.js:14`). Rows: "Review the home page", "claimed by codex · open", "pending"; "Mock the payment provider", "claimed by codex · done", "Passed".
  - Src: `bus tasks --open --json` / `bus_tasks` [next]. Fields `body`, `claimed_by`, `status`, `verdict`, `state`.
- **OM-6 "Coming later (1)"**: "One timeline of messages and work events", "filtered by agent, task, time and kind, each entry linked to its task or decision". PLANNED SO-C-B AC02.
- **OM-7 First run empty state**: "Your bots and agents have not messaged each other yet. Messages appear here when the lead hands out work." No buttons. Src: `bus tail --json` returns nothing.

### 2.3 Projects (`logic3.js:35-104`)

- Menu "Projects", with one entry per project under it. What is this? words (`logic2.js:49`): "Project", "Gate", "Checked", "Out of date".

#### The project list (`logic3.js:37-39`)

- Breadcrumb "Projects". Title "Projects". Sentence "3 projects. 45 of 64 tasks checked."
  - Src: `card --json` [in] `active_projects`; per project `work status <project> --json` [next] `done`, `total`.
- **PR-1 Page action "Start a project"** (amber). No flow drawn and no command (G-1).
- **PR-2 Section "YOUR PROJECTS"**, a list with column heads "Project", "Tasks checked", "Needs you". One row per project: glyph (warn when something needs the person, else ok), the name, "<status> · last checked <time>", a bar with "14 of 31", and "2 need you" or "nothing needs you".
  - Src: `card --json` `active_projects`; `work status <project> --json` `status`, `done`, `total`, `tasks[].events` (for the bar's middle part), `blocked`, `stale_evidence` (for "need you"); "last checked" from `memev_contract_status` [next] `last_verified`.
  - Status words: `memev_project_status` project status ACTIVE, VERIFYING and so on, in plain words ("Active", "Being checked").
  - Act: a row opens the project.
- **PR-3 First run**: sentence "No projects yet."; empty state "Start a project when you have work to hand out. Your apps already share one memory without one." with "Start a project" (amber, G-1) and "Import what you already have" (opens the Import sheet, MEM-S2). Project entries are hidden in the menu.

#### A project (`logic3.js:40-103`)

- Breadcrumb "Projects › <name>". Title: the project name. What is this? as the list.
- Tabs (`logic3.js:42`): "Overview", "Plan", "Work", "Risks and decisions", "Results", "Outputs". Six tabs: the sixth breaks the five-tab rule at the owner's request (05-decisions:47). "Overview" and "Risks and decisions" carry the needs-you diamond when the project has something that needs the person.
- The mockup draws every tab in full for Website relaunch only. For the other projects it draws only the Overview chart and a note ("This mockup draws this view in full for Website relaunch only. Every project has the same six views." with a row "Open this view for Website relaunch", `logic3.js:45-48`). The app builds every tab for every project.

#### Tab: Overview (`logic3.js:90-103`)

- Sentence: "14 of 31 tasks checked. 2 things need you. Next milestone: design signed off." Other projects: "<done> of <total> tasks checked. 1 thing needs you." or "Nothing needs you."
  - Src: `work status <project> --json` `done`, `total`, `blocked`, `stale_evidence`. The "Next milestone" clause has no source (G-2).
- **PO-1 Section "NEEDS YOU"**: "Checkout page is blocked" with "Resolve" (amber), and "Header check is out of date" with "Re-check". Panels as OV-2 and OV-4. Src as OV-1, for this project.
- **PO-2 Section "PROGRESS"**, two charts:
  - **PO-3 "Reported is not checked"**, unit "Tasks in this project, by how far each has got". Rows "Requested" 31, "Accepted" 22, "Reported done" 17, "Checked" 14 (technical: requested, accepted, observed, verified). Src: `work status <project> --json` `tasks[].events{requested, accepted, observed, verified}`, counted.
  - **PO-4 "Checks passed by task"**, unit "Checks passed of total, trouble first". Rows "Checkout page" 0 of 5, "Accessibility review" 0 of 3, "Home page build" 1 of 4, "Information architecture" 2 of 2, "Content audit" 3 of 3. A row opens Results. Src: `memev_contract_status` / `contract status <id>` [next], per task `passed` and total, sorted by trouble first.
- **PO-5 Section "MILESTONES"**, tag "Sample, not built yet". A line with four points: "Content audit" done, "Structure" done, "Design signed off" next, "Launch" later, and an amber "today" mark. Reason (inventory §7): milestone entities exist in the engine, but no tool, route or command reads them (SO-C-B AC03).
- **PO-6 Expander "What your AI apps know about this project"**, "what each session starts with · project memory · checkpoints · people":
  - Section "WHAT EACH AI SESSION STARTS WITH", rows "Project state and tasks", "Last session", "Recent decisions", "Open contracts". Src: `card --project <p> --json` [in] `project_state`, `project_tasks`, `last_session`, `recent_decisions`, `open_contracts`.
  - Section "PROJECT MEMORY":
    - "Search this project’s memory", "the same search as Memory, limited to this project", button "Open Memory" (opens Memory, Search). Src `recall <q> --project <p> --json` [in].
    - "Progress checkpoints", "done: ... · doing: ... · next: ...". Src `card --json` `project_state` (checkpoints) [in].
    - "People on this project", "Designer: prefers annotated screenshots · Client contact: signs off designs". Src: notes of kind person, saved with `remember --kind person`. Reading them by kind has no command (G-22).
- **PO-7 Expander "Report, runs and cost"**, "14 unattended runs today · 3 bots with the runner on":
  - "Unattended runs by this project’s bots", "14 today · counted per bot, not per project · 3 bots with the runner on". Src `runner status --json` [next] `today`, `enabled`, for the seats that hold this project's tasks.
  - "Status report", "one page for a client or a board, every figure linked to its source", button "Generate". Tag sample. NOT SCOPED, needs a contract; nothing on main generates a report.
  - "Cost and token use", "Not available yet". Tag sample. DRAFT MU-1; nothing records spend or tokens.
- **PO-8 "Coming later (1)"**: "Goal to plan", "describe an outcome in plain words; Memev drafts the brief and the plan for your approval". PLANNED SO-C-A AC03, SO-REQ-045.

#### Tab: Plan (`logic3.js:53-62`)

- Sentence: "1 of 4 gates passed. Next: design signed off." No source (G-2).
- **PP-1 Section "ROADMAP"**, tag "Sample, not built yet". Months "Oct" to "Mar". Lanes "Design" ("Design system and page designs"), "Content" ("Audit, rewrite, migrate"), "Build" ("Home, checkout, search"), "Launch" ("Readiness and cut-over"), and an amber today line. A row "Gates": "G1 Brief approved" (passed), "G2 Design signed off", "G3 Build verified", "G4 Launch".
  - Lane panel: tag sample, why (what the lane covers), fact "Dates": "A date appears only when it has a source: a decision, a contract or a calendar. Memev does not invent dates or an outlook." PLANNED SO-C-B AC03, SO-1.05 req 3.
  - Gate panel: tag sample, state ("Passed" or "Open · holds 5 tasks of the checkout build"), facts "What must be true", "What it waits for", "Why no date". PLANNED SO-1.05 req 3, SO-C-B AC03; no code on main.
- **PP-2 Expander "Architecture"**, "10 parts, 1 blocked · system and sub-system diagrams". Inside, section "SYSTEM ARCHITECTURE", tag sample, three layers: "People and channels" (Visitors, Editors), "Application" (Web site, Checkout, Content API, Search), "Data and outside services" (Order store, Content store, Payment provider and Analytics with dashed outlines). Caption "A dashed outline is outside your trust boundary. Click a part to see its sub-system."
  - Part panel: tag sample, state, why, group "SUB-SYSTEM" (for Checkout: Cart, Payment adapter, Order writer, Receipt mailer) and group "INTERFACES". PLANNED SO-A-09 (outside parts SO-A-09 C3); no architecture entities on main. The status under each part would come from its linked tasks (`memev_project_status`).
- **PP-3 Expander "Delivery and development plans"**, "scope, schedule, workstreams, release stages". Rows, each tag sample: "Delivery plan" (PLANNED SO-A-09 C1, C2), "Development plan" (PLANNED SO-A-09 C1, C2), "Interfaces" (group "SAMPLE INTERFACES"; PLANNED SO-A-09 C3).

#### Tab: Work (`logic3.js:63-67`, tasks `:105-110`)

- Sentence: "Home page build is in progress. Checkout page is blocked." Src: `work status <project> --json` `active`, `blocked`.
- **PW-1 Section "TASKS"**, a list, heads "Task", "State", "Held by", "Checks", "Show all (31)". Rows: "Checkout page" Blocked, codex, 0 of 5; "Home page build" In progress, claude-code, 1 of 4; "Accessibility review" Ready, none, 0 of 3; "Information architecture" Checked, codex, 2 of 2; "Content audit" Checked, claude-code, 3 of 3.
  - Src: `memev_project_status` [next] / `work status <project> --json`: task `title`, `state` (READY, IN_PROGRESS, BLOCKED, VERIFIED and the rest, shown in plain words), `holder`, `deps`, `blocked_reason`; checks from `memev_contract_status` for the task's `contract`.
  - Panel: state; why ("Blocked: no payment test key. The key was requested on 3 Nov." or "Held by claude-code · held, renewed 1 min ago."); facts "Depends on", "Held by", "Checks"; buttons and a group "MORE ACTIONS".
  - "renewed 1 min ago" needs the lease's last heartbeat time, which the task fields do not carry (G-23).
  - Act, "Give to another bot": `memev_task_reassign` [next]. No flow drawn. Confirm (rule).
  - Act, "Cancel with a reason": confirm (sheet "Cancel "<task>"?"), then `work cancel <project> <task> --reason <why>` / `memev_task_cancel` [next].
  - Act, "Edit title, priority and labels", button "Edit": `memev_task_update` [next]. Confirm (rule).
  - Act, "Mark as checked", "the lead only, with evidence, on this computer", button "Mark": confirm (sheet "Mark "<task>" as checked?"), then `memev_task_verify` [next], local only.
  - Act, "Change the scope", "evidence bound to the old scope goes out of date", button "Amend": `memev_scope_amend` [next]. Confirm (rule).
  - Act, "Pause or resume the whole project", "every task in it stops or carries on", button "Pause": `work pause|resume <project> --reason` / `memev_project_pause`, `memev_project_resume` [next]. Confirm (rule).
- **PW-2 Expander "How bots take and hold tasks"**, technical detail on only, "one bot holds a task at a time; a silent bot loses its hold". Rows "Plan tasks with dependencies" ("done by the lead agent", `memev_task_plan`), "Claim with a lease" ("one agent holds a task at a time", `memev_task_claim`), "Heartbeat, report, release" ("a silent agent loses its lease", `memev_task_heartbeat · memev_task_report · memev_task_release`). Explanation rows; the app runs none of these.
- **PW-3 "Coming later (1)"**: "Skills and capacity", "what each agent is good at and how much it can take". PLANNED SO-05-B AC01, SO-C-B AC01.

#### Tab: Risks and decisions (`logic3.js:68-82`)

- Sentence: "1 task is blocked. 2 risks are open." The first clause comes from `work status <project> --json` `blocked`. The second has no source (G-3).
- **PK-1 Section "BLOCKED RIGHT NOW"**: "Checkout page", "no payment test key", button "Resolve" (amber). Panel as OV-2. Src `work status <project> --json` `blocked`.
- **PK-2 Section "WAITING FOR YOUR DECISION"**, tag "Sample, not built yet". Rows "Hosting: managed or self-run?" ("recommendation: managed · consequence: a monthly cost") and "Launch with or without search?" ("recommendation: without · proceeds either way"), each with "Decide". PLANNED SO-A-05, SO-C-A AC04; work status has an `approvals` field, nothing creates an approval yet.
- **PK-3 Section "RISKS"**, tag sample, heads "Risk", "Owner", "State", "Show all (3)". Rows "Payment key arrives late", "Header fails contrast", then "Content migration overruns". Panel facts "Impact", "Mitigation", "Owner", "State", "Linked work". PLANNED SO-A-12 C1; no RAID schema on main.
- **PK-4 Expander "Decisions already made (3)"**, "mock the payment provider · drop the blog migration · static site generator". Rows "Mock the payment provider" ("5 Nov · by you"), "Drop the blog migration" ("5 Nov · by you"), "Static site generator over a hosted CMS" ("3 Nov · ranked head to head"). Src `card --json` [in] `recent_decisions`; `recall --project <p> --json` for notes of kind decision.
- **PK-5 Expander "Scope changes (2)"**, "what changed, and which evidence it made out of date":
  - Rows "Blog migration removed" ("5 Nov · made 2 pieces of evidence out of date") and "Search moved after launch" ("3 Nov · made nothing out of date"). No command lists past scope changes (G-4).
  - "Record a scope change", "evidence bound to the old scope goes out of date", button "Amend": `memev_scope_amend` [next]. Confirm (rule).
  - "What does a change make out of date?", button "Show impact": `work impact <id> --project <p>` [next]. Read.
- **PK-6 "Coming later (3)"**: "Assumptions" (PLANNED SO-A-12 C1), "Issues and dependencies" (PLANNED SO-A-12 C2), "Full change history" (PLANNED SO-C-C AC01).

#### Tab: Results (`logic3.js:83-88`)

- Sentence: "6 of 17 checks pass. 1 check is out of date." Src: `memev_contract_status` `passed`, total, for the project's contracts; `work status <project> --json` `stale_evidence`.
- **PS-1 Page action "Re-check everything"** (amber). Act: `verify` with no id (every open contract) / `memev_contract_verify` [next]. Confirm (rule).
- **PS-2 Section "OUT OF DATE"**: "Header test", "the header changed after the check passed", button "Re-check". Panel as OV-4.
- **PS-3 Section "CHECKS BY TASK"**, heads "Task", "Result", "Passed". Rows "Content audit" Passed 3 of 3, "Information architecture" Passed 2 of 2, "Home page build" Not checked yet 1 of 4, "Checkout page" Failed 0 of 5 (red), "Accessibility review" Not checked yet 0 of 3. Three rows carry a second line "deliverable: <name> · <version> · <state>".
  - Src: `memev_contract_status` / `contract status <id>` [next]: per task PASS, FAIL or NOT VERIFIED, `passed`, `last_verified`. The deliverable lines have no source (G-5).
  - Panel: state "<result> · <passed> · last checked <when>"; why "Every check writes a receipt: what ran, what passed, when."; buttons "Open the receipt" (reads `contracts/receipts/<id>.md`, named by `contract status`) and "Re-check" (`verify <id>` [next], confirm (rule)); facts "Deliverable", "What exists today" ("A finished task records the hash of what it produced. Nothing lists, previews or versions deliverables yet."); group "NOT BUILT YET": "Preview Markdown, code, diagrams and images", "Compare versions". PLANNED SO-A-06 C2 to C5, SO-C-C AC03.

#### Tab: Outputs (`logic3.js:49-52`, `tpl.html:205-221`)

- Sentence: "3 outputs: 1 accepted, 1 reviewed, 1 draft." It counts sample outputs (G-5).
- **PU-1 Section "WHAT THIS PROJECT HAS PRODUCED"**, tag "Sample, not built yet". The output list on the left and the preview canvas on the right. Before a pick: "Pick an output on the left to preview it here. You can then save a copy or open its folder." The canvas is OP-13. Reason as OP-12: only `artifact_hash` exists.
- **PU-2 Note**: "The files stay in this project’s folder on your computer. Save a copy puts one where you choose; Show in folder opens the folder. A page made by a bot is previewed in a sealed frame that cannot reach the internet."
- **PU-3 "Coming later (2)"**: "Versions side by side" (PLANNED SO-A-06 C2 to C5), "Outputs from every project in one place" (PLANNED SO-C-C AC03).

### 2.4 Bots and agents (`logic4.js:3-59`)

- Menu "Bots and agents" (warn diamond in the normal state). Title "Bots and agents". Tabs "Team" (warn diamond in the normal state), "Connect", "Access". Messages moved to Operations (05-decisions:26, :92).
- What is this? words (`logic2.js:50`): "Bot or agent", "Lead", "Access key", "Secure link".

#### Tab: Team (`logic4.js:49-58`)

- Sentence: "5 bots and agents. 4 live. ops-bot has been quiet for 48 minutes." All clear: "5 bots and agents. All 5 live." Src `bus roster --json` [in] `seats[].state`, `minutes_since`.
- **BT-1 Page action "Add a bot or app"** (amber). Opens the Connect tab.
- **BT-2 Section "YOUR TEAM"**, heads "Bot or agent", "State", "Working on". Rows: "ops-bot" ("bot, connected by address", "gone quiet · 48 min", "nightly stock sync"), "codex" ("Codex", "live · 2 min", "Website relaunch: checkout page (blocked)"), "support-bot", "claude-code · lead" ("Claude Code", "live", "Website relaunch: home page build"), "sales-bot".
  - Src: `bus roster --json` [in] per seat `seat`, `client`, `device_kind`, `state` (live, stale, never joined, removed; shown as live, gone quiet), `minutes_since`, `operator`. "Working on": the task this seat holds, from `memev_project_status` [next] `holder`, or the seat's bus `claims`.
  - Panel: BOT-P.
- **BOT-P The bot panel** (`logic2.js:18-30`), also opened from Overview and Health:
  - Title "<name>" or "<name> · lead". State "live · seen just now" or "gone quiet · last seen 48 min ago".
  - Why: "Working on: <task>." or "It has not reported in for 48 minutes. Its last task was the nightly stock sync."
  - Buttons: "Say hello" and "Follow its messages"; for a quiet bot "Ask it to report in" and "Follow its messages".
    - "Say hello" and "Ask it to report in": `bus hello <seat>` [next]. Confirm (rule).
    - "Follow its messages": opens Operations, Workspace on that bot's pane. Src as OP-7 (waits for SO-N-15). Never `bus watch`.
  - Facts: "App" (`client`), "Computer" (`device_label`), "Working on", "Last message" (`last_message`), "Unread" ("2", "1, on hold", "0"; `bus unread --peek` waits, G-8), "May" ("read, change · limited to projects/website"; from `token list`, text only, G-24).
  - Group "ACTIONS FOR THIS BOT":
    - "Comms check", "who answered, who did not and why", button "Check": `bus check --to <seat> --json` / `bus_check` [next]. Shows `answered`, `missing[].reason`. Confirm (rule).
    - "Rename", "history follows the new name", button "Rename": `bus rename OLD NEW` [next]. Confirm (rule).
    - "Make this bot the lead" (button "Make lead"), or "This bot leads" (button "Change lead"), "other bots take tasks from the lead": `bus operator <seat>` / `bus_set_operator` [next], this computer only. Confirm (rule).
    - "Background runner", "off · when on, the app takes one unattended turn per lead task, capped per day", button "Switch on": `runner on <seat> --max-runs-per-day N` [next]; state from `runner status --json` `enabled`. A daily cap is required. Confirm (rule).
    - "Its access", "key, limits and expiry", button "Open Access": opens the Access tab.
  - Terminal box for claude-code, codex and sales-bot, role "lead · sample terminal, not built". Tag: sample. Reason (plan line): "NOT SCOPED · needs a session-host contract with its own security review."
- **BT-3 "Coming later (6)"** (`logic4.js:52-58`): "Pause all bots" (NOT SCOPED; today `work pause <project>` and `runner off <seat>`), "Watch a bot’s terminal live" (NOT SCOPED; a sample terminal in the panel), "Richer states" (PLANNED SO-C-B AC01), "Capability records per agent" (PLANNED SO-05-B), "A project lead separate from the overall lead" (PLANNED SO-L-A), "Agent identity for teams" ("BIND: BUSINESS AND CORPORATE · VERIFY: FREE", `agentid bind|verify|setup`).

#### Tab: Connect (`logic4.js:17-40`)

- Sentence: "2 of 4 AI apps on this computer are connected. Web access is off." First run: "1 of 4 AI apps on this computer is connected. Web access is off."
  - Src: `clients` [in] (text only); web access from `cloud status` [next] (text only).
- **BC-1 Page action "Connect an app"** (amber). Opens the sheet BC-S1.
- **BC-2 Note**, first run only: "Connect a second app to share memory between them. Until then Memev remembers for this one app."
- **BC-3 Section "AI APPS ON THIS COMPUTER"**, heads "App", "State". Rows "Claude Code" (connected, "found on this computer · memory carries over fully: at start, on every prompt, before compaction", button "Disconnect"), "Codex" (connected, "fully", "Disconnect"; first run: not connected, "Connect"), "Cursor" (not connected, "at the start of a session only", "Connect"), "Claude Desktop" (not connected, "when the app follows its instructions", "Connect").
  - Src: `clients` [in], its lines as printed (marker, name, state). It prints no id, so the app cannot yet run `connect` for a row (CLI-CONTRACT.md; G-11). The "memory carries over" text has no core field (G-11).
  - Act, "Connect": opens BC-S1. Act, "Disconnect": confirm (sheet "Disconnect <app>?"), then `disconnect <app>` [next].
  - Panel: state; why "Before it connects an app, Memev shows exactly what it will change, and keeps a copy of the app’s old settings."; the row's button; facts "Memory carries over", "Old settings" ("copied before any change").
- **BC-4 Expander "14 more apps Memev can connect"**, "not found on this computer". Note: "VS Code, Windsurf, Zed, Gemini CLI, opencode, Copilot CLI, Cline, Roo, Continue, Kiro, JetBrains AI, Amp, Goose and Aider. How much memory carries over varies by app." Src `clients` rows that are not found. The mockup counts 18 apps; the inventory counts 17 (`clients.mjs`). The app shows what the core prints.
- **BC-5 Expander "Web apps and other bots"**, "none connected · web access is off":
  - Section "SECURE LINK FOR CLOUD AI":
    - "Web access", "off · cloud AI apps cannot reach your memory until you switch it on", button "Switch on": opens BC-S2.
    - "Provider", "automatic, Tailscale, Cloudflare or a quick tunnel", button "Choose": `cloud use auto|tailscale|cloudflare|quick` [next]. No flow drawn. Confirm (rule).
  - Section "WEB AI APPS":
    - "ChatGPT", "needs web access switched on; then shows an address and a code and waits until the app is seen", button "Connect": `cloud connect chatgpt` [next]. Confirm (rule). The consent page on this computer approves it.
    - "Claude", "the same; you approve each app on this computer", button "Connect": `cloud connect claude` [next].
    - "Web apps connected", "0 · no limit on any plan". Src `plan --json` [in] `connectors_counted`.
  - Section "BOTS AND OTHER AGENTS": "Connect a bot by its address", "the bot gets its own key and its own place on the team; guided set-up is not built yet", button "Connect a bot". Tag sample. Reason: any app or bot that accepts a connector address can connect over the secure link today, by hand; the guided set-up is not built (PLANNED, guided bot set-up).
- **BC-6 Expander "Get help setting up"**, "a helper sets Memev up with you; every command is on an allow-list and logged". Row "Let a helper set Memev up with you", "you start it with a code; every command is on an allow-list and logged; you can stop it", button "Start". Src `setup remote --code C`, `setup remote stop`, `setup remote log`. These are on main but not in COVERAGE (G-13).
- **BC-7 "Coming later (4)"**: "Grok: the app, the web and the terminal" (vendor documentation only, nothing tested; MEM-045 unlanded), "More assistants around the world" (NOT SCOPED; vendor documentation, not a test), "Folders and GitHub repositories per project" (PLANNED SO-O-A, SO-O-B, SO-A-07), "Readiness of every tool" (PLANNED SO-1.06).
- **BC-S1 Sheet "Connect an app"** (`logic5.js:137`): note "Before it connects an app, Memev shows exactly what it will change, and keeps a copy of the app’s old settings. You can disconnect at any time."; rows "Cursor" and "Claude Desktop", each "Show changes"; main button "Connect" (amber).
  - Act, "Show changes": `connect <app> --dry-run --print` [next]. Read.
  - Act, "Connect": `connect <app>` [next]. The sheet is the confirmation. Blocked by G-11.
- **BC-S2 Sheet "Let AI apps on the web reach your memory?"** (`logic5.js:138`): note "Off unless you switch it on. You need it for ChatGPT and Claude on the web, and for your other computers."; rows "Automatic" ("Memev picks a tunnel that works here"), "Tailscale" ("your own private network"), "Cloudflare" ("your own tunnel"), "Quick tunnel" ("a temporary address, for trying it out"); notice "The tunnel is yours. Each web app still has to be approved by you, on this computer, before it can read anything."; main button "Switch on" (amber).
  - Act: `cloud use <provider>` then `cloud on` [next]. The sheet is the confirmation.

#### Tab: Access (`logic4.js:41-47`)

- Sentence: "6 keys: you and five bots. None has expired." Src `token list` [next] (text only, G-24).
- **BA-1 Page action "Issue a key"** (amber). No flow drawn. Act: `token issue --agent --scopes --paths --ttl [--remote]` [next]. Confirm (rule).
- **BA-2 Section "WHO CAN GET IN, AND WHAT EACH MAY DO"**, heads "Holder", "May", "Limited to", "Show all (6)". Rows "you (owner)" ("read, change, manage", "everything"), "claude-code", "codex", "sales-bot", "support-bot" ("read, change", "projects/<name>"), then "ops-bot" ("read").
  - Src: `token list` [next]: agent, scopes, paths, local or remote, expiry, revoked. Text only (G-24).
  - Panel: why "What this key lets its holder do, and where."; buttons "Revoke" and "Replace with a new key"; facts "May", "Limited to", "Where it works" ("this Mac"), "Expires" ("never", "90 days"); group "RULE FOR BOTS ON THE WEB": "A bot connected over the web can message only the lead", "bots on this computer can message each other".
  - Act, "Revoke": confirm (sheet "Revoke the key for <holder>?"), then `token revoke` [next].
  - Act, "Replace with a new key": confirm (sheet "Replace the key for <holder>?"), then `token rotate` (G-24).
- **BA-3 Note**: "Bots on this computer can message each other. A bot connected over the web can message only the lead." The rule is set by hand in `.memev/config.json` (`bus cloud_tasks`) today; the app shows it as text.

### 2.5 Memory (`logic4.js:62-97`)

- Menu "Memory". Title "Memory". Tabs "Search", "Review (7)", "Browse", "History", "Health" (`logic4.js:63`). The count in "Review (7)" comes from `review list --json` [in] `inbox` length.
- What is this? words (`logic2.js:51`): "Memory", "Review", "Claim".
- The Health tab is the 6 Nov decision (05-decisions:163-165). `build.sh`'s prop list does not name it; the code draws it.

#### Tab: Search (`logic4.js:92-96`)

- Sentence: "1,284 memories. 7 to review." Src `status --json` [in] `index.notes`; `review list --json` `inbox`.
- **MS-1 Page actions** "Import" (secondary, opens MEM-S2) and "Save a memory" (amber, opens MEM-S1).
- **MS-2 Search field** "Search your memory", chips "This project: website" and "All projects". Src `recall <words> --project <p> --json` or `--all-projects` [in], words passed one argument each (PAGES.md). Fields `hits[{path, heading, line, snippet, score, project, related}]`, `total`, `offset`, `next`.
- **MS-3 Section "MOST RECENT"**, a list, "Show all (1,284)". Rows: the snippet as title ("Mock the payment provider until the test key arrives"), the path under it ("decisions/payments.md"), the project on the right ("website"). Technical: "section Provider · score 0.92".
  - Src after a search: `recall --json` hits. With no search, "most recent" has no command (G-27). "Show all (1,284)" pages with `offset` and `next`.
  - Panel: title the path; why the snippet; buttons "It helped" and "It did not help"; facts "Project", "Where it came from" ("written by claude-code on 3 Nov; used 6 times"), "Why recall returned it" ("it matched your words; your answer here tunes later recall"), "Linked to"; group "LINKS": "Show the chain between two things", "for example header → accessibility review", button "Explain". Technical plan line: "matched words and weights: ...".
  - Src for the panel: `memev_provenance` [next] (where it came from, uses); `why <query> --item <path#heading> --json` / `memev_why` [next] (matched words and multipliers); `memev_graph` [next] (linked to).
  - Act, "It helped" and "It did not help": `memev_feedback` [next]. Confirm (rule).
  - Act, "Explain": `memev_explain` [next]. Read.
  - COVERAGE also lists `memev_link` (record a link). The mockup draws no button for it.
- **MS-4 Expander "Advanced"**, technical detail on only, "head-to-head rankings · the project name for this folder":
  - "Head-to-head rankings", "Static site generator 1560 · Hosted CMS 1490". Src `memev_tournament_standings` [next]. Read.
  - "Project for this folder", "website, from the git top level", button "Pin a name". Src `project` [in]. Act `project set <name>` (G-26). Confirm (rule).
- **MS-5 First run**: sentence "Nothing saved yet."; empty state "Memory fills as you work in a connected app. You can also bring in existing notes or AI history." with "Import" (amber, opens MEM-S2). Src `status --json` `index.notes` = 0.
- **MEM-S1 Sheet "Save a memory"** (`logic5.js:140`): note "If a save is refused, Memev says why (for example: a greeting, a duplicate, a secret). You can save it anyway."; fields "What to remember", "Kind" ("decision · gotcha · fact · pattern · win · person · preference"), "Why it matters" ("kept with the memory and shown on recall"), "Review by" ("a date or a number of days"); main button "Save" (amber).
  - Act: `remember <text> --kind K --why W --review-by DATE|DAYS --project P` / `memev_remember` [next]; "save it anyway" adds `--force`. The sheet is the confirmation. The core always prints JSON; a refusal says why.
- **MEM-S2 Sheet "Import"** (`logic5.js:141`): note "Everything is previewed before anything is kept, and each import can be undone."; rows "Folders of notes" ("Markdown and text", "Choose"), "AI app history on this computer" ("previewed before anything is kept", "Preview"), "A chat export (.zip)" ("Choose"), "Earlier imports" ("each can be undone", "Undo"); no main button.
  - Act: `import --path DIR --preview`, then `--yes [--keep ID,ID]`; `import --ai-history --preview`; `import --export FILE.zip --preview`; `import list`; `import undo [ID]` [next]. Each keep and each undo needs a confirmation (rule). No flow drawn.

#### Tab: Review (`logic4.js:78-81`)

- Sentence: "7 memories to review. Keep what is still true, drop what is not." Src `review list --json` [in] `inbox`.
- **MR-1 Filter field** "Filter the review list", chips "All (7)", "Likely junk (7)", "Duplicates (3)", "Never recalled (41)". Src: `review list --json` for All; `audit --json` [in] `likely_junk`, `counts.duplicates`, `counts.never_recalled` for the others. App filter.
- **MR-2 Section "TO REVIEW"**, a list, "Show all (7)". Rows "Staging URL is temporary" ("short-lived · review by 12 Nov") and "Old logo guidance" ("flagged unhelpful twice"), each with "Keep" and "Drop".
  - Src: `review list --json` / `memev_review` [in] `inbox[]`: text, why flagged, review date.
  - Act, "Keep": `review keep <item>` [next]. Confirm (rule).
  - Act, "Drop": confirm (sheet "Drop "<memory>"?"), then `review drop <item>` [next].
  - COVERAGE also lists `review reset`; it appears only in History's reason text.
- **MR-3 Note**: "1,284 memories in all. 0.5% look like junk." Src `audit --json` [in] `memories`, `junk_share`.
- Empty (PAGES.md): "Nothing is waiting for review."

#### Tab: Browse (`logic4.js:82-88`)

- Sentence: "1,284 memories, by kind." Src `status --json` `index.notes`.
- **MB-1 Filter field** "Filter by name", chips "Notes", "Claims", "Checkpoints", "Session journals". App filter.
- **MB-2 Section "NOTES"**, a list, "Show all (1,284)". Rows "decisions/payments.md", "gotchas/header.md", "work/checkpoints/website.md", each with "updated today" or "updated yesterday". Rows open nothing in the mockup.
  - Src: `memev_list <folder>` [next], 50 per page; a note opens with `memev_get` [next]. COVERAGE places `memev_write` and `memev_append` on a note's detail; the mockup draws no note detail, so the app needs one (not drawn). The "updated" time shows only if `memev_list` returns one.
- **MB-3 Expander "Claims (3)"**, "what is recorded as fact, contested or a theory". Rows "The header meets AA contrast" (contested), "Checkout needs a test key" (fact), "Launch slips if design is late" (theory). Src `claims --status --subject` / `memev_claims` [next]: subject, predicate, object, status. `memev_claim` (add evidence) has no button.
- **MB-4 Expander "Progress checkpoints"**, "done: content audit, structure · doing: home page build". Rows "Done", "Doing", "Next". Src `card --json` `project_state` [in]; written by `checkpoint` / `memev_checkpoint`.
- **MB-5 Expander "Session journals (2 today)"**, "claude-code 38 turns · codex 12 turns". Rows "claude-code · today 16:02" ("38 turns"), "codex · today 15:10" ("12 turns"). Src `memev_list work/sessions` [next], then `memev_get`; the app counts turns.
- **MB-6 "Coming later (1)"**: "Findings", "something an AI noticed that stays short of a fact until confirmed". PLANNED; the finding model is built, nothing reads it.

#### Tab: History (`logic4.js:89-91`)

- Sentence: "Every change to your memory is recorded and kept."
- **MH-1 Note**: "Each change is written to a record that cannot be quietly altered, and saved as a version. A screen that lists the changes is not built yet."
- **MH-2 Section "HISTORY AND UNDO"**, tag "Sample, not built yet". Rows "Every change to your memory, newest first" ("who wrote it, when, from which app") and "Undo a change" ("a dropped memory is kept, not erased", button "Undo"). Reason: "A read-only view of every change, and a way to take one back. Today an import can be undone and a review can be reset; nothing else has an undo." PLANNED, docs/SOLUTIONS.md local read-only view. No command lists the change record.

#### Tab: Health (`logic4.js:67-77`)

- Sentence: "Your memory is healthy. Last measured 2 minutes ago." The "last measured" time has no source (G-19).
- Every row's detail panel is tagged sample, with "Measured on this computer. Sizes and memory use are reported today; timings need the speed contract." and "PLANNED · timings and the daily record arrive with SR-1.35" (`logic4.js:68`). The charts and lists themselves carry no tag.
- **MX-1 Chart "How close to each limit"**, unit "Share of the level where you would notice a slowdown". Rows "Start-up" 0.4 s, "Pause while saving" 40 ms, "Memev's memory use" 96 MB, "Largest backup file" 3 MB, "Disk space used" 22%. Levels: start 5 s, pause 800 ms, memory 1 GB, backup file 90 MB (from VAULT-GROWTH-MODEL.md).
  - Src: memory use `status --json` [in] `memory.rss_mb`; disk and backup file from `doctor --json` [in] ids `disk`, `backup-vault` (`detail`). Start-up and pause: none (G-19).
- **MX-2 Chart "Did memory reach your prompts"**, unit "Today, across connected apps". Rows "Memory added" 34, "Nothing relevant" 3, "Too slow, skipped" 1. Src: none; "prompt hook counts, planned with SR-1.35" (G-19).
- **MX-3 Section "SPEED"**: "Start-up" ("until Memev answers · slows at 3 s, too slow at 5 s", 0.4 s), "Finding a memory" ("typical", 3 ms), "Finding a memory, slowest" ("slowest 1 in 20 · a prompt gives up at 1.5 s", 9 ms), "Saving a memory" ("typical", 8 ms), "Longest pause while saving" ("slows at 400 ms, too slow at 800 ms", 40 ms), "Finding the bots on the message bus" ("target under 50 ms", 2 ms). The mockup cites `scripts/bench/speed`, a bench script, not a core command (G-19).
- **MX-4 Section "SIZE AND LIMITS"**:
  - "Memories", "3,210 searchable sections", 1,284. Src `status --json` `index.notes`, `index.sections` [in].
  - "Search index on disk", "can be rebuilt from your notes", 9 MB. Src `compact --dry-run --json`, refused while Memev runs (G-20).
  - "Change record", "kept for good, grows slowly", 38 MB. Src `compact --dry-run --json` (G-20); `status --json` `oplog.bytes` is the nearest live figure.
  - "Memev's memory use", "slows at 600 MB, too slow at 1 GB", 96 MB. Src `status --json` `memory.rss_mb` [in].
  - "Largest backup file", "warns at 40 MB, GitHub refuses over 100 MB", 3 MB. Src `doctor --json` `detail` of the backup checks [in], if it carries the size.
  - "Backup history", "warns at 800 MB", 120 MB. Src `compact --dry-run` (G-20).
  - "Free disk", "warns under 10 GB", 410 GB. Src `doctor --json` id `disk` [in].
- **MX-5 Section "QUALITY"**:
  - "Never recalled", "kept, and listed under Review", 41. Src `audit --json` `counts.never_recalled` [in].
  - "Look like junk", "of all memories", 0.5%. Src `audit --json` `junk_share` [in].
  - "Changed while Memev was stopped", "picked up at the last start", 2. Src `status --json` `recovered` [in].
  - "Integrity sweep", "every note checked against its record · last finished 40 minutes ago", "Clean". The mockup cites `memev verify`, which verifies contracts. No command reports a sweep (G-21); `doctor --json` ids `oplog` and `drift` are the nearest.
- **MX-6 Note**: "Growth over time appears here once Memev has recorded a few days of measurements."

### 2.6 Checked work (`logic4.js:100-102`)

- Menu "Checked work". Title "Checked work". No tabs. Header rule SH-15.
- Sentence: "Checked work is on. 42 of 54 checks pass. Last checked 12 minutes ago."
  - Src: "on" from `contracts status` (G-12); counts from `memev_contract_status` [next] `passed` and total over all contracts; "last checked" the newest `last_verified`.
- What is this? words (`logic2.js:52`): "Contract", "Check", "Receipt".
- **CW-1 Page action "New contract"** (amber). Opens CW-S1.
- **CW-2 Section "CONTRACTS"**, heads "Contract", "State", "Checks passed". Rows "Home page" (open, 1 of 4), "Content audit" (done, 3 of 3), "Checkout" (draft, not signed).
  - Src: `contract list` / `memev_contract_list` [in] `contracts[]` (drafts, open, done; title, status); per contract `contract status <id>` / `memev_contract_status` [next]: per task PASS, FAIL or NOT VERIFIED, `signature` ok or BROKEN, `last_verified`, `passed`, `remaining`.
  - Panel: state "<status> · <passed> checks pass"; why ("A draft. Nothing is held to it until it is signed." or "AI work on this contract counts as done only when its checks pass."); facts "Signature", "Last checked", "Tasks"; group "MORE ACTIONS": "Approve a contract signed elsewhere" ("only here, only you", button "Approve") and "Amend" ("a signed contract changes only as a new signed version", button "Amend").
  - Act, draft, "Sign": `contract sign <id>` / `memev_contract_sign` [next], with this machine's key. Confirm (rule).
  - Act, "Amend": `contract amend <id>` / `memev_contract_amend` [next], drafts version N+1. Confirm (rule).
  - Act, "Re-check now": `verify <id>` / `memev_contract_verify` [next]; writes a receipt. Confirm (rule).
  - Act, "Open receipts": reads `contracts/receipts/<id>.md`, named by `contract status`.
  - Act, "Approve": `contract approve <id>` / `memev_contract_approve` [next], this computer and admin only. Confirm (rule).
  - Empty (PAGES.md): "No contracts yet."
- **CW-3 Expander "Checked work is on"**, "new installs start with this off; nothing blocks until you switch it on":
  - "AI work is held to signed tasks with checks", "switch it off and nothing is held or blocked", button "Switch off": confirm (sheet "Switch off Checked work?"), then `contracts off`; "Switch on" runs `contracts on`. Not in COVERAGE (G-12).
  - Technical detail on only: "Block a push while a contract is open", "advanced · installs a local pre-push gate", button "Install gate": `contract hook` / `gate --workdir DIR` [next]. Confirm (rule).
- **CW-S1 Sheet "New contract"** (`logic5.js:139`): note "A contract is a signed list of tasks, each with a check a machine can run."; rows "From a request in plain words" ("Memev drafts tasks with checks a machine can run", button "Draft"), "From a short scope" ("Memev breaks it into tasks", "Decompose"), "Blank draft" ("New"); no main button.
  - Act: `contract from-request` / `memev_contract_from_request`; `contract decompose` / `memev_contract_decompose`; `contract new --title --project --workdir --base` / `memev_contract_draft` [next]. Each makes a draft only. Confirm (rule).

### 2.7 Settings (`logic5.js:3-66`)

- Menu "Settings". Title "Settings". Tabs "General", "Plan and computers", "Storage and backup", "Advanced", "Help and support" (`logic5.js:4`, 05-decisions:18). "Help and support" is also the sidebar foot entry; while it is open no menu entry is selected.
- What is this? words (`logic2.js:54`): "Plan", "Backup", "Technical detail".

#### Tab: General (`logic5.js:56-65`)

- Sentence: "Dark theme. Starts when you sign in. Version 0.1.0." Src: theme (app); start at login from `service` status [next] or `doctor --json` id `service`; version `status --json` `version`.
- **SG-1 Section "GENERAL"**, a list:
  - "Appearance", "dark theme", button "Switch to light" / "Switch to dark". App setting.
  - "Technical detail", "off · the engine’s own words and the command behind each item", button "Switch on" / "Switch off". App setting (SH-23).
  - "Start at login", "on · Memev starts when you sign in", button "Switch off". Act `service remove`, or `service install` to switch on [next] (launchd or systemd). Confirm (rule).
  - "Update", "version 0.1.0 · no release published yet · a release file is checked against its SHA-256 before it is installed", button "Check". State from `doctor --json` id `update`. Nothing checks for a release (G-16). Installing is `update <tarball> --sha256 HEX` [next], confirm (rule).
  - "How you mostly work", "I run a business with bots · the app opens on the Executive overview", button "Change". App setting, asked once at first run.
  - "Privacy", "anonymous milestone report: off · nothing is sent unless you switch it on", button "See what would be sent". Src `report status --json` [in] `enabled`, `asked`. Act `report show` [next] (read); `report on` / `report off` [next], confirm (rule).
- **SG-2 Expander "Set-up checklist: 6 of 7 done"**, "installed · second app joined · two apps, one memory · first message · invite not sent". Rows "Installed", "Second app joined", "First recall across apps", "Two apps, one memory", "First bus message", "Invite sent", "Comms activated", each "yes" or "no" with an ok or hollow glyph; then "Run setup again", "walk through the setup steps again", button "Start".
  - Src: `onboarding --milestones --json` (`installed`, `second_app_joined`, `first_cross_app_recall`, `two_apps_one_memory`, `first_bus_message`, `invite_sent`, `comms_activation`, each with `at`). "Run setup again": `onboarding --json` (13 items `{status, step, detail}`). Neither is in COVERAGE (G-14).
- **SG-3 "Coming later (1)"**: "Notifications and a morning summary", "what needs you, which bot went quiet, what was checked overnight". PLANNED SO-C-C AC05.

#### Tab: Plan and computers, free plan (`logic5.js:10-15`)

- Sentence: "Personal, free. 1 computer. No web apps connected." Src `plan --json` [in] `plan`, `computers_used`, `connectors_counted`.
- **SP-1 Section "THIS COMPUTER"**: "iMac (this Mac)", "holds your memory", ok glyph. Src `devices list --json` [in] `label`, `kind`, `status`; `plan --json` `computers_used`, `computers_allowed`.
- **SP-2 Section "USE MEMEV ON YOUR OTHER COMPUTERS"**:
  - "Personal Plus", "$19 a month or $190 a year · 3 computers on one memory · first 200 customers keep $12 or $120 · draft price, not an offer", button "Upgrade" (amber): opens SP-S1.
  - "Personal Pro", "$49 a month or $490 a year · 10 computers · draft price, not an offer", button "Choose Pro": opens SP-S1.
  - The prices are app text marked draft. `upgrade` [next] prints the plans and the checkout address.
- **SP-3 Expander "What you get today, and what is planned"**, "one memory on up to 3 computers works today". Rows "One memory and one message bus across your computers" ("works today, over the secure link"), "Automatic merge, hosted sync, hosted backup and relay" ("planned; not part of what you pay for today"), "Already bought?" ("enter the licence key", button "Activate": `license activate <key>` [next], confirm (rule)).
- **SP-4 Expander "Move to a second computer instead"**, "free: one computer at a time, nothing lost". Row "Restore your memory from your backup", "on the new computer, during setup". Src `init --from <backup URL>`, run by the first launch only (COVERAGE: `init` is not in the app).
- **SP-5 "Coming later (3)"**:
  - "The converging engine", "several computers edit at once and the changes merge by themselves, with the engine’s full security". The panel says the engine stays outside the Memev code and is called by it, is in no released Memev, and is not part of what you pay for today. PLANNED, owner decision 5 Nov 2026. The app shows this as text only.
  - "Hosted end-to-end encrypted sync between computers", "so the first computer need not be switched on". PLANNED SR-4.03.
  - "Hosted encrypted backup and a stable relay address", "a backup we host but cannot read, and one address that does not change". PLANNED SR-4.04, SR-4.05.

#### Tab: Plan and computers, paid plan (`logic5.js:16-22`)

- Sentence: "Personal Plus. 2 of 3 computers. Licence valid to 5 December." Src `plan --json` `computers_used`, `computers_allowed`; `license status --json` [in] `tier_label`, `renewal`.
- **SP-6 Page action "Add a computer"** (amber). Opens SP-S2.
- **SP-7 Section "YOUR COMPUTERS"**, heads "Computer", "State". Rows "iMac (this Mac)" ("holds your memory", "this Mac · now"), "MacBook Air" ("joined", "live · 6 min"), "Windows PC" ("invite sent", "invited · never seen").
  - Src: `devices list --json` [in] `label`, `kind`, `status`, `last_seen`. No field in the device record names an unused invite; showing "invite sent" needs a source (G-18).
  - Panel: state; why "Each computer signs every request with its own key. A computer beyond your plan’s limit cannot be added. If the plan lapses, added computers can still read but not write; nothing is lost."; one button by row: "Rename", "Remove" or "Cancel invite"; facts "Kind", "Web apps" ("0 connected · no limit on any plan").
  - Act, "Rename": `devices rename` [next]. Confirm (rule).
  - Act, "Remove": confirm (sheet "Remove <computer>?"), then `devices remove` [next].
  - Act, "Cancel invite": no command (G-18).
- **SP-8 Section "NEED MORE?"**, tag "Sample, not built yet". Row "Personal Pro", "10 computers · $49 a month or $490 a year · you pay the difference for the rest of this period · a lower plan applies from the next renewal", button "Upgrade to Pro" (opens SP-S1). Reason: "Today the fourth computer is refused with "remove one or contact us". Planned: the same message offers this upgrade, Stripe charges the difference, and a new licence with the higher allowance reaches this app by itself. No reinstall." DRAFT PL-1.
- **SP-9 Expander "Licence"**, "valid to 5 December · one person, not transferable":
  - "Enter a licence key", button "Activate": `license activate <key>` [next]. Confirm (rule).
  - "Install a licence file", "for a computer with no internet", button "Install": `license install <file>` [next]. Confirm (rule).
  - "Manage payment", "sample, not built yet: no billing exists; payment is monthly or yearly, no refunds, and a cancelled plan runs to the end of its paid period", button "Manage". Tagged in its own text (05-decisions:81). No command.
- **SP-10 Expander "How it works today"**, "the other computers reach the memory on this Mac, so this Mac must be on". Rows "One memory and one bus on all your computers", "The other computers reach the memory on this Mac", "A computer beyond your limit cannot be added". Text from docs/PRICING.md.
- **SP-11 "Coming later (3)"**: as SP-5.
- **SP-S1 Sheet "Upgrade your plan"**, tag "Partly built" (`logic5.js:129-135`). Note: "Personal Plus: $19 a month or $190 a year, 3 computers. Personal Pro: $49 a month or $490 a year, 10 computers. The first 200 customers keep $12 or $120 on Plus while subscribed. Draft prices, not an offer." Rows:
  1. "1. Checkout opens in your browser", "payment is taken by Stripe; Memev never sees your card", "test mode only today".
  2. "2. Your licence key arrives by itself", "by email and straight into this app; nothing to copy or paste", "not built yet".
  3. "3. This app switches to the paid plan", "the licence is checked on this computer; there is no account to sign in to · activating by key needs the licence service, which is not built", "works today with a licence file".
  4. "4. Add your other computers", "needs web access switched on · a one-time code from this computer; on the other one choose "I am adding a computer"", "works today".
  - Notice: "Nothing can be bought yet. Checkout runs in test mode, and the service that issues a key after payment is not built."
  - Buttons: "Continue to checkout" (amber): `upgrade` prints the checkout address; the app opens it in the browser. "I already have a key": `license activate <key>` [next].
- **SP-S2 Sheet "Add a computer"** (`logic5.js:136`). Note: "You are using 2 of 3 computers. Adding one needs web access (your secure link) switched on, because the new computer reaches this one over it." Rows "One-time code" ("MEMEV-7K2Q-91XD · expires in 15 minutes", "Copy"), "Or scan the QR code" ("shown beside the code", "Show"), "On the other computer" ("install Memev, then choose "I am adding a computer" and type the code"). Main button "Done".
  - Src: `invite --ttl SECONDS [--local]` [next] prints the code, link and QR. The sheet opening is the confirmation. A computer beyond the plan's limit is refused (05-decisions:78).

#### Tab: Storage and backup (`logic5.js:24-31`)

- Sentence: "1,284 notes in 38 MB. Backed up 4 minutes ago." Src `status --json` `index.notes`; size from `compact --dry-run` (G-20); last backup from `backup status` / `github status` [in, read], if their one line carries the time.
- **SS-1 Section "BACKUP"**: "Your private GitHub repository", "up to date · last push 4 min ago", ok glyph, button "Back up now".
  - Src: `github status`, `backup status` [in] (text, one line). PAGES.md: show it without the remote's address.
  - Panel: state "up to date"; why "Your notes are pushed as plain Markdown to a repository only you can see. Memev holds no copy."; button "Back up now"; facts "Another backup address" ("any other git address can be set"), "Sync conflicts" ("none"), "Writers" ("one computer at a time"). Facts from `doctor --json` ids `merge`, `writers`, `backup`.
  - Act, "Back up now": no command pushes on demand; the core's daemon pushes at start, every `sync.interval_min` minutes and at stop (CLI-CONTRACT.md) (G-17).
  - Setting a remote: `github setup --repo R` or `backup set <git-url>` [next]. Confirm (rule). No button for it is drawn here.
- **SS-2 Section "STORAGE"**: "Your notes folder", "~/memev · 38 MB · plain files you can open with any editor", button "Open folder". App: opens the folder. PAGES.md says paths are never shown; the design shows "~/memev" (D-4).
- **SS-3 Expander "Vault detail"**, technical detail on only, "1,284 notes · 48,210 log entries · 312 graph entities · 96 MB memory in use". Rows "Notes" (`index.notes`), "Log entries" (`oplog.seq`), "Graph entities" (`graph.entities`), "Memory in use" (`memory.rss_mb`), all from `status --json` / `memev_status` [in]; "Tidy old history files", "a compaction would free 6 MB · in Advanced", button "Open Advanced" (the saving needs `compact --dry-run`, G-20).
- **SS-4 "Coming later (3)"**: "Test my backup", "prove the backup can be restored, without touching your notes" (NOT SCOPED); "Export a copy", "one file with all your notes, to keep or to move" (NOT SCOPED); "Remote access log", "each request from another computer or a cloud AI, with its time and device" (NOT SCOPED; `.memev/access.jsonl` is written, nothing reads it).

#### Tab: Advanced (`logic5.js:32-44`)

- Sentence: "For developers, and for actions you rarely need. Each risky action asks first."
- **SA-1 Expander "For developers"**, "what the AI apps call · the memev command · where Memev runs":
  - "The connection each AI app uses", "memev mcp, started by the app itself". Text; `mcp` is not in the app.
  - "The moments an app tells Memev about", "session start, each prompt, before compaction, stop". Text; `hook` is not in the app.
  - "Running in the foreground or in a container", "memev serve, memev container". Text; `container` [next] read.
  - "Joining from another computer", "memev join, on that computer". Text.
  - "The memev command", "on your PATH, so terminals and AI apps can call it", button "Remove": confirm (sheet "Remove the memev command from your PATH?"), then `path remove` [next]. State from `path status`.
  - "Where Memev runs", "on this computer · AI provider kind: hosted API", button "Change". Src `deployment --json` [next] `mode`, `provider`, `runs_where`. Act `deployment set --mode M --ai T --json`; COVERAGE lists `deployment` as read only (G-25). Confirm (rule).
  - Section "APP FEATURES FOR DEVELOPERS", tag "Sample, not built yet": "Copy the command behind any item", "Raw data view", "Keyboard command menu" ("Ctrl or Cmd K"), "Compact rows", "Live log of the Memev service" ("needs new work"). PLANNED, app shell; the live log needs new engine work.
- **SA-2 Expander "Maintenance"**, "tidy history files · self-test · preflight · push gate · agent identity":
  - "Tidy old history files", "frees very little disk space · stops Memev for a moment", button "Compact": confirm (sheet "Tidy old history files?"), then `stop`, `compact --json`, `start` [next]. The core refuses `compact` while Memev runs.
  - "Self-test", button "Run": `selftest` [next].
  - "Preflight", button "Run": `preflight --port N` [next].
  - "Block a push while a contract is open", "installs a local pre-push gate", button "Install gate": `contract hook` / `gate --workdir` [next]. Confirm (rule).
  - "Verify who an agent acts for", "agent identity", button "Verify": `agentid verify` [next].
  - "Project name for this folder", "website, from the git top level", button "Pin a name": `project set <name>` (G-26).
  - "Head-to-head rankings", "Static site generator 1560 · Hosted CMS 1490": `memev_tournament_standings` [next].
- **SA-3 Expander "Web access"**, "public funnel off · relay off · new web-access key":
  - "Public funnel", "off". Src and act `cloud funnel on|off` [next]. No button drawn.
  - "Relay", "off". Src and act `cloud relay on|off|address|status` [next]. No button drawn.
  - "New web-access key", "cuts off anyone holding the old address", button "Rotate": confirm (sheet "Make a new web-access key?"), then `cloud rotate-key` [next].
- **SA-4 Expander "Remove or stop"**, "each of these asks you to confirm":
  - "Stop Memev", "your AI apps lose their shared memory until it starts again", button "Stop": confirm (sheet "Stop Memev?"), then `stop` [next].
  - "Remove Memev", "disconnect every app, stop Memev, remove the login service · your notes stay in ~/memev", button "Uninstall": confirm (sheet "Remove Memev?"), then `uninstall` [next], with the app's own uninstall.
  - "Also remove the runtime and launcher", button "Purge": confirm (sheet "Also remove the runtime and launcher?"), then `uninstall --purge` [next].

#### Tab: Help and support (`logic5.js:45-54`)

- Sentence: "Three ways to reach us. Nothing is sent from Memev until you send it."
- **SU-1 Section "EMAIL SUPPORT"**: the support address as the row title, "for anything private: your licence, your account, a diagnostic bundle", button "Write an email". The address is not written in this file: 05-decisions:151-153 says to check the leak denylist before it goes into a repository. Take it from the private canvas when that check is done.
  - Panel: title "Email support"; why "Opens a new message in your own mail app. Nothing is attached unless you attach it."; buttons "Copy the address" and "Open in mail"; facts "Address", "Good for" ("anything you would not post in public"). App: a mailto link. Paid support terms: SR-5.06.
- **SU-2 Section "PROBLEMS AND IDEAS"**, tag "Sample, not built yet":
  - "Report a problem", "a short form that raises an issue on GitHub · public, so nothing private goes in it", button "Report a problem": opens SU-S1.
  - "Ask for a feature", "a short form that raises a feature request on GitHub", button "Ask for a feature": opens SU-S2.
- **SU-3 Expander "Send us a diagnostic bundle"**, "health and logs with secrets scrubbed · by email only, never in a public issue". Row "Create a support bundle", "you choose whether to attach it to an email; it is never added to a GitHub issue", button "Create": `doctor --bundle` (G-15).
- **SU-4 Expander "Words Memev uses"**, "checked · contract · lead · bot · gone quiet · secure link · receipt". Rows from HELP: Checked, Needs you, Gone quiet, Lead, Contract, Check, Receipt, Access key, Secure link, Off by choice (`logic5.js:51`). App text.
- **SU-5 "Coming later (2)"**: "Known issues", "a list of what we already know is wrong, so you need not report it" (pull request 395, unlanded, adds docs/KNOWN-ISSUES.md and two issue templates); "Paid support", "what response a paying customer can count on" (SR-5.06).
- **SU-S1 Sheet "Report a problem"**, tag "Sample, not built yet" (`logic5.js:142-144`). Note: "Tell us what went wrong. You see everything that will be sent before it goes." Fields "Title", "What happened", "What you expected", "Steps to make it happen again", "Added by Memev (you can remove it)" ("Memev 0.1.0 · macOS 12.7 · Node 22"). Notice: "This creates a public issue on GitHub. Anyone can read it, and you need a GitHub account. No account, or something private? Use Send by email. Do not include passwords, keys, note text, file paths or project names. Nothing from your memory is attached." Buttons "Send by email" and "Continue on GitHub", both alike.
  - Reason: NOT SCOPED, needs a contract. Proposed route: the browser opens the new-issue page filled in, Memev holds no GitHub token, the person presses Submit there. The filled-in page has a limit of about 8 KB; long text must never be cut without saying so. Added by Memev: only version, system and Node major version (`--version`, the OS, Node).
- **SU-S2 Sheet "Ask for a feature"**, tag sample (`logic5.js:145-146`). Note "Tell us what you would like Memev to do." Fields "Title", "What would it let you do?", "How do you manage today?", "Added by Memev (you can remove it)" ("Memev 0.1.0"). Notice: "This creates a public feature request on GitHub. Anyone can read it, and you need a GitHub account. No account? Use Send by email." Buttons as SU-S1. NOT SCOPED, same route.

### 2.8 Health (`logic4.js:105-117`)

- No menu entry. Opened from the sidebar health line (SH-2), the stopped banner and "See why it stopped". Breadcrumb "Health". Title "Health".
- Sentence: "Everything is working. One thing to look at: ops-bot has been quiet for 48 minutes." All clear: "Everything is working." Stopped: "Memev is stopped. Your AI apps cannot reach their shared memory. Nothing is lost."
  - Src: `doctor --json` [in] checks with `status` warn or fail; `bus roster --json` stale seats.
- What is this? words (`logic2.js:53`): "Check", "Off by choice".
- **HE-1 Section "TO LOOK AT"** (normal state): "Bots reporting in", "ops-bot has been quiet for 48 minutes", button "Ask it" (amber). Panel BOT-P. Src `bus roster --json`. Hidden in All clear.
- **HE-2 Section "TO LOOK AT"** (stopped): "Memev is not running", "start it and your apps share their memory again", button "Start Memev" (amber). Act `start` [next]. Confirm (rule).
- **HE-3 Expander "12 checks are fine"** (13 in All clear), "running · history · backup · search · messages · lead · plan". Rows with the ok glyph:
  - "Memev running" ("started 09:12 today"), "Node and Git" ("Node 22.11, git 2.47"), "History" ("complete, nothing altered"), "Passwords in notes" ("none found"), "Backup" ("saved 4 min ago"), "Lead chosen" ("claude-code"), "Update" ("no release published yet"), "Two apps share one memory" ("2 of 2 apps joined"), "Memory on" ("1,284 notes"), "Search" ("index up to date"), "Messages" ("bus running"), "Plan" ("Personal, free" or "Personal Plus"); in All clear also "Bots reporting in" ("all 5 live").
  - Src: `doctor --json` [in] `{id, title, status, detail}`, ids `daemon`, `node`, `git`, `oplog`, `drift`, `secrets`, `backup`, `bus-lead`, `update`, `vault`, `index`, `bus`, `license`; "Two apps share one memory" from `ready --json` [next] `smoke`, `clients[].joined`; "started" from `status --json` `started`. The doctor defines 36 checks; 26 ran on the inventory's Mac.
  - The first native slice (SLICE.md) lists each returned check as its title and status word. The grouped rows above are the design.
- **HE-4 Expander "Off by your choice (1)"**, "web access for cloud AI". Row "Web access for cloud AI", "off until you switch it on", button "Manage" (opens Bots and agents, Connect). Src `doctor --json` id `remote`; `cloud status` [next].
- **HE-5 Section "MEMEV SERVICE"**: one row "Running since 09:12 today" with "Restart", or "Stopped" with "Start". Src `status --json` `started`; `GET /healthz`. Act `restart` or `start` [next]. Confirm (rule).
- **HE-6 Expander "Under your control (6)"**, "what leaves this computer · who can get in · history · backup". Rows "What leaves this computer" ("Memev itself sends nothing. What a bot reads goes to that bot’s AI provider. Your backup goes to your own private store."), "Web access for cloud AI" ("off"), "Who can get in" ("6 keys: you and five bots, each limited to its own project; none expired"), "Work held to checks" ("on · 45 of 64 tasks checked"), "History" ("every change is recorded and kept; the record cannot be quietly changed"), "Backup" ("up to date").
  - Src: `doctor --json` ids `remote`, `token-expiry`, `oplog`, `secrets`, `backup`; `token list` [next] (text only, G-24); `cloud status` [next]; `contracts status` (G-12); tasks checked from `work status` per project.
- **HE-7 Expander "Get help"**, "a support bundle with secrets scrubbed · Help and support":
  - "Support bundle", "health and logs with secrets scrubbed; send it by email only", button "Create": `doctor --bundle` (G-15). Confirm (rule).
  - "Help and support", "email, report a problem, ask for a feature", button "Open": opens Settings, Help and support.
  - Technical detail on: "Self-test", "a throwaway vault: write, recall, verify the log", button "Run": `selftest` [next]. Confirm (rule).
  - Technical detail on: "Preflight", "Node, Git, port and folder checks", button "Run": `preflight --port N` [next]. Read.
- **HE-8 "Coming later (1)"**: "A guided repair page for every check", "what went wrong, and the one button that fixes it". PLANNED SO-C-C AC04; today `doctor --fix --yes` (COVERAGE [next]).

### 2.9 Install wizard (`logic5.js:149-170`, `tpl.html:330-364`)

- Not a menu entry. The first launch.
- Title "Installing Memev, step by step". Note: "Nine windows in four stages. The graphical installer is not built yet; under each window is a line saying what works today. Switch Technical detail on to see the command behind each step." (`logic5.js:233`). The code draws ten windows in the four stages (the download page is "A web page, not an app window", and the adding-a-computer window is a branch), then three cards that wait on the Overview.
- Each window: a title bar with the mark and "Memev Setup" (or its own bar text) and the step on the right; the title (20/28); a body; item rows with a glyph and a state; an optional progress bar; buttons (one amber); under the window a caption that says what works today, and with technical detail on the command.
- Each stage opens with a 24 px amber line, the stage label in mono and a note (`tpl.html:337`).
- `CLI-CONTRACT.md` limits what the native window runs at first launch: `init --root DIR --local-only --no-connect --no-service --port N`, `join CODE`, `backup set URL`, `remember` then `recall` as the check, then `status`, `doctor`, `card`, `clients`. Several drawn steps run more than that (D-1).

Stage "1 · GET IT", "download, welcome, install":

- **IN-1 "Get Memev"** (bar "A web page, not an app window", step "download page"). Body "One download for each kind of computer. The page shows the checksum beside each file." Items "macOS" (".dmg"), "Windows" (".exe · Node and Git included"), "Linux" ("install script"). Button "Download for macOS" (amber). Caption "Not built yet: no release is published." Command today: git clone, then `npm install -g . && memev init --no-connect`; installers MEM-165 draft.
- **IN-2 "Welcome to Memev"** ("Get it · 1 of 2"). Body "Memev installs for you only. It needs no administrator password and changes nothing else on your computer." Items "The app" ("Applications"), "Your notes folder" ("~/memev"), "Its own runtime" ("Node 22, kept private"). Buttons "Cancel", "Continue" (amber). Caption "Not built yet: the double-click installer." Command `scripts/install.sh`, `scripts/install.ps1`.
- **IN-3 "Installing"** ("Get it · 2 of 2"). Body "Four quick checks, then the install. This takes under a minute." Items "Runtime: Node 22" ("included, verified"), "Git" ("found 2.47"), "Port 7787" ("free"), "Notes folder" ("writable"), "Download verified against its SHA-256" ("done"), "Starting Memev" ("working", live glyph). Progress bar 78%. No buttons. Caption "The checks and the install scripts work today. Rev1 had two windows here; they are one now." Src `preflight --port N` [next]; then `init` (first launch).

Stage "2 · CONNECT", "your memory, your AI apps, the lead":

- **IN-4 "Is this your first computer?"** ("Connect · 1 of 3"). Body "Your memory is a folder of plain notes. Choose where this computer gets it from." Items "This is my first computer" ("start a new memory in ~/memev"), "I am adding a computer" ("join my memory with a one-time code · paid plan"), "I am moving to a new computer" ("restore from my backup"). Buttons "Back", "Continue" (amber). Caption "All three choices work today as commands." Src `init --root DIR`, `init --join CODE` or `join CODE`, `init --from URL`.
- **IN-5 "Add this computer to your memory"** ("Connect · adding a computer"). Body "On your first computer open Settings, Plan and computers, Add a computer. Type the code it shows." Items "One-time code", "Plan" ("Personal Plus · 2 of 3 used"), "Your memory" ("stays on the iMac, reached over the secure link"). Buttons "Back", "Join" (amber). Caption "Shown only after "I am adding a computer". Joining works today and needs web access on; a computer beyond your plan’s limit is refused." Src `join <code>` (the person pressed Join; nothing is sent before), `plan --json`.
- **IN-6 "Connect your AI apps"** ("Connect · 2 of 3"). Body "Memev adds itself to each app’s settings and keeps a copy of the old settings first. You can disconnect at any time." Items "Claude Code" ("found · connect"), "Codex" ("found · connect"), "Cursor" ("found · not now"), "Claude Desktop" ("found · not now"). Buttons "Back", "Connect 2 apps" (amber). Caption "One window for every app found. Rev1 asked once per app." Src `clients` [in]; act `connect <app>` [next], blocked by G-11 and D-1.
- **IN-7 "Which app should lead?"** ("Connect · 3 of 3"). Body "The lead hands tasks to the others and is the one you talk to." Items "Claude Code" ("connected"), "Codex" ("connected"), "Link test" ("hello was read"). Buttons "Back", "Make Claude Code the lead" (amber). Caption "Works today." Src `init --lead SEAT` or `bus operator <seat>` [next], `bus hello <seat>` [next] (D-1).

Stage "3 · PROTECT", "a backup that you own":

- **IN-8 "Back up to your own private GitHub?"** ("Protect · 1 of 1"). Body "Your notes are pushed as plain Markdown to a repository only you can see. Memev holds no copy." Items "Repository" ("memev-vault (private)"), "Writers" ("one computer at a time"). Buttons "Skip", "Set up backup" (amber). Caption "Works today." Src `github setup --repo`, `github status`, `backup set <git-url>`. The native window may run only `backup set URL` (D-1).

Stage "4 · FINISH", "a test, then the app opens":

- **IN-9 "Testing everything"** ("Finish · 1 of 2"). Body "Memev checks itself, then waits for two apps to share one memory." Items "Memev running", "Log and search index", "Apps connected" ("2 of 2"), "Bus and lead", "Two apps share one memory" ("open Codex once", live glyph). Progress 85%. No buttons. Caption "Works today." Src `doctor --json` [in], `ready --timeout S --json` [next] (`ok`, `smoke`, `clients[].joined`). The native slice's check is `remember PHRASE --force` then `recall PHRASE --json`.
- **IN-10 "Memev is ready"** ("Finish · 2 of 2"). Body "Open a new session in each connected app: it starts with your memory. The Memev app opens on the Executive overview." Items "Health" ("all working"), "2 apps connected, lead set", "Backup" ("on"), "Start Memev when I sign in" ("ticked"). Button "Open Memev" (amber). Caption "The desktop app is not built yet; today setup ends in the terminal. The start-at-login question from rev1 is the ticked line here." Src `ready`, `onboarding --json` (G-14), `service install` [next] (the native slice passes `--no-service` to init).

Stage "AFTER INSTALL · ON THE EXECUTIVE OVERVIEW", "three optional questions leave the wizard and wait in a "Finish setting up" card" (shown on the Overview as OV-12):

- **IN-11 "Let cloud AI apps reach your memory?"** (bar "Finish setting up"). Body "Off unless you switch it on. You need it for ChatGPT and Claude on the web, and for your other computers." Items "Secure link" ("off"), "If on: automatic, Tailscale or Cloudflare" ("your own tunnel"). Buttons "Keep it off", "Switch on" (amber). Caption "Works today." Src `cloud on|off|status`, `cloud use` [next] (D-1).
- **IN-12 "Connect ChatGPT?"**. Body "Paste this address into ChatGPT’s connector settings, or scan the code. You then approve ChatGPT here, on this computer." Items "Connector address" ("copied"), "Waiting for ChatGPT" ("not seen yet", live glyph), "Next: Claude on the web". Buttons "Skip", "I have pasted it" (amber). Caption "Works today for ChatGPT and Claude. Grok and others: not built." Src `cloud connect chatgpt|claude` [next]; consent at `/oauth/consent` on this computer. The connector address is a secret: show it only to copy, never in logs.
- **IN-13 "Help improve Memev?"**. Body "An anonymous count of setup milestones, never your notes. Off unless you say yes." Items "Milestone report" ("off"), "See exactly what would be sent" ("one click"). Buttons "No thanks", "Switch on" (amber). Caption "Works today. Asked once." Src `report status --json` [in]; `report show`, `report on|off` [next].

---

## 3. Gap list: drawn as working, with no command in COVERAGE.md

Doctor ids: `update` and `writers` appear in `doctor --json` only when there is something to report. An absent id means that check is fine, and the pip shows fine.


Each item below is drawn without a "Sample" tag and outside "Coming later", so a person would read it as working. COVERAGE.md has no command for it, or marks the command as waiting for a core addition. **The app must flag each one, never fake it.** Until the core has the command, the element shows what the core does say, or it is hidden, or it carries the "Sample, not built yet" tag.

I found these by listing every `memev ...`, `memev_*` and `bus_*` token in the mockup's command strings, matching them against COVERAGE.md, and then checking each drawn element against its source.

- **G-1 Start a project.** OV-11, PR-1, PR-3. No command creates a project. `memev_task_plan` plans tasks inside a project and `project set` pins a folder's name; neither starts one.
- **G-2 Next milestone and gates.** The project Overview sentence "Next milestone: design signed off." and the Plan sentence "1 of 4 gates passed. Next: design signed off." The blocks they summarise are tagged sample; the sentences are not. Milestone entities have no reader on main.
- **G-3 Open risks.** The Risks and decisions sentence "2 risks are open." The risks list is tagged sample; the sentence is not. No RAID data exists.
- **G-4 Past scope changes.** PK-5 rows "Blog migration removed" and "Search moved after launch". `memev_scope_amend` records a change and `work impact` answers what it makes stale, but nothing lists past changes.
- **G-5 Deliverables.** PS-3 lines "deliverable: <name> · <version> · <state>", and the Outputs tab sentence "3 outputs: 1 accepted, 1 reviewed, 1 draft." Only `artifact_hash` per task exists.
- **G-6 Model and effort choices.** The model and effort selects in each agent pane (OP-7) and on the lead panel (OP-9), with "now strong · high". The usage row (OP-6) is tagged; these selects are not. No command sets a model or effort for a seat; the runner keeps one command per bot. Only the "Coming later" row OP-14 explains it.
- **G-7 Agent panes, "Messages (works today)".** OP-3, OP-7, the "By bot" chip (OM-2) and "Follow its messages" (BOT-P). The panes need `bus since` filtered by sender and recipient. COVERAGE.md marks the panes as waiting for the core's `--from/--to` filters (SO-N-15). The app may filter `bus since --json` itself (05-decisions:90 calls it small new work), but must not label it as working before that filter exists and is tested.
- **G-8 Unread counts that do not mark messages read.** "2 unread" and "1 unread, on hold" in the panes (OP-7), "Unread" in BOT-P, "3 unread" in the Messages sentence, the "Unread (3)" chip and "Unread and holds" (OM-4). `bus unread` marks messages read today. COVERAGE.md: waits for `--peek` (SO-N-15).
- **G-9 A message's linked task.** OM-3 fact "Linked task". Messages carry `ref` and `project`; nothing maps a message to a project task.
- **G-10 Who "you" are on the bus.** OP-10, OP-11 and OM-1 send and show messages from the person. Neither COVERAGE.md nor the inventory says which seat the app sends as; `bus send TO TYPE TEXT` has no sender option. The lead answers only when its app is open or its background runner is on (HELP "When the lead answers"); the app must not show a reply as coming before one arrives.
- **G-11 Connecting and disconnecting an app.** BC-3 "Connect" and "Disconnect", BC-S1, IN-6 "Connect 2 apps". `clients` prints a marker, a name and a state, but no id, so the app cannot pass an id to `connect` (CLI-CONTRACT.md; SR-2.09.2 not in the core tree). The "memory carries over ..." line for each app has no core field. `clients` is text only.
- **G-12 Checked work on or off.** CW-3 "Switch off" and its sheet, the sentence "Checked work is on.", HE-6 "Work held to checks", the "Checks on" pip. The core has `contracts on|off|status`; COVERAGE.md does not list it.
- **G-13 A helper sets Memev up.** BC-6 "Start". The core has `setup remote --code`, `stop` and `log`; COVERAGE.md does not list them.
- **G-14 Set-up checklist.** SG-2 (the seven milestones and "Run setup again"), IN-10. The core has `onboarding --milestones --json` and `onboarding --json`; COVERAGE.md does not list them.
- **G-15 Support bundle.** HE-7 "Create", SU-3 "Create". The core has `doctor --bundle`; COVERAGE.md lists only `doctor --json` and `doctor --fix`.
- **G-16 Check for an update.** SG-1 "Update", button "Check". `update` installs a tarball that the person already has. Nothing looks for a new release. `doctor --json` id `update` gives the state line only.
- **G-17 Back up now.** SS-1 and its panel. No command pushes on demand. The core's daemon pushes at start, on its interval and at stop. CLI-CONTRACT.md says the window never runs `push`.
- **G-18 Invites.** SP-7 row "Windows PC", "invite sent", "invited · never seen", and its button "Cancel invite". No field in the inventory's device record names an unused invite, and nothing cancels one.
- **G-19 Memory speed and prompt reach.** MX-1 "Start-up" and "Pause while saving", MX-2 "Did memory reach your prompts" (all three rows), MX-3 "SPEED" (all six rows), and the sentence "Last measured 2 minutes ago". The mockup cites `scripts/bench/speed`, a bench script, and "prompt hook counts, planned with SR-1.35". Their detail panels are tagged; the charts and lists are not.
- **G-20 Sizes that need compaction's report.** MX-4 "Search index on disk", "Change record", "Backup history"; the Storage sentence "in 38 MB"; SS-3 "a compaction would free 6 MB". `compact --dry-run --json` refuses while Memev runs.
- **G-21 Integrity sweep.** MX-5 "Integrity sweep", "last finished 40 minutes ago", "Clean". The mockup cites `memev verify`, which re-runs contract checks. No command reports a sweep of notes against the record or when it last finished.
- **G-22 People on this project.** PO-6 row "People on this project". Notes of kind person can be saved; `recall` has no filter by kind.
- **G-23 Lease renewal time.** PW-1 panel "held, renewed 1 min ago". The task fields carry `lease_id` and `holder`, not the time of the last heartbeat.
- **G-24 Keys.** BA-2, the "May" fact in BOT-P, HE-6 "Who can get in", the sheet "Replace the key for <holder>?". `token list` is text only. COVERAGE.md lists `token` list, issue and revoke; `token rotate`, which "Replace with a new key" needs, is not listed.
- **G-25 Where Memev runs.** SA-1 "Change". COVERAGE.md lists `deployment` as read only; the action `deployment set` is not listed.
- **G-26 Pin a project name.** MS-4 and SA-2 "Pin a name". COVERAGE.md lists `project` as read; `project set` is not listed.
- **G-27 Most recent memories.** MS-3 "MOST RECENT" with no search typed. `recall` needs a query; `memev_list` lists a folder, not by recency.

## 4. Conflicts to settle before building

**Owner rulings, 7 Nov 2026 about 18:25Z (asked in the lead's session). They settle D-1, D-2, D-4 and D-5:**

- D-1: the install wizard runs `connect`, the lead, `github setup`, `service` and `cloud` itself, each after a confirmation that names what it changes. CLI-CONTRACT.md is rewritten to match.
- D-2: the Operations chat sends on Enter, with the recipient named in the prompt box. Every other action keeps its confirmation.
- D-4: the person's own paths are shown (the notes folder, project outputs), shortened with ~. Token, key and system paths never are.
- D-5: the support address goes into Help and support. The owner allowed the company name in both repositories.
- D-3, D-6 and D-7 stand as written below.


These are not missing core commands. They are places where the design and this repository's own rules disagree.

- **D-1 The install wizard runs more than the window's contract allows.** IN-6 (`connect`), IN-7 (`bus operator`, `bus hello`), IN-8 (`github setup`), IN-10 (`service install`), IN-11 and IN-12 (`cloud`). CLI-CONTRACT.md says the window never runs `connect` without a known id, never runs `github setup` or `cloud` itself, and passes `--no-service` to `init`. Either the contract grows, with the confirmations named, or those windows say what to do in the terminal.
- **D-2 Sending to the lead.** OP-11 sends on Enter with no sheet. COVERAGE.md:5 says every action runs behind a confirmation that names what changes. A chat needs a ruling: for example, a message is an action that names its recipient in the prompt box, and no sheet.
- **D-3 The command field.** SH-9 is on every screen and does nothing in the mockup. 03-interaction-model §2.6 says it goes anywhere and runs any action a button runs, and must never turn a search into an instruction to a bot. It is app work over the commands above, with the same confirmations.
- **D-4 Paths on screen.** The design shows "~/memev" (SS-2, IN-2, SA-4, the uninstall sheet) and output paths such as "projects/website/pages/home.html" (OP-13). PAGES.md says paths are never shown. One rule must win.
- **D-5 The support address.** SU-1. It stays out of this repository until the leak denylist has been checked (05-decisions:151-153).
- **D-6 Counts that differ between the notes and the code.** 14 pips in the code, 13 in 05-decisions:14 (the code is used here). `line` is #2B3543 dark and #D6DBE3 light in the code, #252E3A and #DDE1E7 in 05-decisions:62 (the code is used). The mockup says 18 known apps (`logic4.js:19`), the inventory 17. The app shows what the core prints. Memory, Search in first run draws two amber buttons, the empty state's "Import" and the page action "Save a memory" (`logic4.js:65-66`, `logic1.js:139`, `logic5.js:179`); the app keeps only the empty state's.
- **D-7 Buttons with no flow drawn.** Many action buttons only print "Mockup only: ... changes nothing here." (for example "Change the lead", "Write to a bot", "Issue a key", "Give to another bot", "Rename", "Check", "Make lead", "Switch on" for the runner, "Keep", "It helped", "Sign", "Approve", "Restart"). The map gives the command for each. Each still needs its confirmation drawn, per COVERAGE.md:5, before it is built.

## 5. Addendum, 7 Nov 2026: three screens the owner asked for after the map was written

These follow the rev2 visual language of section 1: the section label, lists on `panel`, the one amber button, form sheets with an amber main button, and confirmation sheets with a red main button. The sources are the commands of the core on memev main, pinned in the installers at `f5c2254e9e97a5f9ce04513ad0c1d8de619a7f6f`. Where that core has no command, the element says so; nothing is a sample figure.

The guided first launch keeps its first steps (this computer or another, the notes folder, the backup remote, the check that a note comes back). After the check it asks three more things, in this order, and each one has a "Skip" button: connect your AI apps (5.1), bring in your chat history (5.2), and choose the lead (5.4). Skipping changes nothing. A computer that joined another computer's memory skips all three, because its memory lives on the other computer.

### 5.1 Bots and agents, Connect tab: the apps Memev found

- Sentence: "<n> of <m> AI apps found on this computer are connected." When none is found: "No AI apps were found on this computer."
- **BC-8 Section "AI APPS ON THIS COMPUTER"**, a list of every app the core found. Each row: a tick box, the app name, and its state in `sub`.
  - Found and supported, not connected: ticked, "found · not connected".
  - Connected: shown ticked and greyed, "connected", with the row's "Disconnect" button (the confirmation sheet "Disconnect <app>?", then `disconnect <id>`).
  - Disconnected by you earlier: not ticked, "you disconnected it".
  - Found but not supported, or a supported app whose settings file Memev cannot edit: no tick; the second line is the core's own manual step.
  - Src: `onboarding --json`. Its items carry `app` (the id), `step` (the name), `status` (`added`, `ready`, `skipped`, `needs-manual`, `unsupported`) and `detail` (the manual step). A found, connectable app is the item whose detail is `memev connect <id>`. This is the only output of the trial core that prints an app id: `clients` prints names only, and there is no `clients --json` or `connect list`. `onboarding --json` is not a pure read: it rewrites `.memev/onboarding.json`, records the milestones and registers the seats of apps that are already connected. It does not start a stopped Memev and connects nothing. The app does not keep a list of its own.
- **BC-9 Page action "Connect <n> apps"** (amber, enabled while at least one box is ticked). It opens **BC-S3**, a sheet titled "Connect <n> AI apps?". Note: "Memev adds itself to the settings of each app below and keeps a copy of the old settings first. Each app gets its own seat on the message bus. You can disconnect any of them later." Rows: one per ticked app. Main button "Connect" (amber). The sheet is the confirmation.
  - Act: `connect <id> <id> ...`, the ids of the ticked rows only, never `connect` with no app and never `connect all`. The core's answer is shown, line by line, in the sheet, then the list is read again.
- **BC-10 Line "Apps not found on this computer"**: the count of the `clients` rows that read "not found". Text only.

### 5.2 Import history

The trial core at `8520a874d1159054bea7e51b310ed415a29c629e` has the history scan (`docs/IMPORT.md` in the core).

- Setup asks once, after the note check: "Bring in your existing AI chat history?" It lists every source `import scan --json` finds, each with a tick box, the app's name, and a second line with its count ("42 sessions", or "not counted yet" when the core did not count it), its size, its first and last dates, how many projects it spans, and where it is (the person's own path, shortened with ~). A source the core marks (`marked`, 100 items or fewer) is ticked; the rest are not. The main button reads "Import <n> sources" (amber), or "Continue" with nothing ticked; "Skip" moves on.
  - Src: `import scan --json`: `sources[]` with `id`, `app`, `kind` (`files`, `sessions`, `export`), `path`, `count` (or `null`), `bytes`, `first`, `last`, `marked`, `projects`. The scan imports nothing, needs no vault and does not stop Memev.
- One confirmation sheet, "Import <n> sources?" (or "Import <app>?" for one), names each chosen source with its line, and says what happens: one note per conversation, nothing leaves the computer, no AI model is used, Memev stops while it imports and starts again, a large history takes minutes, and it can be undone. Main "Import" (amber).
  - Act: `import --source ID --source ID ... --yes`.
- The import runs in the background. A sheet shows a moving bar and the time so far; it can be closed, and the top bar's status line keeps the time until the import ends. The core prints no progress of its own, so the bar does not claim a share. At the end a sheet says "Imported <n> items" (from the core's "Imported <n> item(s) as <id>") with "Undo this import", or the core's answer when it did not finish.
- The Import sheet (MEM-S2) opens from Memory, Search, page action "Import". Rows: "Your AI apps' history on this computer" ("Choose": the same list in a sheet, then the same confirmation), "A chat export (.zip)" ("Choose": a file picker for a ChatGPT or Claude export, then `import --export FILE --preview` shown as the core's text and `--yes` after "Import"), "Earlier imports" ("List": `import list`, each with "Undo").
- Undo: the confirmation sheet "Undo import <id>?" ("Everything that import brought in leaves your memory.", main "Undo import", red) runs `import undo <id>`.
- A path that holds `" % ! & | < > ^ ( )` is refused before it reaches the core, as every argument to a `.cmd` core is; the sheet says so.
- Setup's lead question waits while an import runs, because the import stops Memev and choosing the lead needs it running.

### 5.3 Bots and agents, a new tab "Background agents"

- Tabs become "Team", "Connect", "Background agents", "Access".
- Sentence: "<n> background agents are on." or "No background agent is on."
- **BB-1 Section "BACKGROUND AGENTS"**, a list from `runner status --json` (`seat`, `enabled`, `max_runs_per_day`, `cmd`, `today`). Each row: the seat, "on · <today> of <max> runs today" or "off", and a "Stop" button on a row that is on.
  - Act, "Stop": the confirmation sheet "Stop the background agents for <seat>?" ("It takes no more turns until you start it again. Work it already did is kept.", main "Stop", red), then `runner off <seat>`.
- **BB-2 Page action "Add background agents"** (amber). It opens **BB-S1**, a sheet "Add background agents". Fields: "App" (a choice of the apps that are connected, from BC-8), "How many agents" (1 to 20), "Most runs a day" (required, a whole number of 1 or more; the sheet will not start without it). Note: "Each agent takes one unattended turn when the lead posts it a task, until the day's limit." Main button "Start" (amber). The sheet is the confirmation.
  - Act: `runner on <app> --agents N --max-runs-per-day M`. The core's answer is shown; it can refuse, for example when it knows no headless command for that app.
- The runner takes turns while `runner watch <app>` runs. The trial core has no service that keeps it running, so the tab says so in one line.

### 5.4 Choose the lead, in setup

- Setup's last question: "Which app should lead?" Rows: the seats from `bus roster --json` (`seats[].seat`, `client`, `state`), one to pick. Main button "Make <seat> the lead" (amber), and "Skip".
- Act: `bus operator <seat>`, on this computer only, after the person pressed the button.

### 5.6 Chats: talk with the lead from your phone

The trial core at `ceb322f853488ee83162a5d6b39169243bb7ace7` carries the chat bridge (`docs/CHAT.md` in the core). This is a screen of its own, with a menu entry "Chats" after "Bots and agents". No tabs; header rule SH-15.

- Title "Chats". Sentence: "<n> of 4 chat channels are on." or "Every chat channel is off. Each one stays off until you turn it on."
  - Src: `chat status --json`: `channels[]` with `channel` (`telegram`, `whatsapp`, `discord`, `lark`), `on`, `running`, `seat`, `last_in`, `last_out`, `last_refusal`, `error`; for WhatsApp `enabled`, `linked`, `installed`; for Telegram and Discord `set_up`, `bot`, `paired`; for Lark `set_up`, `paired`, `available`.
- What is this? words: "Chat channel", "Pairing", "Linked device".
- Each channel is a section with one row: a glyph (ok when paired or linked and running, live when on and waiting, off when off), the state in plain words ("off", "set up, not paired", "paired · running", "linked · running"), the last message in and out, and the core's error when it has one. Buttons on the row are secondary; the page action "Refresh" is the screen's amber button.
- **CH-1 Section "TELEGRAM"**, "recommended: Telegram's official bot route".
  - "Set up": the sheet "Set up Telegram" with one field, "Bot token" (hidden as it is typed), and the note "In Telegram, open @BotFather, send /newbot and follow its steps. Paste the token it gives you. Memev checks it with Telegram and keeps it on this computer only." Main "Set up" (amber). Act: `chat telegram setup --json`, with the token passed in the variable `MEMEV_TELEGRAM_TOKEN`, never as an argument.
  - "Pair": `chat telegram pair --json` gives `link` and `expires`. The sheet "Pair your phone" shows the link as a QR code and as text, with "Copy the link" and the note "Open it on your phone within 10 minutes and press Start. Only that one person is accepted."
  - "Turn off": the confirmation sheet "Turn off Telegram?" ("Memev forgets the bot token and the paired person. Nobody reaches the lead through Telegram until you set it up again.", main "Turn off", red), then `chat telegram disable --json`.
- **CH-2 Section "WHATSAPP"**, tag-free, with the core's warning as a notice on `raise`: "This uses an unofficial WhatsApp connection. WhatsApp's terms do not allow it, and WhatsApp could suspend this number. Memev only messages you, in your own chat."
  - "Turn on": the sheet "Turn on WhatsApp?" with the warning, a tick box "I understand that WhatsApp could suspend this number", and the note "Memev installs the pinned WhatsApp library on this computer. It writes only to your own "Message yourself" chat." The main button "Turn on" (amber) stays disabled until the box is ticked. Act: `chat whatsapp enable --accept-risk --json`.
  - "Link": the sheet "Link WhatsApp" runs `chat whatsapp link --json`. Each line it prints with a `qr` is drawn as a QR code (the code changes every few seconds, and the sheet shows the newest); a line with `pairing_code` is shown as text. The note: "On your phone: WhatsApp > Settings > Linked devices > Link a device, then scan this." An optional field "Or link with your phone number" runs it with `--phone <number>` for an 8-character code. The sheet closes on `linked`; Cancel stops the command.
  - "Turn off": the confirmation sheet "Turn off WhatsApp?" ("Memev logs this computer out of WhatsApp and removes everything it kept here.", main "Turn off", red), then `chat whatsapp disable --json`.
- **CH-3 Section "Z.AI (DISCORD OR LARK)"**: two rows, Discord and Lark. They share one seat, so only one is on at a time; the core refuses the second.
  - Discord, "Set up": the sheet "Set up the Discord route" with "Bot token" (hidden), and the ids "Server id", "Channel id", "z.ai bot user id" and "Your user id" (optional). Main "Set up" (amber). Act: `chat discord setup --json` with the token in `MEMEV_DISCORD_TOKEN`, then `chat discord pair --guild G --channel C --zai Z [--person P] --json`. Each id is a long number; the sheet refuses anything else before it runs.
  - Lark: its state only. The core stores Lark settings, but its connection is not built yet (`available: false`), so the row says so and offers no set-up.
  - "Turn off" on the row that is on: the confirmation sheet "Turn off the <Discord|Lark> route?" ("z.ai no longer reaches your lead through it, and its settings on this computer are removed.", main "Turn off", red), then `chat discord|lark disable --json`.
- Every message from a phone reaches the lead as data to read, never as a command; the screen says so in one line under the sections.

### 5.5 What the window may run, added to CLI-CONTRACT.md

`onboarding --json`, `runner status --json`, `import scan --json`, `import list` and `chat status --json` as reads; `connect <id>...`, `disconnect <id>`, `import --source ID ... --yes`, `import --export FILE --preview` and the same with `--yes`, `import undo <id>`, `runner on <app> --agents N --max-runs-per-day M`, `runner off <seat>`, `bus operator <seat>`, and the `chat` commands of 5.6, each only after its sheet.
