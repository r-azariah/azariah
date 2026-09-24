# Convenience Empire: handoff to a local session

Start a new chat in the Claude app, pick **local**, and paste everything below the line.

---

# Handoff: Convenience Empire

**First thing: tell me whether you're running locally or in the cloud.** If it's the cloud, stop and say so. I don't use cloud sessions, and the last session didn't tell me until way too late.

You're picking up from a cloud session that planned this game with me and built the first pieces. It couldn't reach Studio, which is why you're taking over. Everything we decided is below so you don't have to rediscover it.

## About me and how I work
- I talk casually. Talk back the same way, like we're mutuals. No corporate jargon, no em dashes, and keep open-ended questions to a minimum. Give me a recommendation, not a list of options.
- If there's a simple way to do something, lead with it. Don't make me do extra steps.
- When I say "pause," stop and wait. When I say "continue," pick up where we left off.
- **My machines:** "the laptop" is what I bring to class (I'm on it now). "The desktop" is the PC in my room at home. The desktop runs a Claude session called "Roblox development." that has the history from our TCG game. Don't confuse my desktop (the PC) with the Claude Desktop app.
- Small steps. After each one, tell me what you built, where it lives and how to test it in Play mode.
- If something in the TCG place is unclear and you'd have to guess, ask me.
- Function first, pretty later.
- Commit to the branch `claude/magical-cerf-8m9sox`. It already has a pull request (https://github.com/r-azariah/azariah/pull/1), and pushing to the branch updates it. Don't open another one.

## Get the repo
If it isn't on the laptop yet:
```
git clone https://github.com/r-azariah/azariah.git
cd azariah
git fetch origin claude/magical-cerf-8m9sox
git checkout claude/magical-cerf-8m9sox
```
Files worth reading: `CLAUDE.md`, `convenience-empire/README.md` and `convenience-empire/KICKOFF_PROMPT.md` (the full build plan).

## Where things are right now
- Roblox Studio is open on the laptop on a **baseplate** (not the TCG place yet), and the **Roblox Studio MCP server is running**.
- Nothing has been built in Studio yet.
- The TCG shop game isn't in the repo. It only exists in Studio. We just finished its city map and it looks great, which is a big reason we're reusing it.

## The game
**Convenience Empire** is a Roblox game in the same family as our TCG shop game, but with a much bigger progression fantasy. You start with one run-down corner store and a homemade sign, and you grow it into a brand that's everywhere.

The original pitch (from Codex, and we kept the core of it):
- At the start you physically run the store yourself: stock shelves, unpack deliveries, clean up, work the register, deal with shoplifters, rush hours, expired food and broken coolers.
- As you grow you stop doing everything yourself and hire cashiers, stockers, managers, delivery drivers and eventually regional managers.
- The long ladder: small store, bigger location, second location, local chain, distribution warehouse, regional brand, national brand, international, then airports, gas stations, malls and downtown spots, and eventually your own factories and branded products.
- Later it becomes more about logistics and business decisions: what to stock, shelf space, pricing, layouts, which neighborhoods to expand into, cheap stores everywhere vs nicer stores with better margins.
- The visual payoff: the sign starts homemade, the branding gets cleaner over time, and eventually your logo is on trucks, warehouses, billboards and stores all over the map.
- The real fantasy isn't "own a store." It's watching something tiny you built become a brand that exists everywhere.

## Design decisions we made (and why)
1. **The hard part is the middle.** Running one store by hand and running a chain are two different games. Most games that try both end up with a great first few hours and then a spreadsheet. Our answer is the next point.
2. **Your first store is the blueprint.** Whatever the player sets up by hand in store #1 (layout, products, prices) is what every future location copies. The hands-on part is R&D, not a tutorial you outgrow. The player goes back to the flagship to test a new layout or product, and if it works, it goes out to the chain.
3. **You can always drop in.** Even with a bunch of stores, you can jump behind the register or stock shelves at any of them, and it still helps.
4. **Each tier adds a new problem, not just a bigger number.** Store 1 is you vs. the clock. The first hire is "you can't be in two places." A chain is about consistency. The warehouse is logistics. National means rival chains and real estate. International means localization: 7-Eleven sells onigiri in Japan and Slurpees in the US.
5. **Store hours are progression.** You start with short hours, unlock longer ones and end at 24/7. Fun fact we can use: 7-Eleven is named after its original 7am to 11pm hours.
6. **Hiring order is the player's choice.** Cashier or stocker first. Most will pick cashier since the register backs up during rush hour, but letting them choose makes it their first real decision. Staff are visible NPCs doing the job, and watching someone else ring people up is the payoff.
7. **The automated store is the big milestone.** Cashier plus stocker isn't enough because someone still has to order stock, so there's a manager who reorders automatically. With all three hired, the store runs itself. That unlocks expanding and offline earnings (capped), and offline earnings are a big reason people come back to Roblox tycoons.
8. **Keep the 6-player city map.** Each player gets a plot, like the TCG game. You can't fit a bunch of stores per player on one map, so growth has two tracks:
   - The flagship grows on your plot: bigger floor, parking lot, gas pumps, food counter. The starter store only takes part of the plot so there's room.
   - New locations live on a city map screen (office computer or phone). You manage them instead of walking them, but they show up in the shared city as storefronts, billboards and delivery trucks with your logo. The city slowly fills up with all 6 players' brands, and seeing your truck drive past someone else's store is the flex.
9. **Customers are per-store.** Each store gets its own customers from the street near its plot, so nobody can steal anyone's customers. Fighting over shoppers could come back later as an event, but one strong player shouldn't be able to starve the other 5.
10. **One shared clock for the server.** All stores open and close together and the streetlights come on at night. Closed time is for restocking, ordering and upgrades, so it's never dead time. The business window runs at normal speed and the night fast-forwards, about 10 real minutes per game day, because Roblox sessions are short.
11. **Franchise vs corporate stores (later).** That's how 7-Eleven actually grew. Franchising is fast and cheap, but some franchisees cut corners and hurt your brand rating. Corporate stores are slow and expensive, but you control everything.
12. **Signature product (later).** Every big convenience store brand has one thing: the Slurpee, Wawa hoagies, Buc-ee's brisket. The player invents theirs and it spreads with the brand.
13. **Branding glow-up.** The sign starts ugly and homemade. A rebrand later is a big payoff moment, and the before/after screenshots are free marketing.
14. **Reuse the TCG game, don't rebuild it.** The map, the plot system and especially the NPC system. We worked hard to get the NPCs right in the TCG game, so port that system and its fixes instead of rewriting it. The desktop's "Roblox development." session knows that history if you need it.
15. **Roblox-safe products only.** No alcohol, tobacco, vapes or lottery tickets, even though real convenience stores sell all of them. Energy drinks and coffee are fine.
16. **Scope.** Build the first store and the first hires before anything else. If going from "I do everything" to "I have staff and just drop in" feels good, the rest of the ladder is content. Don't build the world map first, even though it makes the coolest screenshot.

Market note: "Convenience Empire" wasn't taken on Steam when we checked, but this is a Roblox game, and Roblox tycoons are their own crowded genre. Our angle is doing both the hands-on store and the empire well.

## What's already built (in the repo, tested outside Studio, never run in real Studio)
**`convenience-empire/studio/BuildStoreTemplate.luau`:** a Command Bar script that builds the whole graybox starter store. `convenience-empire/docs/store-layout.png` shows it from above.
- The store is 44 x 44 studs with 14-stud walls. The front wall has a big window, an 8-stud entrance and a solid piece by the register.
- The sales floor has 3 double-sided aisles (12 shelf slots), 4 drink coolers along the stockroom wall, a hot food counter with a coffee station and roller grill, and a register counter by the door with a cashier spot and a 5-spot checkout line.
- The stockroom has a box area, a rack and a staff spawn, connected to the sales floor by a doorway behind the register. A back door leads out to the delivery pad.
- A crooked homemade "CORNER STORE" sign in a marker font, and an Open/Closed sign on the door (starts as "CLOSED").
- Expansion zones mark the space saved on the plot: side (bigger floor, parking, gas pumps) and rear (parking, loading dock, warehouse).
- Tags: `Shelf`, `Cooler`, `HotFood`, `Register`, `CashierSpot`, `QueueSpot`, `Stockroom`, `DeliveryZone`, `OpenSign`, `CustomerSpawn`, `EntrancePoint`, `StaffSpawn`, `Marker`.
- Slot attributes: `ShelfType`, `Capacity`, `ProductId`. Shelf and cooler slots start with `ProductId = ""`, and the first product the player stocks claims the slot, so their layout becomes the blueprint. The coffee station and roller grill are fixed to coffee and hot dogs.
- Every slot has a `StandPoint` attachment where an NPC stands to use it.
- Everything is in one Model called `StoreTemplate`, with an invisible `Origin` part as the pivot (plot center at ground level). It builds in Workspace so we can look at it, then moves to ServerStorage, and PlotService clones it onto each plot with PivotTo.
- With a plot's floor part selected, it builds on that plot and sizes the expansion zones to it. With nothing selected, or anything wider than 300 studs like the Baseplate, it builds at the camera focus on an 80x80 plot. `ROTATION_DEGREES` at the top spins it if the front doesn't face the street.
- Graybox settings: the roof is see-through (0.6) and markers are half transparent. Hide everything tagged `Marker` at runtime.
- It needs about a 48 x 60 stud plot. On smaller plots it warns and skips any expansion zone that doesn't fit.
- `CustomerSpawn` is a placeholder. Move it to wherever the TCG game's customers come in from the street.

**`convenience-empire/src/shared/Config.luau`:** first-guess balance numbers.
- Business window 8:00 to 18:00 takes 480 real seconds, and the night takes 120.
- Starting cash is 250 and deliveries take 20 seconds.
- Customers: up to 6 per store and 30 per server, spawning every 6 to 14 seconds. Rush hours at 12 and 17 double the spawn rate. Patience is 45 seconds, each visit buys 1 to 4 items, and customers walk if a price is more than 25% over default.
- Cashier and stocker each cost 300 to hire and 40 a day. Staff go up to level 5, each level is 10% faster, and going from level n to n+1 costs n x 150.

**`convenience-empire/src/shared/ProductCatalog.luau`:** the 10 starter products with cost, price, units per box, shelf life and popularity.
- Shelf: chips 1.00 to 2.00, candy bar 0.60 to 1.50, gum 0.40 to 1.00, bread 1.50 to 3.00 (spoils in 48h).
- Cooler: soda 0.80 to 2.00, water 0.40 to 1.50, energy drink 1.50 to 3.50, milk 1.80 to 3.50 (spoils in 72h).
- Hot food: hot dog 0.80 to 2.50 (spoils in 4h), coffee 0.30 to 1.75 (spoils in 2h).

**`convenience-empire/tests/run.sh`:** needs the Luau CLI. It type-checks the shared modules, sanity-checks the catalog and config, and runs the builder against a fake Roblox API. That confirms nothing overlaps, everything fits on the plot, and NPCs can reach every spot: customers only through the front door, staff from the stockroom, deliveries through the back door. We broke the layout on purpose four ways (bricked entrance, blocked stockroom door, blocked back door, aisles into the coolers), and it caught every one.

## Do this now
1. Tell me local or cloud. Then confirm you can see Studio through the MCP and tell me what place is open.
2. Run `BuildStoreTemplate.luau` in Studio on the baseplate. It should make one `StoreTemplate` model in Workspace with about 61 parts, with the store front facing -Z.
3. Check it for real: parts where the floor plan says, the signs readable from the street side, tags present. Tell me what you see.
4. Fix anything that breaks in real Studio, since it was only tested against a mock. If you change the builder or the shared modules, rerun `tests/run.sh` and commit.
5. Then follow `KICKOFF_PROMPT.md` from Step 1: audit the TCG place (plot system, NPC system, what to reuse, adapt or cut), then the core loop (clock and hours, deliveries, stocking, customers, register, pricing, end-of-day summary, saving), then first hires. The audit needs the TCG place open, and it has to be a copy, never the original. Tell me when you need me to open it.

## Still unknown (figure these out in the TCG audit)
- How big the TCG plots are, and whether the store fits or needs resizing.
- Which way the street is on each plot, for `ROTATION_DEGREES`.
- Whether the TCG project uses Rojo or lives only in Studio.
- How the TCG NPC system spawns, paths, picks items, queues and despawns, and which bug fixes are in it.
- Where TCG customers enter from, for `CustomerSpawn`.
- Whether the balance numbers feel right. They're all guesses until we playtest.
