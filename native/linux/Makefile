# SPDX-License-Identifier: Apache-2.0
# gcc is what the Linux CI job runs. It treats format truncation as an error.
CC ?= gcc
CFLAGS ?= -std=c11 -Wall -Wextra -Werror -D_POSIX_C_SOURCE=200809L
GTK_CFLAGS := $(shell pkg-config --cflags gtk4 libadwaita-1)
GTK_LIBS := $(shell pkg-config --libs gtk4 libadwaita-1)

.PHONY: all test clean

all: memev-desktop

memev-desktop: src/main.c src/ui.c src/ui.h src/read.c src/read.h src/guide.c src/guide.h src/ops.c src/ops.h
	$(CC) $(CFLAGS) $(GTK_CFLAGS) -o $@ src/main.c src/ui.c src/read.c src/guide.c src/ops.c $(GTK_LIBS)

test-read: src/read.c src/read.h test/test-read.c
	$(CC) $(CFLAGS) -o $@ src/read.c test/test-read.c

test-guide: src/guide.c src/guide.h src/read.c src/read.h test/test-guide.c
	$(CC) $(CFLAGS) -o $@ src/guide.c src/read.c test/test-guide.c

proof: src/proof.c src/guide.c src/guide.h src/read.c src/read.h
	$(CC) $(CFLAGS) -o $@ src/proof.c src/guide.c src/read.c

test-ops: src/ops.c src/ops.h src/read.c src/read.h test/test-ops.c
	$(CC) $(CFLAGS) -o $@ src/ops.c src/read.c test/test-ops.c

test: test-read test-guide test-ops memev-desktop
	./test-read
	./test-guide
	./test-ops
	./test/self-check.sh

real-core: proof
	./test/real-core.sh

clean:
	rm -f memev-desktop test-read test-guide test-ops proof
