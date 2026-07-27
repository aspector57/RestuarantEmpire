# Restaurant Empire Successor

A restaurant management/tycoon game. Full design rationale lives in `docs/design.md` — read the relevant section before implementing anything, but do not load the whole document unless you need to.

**Genre framing (this governs every judgment call):** this is a **tycoon/empire management game, cooking-themed** — not a cooking game. The player is a chef who opens one restaurant on a tight budget and builds it into an empire. Business and strategic decisions carry more weight than culinary execution.

---

## Current milestone: M0 — Headless Simulation

**No graphics. No engine. Plain C# with zero engine dependencies, validated by unit tests and logs.**

M0 exists to prove the core math and the one architectural claim the entire design rests on — supplier policy propagation — before any rendering work begins.

### In scope for M0

- **Company/Empire → Restaurant hierarchy.** Every Restaurant belongs to a Company parent from day one, even when there is exactly one restaurant. Non-negotiable; retrofitting this later is a painful migration.
- **Time** — ticks, day/week/month advancement, speed multipliers as data only.
- **Economy** — cash, revenue, COGS, labor cost, live prime-cost percentage.
- **Ingredients** — stock, par levels, spoilage.
- **Suppliers** — the full propagation contract (see Architecture Rules below).
- **Recipes** — live contribution-margin calculation, Kasavana-Smith classification (Star / Plowhorse / Puzzle / Dog).
- **Kitchen** — station throughput math only, no visuals.
- **Customers** — a basic arrival and satisfaction formula.
- **Save/load** — see Architecture Rules.

### Explicitly NOT in scope for M0

Rendering, UI, layout/build mode, the Advisor, Events, Competitors, Marketing, hiring profiles, promotion ladders, General Managers, R&D, Power Rankings, Regulars. These belong to M1–M5. **Do not build ahead.** Scope discipline is the single largest risk to this project — the design review identified building late-milestone content before earlier milestones are validated as the most likely way this fails.

### M0 exit tests (both must pass before M1)

1. Switching a Supplier assignment updates every dependent Recipe's contribution margin with **zero manual edits**.
2. A new Recipe or Furniture object can be added **purely by writing a data file**, with no engine/code changes.

**Status: M0 SCOPE COMPLETE — both exit tests pass.** Company→Restaurant hierarchy · Suppliers and Pricing, both resolving up a Company→Restaurant inheritance chain · Ingredients with par levels · Recipes with live contribution margin · Kasavana-Smith classification · JSON content loading · Time (`GameClock`) · Kitchen throughput (brigade stations, queueing, 86'ing) · Customers (arrival curve, satisfaction formula), joined by a headless `ServiceSimulation` · Economy (append-only ledger, live prime cost) · save/load with version stamps and graceful degradation.

**Two things deferred out of M0, deliberately, both because they need the game loop rather than the core:** the autosave *policy* (rolling slots, prompt-on-exit, autosave after a long jump-ahead) — the format and degradation are built, the scheduling is not; and labour cost *generation* — Economy tracks it, but nothing produces it until Employees arrive at M1, so prime cost is only as complete as the labour figure booked against it.

**Caveat on prime cost:** Economy *tracks* labour cost, but nothing *generates* it yet — Employees arrive at M1. Until then a labour figure has to be booked by the caller, so prime cost is only as honest as what gets recorded against it.

**Simulation determinism is a hard requirement.** Use `DeterministicRandom`, never `System.Random` — the runtime does not guarantee `System.Random`'s sequence across versions or platforms, and tests, save/load, and post-hoc explanation of a night all depend on the same seed always producing the same service.

---

## Current milestone: M2 — Depth (Advisor, menu engineering, full employees)

Aaron's call: build the Advisor and the systems around it BEFORE graphics, since graphics
would only make bad interrupts prettier. M1's simulation half is done and its mechanism bar
passes; M1's *presentation* half (Unity, build mode, dashboard UI) is deferred until the
systems underneath are worth looking at.

### Built so far in M2

**Featured menu slots.** Two by default. `Menu.Feature` returns whatever it displaced,
because "what did this cost me?" is the mechanic — scarcity is the only reason promoting a
dish is a decision.

**The Advisor**, with the three-tier authority model intact:

- **Chore** — stated flatly, never asked. Restocking, unstaffed equipment, unservable tables.
- **Proposal** — a question with its reasoning visible, and ONLY where "no" is defensible.
- **Strategic** — named, never proposed. The player initiates.

The line it must not cross is pinned by tests: it may say *"the risotto earns most of
anything we sell and nobody orders it — want it featured?"*, but never *"this is a Puzzle"*.
Same information, entirely different relationship, and only the first leaves the player
running the restaurant. A test asserts the words "puzzle", "plowhorse" and "kasavana" never
appear in anything it says.

It also surfaces opportunities, not only problems, and is capable of saying nothing at all —
there is a test for a healthy restaurant getting no advice, because an Advisor that always
has an opinion stops being read.

### Found while building it: the popularity axis is currently degenerate

Guests pick dishes **uniformly at random**, so with four dishes every dish lands near a 25%
share — comfortably above the 17.5% popularity bar. A Puzzle (high margin, LOW volume)
therefore cannot arise naturally; only featuring something else pushes the others under.

That makes half of the Kasavana-Smith matrix decorative until dishes have different natural
appeal. The design already has the answer scheduled here at M2: **Customer archetypes**, who
want different things. Until that lands, the menu matrix is measuring the simulation's
uniform RNG rather than anything about the menu.

## Earlier milestone: M1 — Single Restaurant, Placeholder Graphics, the Core Loop

Not started. Scope per the design doc's Phase 8, plus the Time Control & Interrupts model from Phase 5, which is M1's primary time interface rather than a convenience feature.

### M1 exit test — split in two (Aaron's call, supersedes the single bar in the design doc)

The design doc's original bar was "sim a full in-game month and count how many times the game stopped for a decision that felt worth stopping for." That conflates two very different questions and quietly gates M1 on interrupt *quality*, which cannot be good before the Advisor exists at M2. Split:

**(a) Mechanism — objective, and testable.** Live play runs at 1x/2x/3x, and jump-ahead by day/week/month **pauses, resumes, and returns control cleanly.** This is a correctness bar, not a taste one: if the sim cannot stop at an arbitrary moment and carry on from exactly there, nothing built on top of it will work.

**(b) Rhythm — subjective, and deliberately cheap.** A minimal **hardcoded** interrupt set is enough to feel whether the fast-forward-with-interrupts loop has a pulse:
- an ingredient stockout mid-service
- a walkout streak
- cash crossing a threshold

Three is the point. Do not grow this list at M1 — the question is whether the *loop* has a pulse, not whether the interrupts are good.

**Full interrupt quality is an M2 bar**, once the Advisor exists and can generate them properly. Judging variety and "was that worth stopping for?" against three hardcoded triggers would fail M1 for the wrong reason.

### Floor space is the constraint on equipment (Aaron)

"You shouldn't ever get to like 15 ovens" — correct, and the honest limit is not a cap but **floor space**. `Restaurant.FloorArea` is square metres of building; stations and fittings both consume it, so the kitchen and dining room compete for one floor. Fifteen ovens is legal and leaves you 21 covers.

`data/equipment.json` is the catalogue: every station has a cheap, standard and premium model, and **premium is deliberately faster AND smaller per unit**. That makes upgrading a real alternative to expanding — when the building is full, better equipment is the only way left to add throughput. It is also the seed of the Sims-style shop Aaron wants.

A 90m2 unit comfortably holds a working kitchen (3 ovens, 2 saute, 2 cold) and 40 covers, and nothing like fifteen of anything. `FloorArea = 0` means unmeasured and constrains nothing, which keeps food trucks and test fixtures from needing a lease.

### Build mode, and why a location has a ceiling (Aaron)

Aaron's framing: *"builder mode where if you have the space you can add another floor or extend the building — which won't always be possible, for example in the city you can't just knock down the wall and build into the building next to you."*

Built so far, as the crude first form: `Neighbourhood.MaxFloorArea` caps how big a site can ever get, `ExtensionCostPerSquareMetre` prices the land, and `Restaurant.ExtendBuilding` buys more of it. Refused past the cap with a message that says why.

The tension is deliberate and is what makes location a real choice rather than "pick the busiest": **the best traffic comes with the least room to grow.**

| | Lunch traffic | Site cap | Land |
|---|---|---|---|
| City Centre | highest | 110m2 | 950/m2 |
| Business District | high | 150m2 | 720/m2 |
| Nightlife Quarter | (evenings) | 140m2 | 660/m2 |
| Suburban High Street | modest | 280m2 | 340/m2 |

Outgrowing a city site is therefore a real, unfixable predicament — and the only remaining move is upgrading equipment into the same space, which is exactly what the premium tier exists for. There is a test covering that moment.

**Not built, and deliberately:** extra floors, and any spatial layout at all. Floor area is a single number, not a grid. A real build mode with placement is Furniture/Layout at M1 proper, and the FreeSO/FreeSims references in the design doc are the model for it.

### A site costs money to take on, and to keep (Aaron)

Each `Neighbourhood` carries a `LeasePremium` (key money, paid once before you have sold
anything) and a `MonthlyRent`. Starting capital is a fixed bankroll, so **the site you pick
determines how much you have left to trade with**: a city pitch leaves roughly 10,500 after
key money and fit-out, a suburban one roughly 19,500.

Combined with the traffic and the ceiling, choosing a location is now a four-way trade:
footfall, room to grow, land price, and how much of your capital survives opening day.

### Balance: hard but doable (Aaron), and how it was reached

Aaron's bar: *"the math needs to be realistic but also remember this is a game and we want
to be able to win. It shouldn't be easy but it should definitely be doable."*

A 100-run sweep (4 sites x 5 sizes x 5 seeds) drove three changes:

1. **A cook works a line, not a pan.** `KitchenPass.PlatesPerCook = 2`. Modelling one cook
   as one plate forced a headcount that bankrupted every restaurant in the sweep — 0/100
   configurations profitable at some settings. This was the single biggest error.
2. **Wages 16/12** rather than 18/14.40, and menu prices up roughly 15%.
3. **Realistic rents**, roughly doubled. Rent is the fixed cost that actually kills small
   restaurants, so it is what makes the opening squeeze real without punishing growth.

Result: **85 of 100 configurations profitable.** Starter builds sit at break-even to
slightly negative on every site; growth is clearly rewarded; all four sites clear a living
at their best build. Re-run the sweep by removing the `Skip` on `Sweep.OneHundredRuns`.

**Protect this shape when balancing.** A starting position that is immediately comfortable
deletes the arc; one that cannot be dug out of deletes the game.

### The scale arc, measured

A starting restaurant SHOULD lose money — the question is whether investing digs you out.
Suburban, dinner only, thirty days:

| Kitchen / seats | Revenue | Labour as % | Net |
|---|---|---|---|
| 1 unit / 12 | 5,711 | 95% | −3,831 |
| 3 units / 30 | 18,806 | 72% | −3,098 |
| 4 units / 40 | 27,697 | 58% | **+1,381** |

So you open underwater with a runway of a few months and have to reach roughly forty
covers to survive. That is the intended shape and it is worth protecting during balance
work: a starting position that is immediately profitable would remove the whole arc.

### Pricing is per-dish (Aaron)

The real game prices each menu item individually. That is already how the model works —
`PricingPolicy.SetPrice(recipeId, price)` is the primitive and `AdjustPrice` (a multiplier)
is a convenience built on top. The Sim harness only exposes the multiplier, which is fine
for testing the loop but means the player cannot currently make the actual menu-engineering
move: repricing ONE dish and watching the Kasavana-Smith matrix rearrange. Until then
`[x]matrix` in the harness is decoration.

### Playtest verdict so far: (a) passes, (b) does NOT

Two sessions, Aaron at the keyboard. Both found real defects the tests could not — a walkout death spiral, then an economy where tripling prices tripled revenue. Both are fixed.

But on the rhythm question itself he answered `y` repeatedly and then said plainly: *"I was actually saying the stops were worth it but honestly I was being a bit generous here."* **Treat that as a fail, not a pass.** A polite yes is not evidence.

The diagnosis, from his logs rather than from theory:

- **Almost every stop was the same sentence.** "N guests have walked out in a row — the kitchen is losing the room", perhaps fifteen times across twenty days. Phase 10 predicted exactly this: fast-forward compresses in-game time, so repetition arrives far sooner in *real* playtime than a per-night design would suggest.
- **The stop names a problem but not an action.** It says the kitchen is losing the room. It does not say the oven is the bottleneck, that a slot costs £2,800, or that you can afford three. The player is left to infer all of it from four lines of complaint text.
- He also noted, fairly, that judging this from text alone is hard — which is a real limit on what this harness can settle.

**What NOT to do about it:** invent more interrupt types to manufacture variety. Variety is the Advisor's job at M2 and faking it now would build the wrong thing twice.

**Done.** The walkout interrupt now names the bottleneck station, blames it by number, and quotes the cheapest fix with its price against your cash. What remains unaddressed is repetition — that needs the Advisor at M2.

**Was:** make the three existing interrupts carry their own reasoning and the specific move available — the design's Tier-2 Advisor pattern ("a question, with visible reasoning"). Nearly all the data already exists (`BusiestStationId`, per-station queue minutes, cash on hand, slot cost). That is a presentation change to existing triggers, not a new system.

### Opening hours are the operator's choice (Aaron)

A restaurant sets whatever hours it likes — including round-the-clock, and including services that run past midnight. `Restaurant.ServiceWindows` is a free list; the clock runs continuously regardless, and windows only decide when guests turn up.

The honest way to model 24/7 is **several windows with their own peaks** (breakfast, lunch, late-night) rather than one flat 24-hour window whose single peak lands arbitrarily at noon. Each service then has its own demand to staff against.

**Staying open longer must carry real risk, and currently doesn't.** Right now long hours are close to pure upside — more covers, more revenue, almost no added cost. Aaron's list of what the downside *should* be, and where each one stands:

| The cost | Status |
|---|---|
| **More stock to buy, and more capital tied up in it** | **Gap.** Ingredients are charged when *used*, never when *bought*, so a deep pantry costs nothing to hold. Fixing this means purchase-vs-consumption accounting. |
| **Spoilage on that bigger pantry** | **Gap.** Listed in M0 scope, deliberately cut from the built slice. Nothing rots, so over-ordering is free. |
| **More labour to cover more hours** | **Scheduled — M1.** Economy tracks labour; nothing generates it until Employees. |
| **Equipment each service needs** (an espresso machine for breakfast) | **Built.** `Restaurant.BuyStation` charges `CapitalExpenditure`; stations already gate dishes, so a breakfast recipe naming a `coffee` station cannot be cooked until the machine is bought. |
| **Decor and furniture** | **Built.** `Restaurant.Buy`/`BuyTables` charge the books, and `SeatingCapacity` is now *derived* from furniture rather than declared — a bigger room is something you pay for. Comfort feeds satisfaction at the smallest of the four weights, per the design's insistence that decor nudges rather than decides. |
| **Local traffic may not support every daypart** | **Built.** `Neighbourhood` gives an hourly traffic profile; `ServiceWindow` has no demand knob at all. The player picks hours, the location decides whether anyone is there. |
| **A menu nobody wants at that hour** | **Built.** Recipes carry optional `dayparts`; guests only order what suits the hour, and a party that finds nothing leaves without ordering (`ServiceResult.PartiesLostToMenu`). |

### Deferred deliberately — "we don't need to over-engineer this, the point is to have fun" (Aaron)

Ideas raised and consciously NOT built, with the reasoning, so they are choices rather than oversights:

- **Fridge / storage capacity.** Would be a cap on `Inventory` par levels. Cheap to add, but it only bites once ingredients are charged when *bought* and can *spoil* — without those two it is a constraint with no consequence. Revisit together with them.
- **Chef skill by daypart.** Aaron flagged the tension himself: breakfast is *easier* to cook than dinner, so "you need a specialist" doesn't follow cleanly. Employees are M1/M2 anyway. If it ever lands, the honest version is probably that a great dinner kitchen finds breakfast a distraction, not that it lacks the skill.
- **Prep-time interference** (why a fine-dining kitchen won't do breakfast). Genuinely the real-world reason, but it needs a prep system that does not exist. The daypart menu already delivers most of the *feel* — a tasting-menu restaurant simply has nothing breakfast-appropriate to sell.

The bar for adding any of these: does it create a decision that is fun to make? Not: is it realistic.

### What this implies, flagged before building

- **Most of (a) is testable headlessly, before Unity.** Jump-ahead with pause-on-condition is a simulation concern, not a rendering one. The riskiest part of M1 can be de-risked in the existing core.
- **All three (b) interrupts read state that already exists** — `TicketOutcome.OutOfStock`, `ServiceResult.Walkouts`, `Economy.CashOnHand`. No new simulation needed.
- **`ServiceSimulation.Run` is currently atomic** and cannot satisfy "mid-service". It runs all 180 minutes in one call and returns a finished result; there is no way to stop at minute 47 and resume. Making service resumable is a real structural change and is the first thing M1 has to confront.

---

## Architecture Rules (violating these is a bug, not a style choice)

**1. Policy propagates; nothing is cached.**
This is the most load-bearing rule in the project. It exists because Restaurant Empire II — the game this one is a successor to — required players to manually re-edit every recipe after changing a supplier, and it was its most-criticized flaw.

- A Supplier is a first-class object with a stable ID.
- Recipes reference ingredients **by ID**, never by a cached cost value.
- Contribution margin is computed **live at read time**, pulling whatever the currently-assigned Supplier costs right now.
- Switching a Supplier is a **single write to one assignment record**. Every dependent Recipe and location sees it on next computation.
- The same "single assignment, many live readers, no snapshots" pattern applies to schedule templates and (later) GM delegation.

**Sourcing scope — decided, implemented in M0.** Assignments resolve up an inheritance chain: **Company → (Region, added at M4) → Restaurant**. The company-level assignment is the default that propagates everywhere; a lower scope may override, and that override is the "exception requiring explicit opt-in" the design doc's Suppliers contract calls for. Reads walk the chain live and never snapshot, so clearing an override falls back to whatever the company says *now*, not what it said when the override was made.

The Region tier is deliberately **not built yet** — it has nothing to override until multi-location exists. It slots in without touching Recipes, costing, or any read site, because resolution already walks a chain. The reason to want it at all: without a regional tier, sourcing at ten restaurants is the identical decision as at one, which is the flat-scaling anti-pattern below. "National contract vs. local sourcing" is a decision that *cannot exist* before expansion, which is exactly what multi-location is supposed to add.

**2. Content is data-driven, not hardcoded.**
Recipes, furniture/object types, employee traits, and event definitions live in external JSON (or equivalent def files). This is required for the modding goal and is extremely expensive to retrofit. Follow RimWorld's pattern.

**3. Save/load requirements.**
- Player can save manually at any time; prompt on exit if unsaved; autosave after any long jump-ahead sim (week/month), keeping a small rolling set rather than one overwriting slot.
- Every save carries a **version stamp** (game version + active content packs).
- Saved objects reference definitions **by stable string ID**, never by index or load order.
- A missing or changed definition must **degrade gracefully** — drop the object, log it, warn the player plainly. Never crash. Never fail the whole load.
- Format is **inspectable** (JSON), not an opaque binary blob.

**4. M0 is engine-agnostic.**
Plain C#, no Unity references. Unity is the chosen engine from M1 onward; the M0 core must drop in without a rewrite.

**5. Location type is a parameter, not a class hierarchy.**
Brick-and-mortar / food truck / ghost kitchen / delivery-only are all the same Restaurant object with different capacity constraints.

**6. Kitchen tickets are channel-agnostic.**
A ticket does not know whether it came from a dine-in table or a delivery order. Cheap now, expensive later.

---

## Binding Design Principles

1. **Chef skill is real but not dominant.** Business decisions outweigh cooking skill. Signature dish creation requires the player personally and permanently — it can never be delegated.
2. **Hard and strategic, never unfair.** Difficulty comes from legible cause and effect. Every outcome must trace to a specific named cause — never an opaque score change. Losable, always explicable.
3. **Rivals are relationship-bearing NPCs**, not fixed antagonists. They can become allies as plausibly as enemies.
4. **Expansion is capital-gated, not milestone-gated.** No quest flags or artificial unlocks — when the player can genuinely afford it, they can do it.
5. **The game suggests, you decide.** The player should never have to reverse-engineer optimal play, but the game must not solve strategy for them. Three tiers: chores are automated or flagged flatly; tactical proposals are questions with visible reasoning answered yes/no (only where "no" is genuinely defensible); strategic decisions are never yes/no prompts at all.
6. **Design for memorable moments, not just balanced systems.**

## Anti-patterns (each one killed a real game in the competitive review)

- **Micromanagement tax on good decisions** — a correct strategic choice must never cost linear-in-restaurant-count manual labor.
- **Flat scaling** — bigger numbers are not new decisions. Scale must add new kinds of tradeoffs.
- **Disconnected skill layers** — if a player action doesn't feed back into simulation state, cut it.
- **Managers who don't manage** — if a role is delegated, that role's competence must genuinely determine outcomes.

---

## Working agreements

- **Write the test first**, especially for the exit tests above. M0 is verified by tests, not by playing.
- **Keep the simulation core free of presentation concerns.** Read surfaces (Dashboard/Advisor) are one component and are a lens over state — never a source of truth.
- **When the design doc and an implementation convenience conflict**, raise it rather than quietly diverging — several rules here exist specifically because a well-known game got them wrong.
- **Prefer small, verifiable increments.** Do not scaffold M1+ systems "while we're in here."
