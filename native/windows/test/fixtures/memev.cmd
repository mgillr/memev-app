@echo off
rem SPDX-License-Identifier: Apache-2.0
rem A fixture core for the Windows window: it prints the same answers as native/linux/test/fixtures/memev.
if "%~1"=="status" goto show
if "%~1"=="doctor" goto show
if "%~1"=="card" goto show
echo unknown command 1>&2
exit /b 2
:show
type "%~dp0..\..\..\linux\test\fixtures\%~1.json"
