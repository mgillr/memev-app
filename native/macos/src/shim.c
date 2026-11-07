/* SPDX-License-Identifier: Apache-2.0 */
#include "shim.h"

#include <stdlib.h>

MemevGlance *memev_shim_new(void) {
  MemevGlance *g = calloc(1, sizeof *g);
  if (g) memev_glance_clear(g);
  return g;
}

void memev_shim_free(MemevGlance *g) { free(g); }

void memev_shim_load(MemevGlance *g) { memev_load_glance(g); }

bool memev_shim_have_binary(const MemevGlance *g) { return g->have_binary; }
bool memev_shim_daemon_up(const MemevGlance *g) { return g->daemon_up; }
const char *memev_shim_north_star(const MemevGlance *g) { return g->north_star; }
int memev_shim_look(const MemevGlance *g) { return g->checks_fail + g->checks_warn; }
int memev_shim_check_shown(const MemevGlance *g) { return g->check_shown; }

const char *memev_shim_check_title(const MemevGlance *g, int i) {
  return i >= 0 && i < g->check_shown ? g->checks[i].title : "";
}

const char *memev_shim_check_status(const MemevGlance *g, int i) {
  return i >= 0 && i < g->check_shown ? g->checks[i].status : "";
}

void memev_shim_health(const MemevGlance *g, char *out, int n) {
  if (n > 0) memev_health_sentence(g, out, (size_t)n);
}

void memev_shim_overview(const MemevGlance *g, char *out, int n) {
  if (n > 0) memev_overview_sentence(g, out, (size_t)n);
}

#include <stdio.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

GuidePlan *memev_shim_plan_new(void) {
  GuidePlan *p = calloc(1, sizeof *p);
  if (p) guide_plan_clear(p);
  return p;
}

void memev_shim_plan_free(GuidePlan *p) { free(p); }

void memev_shim_plan_set(GuidePlan *p, int first_computer, const char *notes_dir, const char *join_code, bool confirm_join,
                         const char *backup_url, bool confirm_backup) {
  p->first_computer = first_computer;
  snprintf(p->notes_dir, sizeof p->notes_dir, "%s", notes_dir ? notes_dir : "");
  snprintf(p->join_code, sizeof p->join_code, "%s", join_code ? join_code : "");
  p->confirm_join = confirm_join;
  snprintf(p->backup_url, sizeof p->backup_url, "%s", backup_url ? backup_url : "");
  p->confirm_backup = confirm_backup;
  const char *port = getenv("MEMEV_GUIDE_PORT");
  if (port) p->port = atoi(port);
}

void memev_shim_plan_select(GuidePlan *p, const char *const *names, int count) {
  p->selected_count = 0;
  for (int i = 0; i < count && i < GUIDE_APPS_MAX; i++) {
    snprintf(p->selected[p->selected_count], sizeof p->selected[0], "%s", names[i]);
    p->selected_count++;
  }
  p->confirm_apps = p->selected_count > 0;
}

int memev_shim_plan_check(const GuidePlan *p, char *why, int whyn) {
  if (whyn <= 0) return -1;
  why[0] = 0;
  GuideCmd cmds[GUIDE_CMD_MAX];
  return guide_build(p, cmds, GUIDE_CMD_MAX, why, (size_t)whyn) < 0 ? -1 : 0;
}

int memev_shim_plan_apply(const GuidePlan *p, char *detail, int detailn) {
  if (detailn <= 0) return -1;
  detail[0] = 0;
  char bin[4096];
  if (memev_resolve_bin(bin, sizeof bin) != 0) {
    snprintf(detail, (size_t)detailn, "memev was not found");
    return -1;
  }
  return guide_apply(p, bin, detail, (size_t)detailn);
}

static GuideApp shim_apps[GUIDE_APPS_MAX];
static int shim_app_count = -1;

int memev_shim_clients_load(void) {
  if (shim_app_count >= 0) return shim_app_count;
  char bin[4096];
  if (memev_resolve_bin(bin, sizeof bin) != 0) return -1;
  static char out[8192];
  out[0] = 0;
  char *argv[] = {bin, "clients", NULL};
  int code = 0;
  if (memev_capture(bin, argv, out, sizeof out, 10000, &code) != 0 || code != 0) return -1;
  shim_app_count = guide_parse_clients(out, shim_apps, GUIDE_APPS_MAX);
  return shim_app_count;
}

const char *memev_shim_client_name(int i) { return i >= 0 && i < shim_app_count ? shim_apps[i].name : ""; }
const char *memev_shim_client_state(int i) { return i >= 0 && i < shim_app_count ? shim_apps[i].state : ""; }
bool memev_shim_client_selectable(int i) { return i >= 0 && i < shim_app_count && shim_apps[i].selectable; }

static int marker_path(char *out, size_t n) {
  const char *cfg = getenv("MEMEV_CONFIG_DIR");
  if (cfg && cfg[0]) return snprintf(out, n, "%s/desktop-first-launch-done", cfg) < (int)n ? 0 : -1;
  const char *home = getenv("HOME");
  if (!home || !home[0]) return -1;
  return snprintf(out, n, "%s/.config/memev/desktop-first-launch-done", home) < (int)n ? 0 : -1;
}

bool memev_shim_want_guide(void) {
  const char *flag = getenv("MEMEV_GUIDE");
  if (flag && strcmp(flag, "off") == 0) return false;
  if (flag && strcmp(flag, "1") == 0) return true;
  char path[1024];
  if (marker_path(path, sizeof path) != 0) return true;
  return access(path, F_OK) != 0;
}

void memev_shim_guide_done(void) {
  char path[1024];
  if (marker_path(path, sizeof path) != 0) return;
  char dir[1024];
  snprintf(dir, sizeof dir, "%s", path);
  char *slash = strrchr(dir, '/');
  if (!slash) return;
  *slash = 0;
  /* make each missing parent, owner-only */
  for (char *s = dir + 1; *s; s++) if (*s == '/') { *s = 0; mkdir(dir, 0700); *s = '/'; }
  mkdir(dir, 0700);
  FILE *f = fopen(path, "w");
  if (f) { fputs("ok\n", f); fclose(f); }
}
