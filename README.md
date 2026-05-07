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

Create `.env` based on `.env.example` and fill in your Nextcloud credentials:

```
NC_SERVER_URL_QA=https://your-nextcloud.example.com
NC_USER_QA=your_username
NC_PASSWORD_QA=your_app_password
```

## Releasing

The release script is at `tools/release/release_manager.py`. Requires [uv](https://docs.astral.sh/uv/) with an active virtual environment that has the dependencies installed.

### Build specifics

The project targets **.NET Framework 4.8** and uses **Costura.Fody** to embed all dependencies into a single `Updater.exe`. Standard `dotnet publish` does not work for this — build must be done via Visual Studio.

In Visual Studio: set configuration to **Release** → **Build → Build Solution**. The output is in `SCT_Updater\bin\Release\app.publish\`.

### Release steps

**1. Configure credentials**

Create `tools/release/.env` based on `tools/release/.env.example` and fill in your Nextcloud credentials.

**2. Build in Visual Studio**

Set configuration to **Release** and build the solution. Verify `SCT_Updater\bin\Release\app.publish\Updater.exe` exists.

**3. Run the release manager**

From the repo root:

```bash
uv run --active .\tools\release\release_manager.py zip .\SCT_Updater\bin\Release\app.publish\ updater 1.0.1 --upload
```

Replace `1.0.1` with the actual version from `AssemblyInfo.cs`.

| Argument | Description |
|----------|-------------|
| `zip` | Package mode — single zip archive |
| `.\SCT_Updater\bin\Release\app.publish\` | Path to Costura build output |
| `updater` | Product identifier on Nextcloud |
| `1.0.1` | Version (must match `AssemblyVersion` in `AssemblyInfo.cs`) |
| `--upload` | Skip confirmation and upload immediately |

The script creates `release_artifacts/` locally (gitignored) with the zip and manifest, then uploads to `SCT/Updater/versions/updater/1.0.1/`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
