# Convenience Empire: Kickoff Prompt

You're helping me build **Convenience Empire**, a Roblox game where you start by working the register at one tiny corner store and grow it into a worldwide brand. I have Roblox Studio open. We're reusing the map from our TCG shop game: a city map where up to 6 players each get their own plot. It already looks great, so this is a revamp, not a blank baseplate.

## The pitch
Each player claims a plot in a shared city and starts with one run-down corner store with a homemade sign. You're the only worker: you stock shelves, unpack deliveries, work the register and survive rush hour. As you earn, you hire help until the store runs itself. Then you grow: expand the flagship on your plot and open new locations. Eventually your logo is on storefronts, billboards and delivery trucks all over the city, next to the other 5 players' brands. The fantasy is watching something tiny you built become a brand that's everywhere.

## How progression works
1. **Solo shift.** You do everything. The store has open and closed hours on a shared city clock. Open means customers. Closed means restock, order and buy upgrades.
2. **First hire.** Once you've earned enough, you hire a cashier or a stocker (the player picks which one first). They're visible NPCs doing the job, paid a daily wage.
3. **Full crew.** Hire the other role, then a manager who reorders stock automatically. With all three covered, the store is "automated" and runs without you. That's the big milestone.
4. **Expansion.** Automation unlocks two growth tracks: grow the flagship on your plot (bigger floor, parking lot, gas pumps, food counter) and open new locations.
5. **Brand in the city.** New locations show up around the shared city as storefronts, billboards and delivery trucks with your logo.

## Design pillars
1. **Your first store is the blueprint.** Whatever the player sets up in store #1 (layout, products, prices) is what every future location copies. The hands-on part is R&D, not a tutorial you outgrow.
2. **Each tier adds a new problem, not just bigger numbers.** Store 1 is you vs. the clock. The first hire is "you can't be in two places." Later tiers bring consistency, logistics, competition and localization.
3. **Branding glow-up.** The sign starts homemade and ugly. A rebrand later is a big payoff moment.
4. **Signature product.** Eventually the player invents one item their brand is known for.
5. **You can always drop in.** Even when the store is automated, the player can jump behind the register or stock shelves and it still helps.
6. **Store hours are progression.** Start with short hours, unlock longer ones, end at 24/7. (7-Eleven is named after its original 7am to 11pm hours.)

## This session: the starter store, from solo shift to first hires
Goal: progression steps 1 and 2, playable in a 6-player server. Do NOT build the manager, automation, expansions or new locations yet. But build so they're easy to add later (see Architecture and Future).

### Step 1: Audit the TCG place first
Before building anything:
- Make sure we're working in a copy, not the live TCG game. If this is the original TCG place, stop and tell me so I can duplicate it. Never delete or edit TCG stuff in the original.
- Look through Workspace, ServerScriptService, ReplicatedStorage, ServerStorage, StarterGui and StarterPlayer and list what's there.
- Figure out exactly how the TCG plot system works: how many plots, how a player gets assigned one on join, how their stuff spawns onto it, how it's cleaned up when they leave, and how it saves. We're reusing this for the 6-player setup.
- Break down the TCG NPC system in detail too (see the NPCs section below). That's the part we worked hardest on, so it gets its own writeup in the audit.
- Sort everything into three buckets:
  - **Reuse as-is:** map, terrain, lighting, roads, general buildings and props, the plot system.
  - **Adapt/reskin:** shelves, register, customer NPCs, money, save system, UI.
  - **Cut:** card packs, trading, anything TCG-specific.
- Check whether the project uses Rojo or a Git repo for scripts. If it does, follow its folder structure. If not, work directly in Studio.
- Show me the audit before moving on.

### Step 2: Get studs down (graybox the starter store)
Build the starter store on one plot. The map's look is the reason we're reusing it, so fit the store into it without wrecking what's there.
- The starter store should be small and only take up part of the plot. Leave the rest open for future expansions (bigger floor, parking lot, gas pumps). Mark that space with transparent, non-collidable parts in an `ExpansionZones` folder so we don't build over it.
- **Front:** entrance, windows, a homemade-looking sign (a plain part with text is fine for now), an Open/Closed sign on the door.
- **Floor:** 3 to 4 aisles of shelves, a wall of drink coolers, a small hot food spot (roller grill and coffee), a register counter by the door.
- **Back room:** stockroom with space for boxes, a back door, and a delivery drop spot outside it.

Keep everything anchored and sized like a real small store (not huge). Organize it as one Model called `StoreTemplate` in ServerStorage with folders: `Building`, `Shelves`, `Coolers`, `HotFood`, `Register`, `Stockroom`, `DeliveryZone`, `Sign`, `ExpansionZones`. When a player joins, clone it onto their plot with PivotTo. It has to work on any of the 6 plots. Graybox first with simple parts and colors. Detail comes later.

### Step 3: Core loop
Build these in order and playtest after each one:
1. **Products.** One ModuleScript catalog in ReplicatedStorage. Each product has: id, name, wholesale cost, default price, shelf type (shelf, cooler, hot food), and whether it spoils plus its shelf life. Start with about 10: chips, candy bar, gum, soda, water, energy drink, milk, bread, hot dog, coffee.
2. **City clock and store hours.** One shared day/night clock for the whole server, so all stores open and close together and lighting/streetlights change with it. Each store has hours (start around 8am to 6pm game time). The door sign flips between Open and Closed, and customers only come while open. Put day length and hours in a Config module. Start around 10 real minutes per game day, with a short closed stretch.
3. **Deliveries.** The player orders stock from a simple UI. Cost comes out when they order. Boxes show up at their delivery zone after a short delay.
4. **Stocking.** The player picks up a box, carries it and fills a matching shelf slot. Shelves visibly show how full they are.
5. **Customers.** Built on the NPC system ported from the TCG game (see the NPCs section). Each store spawns its own customers from the street near its plot, so players can't steal each other's customers. NPCs walk in, browse, grab items from stocked shelves and line up at the register. If what they want is out of stock (or overpriced, once pricing exists) they leave annoyed.
6. **Register.** The player scans items and takes payment. Cash goes up. Make it quick and satisfying with sounds and a small cash popup.
7. **Pricing.** The player can set prices. Higher price means more profit per item, but more customers walk.
8. **End of day.** At closing time, show a summary: revenue, costs, wages, profit, customers lost, items spoiled.
9. **Saving.** DataStoreService saves cash, stock, prices, day count and staff. Wrap calls in pcall with retries. Save on PlayerRemoving and in game:BindToClose.

### Step 4: First hires
- A hire menu with two roles: **Cashier** and **Stocker**. The player picks which one to hire first. Each costs an upfront fee plus a daily wage paid at close.
- Staff use the same NPC base as customers, just with a different behavior module.
- The cashier NPC stands at the register and checks out customers, a bit slower than a player at level 1.
- The stocker NPC takes boxes from the stockroom and fills the emptiest matching shelves.
- Staff have a level the player can upgrade for speed.
- If the player walks up to the register while the cashier is there, the player takes over and the cashier goes idle or helps stock.
- If the player can't cover wages at close, warn them clearly. Money never goes negative without the player knowing why.

## NPCs: carry over what we learned in the TCG game
The TCG game already has working shop NPCs, and we learned a lot getting them right. Don't rebuild them from scratch.
- In the audit, find the TCG NPC system and break it down for me: how NPCs spawn, how they path to shelves and the register, how they pick what to buy, how the checkout line works, and how they despawn. Call out every fix or workaround in the code (stuck checks, retry loops, comments explaining a bug). Those workarounds are the lessons, so they come with us.
- Port that system and adapt it instead of rewriting it. Browsing card packs becomes browsing shelves, coolers and hot food. The TCG checkout becomes the register line.
- Customers and staff share one NPC base (spawning, pathing, animation, cleanup) with a behavior module on top: `CustomerBehavior`, `CashierBehavior`, `StockerBehavior`. Future roles (manager, shoplifter) plug in the same way.
- If the TCG system doesn't already handle these, add them. They're the usual Roblox NPC problems:
  - The server owns NPC physics: call SetNetworkOwner(nil) on each NPC's root part so they don't stutter.
  - Humanoid:MoveTo gives up after 8 seconds, so long walks go waypoint by waypoint or re-issue MoveTo.
  - Recompute the path when Path.Blocked fires.
  - Stuck detection: if an NPC makes no progress for a few seconds, recompute its path. If that fails, nudge it to the next waypoint or despawn it.
  - Collision groups so NPCs don't block each other, doorways or players.
  - The checkout line uses fixed queue spots that move up as people get served.
  - Every customer has a patience timer. If it runs out in line or they can't find what they want, they leave unhappy, and that counts toward "customers lost" in the end-of-day summary.
  - Pool NPC models and reuse them instead of creating and destroying them all day.

## Future (don't build yet, but don't block it)
- **Manager** who auto-reorders stock. Cashier + stocker + manager = automated store, which unlocks expansion and offline earnings (capped) while the player is away.
- **Store hours upgrades** up to 24/7.
- **Flagship expansions** that fill the `ExpansionZones` on the plot.
- **New locations.** You can't physically fit a bunch of stores per player on a 6-player map, so extra locations are managed from a city map screen (office computer or phone). Each one is a copy of the player's blueprint store with its own stats. They show up in the shared city as storefronts, billboards and delivery trucks with the player's logo.
- **Branding:** a sign/logo editor and a rebrand event.
- **Signature product.**
- **Store events:** shoplifters, spills and messes, broken coolers, expired food. Shoplifters and other event NPCs use the same NPC base.

## Architecture rules
- **The server owns money.** Cash, stock, wages and sales only change on the server. Clients send requests through RemoteEvents/RemoteFunctions, and the server validates every one (can they afford it, are they close enough to the shelf, is it their store, does the product exist).
- One ModuleScript per system in ServerScriptService: PlotService, ClockService, EconomyService, InventoryService, DeliveryService, NpcService, CustomerService, RegisterService, StaffService, DataService. NpcService is the shared NPC base; CustomerService and StaffService drive behaviors on top of it. Shared config and the product catalog go in ReplicatedStorage. All remotes live in one `Remotes` folder.
- Use CollectionService tags (`Shelf`, `Cooler`, `HotFood`, `Register`, `DeliveryZone`) and Attributes (product type, capacity) instead of hardcoded paths. Every system finds its parts by tag inside a store model, so any store is just a clone of the template. That's the foundation for the blueprint system and new locations later.
- Store logic never assumes a world position. Everything is relative to the plot.
- Key per-store data by the owner's UserId plus a `StoreId` attribute (the starter store is `"flagship"`). Save stores as a list even though there's only one right now.
- Use Luau type annotations where they're easy. Clear names. Short comments only where the why isn't obvious.

## Roblox rules
- Keep the product list Roblox-safe: no alcohol, tobacco, vapes or lottery tickets. Energy drinks and coffee are fine.
- Watch performance: 6 stores' worth of customers and staff adds up. Cap NPCs per store and per server.
- When a player leaves, save first, then remove their store and NPCs and free the plot.

## How to work with me
- Small steps. After each step, tell me what you built, where it lives and how to test it in Play mode (use a multi-player local test for plot stuff).
- If something in the TCG place is unclear and you'd have to guess, ask me instead.
- Don't polish visuals yet. Function first, then we make it pretty.
- End the session with: what works, what's stubbed, known bugs, and the next 3 things to build.
