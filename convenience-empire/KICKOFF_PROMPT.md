# Convenience Empire: Kickoff Prompt

You're helping me build **Convenience Empire**, a Roblox game where you start with one tiny corner store you run with your own hands and grow it into a worldwide brand. I have Roblox Studio open. We're reusing the map from our TCG shop game, which already looks great, so this is a revamp, not a blank baseplate.

## The pitch
You start with one run-down corner store with a homemade sign. You stock shelves, unpack deliveries, work the register and survive rush hour. Over time you hire staff, open more stores, build a warehouse, and eventually your logo is on trucks, billboards and stores around the world. The fantasy is watching something tiny you built become a brand that's everywhere.

## Design pillars (keep these in mind even though we're only building the start)
1. **Your first store is the blueprint.** Whatever the player sets up in store #1 (layout, products, prices) is what every future store copies. The hands-on part is R&D, not a tutorial you outgrow.
2. **Each tier adds a new problem, not just bigger numbers.** Store 1 is you vs. the clock. Store 2 is "you can't be in two places." Later tiers bring consistency, logistics, competition and localization.
3. **Branding glow-up.** The sign starts homemade and ugly. A rebrand later is a big payoff moment.
4. **Signature product.** Eventually the player invents one item their brand is known for.
5. **You can always drop in.** Even with 50 stores, you can walk into any one and work a shift.

## This session: first store only
Goal: a playable corner store where the core loop works. Do NOT build the world map, warehouses, multiple stores or franchising yet. But build store 1 so cloning it later is easy (see Architecture).

### Step 1: Audit the TCG place first
Before building anything:
- Make sure we're working in a copy, not the live TCG game. If this is the original TCG place, stop and tell me so I can duplicate it. Never delete or edit TCG stuff in the original.
- Look through Workspace, ServerScriptService, ReplicatedStorage, StarterGui and StarterPlayer and list what's there.
- Sort everything into three buckets:
  - **Reuse as-is:** map, terrain, lighting, roads, general buildings and props.
  - **Adapt/reskin:** shelves, register, customer NPCs, money, save system, UI.
  - **Cut:** card packs, trading, anything TCG-specific.
- Check whether the project uses Rojo or a Git repo for scripts. If it does, follow its folder structure. If not, work directly in Studio.
- Show me the audit before moving on.

### Step 2: Get studs down (graybox the store)
Pick the building or spot on the TCG map that works best for a small corner store (ideally a street corner with room out back for deliveries). The map's look is the reason we're reusing it, so fit the store into it without wrecking what's there. Block out:
- **Front:** entrance, windows, a homemade-looking sign (a plain part with text is fine for now).
- **Floor:** 3 to 4 aisles of shelves, a wall of drink coolers, a small hot food spot (roller grill and coffee), a register counter by the door.
- **Back room:** stockroom with space for boxes, a back door, and a delivery drop spot outside it.

Keep everything anchored and sized like a real small store (not huge). Organize it as one Model called `Store_Flagship` with folders: `Building`, `Shelves`, `Coolers`, `HotFood`, `Register`, `Stockroom`, `DeliveryZone`, `Sign`. Graybox first with simple parts and colors. Detail comes later.

### Step 3: Core loop
Build these in order and playtest after each one:
1. **Products.** One ModuleScript catalog in ReplicatedStorage. Each product has: id, name, wholesale cost, default price, shelf type (shelf, cooler, hot food), and whether it spoils plus its shelf life. Start with about 10: chips, candy bar, gum, soda, water, energy drink, milk, bread, hot dog, coffee.
2. **Deliveries.** The player orders stock from a simple UI. Cost comes out when they order. Boxes show up at the delivery zone after a short delay.
3. **Stocking.** The player picks up a box, carries it and fills a matching shelf slot. Shelves visibly show how full they are.
4. **Customers.** NPCs walk in, browse, grab items from stocked shelves and line up at the register. If what they want is out of stock (or overpriced, once pricing exists) they leave annoyed.
5. **Register.** The player scans items and takes payment. Cash goes up. Make it quick and satisfying with sounds and a small cash popup.
6. **Pricing.** The player can set prices. Higher price means more profit per item, but more customers walk.
7. **Day cycle.** Open, a rush hour spike, close. End-of-day summary: revenue, costs, profit, customers lost, items spoiled.
8. **Saving.** DataStoreService saves cash, stock, prices and day count. Wrap calls in pcall with retries. Save on PlayerRemoving and in game:BindToClose.

### Step 4: First hire (stretch goal)
If the loop above works, add hiring one cashier NPC who runs the register when the player isn't at it, for a daily wage. This is the first "you can't do everything yourself" moment, so it should feel like a big deal.

## Architecture rules
- **The server owns money.** Cash, stock and sales only change on the server. Clients send requests through RemoteEvents/RemoteFunctions, and the server validates every one (can they afford it, are they close enough to the shelf, does the product exist).
- One ModuleScript per system in ServerScriptService: EconomyService, InventoryService, DeliveryService, CustomerService, RegisterService, DayCycleService, DataService. Shared config and the product catalog go in ReplicatedStorage. All remotes live in one `Remotes` folder.
- Use CollectionService tags (`Shelf`, `Cooler`, `HotFood`, `Register`, `DeliveryZone`) and Attributes (product type, capacity) instead of hardcoded paths. Every system should find its parts by tag inside a store model, so a second store is just a clone of the model. That's the foundation for the blueprint system later.
- Give the store model a `StoreId` attribute and key all per-store data by it, even though there's only one store right now.
- Use Luau type annotations where they're easy. Clear names. Short comments only where the why isn't obvious.

## Roblox rules
- Keep the product list Roblox-safe: no alcohol, tobacco, vapes or lottery tickets. Energy drinks and coffee are fine.
- Watch performance: cap the NPC count, use PathfindingService with simple waypoints, and clean up NPCs when they leave.

## How to work with me
- Small steps. After each step, tell me what you built, where it lives and how to test it in Play mode.
- If something in the TCG place is unclear and you'd have to guess, ask me instead.
- Don't polish visuals yet. Function first, then we make it pretty.
- End the session with: what works, what's stubbed, known bugs, and the next 3 things to build.
