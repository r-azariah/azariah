# AZARIAH

A personal portable workspace that lives on a USB drive: a custom file explorer, a Roblox
workspace, a portable Setup Kit, and later an encrypted Vault, a password manager and a
JARVIS-style assistant. Built with C# / .NET 10 and Avalonia.

Plug the drive into one of your own PCs and AZARIAH opens by itself. On any other PC the
drive is a normal USB stick, and you can start `Azariah.exe` yourself.

## Status

Phase 1 (file explorer shell) and Phase 4 (auto-launch) are working. See [ROADMAP.md](ROADMAP.md)
and the checkpoint in [TODO.md](TODO.md).

| Section | State |
| --- | --- |
| Home | Storage, recent files, shortcuts, Vault and this-PC status |
| Files | Browse, new folder, rename, copy/cut/paste, move, Trash, search, details + SHA-256, drag and drop, open with Windows |
| Roblox | Workspace folders with Places / Scripts / Models / Images / Docs / Backups filters |
| Setup Kit | Organized installers, skills, configs, docs; programs never run without a confirmation |
| Transfer | Drop zone with "Add from this PC" and "Copy selected to this PC" |
| Settings | Auto-launch on/off, drive name, theme, hidden files, Trash |
| Vault, Passwords, AI | Designed, placeholder pages (Phases 2, 5, 7) |

## Quick start (Windows)

1. Put `Azariah.exe` in the root of your USB drive (for example `E:\Azariah.exe`).
2. Double-click it. The first time, it offers to set up the drive: it creates `Files`, `Roblox`,
   `Transfer`, `Public`, `SetupKit` and a small `.azariah` folder. Existing files are not touched.
3. On your own PCs: **Settings → Auto-launch on this PC → Turn on**. From then on, plugging the
   drive in opens AZARIAH within a couple of seconds. It shows up as "Azariah" in
   Task Manager → Startup apps, where you can switch it off.

Windows SmartScreen may warn the first time because the exe isn't code-signed yet:
**More info → Run anyway**.

## Drive layout

```
E:\
├── Azariah.exe        the app (portable, nothing to install)
├── Files\             your stuff
├── Roblox\            Places, Scripts, Models, Assets, Images, Docs, Backups, Exports
├── Transfer\          moving files between computers
├── Public\            things that are fine for anyone to see
├── SetupKit\          Installers\, Skills\, Configs\, Docs\  (public, no secrets)
├── Vault\             encrypted data (Phase 2)
└── .azariah\          app data: drive id, settings, recent files, logs, Trash
```

The drive letter is never hardcoded. AZARIAH finds its root from `--root`, from where the exe
lives, or by scanning drives for `.azariah\drive.json`.

## Build

Requires the .NET 10 SDK.

```powershell
dotnet test Azariah.sln                       # 38 tests, including headless UI rendering
dotnet run --project src/Azariah.App -- --root D:\TestDrive
./scripts/publish.ps1 -Drive E:\              # build Azariah.exe and copy it to the drive
```

From Linux or macOS: `scripts/publish.sh` cross-builds the same Windows exe.
The GitHub Actions workflow is manual-only (Actions → Build AZARIAH → Run workflow) and
uploads `Azariah.exe` as an artifact.

Avalonia sends anonymous build-time telemetry; the scripts and workflow opt out with
`AVALONIA_TELEMETRY_OPTOUT=1`. The app itself sends nothing anywhere.

## Docs

- [ARCHITECTURE.md](ARCHITECTURE.md): projects, layers, how the AI plugs in later
- [SECURITY.md](SECURITY.md): threat model, Vault and trusted-device design, auto-launch safety
- [ROADMAP.md](ROADMAP.md): phases
- [TODO.md](TODO.md): checkpoint for the next coding session
