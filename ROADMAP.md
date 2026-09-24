# Roadmap

| Phase | What | Status |
| --- | --- | --- |
| 1 | File explorer shell: Home, Files, Roblox, Transfer, Settings, Trash, drive detection | **Done** |
| 4 | Auto-launch on your PCs (verified local copy + startup entry) | **Done** (moved up) |
| Kit | Setup Kit v1: organized folders, run confirmation | **Done** |
| Kit | Setup Kit v2: installer manifests + SHA-256/signature checks, skills browser + install to this PC, secret scanner | Next |
| 2 | Encrypted Vault (Argon2id, AES-256-GCM per file, recovery key, auto-lock) | Next |
| 3 | Trusted computers (DPAPI/TPM device keys, pair/revoke, auto-unlock) | After Vault |
| 7a | AZARIAH assistant via MCP: Claude Desktop uses AZARIAH's tools (search drive, read Roblox scripts, open files, screenshot on hotkey) | After Vault |
| 5 | Passwords (KDBX 4, KeePassXC-compatible, clipboard auto-clear) | Later |
| 6 | Roblox workspace v2: project view, checkpoints (ZIP snapshots), Luau search | Later |
| 7b | In-app chat, local models on the drive, cloud providers, "about me" memory in the Vault | Later |
| Kit | "Set Up This PC": pick installers/skills/configs and apply them in one go | Later |

## Phase details

### Setup Kit v2
- `<installer>.installer.json` sidecar: name, version, date downloaded, architecture,
  official source URL, optional vendor SHA-256, expected publisher, notes.
- Installers page: grouped by category, verify checksum, show signature, launch with
  confirmation; refuse on checksum mismatch.
- Skills page: `SetupKit/Skills/<Tool>/<skill>/`, description from `SKILL.md` front matter,
  preview, copy to a folder, install to known targets (`%USERPROFILE%\.claude\skills`).
  Targets live in `SetupKit/tools.json`, so new tools are data, not code.
- Secret scanner: warn on API key patterns, private keys, `.env`, `credentials.json`, `.kdbx`.

### Phase 2: Vault
See SECURITY.md. Deliverables: vault format + tests (tamper, truncation, wrong password,
corrupted file isolation), unlock/lock UI, browse/add/export/delete inside the Vault,
in-app preview, auto-lock on removal/exit/idle, recovery key.

### Phase 3: Trusted computers
DPAPI-protected device key, key slot on the drive, auto-unlock with fail-closed fallback,
Settings list/pair/revoke/disable, optional Windows Hello gate, Vault key rotation.

### Phase 7: AZARIAH assistant
1. MCP server inside AZARIAH exposing tools gated by `AiCapability` permissions.
2. Global hotkey → capture screen → hand to the assistant (only on press).
3. Later: in-app chat window, `IAiProvider` implementations (local llama.cpp/Ollama with
   models stored on the drive; cloud APIs with keys kept in the Vault), `IAiRouter`.
