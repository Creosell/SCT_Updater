# SCT Updater

Windows application for managing updates of Screen Check Tool (SCT) suite components. Downloads and installs module updates from Nextcloud.

## Features

- **Suite update management** — checks server manifest, compares installed versions against latest available
- **Self-update** — detects when the Updater itself needs updating, performs self-replacement via `update_stub.bat`
- **ZIP update pre-cleanup** — before extracting a new ZIP, fetches the old version manifest and removes stale files
- **Device configs sync** — detects outdated `config/device_configs/` by comparing local file count against server
- **Delta updates** — for file-mode packages, only downloads changed files based on hash comparison
- **Parallel downloads** — up to 20 simultaneous downloads via `HttpClient`
- **Local state tracking** — installed versions persisted in `local_versions.json`
- **Credentials via `.env`** — Nextcloud connection loaded from `.env` at startup (`NC_USER_QA`, `NC_PASSWORD_QA`, `NC_SERVER_URL_QA`)
- **Driver support** — drivers downloaded to `drivers/` folder; installation triggered manually via `install.bat`

## Requirements

- Windows 10/11
- .NET Framework 4.8

## Configuration

Copy `.env.example` to `.env` and fill in credentials:

```
NC_SERVER_URL_QA=https://your-nextcloud.example.com
NC_USER_QA=your_username
NC_PASSWORD_QA=your_app_password
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
