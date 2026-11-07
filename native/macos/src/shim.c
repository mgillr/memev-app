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
