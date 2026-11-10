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

  static func bots() -> PageData {
    var d = PageData(sections: [])
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
    let lines = code == 0 ? text.split(separator: "\n").map { String($0).trimmingCharacters(in: .whitespaces) }.filter { !$0.isEmpty } : []
    d.sections.append(PageSection(head: "APPS ON THIS COMPUTER", rows: lines.prefix(40).map { PageRow(title: safe($0), sub: "", tone: 0) },
                                  empty: "The app list did not answer.", tab: "connect", source: "memev clients"))
    d.sentences["connect"] = "The AI apps on this computer, and the computers and apps that reach your memory, as the core lists them."
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
