# Restaurant Empire Successor

A restaurant management/tycoon game. Full design rationale lives in `docs/design.md` — read the relevant section before implementing anything, but do not load the whole document unless you need to.

**Setting: American.** US spellings throughout (`Neighborhood`, `labor`, `center`), dollars,
and square feet — including in identifiers and data IDs, not just prose. Overseas expansion is
a later feature, not a reason to write British English now.

**Genre framing (this governs every judgment call):** this is a **tycoon/empire management game, cooking-themed** — not a cooking game. The player is a chef who opens one restaurant on a tight budget and builds it into an empire. Business and strategic decisions carry more weight than culinary execution.

---

## Earlier milestone: M0 — Headless Simulation

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

**Two things deferred out of M0, deliberately, both because they need the game loop rather than the core:** the autosave *policy* (rolling slots, prompt-on-exit, autosave after a long jump-ahead) — the format and degradation are built, the scheduling is not; and labor cost *generation* — Economy tracks it, but nothing produces it until Employees arrive at M1, so prime cost is only as complete as the labor figure booked against it.

**Caveat on prime cost:** Economy *tracks* labor cost, but nothing *generates* it yet — Employees arrive at M1. Until then a labor figure has to be booked by the caller, so prime cost is only as honest as what gets recorded against it.

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

### M2 is authorized ONLY insofar as it serves the M1(b) bar (Aaron)

Starting M2 while M1(b) fails looks like a violation of "do not build ahead" and is not: it
is the roadmap error flagged before M1 started, surfacing exactly as predicted. M1(b)'s
diagnosis was interrupts that name a problem but not an action. **The Advisor is the thing
that fixes that — you cannot pass M1(b) without it.** Building it is the remediation.

But the scope is bounded explicitly, or the precedent erodes the rule:

> **M2 work is authorized only insofar as it serves the M1(b) bar. The Advisor and customer
> archetypes qualify. R&D, promotion ladders, and Power Rankings do not** — those are the
> "while we're in here" additions the rule exists to stop.

**Customer archetypes, appetite, and price** — which fixed the degenerate popularity axis.

Guests used to pick dishes uniformly at random, so with four dishes every dish landed near
a 25% share, comfortably above the 17.5% popularity bar. A Puzzle (high margin, LOW volume)
could not arise naturally at all, and half the Kasavana-Smith matrix was measuring the RNG.

**PRICE was the missing mechanism, and archetypes were not the prerequisite.** Aaron's
correction, and it was right. `CustomerParty.PriceSensitivity` existed from the very first
commit but was only ever read on the way OUT — in `SatisfactionModel.ScoreValue`, judging
whether a meal felt like value after it had been eaten — and nowhere in dish selection. So a
guest ordered the £34 risotto as readily as the £14 margherita and grumbled afterwards. High
price affected the post-hoc score and never the order rate, which is precisely why no Puzzle
could form: high-margin/low-volume is *definitionally* about volume.

**This was an implementation gap against a contract that already existed, not a design gap.**
Phase 4's Customers contract lists "budget/price sensitivity" among what a customer knows. It
got half-implemented — **judging, never choosing.** Worth watching for the same shape
elsewhere: a field that exists, is populated, and is only read on one side of the decision it
was meant to inform.

The experiment that settled it — one menu, one site, prices flattened to a single number so
only taste could differentiate dishes:

| | share spread | quadrants reached |
|---|---:|---|
| Real prices | **2.6x** | Star, Plowhorse, **Puzzle** |
| Every price flattened to 16 | 1.5x | Star, Plowhorse — **no Puzzle** |

Archetypes alone produce a 1.5x spread: real texture, but not enough to push anything under
the popularity bar. `FlatteningEveryPriceCollapsesThePuzzleQuadrant` pins this.

**What archetypes DO add, measured afterwards rather than assumed:** they decide *which* dish
lands in each quadrant. Same menu, same prices, different crowd —

| Crowd | The Puzzle | Where the margherita lands |
|---|---|---|
| Business district, lunch | **Pizza Margherita** | **Puzzle** |
| Nightlife quarter, dinner | Black Truffle Risotto | Plowhorse |
| Suburban, dinner | Black Truffle Risotto | **Star** |

So the two do genuinely different jobs: **price decides whether the low-popularity quadrant
can exist at all; archetypes decide who is in it.** The order Aaron proposed — cheap price
fix first, then check whether archetypes earn their keep — is the one that produced this
table, and it is the order to repeat next time.

**Careful with tests that pass on an artifact.** A dish nobody ordered has a zero popularity
share and an above-average margin, so the matrix classifies it a Puzzle — correctly but
uninformatively. A breakfast dish left on a dinner menu therefore yields a Puzzle no matter
what prices do. The first version of the price test passed for exactly that reason. Assertions
about the matrix should filter to dishes that actually sold (`SoldPuzzles`).

Now: recipes carry **tags** (`seafood`, `luxury`, `quick`, `sharing`, `vegetarian`...), and
every party has an **archetype** plus a personal taste. Business Lunchers pull toward quick
and light, Romantic Couples toward refined and rich, Families toward sharing and classic and
away from luxury, Influencers hard toward luxury and seafood. Who is out depends on the hour
AND the neighborhood — a business district at 1pm is not a nightlife quarter at midnight.

Aaron's addition of personal preferences ("loves seafood") sits on top of the archetype, so
two Business Lunchers still order differently.

The payoff, measured on one identical menu:

| | tops the menu | notable |
|---|---|---|
| Business district, lunch | focaccia, flat white | truffle risotto is a **Puzzle** — nobody wants luxury at 1pm |
| Nightlife quarter, dinner | **sea bass, risotto both Stars** | influencers chase refined and luxurious |
| Suburban, dinner | **margherita** | families want pizza and sharing plates |

A menu is no longer good or bad in the abstract. It is good or bad for the people who walk
past, which is what the whole mechanic was quietly assuming.

Also added a seafood dish, because the design's own Advisor example is "we're sitting on a
lot of fish — want to feature the fish dish?" and there was no fish.

### Ingredient quality became something guests ACT on (Aaron), and a star readout

Aaron's call: *"if you use cheap ingredients and charge a premium, people will notice and
either complain or not order it."* He was describing a live exploit.

`MenuCosting.IngredientQuality` was correct and live — supplier tier resolved through the
inheritance chain, no caching — and it fed the satisfaction score and **nothing else**.
Measured on one seed with only the supplier swapped:

| Supplier | Covers | Walkouts | Satisfaction |
|---|---:|---:|---:|
| Budget Wholesale (tier 1) | 4,089 | 151 | 0.563 |
| Valley Produce (tier 3) | 4,089 | 151 | 0.731 |

**Identical covers, identical walkouts.** Since budget stock costs ~40% less, the cheapest
supplier was strictly dominant and free — in the sourcing system this entire project exists
to get right. The third instance of the same shape: a field that is populated, is read on the
judging side, and never on the choosing side. See also `PriceSensitivity` above.

**The first fix did not work, and measuring is what showed it.** Quality went into
`CustomerParty.AppetiteFor`, which decides which dish off a menu. But appetite is *relative* —
when one supplier serves every dish, a quality multiplier is a common factor across all of
them and **cancels out exactly**. Covers moved 120 -> 123. It could never make anyone eat
somewhere else, because it only ever redistributed within the menu.

**So quality belongs in `SatisfactionModel.ScoreValue`**, which is read at the DOOR. Value is
what you get over what you give, and only the second half was modeled; what arrives is part
of what was paid for. Same function now serves the pre-order balk and the post-meal score, so
judging and choosing cannot drift apart. Quality stays in `AppetiteFor` too, where it does a
different and still-real job: discriminating between dishes when suppliers are mixed per
ingredient.

**Partially closed, and the residue is recorded rather than tuned away:**

| | Gross profit | Left on reading the menu |
|---|---:|---:|
| Budget stock, honest prices | 1,257 | 0 |
| Mid-tier, honest prices | 1,225 | 0 |
| **Budget stock, 1.6x prices** | 2,143 | **11** |
| **Mid-tier, 1.6x prices** | **2,215** | 0 |

Gouging on cheap stock is now punished — mid-tier overtakes it. But cheap stock at *fair*
prices is still marginally ahead, because value saturates when prices are honest and quality
cannot push anyone out the door. **That residue needs meals to be REMEMBERED** — reputation
converting satisfaction into future volume — which is the option Aaron deliberately deferred.
Do not close it by cranking the 0.6/0.8 constants in `ScoreValue` until budget loses; that is
tuning until the number pleases you, which this project has already been caught doing once.

**The star readout (`DishRating`), and why the breakdown is the whole point.** Five stars per
dish, split into food / speed / value / room under the guest's own weights. The total is a
DISPLAY of the four components and never drives behavior — the components drive behavior on
their own. A bare "2.4 stars" would violate Binding Principle 2 directly: the player could not
tell whether the risotto is dear for what it is, slow out of the kitchen, or made with budget
cheese, and those are three different fixes at three different prices. `[r]atings` in the
harness. Nothing is stored; it is a live lens, so switching supplier moves every dish at once.

Two subtleties worth keeping: `Weakest` compares WEIGHTED losses, so it never sends the player
to redecorate over a dish failing on ingredients; and a value score below the walk-away
threshold overrides a healthy total, because a £60 margherita on premium stock still scores
four stars and would otherwise report "people are happy with this" about a dish nobody buys.

**Two existing tests changed, and neither was weakened.** `AtTheSamePrices_PremiumBuysHappiness`
asserted *identical takings* between mid and premium — which held only because quality could
not affect volume. Its own closing comment had predicted this: *"once satisfaction converts
into volume, that is precisely the bet the player will be making."* It now asserts premium
sells MORE and keeps a worse food-cost ratio, which is a stronger claim. And
`FoodCostIncludesPlatesCookedForGuestsWhoWalkedOut` choked the saute station, which cooks the
risotto — a dish guests now rarely order, so nothing backed up and there were no walkouts to
account for. It chokes the oven instead. The assertions are untouched.

**Effect on the instruments:** sweep unchanged at 100/100; the campaign now has **city
surviving twelve months** (3,748 cash) where all four sites busted before. First winning path
that probe has ever produced.

### Reputation: meals are remembered, and a dish is not the restaurant (Aaron)

The loop that makes bad food cost something. Until this existed a meal was judged and then
forgotten, so cutting every corner was free.

**Aaron's design call, which is the good part:** *"the dish could have a different ranking
than the restaurant itself — if you get a cheap decent dish you might be satisfied with it
but you probably don't love the restaurant... you can be moderately successful but not like
the best in the world."*

So the two ratings are connected without being the same number. `DishRating` says whether a
plate pleased the person eating it. `Reputation` says what the neighborhood thinks of the
place — and it has a **CEILING set by what you are actually attempting**:

    ceiling = 0.45 competence + (ingredient quality x 0.40) + (room x 0.08)

Competence is free and gets you to the middle. Past that you are buying it. The room counts
for exactly what it counts for in a single meal (`AmbianceWeight`), deliberately, so decor is
the smallest lever everywhere rather than a nudge in one system and a decider in another — at
0.15 a set of walnut tables moved the ceiling 0.69 -> 0.84 on furniture alone.

**Reputation also decides what you can CHARGE**, and that half is what makes sourcing well
rational at all. Without it reputation buys only footfall, footfall does not pay for truffles,
and budget stock out-earns premium at every horizon — so no player would ever source well. It
is also just how the trade works: nobody pays £200 a head because the ingredients cost £60.

Measured, 180 days, one menu, only the supplier and the price multiplier moved:

| | Budget stock | Premium stock |
|---|---:|---:|
| at 1.0x | 248k | 179k (giving it away) |
| at 1.4x | **345k** | 312k |
| at 1.8x | 335k, and 5,586 parties balk | **448k**, nobody balks |

Cheap food peaks and then *loses* money as you push the price, because the standing is not
there to carry it. Good food keeps climbing — but you must survive the lean years at 179k
while you earn the name. That is the arc.

And the ladder of standing itself, all run equally competently:

| Supplier | Ceiling | Settles at | Verdict |
|---|---:|---:|---|
| Budget | 0.57 | **0.551** | no strong opinion either way |
| Mid-tier | 0.73 | 0.730 (at ceiling) | as well liked as these ingredients allow |
| Premium | 0.89 | 0.890 (at ceiling) | people go out of their way to eat here |

**Note the budget row never reaches its own ceiling.** What holds it back is the food people
are actually eating, not an artificial cap. The ceiling earns its keep one tier up, where
competent execution would otherwise carry a restaurant somewhere its ingredients do not
deserve. Tests assert the plateau message against MID-TIER for that reason.

**This is genuine STATE, and not an Architecture Rule 1 violation.** Rule 1 forbids caching
values DERIVED from policy — a plate cost, a contribution margin. Reputation is accumulated
history and cannot be recomputed from current state; remembering last month is the entire
point. It is saved, and an older save without it loads at neutral (unknown, not hated).

**A ceiling must PULL, not clamp (Aaron).** The first version did `if (Standing > Ceiling) Standing = Ceiling`, which meant a supplier switch destroyed a reputation instantly — measured at **0.890 to 0.568 in a single day of service.** Six months of work gone over one dinner, with no window to notice it. Aaron: *"it shouldn't be instant unless there is a critic or blogger or influencer who catches it quickly. Other than that it should deteriorate over weeks or months."*

Now the ceiling drags standing down at `BadNewsRate` instead, and the rates are five times slower again. The curve:

| | Building (premium stock) | Losing it (switched to bulk) |
|---|---|---|
| 1 day | — | 0.884 — barely moves |
| 1 month | 0.622 | 0.627 |
| 3 months | 0.780 | 0.568 |
| 6 months | 0.879 *people go out of their way* | 0.556 |

Six months to build, two to three to lose. **And the slide is now named while it is happening:** `Reputation.LivingOnPastGlory` reports *"still trading on a name these ingredients no longer justify"*, shown in the harness header as `and FALLING toward 57`. That is the only moment when the damage is visible and not yet done, so it must not be mistaken for a plateau — the earlier code called it "as well liked as these ingredients allow" at a standing of 0.884, which was flatly wrong.

The sudden version belongs to Events at M3+: a critic or influencer who catches the change collapses that window. Recorded in the M5 addendum in `docs/design.md`. **The slow curve is the correct default precisely so the fast path can be a dramatic exception.**

**Calibration error worth remembering:** the first rates were ten times too fast. A busy
restaurant moved a third of the way to a new standing in a SINGLE DAY, which is a status
effect wearing a reputation's clothes — and it broke two location tests, which is how it was
caught. Rates are per MEAL, and a night is a hundred-plus meals, so they must be set against
that and not against intuition about nights. Now: a night moves standing ~4%, a month ~70%.

**Two existing tests were wrong in ways reputation exposed rather than caused.**
`ARestoredGameStillSimulatesIdentically` ran the compared night FIRST and saved afterwards,
so it silently compared two different starting states; it passed only because nothing
persistent survived a service. It now saves first, which is what it always meant to test.
And `DecorNudgesSatisfaction` asserted a formula bound by measuring a whole run — now that a
nicer room also lifts standing and therefore footfall, the observed gap came out at 0.0814
against a 0.08 weight. That overage is a real second channel, not a breach, so the bound is
now asserted against the formula via `DishRating` where it actually lives.

**Effect on the instruments:** sweep unchanged at 100/100; the campaign's city site went from
BUST, to surviving at 3,748, to **comfortable at 16,032**. Business, nightlife and suburban
still bust under the probe's buy-an-oven-first policy.

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

"You shouldn't ever get to like 15 ovens" — correct, and the honest limit is not a cap but **floor space**. `Restaurant.FloorArea` is square meters of building; stations and fittings both consume it, so the kitchen and dining room compete for one floor. Fifteen ovens is legal and leaves you 21 covers.

`data/equipment.json` is the catalogue: every station has a cheap, standard and premium model, and **premium is deliberately faster AND smaller per unit**. That makes upgrading a real alternative to expanding — when the building is full, better equipment is the only way left to add throughput. It is also the seed of the Sims-style shop Aaron wants.

A 900 sq ft unit comfortably holds a working kitchen (3 ovens, 2 saute, 2 cold) and 40 covers, and nothing like fifteen of anything. `FloorArea = 0` means unmeasured and constrains nothing, which keeps food trucks and test fixtures from needing a lease.

### Build mode, and why a location has a ceiling (Aaron)

Aaron's framing: *"builder mode where if you have the space you can add another floor or extend the building — which won't always be possible, for example in the city you can't just knock down the wall and build into the building next to you."*

Built so far, as the crude first form: `Neighborhood.MaxFloorArea` caps how big a site can ever get, `ExtensionCostPerSquareMeter` prices the land, and `Restaurant.ExtendBuilding` buys more of it. Refused past the cap with a message that says why.

The tension is deliberate and is what makes location a real choice rather than "pick the busiest": **the best traffic comes with the least room to grow.**

| | Lunch traffic | Site cap | Land |
|---|---|---|---|
| City Center | highest | 110 sq ft | 950/ sq ft |
| Business District | high | 150 sq ft | 720/ sq ft |
| Nightlife Quarter | (evenings) | 140 sq ft | 660/ sq ft |
| Suburban High Street | modest | 280 sq ft | 340/ sq ft |

Outgrowing a city site is therefore a real, unfixable predicament — and the only remaining move is upgrading equipment into the same space, which is exactly what the premium tier exists for. There is a test covering that moment.

**Not built, and deliberately:** extra floors, and any spatial layout at all. Floor area is a single number, not a grid. A real build mode with placement is Furniture/Layout at M1 proper, and the FreeSO/FreeSims references in the design doc are the model for it.

### A site costs money to take on, and to keep (Aaron)

Each `Neighborhood` carries a `LeasePremium` (key money, paid once before you have sold
anything) and a `MonthlyRent`. Starting capital is a fixed bankroll, so **the site you pick
determines how much you have left to trade with**: a city pitch leaves roughly 10,500 after
key money and fit-out, a suburban one roughly 19,500.

Combined with the traffic and the ceiling, choosing a location is now a four-way trade:
footfall, room to grow, land price, and how much of your capital survives opening day.

### Balance: hard but doable (Aaron), and how it was reached

Aaron's bar: *"the math needs to be realistic but also remember this is a game and we want
to be able to win. It shouldn't be easy but it should definitely be doable."*

A 100-run sweep (4 sites x 5 sizes x 5 seeds) drove three changes:

1. **A cook works a line, not a pan.** `KitchenPass.PlatesPerCook = 2`. Modeling one cook
   as one plate forced a headcount that bankrupted every restaurant in the sweep — 0/100
   configurations profitable at some settings. This was the single biggest error.
2. **Wages 16/12** rather than 18/14.40, and menu prices up roughly 15%.
3. **Realistic rents**, roughly doubled. Rent is the fixed cost that actually kills small
   restaurants, so it is what makes the opening squeeze real without punishing growth.

Result: **85 of 100 configurations profitable.** Starter builds sit at break-even to
slightly negative on every site; growth is clearly rewarded; all four sites clear a living
at their best build. Re-run the sweep by removing the `Skip` on `Sweep.OneHundredRuns`.

### The two instruments bracket the truth — do not tune against either alone

Re-measured after archetypes and the price fix, and the headline numbers disagree by a
hundred points because **they measure two different players**:

| Instrument | What it models | Result |
|---|---|---|
| `Sweep.OneHundredRuns` | 100 static builds, correctly staffed and stocked, chosen in advance | **100/100 profitable** |
| `Campaign.TwelveMonths` | one journey from a real 30,000 opening, reinvesting as it goes | **4/4 sites BUST by month 12** |

Neither is the win rate. The sweep hands the player a good build; the campaign makes the
player earn it. **The gap between them is the game**, and the thing that carries a player
across it is the Advisor. That gives M1(b) a number instead of a taste judgment: *does an
Advisor-guided opening survive twelve months?*

**Attribution, measured commit by commit rather than assumed:**

| Commit | Profitable |
|---|---:|
| `81f26c0` campaign probe | 79/100 |
| `041a3bf` archetypes + appetite | **100/100** |
| `2c04082` price in dish selection | 100/100 (no change) |

So **archetypes** caused the easing, by matching demand to what guests actually want — fewer
parties leave without ordering. The price fix is balance-neutral: zeroing `PriceAppeal` and
re-running the sweep gives byte-identical output. The "85 of 100" figure recorded above is
stale — it predates the campaign-probe commit, where the same instrument reads 79.

**The campaign probe's own policy is the naive mistake, and that is worth knowing.** It buys
an oven whenever one fits and only buys tables when the floor cannot take another
(`Campaign.cs`, the reinvest block) — so suburban finishes with nine kitchen units and twelve
seats. That is *exactly* Aaron's playtest error: "I bought a ton of ovens and kept getting
backed up." Four sites busting is therefore evidence that the naive strategy loses, which is
correct design, **not** evidence that the economy needs softening.

**So: do not tune prices, wages, rents or ingredient costs to chase either number.** The next
honest move is an Advisor-guided campaign, not a balance patch.

**Protect this shape when balancing.** A starting position that is immediately comfortable
deletes the arc; one that cannot be dug out of deletes the game.

### The scale arc, measured

A starting restaurant SHOULD lose money — the question is whether investing digs you out.
Suburban, dinner only, thirty days:

| Kitchen / seats | Revenue | Labor as % | Net |
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

**Do not over-tune interrupts against the terminal harness before graphics land.** This is a
limit on the *instrument*, not a caveat on the reading. There is a real risk of tuning
interrupts until they feel right in a scrolling text log and finding the whole calculus shifts
once a walkout is something you *watch* happen. Tune them enough to test the loop; save the
fine-tuning for when the presentation is real.

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
| **More labor to cover more hours** | **Scheduled — M1.** Economy tracks labor; nothing generates it until Employees. |
| **Equipment each service needs** (an espresso machine for breakfast) | **Built.** `Restaurant.BuyStation` charges `CapitalExpenditure`; stations already gate dishes, so a breakfast recipe naming a `coffee` station cannot be cooked until the machine is bought. |
| **Decor and furniture** | **Built.** `Restaurant.Buy`/`BuyTables` charge the books, and `SeatingCapacity` is now *derived* from furniture rather than declared — a bigger room is something you pay for. Comfort feeds satisfaction at the smallest of the four weights, per the design's insistence that decor nudges rather than decides. |
| **Local traffic may not support every daypart** | **Built.** `Neighborhood` gives an hourly traffic profile; `ServiceWindow` has no demand knob at all. The player picks hours, the location decides whether anyone is there. |
| **A menu nobody wants at that hour** | **Built.** Recipes carry optional `dayparts`; guests only order what suits the hour, and a party that finds nothing leaves without ordering (`ServiceResult.PartiesLostToMenu`). |

### Deferred deliberately — "we don't need to over-engineer this, the point is to have fun" (Aaron)

Ideas raised and consciously NOT built, with the reasoning, so they are choices rather than oversights:

- **Fridge / storage capacity.** Would be a cap on `Inventory` par levels. Cheap to add, but it only bites once ingredients are charged when *bought* and can *spoil* — without those two it is a constraint with no consequence. Revisit together with them.
- **Chef skill by daypart.** Aaron flagged the tension himself: breakfast is *easier* to cook than dinner, so "you need a specialist" doesn't follow cleanly. Employees are M1/M2 anyway. If it ever lands, the honest version is probably that a great dinner kitchen finds breakfast a distraction, not that it lacks the skill.
- **Prep-time interference** (why a fine-dining kitchen won't do breakfast). Genuinely the real-world reason, but it needs a prep system that does not exist. The daypart menu already delivers most of the *feel* — a tasting-menu restaurant simply has nothing breakfast-appropriate to sell.

- **Make in-house vs. buy in** (Aaron). Raising ingredient costs on the cheap fast dishes was
  considered as a difficulty lever and rejected on Aaron's objection, which is correct: *"you
  won't just buy focaccia, you will probably make it."* Flour and yeast are cheap; what
  focaccia actually costs is kitchen time. Pricing that as groceries would model a lie.

  The good version of the idea is his: **let the player choose per dish.** Buy it in — higher
  ingredient cost, near-zero kitchen time. Make it in house — cheap ingredients, but it
  consumes the capacity the dining room is competing for. That converts "cheap dishes are
  strictly better" into a genuine trade, and it is legible cause and effect.

  **Deliberately NOT built yet**, under the M2 bound above: it does not serve the M1(b) bar.
  Revisit once the Advisor has closed M1(b), and note it pairs naturally with the deferred
  purchase-vs-consumption accounting and spoilage, since all three are about capital and
  capacity tied up before a plate is ever sold.

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

- **Tests protect what is already right; playing is what finds what is wrong.** 182 tests found
  none of the four defects that actually mattered — the walkout death spiral, the price-gouging
  exploit, the equipment/cook ratio, and the false win rate. All four came from Aaron playing.
  **The ratio of effort should keep shifting toward playing from here.** Tests are still written
  first for exit criteria and still pin every fix, but they are a ratchet, not a search.
- **Write the test first**, especially for the exit tests above. M0 is verified by tests, not by playing.
- **Keep the simulation core free of presentation concerns.** Read surfaces (Dashboard/Advisor) are one component and are a lens over state — never a source of truth.
- **When the design doc and an implementation convenience conflict**, raise it rather than quietly diverging — several rules here exist specifically because a well-known game got them wrong.
- **Prefer small, verifiable increments.** Do not scaffold M1+ systems "while we're in here."
