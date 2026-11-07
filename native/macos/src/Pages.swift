// SPDX-License-Identifier: Apache-2.0
// The pages over the core's own commands, as native/PAGES.md lists them. Read-only: nothing here writes, connects or sends.
// Each page reads the core off the main thread and fills its rows on it. Only the fields PAGES.md names are shown.
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

struct PageSection { let head: String; let rows: [PageRow]; let empty: String }

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

enum Pages {
  static func memory(query: String) -> [PageSection] {
    var out: [PageSection] = []
    if !query.trimmingCharacters(in: .whitespaces).isEmpty {
      let words = query.split(separator: " ").map(String.init)
      if let hits = memevJSON(["recall"] + words + ["--json"]) as? [[String: Any]] {
        out.append(PageSection(head: "SEARCH", rows: hits.prefix(20).map { h in
          let proj = str(h["project"])
          return PageRow(title: safe(str(h["snippet"])), sub: safe(str(h["heading"])) + (proj.isEmpty ? "" : " · \(proj)"), tone: 0)
        }, empty: "Nothing matched."))
      } else {
        out.append(PageSection(head: "SEARCH", rows: [], empty: "The search did not answer."))
      }
    }
    if let r = memevJSON(["review", "list", "--json"]) as? [String: Any], let inbox = r["inbox"] as? [[String: Any]] {
      out.append(PageSection(head: "REVIEW", rows: inbox.prefix(50).map { i in
        PageRow(title: safe(str(i["text"] ?? i["title"])), sub: safe(str(i["why"] ?? i["reason"] ?? i["flag"])), tone: 1)
      }, empty: "Nothing is waiting for review."))
    } else {
      out.append(PageSection(head: "REVIEW", rows: [], empty: "The review list did not answer."))
    }
    if let a = memevJSON(["audit", "--json"]) as? [String: Any] {
      out.append(PageSection(head: "CLEAN-UP", rows: [
        PageRow(title: "\(str(a["memories"])) memories, \(str(a["likely_junk"])) likely junk", sub: str(a["next"]), tone: 0),
      ], empty: ""))
    } else {
      out.append(PageSection(head: "CLEAN-UP", rows: [], empty: "The clean-up report did not answer."))
    }
    return out
  }

  static func checked() -> [PageSection] {
    guard let r = memevJSON(["contract", "list"]) as? [String: Any], let list = r["contracts"] as? [[String: Any]] else {
      return [PageSection(head: "CONTRACTS", rows: [], empty: "The contract list did not answer.")]
    }
    return [PageSection(head: "CONTRACTS", rows: list.map { c in
      let status = str(c["status"])
      let receipt = (c["receipt"] != nil || c["verified"] as? Bool == true) ? "receipt" : "no receipt yet"
      return PageRow(title: str(c["title"]), sub: "\(status) · \(receipt)", tone: status.contains("fail") ? 2 : 0)
    }, empty: "No contracts yet.")]
  }

  static func bots() -> [PageSection] {
    var out: [PageSection] = []
    if let r = memevJSON(["bus", "roster", "--json"]) as? [String: Any] {
      let seats = (r["seats"] as? [[String: Any]]) ?? []
      var rows = seats.map { s in
        PageRow(title: str(s["name"] ?? s["seat"]), sub: [str(s["client"] ?? s["app"]), (s["live"] as? Bool == true) ? "seen recently" : "not seen recently"].filter { !$0.isEmpty }.joined(separator: " · "), tone: 0)
      }
      let op = str(r["operator"])
      if !op.isEmpty { rows.insert(PageRow(title: "Lead: \(op)", sub: "", tone: 0), at: 0) }
      else if let need = r["lead_needed"] as? String { rows.insert(PageRow(title: "No lead is set.", sub: need, tone: 1), at: 0) }
      out.append(PageSection(head: "TEAM", rows: rows, empty: "No seats yet."))
    } else { out.append(PageSection(head: "TEAM", rows: [], empty: "The roster did not answer.")) }
    if let r = memevJSON(["devices", "list", "--json"]) as? [String: Any], let devs = r["devices"] as? [[String: Any]] {
      let kinds = ["host": "this computer", "local-client": "an app on this computer", "remote": "another computer"]
      out.append(PageSection(head: "COMPUTERS AND APPS", rows: devs.map { d in
        PageRow(title: str(d["label"]), sub: "\(kinds[str(d["kind"])] ?? str(d["kind"])) · \(str(d["status"]))", tone: str(d["status"]) == "active" ? 0 : 1)
      }, empty: "No computers or apps yet."))
    } else { out.append(PageSection(head: "COMPUTERS AND APPS", rows: [], empty: "The device list did not answer.")) }
    let (code, text) = memevRun(["clients"])
    let lines = code == 0 ? text.split(separator: "\n").map { String($0).trimmingCharacters(in: .whitespaces) }.filter { !$0.isEmpty } : []
    out.append(PageSection(head: "APPS ON THIS COMPUTER", rows: lines.prefix(40).map { PageRow(title: safe($0), sub: "", tone: 0) }, empty: "The app list did not answer."))
    return out
  }

  static func projects() -> [PageSection] {
    var rows: [PageRow] = []
    if let c = memevJSON(["card", "--json"]) as? [String: Any] {
      let p = str(c["project"]), star = str(c["north_star"])
      if !p.isEmpty && !p.contains("/") { rows.append(PageRow(title: p, sub: star.isEmpty ? "No north star yet." : star, tone: 0)) }
    }
    return [PageSection(head: "CURRENT PROJECT", rows: rows, empty: "No project is set for this notes folder yet.")]
  }

  static func settings() -> [PageSection] {
    var rows: [PageRow] = []
    if let p = memevJSON(["plan", "--json"]) as? [String: Any] {
      rows.append(PageRow(title: "Plan: \(str(p["plan"]))", sub: "\(str(p["computers_used"])) of \(str(p["computers_allowed"])) computers · \(str(p["connectors_counted"])) connectors", tone: 0))
    }
    if let l = memevJSON(["license", "status", "--json"]) as? [String: Any] {
      let lic = l["license"] as? [String: Any] ?? [:]
      rows.append(PageRow(title: "Licence: \(str(l["tier_label"]))", sub: [str(lic["state"]), str(lic["reason"])].filter { !$0.isEmpty }.joined(separator: " · "), tone: 0))
    }
    if let r = memevJSON(["report", "status", "--json"]) as? [String: Any] {
      rows.append(PageRow(title: "Anonymous report: \((r["enabled"] as? Bool == true) ? "on" : "off")", sub: "", tone: 0))
    }
    let (code, b) = memevRun(["backup", "status"])
    if code == 0 {
      let line = b.trimmingCharacters(in: .whitespacesAndNewlines)
      rows.append(PageRow(title: line.hasPrefix("Backing up to") ? "Backup: on" : "Backup: not set up", sub: "", tone: line.hasPrefix("Backing up to") ? 0 : 1))
    }
    rows.append(PageRow(title: "Theme: follows this computer", sub: "", tone: 0))
    return [PageSection(head: "SETTINGS", rows: rows, empty: "Settings did not answer.")]
  }
}
