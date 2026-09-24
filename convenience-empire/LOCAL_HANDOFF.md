# Convenience Empire: handoff to a local session

Paste everything below the line into a Claude session running locally on the laptop, with the Roblox Studio MCP connected.

---

We're building **Convenience Empire**, a Roblox game: you start by working the register at one tiny corner store and grow it into a worldwide brand. You should be running locally on my laptop with the Roblox Studio MCP connected. Studio is open on a baseplate.

**First, tell me whether you're running locally or in the cloud.** If it's the cloud, stop and say so. I don't use cloud sessions.

## Get the repo
If it isn't already here:
```
git clone https://github.com/r-azariah/azariah.git
cd azariah
```
Then:
```
git fetch origin claude/magical-cerf-8m9sox
git checkout claude/magical-cerf-8m9sox
```

Read these before doing anything else:
- `CLAUDE.md`: my machines (laptop vs desktop) and how I like to work
- `convenience-empire/README.md`: what's built and how it fits together
- `convenience-empire/KICKOFF_PROMPT.md`: the full design and build plan

## What's already done
A cloud session built these and tested them outside Studio against a fake Roblox API. None of it has run in real Studio yet.
- `convenience-empire/studio/BuildStoreTemplate.luau`: builds the graybox starter store (aisles, coolers, hot food, register and checkout line, stockroom, delivery pad, homemade sign, expansion zones) with all the tags, slot attributes and NPC stand points the plan needs. `docs/store-layout.png` shows it from above.
- `convenience-empire/src/shared/Config.luau` and `ProductCatalog.luau`: balance numbers and the 10 starter products.
- `convenience-empire/tests/run.sh`: layout and catalog checks (needs the Luau CLI).

## Do this now
1. Confirm you can see Studio through the MCP and tell me what place is open.
2. Run `BuildStoreTemplate.luau` in Studio on the baseplate. Leave nothing selected or select the Baseplate (it ignores anything wider than 300 studs) and aim the camera near the origin. It should make one Model called `StoreTemplate` in Workspace, about 61 parts, with the store front facing -Z.
3. Check it for real: parts where the floor plan says they are, the signs readable from the street side, tags on the shelf, cooler, hot food, register and spot parts. Tell me what you see.
4. Fix anything that breaks in real Studio. The builder was only tested against a mock. If you change the builder or the shared modules, rerun `tests/run.sh` and commit to the same branch.
5. Then follow `KICKOFF_PROMPT.md` from Step 1. The TCG audit needs the TCG place open, and it has to be a copy, never the original. Tell me when you need me to open it.

## Heads up
- The TCG shop game isn't in the repo. It only exists in Studio. Its plot system and NPC system are what we reuse, so the audit matters.
- My desktop at home has a separate session called "Roblox development." that has the TCG history. Don't confuse my desktop (the PC) with the Claude Desktop app.
