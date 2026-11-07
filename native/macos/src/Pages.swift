// SPDX-License-Identifier: Apache-2.0
// The pages over the core's own commands, as native/PAGES.md lists them. Read-only: nothing here writes, connects or sends.
// Each page reads the core off the main thread and fills its rows on it. Only the fields PAGES.md names are shown.
// Each section names the rev2 tab it sits under (native/REV2-SCREENS.md section 2) and the command behind it.
import AppKit

/// Runs memev with a fixed argument list. Returns (exit code, stdout); code -1 when it could not run.
func memevRun(_ args: [String], timeout: Int = 10000) -> (Int, String) {
  var buf = [CChar](repeating: 0, count: 1 << 20)
  let cs = args.map { strdup($0) }
  defer { for c in cs { free(c) } }
  let ptrs = cs.map { UnsafePointer($0) }
  let code = ptrs.withUnsafeBufferPointer { memev_shim_run($0.baseAddress, Int32(args.count), &buf, Int32(buf.count), Int32(timeout)) }
  return (Int(code), String(cString: buf))
}

func memevJSON(_ args: [String]) -> Any? {
  let (code, out) = memevRun(args)
  guard code == 0, let d = out.data(using: .utf8) else { return nil }
  return try? JSONSerialization.jsonObject(with: d)
}

/// One row of a page: a title line and an optional dimmer second line.
struct PageRow { let title: String; let sub: String; let tone: Int } // tone 0 plain, 1 warn, 2 bad

struct PageSection {
  let head: String
  let rows: [PageRow]
  let empty: String
  var tab = ""
  var source = ""
}

/// What one screen's read returned: its sections, and the sentence for each tab that the core's answers can fill.
struct PageData {
  var sections: [PageSection]
  var sentences: [String: String] = [:]
  var counts: [String: Int] = [:]
  /// The apps the core found (REV2-SCREENS.md 5.1), and whether onboarding --json answered.
  var apps: [AppItem] = []
  var appsRead = false
  /// The background agents (5.3), and whether runner status --json answered.
  var runners: [RunnerRow] = []
  var runnersRead = false
}

/// One app the core found on this computer, from onboarding --json (REV2-SCREENS.md 5.1).
struct AppItem {
  let id: String
  let name: String
  let status: String
  let detail: String
  var note = ""
  /// Found and supported, not connected: the core's own step is exactly `memev connect <id>`.
  var connectable: Bool { status == "needs-manual" && detail == "memev connect \(id)" }
  var connected: Bool { status == "ready" || status == "added" }
  var disconnectedByYou: Bool { status == "skipped" }
  /// What the row's second line says.
  var state: String {
    if connectable { return "found \u{00B7} not connected" }
    if connected { return note.isEmpty ? "connected" : "connected \u{00B7} \(note)" }
    if disconnectedByYou { return "you disconnected it" }
    return detail
  }
}

/// One background agent, from runner status --json (REV2-SCREENS.md 5.3).
struct RunnerRow {
  let seat: String
  let enabled: Bool
  let maxPerDay: Int
  let today: Int
}

/// An id the core printed: ASCII letters, digits, - _ . only, at most 64 characters, as the Windows window takes them.
func plainId(_ s: String) -> Bool {
  !s.isEmpty && s.count <= 64 && s.unicodeScalars.allSatisfy { $0.isASCII && (CharacterSet.alphanumerics.contains($0) || "-_.".unicodeScalars.contains($0)) }
}

/// The person's own home shown as ~ (owner ruling D-4): their paths are shown, shortened.
func showPath(_ s: String) -> String {
  let home = NSHomeDirectory()
  return home.count > 1 ? s.replacingOccurrences(of: home, with: "~") : s
}

/// The core's answer as JSON, whatever its exit code: onboarding --json exits non-zero while a step is not done yet.
func memevJSONAnyCode(_ args: [String], timeout: Int = 60000) -> Any? {
  let (code, out) = memevRun(args, timeout: timeout)
  guard code >= 0, let d = out.data(using: .utf8) else { return nil }
  return try? JSONSerialization.jsonObject(with: d)
}

func str(_ v: Any?) -> String {
  switch v {
  case let s as String: return s.replacingOccurrences(of: "\n", with: " ")
  case let n as NSNumber: return n.stringValue
  default: return ""
  }
}

/// Words that never reach a page: anything shaped like a path in a user's home or a token.
func safe(_ s: String) -> String {
  if s.contains("/Users/") || s.contains("/home/") || s.contains("/private/") || s.contains("/var/folders/") { return "" }
  return s
}

func plural(_ n: Int, _ one: String, _ many: String) -> String { "\(n) \(n == 1 ? one : many)" }

enum Pages {
  static func memory(query: String) -> PageData {
    var d = PageData(sections: [])
    let recall = "memev recall <words> --json"
    if !query.trimmingCharacters(in: .whitespaces).isEmpty {
      let words = query.split(separator: " ").map(String.init)
      if let hits = memevJSON(["recall"] + words + ["--json"]) as? [[String: Any]] {
        d.sections.append(PageSection(head: "SEARCH", rows: hits.prefix(20).map { h in
          let proj = str(h["project"])
          return PageRow(title: safe(str(h["snippet"])), sub: safe(str(h["heading"])) + (proj.isEmpty ? "" : " · \(proj)"), tone: 0)
        }, empty: "Nothing matched.", tab: "search", source: recall))
      } else {
        d.sections.append(PageSection(head: "SEARCH", rows: [], empty: "The search did not answer.", tab: "search", source: recall))
      }
    }
    let review = "memev review list --json"
    if let r = memevJSON(["review", "list", "--json"]) as? [String: Any], let inbox = r["inbox"] as? [[String: Any]] {
      d.sections.append(PageSection(head: "REVIEW", rows: inbox.prefix(50).map { i in
        PageRow(title: safe(str(i["text"] ?? i["title"])), sub: safe(str(i["why"] ?? i["reason"] ?? i["flag"])), tone: 1)
      }, empty: "Nothing is waiting for review.", tab: "review", source: review))
      let n = inbox.count
      d.counts["review"] = n
      d.sentences["review"] = n == 0 ? "Nothing is waiting for review." : "\(plural(n, "memory", "memories")) to review. Keep what is still true, drop what is not."
      d.sentences["search"] = n == 0 ? "Search everything your AI apps have saved." : "Search everything your AI apps have saved. \(n) to review."
    } else {
      d.sections.append(PageSection(head: "REVIEW", rows: [], empty: "The review list did not answer.", tab: "review", source: review))
    }
    if let a = memevJSON(["audit", "--json"]) as? [String: Any] {
      d.sections.append(PageSection(head: "CLEAN-UP", rows: [
        PageRow(title: "\(str(a["memories"])) memories, \(str(a["likely_junk"])) likely junk", sub: str(a["next"]), tone: 0),
      ], empty: "", tab: "review", source: "memev audit --json"))
    } else {
      d.sections.append(PageSection(head: "CLEAN-UP", rows: [], empty: "The clean-up report did not answer.", tab: "review", source: "memev audit --json"))
    }
    return d
  }

  static func checked() -> PageData {
    let src = "memev contract list"
    guard let r = memevJSON(["contract", "list"]) as? [String: Any], let list = r["contracts"] as? [[String: Any]] else {
      return PageData(sections: [PageSection(head: "CONTRACTS", rows: [], empty: "The contract list did not answer.", source: src)],
                      sentences: ["": "The contract list did not answer."])
    }
    // contracts.mjs contractList(): state is done (its verify passed, with a receipt), open (signed, not verified yet), superseded or draft
    let words = ["done": "checked: its verify passed", "open": "signed, not checked yet", "superseded": "replaced by a newer version", "draft": "a draft, not signed"]
    let done = list.filter { str($0["state"]) == "done" }.count
    let sentence = list.isEmpty ? "No contracts yet." : "\(plural(list.count, "contract", "contracts")). \(done) checked."
    return PageData(sections: [PageSection(head: "CONTRACTS", rows: list.map { c in
      let state = str(c["state"])
      let v = str(c["version"])
      return PageRow(title: str(c["title"]) + (v.isEmpty || v == "1" ? "" : " (version \(v))"), sub: words[state] ?? state, tone: state == "open" || state == "draft" ? 1 : 0)
    }, empty: "No contracts yet.", source: src)], sentences: ["": sentence])
  }

  /// onboarding --json: the one output that names each app's id (its items' `app`). Items with no app (the vault, the bus,
  /// Memev running) are left out, so their paths never reach the window; a connected app's one manual step stays with it.
  static func apps() -> ([AppItem], Bool) {
    guard let items = memevJSONAnyCode(["onboarding", "--json"]) as? [[String: Any]] else { return ([], false) }
    var out: [AppItem] = []
    for it in items {
      let id = str(it["app"])
      if !plainId(id) { continue }
      let detail = showPath(str(it["detail"]))
      if it["note"] as? Bool == true {
        if let i = out.firstIndex(where: { $0.id == id }) { out[i].note = detail }
        continue
      }
      if out.contains(where: { $0.id == id }) { continue }
      let step = str(it["step"])
      out.append(AppItem(id: id, name: step.isEmpty ? id : step, status: str(it["status"]), detail: detail))
    }
    return (out, true)
  }

  /// runner status --json: each background agent's seat, whether it is on, and its runs today against its daily limit.
  static func runners() -> ([RunnerRow], Bool) {
    guard let list = memevJSON(["runner", "status", "--json"]) as? [[String: Any]] else { return ([], false) }
    let rows = list.compactMap { r -> RunnerRow? in
      let seat = str(r["seat"])
      if seat.isEmpty { return nil }
      return RunnerRow(seat: seat, enabled: r["enabled"] as? Bool == true, maxPerDay: Int(str(r["max_runs_per_day"])) ?? 0, today: Int(str(r["today"])) ?? 0)
    }
    return (rows, true)
  }

  static func bots() -> PageData {
    var d = PageData(sections: [])
    (d.apps, d.appsRead) = apps()
    (d.runners, d.runnersRead) = runners()
    let rosterSrc = "memev bus roster --json"
    if let r = memevJSON(["bus", "roster", "--json"]) as? [String: Any] {
      // bus.mjs roster(): a row is a seat, or a computer with no app yet (seat null); state is live, stale, never joined or removed
      let seats = (r["seats"] as? [[String: Any]]) ?? []
      let states = ["live": "here now", "stale": "not seen recently", "never joined": "has not opened a session yet", "removed": "removed"]
      var rows = seats.map { s -> PageRow in
        let seat = str(s["seat"]), state = str(s["state"])
        let title = seat.isEmpty ? str(s["device_label"]) : seat
        let sub = seat.isEmpty ? "a computer with no AI app on it yet" : [s["operator"] as? Bool == true ? "the lead" : "", states[state] ?? state].filter { !$0.isEmpty }.joined(separator: " · ")
        return PageRow(title: title, sub: sub, tone: state == "live" || seat.isEmpty ? 0 : state == "removed" ? 2 : 1)
      }
      if str(r["operator"]).isEmpty { rows.insert(PageRow(title: "No lead is set.", sub: "Choose which AI leads and coordinates the others.", tone: 1), at: 0) }
      d.sections.append(PageSection(head: "TEAM", rows: rows, empty: "No seats yet.", tab: "team", source: rosterSrc))
      let named = seats.filter { !str($0["seat"]).isEmpty }
      let live = named.filter { str($0["state"]) == "live" }.count
      let quiet = named.filter { str($0["state"]) == "stale" }.map { str($0["seat"]) }
      var s = named.isEmpty ? "No bots or agents have joined yet." : "\(plural(named.count, "bot or agent", "bots and agents")). \(live) live."
      if let q = quiet.first { s += quiet.count == 1 ? " \(q) has not been seen recently." : " \(quiet.count) have not been seen recently." }
      d.sentences["team"] = s
      d.counts["quiet"] = quiet.count
    } else {
      d.sections.append(PageSection(head: "TEAM", rows: [], empty: "The roster did not answer.", tab: "team", source: rosterSrc))
    }
    let devSrc = "memev devices list --json"
    if let r = memevJSON(["devices", "list", "--json"]) as? [String: Any], let devs = r["devices"] as? [[String: Any]] {
      // devices.mjs KINDS
      let kinds = ["host": "this computer", "local-client": "an app on this computer", "remote-computer": "another computer", "cloud-connector": "a web app"]
      d.sections.append(PageSection(head: "COMPUTERS AND APPS", rows: devs.map { dv in
        PageRow(title: str(dv["label"]), sub: "\(kinds[str(dv["kind"])] ?? str(dv["kind"])) · \(str(dv["status"]))", tone: str(dv["status"]) == "active" ? 0 : 1)
      }, empty: "No computers or apps yet.", tab: "connect", source: devSrc))
    } else {
      d.sections.append(PageSection(head: "COMPUTERS AND APPS", rows: [], empty: "The device list did not answer.", tab: "connect", source: devSrc))
    }
    let (code, text) = memevRun(["clients"])
    let lines = code == 0 ? text.split(separator: "\n").map { String($0) } : []
    d.counts["notFound"] = lines.filter { $0.hasSuffix("not found") }.count
    let found = d.apps.count
    let connected = d.apps.filter { $0.connected }.count
    d.sentences["connect"] = !d.appsRead ? "The list of AI apps did not answer."
      : found == 0 ? "No AI apps were found on this computer."
      : "\(connected) of \(found) AI apps found on this computer \(connected == 1 ? "is" : "are") connected."
    let on = d.runners.filter { $0.enabled }.count
    d.sentences["agents"] = !d.runnersRead ? "The background agents did not answer."
      : on == 0 ? "No background agent is on." : on == 1 ? "1 background agent is on." : "\(on) background agents are on."
    return d
  }

  static func projects() -> PageData {
    var rows: [PageRow] = []
    if let c = memevJSON(["card", "--json"]) as? [String: Any] {
      let p = str(c["project"]), star = str(c["north_star"])
      if !p.isEmpty && !p.contains("/") { rows.append(PageRow(title: p, sub: star.isEmpty ? "No north star yet." : star, tone: 0)) }
    }
    let sentence = rows.isEmpty ? "No project is set for this notes folder yet." : "1 project: \(rows[0].title)."
    return PageData(sections: [PageSection(head: "CURRENT PROJECT", rows: rows, empty: "No project is set for this notes folder yet.", source: "memev card --json")],
                    sentences: ["": sentence])
  }

  /// Settings, split by tab: General (the anonymous report and the theme), Plan and computers, Storage and backup.
  static func settings(theme: String) -> PageData {
    var d = PageData(sections: [])
    var general: [PageRow] = []
    var plan: [PageRow] = []
    var backup: [PageRow] = []
    if let p = memevJSON(["plan", "--json"]) as? [String: Any] {
      plan.append(PageRow(title: "Plan: \(str(p["plan"]))", sub: "\(str(p["computers_used"])) of \(str(p["computers_allowed"])) computers · \(str(p["connectors_counted"])) connectors", tone: 0))
      d.sentences["plan"] = "\(str(p["plan"])). \(str(p["computers_used"])) of \(str(p["computers_allowed"])) computers."
    }
    if let l = memevJSON(["license", "status", "--json"]) as? [String: Any] {
      let lic = l["license"] as? [String: Any] ?? [:]
      plan.append(PageRow(title: "Licence: \(str(l["tier_label"]))", sub: [str(lic["state"]), str(lic["reason"])].filter { !$0.isEmpty }.joined(separator: " · "), tone: 0))
    }
    if let r = memevJSON(["report", "status", "--json"]) as? [String: Any] {
      general.append(PageRow(title: "Anonymous report: \((r["enabled"] as? Bool == true) ? "on" : "off")", sub: "", tone: 0))
    }
    general.append(PageRow(title: "Theme: \(theme)", sub: "", tone: 0))
    let (code, b) = memevRun(["backup", "status"])
    if code == 0 {
      let on = b.trimmingCharacters(in: .whitespacesAndNewlines).hasPrefix("Backing up to")
      backup.append(PageRow(title: on ? "Backup: on" : "Backup: not set up", sub: "", tone: on ? 0 : 1))
      d.sentences["storage"] = on ? "Backup is on." : "Backup is not set up."
    }
    d.sections = [
      PageSection(head: "GENERAL", rows: general, empty: "", tab: "general", source: "memev report status --json"),
      PageSection(head: "PLAN", rows: plan, empty: "The plan did not answer.", tab: "plan", source: "memev plan --json · memev license status --json"),
      PageSection(head: "BACKUP", rows: backup, empty: "The backup status did not answer.", tab: "storage", source: "memev backup status"),
    ]
    return d
  }
}
