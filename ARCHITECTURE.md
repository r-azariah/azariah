# Architecture

## Goals

- Portable: one self-contained `Azariah.exe` that runs from the USB with nothing installed.
- Drive-relative: every stored path is relative to the drive root, found at runtime.
- Modular: Vault, Passwords and AI plug into the same session without the file explorer
  knowing about them.
- Safe by default: writes stay inside the drive, destructive actions confirm and are
  recoverable, secrets never touch plaintext storage or logs.

## Technology choice

| Option | Verdict |
| --- | --- |
| **Avalonia 12 (chosen)** | Full control over a custom dark look, fast Skia rendering with list virtualization, portable single-file publish, and UI + logic can be built and tested headlessly in CI on any OS. Windows-only features sit behind small platform classes. |
| WPF | Mature and stable, but dated styling without extra libraries, and it can only be built/tested on Windows. |
| WinUI 3 | Most native Fluent look, but unpackaged self-contained deployment from a USB is awkward and the tooling is Windows-only. |

MVVM uses CommunityToolkit.Mvvm. Icons come from Material.Icons.Avalonia. Views are mapped
to view models explicitly in `App.axaml` (no reflection).

## Projects

```
src/
  Azariah.Core              no UI, no platform APIs (except guarded ones)
    Drive/                  DriveLayout, DriveRootLocator, DriveMarker, DriveMonitor, DriveInitializer
    Files/                  FileOperationService, TrashService, FileSearchService, RecentFilesStore,
                            PathGuard, FileNameRules, FileKind
    Settings/               AppSettings (+ store on the drive)
    SetupKit/               SetupKitLayout (folder structure, no-secrets READMEs)
    Launcher/               LauncherConfig (per-PC), DriveArrivalWatcher
    Serialization/          source-generated JSON, atomic writes
    Diagnostics/            IAppLog (no-secrets rule)
  Azariah.AI.Abstractions   Phase 7 contracts only: IAiProvider, IAiTool, AiCapability,
                            IAiPermissionPolicy, DataSensitivity, IAiRouter
  Azariah.App               Avalonia UI
    Platform/               AutoLaunchManager, WatchMode, StartupRegistration (HKCU Run),
                            SingleInstance, WatcherSignals
    Services/               WorkspaceSession, DialogService, ShellService, PlatformUi, FileClipboard
    ViewModels/, Views/     pages and dialogs
    Theme/                  color tokens (dark + light) and styles
tests/
  Azariah.Core.Tests        drive detection, file operations, Trash, search, launcher watcher
  Azariah.App.Tests         headless rendering of every page
```

## Runtime flow

```
Azariah.exe [--root X:\] [--watch]
   │
   ├─ --watch → WatchMode (no UI): poll drives every second, launch the local copy
   │            with --root when a paired drive appears
   │
   └─ UI mode
        DriveRootLocator: --root → AZARIAH_ROOT → walk up from exe → scan volumes
        SingleInstance: one window per drive id (second launch just focuses it)
        MainViewModel
          ├─ WelcomeViewModel (no drive / fresh drive → set up)
          └─ WorkspaceViewModel(WorkspaceSession)
                sidebar pages: Home, Files, Vault, Passwords, Roblox, Setup Kit, Transfer, AI, Settings
        DriveMonitor: removal → "disconnected" overlay (Phase 2: lock Vault first);
                      return under a new letter → session rebuilt against the new root
```

`WorkspaceSession` owns every service for one mounted drive. If the drive comes back as a
different letter the whole session is rebuilt, so nothing holds a stale path. Future modules
(Vault, Passwords, AI) are added to the session the same way.

## File operations

All normal-file changes go through `FileOperationService`:

- Destination must be inside the drive root (exports to the PC are a separate, explicit call).
- `.azariah` and `Vault` are protected from the Files browser (accident prevention, not security).
- Replacing a file copies to a temp file first and swaps it in only when complete.
- Conflicts: Keep both / Replace (folders merge) / Skip, chosen by the user.
- Copies report byte-level progress and can be cancelled.
- Delete moves to `.azariah/Trash` (Windows has no Recycle Bin on USB drives); permanent
  deletion only happens from the Trash page, with confirmation.

## Setup Kit

`SetupKit/` is public, unencrypted and usable without unlocking anything. Categories and tools
are folders, so adding a new AI app or dev tool needs no code change. Opening any program or
script (`.exe`, `.msi`, `.bat`, `.ps1`, ...) from any page shows a "Run this program?"
confirmation; nothing runs automatically. Next steps (see ROADMAP) add sidecar manifests
(`<installer>.installer.json`: name, version, date, architecture, official source, SHA-256),
checksum verification before launch, a skills browser with install targets, a secret scanner,
and a "Set Up This PC" checklist.

## AI (Phase 7) shape

The assistant is a separate module that only talks to the rest of the app through tools.

```
            ┌──────────── brains (swappable) ────────────┐
            │ Claude Desktop / Claude Code via MCP       │  ← uses your Claude plan
            │ Local model (llama.cpp/Ollama, models on USB)│
            │ Cloud API providers (IAiProvider)          │
            └───────────────┬────────────────────────────┘
                            │ tool calls
                  ┌─────────▼──────────┐
                  │  IAiTool registry  │  search_usb_files, read_file, capture_screen,
                  │  + permission      │  open_file, list_roblox_project, ...
                  │    policy          │  each tool declares its AiCapability
                  └─────────┬──────────┘
                            │ same services the UI uses
                  FileSearchService, FileOperationService, Vault (when unlocked), ...
```

- **MCP host mode**: AZARIAH runs a local MCP server; Claude Desktop (on your subscription)
  calls its tools. This is how Roblox Studio's MCP integration works too. The chat lives in
  Claude Desktop.
- **In-app mode**: AZARIAH calls an `IAiProvider` itself (local model or an API key stored in
  the Vault). `IAiRouter` keeps `DataSensitivity.Vault` content on local models unless you allow otherwise.
- **Permissions** (`AiCapability`): See Screen, Read USB Files, Read Vault, Open Files,
  Modify Files, Execute Actions. Each is Disabled / Ask every time / Allowed. Tools needing a
  disabled capability are never offered to the model. Read Vault also requires the Vault to be unlocked.
- Screen capture only on an explicit hotkey press; nothing is recorded in the background.
