/* SPDX-License-Identifier: Apache-2.0 */
/* Plain accessors over the shared reader (native/linux/src/read.c), so Swift never walks C arrays as tuples. */
#ifndef MEMEV_SHIM_H
#define MEMEV_SHIM_H

#include <stdbool.h>
#include "../../linux/src/read.h"

MemevGlance *memev_shim_new(void);
void memev_shim_free(MemevGlance *g);
/* Fills g from the core. Writes nothing and sends nothing beyond loopback. */
void memev_shim_load(MemevGlance *g);

bool memev_shim_have_binary(const MemevGlance *g);
bool memev_shim_daemon_up(const MemevGlance *g);
const char *memev_shim_north_star(const MemevGlance *g);
int memev_shim_look(const MemevGlance *g);
int memev_shim_check_shown(const MemevGlance *g);
const char *memev_shim_check_title(const MemevGlance *g, int i);
const char *memev_shim_check_status(const MemevGlance *g, int i);

/* The sentences, in buffers owned by g's caller: out must hold n bytes. */
void memev_shim_health(const MemevGlance *g, char *out, int n);
void memev_shim_overview(const MemevGlance *g, char *out, int n);

#endif

/* The guided first launch, through the shared guide (native/linux/src/guide.c). */
#include "../../linux/src/guide.h"

GuidePlan *memev_shim_plan_new(void);
void memev_shim_plan_free(GuidePlan *p);
void memev_shim_plan_set(GuidePlan *p, int first_computer, const char *notes_dir, const char *join_code, bool confirm_join,
                         const char *backup_url, bool confirm_backup);
void memev_shim_plan_select(GuidePlan *p, const char *const *names, int count);
/* 0 when the plan builds; why holds the reason or a note. */
int memev_shim_plan_check(const GuidePlan *p, char *why, int whyn);
/* Runs the plan with the resolved memev command. 0 on success; detail holds what the core said. */
int memev_shim_plan_apply(const GuidePlan *p, char *detail, int detailn);

/* The core's client list, read once. Returns the count, -1 when it could not be read. */
int memev_shim_clients_load(void);
const char *memev_shim_client_name(int i);
const char *memev_shim_client_state(int i);
bool memev_shim_client_selectable(int i);

/* The first-launch marker: MEMEV_GUIDE=1 or off forces it, else it shows until the marker file exists. */
bool memev_shim_want_guide(void);
void memev_shim_guide_done(void);

/* Runs the resolved memev with these arguments (no shell) and returns its exit code, or -1 when it could not run or timed out.
   out holds its standard output. */
int memev_shim_run(const char *const *args, int argc, char *out, int outn, int timeout_ms);
