# AZARIAH design direction

Working document for the UI redesign. The owner wants to take part in every identity decision:
**show options and ask before committing** (see "Open decisions").

Skills to use while working on this: `frontend-design` (Anthropic) and `ui-ux-pro-max`
(search tool: `python3 <skill>/scripts/search.py "<query>" --stack avalonia` or `--domain ux|typography|style`).

## What this is

Not a website, not a SaaS dashboard, not a product selling itself. It's the owner's own tool:
a small operating environment on a USB drive. It already knows who it's for, so it never
explains itself. Every persistent element must do one of: show information the owner needs,
expose an action, establish hierarchy, or carry the visual identity. Otherwise delete it.

## Logo

`[A]`: square brackets around a geometric A, white on black. Vector in
`Theme/Colors.axaml` (`AzMark`, EvenOdd so the A's counter is cut out) and rendered into
`Assets/azariah.ico` from the same geometry. Use it plain. No glow, no gradients, no variations.

## Done in the interim pass (session 2)

- `[A]` mark in the app and as the exe/taskbar icon.
- Native Windows title bar (Avalonia 12's drawn title bar overlapped our own and added a
  fullscreen button). A custom bar comes with the redesign; see "Avalonia notes".
- Neutral foundations: true black / near-white, no cyan accent. Color is reserved for state.
- Removed: greeting, marketing copy, feature bullet lists, "Soon" pills, explanatory banners,
  eyebrow labels, the Vault/Passwords/AI placeholder pages, decorative cards on Home.
- Navigation cut to Home, Files, Roblox, Setup, Settings. Transfer is a location (on Home and
  in Files); Import/Export moved into the Files toolbar so they work everywhere.
- Home shows state only: recent files, storage, Vault state, this PC, auto-launch, Transfer
  count, places.

This is a clean base, not the redesign. The visual identity below is still open.

## Why the first UI felt generic (audit)

- The SaaS card kit: identical rounded cards, icon-in-tinted-square everywhere, pills.
- Cyan accent on tinted near-black (#0A0D12): the "dark dashboard with one bright accent" default.
- Template chrome: tracked all-caps eyebrow above every heading, "A · B · C" meta strings.
- Marketing voice inside the owner's own tool ("Your own JARVIS, minus the voice").
- Controls that do nothing (disabled permission toggles) and pages for unbuilt features.
- Nine equal nav items because nine sections were named, not because they're used equally.

## Point of view (proposal)

**Place names as architecture.** Where you are is set in large type, like a sign on a
building; everything operational is small, precise and close to the content. The one bold move,
borrowed from the NexStudio reference: *content interrupts the type*. The file list, a preview,
or the Vault's lock state overlaps the lower part of the giant location word, creating depth
without gradients or shadows. Big type is rare: full size on Home and Vault, reduced on Files
(density wins there), absent in dialogs.

From the Loop reference: sparse editorial information architecture, tiny functional navigation
around large content, offset (not centered) composition, a small solid square as the only
marker for "current".

Keyboard-first. Quiet until needed. Motion answers actions (open, expand, lock) and nothing else.

### Color tokens (start neutral)

| Token | Light | Dark | Use |
| --- | --- | --- | --- |
| Paper | #EDEDEA | #000000 | background |
| Ink | #000000 | #EDEDEA | text, primary actions, selection |
| Graphite | #5E5E5A | #9A9A96 | secondary text |
| Rule | #D2D2CD | #262626 | the few lines that carry structure |
| Alarm | #DC2626 | #F87171 | destructive actions, errors |

A signature accent can come later, owner's call.

### Typography: three directions to show the owner

Render the **same Files screen** in each (1280x800, light and dark) and let the owner pick.
All faces are OFL from `google/fonts` (sparse-clone just the family folders; use static TTFs,
embed as `AvaloniaResource`).

- **A. Heavy grotesk**: Archivo Black for place names; IBM Plex Sans for everything else
  (tabular figures via font features). Closest to NexStudio. Confident, graphic.
- **B. Condensed poster**: Anton for place names; Inter (bundled) for UI; IBM Plex Mono only
  for sizes and dates. Closest to Loop. Tall, editorial.
- **C. Hairline instrument**: IBM Plex Sans Thin at display size, Plex Sans Regular/Medium for
  UI. Huge but light, like an engraved instrument panel. Quietest of the three.

Rules regardless of choice: at most two families; dense lists at 13–14px with 1.4–1.5 line
height; no all-caps labels except where the chosen direction needs them; numbers tabular.

### Layout

```
[A]  Home  Files  Roblox  Vault  Setup                  D:\  23.8 GB free   _ □ ×
                                                                           (custom bar)
 ROBLOX ─────────────────────────────── (giant place name, left aligned)
     ┌─ content layer overlaps the bottom of the word ───────────────┐
     │ Obby Rush           place   yesterday   48 MB                 │  preview
     │ SpawnHandler.luau   script  2 hr ago    4 KB                  │  (right)
     └───────────────────────────────────────────────────────────────┘
 Ctrl+Space  ask or go anywhere                              3 selected  copying 40%
```

Left-aligned throughout. Asymmetric: content column plus a preview column; no centered heroes.

### Information architecture (proposal, partly applied)

| Destination | Where it lives |
| --- | --- |
| Home | Permanent. Current state, not a landing page. |
| Files | Permanent. The filesystem: list and grid, preview, pins, recent locations. |
| Roblox | Permanent. Project-oriented: each project with places, scripts, assets, checkpoints. |
| Vault | Permanent once built. Locked = the giant word with nothing over it; unlocking slides the private filesystem over it. **Passwords is a tab inside Vault.** |
| Setup | Permanent. Installers table (version, arch, date, source, checksum) + skills. Not an app store. |
| Transfer | Not a section. A location surfaced on Home when it has items. |
| AI | Not a section. The command surface, everywhere. Permissions live in Settings. |
| Settings | Reachable from `[A]`, the command surface, or a small corner link. |

### Command surface

- Hotkey (default Ctrl+Space in-app; global hotkey later, configurable) opens a single-line
  input at the top-left of the content area, focus already inside. Escape closes.
- Typing filters places, files, recent items and commands immediately (local, instant).
  Natural-language requests go to the assistant (MCP/local/cloud per ARCHITECTURE.md).
- Simple results stay in a compact list under the input; a conversation expands the surface
  downward into a workspace. It knows the current page and selection.
- History of commands is personal state (shown on Home).

### Motion

Fast (120–220 ms), ease-out, interruptible. Uses: page change (content layer slides over the
place name), command surface expand, preview open, Vault lock/unlock (content layer leaves or
arrives), selection movement. Nothing else animates. Respect reduced motion.

## Next session: do this, in order

1. Fetch fonts (Archivo Black, Anton, IBM Plex Sans, IBM Plex Mono) and render the three type
   directions on the Files screen. **Show the owner and ask** (typography, then the open
   decisions below). Don't build further until they pick.
2. Redesign **Files** completely in the chosen direction: custom title bar, place-name type,
   content layer, list + grid, preview, pins. Build, run, screenshot
   (`AZARIAH_SCREENSHOTS=<dir> dotnet test tests/Azariah.App.Tests`), critique, iterate.
3. Only when Files looks right: Home, Setup, Roblox, dialogs, Settings.
4. Then the command surface (Ctrl+Space) with local results (places, files, commands).

## Open decisions (ask the owner)

1. Typography: A, B or C (after seeing renders).
2. Navigation: top-bar text nav (above), a thin left text rail, or command-surface only.
3. Default theme: light paper, black, or follow Windows.
4. Home's big element: last project you touched (e.g. "OBBY RUSH", click to resume), the
   drive name, or nothing big at all.
5. Accent color: none for now, or pick one later.

## Avalonia notes

- Custom title bar in Avalonia 12: `ExtendClientAreaToDecorationsHint="True"` plus a region
  tagged `WindowDecorationProperties.ElementRole="TitleBar"`. The default drawn decorations
  add their own title text and a fullscreen button; replace them via `WindowDecorationsTheme`
  or `WindowDecorations="BorderOnly"` with our own caption buttons
  (`ElementRole` Minimize/Maximize/Close). Test on real Windows before shipping.
- Theme brushes via `DynamicResource` + `ThemeDictionaries` (already set up).
- Keep FluentTheme as the base; restyle with selectors, don't rebuild control templates.
