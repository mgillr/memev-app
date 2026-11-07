# SPDX-License-Identifier: Apache-2.0
CC ?= gcc
CFLAGS ?= -std=c11 -Wall -Wextra -Werror -D_POSIX_C_SOURCE=200809L
GTK_CFLAGS := $(shell pkg-config --cflags gtk4 libadwaita-1)
GTK_LIBS := $(shell pkg-config --libs gtk4 libadwaita-1)

.PHONY: all test clean

all: memev-desktop

memev-desktop: src/main.c src/ui.c src/ui.h src/read.c src/read.h
	$(CC) $(CFLAGS) $(GTK_CFLAGS) -o $@ src/main.c src/ui.c src/read.c $(GTK_LIBS)

test-read: src/read.c src/read.h test/test-read.c
	$(CC) $(CFLAGS) -o $@ src/read.c test/test-read.c

test: test-read memev-desktop
	./test-read
	./test/self-check.sh

clean:
	rm -f memev-desktop test-read
