/* SPDX-License-Identifier: Apache-2.0 */
#ifndef MEMEV_READ_H
#define MEMEV_READ_H

#include <stddef.h>
#include <stdbool.h>

#define MEMEV_CHECKS_MAX 64

typedef struct {
  char id[64];
  char title[128];
  char status[16];
} MemevCheck;

typedef struct {
  bool have_binary;
  bool daemon_up;
  char version[32];
  char project[80];
  char north_star[160];
  int notes; /* -1 when the core did not say */
  int checks_ok;
  int checks_warn;
  int checks_fail;
  int check_count;
  int check_shown;
  MemevCheck checks[MEMEV_CHECKS_MAX];
  char error[160];
} MemevGlance;

/* Host is loopback only. Returns 0 on acceptance. Never resolves a name. */
int memev_loopback_url(const char *url, char *host, size_t hostn, int *port, char *path, size_t pathn);

/* GET a loopback URL. Returns 0 and a body on HTTP 200. */
int memev_http_get(const char *url, char *body, size_t bodyn, int timeout_ms);

/* Copy a string, number or boolean at a dotted object key. 0 on a hit. */
int memev_json_get(const char *json, const char *key, char *out, size_t outn);

/* Run bin with argv (argv[0] is bin). 0 when the process ends in time. */
int memev_capture(const char *bin, char *const argv[], char *out, size_t outn, int timeout_ms, int *exit_code);
/* As memev_capture, with stderr kept in out and the tail kept when it is too long; -2 when stopped at the limit. */
int memev_capture_all(const char *bin, char *const argv[], char *out, size_t outn, int timeout_ms, int *exit_code);

/* MEMEV_BIN, or memev on PATH. 0 when an executable was copied to out. */
int memev_resolve_bin(char *out, size_t outn);

void memev_glance_clear(MemevGlance *g);

/* Read status, doctor and card from MEMEV_BIN or PATH, then loopback /healthz.
   MEMEV_HEALTH_URL=off skips the request. Writes nothing. */
int memev_load_glance(MemevGlance *g);

void memev_overview_sentence(const MemevGlance *g, char *out, size_t n);
void memev_health_sentence(const MemevGlance *g, char *out, size_t n);

#endif
