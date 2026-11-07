/* SPDX-License-Identifier: Apache-2.0 */
#ifndef MEMEV_GUIDE_H
#define MEMEV_GUIDE_H

#include <stddef.h>
#include <stdbool.h>

#define GUIDE_APPS_MAX 32
#define GUIDE_CMD_MAX 6
#define GUIDE_ARG_MAX 12

typedef struct {
  char name[80];
  char state[80];
  bool selectable;
} GuideApp;

typedef struct {
  int first_computer; /* 1 this computer, 0 another, -1 not chosen */
  char notes_dir[512];
  int port;
  char join_code[128];
  bool confirm_join;
  char backup_url[512];
  bool confirm_backup;
  bool confirm_apps;
  int selected_count;
  char selected[GUIDE_APPS_MAX][80];
  char phrase[160];
} GuidePlan;

typedef struct {
  int argc;
  char arg[GUIDE_ARG_MAX][512];
} GuideCmd;

void guide_plan_clear(GuidePlan *plan);

/* Read the core's client lines. Names come only from that text. */
int guide_parse_clients(const char *text, GuideApp *apps, int max);

/* Commands that may run. Never includes connect, push, or a remote until asked. */
int guide_build(const GuidePlan *plan, GuideCmd *out, int max, char *why, size_t whyn);

/* Run the plan against bin. 0 when a first computer's recall contains the phrase. */
int guide_apply(const GuidePlan *plan, const char *bin, char *detail, size_t detailn);

#endif
