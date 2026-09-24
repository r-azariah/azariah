# azariah

## Say where you're running, first thing
- At the start of every session, tell the user whether you're running locally or in the cloud. Don't wait until something fails.
- The user doesn't use cloud sessions. If you're in the cloud, say so right away and help them move to a local session (see `convenience-empire/LOCAL_HANDOFF.md`). Don't steer them toward cloud workflows.

Personal repo. Current project: **Convenience Empire**, a Roblox game. Everything for it lives in `convenience-empire/` (start with its README).

## The user's machines

- **"The laptop"**: what the user carries to class. When they talk to a cloud session from the Claude app on it, they call that session "the laptop."
- **"The desktop"**: the PC in their room at home. The "Roblox development." Claude session runs on it, with a live connection to Roblox Studio.
- Don't mix up "the desktop" (the home PC) with the Claude Desktop app.

## What can reach Studio

- Only a Claude session that actually runs on the machine with Studio open, and has the Studio connection set up, can see or edit Studio. Today that's the desktop session.
- Cloud sessions (including ones opened from the laptop's Claude app) run in a container and can't see Studio or the MCP server, even when both are open on the laptop.
- The TCG shop game isn't in this repo. It only exists in Studio.
