# Checkpoint / TODO

Read this first when picking up the project in a new session.

## Where things stand

- Solution builds with **zero warnings** (warnings are errors) on the .NET 10 SDK.
- `dotnet test Azariah.sln`: 38 tests pass (35 core, 3 headless UI incl. rendering every page).
- `dotnet publish src/Azariah.App -c Release -r win-x64` produces a single portable, trimmed
  `Azariah.exe` (~24 MB). Works when cross-built from Linux.
- A trimmed linux-x64 build was smoke-tested under Xvfb: setup, navigation, new folder,
  rename (F2), delete to Trash via Enter in the dialog, Settings.
- v0.1 exe was handed to the user directly (zip), since the Actions workflow only appears
  once it's on the default branch.
- Not yet verified on real Windows hardware: auto-launch end to end, Explorer drag-in,
  system clipboard paste, custom title bar. **Test these first on Windows.**

## Done

- Phase 1 shell: Home (state view), Files (with Import/Export), Roblox (kind filters),
  Setup (sections), Settings, Trash page, Welcome/setup screen, disconnect overlay,
  dark/light themes, in-window dialogs.
- Interim design cleanup (session 2): `[A]` logo + icon, native title bar, neutral palette,
  marketing copy and placeholder pages removed, nav cut to Home/Files/Roblox/Setup/Settings.
  Transfer is a location; AI will be a command surface; Passwords will live inside Vault.
- Core: drive root detection (no drive letters), drive monitor (re-plug under new letter
  rebuilds the session), safe file ops (temp-file replace, conflicts, progress, cancel),
  on-drive Trash, search (text, wildcards, kinds), recent files (relative, never Vault),
  atomic JSON, no-secrets logging.
- Phase 4 auto-launch: `Azariah.exe --watch`, local verified copy in
  `%LOCALAPPDATA%\Programs\Azariah`, HKCU Run entry, per-PC `launcher.json`, Settings toggle,
  single instance per drive (second launch focuses the window).
- Run confirmation before opening any executable/script from the app.
- AI contracts in `src/Azariah.AI.Abstractions` (providers, tools, capabilities, permissions).
- Docs: README, ARCHITECTURE, SECURITY (Vault + trusted devices + auto-launch), ROADMAP.

## Session 3 additions (v0.3)

- [A] open animation (glitch in, brackets open; skippable; off via Settings or Windows animation setting).
- Unplugging the drive shows a short "Disconnected" screen, then AZARIAH closes (so the next
  plug-in opens it fresh). Disconnect needs 2 failed checks in a row.
- Drop-to-update: Settings > Update. A dropped zip/exe is staged in %TEMP% and run with
  `--finish-update`, which waits for the app to exit, replaces the drive exe and the PC copy
  (hash-verified), restarts the watcher, and reopens AZARIAH. Only 0.3+ builds can be installed this way.
- Started from the drive on a PC with auto-launch and an identical PC copy: hands off to the PC copy.
- Settings shows whether the background watcher is running (with a Start button); the app also
  restarts a dead watcher on launch. Single-instance now falls through if the old window hangs.
- **Auto-launch still unverified on real Windows.** First report: replug did nothing (likely the old
  window never closed). Check with v0.3; if it still fails, read `%LOCALAPPDATA%\Azariah\logs`.

## Session 3b (v0.4)

- Brand: `[A]` = mark/icon, `[AZARIAH]` = wordmark (Montserrat Bold outlines as vector paths +
  the logo's bracket proportions) in `src/Azariah.App/Brand.cs`. Window title and FileDescription
  are `[AZARIAH]`; ProductName stays `Azariah` (update validation relies on it).
- Launch animation lengthened to ~2.6 s: [A] glitch in, glitch swap to [AZARIAH], brackets open.
  Clock starts on the first rendered tick.
- Old icon showing after an update = Windows icon cache (`ie4uinit.exe -show` or restart).

## Session 4 (v0.5)

- The owner's drive already has a pass system: `<drive>\CLAUDE-START-HERE.md` (index) and
  `Games\<GAME>\CLAUDE.md` + `PASSES.md` per game. The app now has its own pass at
  `<drive>\Projects\AZARIAH\` (CLAUDE.md + PASSES.md) and a row in CLAUDE-START-HERE.md.
  **When the owner says "pass" about the app: update those two files (hand them over as a zip, since
  cloud sessions can't write to the USB), commit + push, ship the exe.**
- Drive layout agreed: `Roblox\` = general Roblox stuff (Scripts, Models, Assets, Images, Docs),
  `Games\<GAME>\` = game-specific. The Roblox page has Games / General scopes.
- The app no longer recreates standard folders on every launch (only at setup or from Settings).
- Next for Roblox: show each `Games\<GAME>` as a project (CLAUDE.md status, LATEST place,
  Old Versions, last pass).

## Next, in order

0. **UI redesign: follow `docs/DESIGN.md` "Next session: do this".** Start by rendering the
   three typography directions and asking the owner. Don't build screens before they choose.
1. **Windows smoke test** of the checklist in README "Quick start" and fix anything found.
2. **Setup Kit v2** (see ROADMAP): installer sidecar manifests + SHA-256 verify + Authenticode
   check (`WinVerifyTrust`), Installers page, Skills page with install targets from
   `SetupKit/tools.json`, secret scanner.
3. **Phase 2 Vault** per SECURITY.md. Pick the Argon2id binding (libsodium-based, maintained),
   use .NET `AesGcm`. Write format tests before UI. Wire lock into
   `MainViewModel.OnDisconnected` and `Shutdown` (comments mark the spots).
4. **Phase 7a**: MCP server exposing drive search / read / open tools, gated by permissions.

## Known limitations

- Dragging files *out* of AZARIAH into Explorer isn't supported yet (drag in and internal
  drag to folders work).
- Cut in AZARIAH + paste in Explorer copies instead of moving (system clipboard gets files only).
- Folder sizes aren't shown in lists (only files).
- If the exe runs from the USB (not the auto-launch copy) and the drive is yanked, Windows may
  kill the app. No data loss: all writes are atomic or temp-then-swap.
- The exe isn't code-signed, so SmartScreen warns on first run.

## Conventions

- Stored paths are drive-relative. Never persist absolute drive paths on the USB.
- All file changes go through `FileOperationService` (or `TrashService`).
- No secrets in logs, recent files, settings, Setup Kit, or git.
- Package versions live in `Directory.Packages.props`.
- Avalonia build telemetry: set `AVALONIA_TELEMETRY_OPTOUT=1` when building.
