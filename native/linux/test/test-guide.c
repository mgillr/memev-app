/* SPDX-License-Identifier: Apache-2.0 */
#include "../src/guide.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static void expect(int cond, const char *what) {
  if (!cond) {
    fprintf(stderr, "fail: %s\n", what);
    exit(1);
  }
}

static int has_arg(const GuideCmd *cmd, const char *s) {
  for (int i = 0; i < cmd->argc; i++) if (strcmp(cmd->arg[i], s) == 0) return 1;
  return 0;
}

static int any_cmd(const GuideCmd *cmds, int n, const char *sub) {
  for (int i = 0; i < n; i++) if (cmds[i].argc > 1 && strcmp(cmds[i].arg[1], sub) == 0) return 1;
  return 0;
}

static void test_parse(void) {
  const char *text =
    "    Example One     not found\n"
    "  · Example Two     found, not connected\n"
    "  · Longer Example Name found, not supported yet\n"
    "  ✓ Example Three   connected (hooks + MCP)\n";
  GuideApp apps[8];
  int n = guide_parse_clients(text, apps, 8);
  expect(n == 4, "four rows");
  expect(strcmp(apps[0].name, "Example One") == 0 && !apps[0].selectable, "not found");
  expect(strcmp(apps[1].name, "Example Two") == 0 && apps[1].selectable, "selectable");
  expect(strcmp(apps[2].name, "Longer Example Name") == 0 && !apps[2].selectable, "unsupported");
  expect(strcmp(apps[3].name, "Example Three") == 0 && !apps[3].selectable, "already connected");
}

static void test_silent(void) {
  GuidePlan plan;
  guide_plan_clear(&plan);
  plan.first_computer = 1;
  snprintf(plan.notes_dir, sizeof plan.notes_dir, "/tmp/memev-notes-proof");
  snprintf(plan.backup_url, sizeof plan.backup_url, "file:///tmp/memev-backup-proof.git");
  plan.confirm_backup = false;
  plan.confirm_apps = true;
  plan.selected_count = 1;
  snprintf(plan.selected[0], sizeof plan.selected[0], "Example Two");
  GuideCmd cmds[GUIDE_CMD_MAX];
  char why[256];
  int n = guide_build(&plan, cmds, GUIDE_CMD_MAX, why, sizeof why);
  expect(n == 3, "init, remember, recall");
  expect(any_cmd(cmds, n, "init") && has_arg(&cmds[0], "--no-connect") && has_arg(&cmds[0], "--local-only"), "init stays local");
  expect(!any_cmd(cmds, n, "connect"), "no connect");
  expect(!any_cmd(cmds, n, "backup"), "url alone does not record a remote");
  expect(!any_cmd(cmds, n, "push"), "no push");
  expect(strstr(why, "does not keep its own list") != NULL, "the skip is explained");

  plan.confirm_backup = true;
  n = guide_build(&plan, cmds, GUIDE_CMD_MAX, why, sizeof why);
  expect(n == 4 && any_cmd(cmds, n, "backup"), "confirm records the remote");
  expect(!any_cmd(cmds, n, "push"), "recording a remote does not upload");

  plan.first_computer = 0;
  plan.confirm_join = false;
  snprintf(plan.join_code, sizeof plan.join_code, "abc");
  expect(guide_build(&plan, cmds, GUIDE_CMD_MAX, why, sizeof why) < 0, "join waits");
  plan.confirm_join = true;
  n = guide_build(&plan, cmds, GUIDE_CMD_MAX, why, sizeof why);
  expect(n == 1 && any_cmd(cmds, n, "join") && !any_cmd(cmds, n, "init"), "join is the only command");
}

int main(void) {
  test_parse();
  test_silent();
  printf("guide tests passed\n");
  return 0;
}
