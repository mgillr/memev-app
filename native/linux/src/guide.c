/* SPDX-License-Identifier: Apache-2.0 */
#include "guide.h"
#include "read.h"

#include <stdio.h>
#include <string.h>

void guide_plan_clear(GuidePlan *plan) {
  memset(plan, 0, sizeof *plan);
  plan->first_computer = -1;
  plan->port = 18799;
  snprintf(plan->phrase, sizeof plan->phrase, "The proof note is a blue window.");
}

static const char *SUFFIXES[] = {
  "found, not supported yet",
  "found, disconnected by you",
  "found, not connected",
  "connected (hooks + MCP + instructions)",
  "connected (MCP + instructions)",
  "connected (hooks + MCP)",
  "not found",
  NULL
};

int guide_parse_clients(const char *text, GuideApp *apps, int max) {
  if (!text || !apps || max < 1) return 0;
  int count = 0;
  const char *p = text;
  while (*p && count < max) {
    const char *nl = strchr(p, '\n');
    size_t len = nl ? (size_t)(nl - p) : strlen(p);
    char line[256];
    if (len >= sizeof line) len = sizeof line - 1;
    memcpy(line, p, len);
    line[len] = 0;
    p = nl ? nl + 1 : p + strlen(p);
    char *s = line;
    while (*s == ' ' || *s == '\t') s++;
    if (*s == 0) continue;
    /* Drop the marker the core prints before the name. It may be more than one byte. */
    while (*s && !((*s >= 'A' && *s <= 'Z') || (*s >= 'a' && *s <= 'z'))) s++;
    const char *matched = NULL;
    for (int i = 0; SUFFIXES[i]; i++) {
      size_t sl = strlen(SUFFIXES[i]);
      size_t have = strlen(s);
      if (have > sl && strcmp(s + have - sl, SUFFIXES[i]) == 0) {
        matched = SUFFIXES[i];
        break;
      }
    }
    if (!matched) continue;
    size_t have = strlen(s);
    size_t sl = strlen(matched);
    char name[80];
    size_t nlen = have - sl;
    while (nlen > 0 && s[nlen - 1] == ' ') nlen--;
    if (nlen == 0 || nlen >= sizeof name) continue;
    memcpy(name, s, nlen);
    name[nlen] = 0;
    GuideApp *app = &apps[count++];
    memset(app, 0, sizeof *app);
    memcpy(app->name, name, nlen + 1);
    snprintf(app->state, sizeof app->state, "%s", matched);
    app->selectable = strcmp(matched, "found, not connected") == 0;
  }
  return count;
}

/* The host part, after the scheme, must not start with '-', or git could read it as an option (ssh://-oProxyCommand=...). */
static int host_ok(const char *h) { return h[0] != 0 && h[0] != '-' && h[0] != '/'; }

static int url_ok(const char *s) {
  size_t n = s ? strlen(s) : 0;
  if (n < 8 || n > 400) return 0;
  for (size_t i = 0; i < n; i++) {
    unsigned char c = (unsigned char)s[i];
    if (c <= 32 || c == 127) return 0;
  }
  if (strncmp(s, "file:///", 8) == 0) return s[8] != 0;
  if (strncmp(s, "https://", 8) == 0) return host_ok(s + 8);
  if (strncmp(s, "ssh://", 6) == 0) return host_ok(s + 6);
  if (strncmp(s, "git://", 6) == 0) return host_ok(s + 6);
  if (strncmp(s, "git@", 4) == 0) {
    const char *colon = strchr(s + 4, ':');
    return colon && colon > s + 4 && host_ok(s + 4) && colon[1] != 0;
  }
  return 0;
}

/* A full path, written out with '.', '..' and repeated slashes resolved the way the core's path.resolve does, into out.
   0 when it is not a full path, holds a control character, or resolves to the root of the disk. */
static int absolute_dir(const char *s, char *out, size_t outn) {
  size_t n = s ? strlen(s) : 0;
  if (n < 2 || n > 400 || s[0] != '/' || outn < 2) return 0;
  for (size_t i = 0; i < n; i++) {
    unsigned char c = (unsigned char)s[i];
    if (c < 32 || c == 127) return 0;
  }
  size_t o = 0;
  out[0] = 0;
  for (const char *p = s; *p;) {
    while (*p == '/') p++;
    const char *e = p;
    while (*e && *e != '/') e++;
    size_t len = (size_t)(e - p);
    if (len == 0) break;
    if (len == 1 && p[0] == '.') { p = e; continue; }
    if (len == 2 && p[0] == '.' && p[1] == '.') {
      while (o > 0 && out[o - 1] != '/') o--;
      if (o > 0) o--;
      out[o] = 0;
      p = e;
      continue;
    }
    if (o + 1 + len + 1 > outn) return 0;
    out[o++] = '/';
    memcpy(out + o, p, len);
    o += len;
    out[o] = 0;
    p = e;
  }
  return o > 0;
}

static int code_ok(const char *s) {
  if (!s || s[0] == 0 || s[0] == '-') return 0;
  for (const char *p = s; *p; p++) {
    unsigned char c = (unsigned char)*p;
    if (c <= 32 || c == 127) return 0;
  }
  return 1;
}

static int push_cmd(GuideCmd *cmd, const char *bin) {
  memset(cmd, 0, sizeof *cmd);
  snprintf(cmd->arg[0], sizeof cmd->arg[0], "%s", bin);
  cmd->argc = 1;
  return 0;
}

static int add_arg(GuideCmd *cmd, const char *s) {
  if (cmd->argc >= GUIDE_ARG_MAX) return -1;
  size_t n = strlen(s);
  if (n == 0 || n >= sizeof cmd->arg[0]) return -1;
  memcpy(cmd->arg[cmd->argc], s, n + 1);
  cmd->argc++;
  return 0;
}

int guide_build(const GuidePlan *plan, GuideCmd *out, int max, char *why, size_t whyn) {
  if (why && whyn) why[0] = 0;
  if (!plan || !out || max < 1) return -1;
  if (plan->first_computer < 0) {
    if (why) snprintf(why, whyn, "Choose this computer or another one.");
    return -1;
  }
  int n = 0;
  if (plan->first_computer == 0) {
    if (!plan->confirm_join || !code_ok(plan->join_code)) {
      if (why) snprintf(why, whyn, "Joining waits until you enter a code and confirm. Nothing is sent before that.");
      return -1;
    }
    if (n >= max) return -1;
    push_cmd(&out[n], "memev");
    add_arg(&out[n], "join");
    add_arg(&out[n], plan->join_code);
    return 1;
  }
  char dir[sizeof plan->notes_dir];
  if (!absolute_dir(plan->notes_dir, dir, sizeof dir)) {
    if (why) snprintf(why, whyn, "The notes folder has to be a full path, and not the root of the disk.");
    return -1;
  }
  if (plan->phrase[0] == 0) return -1;
  char port[16];
  int p = plan->port > 0 && plan->port < 65536 ? plan->port : 18799;
  snprintf(port, sizeof port, "%d", p);
  if (n >= max) return -1;
  push_cmd(&out[n], "memev");
  add_arg(&out[n], "init");
  add_arg(&out[n], "--root");
  add_arg(&out[n], dir);
  add_arg(&out[n], "--local-only");
  add_arg(&out[n], "--no-connect");
  add_arg(&out[n], "--no-service");
  add_arg(&out[n], "--port");
  add_arg(&out[n], port);
  n++;
  if (plan->confirm_backup) {
    if (!url_ok(plan->backup_url)) {
      if (why) snprintf(why, whyn, "The remote address is not one this window will record.");
      return -1;
    }
    if (n >= max) return -1;
    push_cmd(&out[n], "memev");
    add_arg(&out[n], "backup");
    add_arg(&out[n], "set");
    add_arg(&out[n], plan->backup_url);
    n++;
  }
  if (n + 1 >= max) return -1;
  push_cmd(&out[n], "memev");
  add_arg(&out[n], "remember");
  add_arg(&out[n], plan->phrase);
  add_arg(&out[n], "--force");
  n++;
  push_cmd(&out[n], "memev");
  add_arg(&out[n], "recall");
  add_arg(&out[n], plan->phrase);
  add_arg(&out[n], "--json");
  n++;
  (void)plan->confirm_apps;
  (void)plan->selected_count;
  if (why && plan->selected_count > 0)
    snprintf(why, whyn, "No app is connected. The client list has names, not the id the connect command needs, and this window does not keep its own list.");
  return n;
}

static void scrub(const char *src, char *dst, size_t n) {
  dst[0] = 0;
  if (!src) return;
  size_t len = strlen(src);
  const char *start = len > 180 ? src + len - 180 : src;
  size_t o = 0;
  for (const char *p = start; *p && o + 1 < n; p++) {
    /* a line that names a token is cut there, so its value is never shown */
    if (strncmp(p, "token", 5) == 0) {
      while (p[1] && p[1] != '\n') p++;
      continue;
    }
    unsigned char c = (unsigned char)*p;
    dst[o++] = c < 32 && c != '\n' ? ' ' : (char)c;
  }
  dst[o] = 0;
}

int guide_apply(const GuidePlan *plan, const char *bin, char *detail, size_t detailn) {
  if (detail && detailn) detail[0] = 0;
  if (!bin || !bin[0]) return -1;
  GuideCmd cmds[GUIDE_CMD_MAX];
  char why[256];
  int n = guide_build(plan, cmds, GUIDE_CMD_MAX, why, sizeof why);
  if (n < 0) {
    if (detail) snprintf(detail, detailn, "%s", why[0] ? why : "The steps are not ready.");
    return -1;
  }
  char out[65536];
  for (int i = 0; i < n; i++) {
    char *argv[GUIDE_ARG_MAX + 1];
    for (int a = 0; a < cmds[i].argc; a++) argv[a] = cmds[i].arg[a];
    argv[0] = (char *)bin;
    argv[cmds[i].argc] = NULL;
    int code = 0;
    out[0] = 0; /* memev_capture leaves out unwritten when it cannot start the command */
    int timeout = strcmp(cmds[i].arg[1], "init") == 0 ? 20000 : 10000;
    if (memev_capture(bin, argv, out, sizeof out, timeout, &code) != 0 || code != 0) {
      char bit[200];
      scrub(out, bit, sizeof bit);
      if (detail) snprintf(detail, detailn, "The command %s stopped. %s", cmds[i].arg[1], bit);
      return -1;
    }
    if (strcmp(cmds[i].arg[1], "recall") == 0 && !strstr(out, plan->phrase)) {
      if (detail) snprintf(detail, detailn, "Recall did not return the note just saved.");
      return -1;
    }
  }
  if (detail && why[0]) snprintf(detail, detailn, "%s", why);
  return 0;
}
