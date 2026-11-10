@echo off
rem SPDX-License-Identifier: Apache-2.0
rem A fixture core for the Windows window: it prints the same answers as native/linux/test/fixtures/memev for status, doctor
rem and card, and this folder's answers for the screens only the Windows window has so far (REV2-SCREENS.md section 5).
if "%~1"=="status" goto shared
if "%~1"=="doctor" goto shared
if "%~1"=="card" goto shared
if "%~1"=="onboarding" goto own
if "%~1"=="clients" goto clients
if "%~1"=="runner" goto runner
if "%~1"=="import" goto import
if "%~1"=="chat" goto chat
echo unknown command 1>&2
exit /b 2
:shared
type "%~dp0..\..\..\linux\test\fixtures\%~1.json"
exit /b 0
:own
type "%~dp0%~1.json"
exit /b 1
:clients
type "%~dp0clients.txt"
exit /b 0
:runner
if not "%~2"=="status" exit /b 2
type "%~dp0runner.json"
exit /b 0
:import
if "%~2"=="scan" goto scan
if not "%~2"=="list" exit /b 2
type "%~dp0import-list.txt"
exit /b 0
:scan
type "%~dp0import-scan.json"
exit /b 0
:chat
if not "%~2"=="status" exit /b 2
type "%~dp0chat.json"
exit /b 0
