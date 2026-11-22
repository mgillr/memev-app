/* SPDX-License-Identifier: Apache-2.0 */
#include "../src/ops.h"

#include <stdio.h>
#include <string.h>

static int failed;

static void expect(int ok, const char *what) {
  if (ok) return;
  fprintf(stderr, "not ok: %s\n", what);
  failed++;
}

int main(void) {
  OpsView v;
  char args[OPS_ARGS][OPS_ARG];
  char why[160];
  char page[4096];
  int argc = 0;
  const char *roster =
    "{\"operator\":\"claude-code\",\"live_minutes\":2,\"seats\":["
    "{\"seat\":\"claude-code\",\"state\":\"live\",\"app\":\"claude-code\",\"operator\":true,\"device_label\":\"secret-host\"},"
    "{\"seat\":\"cursor\",\"state\":\"stale\",\"app\":\"cursor\",\"operator\":false}"
    "]}";
  const char *none =
    "{\"operator\":null,\"lead_needed\":\"pick one\",\"seats\":["
    "{\"seat\":null,\"state\":\"never joined\",\"device_label\":\"secret-host\"},"
    "{\"seat\":\"cursor\",\"state\":\"stale\",\"operator\":false}"
    "]}";
  const char *msgs =
    "[{\"from\":\"you\",\"to\":\"claude-code\",\"type\":\"task\",\"body\":\"Open the notes project.\",\"seq\":1},"
    "{\"from\":\"claude-code\",\"to\":\"you\",\"type\":\"note\",\"body\":\"saved in /tmp/vault-marker\"}]";
  memset(&v, 0, sizeof v);
  expect(ops_read_roster(roster, &v) == 0, "roster parses");
  expect(strcmp(v.lead, "claude-code") == 0, "lead is the operator");
  expect(v.nseats == 2, "two seats");
  expect(v.seats[0].lead == 1, "first seat is the lead");
  expect(strstr(v.seats[0].name, "secret-host") == NULL, "hostname stays off the seat");
  ops_sentence(&v);
  expect(strcmp(v.sentence, "claude-code leads. Tell it what you want done.") == 0, "lead sentence");
  expect(ops_read_messages(msgs, &v) == 0, "messages parse");
  expect(v.nmsgs == 1, "a message that names a path is dropped");
  expect(strcmp(v.msgs[0].body, "Open the notes project.") == 0, "message body");
  memset(&v, 0, sizeof v);
  expect(ops_read_roster(none, &v) == 0, "roster with no lead parses");
  expect(v.lead[0] == 0, "no lead");
  expect(v.nseats == 1 && strcmp(v.seats[0].name, "cursor") == 0, "a seat with no name is skipped");
  ops_sentence(&v);
  expect(strcmp(v.sentence, "No lead is set. Pick one, then tell it what you want done.") == 0, "no-lead sentence");
  expect(ops_send_argv("", "hello", args, &argc, why, sizeof why) != 0, "send needs a lead");
  expect(ops_send_argv("claude-code", "", args, &argc, why, sizeof why) != 0, "send needs text");
  expect(ops_send_argv("claude-code", "go /tmp/x", args, &argc, why, sizeof why) != 0, "send refuses a path");
  expect(ops_send_argv("claude-code", "Open the notes project.", args, &argc, why, sizeof why) == 0, "send builds");
  expect(argc == 5 && strcmp(args[0], "bus") == 0 && strcmp(args[1], "send") == 0, "send command");
  expect(strcmp(args[2], "claude-code") == 0 && strcmp(args[3], "task") == 0, "send target and type");
  expect(strcmp(args[4], "Open the notes project.") == 0, "send text is one argument");
  expect(ops_read_roster(roster, &v) == 0, "roster again");
  expect(ops_lead_argv(&v, "not-a-seat", args, &argc, why, sizeof why) != 0, "lead must be on the roster");
  expect(ops_lead_argv(&v, "cursor", args, &argc, why, sizeof why) == 0, "lead argv");
  expect(argc == 3 && strcmp(args[0], "bus") == 0 && strcmp(args[1], "operator") == 0 && strcmp(args[2], "cursor") == 0, "operator command");

  {
    char bin[] = "/bin/false";
    expect(pages_text(bin, "settings", "", page, sizeof page) == 0, "settings still returns a page");
    expect(strstr(page, "did not answer") != NULL, "a failed command is one sentence");
  }
  if (failed) {
    fprintf(stderr, "%d ops test(s) failed\n", failed);
    return 1;
  }
  printf("ops tests passed\n");
  return 0;
}
