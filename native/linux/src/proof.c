/* SPDX-License-Identifier: Apache-2.0 */
#include "guide.h"
#include "read.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int main(int argc, char **argv) {
  if (argc < 3) {
    fprintf(stderr, "usage: proof <memev> <notes-dir>\n");
    return 2;
  }
  const char *bin = argv[1];
  GuidePlan plan;
  guide_plan_clear(&plan);
  plan.first_computer = 1;
  snprintf(plan.notes_dir, sizeof plan.notes_dir, "%s", argv[2]);
  /* a remote address without the tick: the proof then checks that none was recorded */
  snprintf(plan.backup_url, sizeof plan.backup_url, "file:///tmp/memev-proof-not-recorded.git");
  plan.confirm_backup = false;
  plan.confirm_apps = false;
  if (getenv("MEMEV_GUIDE_PORT")) plan.port = atoi(getenv("MEMEV_GUIDE_PORT"));

  char clients[8192];
  char *cargv[] = {(char *)bin, "clients", NULL};
  int code = 0;
  if (memev_capture(bin, cargv, clients, sizeof clients, 10000, &code) != 0 || code != 0) {
    fprintf(stderr, "clients command failed\n");
    return 1;
  }
  GuideApp apps[GUIDE_APPS_MAX];
  int n = guide_parse_clients(clients, apps, GUIDE_APPS_MAX);
  int selectable = 0;
  for (int i = 0; i < n; i++) if (apps[i].selectable) selectable++;
  if (n < 1) {
    fprintf(stderr, "the client list did not parse\n");
    return 1;
  }
  printf("clients=%d selectable=%d\n", n, selectable);

  char detail[400];
  if (guide_apply(&plan, bin, detail, sizeof detail) != 0) {
    fprintf(stderr, "proof failed: %s\n", detail);
    char *stop[] = {(char *)bin, "stop", NULL};
    memev_capture(bin, stop, clients, sizeof clients, 5000, &code);
    return 1;
  }
  setenv("MEMEV_BIN", bin, 1);
  setenv("MEMEV_HEALTH_URL", "off", 1);
  MemevGlance glance;
  memev_load_glance(&glance);
  char sentence[512];
  memev_overview_sentence(&glance, sentence, sizeof sentence);
  printf("overview=%s\n", sentence);
  printf("notes=%d\n", glance.notes);
  char *stop[] = {(char *)bin, "stop", NULL};
  memev_capture(bin, stop, clients, sizeof clients, 5000, &code);
  if (!glance.daemon_up || !strstr(sentence, "is running") || strstr(sentence, "not running")) {
    fprintf(stderr, "overview did not come from a running core\n");
    return 1;
  }
  if (glance.notes < 1) {
    fprintf(stderr, "the core reported no notes\n");
    return 1;
  }
  printf("proof=ok\n");
  return 0;
}
