/* SPDX-License-Identifier: Apache-2.0 */
#ifndef MEMEV_OPS_H
#define MEMEV_OPS_H

#include <stddef.h>

#define OPS_SEATS 16
#define OPS_MSGS 40
#define OPS_NAME 64
#define OPS_LINE 200
#define OPS_ARG 512
#define OPS_ARGS 6
#define OPS_PROMPT "Tell your lead agent what you want done, or ask a question"
#define OPS_BLANK "Nothing has been said yet. Tell your lead agent what you want done."

typedef struct {
  char name[OPS_NAME];
  char state[32];
  char app[OPS_NAME];
  int lead;
} OpsSeat;

typedef struct {
  char from[OPS_NAME];
  char to[OPS_NAME];
  char body[OPS_LINE];
} OpsMsg;

typedef struct {
  char lead[OPS_NAME];
  char sentence[160];
  char note[160];
  OpsSeat seats[OPS_SEATS];
  int nseats;
  OpsMsg msgs[OPS_MSGS];
  int nmsgs;
} OpsView;

/* Roster object from `bus roster --json`. 0 when the seats array parsed. */
int ops_read_roster(const char *json, OpsView *v);
/* Message array from `bus since --json`. 0 when it parsed. */
int ops_read_messages(const char *json, OpsView *v);
void ops_sentence(OpsView *v);

/* Runs roster and since. The view is filled either way; 0 when both answered. */
int ops_load(const char *bin, OpsView *v);

/* argv words after the memev binary: bus send LEAD task TEXT. */
int ops_send_argv(const char *lead, const char *text, char out[][OPS_ARG], int *argc, char *why, size_t whyn);
/* argv words: bus operator SEAT. The seat has to be one this roster returned. */
int ops_lead_argv(const OpsView *v, const char *seat, char out[][OPS_ARG], int *argc, char *why, size_t whyn);

/* One page of plain lines for projects, bots, memory, checked or settings.
   A leading "# " marks a section. No paths and no raw JSON. */
int pages_text(const char *bin, const char *which, const char *query, char *out, size_t n);

#endif
