/* SPDX-License-Identifier: Apache-2.0 */
#include "read.h"

#include <arpa/inet.h>
#include <ctype.h>
#include <errno.h>
#include <fcntl.h>
#include <netinet/in.h>
#include <poll.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/wait.h>
#include <time.h>
#include <unistd.h>

static const char *skip_ws(const char *p) {
  while (*p == ' ' || *p == '\t' || *p == '\n' || *p == '\r') p++;
  return p;
}

static const char *skip_string(const char *p) {
  if (*p != '"') return NULL;
  for (p++; *p; p++) {
    if (*p == '\\') {
      if (!p[1]) return NULL;
      p++;
      continue;
    }
    if (*p == '"') return p + 1;
  }
  return NULL;
}

static const char *skip_value(const char *p) {
  p = skip_ws(p);
  if (*p == '"') return skip_string(p);
  if (*p == '{') {
    p++;
    p = skip_ws(p);
    if (*p == '}') return p + 1;
    for (;;) {
      p = skip_ws(p);
      if (*p != '"') return NULL;
      p = skip_string(p);
      if (!p) return NULL;
      p = skip_ws(p);
      if (*p != ':') return NULL;
      p = skip_value(p + 1);
      if (!p) return NULL;
      p = skip_ws(p);
      if (*p == ',') { p++; continue; }
      if (*p == '}') return p + 1;
      return NULL;
    }
  }
  if (*p == '[') {
    p++;
    p = skip_ws(p);
    if (*p == ']') return p + 1;
    for (;;) {
      p = skip_value(p);
      if (!p) return NULL;
      p = skip_ws(p);
      if (*p == ',') { p++; continue; }
      if (*p == ']') return p + 1;
      return NULL;
    }
  }
  if (*p == '-' || (*p >= '0' && *p <= '9')) {
    if (*p == '-') p++;
    while (*p >= '0' && *p <= '9') p++;
    if (*p == '.') {
      p++;
      while (*p >= '0' && *p <= '9') p++;
    }
    if (*p == 'e' || *p == 'E') {
      p++;
      if (*p == '+' || *p == '-') p++;
      while (*p >= '0' && *p <= '9') p++;
    }
    return p;
  }
  if (strncmp(p, "true", 4) == 0) return p + 4;
  if (strncmp(p, "false", 5) == 0) return p + 5;
  if (strncmp(p, "null", 4) == 0) return p + 4;
  return NULL;
}

static int object_get(const char *obj, const char *key, const char **val, const char **vend) {
  obj = skip_ws(obj);
  if (*obj != '{') return -1;
  obj++;
  size_t klen = strlen(key);
  for (;;) {
    obj = skip_ws(obj);
    if (*obj == '}') return -1;
    if (*obj != '"') return -1;
    const char *ks = obj + 1;
    const char *after = skip_string(obj);
    if (!after) return -1;
    size_t got = (size_t)((after - 1) - ks);
    obj = skip_ws(after);
    if (*obj != ':') return -1;
    obj = skip_ws(obj + 1);
    const char *vs = obj;
    const char *ve = skip_value(vs);
    if (!ve) return -1;
    if (got == klen && memcmp(ks, key, klen) == 0) {
      *val = vs;
      *vend = ve;
      return 0;
    }
    obj = skip_ws(ve);
    if (*obj == ',') { obj++; continue; }
    if (*obj == '}') return -1;
    return -1;
  }
}

static int decode_scalar(const char *v, const char *vend, char *out, size_t outn) {
  v = skip_ws(v);
  if (outn == 0) return -1;
  if (*v == '"') {
    size_t o = 0;
    const char *endq = vend - 1;
    for (const char *p = v + 1; p < endq;) {
      unsigned char c;
      if (*p == '\\') {
        p++;
        if (p >= endq) return -1;
        char e = *p++;
        switch (e) {
          case 'n': c = '\n'; break;
          case 't': c = '\t'; break;
          case 'r': c = '\r'; break;
          case '"':
          case '\\':
          case '/': c = (unsigned char)e; break;
          case 'u':
            c = '?';
            for (int i = 0; i < 4 && p < endq; i++) p++;
            break;
          default: c = (unsigned char)e; break;
        }
      } else {
        c = (unsigned char)*p++;
      }
      if (c == '\n' || c == '\r') c = ' ';
      if (o + 1 >= outn) break;
      out[o++] = (char)c;
    }
    out[o] = 0;
    return 0;
  }
  if (strncmp(v, "null", 4) == 0) return -1;
  size_t n = (size_t)(vend - v);
  while (n > 0 && (v[n - 1] == ' ' || v[n - 1] == '\n' || v[n - 1] == '\r' || v[n - 1] == '\t')) n--;
  if (n + 1 > outn) n = outn - 1;
  memcpy(out, v, n);
  out[n] = 0;
  return n == 0 ? -1 : 0;
}

int memev_json_get(const char *json, const char *key, char *out, size_t outn) {
  if (!json || !key || !out || outn == 0) return -1;
  const char *cur = json;
  const char *k = key;
  for (;;) {
    const char *dot = strchr(k, '.');
    size_t len = dot ? (size_t)(dot - k) : strlen(k);
    if (len == 0 || len > 80) return -1;
    char part[81];
    memcpy(part, k, len);
    part[len] = 0;
    const char *val = NULL;
    const char *vend = NULL;
    if (object_get(cur, part, &val, &vend) != 0) return -1;
    if (!dot) return decode_scalar(val, vend, out, outn);
    cur = val;
    k = dot + 1;
  }
}

int memev_loopback_url(const char *url, char *host, size_t hostn, int *port, char *path, size_t pathn) {
  if (!url || !host || !port || !path || hostn < 4 || pathn < 2) return -1;
  if (strncmp(url, "http://", 7) != 0) return -1;
  const char *p = url + 7;
  if (*p == '[') {
    if (strncmp(p, "[::1]", 5) != 0) return -1;
    snprintf(host, hostn, "::1");
    p += 5;
  } else {
    const char *slash = strchr(p, '/');
    const char *colon = strchr(p, ':');
    const char *hend = p + strlen(p);
    if (slash && slash < hend) hend = slash;
    if (colon && colon < hend) hend = colon;
    size_t hlen = (size_t)(hend - p);
    if (hlen == 0 || hlen >= hostn) return -1;
    memcpy(host, p, hlen);
    host[hlen] = 0;
    if (strcmp(host, "127.0.0.1") != 0 && strcmp(host, "localhost") != 0) return -1;
    p = hend;
  }
  if (*p == ':') {
    char *end = NULL;
    long n = strtol(p + 1, &end, 10);
    if (end == p + 1 || n < 1 || n > 65535) return -1;
    *port = (int)n;
    p = end;
  } else {
    *port = 80;
  }
  if (*p == 0) {
    snprintf(path, pathn, "/");
    return 0;
  }
  if (*p != '/' || strlen(p) >= pathn) return -1;
  snprintf(path, pathn, "%s", p);
  char *hash = strchr(path, '#');
  if (hash) *hash = 0;
  return 0;
}

static int ms_left(const struct timespec *start, int timeout_ms) {
  struct timespec now;
  clock_gettime(CLOCK_MONOTONIC, &now);
  long ms = (now.tv_sec - start->tv_sec) * 1000L + (now.tv_nsec - start->tv_nsec) / 1000000L;
  if (ms >= timeout_ms) return 0;
  return timeout_ms - (int)ms;
}

int memev_http_get(const char *url, char *body, size_t bodyn, int timeout_ms) {
  char host[64];
  char path[1024];
  int port = 0;
  if (memev_loopback_url(url, host, sizeof host, &port, path, sizeof path) != 0) return -1;
  if (bodyn < 2 || timeout_ms < 1) return -1;

  int fd;
  if (strcmp(host, "::1") == 0) {
    fd = socket(AF_INET6, SOCK_STREAM, 0);
    if (fd < 0) return -1;
    struct sockaddr_in6 addr;
    memset(&addr, 0, sizeof addr);
    addr.sin6_family = AF_INET6;
    addr.sin6_port = htons((uint16_t)port);
    if (inet_pton(AF_INET6, "::1", &addr.sin6_addr) != 1) { close(fd); return -1; }
    int flags = fcntl(fd, F_GETFL, 0);
    fcntl(fd, F_SETFL, flags | O_NONBLOCK);
    if (connect(fd, (struct sockaddr *)&addr, sizeof addr) < 0 && errno != EINPROGRESS) {
      close(fd);
      return -1;
    }
  } else {
    fd = socket(AF_INET, SOCK_STREAM, 0);
    if (fd < 0) return -1;
    struct sockaddr_in addr;
    memset(&addr, 0, sizeof addr);
    addr.sin_family = AF_INET;
    addr.sin_port = htons((uint16_t)port);
    if (inet_pton(AF_INET, "127.0.0.1", &addr.sin_addr) != 1) { close(fd); return -1; }
    int flags = fcntl(fd, F_GETFL, 0);
    fcntl(fd, F_SETFL, flags | O_NONBLOCK);
    if (connect(fd, (struct sockaddr *)&addr, sizeof addr) < 0 && errno != EINPROGRESS) {
      close(fd);
      return -1;
    }
  }

  struct timespec start;
  clock_gettime(CLOCK_MONOTONIC, &start);
  struct pollfd pfd = {.fd = fd, .events = POLLOUT};
  if (poll(&pfd, 1, ms_left(&start, timeout_ms)) <= 0) { close(fd); return -1; }
  int soerr = 0;
  socklen_t sl = sizeof soerr;
  getsockopt(fd, SOL_SOCKET, SO_ERROR, &soerr, &sl);
  if (soerr != 0) { close(fd); return -1; }

  char req[1400];
  int reqn = snprintf(req, sizeof req,
    "GET %s HTTP/1.0\r\nHost: %s\r\nConnection: close\r\n\r\n",
    path, strcmp(host, "::1") == 0 ? "[::1]" : "127.0.0.1");
  if (reqn < 0 || (size_t)reqn >= sizeof req) { close(fd); return -1; }
  size_t sent = 0;
  while (sent < (size_t)reqn) {
    pfd.events = POLLOUT;
    if (poll(&pfd, 1, ms_left(&start, timeout_ms)) <= 0) { close(fd); return -1; }
    ssize_t n = send(fd, req + sent, (size_t)reqn - sent, 0);
    if (n < 0) {
      if (errno == EAGAIN || errno == EWOULDBLOCK) continue;
      close(fd);
      return -1;
    }
    sent += (size_t)n;
  }

  char raw[16384];
  size_t got = 0;
  for (;;) {
    int left = ms_left(&start, timeout_ms);
    if (left <= 0) { close(fd); return -1; }
    pfd.events = POLLIN;
    int pr = poll(&pfd, 1, left);
    if (pr < 0) { if (errno == EINTR) continue; close(fd); return -1; }
    if (pr == 0) { close(fd); return -1; }
    if (got + 1 >= sizeof raw) { close(fd); return -1; }
    ssize_t n = recv(fd, raw + got, sizeof raw - 1 - got, 0);
    if (n < 0) {
      if (errno == EAGAIN || errno == EWOULDBLOCK) continue;
      close(fd);
      return -1;
    }
    if (n == 0) break;
    got += (size_t)n;
  }
  close(fd);
  raw[got] = 0;
  char *split = strstr(raw, "\r\n\r\n");
  if (!split) return -1;
  int code = 0;
  if (sscanf(raw, "HTTP/%*s %d", &code) != 1 || code != 200) return -1;
  const char *payload = split + 4;
  snprintf(body, bodyn, "%s", payload);
  return 0;
}

static int capture(const char *bin, char *const argv[], char *out, size_t outn, int timeout_ms, int *exit_code) {
  int fds[2];
  if (pipe(fds) != 0) return -1;
  pid_t pid = fork();
  if (pid < 0) {
    close(fds[0]);
    close(fds[1]);
    return -1;
  }
  if (pid == 0) {
    dup2(fds[1], STDOUT_FILENO);
    int devnull = open("/dev/null", O_WRONLY);
    if (devnull >= 0) dup2(devnull, STDERR_FILENO);
    close(fds[0]);
    close(fds[1]);
    execv(bin, argv);
    _exit(127);
  }
  close(fds[1]);
  int flags = fcntl(fds[0], F_GETFL, 0);
  fcntl(fds[0], F_SETFL, flags | O_NONBLOCK);
  struct timespec start;
  clock_gettime(CLOCK_MONOTONIC, &start);
  size_t used = 0;
  int timed = 0;
  for (;;) {
    int left = ms_left(&start, timeout_ms);
    if (left <= 0) { timed = 1; break; }
    struct pollfd pfd = {.fd = fds[0], .events = POLLIN};
    int pr = poll(&pfd, 1, left);
    if (pr < 0) { if (errno == EINTR) continue; break; }
    if (pr == 0) { timed = 1; break; }
    char buf[4096];
    ssize_t n = read(fds[0], buf, sizeof buf);
    if (n == 0) break;
    if (n < 0) {
      if (errno == EAGAIN || errno == EWOULDBLOCK) continue;
      break;
    }
    size_t room = outn > 0 && used + 1 < outn ? outn - 1 - used : 0;
    size_t take = (size_t)n < room ? (size_t)n : room;
    if (take > 0) memcpy(out + used, buf, take);
    used += take;
  }
  if (timed) kill(pid, SIGKILL);
  close(fds[0]);
  int st = 0;
  waitpid(pid, &st, 0);
  if (outn > 0) out[used < outn ? used : outn - 1] = 0;
  if (exit_code) *exit_code = WIFEXITED(st) ? WEXITSTATUS(st) : 128;
  return timed ? -1 : 0;
}

static int executable(const char *path) {
  return path && access(path, X_OK) == 0;
}

static int resolve_bin(char *out, size_t outn) {
  const char *env = getenv("MEMEV_BIN");
  if (env && env[0]) {
    if (!executable(env)) return -1;
    snprintf(out, outn, "%s", env);
    return 0;
  }
  const char *path = getenv("PATH");
  if (!path) return -1;
  char copy[4096];
  snprintf(copy, sizeof copy, "%s", path);
  for (char *p = copy; *p;) {
    char *colon = strchr(p, ':');
    if (colon) *colon = 0;
    if (*p) {
      snprintf(out, outn, "%s/memev", p);
      if (executable(out)) return 0;
    }
    if (!colon) break;
    p = colon + 1;
  }
  return -1;
}

static int plain_version(const char *s) {
  if (!s || !s[0] || strlen(s) > 31) return 0;
  for (const char *p = s; *p; p++) {
    if ((*p < '0' || *p > '9') && *p != '.') return 0;
  }
  return 1;
}

static int plain_label(const char *s, size_t cap) {
  size_t n = s ? strlen(s) : 0;
  if (n == 0 || n > cap) return 0;
  for (const char *p = s; *p; p++) {
    unsigned char c = (unsigned char)*p;
    if (c < 32 || c == '/' || c == '\\') return 0;
  }
  return 1;
}

static void copy_status_word(char *dst, size_t n, const char *src) {
  size_t i = 0;
  if (!src) src = "";
  for (; src[i] && i + 1 < n && i < 12; i++) {
    char c = src[i];
    if (c < 'a' || c > 'z') break;
    dst[i] = c;
  }
  if (i == 0) snprintf(dst, n, "unknown");
  else dst[i] = 0;
}

static void tally(MemevGlance *g, const char *status) {
  if (strcmp(status, "ok") == 0) g->checks_ok++;
  else if (strcmp(status, "fail") == 0) g->checks_fail++;
  else g->checks_warn++;
}

static const char *array_start(const char *json) {
  const char *p = skip_ws(json);
  if (*p == '[') return p;
  if (*p == '{') {
    const char *val = NULL;
    const char *vend = NULL;
    if (object_get(p, "checks", &val, &vend) == 0 && skip_ws(val)[0] == '[') return skip_ws(val);
  }
  return NULL;
}

static void load_checks(MemevGlance *g, const char *json) {
  const char *p = array_start(json);
  if (!p) return;
  p++;
  for (;;) {
    p = skip_ws(p);
    if (*p == ']') return;
    if (*p != '{') return;
    const char *end = skip_value(p);
    if (!end) return;
    size_t len = (size_t)(end - p);
    char obj[2048];
    if (len < sizeof obj) {
      memcpy(obj, p, len);
      obj[len] = 0;
      char id[64] = "";
      char title[128] = "";
      char status[32] = "";
      memev_json_get(obj, "id", id, sizeof id);
      memev_json_get(obj, "title", title, sizeof title);
      memev_json_get(obj, "status", status, sizeof status);
      char word[16];
      copy_status_word(word, sizeof word, status);
      tally(g, word);
      if (g->check_shown < MEMEV_CHECKS_MAX) {
        MemevCheck *c = &g->checks[g->check_shown++];
        snprintf(c->id, sizeof c->id, "%s", id);
        if (title[0]) snprintf(c->title, sizeof c->title, "%s", title);
        else snprintf(c->title, sizeof c->title, "%s", id[0] ? id : "Check");
        snprintf(c->status, sizeof c->status, "%s", word);
      }
      g->check_count++;
    }
    p = skip_ws(end);
    if (*p == ',') { p++; continue; }
    if (*p == ']') return;
    return;
  }
}

static int json_value(const char *s) {
  s = skip_ws(s);
  return *s == '{' || *s == '[';
}

void memev_glance_clear(MemevGlance *g) {
  memset(g, 0, sizeof *g);
  g->notes = -1;
}

int memev_load_glance(MemevGlance *g) {
  memev_glance_clear(g);
  char bin[4096];
  if (resolve_bin(bin, sizeof bin) != 0) {
    snprintf(g->error, sizeof g->error, "memev was not found");
    return 0;
  }
  g->have_binary = true;
  char out[262144];
  int code = 0;
  char *status_argv[] = {bin, "status", "--json", NULL};
  if (capture(bin, status_argv, out, sizeof out, 3000, &code) == 0 && code == 0 && json_value(out)) {
    g->daemon_up = true;
    char tmp[64];
    if (memev_json_get(out, "version", tmp, sizeof tmp) == 0 && plain_version(tmp))
      snprintf(g->version, sizeof g->version, "%s", tmp);
    if (memev_json_get(out, "index.notes", tmp, sizeof tmp) == 0) {
      char *end = NULL;
      long n = strtol(tmp, &end, 10);
      if (end != tmp && n >= 0 && n < 1000000000L) g->notes = (int)n;
    }
  }
  char *doctor_argv[] = {bin, "doctor", "--json", NULL};
  if (capture(bin, doctor_argv, out, sizeof out, 3000, &code) == 0 && code == 0 && json_value(out))
    load_checks(g, out);
  char *card_argv[] = {bin, "card", "--json", NULL};
  if (capture(bin, card_argv, out, sizeof out, 3000, &code) == 0 && code == 0 && json_value(out)) {
    char tmp[200];
    if (memev_json_get(out, "project", tmp, sizeof tmp) == 0 && plain_label(tmp, 79))
      snprintf(g->project, sizeof g->project, "%s", tmp);
    if (memev_json_get(out, "north_star", tmp, sizeof tmp) == 0 && plain_label(tmp, 159))
      snprintf(g->north_star, sizeof g->north_star, "%s", tmp);
  }

  const char *health = getenv("MEMEV_HEALTH_URL");
  if (!health) health = "http://127.0.0.1:7787/healthz";
  if (strcmp(health, "off") != 0) {
    char body[8192];
    if (memev_http_get(health, body, sizeof body, 500) == 0) {
      char ok[8];
      if (memev_json_get(body, "ok", ok, sizeof ok) == 0 && strcmp(ok, "true") == 0) {
        g->daemon_up = true;
        char tmp[64];
        if (!g->version[0] && memev_json_get(body, "version", tmp, sizeof tmp) == 0 && plain_version(tmp))
          snprintf(g->version, sizeof g->version, "%s", tmp);
      }
    }
  }
  return 0;
}

static void append_text(char *out, size_t n, const char *s) {
  size_t len = strlen(out);
  if (len + 1 >= n) return;
  snprintf(out + len, n - len, "%s", s);
}

void memev_health_sentence(const MemevGlance *g, char *out, size_t n) {
  if (!g->have_binary) {
    snprintf(out, n, "Memev is not on this computer yet.");
    return;
  }
  if (!g->daemon_up) {
    snprintf(out, n, "Memev is not running.");
    return;
  }
  int look = g->checks_fail + g->checks_warn;
  if (g->check_count == 0) {
    snprintf(out, n, "Memev is running. No checks were returned.");
    return;
  }
  if (look == 0) snprintf(out, n, "All %d checks are fine.", g->check_count);
  else if (look == 1) snprintf(out, n, "1 check to look at.");
  else snprintf(out, n, "%d checks to look at.", look);
}

void memev_overview_sentence(const MemevGlance *g, char *out, size_t n) {
  if (!g->have_binary) {
    snprintf(out, n, "This app only reads a Free Core install. It does not install one.");
    return;
  }
  if (!g->daemon_up) {
    snprintf(out, n, "Memev is not running. Start it on this computer, then press Refresh.");
    return;
  }
  if (g->version[0]) snprintf(out, n, "Memev %s is running", g->version);
  else snprintf(out, n, "Memev is running");
  if (g->project[0]) {
    append_text(out, n, " for ");
    append_text(out, n, g->project);
  }
  append_text(out, n, ".");
  if (g->notes == 0) append_text(out, n, " No notes are stored yet.");
  else if (g->notes == 1) append_text(out, n, " 1 note is in the index.");
  else if (g->notes > 1) {
    char bit[64];
    snprintf(bit, sizeof bit, " %d notes are in the index.", g->notes);
    append_text(out, n, bit);
  }
  int look = g->checks_fail + g->checks_warn;
  if (look == 1) append_text(out, n, " 1 check to look at.");
  else if (look > 1) {
    char bit[64];
    snprintf(bit, sizeof bit, " %d checks to look at.", look);
    append_text(out, n, bit);
  }
}
