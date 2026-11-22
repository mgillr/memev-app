/* SPDX-License-Identifier: Apache-2.0 */
#include "ops.h"
#include "read.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static int safe_name(const char *s) {
  size_t n, i;
  if (!s) return 0;
  n = strlen(s);
  if (n == 0 || n >= OPS_NAME) return 0;
  for (i = 0; i < n; i++) {
    char c = s[i];
    int ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
      || c == '_' || c == '-' || c == '.' || c == '@' || c == '+';
    if (!ok) return 0;
  }
  return 1;
}

static int banned(const char *s) {
  return s && strpbrk(s, "\"%!&|<>^()\r\n") != NULL;
}

static void flatten(char *dst, size_t n, const char *src) {
  size_t i = 0;
  if (!src) src = "";
  for (; *src && i + 1 < n; src++) {
    unsigned char c = (unsigned char)*src;
    dst[i++] = (c < 32 || c == 127) ? ' ' : (char)c;
  }
  dst[i] = 0;
}

static int has_path(const char *s) {
  return s && (strstr(s, "://") || strstr(s, "/home/") || strstr(s, "/Users/")
    || strstr(s, "/tmp/") || strstr(s, "/var/") || strstr(s, "\\"));
}

static const char *after_key(const char *json, const char *key) {
  char pat[96];
  const char *p = json;
  size_t klen;
  if (!json || !key) return NULL;
  snprintf(pat, sizeof pat, "\"%s\"", key);
  klen = strlen(pat);
  while ((p = strstr(p, pat)) != NULL) {
    const char *q = p + klen;
    while (*q == ' ' || *q == '\n' || *q == '\r' || *q == '\t') q++;
    if (*q == ':') {
      q++;
      while (*q == ' ' || *q == '\n' || *q == '\r' || *q == '\t') q++;
      return q;
    }
    p += klen;
  }
  return NULL;
}

typedef int (*obj_fn)(const char *obj, void *ud);

static int walk_objects(const char *array, obj_fn fn, void *ud) {
  const char *p;
  int n = 0;
  if (!array || *array != '[') return -1;
  p = array + 1;
  while (*p) {
    while (*p == ' ' || *p == '\n' || *p == '\r' || *p == '\t' || *p == ',') p++;
    if (*p == ']') return 0;
    if (*p != '{') return -1;
    {
      const char *start = p;
      int depth = 0;
      char *buf;
      size_t len;
      do {
        if (*p == '"') {
          p++;
          while (*p && *p != '"') {
            if (*p == '\\' && p[1]) p += 2;
            else p++;
          }
          if (*p == '"') p++;
          continue;
        }
        if (*p == '{' || *p == '[') depth++;
        else if (*p == '}' || *p == ']') depth--;
        if (*p) p++;
      } while (*p && depth > 0);
      len = (size_t)(p - start);
      if (n < 80 && len > 1 && len < 16000) {
        buf = malloc(len + 1);
        if (!buf) return -1;
        memcpy(buf, start, len);
        buf[len] = 0;
        if (fn(buf, ud) != 0) { free(buf); return -1; }
        free(buf);
      }
      n++;
    }
  }
  return -1;
}

static int take_seat(const char *obj, void *ud) {
  OpsView *v = ud;
  char name[OPS_NAME], state[32], app[OPS_NAME], flag[8];
  OpsSeat *seat;
  if (v->nseats >= OPS_SEATS) return 0;
  name[0] = state[0] = app[0] = 0;
  if (memev_json_get(obj, "seat", name, sizeof name) != 0 || !safe_name(name)) return 0;
  if (memev_json_get(obj, "state", state, sizeof state) != 0) state[0] = 0;
  if (memev_json_get(obj, "app", app, sizeof app) != 0 || !safe_name(app)) app[0] = 0;
  seat = &v->seats[v->nseats++];
  memcpy(seat->name, name, sizeof seat->name);
  flatten(seat->state, sizeof seat->state, state);
  memcpy(seat->app, app, sizeof seat->app);
  seat->lead = memev_json_get(obj, "operator", flag, sizeof flag) == 0 && strcmp(flag, "true") == 0;
  return 0;
}

static int take_msg(const char *obj, void *ud) {
  OpsView *v = ud;
  char from[OPS_NAME], to[OPS_NAME], body[OPS_LINE];
  OpsMsg *m;
  if (v->nmsgs >= OPS_MSGS) return 0;
  if (memev_json_get(obj, "from", from, sizeof from) != 0 || !safe_name(from)) return 0;
  if (memev_json_get(obj, "to", to, sizeof to) != 0 || !safe_name(to)) return 0;
  if (memev_json_get(obj, "body", body, sizeof body) != 0) return 0;
  if (has_path(body) || banned(body)) return 0;
  m = &v->msgs[v->nmsgs++];
  memcpy(m->from, from, sizeof m->from);
  memcpy(m->to, to, sizeof m->to);
  flatten(m->body, sizeof m->body, body);
  return 0;
}

int ops_read_roster(const char *json, OpsView *v) {
  char lead[OPS_NAME];
  const char *seats;
  if (!v) return -1;
  v->lead[0] = 0;
  v->nseats = 0;
  if (!json) return -1;
  if (memev_json_get(json, "operator", lead, sizeof lead) == 0 && safe_name(lead))
    memcpy(v->lead, lead, sizeof v->lead);
  seats = after_key(json, "seats");
  if (!seats || *seats != '[') return -1;
  return walk_objects(seats, take_seat, v);
}

int ops_read_messages(const char *json, OpsView *v) {
  const char *p;
  if (!v) return -1;
  v->nmsgs = 0;
  if (!json) return -1;
  p = json;
  while (*p == ' ' || *p == '\n' || *p == '\r' || *p == '\t') p++;
  if (*p != '[') return -1;
  return walk_objects(p, take_msg, v);
}

void ops_sentence(OpsView *v) {
  if (!v) return;
  if (v->lead[0]) snprintf(v->sentence, sizeof v->sentence, "%s leads. Tell it what you want done.", v->lead);
  else snprintf(v->sentence, sizeof v->sentence, "%s", "No lead is set. Pick one, then tell it what you want done.");
}

static int run_at(const char *bin, char *const argv[], char *out, size_t n) {
  int code = 0;
  if (!bin || memev_capture(bin, argv, out, n, 15000, &code) != 0 || code != 0) return -1;
  return 0;
}

int ops_load(const char *bin, OpsView *v) {
  char out[262144];
  char *roster[5];
  char *since[6];
  if (!v) return -1;
  memset(v, 0, sizeof *v);
  roster[0] = (char *)bin; roster[1] = "bus"; roster[2] = "roster"; roster[3] = "--json"; roster[4] = NULL;
  if (run_at(bin, roster, out, sizeof out) != 0 || ops_read_roster(out, v) != 0) {
    snprintf(v->note, sizeof v->note, "%s", "The roster could not be read.");
    snprintf(v->sentence, sizeof v->sentence, "%s", v->note);
    return -1;
  }
  since[0] = (char *)bin; since[1] = "bus"; since[2] = "since"; since[3] = "0"; since[4] = "--json"; since[5] = NULL;
  if (run_at(bin, since, out, sizeof out) != 0 || ops_read_messages(out, v) != 0)
    snprintf(v->note, sizeof v->note, "%s", "The message list could not be read.");
  ops_sentence(v);
  return v->note[0] ? -1 : 0;
}

int ops_send_argv(const char *lead, const char *text, char out[][OPS_ARG], int *argc, char *why, size_t whyn) {
  if (why && whyn) why[0] = 0;
  if (!lead || !lead[0] || !safe_name(lead)) {
    if (why) snprintf(why, whyn, "%s", "No lead is set. Pick one, then tell it what you want done.");
    return -1;
  }
  if (!text || !text[0] || strspn(text, " \t") == strlen(text)) {
    if (why) snprintf(why, whyn, "%s", "Write what you want done.");
    return -1;
  }
  if (strlen(text) >= 400 || banned(text) || has_path(text)) {
    if (why) snprintf(why, whyn, "%s", "That message was not sent.");
    return -1;
  }
  snprintf(out[0], OPS_ARG, "%s", "bus");
  snprintf(out[1], OPS_ARG, "%s", "send");
  snprintf(out[2], OPS_ARG, "%s", lead);
  snprintf(out[3], OPS_ARG, "%s", "task");
  snprintf(out[4], OPS_ARG, "%s", text);
  *argc = 5;
  return 0;
}

int ops_lead_argv(const OpsView *v, const char *seat, char out[][OPS_ARG], int *argc, char *why, size_t whyn) {
  int i, found = 0;
  if (why && whyn) why[0] = 0;
  if (!v || !seat || !safe_name(seat)) {
    if (why) snprintf(why, whyn, "%s", "That seat cannot be used.");
    return -1;
  }
  for (i = 0; i < v->nseats; i++) if (strcmp(v->seats[i].name, seat) == 0) found = 1;
  if (!found) {
    if (why) snprintf(why, whyn, "%s", "That seat is not on the roster.");
    return -1;
  }
  snprintf(out[0], OPS_ARG, "%s", "bus");
  snprintf(out[1], OPS_ARG, "%s", "operator");
  snprintf(out[2], OPS_ARG, "%s", seat);
  *argc = 3;
  return 0;
}

static void add_line(char *out, size_t n, const char *line) {
  size_t len;
  if (!line || !line[0] || has_path(line)) return;
  len = strlen(out);
  if (len > 0 && len + 1 < n) out[len++] = '\n';
  if (len + 1 >= n) { out[n - 1] = 0; return; }
  snprintf(out + len, n - len, "%s", line);
}

static const char *state_words(const char *state) {
  if (strcmp(state, "live") == 0) return "here now";
  if (strcmp(state, "stale") == 0) return "not seen recently";
  if (strcmp(state, "never joined") == 0) return "has not opened a session yet";
  if (strcmp(state, "removed") == 0) return "removed";
  return state && state[0] ? state : "not seen";
}

static const char *kind_words(const char *kind) {
  if (strcmp(kind, "host") == 0) return "this computer";
  if (strcmp(kind, "local-client") == 0) return "an app on this computer";
  if (strcmp(kind, "remote-computer") == 0) return "another computer";
  if (strcmp(kind, "cloud-connector") == 0) return "a web app";
  return kind && kind[0] ? kind : "a computer";
}

static const char *contract_words(const char *state) {
  if (strcmp(state, "done") == 0) return "checked: its verify passed";
  if (strcmp(state, "open") == 0) return "signed, not checked yet";
  if (strcmp(state, "superseded") == 0) return "replaced by a newer version";
  if (strcmp(state, "draft") == 0) return "a draft, not signed";
  return state && state[0] ? state : "unknown";
}

struct list_ud { char *out; size_t n; int count; const char *mode; };

static int take_line(const char *obj, void *ud) {
  struct list_ud *u = ud;
  char a[160], b[200], c[80], line[360];
  a[0] = b[0] = c[0] = line[0] = 0;
  if (strcmp(u->mode, "recall") == 0) {
    memev_json_get(obj, "heading", a, sizeof a);
    memev_json_get(obj, "snippet", b, sizeof b);
    memev_json_get(obj, "project", c, sizeof c);
    if (!a[0] && !b[0]) return 0;
    if (c[0] && !has_path(c)) snprintf(line, sizeof line, "%s — %s · %s", a[0] ? a : "A note", b, c);
    else snprintf(line, sizeof line, "%s — %s", a[0] ? a : "A note", b);
  } else if (strcmp(u->mode, "review") == 0) {
    memev_json_get(obj, "snippet", a, sizeof a);
    if (!a[0]) memev_json_get(obj, "text", a, sizeof a);
    memev_json_get(obj, "why", b, sizeof b);
    if (!b[0]) memev_json_get(obj, "reason", b, sizeof b);
    if (!a[0]) return 0;
    snprintf(line, sizeof line, "%s%s%s", a, b[0] ? " — " : "", b);
  } else if (strcmp(u->mode, "contract") == 0) {
    char state[32], version[16];
    state[0] = version[0] = 0;
    memev_json_get(obj, "title", a, sizeof a);
    memev_json_get(obj, "state", state, sizeof state);
    memev_json_get(obj, "version", version, sizeof version);
    if (!a[0] || has_path(a)) return 0;
    if (version[0] && strcmp(version, "1") != 0) snprintf(line, sizeof line, "%s (version %s) — %s", a, version, contract_words(state));
    else snprintf(line, sizeof line, "%s — %s", a, contract_words(state));
  } else if (strcmp(u->mode, "device") == 0) {
    char kind[40], status[24];
    kind[0] = status[0] = 0;
    memev_json_get(obj, "label", a, sizeof a);
    memev_json_get(obj, "kind", kind, sizeof kind);
    memev_json_get(obj, "status", status, sizeof status);
    if (has_path(a) || strcmp(a, kind_words(kind)) == 0) a[0] = 0;
    if (a[0]) snprintf(line, sizeof line, "%s · %s · %s", a, kind_words(kind), status[0] ? status : "unknown");
    else snprintf(line, sizeof line, "%s · %s", kind_words(kind), status[0] ? status : "unknown");
  }
  if (line[0]) { add_line(u->out, u->n, line); u->count++; }
  return 0;
}

static void page_projects(const char *bin, char *out, size_t n) {
  char raw[65536], project[80], star[160], line[280];
  char *argv[4];
  argv[0] = (char *)bin; argv[1] = "card"; argv[2] = "--json"; argv[3] = NULL;
  add_line(out, n, "# CURRENT PROJECT");
  if (run_at(bin, argv, raw, sizeof raw) != 0) {
    add_line(out, n, "The project did not answer.");
    return;
  }
  project[0] = star[0] = 0;
  memev_json_get(raw, "project", project, sizeof project);
  memev_json_get(raw, "north_star", star, sizeof star);
  if (!project[0] || has_path(project)) {
    add_line(out, n, "No project is set for this notes folder yet.");
    return;
  }
  if (star[0] && !has_path(star)) snprintf(line, sizeof line, "%s — %s", project, star);
  else snprintf(line, sizeof line, "%s — No north star yet.", project);
  add_line(out, n, line);
}

static void page_bots(const char *bin, char *out, size_t n) {
  OpsView v;
  char raw[262144], line[240];
  char *argv[5];
  int i;
  add_line(out, n, "# TEAM");
  if (ops_load(bin, &v) != 0 && !v.lead[0] && v.nseats == 0) {
    add_line(out, n, v.note[0] ? v.note : "The roster did not answer.");
  } else {
    add_line(out, n, v.sentence);
    if (v.note[0]) add_line(out, n, v.note);
    if (v.nseats == 0) add_line(out, n, "No seats yet.");
    for (i = 0; i < v.nseats; i++) {
      const char *words = state_words(v.seats[i].state);
      if (v.seats[i].lead || strcmp(v.seats[i].name, v.lead) == 0)
        snprintf(line, sizeof line, "%s · the lead · %s", v.seats[i].name, words);
      else if (v.seats[i].app[0] && strcmp(v.seats[i].app, v.seats[i].name) != 0)
        snprintf(line, sizeof line, "%s · %s · %s", v.seats[i].name, v.seats[i].app, words);
      else
        snprintf(line, sizeof line, "%s · %s", v.seats[i].name, words);
      add_line(out, n, line);
    }
  }
  add_line(out, n, "# COMPUTERS AND APPS");
  argv[0] = (char *)bin; argv[1] = "devices"; argv[2] = "list"; argv[3] = "--json"; argv[4] = NULL;
  if (run_at(bin, argv, raw, sizeof raw) != 0) add_line(out, n, "The device list did not answer.");
  else {
    const char *arr = after_key(raw, "devices");
    struct list_ud u = {out, n, 0, "device"};
    if (!arr || walk_objects(arr, take_line, &u) != 0) add_line(out, n, "The device list did not answer.");
    else if (u.count == 0) add_line(out, n, "No computers or apps yet.");
  }
  add_line(out, n, "# APPS ON THIS COMPUTER");
  argv[0] = (char *)bin; argv[1] = "clients"; argv[2] = NULL;
  if (run_at(bin, argv, raw, sizeof raw) != 0) add_line(out, n, "The app list did not answer.");
  else {
    char *save = NULL, *linep = strtok_r(raw, "\n", &save);
    int any = 0;
    while (linep) {
      while (*linep == ' ' || *linep == '\t') linep++;
      if (*linep && !has_path(linep)) { add_line(out, n, linep); any = 1; }
      linep = strtok_r(NULL, "\n", &save);
    }
    if (!any) add_line(out, n, "The app list did not answer.");
  }
}

static int word_ok(const char *w) {
  size_t n, i;
  if (!w) return 0;
  n = strlen(w);
  if (n == 0 || n > 40 || banned(w) || has_path(w)) return 0;
  for (i = 0; i < n; i++) {
    unsigned char c = (unsigned char)w[i];
    if (c <= 32) return 0;
  }
  return 1;
}

static void page_memory(const char *bin, const char *query, char *out, size_t n) {
  char raw[262144];
  char *argv[12];
  int argc = 0, i;
  char words[8][48];
  int nw = 0;
  const char *p = query ? query : "";
  add_line(out, n, "# SEARCH");
  while (*p && nw < 8) {
    while (*p == ' ' || *p == '\t') p++;
    if (!*p) break;
    i = 0;
    while (*p && *p != ' ' && *p != '\t' && i + 1 < (int)sizeof words[0]) words[nw][i++] = *p++;
    words[nw][i] = 0;
    while (*p && *p != ' ' && *p != '\t') p++;
    if (!word_ok(words[nw])) {
      add_line(out, n, "That search was not sent.");
      nw = -1;
      break;
    }
    nw++;
  }
  if (nw == 0) add_line(out, n, "Type what you want to find.");
  else if (nw > 0) {
    argv[argc++] = (char *)bin;
    argv[argc++] = "recall";
    for (i = 0; i < nw; i++) argv[argc++] = words[i];
    argv[argc++] = "--json";
    argv[argc] = NULL;
    if (run_at(bin, argv, raw, sizeof raw) != 0) add_line(out, n, "The search did not answer.");
    else {
      struct list_ud u = {out, n, 0, "recall"};
      const char *arr = raw;
      while (*arr == ' ' || *arr == '\n') arr++;
      if (*arr != '[' || walk_objects(arr, take_line, &u) != 0) add_line(out, n, "The search did not answer.");
      else if (u.count == 0) add_line(out, n, "Nothing matched.");
    }
  }
  add_line(out, n, "# REVIEW");
  argv[0] = (char *)bin; argv[1] = "review"; argv[2] = "list"; argv[3] = "--json"; argv[4] = NULL;
  if (run_at(bin, argv, raw, sizeof raw) != 0) add_line(out, n, "The review list did not answer.");
  else {
    const char *arr = after_key(raw, "inbox");
    struct list_ud u = {out, n, 0, "review"};
    if (!arr || walk_objects(arr, take_line, &u) != 0) add_line(out, n, "The review list did not answer.");
    else if (u.count == 0) add_line(out, n, "Nothing is waiting for review.");
  }
  add_line(out, n, "# CLEAN-UP");
  argv[0] = (char *)bin; argv[1] = "audit"; argv[2] = "--json"; argv[3] = NULL;
  if (run_at(bin, argv, raw, sizeof raw) != 0) add_line(out, n, "The clean-up report did not answer.");
  else {
    char memories[24], junk[24], next[200], line[280];
    memories[0] = junk[0] = next[0] = 0;
    memev_json_get(raw, "memories", memories, sizeof memories);
    memev_json_get(raw, "likely_junk", junk, sizeof junk);
    memev_json_get(raw, "next", next, sizeof next);
    snprintf(line, sizeof line, "%s memories, %s likely junk", memories[0] ? memories : "0", junk[0] ? junk : "0");
    add_line(out, n, line);
    if (next[0] && !has_path(next)) add_line(out, n, next);
  }
}

static void page_checked(const char *bin, char *out, size_t n) {
  char raw[262144];
  char *argv[4];
  struct list_ud u = {out, n, 0, "contract"};
  const char *arr;
  argv[0] = (char *)bin; argv[1] = "contract"; argv[2] = "list"; argv[3] = NULL;
  add_line(out, n, "# CONTRACTS");
  if (run_at(bin, argv, raw, sizeof raw) != 0) {
    add_line(out, n, "The contract list did not answer.");
    return;
  }
  arr = after_key(raw, "contracts");
  if (!arr || walk_objects(arr, take_line, &u) != 0) add_line(out, n, "The contract list did not answer.");
  else if (u.count == 0) add_line(out, n, "No contracts yet.");
}

static void page_settings(const char *bin, char *out, size_t n) {
  char raw[65536], a[64], b[64], c[64], line[240];
  char *argv[5];
  add_line(out, n, "# SETTINGS");
  argv[0] = (char *)bin; argv[1] = "plan"; argv[2] = "--json"; argv[3] = NULL;
  if (run_at(bin, argv, raw, sizeof raw) != 0) add_line(out, n, "The plan did not answer.");
  else {
    a[0] = b[0] = c[0] = 0;
    memev_json_get(raw, "plan", a, sizeof a);
    memev_json_get(raw, "computers_used", b, sizeof b);
    memev_json_get(raw, "computers_allowed", c, sizeof c);
    if (a[0] && !has_path(a)) {
      char conn[32];
      conn[0] = 0;
      memev_json_get(raw, "connectors_counted", conn, sizeof conn);
      if (c[0] && strcmp(c, "null") != 0)
        snprintf(line, sizeof line, "Plan: %s. Computers: %s of %s. Connectors: %s.", a, b[0] ? b : "0", c, conn[0] ? conn : "0");
      else
        snprintf(line, sizeof line, "Plan: %s. Computers: %s. Connectors: %s.", a, b[0] ? b : "0", conn[0] ? conn : "0");
      add_line(out, n, line);
    }
  }
  argv[0] = (char *)bin; argv[1] = "license"; argv[2] = "status"; argv[3] = "--json"; argv[4] = NULL;
  if (run_at(bin, argv, raw, sizeof raw) == 0) {
    char state[32], reason[120];
    a[0] = state[0] = reason[0] = 0;
    memev_json_get(raw, "tier_label", a, sizeof a);
    if (memev_json_get(raw, "state", state, sizeof state) != 0)
      memev_json_get(raw, "license.state", state, sizeof state);
    memev_json_get(raw, "reason", reason, sizeof reason);
    if (a[0] && !has_path(a)) {
      if (reason[0] && !has_path(reason)) snprintf(line, sizeof line, "Licence: %s · %s · %s", a, state[0] ? state : "none", reason);
      else snprintf(line, sizeof line, "Licence: %s · %s", a, state[0] ? state : "none");
      add_line(out, n, line);
    }
  }
  argv[0] = (char *)bin; argv[1] = "report"; argv[2] = "status"; argv[3] = "--json"; argv[4] = NULL;
  if (run_at(bin, argv, raw, sizeof raw) == 0) {
    a[0] = 0;
    memev_json_get(raw, "enabled", a, sizeof a);
    snprintf(line, sizeof line, "Anonymous report: %s", strcmp(a, "true") == 0 ? "on" : "off");
    add_line(out, n, line);
  }
  argv[0] = (char *)bin; argv[1] = "backup"; argv[2] = "status"; argv[3] = NULL;
  if (run_at(bin, argv, raw, sizeof raw) == 0) {
    if (strncmp(raw, "Backing up to", 13) == 0) add_line(out, n, "Backup: on");
    else add_line(out, n, "Backup: not set up");
  }
  add_line(out, n, "Theme: follows this computer");
}

int pages_text(const char *bin, const char *which, const char *query, char *out, size_t n) {
  if (!out || n == 0) return -1;
  out[0] = 0;
  if (!bin || !which) {
    snprintf(out, n, "%s", "Memev is not on this computer.");
    return -1;
  }
  if (strcmp(which, "projects") == 0) page_projects(bin, out, n);
  else if (strcmp(which, "bots") == 0) page_bots(bin, out, n);
  else if (strcmp(which, "memory") == 0) page_memory(bin, query, out, n);
  else if (strcmp(which, "checked") == 0) page_checked(bin, out, n);
  else if (strcmp(which, "settings") == 0) page_settings(bin, out, n);
  else snprintf(out, n, "%s", "That page is not available.");
  return 0;
}
