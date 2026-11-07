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
