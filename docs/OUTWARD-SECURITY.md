# Outward page + security (spec and status)

Owner spec gathered 2026-10-22. This is the source of truth for the guest/outward face and the
password/encryption work. Avoid em dashes in files on the flash (owner rule).

## The idea
AZARIAH behaves differently depending on the PC it is plugged into:
- **Owner devices** (the owner's two PCs): the full workspace, updates, everything.
- **Any other PC** (a friend's, a new machine): the **guest page**, a friendly toolkit launcher.
The owner's private files must not be readable on a non-owner PC without the password (encryption, below).

## Owner devices (BUILT, v0.9.8)
- Each PC is recognized by machine name + Windows MachineGuid (`HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`).
- The owner's devices are listed on the drive in `.azariah/devices.json` (`OwnerDevices`). Seeded 2026-10-22 with:
  - `LAPTOPBRUH`, MachineGuid `07f8f78e-3683-4371-8732-196b3e8916b9`.
  - `ZOTAC-FRAKENST` (desktop), name only; its MachineGuid is recorded on its first owner run (`Harden`).
- Match logic `src/Azariah.Core/Devices/OwnerDevices.cs`: strong match by MachineGuid, or by name for a
  name-only entry. Empty list = treat as owner (unclaimed drive, first-time setup still works).
- This is NOT a security boundary (a name/GUID can be copied). It only picks which face to show. Real
  protection is the encryption below. Keep the same wording as `DriveMarker`.
- Routing: `MainViewModel.OpenDrive` checks `OwnerDevicesStore.IsOwnerDevice(MachineIdentity.Current)`
  and calls `OpenWorkspace` (owner) or `OpenGuest` (guest). `WelcomeViewModel` opens through `OpenDrive`.

## Guest page (BUILT, v0.9.8)
`GuestViewModel` + `GuestView.axaml`. Greeting "HELLO / From [AZARIAH]" and big tiles:
- **Windows makeover** (works): runs the Windhawk kit installer at
  `SetupKit\Installers\Utilities\Windhawk\Install Windhawk.cmd` (confirm first; Windows asks for admin).
- **Biggest files** (works): scans this PC's user folders, shows the largest files, "Show" reveals one.
  Core `src/Azariah.Core/Space/BigFilesScanner.cs` (read-only).
- **Shared files** (works): the Setup Kit (Public/Transfer can be added), so friends can reach what the
  owner shared. Never surfaces owner Notes, games, card shows or the full Files.
- **Optimize PC**, **Local AI**, **Virus check**: tiles marked "Soon" (not built).
- Owner preview: Settings > "Preview guest page" opens the guest page with a banner + "Exit preview"
  (`WorkspaceViewModel.PreviewGuestRequested` -> `MainViewModel` swaps to a preview `GuestViewModel`).

### Guest tiles still to build
- **Optimize PC**: safe cleanup (temp files, recycle bin, caches) with sizes shown and confirmation.
- **Local AI**: the on-flash local LLM (not created yet). Model(s) stored on the flash; runtime installed
  into the verified PC copy, never run from the USB. Picks a model to fit the PC's GPU/VRAM/RAM.
- **Virus check**: a Windows Defender quick scan (`Start-MpScan`/`MpCmdRun -Scan`), results shown. Benign.

## Password unlock on a non-owner PC (SPEC, not built)
Lets the owner turn a guest session into the full workspace by typing a password, for when the owner is
on someone else's PC.
- **Test password (placeholder): `88801550`.** This is temporary. NEVER store the password in plaintext.
  Derive a key with Argon2id and store only a verifier; see SECURITY.md. The test value is for wiring the
  flow only; replace before real use.
- **Type anywhere** on the guest page: an ambient key capture builds the typed digits; a correct match
  unlocks and switches to the owner view. No visible field required (type at any time, any place).
- **Reset the attempt**: pressing Enter, or Tab twice (pick one), clears the current buffer so the owner
  can start the number over if they mistyped.
- **Lockout**: after **5** wrong entries or resets, the flash **ejects itself** and **locks for 10 minutes**
  (wall-clock from the computer). Record the lockout time on the flash so a re-plug within 10 minutes stays
  locked; once 10 minutes have passed, the next plug-in allows attempts again.
- Eject on Windows: CM_Request_Device_Eject / `mountvol /p`, or the shell eject verb. Record lockout
  BEFORE ejecting (e.g. `.azariah/lock.json` with the UTC timestamp and strike count).
- Caveat to tell the owner: eject + timed lock is a deterrent, not real protection (the flash can be read
  on another OS). Real protection is the encryption below; the password gates both.

## Encryption / full security on non-owner devices (SPEC, not built; the big one)
Goal: on a non-owner PC, a guest cannot read the owner's private file **contents, names or sizes** without
the password. On owner devices, no password friction (auto-unlock via a device key).
- Repo rule: **no custom cryptography.** Standard primitives only. Read `SECURITY.md` before building.
- Plan (align with SECURITY.md, Phase 2 Vault + Phase 3 trusted devices):
  - KDF: **Argon2id** (maintained libsodium-based binding) from the password to a master key.
  - Per-file: **AES-256-GCM** (.NET `AesGcm`). Encrypt an index so names/sizes are hidden too.
  - Owner devices auto-unlock with a device key (DPAPI-protected on the PC), so no password on your PCs.
  - Write format tests FIRST (tamper, truncation, wrong password, corrupted-file isolation) before any UI.
- Scope question for the owner: which areas are private-encrypted (Notes, Files, Roblox, Card shows) vs
  shared-plain (Public, Transfer, Setup Kit, the guest tiles). Guest tiles read the PC, not the drive, so
  they keep working while the drive is locked.

## Build / deploy / test
- Build+test+install: `powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1` (drive + this PC's
  auto-launch copy via `--finish-update`, hash-checked). Then commit + push.
- Tests as of v0.9.8: 87 pass (68 core, 19 app). New: `OwnerDevicesTests`, `GuestPageTests`.
- Other PCs do not self-update from the drive; run deploy.ps1 there (desktop is behind).
