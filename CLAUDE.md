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
