# Checkpoint / TODO

Read this first when picking up the project in a new session.

## Status: Phase 1 in progress (work-in-progress checkpoint)

The solution builds (`dotnet build Azariah.sln`, .NET 10 SDK). There is no UI yet.

### Decisions made
- **Language/runtime:** C# on .NET 10 (LTS).
- **UI framework:** Avalonia 12 (Fluent theme, custom dark styling, Material icons via
  `Material.Icons.Avalonia`), MVVM with CommunityToolkit.Mvvm. Reasons: portable self-contained
  publish that runs from the USB with no install, full control over a custom look, and the UI
  and logic can be built and tested in CI on any OS. Windows-only features (DPAPI, WinVerifyTrust,
  USB arrival events) go behind platform interfaces.
- **Package versions** are pinned centrally in `Directory.Packages.props`.
- **Drive root** is never hardcoded. It is found by `--root`, `AZARIAH_ROOT`, walking up from the
  executable to find `.azariah/drive.json`, or scanning mounted volumes for that marker.
  `DriveType.Removable` is NOT relied on (fast USB sticks often report as fixed disks).
- **The drive marker is identification, not trust.** Trust comes from Phase 3 crypto pairing.
- **Deletes go to an on-drive Trash** (`.azariah/Trash`) because Windows has no Recycle Bin for
  removable drives.
- **Auto-launch (Phase 4)** must not blindly run the exe found on a USB (a fake USB with the right
  marker would get code execution). Plan: the launcher runs a verified local copy.

### Done (in `src/Azariah.Core`)
- `Drive/DriveLayout.cs`: well-known folders, root-relative path helpers, protected areas.
- `Drive/DriveMarker.cs`, `Drive/DriveMarkerStore.cs`: `.azariah/drive.json` identity marker.
- `Drive/DriveRootLocator.cs`, `Drive/VolumeInfo.cs`: root detection, volume scan, find by drive id.
- `Drive/DriveMonitor.cs`: detects removal and re-plug (possibly under a new letter).
- `Drive/DriveSummary.cs`: storage usage numbers.
- `Files/PathGuard.cs`, `Files/FileNameRules.cs`, `Files/UniqueNames.cs`: path safety and naming.
- `Files/FileKind.cs`, `Files/FileEntry.cs`, `Files/OperationModels.cs`, `Files/FileOperationException.cs`.
- `Files/TrashService.cs`: move to Trash, list, restore, delete permanently, empty.
- `Serialization/JsonFile.cs`: atomic JSON writes. `Serialization/AzariahJsonContext.cs`: source-gen JSON.
- `Diagnostics/AppLog.cs`: file log with a no-secrets rule.
- `src/Azariah.AI.Abstractions`: empty project reserved for the Phase 7 provider/permission interfaces.

### Next steps (in order)
1. `Files/FileOperationService.cs`: list, create folder, rename, copy/move with conflict policy
   (KeepBoth/Replace/Skip), safe replace via temp file, progress + cancellation, import/export,
   move to Trash. `Files/FileSearchService.cs`: recursive search that skips `.azariah` and `Vault`.
2. `Settings/` (AppSettings + store on the USB), recent files store (root-relative paths, never Vault items),
   `DriveInitializer` (creates standard folders + marker, refuses the system drive by default).
3. Setup Kit core (`SetupKit/`): installer sidecar manifests (`<file>.installer.json` with name,
   version, date downloaded, architecture, official source, optional SHA-256), SHA-256 verify,
   skills catalog (`SetupKit/Skills/<Tool>/<skill>/`), data-driven tool registry (`SetupKit/tools.json`)
   with install targets, secret scanner that warns about keys/tokens in Setup Kit files.
   Installers only launch after an explicit confirmation dialog; never automatically.
4. AI abstractions: provider interface, content parts (text/image), capability permissions
   (See Screen, Read USB Files, Read Vault, Open Files, Modify Files, Execute Actions), data sensitivity routing.
5. `src/Azariah.App` (Avalonia): shell with sidebar (Home, Files, Vault, Passwords, Roblox,
   Setup Kit, Transfer, AI, Settings), file browser, dialogs, drag and drop, Trash view,
   placeholders for Vault/Passwords/AI.
6. Tests (`tests/Azariah.Core.Tests`, headless UI smoke tests).
7. Docs: README, ARCHITECTURE, SECURITY (Vault + trusted-device model), ROADMAP.
8. `scripts/publish-usb.ps1`, manual-only GitHub Actions workflow (user asked to avoid surprise costs).
