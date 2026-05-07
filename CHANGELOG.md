# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [1.0.1] - 2026-05-07

### Added
- Window title now displays application version (e.g. `SCT Updater 1.0.1`)

### Fixed
- ZIP update now removes old version files before extracting new archive (pre-cleanup via old manifest)
- Added `WPF LCD Test.exe` to legacy cleanup list for `screen_checker` product after rename

---

## [1.0.0] - 2026-01-23

Initial release.

### Features
- **Suite update management**: checks server manifest and compares installed component versions against latest available
- **Self-update**: detects when the Updater itself needs updating and performs self-replacement via `update_stub.bat`
- **Device configs sync**: detects missing or outdated `config/device_configs/` folder by comparing local file count against server
- **Parallel downloads**: up to 20 simultaneous file downloads via `HttpClient`
- **Local state tracking**: installed versions persisted in `local_versions.json`
- **Credentials via `.env`**: Nextcloud connection loaded from environment file at startup (`NC_USER_QA`, `NC_PASSWORD_QA`, `NC_SERVER_URL_QA`)
- **Driver support**: drivers downloaded to local `drivers/` folder; installation triggered manually via `install.bat`
