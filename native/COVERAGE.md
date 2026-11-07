# Core coverage: every function and data point of the core, and where the app has it

The pre-deployment build has every row below in the app, as a read or as an action, except rows marked **not in the app**, which say why. Status: **in** (built and tested against a live core), **next** (being built), **waits** (needs a core addition first). Source of the list: the core's `memev` help and its MCP tools at memev `c0f47b1e`.

Actions run the named command with a fixed argument list, behind a confirmation that names what changes, and show the core's answer.

## Overview and Health

| Core function | Kind | Page | Status |
|---|---|---|---|
| `status --json`, `memev_status` | read | Overview, Health | in |
| `doctor --json`, `memev_doctor` | read | Health | in |
| `doctor --fix` | action | Health | next |
| `card --json`, `memev_session_start` | read | Overview, Projects | in |
| `start`, `stop`, `restart` | action | Health | next |
| `ready --json` | read | Health | next |
| `preflight` | read | Health | next |
| `selftest` | action | Health | next |
| `update <tarball> --sha256` | action | Settings, Update | next |

## Memory

| Core function | Kind | Page | Status |
|---|---|---|---|
| `recall --json`, `memev_recall` | read | Memory, Search | in |
| `why`, `memev_why`, `memev_explain`, `memev_provenance` | read | Memory, a hit's detail | next |
| `remember`, `memev_remember` | action | Memory, Add a note | next |
| `memev_list`, `memev_get` | read | Memory, Browse | next |
| `memev_write`, `memev_append` | action | Memory, a note's detail | next |
| `memev_link`, `memev_graph` | read and action | Memory, Links | next |
| `review list --json`, `memev_review` | read | Memory, Review | in |
| `review keep`, `review drop`, `review reset` | action | Memory, Review | next |
| `audit --json`, `memev_audit` | read | Memory, Clean-up | in |
| `compact --dry-run --json`, `compact` | read and action | Memory, Clean-up | next |
| `memev_feedback` | action | Memory, a hit's detail | next |
| `checkpoint`, `memev_checkpoint` | read and action | Projects, Work | next |
| `claims`, `memev_claims`, `memev_claim` | read and action | Memory, Claims | next |
| `import` | action | Memory, Import | next |

## Checked work (contracts)

| Core function | Kind | Page | Status |
|---|---|---|---|
| `contract list`, `memev_contract_list` | read | Checked work | in |
| `contract status <id>`, `memev_contract_status`, its receipt | read | Checked work, detail | next |
| `verify <id>`, `memev_contract_verify` | action | Checked work, detail | next |
| `contract new`, `from-request`, `memev_contract_draft`, `memev_contract_from_request` | action | Checked work, New | next |
| `contract sign`, `memev_contract_sign` | action | Checked work, detail (the owner's key) | next |
| `contract approve`, `memev_contract_approve` | action | Checked work, detail | next |
| `contract amend`, `memev_contract_amend`, `memev_scope_amend` | action | Checked work, detail | next |
| `contract decompose`, `memev_contract_decompose` | action | Checked work, New | next |
| `gate`, `contract hook` | read and action | Checked work, Settings | next |

## Projects and work

| Core function | Kind | Page | Status |
|---|---|---|---|
| `project`, `memev_project_status` | read | Projects | in (name), next (status) |
| `work status <project>` | read | Projects, Work | next |
| `work pause`, `resume`, `cancel`, `memev_project_pause`, `memev_project_resume` | action | Projects, Work | next |
| `work impact`, `memev_task_plan` | read | Projects, Work | next |
| `memev_task_claim`, `_release`, `_heartbeat`, `_update`, `_report`, `_verify`, `_cancel`, `_reassign` | action | Projects, a task's detail | next |
| `memev_tournament_match`, `memev_tournament_standings` | read and action | Projects, Compare | next |

## Bots and agents

| Core function | Kind | Page | Status |
|---|---|---|---|
| `bus roster --json`, `bus_roster` | read | Team | in |
| `bus_set_operator` | action | Team | next |
| `bus_check`, hello, rename | action | Team | next |
| `devices list --json` | read | Computers and apps | in |
| `devices enroll`, `remove`, `rotate`, `rename`, `keygen` | action | Computers and apps | next |
| `clients` | read | Apps on this computer | in |
| `connect`, `disconnect` | action | Apps on this computer | next |
| `token` (list, issue, revoke) | read and action | Access | next |
| `invite`, `join` | action | Computers and apps | next (join is in the guide) |
| `runner` (status, on, off) | read and action | Team | next |
| `agentid` | read | Team | next |

## Operations

| Core function | Kind | Page | Status |
|---|---|---|---|
| `bus tail`, `bus_tail` | read | Messages | next |
| `bus since --json`, `bus_since` | read | Messages, agent panes | next (panes wait for the `--from/--to` filters, SO-N-15) |
| `bus unread`, `bus_unread` | read | Messages | waits (`--peek`, SO-N-15) |
| `bus send`, `bus_send` | action | Workspace, lead conversation | next |
| `bus tasks --open`, `bus_tasks` | read | Messages | next |

## Settings

| Core function | Kind | Page | Status |
|---|---|---|---|
| `plan --json` | read | Plan | in |
| `license status --json` | read | Licence | in |
| `license activate`, `install`, `remove`, `upgrade` | action | Licence | next |
| `report status --json`, `report on`, `off`, `show` | read and action | Privacy | in (read), next (on, off, show) |
| `backup status`, `backup set` | read and action | Storage and backup | in (read), next (set; in the guide) |
| `github` (setup, status) | read and action | Storage and backup | next |
| `service` (install, remove, status) | read and action | General, Start at login | next |
| `path` (add, remove) | action | Advanced | next |
| `cloud` | read and action | Advanced | next |
| `deployment`, `container` | read | Advanced | next |
| `uninstall` | action | Advanced (with the app's own uninstall) | next |

## Not in the app

| Core function | Why |
|---|---|
| `hook` | Run by the AI apps on each event, never by a person. |
| `mcp`, `serve` | The server the apps talk to; the app is a client of it. |
| `init` | Run by the guided first launch, not as a page. |

## Found by the rev2 map (REV2-SCREENS.md), missing above

| Core function | Kind | Page | Status |
|---|---|---|---|
| `contracts on`, `off`, `status` | read and action | Checked work; Health "Work held to checks" | next |
| `onboarding --milestones --json`, `onboarding --json` | read | Settings, General: Set-up checklist; Install | next |
| `doctor --bundle` | action | Health; Help and support | next |
| `setup remote --code`, `stop`, `log` | action | Bots and agents, Connect: Get help setting up | next |
| `token rotate` | action | Bots and agents, Access | next |
| `deployment set` | action | Settings, Advanced: Where Memev runs | next |
| `project set` | action | Memory, Search; Settings, Advanced: Pin a name | next |

## Core gaps: drawn as working in rev2, with no core command yet

Each is closed by a core pull request in memev, never faked in the app. Until it lands, the element shows what the core says, is hidden, or carries "Sample, not built yet". Numbers are REV2-SCREENS.md section 3.

| Gap | Core addition | Status |
|---|---|---|
| G-7, G-8 agent panes and unread counts | `bus since --from --to`, `bus unread --peek` (quiet) | built on core/so-n-15-bus-filters |
| Runs by day chart | `runner status --days N --json` | built on core/so-n-15-bus-filters |
| G-1 Start a project | a project create command | next |
| G-2, G-3, G-4, G-5 milestones, risks, scope history, deliverables | readers for the project's plan, risks, scope log and artifacts | next (SO-A-05, SO-A-06, SO-A-09, SO-A-12) |
| G-6 model and effort per bot | a runner setting per seat | next |
| G-9 message to task | a message's task link | next |
| G-10 who the app sends as | `bus send --as` for the person's seat | next |
| G-11 connect an app | `clients --json` with ids (SR-2.09.2) | next |
| G-16 check for an update | a version check | next (SO-N-07) |
| G-17 back up now | `backup now` | next |
| G-18 invites | `invite list`, `invite cancel` | next |
| G-19, G-20, G-21 memory speed, sizes, integrity sweep | status fields from SR-1.35's measures, compaction's report while running, the sweep's last result | next |
| G-22 people on a project | `recall --kind person` | next |
| G-23 lease renewal time | the task's last heartbeat time | next |
| G-27 most recent memories | `recall --recent` | next |
