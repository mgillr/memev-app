# The pages, and what each reads from the core

Every page reads the local `memev` command (the window's `MEMEV_BIN`, or the app's own copy) with a fixed argument list and no shell. A page shows only the fields listed for it. Paths, tokens, keys, ids of credentials and raw JSON are never shown. Operations is the one page that writes: it can name a lead the person picked, and send that lead a task on this computer's bus. Nothing is connected and nothing leaves this computer.

When a command fails or its answer does not parse, the page says so in one sentence and shows no figures.

## Memory

- **Search**: a search box. `recall <words> --json` (words as one argument per word). Each hit shows its heading and snippet, and its project when it has one. No hit: "Nothing matched."
- **Review**: `review list --json`, the `inbox` list. Each item shows its text and why it was flagged. Empty: "Nothing is waiting for review."
- **Clean-up**: `audit --json`. Shows `memories`, `likely_junk`, and the `next` sentence.

## Checked work

`contract list` (JSON, `contracts`). Each contract shows its title, its status, and whether it has a receipt. Empty: "No contracts yet."

## Bots and agents

- **Team**: `bus roster --json`. Each seat shows its name, its app and whether it was seen in the last `live_minutes`. The operator, when set, is named. With none set, the page shows the core's `lead_needed` sentence.
- **Computers and apps**: `devices list --json`. Each device shows its label, its kind (this computer, an app on it, another computer) and its status. Ids, token ids and keys are not shown.
- **Apps on this computer**: `clients`, its lines as the core prints them.

## Projects

`card --json` for the project name and north star, as on the Overview, and `project` for the current project's name. The folder path the core prints is not shown.

## Settings

- **Plan**: `plan --json`: the plan, computers used and allowed, connectors counted.
- **Licence**: `license status --json`: `tier_label`, the licence `state` and its `reason`.
- **Anonymous report**: `report status --json`: on or off.
- **Backup**: `backup status`, its one line, without the remote's address.
- **Theme**: follows this computer (unchanged).

## Operations

The lead prompt. `bus roster --json` names the lead, or says to pick one, and lists each seat's name and whether it was seen in the last `live_minutes`. `bus since 0 --json` lists what has been said (from, to and the text). Send runs `bus send <lead> task <text>`. "Make <seat> the lead" runs `bus operator <seat>` for a seat the roster just returned. The box's placeholder is "Tell your lead agent what you want done, or ask a question". With no messages yet: "Nothing has been said yet. Tell your lead agent what you want done." The first time this window opens on a computer that already has a vault, it opens here. After the first-launch guide finishes, it opens here too. A later start opens Executive overview.
