/* SPDX-License-Identifier: Apache-2.0 */
#include "../src/guide.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

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

/* Every command is one of those native/CLI-CONTRACT.md lists, so no connect, push, github or cloud command can be built. */
static int only_known(const GuideCmd *cmds, int n) {
  const char *known[] = {"init", "backup", "remember", "recall", "join"};
  for (int i = 0; i < n; i++) {
    int ok = 0;
    for (size_t k = 0; k < sizeof known / sizeof known[0]; k++) if (cmds[i].argc > 1 && strcmp(cmds[i].arg[1], known[k]) == 0) ok = 1;
    if (!ok) return 0;
  }
  return 1;
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
  expect(has_arg(&cmds[0], "--no-service"), "init installs no service");
  expect(only_known(cmds, n), "every command is one the contract lists");
  expect(!any_cmd(cmds, n, "backup"), "url alone does not record a remote");
  expect(strstr(why, "does not keep its own list") != NULL, "the skip is explained");

  plan.confirm_backup = true;
  n = guide_build(&plan, cmds, GUIDE_CMD_MAX, why, sizeof why);
  expect(n == 4 && any_cmd(cmds, n, "backup"), "confirm records the remote");
  expect(only_known(cmds, n), "recording a remote adds only backup set");
  expect(cmds[1].argc == 4 && strcmp(cmds[1].arg[2], "set") == 0 && strcmp(cmds[1].arg[3], plan.backup_url) == 0, "backup set takes the address as its own argument");

  plan.first_computer = 0;
  plan.confirm_join = false;
  snprintf(plan.join_code, sizeof plan.join_code, "abc");
  expect(guide_build(&plan, cmds, GUIDE_CMD_MAX, why, sizeof why) < 0, "join waits");
  plan.confirm_join = true;
  n = guide_build(&plan, cmds, GUIDE_CMD_MAX, why, sizeof why);
  expect(n == 1 && any_cmd(cmds, n, "join") && !any_cmd(cmds, n, "init"), "join is the only command");
  const char *bad_codes[] = {"-x", "a b", "a\tb", "a\nb", ""};
  for (size_t i = 0; i < sizeof bad_codes / sizeof bad_codes[0]; i++) {
    snprintf(plan.join_code, sizeof plan.join_code, "%s", bad_codes[i]);
    expect(guide_build(&plan, cmds, GUIDE_CMD_MAX, why, sizeof why) < 0, "a join code that could be an option or holds a space or control character is refused");
  }
}

/* A plan for this computer with the given notes folder and, when url is set and ticked, a remote. */
static int build_for(const char *dir, const char *url, GuideCmd *cmds) {
  GuidePlan plan;
  guide_plan_clear(&plan);
  plan.first_computer = 1;
  snprintf(plan.notes_dir, sizeof plan.notes_dir, "%s", dir);
  if (url) {
    snprintf(plan.backup_url, sizeof plan.backup_url, "%s", url);
    plan.confirm_backup = true;
  }
  char why[256];
  return guide_build(&plan, cmds, GUIDE_CMD_MAX, why, sizeof why);
}

static void test_paths(void) {
  GuideCmd cmds[GUIDE_CMD_MAX];
  const char *bad[] = {"notes", "./notes", "/", "//", "////", "/../", "/./.", "/tmp/..", "/a/../..", ""};
  for (size_t i = 0; i < sizeof bad / sizeof bad[0]; i++) expect(build_for(bad[i], NULL, cmds) < 0, bad[i]);
  expect(build_for("/a", NULL, cmds) == 3 && strcmp(cmds[0].arg[3], "/a") == 0, "a short full path is taken");
  expect(build_for("//tmp/./x/../notes/", NULL, cmds) == 3 && strcmp(cmds[0].arg[3], "/tmp/notes") == 0, "the folder is passed resolved");
}

static void test_urls(void) {
  GuideCmd cmds[GUIDE_CMD_MAX];
  const char *good[] = {"https://example.org/me/notes.git", "ssh://git@example.org/me/notes.git", "git://example.org/notes.git", "git@example.org:me/notes.git", "file:///tmp/notes.git"};
  for (size_t i = 0; i < sizeof good / sizeof good[0]; i++) expect(build_for("/tmp/n", good[i], cmds) == 4, good[i]);
  const char *bad[] = {"http://example.org/notes.git", "ftp://example.org/notes.git", "https://", "file:///", "git@:x", "git@host:",
    "ssh://-oProxyCommand=x/y", "https://-x/y", "git@-oProxyCommand=x:y", "-uhttps://example.org/x", "https://exa mple.org/x", "https://example.org/x\n"};
  for (size_t i = 0; i < sizeof bad / sizeof bad[0]; i++) expect(build_for("/tmp/n", bad[i], cmds) < 0, bad[i]);
}

/* guide_apply against a fake core: a recall that does not hold the phrase fails the check, one that does passes it. */
static void test_apply(void) {
  char dir[] = "/tmp/memev-guide-XXXXXX";
  expect(mkdtemp(dir) != NULL, "mkdtemp");
  char bin[256];
  snprintf(bin, sizeof bin, "%s/memev", dir);
  for (int round = 0; round < 2; round++) {
    FILE *f = fopen(bin, "w");
    expect(f != NULL, "fake core");
    fprintf(f, "#!/bin/sh\ncase \"$1\" in\n  recall) %s ;;\n  *) exit 0 ;;\nesac\n", round == 0 ? "echo '{\"hits\":[]}'" : "printf '%s\\n' \"$2\"");
    fclose(f);
    chmod(bin, 0755);
    GuidePlan plan;
    guide_plan_clear(&plan);
    plan.first_computer = 1;
    snprintf(plan.notes_dir, sizeof plan.notes_dir, "%s/notes", dir);
    char detail[400] = "";
    int rc = guide_apply(&plan, bin, detail, sizeof detail);
    if (round == 0) expect(rc != 0 && strstr(detail, "Recall did not return") != NULL, "a recall without the note fails the check");
    else expect(rc == 0, "a recall that returns the note passes the check");
  }
  unlink(bin);
  rmdir(dir);
}

int main(void) {
  test_parse();
  test_silent();
  test_paths();
  test_urls();
  test_apply();
  printf("guide tests passed\n");
  return 0;
}
