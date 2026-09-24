# Security

## Threat model

Assume the drive will be lost or stolen at some point (it's on a keychain), and that whoever
has it can copy every byte and read it on their own machine.

| Asset | Protection |
| --- | --- |
| Vault files, passwords, AI memory about you, API keys | Encryption with keys derived from your master password (Phases 2, 5) |
| Normal folders (Files, Roblox, Transfer, Public, Setup Kit) | None. They are a normal USB stick by design. Don't put secrets there. |
| Your PCs | AZARIAH never makes a PC run code from a USB by itself (see Auto-launch) |

Not protected against: a PC that is already compromised (keyloggers or malware running as you
can see anything you unlock there), and someone forcing you to unlock. Don't unlock the Vault
on computers you don't trust.

Security never relies on hidden folders, file attributes, odd file names or extensions, UI
restrictions, or secret algorithms. The `.azariah` and `Vault` folders are protected in the
UI only to prevent accidents.

## Vault design (Phase 2)

Standard, audited primitives only. No custom cryptography.

- **KDF**: Argon2id (libsodium via a maintained .NET binding), random 16-byte salt, parameters
  stored in the header, starting around 256 MiB memory / 3 passes, tuned so unlock takes ~1 s on
  your slowest PC.
- **Key hierarchy**:
  - Random 256-bit Vault Master Key (VMK).
  - The VMK is wrapped (AES-256-GCM) by a key-encryption key derived from your password.
  - Extra "key slots" can wrap the same VMK: a printed **recovery key**, and one slot per
    trusted PC (Phase 3).
  - The password itself is never stored anywhere.
- **Per-file encryption**: every file gets its own random 256-bit file key, wrapped by the VMK
  and stored in that file's header. Content is encrypted in 64 KiB chunks with AES-256-GCM
  using the STREAM construction (chunk counter + last-chunk flag in the nonce, header as
  associated data), the same design used by age and Tink. Truncation, reordering and edits
  are detected.
- **File names** are encrypted inside each file's header; on disk files are random ids.
  A small encrypted index speeds up browsing but can always be rebuilt by scanning.
- **Corruption isolation**: each file stands alone, so one damaged file never takes others with
  it. The header holding the key slots is written twice (`vault.header` + backup) with atomic
  replace. The recovery key covers a forgotten password.
- **Memory**: keys live in pinned buffers, zeroed on lock (`CryptographicOperations.ZeroMemory`).
  .NET can't guarantee no copies ever exist, so exposure is kept short: unlock, use, lock.
- **Locking**: on drive removal, app exit, manual lock, and an idle timer. If the app crashes
  or the drive is yanked, the keys die with the process.
- **Plaintext on disk**: in-app previews for text, images and code avoid temp files. "Open in
  another app" has to write a temp copy (other apps need a path), so it goes to a per-session
  folder on the PC that is wiped on lock, with a warning. SSDs can't guarantee secure deletion,
  so this is opt-in per file.
- **Nothing sensitive in plaintext metadata**: recent files, logs and search never include
  Vault names or content (enforced in `RecentFilesStore` and the logging rule).

## Trusted computers (Phase 3)

Goal: your desktop and laptop unlock the Vault automatically; everything else asks for the password.

- Pairing (only while the Vault is unlocked, only when you click "Pair this PC"):
  1. The PC generates a random 256-bit device key `DK`.
  2. `DK` is stored on the PC only, protected with **DPAPI** (CurrentUser), optionally sealed
     by the **TPM** so it can't be copied off the machine.
  3. The drive gets a new key slot: the VMK wrapped with a key derived from `DK`
     (HKDF-SHA256 then AES-256-GCM), plus the device's id, name and pairing date.
- Auto-unlock: read `DK` via DPAPI, unwrap the slot, done. Any failure (missing key, bad tag,
  revoked slot) **fails closed** to the normal password screen.
- The computer name is shown for convenience and never used for trust.
- The drive alone can't unlock anything (no `DK`). The PC alone can't either (no slot data)
  unless it also has a copy of the drive.
- **Revoke** deletes the slot. If the PC might have been compromised, also run **Rotate Vault
  key**: new VMK, re-wrap every file key (headers only, contents untouched), all slots re-issued.
- Settings: list, pair, revoke, turn auto-unlock off, "always require the master password".
- Trade-off to know: with auto-unlock on, anyone who can sign in to that PC as you and has the
  drive gets in. Optional hardening: require Windows Hello before auto-unlock.

## Auto-launch (Phase 4, built)

Plug in → AZARIAH opens, only on PCs where you turned it on.

- Turning it on copies the running `Azariah.exe` to `%LOCALAPPDATA%\Programs\Azariah\`
  (verified by SHA-256 after copying), saves this drive's id to
  `%LOCALAPPDATA%\Azariah\launcher.json`, and adds a per-user startup entry
  (`HKCU\...\Run`, "Azariah"). No admin rights, no Windows service.
- The watcher (`Azariah.exe --watch`) polls drives once a second, skips network and optical
  drives, and when a paired drive id appears it starts **its own local copy** with `--root`.
- It never executes anything from the USB. That matters: the drive marker is not a secret, so
  a fake USB with a copied `drive.json` and a malicious `Azariah.exe` would otherwise get code
  execution on your PC (this is exactly why Windows removed USB AutoRun). With this design the
  worst a fake drive can do is make your own trusted copy open and show its files.
- Updating: the Settings page offers "Update this PC" when the drive has a different version;
  the new exe is copied and hash-checked, the watcher restarts.
- Running the local copy also means yanking the drive can't crash the app mid-run.

## Setup Kit

- Public by design: usable without unlocking, so it must never hold credentials, API keys,
  tokens, cookies, recovery keys or `.kdbx` files. READMEs in the kit say so; a secret scanner
  that warns about common key formats is planned.
- Programs never run automatically. Opening any executable or script from AZARIAH shows a
  confirmation naming the file and the PC it will run on.
- Files copied onto exFAT/FAT32 lose Windows' Mark-of-the-Web, so SmartScreen won't vet them.
  Planned: record the vendor's SHA-256 in the installer manifest and verify it (plus the
  Authenticode signature) before launching.

## Other decisions

- Deletes go to an on-drive Trash, because Windows deletes from USB drives permanently.
- All settings and metadata writes are atomic (temp file + replace) to survive yanks.
- Logs never contain passwords, keys, tokens, Vault names or Vault content.
- `.gitignore` blocks common secret files (`.env`, `*.pfx`, `*.kdbx`, `credentials.json`, ...).
  Never commit secrets to this repo.

## Changes from the original plan (and why)

1. **Launcher runs a verified local copy, not the exe on the USB.** Blindly running the USB
   exe would let any USB with the right marker run code on your PC.
2. **Per-user startup entry instead of a Windows service.** A service runs as SYSTEM in an
   invisible session and can't show UI without tricks; more privilege, more risk, no benefit.
3. **The drive is found by an id marker, not by "removable" type or drive letter.** Fast USB
   sticks (including PNY's) often report as fixed disks.
4. **App-level Trash.** Windows has no Recycle Bin for removable drives.
5. **Recovery key and backups.** Flash drives fail and get lost. The Vault's ciphertext can be
   backed up anywhere safely; a printed recovery key covers a forgotten password.
6. **Don't unlock on untrusted PCs.** Encryption protects the drive at rest, not a Vault that's
   open on a compromised machine.
7. **Passwords use KDBX 4 (KeePass format)**, so KeePassXC can open them if AZARIAH ever breaks.
8. **AI via MCP first.** Claude Desktop can use AZARIAH's tools on your existing plan, instead
   of AZARIAH holding a paid API key.
