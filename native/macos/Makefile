# SPDX-License-Identifier: Apache-2.0
# Builds the macOS window with the Command Line Tools: swiftc and cc, no Xcode project. Floor macOS 12.
CC ?= cc
ARCH := $(shell uname -m)
SDK := $(shell xcrun --show-sdk-path)
TARGET := $(ARCH)-apple-macos12.0
CFLAGS ?= -std=c11 -Wall -Wextra -Werror -D_POSIX_C_SOURCE=200809L -D_DARWIN_C_SOURCE
READ := ../linux/src/read.c
GUIDE := ../linux/src/guide.c
SWIFT := src/main.swift src/Theme.swift src/Components.swift src/DetailPanel.swift src/Sheet.swift src/Screens.swift src/App.swift src/Guide.swift src/Pages.swift

.PHONY: all app test clean

all: memev-desktop

build/read.o: $(READ) ../linux/src/read.h
	mkdir -p build
	$(CC) $(CFLAGS) -target $(TARGET) -isysroot $(SDK) -c -o $@ $(READ)

build/guide.o: $(GUIDE) ../linux/src/guide.h ../linux/src/read.h
	mkdir -p build
	$(CC) $(CFLAGS) -target $(TARGET) -isysroot $(SDK) -c -o $@ $(GUIDE)

build/shim.o: src/shim.c src/shim.h ../linux/src/read.h ../linux/src/guide.h
	mkdir -p build
	$(CC) $(CFLAGS) -target $(TARGET) -isysroot $(SDK) -c -o $@ src/shim.c

memev-desktop: $(SWIFT) src/bridge.h build/read.o build/guide.o build/shim.o
	swiftc -swift-version 5 -O -target $(TARGET) -sdk $(SDK) -import-objc-header src/bridge.h \
	  -o $@ $(SWIFT) build/read.o build/guide.o build/shim.o -framework AppKit

# The same reader tests as the Linux window, built and run on macOS.
test-read: $(READ) ../linux/src/read.h ../linux/test/test-read.c
	$(CC) $(CFLAGS) -o $@ $(READ) ../linux/test/test-read.c

# The guide's rules, tested on macOS with the Linux tests; and the headless proof of the first launch against a real core.
test-guide: $(GUIDE) $(READ) ../linux/test/test-guide.c
	$(CC) $(CFLAGS) -o $@ $(GUIDE) $(READ) ../linux/test/test-guide.c

proof: ../linux/src/proof.c $(GUIDE) $(READ)
	$(CC) $(CFLAGS) -o $@ ../linux/src/proof.c $(GUIDE) $(READ)

# The design's fonts and their licences, each checked against its pinned SHA-256 first (fonts/SOURCES says where they come from).
FONTS := fonts/SpaceGrotesk-wght.ttf fonts/IBMPlexMono-Regular.ttf fonts/IBMPlexMono-Medium.ttf fonts/OFL-SpaceGrotesk.txt \
  fonts/OFL-IBMPlexMono.txt fonts/SOURCES fonts/SHA256SUMS

Memev.app: memev-desktop Info.plist $(FONTS)
	cd fonts && shasum -a 256 -c SHA256SUMS
	rm -rf $@
	mkdir -p $@/Contents/MacOS $@/Contents/Resources/fonts
	cp Info.plist $@/Contents/Info.plist
	cp memev-desktop $@/Contents/MacOS/memev-desktop
	cp $(FONTS) $@/Contents/Resources/fonts/

app: Memev.app

test: test-read test-guide memev-desktop Memev.app
	cd ../linux && ../macos/test-read
	./test-guide
	./test/self-check.sh
	./test/guard-check.sh

clean:
	rm -rf build memev-desktop test-read test-guide proof Memev.app
