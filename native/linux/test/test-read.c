/* SPDX-License-Identifier: Apache-2.0 */
#include "../src/read.h"

#include <arpa/inet.h>
#include <assert.h>
#include <netinet/in.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/wait.h>
#include <time.h>
#include <unistd.h>

static void pause_ms(int ms) {
  struct timespec ts;
  ts.tv_sec = ms / 1000;
  ts.tv_nsec = (long)(ms % 1000) * 1000000L;
  nanosleep(&ts, NULL);
}

static void expect(int cond, const char *what) {
  if (!cond) {
    fprintf(stderr, "fail: %s\n", what);
    exit(1);
  }
}

static void test_json(void) {
  char buf[64];
  const char *doc =
    "{\"version\":\"0.1.0\",\"index\":{\"notes\":3},\"skip\":\"{not}\",\"name\":\"b\\\"c\"}";
  expect(memev_json_get(doc, "version", buf, sizeof buf) == 0, "version");
  expect(strcmp(buf, "0.1.0") == 0, "version value");
  expect(memev_json_get(doc, "index.notes", buf, sizeof buf) == 0, "notes");
  expect(strcmp(buf, "3") == 0, "notes value");
  expect(memev_json_get(doc, "name", buf, sizeof buf) == 0, "escape");
  expect(strcmp(buf, "b\"c") == 0, "escape value");
  expect(memev_json_get(doc, "missing", buf, sizeof buf) != 0, "missing");
  expect(memev_json_get("{\"a\":null}", "a", buf, sizeof buf) != 0, "null");
  expect(memev_json_get("{\"ok\":true}", "ok", buf, sizeof buf) == 0, "bool");
  expect(strcmp(buf, "true") == 0, "bool value");
}

static void test_urls(void) {
  char host[64];
  char path[256];
  int port = 0;
  expect(memev_loopback_url("http://127.0.0.1:7787/healthz", host, sizeof host, &port, path, sizeof path) == 0, "v4");
  expect(strcmp(host, "127.0.0.1") == 0 && port == 7787 && strcmp(path, "/healthz") == 0, "v4 parts");
  expect(memev_loopback_url("http://localhost/healthz", host, sizeof host, &port, path, sizeof path) == 0, "localhost");
  expect(port == 80 && strcmp(host, "localhost") == 0, "localhost port");
  expect(memev_loopback_url("http://[::1]:7787/healthz", host, sizeof host, &port, path, sizeof path) == 0, "v6");
  expect(strcmp(host, "::1") == 0 && port == 7787, "v6 parts");
  const char *bad[] = {
    "https://127.0.0.1/healthz",
    "http://10.1.2.3/healthz",
    "http://192.0.2.1/healthz",
    "http://127.0.0.1:7787@192.0.2.1/",
    "http://localhost.example/healthz",
    "http://127.0.0.1:0/healthz",
    "http://127.0.0.1:99999/healthz",
    "file:///tmp/x",
    "",
  };
  for (size_t i = 0; i < sizeof bad / sizeof bad[0]; i++)
    expect(memev_loopback_url(bad[i], host, sizeof host, &port, path, sizeof path) != 0, bad[i]);

  struct timespec a, b;
  clock_gettime(CLOCK_MONOTONIC, &a);
  char body[32];
  expect(memev_http_get("http://192.0.2.1/healthz", body, sizeof body, 500) != 0, "reject public-looking test address");
  clock_gettime(CLOCK_MONOTONIC, &b);
  long ms = (b.tv_sec - a.tv_sec) * 1000L + (b.tv_nsec - a.tv_nsec) / 1000000L;
  expect(ms < 100, "reject did not wait on a connect");
}

static pid_t serve(int *port_out) {
  int fd = socket(AF_INET, SOCK_STREAM, 0);
  expect(fd >= 0, "socket");
  struct sockaddr_in addr;
  memset(&addr, 0, sizeof addr);
  addr.sin_family = AF_INET;
  addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
  addr.sin_port = 0;
  expect(bind(fd, (struct sockaddr *)&addr, sizeof addr) == 0, "bind");
  expect(listen(fd, 1) == 0, "listen");
  socklen_t sl = sizeof addr;
  expect(getsockname(fd, (struct sockaddr *)&addr, &sl) == 0, "name");
  *port_out = ntohs(addr.sin_port);
  pid_t pid = fork();
  expect(pid >= 0, "fork");
  if (pid == 0) {
    int c = accept(fd, NULL, NULL);
    char buf[1024];
    if (c >= 0) {
      recv(c, buf, sizeof buf, 0);
      const char *payload = "{\"ok\":true,\"version\":\"9.9.9\"}";
      char resp[256];
      int n = snprintf(resp, sizeof resp,
        "HTTP/1.0 200 OK\r\nContent-Length: %zu\r\nConnection: close\r\n\r\n%s",
        strlen(payload), payload);
      if (n > 0) send(c, resp, (size_t)n, 0);
      close(c);
    }
    close(fd);
    _exit(0);
  }
  close(fd);
  return pid;
}

static void test_http(void) {
  int port = 0;
  pid_t pid = serve(&port);
  char url[128];
  snprintf(url, sizeof url, "http://127.0.0.1:%d/healthz", port);
  char body[128];
  int rc = -1;
  for (int i = 0; i < 20 && rc != 0; i++) {
    rc = memev_http_get(url, body, sizeof body, 500);
    if (rc != 0) pause_ms(20);
  }
  expect(rc == 0, "http get");
  char version[32];
  expect(memev_json_get(body, "version", version, sizeof version) == 0, "http version");
  expect(strcmp(version, "9.9.9") == 0, "http version value");
  int st = 0;
  waitpid(pid, &st, 0);
}

static void write_text(const char *path, const char *body) {
  FILE *f = fopen(path, "w");
  expect(f != NULL, path);
  fputs(body, f);
  fclose(f);
}

static void make_fixture(char *dir) {
  strcpy(dir, "/tmp/memev-read-XXXXXX");
  expect(mkdtemp(dir) != NULL, "mkdtemp");
  char path[512];
  snprintf(path, sizeof path, "%s/memev", dir);
  write_text(path,
    "#!/bin/sh\n"
    "d=$(CDPATH= cd -- \"$(dirname \"$0\")\" && pwd)\n"
    "case \"$1\" in\n"
    "  status|doctor|card) cat \"$d/$1.json\" ;;\n"
    "  *) exit 2 ;;\n"
    "esac\n");
  chmod(path, 0755);
  snprintf(path, sizeof path, "%s/status.json", dir);
  write_text(path, "{\"version\":\"0.1.0\",\"vault\":\"/tmp/vault-marker\",\"index\":{\"notes\":3},\"token\":\"should-not-appear\"}\n");
  snprintf(path, sizeof path, "%s/doctor.json", dir);
  write_text(path, "[{\"id\":\"node\",\"title\":\"Node\",\"status\":\"ok\"},{\"id\":\"backup\",\"title\":\"Backup\",\"status\":\"warn\",\"token\":\"should-not-appear\"}]\n");
  snprintf(path, sizeof path, "%s/card.json", dir);
  write_text(path, "{\"project\":\"demo\",\"north_star\":\"Ship the window\",\"token\":\"should-not-appear\"}\n");
}

static void test_glance(void) {
  char dir[64];
  make_fixture(dir);
  char bin[512];
  snprintf(bin, sizeof bin, "%s/memev", dir);
  setenv("MEMEV_BIN", bin, 1);
  setenv("MEMEV_HEALTH_URL", "off", 1);
  MemevGlance g;
  memev_load_glance(&g);
  expect(g.have_binary, "binary");
  expect(g.daemon_up, "up");
  expect(strcmp(g.version, "0.1.0") == 0, "glance version");
  expect(g.notes == 3, "glance notes");
  expect(strcmp(g.project, "demo") == 0, "project");
  expect(strcmp(g.north_star, "Ship the window") == 0, "star");
  expect(g.checks_ok == 1 && g.checks_warn == 1 && g.check_count == 2, "checks");
  char sentence[512];
  memev_overview_sentence(&g, sentence, sizeof sentence);
  expect(strcmp(sentence, "Memev 0.1.0 is running for demo. 3 notes are in the index. 1 check to look at.") == 0, sentence);
  memev_health_sentence(&g, sentence, sizeof sentence);
  expect(strcmp(sentence, "1 check to look at.") == 0, sentence);
  expect(strstr(sentence, "should-not-appear") == NULL, "token hidden");
  expect(strstr(sentence, "vault-marker") == NULL, "path hidden");

  char path[512];
  snprintf(path, sizeof path, "%s/status.json", dir);
  write_text(path, "{\"version\":\"0.1.0\",\"index\":{\"notes\":5}}\n");
  memev_load_glance(&g);
  memev_overview_sentence(&g, sentence, sizeof sentence);
  expect(strstr(sentence, "5 notes are in the index.") != NULL, "reload sees the new count");

  snprintf(path, sizeof path, "%s/card.json", dir);
  write_text(path, "{\"project\":\"/tmp/vault-marker\"}\n");
  memev_load_glance(&g);
  expect(g.project[0] == 0, "path project is not shown");
}

static void test_health_only(void) {
  char dir[64];
  strcpy(dir, "/tmp/memev-down-XXXXXX");
  expect(mkdtemp(dir) != NULL, "down dir");
  char bin[512];
  snprintf(bin, sizeof bin, "%s/memev", dir);
  write_text(bin, "#!/bin/sh\nexit 1\n");
  chmod(bin, 0755);
  int port = 0;
  pid_t pid = serve(&port);
  char url[128];
  snprintf(url, sizeof url, "http://127.0.0.1:%d/healthz", port);
  setenv("MEMEV_BIN", bin, 1);
  setenv("MEMEV_HEALTH_URL", url, 1);
  MemevGlance g;
  int rc = -1;
  for (int i = 0; i < 20; i++) {
    memev_load_glance(&g);
    if (g.daemon_up && strcmp(g.version, "9.9.9") == 0) { rc = 0; break; }
    pause_ms(20);
  }
  expect(rc == 0, "healthz alone marks the core up");
  expect(g.have_binary, "down binary still counts as present");
  char sentence[256];
  memev_overview_sentence(&g, sentence, sizeof sentence);
  expect(strcmp(sentence, "Memev 9.9.9 is running.") == 0, sentence);
  int st = 0;
  waitpid(pid, &st, 0);
}

static void test_missing(void) {
  setenv("MEMEV_BIN", "/tmp/memev-does-not-exist", 1);
  setenv("MEMEV_HEALTH_URL", "off", 1);
  MemevGlance g;
  memev_load_glance(&g);
  expect(!g.have_binary && !g.daemon_up, "missing");
  char sentence[256];
  memev_overview_sentence(&g, sentence, sizeof sentence);
  expect(strcmp(sentence, "This app only reads a Free Core install. It does not install one.") == 0, sentence);
  memev_health_sentence(&g, sentence, sizeof sentence);
  expect(strcmp(sentence, "Memev is not on this computer yet.") == 0, sentence);
}

/* card --json starts a stopped Memev, so the glance reads it only when status said Memev runs: a stopped core stays stopped */
static void test_stopped_core_is_not_started(void) {
  char dir[64];
  strcpy(dir, "/tmp/memev-stopped-XXXXXX");
  expect(mkdtemp(dir) != NULL, "mkdtemp");
  char path[512];
  snprintf(path, sizeof path, "%s/memev", dir);
  write_text(path,
    "#!/bin/sh\n"
    "d=$(CDPATH= cd -- \"$(dirname \"$0\")\" && pwd)\n"
    "case \"$1\" in\n"
    "  status) echo '{\"running\":false}'; exit 1 ;;\n"
    "  card) : > \"$d/card-called\"; echo '{\"project\":\"demo\"}' ;;\n"
    "  *) exit 2 ;;\n"
    "esac\n");
  chmod(path, 0755);
  setenv("MEMEV_BIN", path, 1);
  setenv("MEMEV_HEALTH_URL", "off", 1);
  MemevGlance g;
  memev_load_glance(&g);
  expect(!g.daemon_up, "a stopped core reads as stopped");
  expect(g.project[0] == 0, "no project is read from a stopped core");
  snprintf(path, sizeof path, "%s/card-called", dir);
  expect(access(path, F_OK) != 0, "card was not run against a stopped core (it would start it)");
}

int main(void) {
  test_json();
  test_urls();
  test_http();
  test_glance();
  test_health_only();
  test_stopped_core_is_not_started();
  test_missing();
  printf("read tests passed\n");
  return 0;
}
