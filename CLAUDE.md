# AZARIAH: notes for coding sessions

Start with `TODO.md` (checkpoint), then `docs/DESIGN.md` for anything visual, then
`ARCHITECTURE.md` and `SECURITY.md`.

- Build/test: `dotnet test Azariah.sln` (.NET 10 SDK). Warnings are errors.
- Windows exe: `scripts/publish.sh` (Linux/macOS) or `scripts/publish.ps1` (Windows).
- Set `AVALONIA_TELEMETRY_OPTOUT=1` for builds.
- UI screenshots for checking layout: `AZARIAH_SCREENSHOTS=<dir> dotnet test tests/Azariah.App.Tests`.

Rules:
- No custom cryptography. Standard primitives and constructions only (see SECURITY.md).
- Never log or persist secrets, keys, Vault names or Vault content in plaintext.
- Never hardcode drive letters; store drive-relative paths.
- Nothing executes from the USB automatically. Programs launch only after a confirmation.
- File changes go through `FileOperationService`; deletes go to Trash.
- UI: it's the owner's tool, not a product page. No marketing copy. Ask before identity decisions.
- Update `TODO.md` at the end of every session.
