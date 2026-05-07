# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

WinForms desktop application (.NET Framework 4.8) for managing updates of Screen Check Tool (SCT) suite components. Downloads and installs module updates from a Nextcloud server.

## Build & Run

**Build (Visual Studio required):**
Visual Studio → set configuration to **Release** → Build Solution.
Output: `SCT_Updater\bin\Release\app.publish\Updater.exe`

> `dotnet build` / `dotnet publish` do not work — the project uses **Costura.Fody** to embed all dependencies into a single exe, which requires MSBuild with .NET Framework toolchain.

## Architecture

**Pattern:** WinForms, single-form application

**Entry Point:** `Program.cs` → `Form1`

**Key Classes:**

| Class | Purpose |
|-------|---------|
| `AppConfig` | Loads `.env`, exposes NC credentials and derived URLs |
| `NextcloudClient` | HTTP/WebDAV client for Nextcloud (download, PROPFIND, manifest fetch) |
| `UpdateService` | Core update logic: delta updates, ZIP updates, self-update, config sync |
| `LocalStateService` | Reads/writes `local_versions.json` |
| `Form1` | UI: update check, product list, install/reinstall buttons |
| `Logger` | Thin wrapper around file + console logging |

**Update flow:**
1. `CheckForUpdatesAsync` fetches `suite_manifest.json` from Nextcloud
2. Compares server versions against `local_versions.json`
3. Self-update (`UPDATER_ID = "suite_updater"`) takes priority — triggers before any other updates
4. ZIP mode: pre-cleanup via old manifest → download → extract → legacy file cleanup
5. Files mode: delta update — only changed files downloaded based on SHA256 hash

## Configuration

Runtime credentials in `SCT_Updater/.env` (gitignored):
```
NC_SERVER_URL_QA=https://your-nextcloud.example.com
NC_USER_QA=your_username
NC_PASSWORD_QA=your_app_password
```

## Version

Current version: **1.0.1** (set in `SCT_Updater\Properties\AssemblyInfo.cs` — `AssemblyVersion` / `AssemblyFileVersion`). See [CHANGELOG.md](CHANGELOG.md) for history.

## Releasing

See [README.md](README.md#releasing) for full release instructions.

Release command (from repo root):
```bash
uv run --active .\tools\release\release_manager.py zip .\SCT_Updater\bin\Release\app.publish\ suite_updater 1.0.1 --upload
```

## Communication Style & Code Standards

**Response Format:**
1. **CONCISENESS**: Minimal responses. No pleasantries.
2. **FORMAT**: Use Markdown. Code blocks must specify language.
3. **SCOPE**: Return only changed code blocks with minimal context. DO NOT return full files unless explicitly requested.
4. **EXPLANATIONS**: Skip explanations for trivial changes.

**Code Style:**
- Prefer elegant, concise, and clean code
- Every method must have XML documentation in English
- No explanatory comments — only final descriptive comments

**Tone:**
- Communication in Russian, technical documentation strictly in English
