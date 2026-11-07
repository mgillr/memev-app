# SPDX-License-Identifier: Apache-2.0
# Builds the macOS window with the Command Line Tools: swiftc and cc, no Xcode project. Floor macOS 12.
CC ?= cc
ARCH := $(shell uname -m)
SDK := $(shell xcrun --show-sdk-path)
TARGET := $(ARCH)-apple-macos12.0
CFLAGS ?= -std=c11 -Wall -Wextra -Werror -D_POSIX_C_SOURCE=200809L -D_DARWIN_C_SOURCE
READ := ../linux/src/read.c
SWIFT := src/main.swift src/App.swift

.PHONY: all app test clean

all: memev-desktop

build/read.o: $(READ) ../linux/src/read.h
	mkdir -p build
	$(CC) $(CFLAGS) -target $(TARGET) -isysroot $(SDK) -c -o $@ $(READ)

build/shim.o: src/shim.c src/shim.h ../linux/src/read.h
	mkdir -p build
	$(CC) $(CFLAGS) -target $(TARGET) -isysroot $(SDK) -c -o $@ src/shim.c

memev-desktop: $(SWIFT) src/bridge.h build/read.o build/shim.o
	swiftc -swift-version 5 -O -target $(TARGET) -sdk $(SDK) -import-objc-header src/bridge.h \
	  -o $@ $(SWIFT) build/read.o build/shim.o -framework AppKit

# The same reader tests as the Linux window, built and run on macOS.
test-read: $(READ) ../linux/src/read.h ../linux/test/test-read.c
	$(CC) $(CFLAGS) -o $@ $(READ) ../linux/test/test-read.c

Memev.app: memev-desktop Info.plist
	rm -rf $@
	mkdir -p $@/Contents/MacOS
	cp Info.plist $@/Contents/Info.plist
	cp memev-desktop $@/Contents/MacOS/memev-desktop

app: Memev.app

test: test-read memev-desktop Memev.app
	cd ../linux && ../macos/test-read
	./test/self-check.sh

clean:
	rm -rf build memev-desktop test-read Memev.app
