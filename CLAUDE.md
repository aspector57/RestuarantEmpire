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

**THE GATE IS LIFTED (Aaron, 2026-07-28), and the reason is recorded so it is not mistaken
for the rule quietly eroding.** The three constraints had closed into a deadlock: M1(b) needed
balance, balance needed the systems that create pressure, and those systems were gated behind
M1(b). Aaron cut the third strand.

M1(b) is now recorded as **blocked on balance, not failing on advice** — the Advisor work is
done and measured, and what remains is an economy missing its drags. The gate reopens as a
different question: build the systems that make the economy real (staff who learn, events,
spoilage, purchase-vs-consumption, competitors), then re-run `AdvisedCampaign` against an
economy worth measuring. **The rule's purpose survives** — it exists to stop late content
being built before early bars are validated, and M1(b) HAS been validated to the point where
more Advisor work would be fitting noise.

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

### M1(b) now has a number instead of a feeling — and it still fails

`AdvisedCampaign` is the instrument the bar was missing. It opens identically to `Campaign`
on all four sites and then does **nothing except what the Advisor tells it**, in the
Advisor's own words. If following the advice survives where the naive policy busts, the
Advisor is carrying the player across the gap, which is what M1(b) is really asking.

**First run: 0 of 4 survived — worse than the naive campaign's 1 of 4.** The cause was in one
column: `seats 12` on every site after a year. The Advisor had built kitchens of five to
eleven units, hired crew to staff them, and **never once mentioned the dining room**. An
Advisor-guided player builds a magnificent kitchen serving twelve covers and goes broke.

`ServiceResult.PartiesTurnedAway` had been counted by the simulation since M1 and read by
nothing. **That is the third instance of the same shape** — a field that is populated, correct,
and only consulted on one side of the decision it exists to inform. `PriceSensitivity` judged
without choosing; `IngredientQuality` scored without steering; `PartiesTurnedAway` counted
without advising. Worth actively hunting for the fourth.

**Two Advisor fixes came out of it, both real:**

1. **`opportunity:room`** — it now says "we are turning people away at the door", with how
   many parties left and how many more covers the free floor would take.
2. **Advice has to say what matters FIRST, or it is only a list.** Every suggestion was
   individually correct and the sequence was ruinous. `Ordered()` now sorts by urgency —
   restocking first because it is free and unblocks sales already paid for, then seats,
   because a guest who cannot sit down never reaches the queue, then the kitchen behind them.
   And **anything that spends money is dropped entirely below two months of runway**: a
   restaurant with three weeks of cash should not be told to buy an oven.

**After both fixes: still 0 of 4.** Suburban reaches 32 covers and holds positive cash to
month three, then goes under. So **M1(b) does not pass**, and that is now a measured claim.

**What the instrument cannot settle, stated so it is not mistaken for proof:** the probe acts
on EVERY suggestion EVERY month, which no player does — a player triages. A flat obedient
reading is the harshest possible test of advice. The gap between "a good static build earns
100/100 in the sweep" and "no incremental path reaches one" is the live question, and it is
about the opening economy rather than about the Advisor.

### Employees have profiles, and hiring has risk in it (Aaron)

> *"in this model, it was hire a cook. In the real game, there will be profiles of cooks with
> their own rates, you can hire good cooks or bad cooks, they can do a good job or bad job,
> things can go wrong."*

**`Employee.Skill` existed, validated, documented as "reserved for M2" — and was read by
NOTHING for the whole of M1.** Fourth instance of the shape, after `PriceSensitivity`,
`IngredientQuality` and `PartiesTurnedAway`. Assume there is a fifth.

**`Candidate` is the mechanic, and the gap is the point.** An applicant ADVERTISES an ability
and is priced off the claim; what they turn out to be is revealed by them working
(`ScoutingError` 0.22 either way). So a dear hire can disappoint and a cheap one can be a
find, and hiring stops being a button that adds a unit. Deterministic from a seed, so the same
day always shows the same people.

**What skill now decides, all calibrated so that 0.5 is exactly neutral** — a payroll nobody
chose behaves precisely as it did before any of this:

- **The pass.** `Payroll.PlateCapacity` replaces a headcount, so a strong brigade works more
  of the kitchen. Bodies still matter: a great cook is worth more than a poor one, never two.
- **The plate.** `SatisfactionModel.PlateQuality(ingredients, craft)` — ingredients set the
  ceiling, the kitchen decides how much of it arrives. **A strong brigade on mid-market stock
  beats a weak one on the best money can buy**, so buying premium and staffing badly is an
  expensive mediocre dinner.
- **The floor.** A better server holds more covers than fourteen.

Measured, one cook against thirty-two covers so the pass is genuinely the constraint:
**44 covers at skill 0.15 against 73 at 0.95**, with wait-balks falling from 46 to 28. Same
wage line, one person, two-thirds more trade.

`KitchenPass.OpenPass` now takes PLATES rather than bodies. Rounding a skilled brigade back to
a headcount and re-multiplying threw the skill away — three excellent cooks came out at 3.9,
floored to 3, which is exactly three average ones.

### Things go wrong, and they go wrong on the dishes you cannot cook (Aaron)

> *"cheap is not accounting for like bad attitude or mistakes — someone's food is bad and
> requests a refund, or they burn the food and have to remake it."*
> *"cheap labor can also be good... maybe they excel with simpler dishes and struggle with
> more complex ones."*

Both right, and the second is the better mechanic. Hiring badly used to cost only a slightly
smaller multiplier, never a bill — so cheap was free. Now a spoiled plate costs the
ingredients twice, the pass the time to remake it, and (about a third of the time, when it
reached the table) the cover itself.

**Mistake chance is skill AND dish complexity**, with `PrepMinutes` already sitting there as a
complexity measure. The shortfall is squared, so being a bit cheap is survivable and being
very cheap is not, and it is scaled by how demanding the dish is.

**Measured — the same kitchen and the same wages, only the menu differs:**

| | Covers | Wasted food | Gross profit | Satisfaction |
|---|---:|---:|---:|---:|
| Cheap brigade, simple menu | 116 | 14 | 914 | 0.631 |
| Cheap brigade, complex menu | 79 | **226** | 833 | 0.539 |
| Strong brigade, simple menu | 120 | 10 | 965 | 0.824 |
| Strong brigade, complex menu | 112 | 7 | **1,558** | 0.804 |

A cheap brigade running pizza and salad lands within 5% of a strong one. Put the same people
on truffle risotto and they waste **sixteen times** the food and lose a third of their covers.
And skill is what UNLOCKS the expensive menu — 1,558 against 833.

**This also repairs an earlier finding.** Menu breadth used to win unconditionally, which made
"what do you commit to" a fake decision. It is now a genuine one, and it is coupled to
staffing: a cheap kitchen can be honestly profitable on a short simple card, and cannot run a
tasting menu however much it would like to.

Mishaps draw from a **separate RNG stream** (`_mishaps`), deliberately. Drawing from the main
sequence would shift every arrival and dish choice after it, silently rewriting the outcome of
every seeded test in the project for no meaningful reason.

**Still true: cheap beats skilled in the campaign**, because the wage premium slightly exceeds
the mistake saving. But that measurement is taken inside a system that loses money either way,
so it is not worth tuning against until the ratchet below is fixed.

**Built: staff learn.** `Employee.Skill` is now mutable and grows toward a hidden
`Potential`, once per plate the kitchen sends — so a busy restaurant trains people faster than
a quiet one, and the same hire is worth more to a place that actually trades. Candidates carry
headroom weighted toward the green, so somebody on the floor wage may be worth considerably
more in six months and is indistinguishable at interview from somebody who is simply not much
use. That makes a cheap hire a BET rather than only a risk, which is the half that was missing.

**And it exposed a real bug it would otherwise have made much worse: THE PAYROLL WAS NEVER
SAVED.** Loading a game silently emptied it, leaving a restaurant with equipment nobody could
work and tables nobody could serve. It went unnoticed because staff carried no state worth
keeping — a cook was a wage and nothing else. Skill that grows makes people irreplaceable, so
`StaffState` now persists id, name, role, wage, skill AND potential. Potential especially,
because it can never be re-derived from anything.

**Superseded, and left here because the reasoning still holds:** *"high potential to learn, start off not
great."* Cheap staff who improve with plates cooked, capped by a hidden potential, would make
a cheap hire a genuine bet rather than only a risk. It needs skill to become mutable state and
therefore saved, so it is its own increment.

### The ratchet, fixed — M1(b) from 0 of 4 to 2 of 4, and why I stopped there

**The root cause was that `AddChores` never saw the trading result.** `understaffed:kitchen`
fired on `manned < units` alone, so buying a unit ALWAYS produced a hiring demand, paid
hourly forever, whether or not anybody had ever waited. And **no suggestion in the entire
Advisor saved money** — it was a one-way valve by construction.

Four changes, each defensible on its own:

1. **Evidence-gated hiring.** Idle equipment is only a problem if the queue is costing trade.
   Nobody waiting means the ROOM is the constraint, and another cook cannot serve people who
   have nowhere to sit.
2. **`overstaffed:kitchen` and `overstaffed:floor`** — the Advisor can now say "we are paying
   for hands we did not need", ranked second only to restocking, because it stops the bleeding
   and costs nothing.
3. **The runway brake distinguishes ONGOING cost from CAPITAL.** Suppressing all spending was
   a death trap: a city site pays 7,800 a month against an 18,000 bankroll, so it counted as
   broke from day one and was advised never to grow — poor because small, told to stay small.
   Wages are forever; a table is bought once and then earns.
4. **A room can be too small without looking it.** Turn-aways alone missed the worst case
   entirely — a slow kitchen holds tables, so twelve covers against eleven stations reported
   four times as many people put off by the WAIT as turned away, and the dining room was never
   mentioned once in twelve months. It now also fires on the shape of the place.

And in the instrument: **one investment a month, in the Advisor's order.** Obeying everything
at once made the ordering meaningless — the run would skip seats for want of cash and spend
that same cash on kitchen two suggestions later.

**Result: 0 of 4 surviving, to 2 of 4.** City +11,445, business +7,301.

**WHY I STOPPED, and it matters more than the score.** The "two covers per unit" constant in
change 4 is empirical, not derived — four was tried first, scored 1 of 4, and two scored 2.
Worse, outcomes swing enormously on purchase ORDERING: nightlife finished +12,306 in one
configuration and −19,423 in another with the identical final shape. **At that sensitivity I am
fitting noise, not fixing a system**, and moving the constant again to reach 3 or 4 would be
the same "tuning until the number pleases you" this project has already been caught doing.

**The sensitivity is itself the finding: the advised margin is thin enough that ordering
decides survival.** That is a balance property, and balance is parked until the systems that
create pressure exist. **M1(b) does not pass. It is no longer failing for want of advice.**

### M1(b), diagnosed: the Advisor has a ratchet and no brake

The bet was that richer hiring would sharpen the advice. **It did not, and the experiment
found something better than an answer to it.** Same advice followed twice, differing only in
who gets hired:

| Site | Cheapest 12/24/36mo | Best CV 12/24/36mo |
|---|---:|---:|
| City | **+12,289** / −8,348 / −45,667 | +1,115 / −18,617 / **−37,119** |
| Business | −785 / −26,206 / −51,126 | −11,793 / −57,726 / −101,017 |
| Nightlife | −28,048 / −71,868 / −117,236 | −34,611 / −87,458 / −140,827 |
| Suburban | −29,871 / −68,316 / −108,596 | −40,668 / −87,977 / −137,279 |

Three findings, in order of how much they matter:

1. **Every advised run declines toward bust, whoever it hires.** The hiring comparison is
   happening inside a system that is already losing. **The Advisor forms a ratchet with no
   brake:** buying kitchen raises `understaffed:kitchen`, hiring for it raises the wage bill,
   the wage bill is paid forever, and nothing anywhere weighs the next suggestion against what
   the restaurant actually earns. Runway suppression was not enough, because cash stays
   healthy while the ratchet is winding and is gone by the time it bites. **That is the M1(b)
   defect, now isolated: the Advisor knows what is wrong and has no idea what you can afford.**
2. **Hiring cheap beats hiring well nearly everywhere**, and only crosses over on city at
   about thirty-six months. Skill costs roughly double at the top of the market and pays back
   through quality, which reaches money only via reputation, which moves over months. A
   decision whose right answer is "always take the cheap one" is decoration — worth fixing,
   but AFTER the ratchet, since the ratchet is what makes every column negative.
3. **Hiring choice does measurably move the outcome** (city +12,289 against +1,115), and the
   risk is real: nightlife paid MORE for a WORSE brigade because `ScoutingError` burned it.
   The mechanic works; its incentives are backwards.

**On the M1(b) bet: not settled.** Advised runs improved a great deal (nightlife −69,708 to
−8,467, suburban −54,264 to −15,039, business to −969) but **still bust on all four sites**.
And the improvement cannot be attributed to this work: those runs span both this change and
the awareness change, and **the advised campaign still hires generic 0.5-skill staff — it never
opens the hiring pool.** The bet that richer hiring sharpens the advice is untested until the
instrument actually chooses between candidates. That is the next step, not a conclusion.

### Being unknown is not the same as being disliked (Aaron)

Aaron: *"perhaps I had too much traffic right away?"* He was right, and it was a real hole.
Standing began at neutral, neutral mapped to a x1.0 traffic multiplier, and so a restaurant
that opened its doors this morning drew the full footfall of the street on day one. Being
undiscovered and being avoided were the same number.

They are now two: **`Awareness`** (how many people have heard of you, 0.35 to 1, earned by
serving anybody at all) times **`OpinionMultiplier`** (what they think, 0.6 to 1.4, earned by
serving them well). A new restaurant is quiet because nobody knows it; a bad one stays quiet
after they do. Two problems, two different fixes — and **marketing, when it exists, belongs on
awareness rather than on standing.** You can buy people knowing about you. You cannot buy them
rating you highly.

Calibration caught me out the same way the reputation rates did: `MealsToBecomeKnown` was
3,000, which was meant to be a season and which a busy dinner service cleared in five weeks.
A working restaurant here serves roughly 2,600 covers a month, so it is now 12,000.

**Measured effect: a real opening ramp, and no change to the endgame.** Month-3 cash falls
about 30% across all four sites (city 49,945 -> 33,135, business 30,371 -> 18,679) while
twelve-month outcomes move by a few percent. That is the right shape for a modelling fix —
being unknown costs you early and stops mattering once you are established.

**It does NOT make the game harder, and should not be mistaken for having done so.**

### The game is easy because the systems that create pressure do not exist yet (Aaron)

Aaron's framing, and it is the right one to hold before touching any difficulty dial:

> *"in this model, it was hire a cook. In the real game, there will be profiles of cooks with
> their own rates, you can hire good cooks or bad cooks, they can do a good job or bad job,
> things can go wrong... I think the complexity will be higher than in this version."*

`Payroll.Hire(new Employee(...))` is a placeholder: a generic unit at a fixed wage that always
performs identically and never leaves. Real profiles bring rates, skill, variance and failure,
and those are costs and decisions the current economy simply does not have. The same is true
of marketing spend, spoilage, purchase-vs-consumption accounting, competitors and events —
every one of them a drag that is not being paid today.

**So do not tune the economy to be hard against this version.** The same trap as tuning
interrupts against a terminal and the browser port: calibrating against a model that is about
to stop existing. Fix what is *wrong* (the traffic hole was wrong). Leave what is merely
*easy* until the systems that make it hard are in.

### RETRACTED: "the game is unwinnable" was an instrument bug

A previous version of this section reported that no site could afford a working restaurant
and that the game could not be won from its own starting position. **That was wrong**, and it
is worth leaving the correction here rather than quietly deleting it.

`SimulationRunner` tracks takings, food and wages internally and exposes `ProjectedCash` as a
computed view. **Nothing reaches the Economy until a caller records it.** `Campaign.cs` books
all four lines every month; every probe written afterwards booked *rent only*. They therefore
measured a restaurant that paid its landlord and its staff and was never once paid by a
customer — and then reported the resulting hole as a balance finding.

The lesson is the same one as the browser port, one layer in: **a new instrument must be
validated against a known-good one before its output is believed.** `Campaign.cs` was sitting
right there, doing it correctly, and the disagreement between them was visible from the first
run — city at −12,940 by month three, against a sweep that said the same shape was profitable.
Two instruments disagreeing by that much is a bug report, not a finding.

### What the corrected instruments actually say

Static builds bought from the real opening bankroll, traded twelve months:

| Site | Focused (3 dishes, $9,300) | Whole card ($14,600) |
|---|---:|---:|
| City Center | 128,021 | **212,309** |
| Business District | 62,480 | 109,585 |
| Nightlife Quarter | 111,517 | 205,952 |
| Suburban High Street | 115,238 | 121,057 |

So the game is winnable everywhere, comfortably, and **Aaron's "it might be too easy" is
confirmed rather than contradicted**: 18,000–27,000 becomes 62,000–212,000 in a year with no
decisions after opening day. A focused menu is cheaper to open but earns LESS — breadth wins
on every site, which kills the hypothesis that menu focus is the missing decision.

**The one finding that survived the correction, and it is still real: an Advisor-guided
opening busts on all four sites (city −2,568, business −2,128, nightlife −69,708, suburban
−54,264).** Following the advice exactly is worse than buying a sensible build on day one and
never touching it again. The Advisor over-invests — seven kitchen units and five to seven
staff — because each suggestion is locally correct and nothing weighs them against what the
room can earn. **M1(b) still does not pass**, for a real reason this time.

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
| **More stock to buy, and more capital tied up in it** | **Built.** `Restaurant.OrderStock` charges the money on DELIVERY, not when the dish is cooked. Aaron: *"you should pay when you buy it and then make money when you sell a dish right?"* Right — and it did not, so a walk-in was free to fill. |
| **Spoilage on that bigger pantry** | **Built.** Meats, produce and dairy perish; the store cupboard keeps. Order to need and food cost lands at 32% of revenue; order ten times over and 92% of it goes in the bin. |
| **More labor to cover more hours** | **Scheduled — M1.** Economy tracks labor; nothing generates it until Employees. |
| **Equipment each service needs** (an espresso machine for breakfast) | **Built.** `Restaurant.BuyStation` charges `CapitalExpenditure`; stations already gate dishes, so a breakfast recipe naming a `coffee` station cannot be cooked until the machine is bought. |
| **Decor and furniture** | **Built.** `Restaurant.Buy`/`BuyTables` charge the books, and `SeatingCapacity` is now *derived* from furniture rather than declared — a bigger room is something you pay for. Comfort feeds satisfaction at the smallest of the four weights, per the design's insistence that decor nudges rather than decides. |
| **Local traffic may not support every daypart** | **Built.** `Neighborhood` gives an hourly traffic profile; `ServiceWindow` has no demand knob at all. The player picks hours, the location decides whether anyone is there. |
| **A menu nobody wants at that hour** | **Built.** Recipes carry optional `dayparts`; guests only order what suits the hour, and a party that finds nothing leaves without ordering (`ServiceResult.PartiesLostToMenu`). |

### Deferred deliberately — "we don't need to over-engineer this, the point is to have fun" (Aaron)

Ideas raised and consciously NOT built, with the reasoning, so they are choices rather than oversights:

### You pay on delivery, and earn when you sell (Aaron)

> *"you should pay when you buy it and then make money when you sell a dish right?"*

Right, and the game did not do it. Ingredients were charged at the moment they were COOKED, so
filling a walk-in cost nothing until the food was sold — a pantry was free to hold, which is
what made par levels a slider rather than a decision.

`Restaurant.OrderStock` now takes the money on delivery. **The food-cost entry was moved, not
duplicated** — `Economy.RecordService` no longer books it, because charging on delivery AND on
the plate would take the money twice and quietly halve every restaurant's margin. There is a
test for exactly that (`StockIsNotPaidForTwice`), since it is the kind of error that looks like
a balance problem for weeks.

`ServiceResult.FoodCost` still reports what a service consumed and binned — it is what the
food-cost RATIO is measured on. It is simply no longer a cash movement.

**The new failure mode is the interesting one, and it is the one that kills real restaurants:
profitable on paper and short of cash, because it is all sitting in the walk-in.**

### Ordering is a POLICY, not a chore (Aaron) — and what that leaves unresolved

> *"you don't want to constantly be ordering because things are spoiling... it shouldn't be a
> huge daily thing you need to always be monitoring, then you are basically playing a stocking
> game if it is too much. It should not be the main focus of the game but should exist in a
> meaningful way at the same time."*

A fair hit, and true of what had been built: perishables need topping up every few days and
nothing did it for you. **`Restaurant.StandingOrder` is on by default** and tops up to par each
morning, paying as it goes and never ordering food it cannot afford — a restaurant with no
money stops getting deliveries, which is a truer failure than a quiet overdraft.

The Advisor changed with it. **Restock chores are suppressed while a standing order is
running**, because "you are low on tomatoes" is a notification about something already handled.
What it says instead is `overordering`: *"we're throwing away too much of what we buy"*, with
the share and the reason. Advise on the policy, not the chore.

Measured, sixty days with **the player doing nothing at all**: 12% of the food bill binned,
nothing 86'd, satisfaction 0.73. The stocking game is gone.

**WHAT IS UNRESOLVED, and it is worth being straight about.** Spoilage is now a background cost
(7–26% of food depending on how the place trades) that the player has **no lever on**, because
the automatic order is already about right and par settings no longer change the outcome —
tight, normal and deep pars produced identical results. It exists meaningfully as a COST. It
does not yet exist as a DECISION.

**CORRECTION — that measurement was confounded and the conclusion was wrong.** It compared a
TWO-dish menu against a THREE-dish one, and menu breadth is doing the work: a narrow card loses
whole parties who find nothing they want (a suburban dinner has couples in it, and
pizza-and-bread offers them nothing refined). Aaron spotted it — *"shouldn't pizza and bread
sell faster?"* — and he was right to.

Controlled properly, three dishes either side sharing the caprese:

| | Covers | Spoiled |
|---|---:|---:|
| Stable pair (pizza, focaccia) | 7,202 | **7%** |
| Perishable pair (sea bass, risotto) | 7,436 | **11%** |

**Perishability costs about half again in waste, and covers barely move.** So a menu built on
things that keep IS cheaper to run — the opposite of what was recorded here before. Second
confounded comparison this session; the first was measuring campaigns that never booked their
takings. **Two instruments that disagree, or a comparison where more than one thing differs,
is a bug report rather than a finding.**

**The missing piece is an UPSIDE to ordering deep** — and Aaron answered it: *"you can order
deeper in the region maybe than for a singular restaurant, like a chain uses a massive national
supplier but a local restaurant uses local ones."*

**That is the right answer, and it lands exactly where the Region tier was already waiting.**
CLAUDE.md's sourcing rule already says the chain resolves Company → Region → Restaurant and
that the Region tier is deliberately unbuilt because "sourcing at ten restaurants is the
identical decision as at one, which is the flat-scaling anti-pattern". This is the thing that
makes it a different decision:

- **Local suppliers** — small drops, fresher on arrival, higher unit price. What a single
  restaurant can actually buy.
- **A national distributor** — cheaper per unit and fewer deliveries, but bulk drops mean stock
  arrives with more of its life already spent, and the quality tier is lower. Viable only once
  the volume is there to get through it.

So ordering deep gains its upside precisely when you have the throughput to justify it, and
**expansion buys a new KIND of decision rather than a bigger number** — which is what the
anti-pattern list demands of scale. It also gives the M5 franchising addendum its supply-side
teeth: a chain that switches to national sourcing is the same move as prestige erosion, seen
from the pantry.

**Deliberately NOT built: it is M4, and needs multi-location to mean anything.** A single
restaurant offered a national contract has no decision to make.

### A scale whose top cannot be reached is a wrong scale (Aaron)

> *"it says on this supplier your standing can never pass 89/100 — this is the best supplier
> possible so would I never be able to reach 100?"*

No, and that was a real defect. The ceiling was `0.45 competence + ingredients x 0.40 + room x
0.08`, which tops out at **93 even with the best of everything** — so a restaurant doing
everything available to it was told it was capped, and given no way to learn why. The three
now sum to exactly 1.0 (0.42 / 0.50 / 0.08), and **a perfect standing needs the best sourcing
AND a perfect room, both**.

**And measuring it turned up something more useful: THE CEILING ALMOST NEVER BINDS.** Standing
converges toward what the meals actually score, and that sits well below the cap at every tier:

| Supplier | Standing settles at | Ceiling | Meal quality |
|---|---:|---:|---:|
| Budget | 0.452 | 0.560 | 0.518 |
| Valley | 0.630 | 0.760 | 0.718 |
| Premium | 0.750 | 0.960 | 0.878 |

So **what holds a restaurant back is the food, not the cap** — the ceiling is a backstop for a
kitchen executing far better than it sources, which is rarer than it sounds. It was only ever
marginally binding, even before this change; two tests were asserting `AtCeiling` and passing
by a whisker. They now assert what is true, and the one that needs the cap to bind forces the
condition rather than hoping trade reaches it.

The browser build says this plainly now instead of quoting a number that rarely matters.

### Freshness: a gradient, not a cliff (Aaron)

> *"we should be able to see how much is about to turn bad, because you may need to order more
> still... then you can decide to toss it... and maybe if it's not fresh people can kinda say
> it didn't taste super fresh?"*

Three things, and they chain:

1. **`Inventory.TurningWithin(id, days)`** — how MUCH is about to go, not just how old the
   oldest batch is. The previous readout said "oldest: 6d", which tells you nothing about the
   size of the hole coming, and you cannot order around a hole you cannot measure.
2. **Freshness reaches the plate.** `MenuCosting.Freshness` takes the WEAKEST ingredient in a
   dish, because one tired component is what a guest notices, and it multiplies into
   `PlateQuality` alongside ingredients and cooking. Full marks for the first half of an
   ingredient's life, then a slide to 0.55 — **never zero, because the worst a guest gets from
   food that is still food is "that didn't taste fresh."**
3. **`Inventory.Discard` — throwing something out on purpose**, oldest first. This is only a
   decision worth having BECAUSE of (2): before freshness, serving tired stock always beat
   binning it, so nobody would ever have chosen to.

**Held back deliberately: people getting sick.** Aaron raised it and it is the right instinct,
but it needs machinery that does not exist — complaints, health inspection, closure — and it
punishes in a way that reads as unfair without a warning system first. With freshness in, it
becomes an **Event** built on top rather than a subsystem of its own. That is the M3+ version.

**It also exposed my own reorder rule as wrong.** `SuggestedReorderQuantity` was buying
`usage x shelfLife x 1.5`, which for a ten-day tomato is FIFTEEN DAYS of stock — so the oldest
thing on the shelf was always most of the way through its life. Measured: a premium-sourced
kitchen capped at 0.609 standing against a 0.890 ceiling purely because everything it served
was days old. It now holds about four days' cover, which keeps what reaches the pass inside
the first half of its life. **Order little and often.**

### Spoilage — SHIPPED, on Aaron's three refinements

He asked for it plainly: *"spoilage should happen over time, so your food goes bad if you are
over buying."* The first two attempts made every site unwinnable — 94% of all food cost went
in the bin — and were reverted rather than shipped. **Three corrections from Aaron made it
work**, and each one earned its place:

1. **"Maybe spoilage only happens on meats and produce?"** Shelf life of **zero means it
   keeps**. Flour, olive oil, rice, coffee and hard cheese are store cupboard and never rot.
   Taxing the dry goods was most of what made it unplayable.
2. **"You're going to be buying more before you get to 0, so you need to use the oldest stuff
   first."** Stock is held as DATED BATCHES and consumed oldest-first. A single total with an
   average age would let every top-up quietly rejuvenate the stock underneath it — reorder
   before you hit zero, which is what anyone does, and nothing would ever spoil.
3. **"Give some grace so you don't need to order every single day, but you should still be
   thoughtful."** Lives are generous against reality: sea bass 4 days rather than 2, basil 7,
   tomato 10, mozzarella 14, butter 30.

**And one thing the measurements forced, which is the deeper fix: par levels are a policy for
things that KEEP.** `SuggestedReorderQuantity` is now capped at what will be used before it
turns, from a smoothed `DailyUsage` per ingredient. Uncapped, a four-day fish was topped back
up to a full shelf every time it dipped, used a fraction and binned — not a difficulty
setting, a broken order.

**Measured, thirty days, the same restaurant ordering differently:**

| Opening order | Spoiled | Share of food cost | Dishes 86'd |
|---|---:|---:|---:|
| To need (20) | 2,336 | **26%** | 0 |
| Comfortable (60) | 3,012 | 31% | 0 |
| Deep (200) | 8,021 | 54% | 0 |
| Ten times over (2,000) | 74,127 | 92% | 0 |

Order to need and **food cost lands at 32% of revenue — the middle of the industry's healthy
28–35% band** — with nothing ever 86'd. Over-order and it balloons. That is the lesson made
mechanical rather than stated.

**Three fixtures had to change, and all three were wrong rather than unlucky:** they stocked
every ingredient in the catalogue for menus that cook six, and one held a hundred thousand
units of everything. Harmless while nothing could rot; now it is precisely the over-ordering
the mechanic exists to punish. **Don't stock what you don't cook.**

**Traps, recorded because they cost two attempts:** the clock's tick is ABSOLUTE, so stock
loaded before a run is dated zero against a day index in the tens of thousands and the first
tick bins the lot — hence `Inventory.StartOfRun`. `Receive` must date from the pantry's own
calendar or a restock arrives already expired. And a patch that targeted `Unit = unit` silently
did not apply to the real line `Unit = unit ?? "unit"`, so shelf life loaded as zero
everywhere and **every measurement taken through it was garbage until the field was verified
as actually assigned.**

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

### Aaron's playtest, 2026-07-29 — what it found, and what is still open

**1. `Markup` IS MISNAMED AND IT MISLEADS.** Aaron: *"if I have a House Focaccia that with
premium vendors costs me $1.72 and I list it for $14, it only shows a 1.8x mark up, is that
right?"* No. `MenuCosting.Markup` is price against the price the dish SHIPPED at ($14 ÷ $8),
which is a real thing the value model needs — but shown beside the ingredient cost it invites
the obvious reading and is wrong by a factor of twenty. A flat white at $7.40 on 16c of coffee
displayed "1.9x markup" when the actual markup is 46x, a 2% food cost. **The browser build now
shows food cost % (the trade's own number) and calls the other figure "x the menu price".**
The C# property should probably be renamed `PriceAgainstDesigned` — it is not markup.

**2. RAISING PRICES WAS NEARLY FREE — FIXED, PARTLY.** Aaron walked
prices up in stages and stayed profitable at every step until the very top of the slider. The
reason is structural: value is judged as `1/(price ÷ designed price)`, so doubling every price
scores 0.5 against a 0.40 walk-away threshold — a hair above ruin. Meanwhile the covers you
keep pay double. At the end state he had **$6.7m in cash, 58% prime cost, and 68–93 parties a
day walking away on price**, and it was still the most profitable configuration he had run.
**Losing most of your custom should not be the optimum.** Measured before the fix:

| Price | Net, one month | Parties balking |
|---|---:|---:|
| x1.0 | 7,315 | 0 |
| x1.5 | 26,634 | **0** |
| x2.0 | 43,545 | 194 |
| x2.5 | **47,001** | 782 |
| x3.0 | −12,244 | 1,903 |

A free six-fold multiplier behind a slider, then a cliff. **Two changes:**

- **`PriceToleranceExponent`** — value is `(1/markup)^2` rather than a plain reciprocal, so
  resistance builds from about a third over the designed price instead of waiting for double.
- **`WalkAwayChance` replaces the hard threshold.** A step function can only ever produce a
  cliff: everyone tolerates the price until one more cent and then nobody comes. At an
  exponent of 2.5 with the old threshold, a 1.4x menu thrived and a 1.6x menu served literally
  nobody. It is now a chance that climbs as the deal worsens, so a dear menu bleeds custom —
  and **the price-sensitive go first**, which is what makes archetypes matter at the door.
  Drawn from its own RNG stream (`_judgement`), like mishaps, so it does not shift arrivals.

After: a smooth curve with an interior optimum and no cliff — 7,315 at the designed price,
34,321 at double, 27,491 at two and a half.

**WHAT IS STILL WRONG, and it is the deeper half.** The optimum sits near 2x the designed
price and pays about 4.7x the profit of pricing as designed. So the slider still hands out free
money, just less of it and with a real ceiling. The root cause is that value at 1x scores
around 1.0 against a 0.40 walk-away, so there is a wide dead zone where raising prices costs
nothing at all. **Closing it means the designed price sitting near the edge of what people will
pay** — which is true of real menus — and that is a calibration with wide blast radius, not a
constant to nudge at the end of a session.

**3. Jargon with no explanation.** *"I have no idea what prime cost means, or plow horse."*
Fair, and a straight violation of the design's own legibility principle — the browser build now
carries a plain-language legend for the four quadrants, food cost % and prime cost.

**4. Firing was a queue, not a choice.** *"When I let a cook go, I can't select which one."*
Now you pick the person, which matters a great deal once they have different wages and skills.

**5. Storage was capacity from thin air.** *"It's giving us storage without even having either
of these."* Right — base capacity is now zero and the fit-out includes an under-counter fridge
and dry shelving that you own. And his idea that **better cold keeps things longer** is in: a
walk-in extends shelf life 40%, which gives the upgrade path a second reason to exist beyond
square footage.

**Hover definitions are in** — every term in the pass readout and every menu-matrix tag
carries its meaning on hover, from a single `GLOSSARY` object so a tooltip and the printed
legend can never drift apart. Aaron also asked for **a pause/settings screen holding the
definitions**, which is queued rather than built at his suggestion.

**STILL OPEN, all Aaron's, none built:**
- **Per-product ordering vs the blanket standing order.** He is right that "always order"
  removes the strategy. The browser build now has a per-ingredient **order** button alongside
  the standing-order switch, but the real answer is probably that the standing order should be
  something you configure per line rather than a single toggle.
- **Staffing a service rather than a day.** *"If you just want a coffee and pastry counter in
  the morning, you don't need your full staff."* Correct, and currently impossible — payroll is
  a day-rate. Needs shifts.
- **A settings or pause screen with the glossary in it**, rather than definitions only
  appearing where the term does.
- **Far more recipes, and a recipe builder.** Content, and a real feature. The data-driven
  loader already supports the first half.

### Price decides who WALKS IN, not who storms out (Aaron)

> *"I do think that you should attract the right people. In real life people know the rough
> costs before going to a restaurant... typically they don't go somewhere and leave unless they
> are in like a city and look at the menu on the door."*

This is the correct model and it replaced ours. We had parties arrive and then read the menu
and leave — 68 to 93 a day in his last playthrough. Wrong twice over: **it is not how people
behave, and it is a weak punishment**, because the ones who stay pay the higher price and make
up the difference. That is precisely why over-charging stayed profitable no matter how steep
the curve got. If they never come, there is nobody left to make it up.

**`ArchetypeProfile.WouldConsider(pricePosition, standing)`** now decides, before arrival,
whether this sort of person would eat here at all. The price-sensitive drop away first, so a
dear menu quietly fills the room with couples and enthusiasts rather than families — **still
one price on the menu**, it just decides who reads it.

**Reputation is the only quality signal available from home**, so it belongs here: you cannot
see the ingredients before you go, but you have heard whether the place is any good, and a
strong name carries about a third more price. **That is what finally makes building a
reputation buy something concrete.**

**Door-balking survives as a small, city-shaped remainder** — `Neighborhood.MenuReadAtTheDoor`
is 1.0 in the city centre, 0.2 in the suburbs. Nobody drives to a high street for dinner and
turns round over the price of a pizza.

**Measured, and this is the fix to the thing that had been half-done twice:**

| Price | Covers | Lost to price | Net |
|---|---:|---:|---:|
| x1.0 | 3,651 | **0** | 7,315 |
| x1.1 | 3,482 | 185 | 9,807 |
| x1.4 | 2,971 | 776 | **14,847** |
| x1.6 | 2,276 | 1,208 | 10,531 |
| x2.0 | 574 | 1,888 | −12,371 |

**The dead zone is gone** — resistance starts at 1.1x rather than 2.0x — and the reward for
finding the optimum fell from 6.4x to **2.0x**. Over-pricing now loses money at 2x rather than
being the best strategy in the game.

**Four tests moved with the model, and two of them taught something:**
- *"Cheap ingredients at premium prices cost you trade"* is **no longer true on the night** —
  measured, the two restaurants serve identical covers and lose identical parties, because you
  cannot see the ingredients from home. Bad food does not empty your restaurant tonight; it
  empties it over months through your name. The test is renamed for that chain and asserts it.
- *"Premium sourcing becomes viable once you charge for it"* now yields a **6% gross gain
  rather than over 100%** — because when price decides who turns up, most of what you gain per
  cover you give back in covers. The clear win is the food-cost ratio (41.9% to 35.5%).
  Charging properly makes premium survivable, not lucrative.

### The parallel implementation (Howard's branch) — what is worth taking

Aaron co-owns a second implementation of this same design: `HSpector1/Restaurant`, branch
`foundation/m0-headless-service-lab`. ~2,000 lines, .NET, same "deterministic engine-agnostic
core" framing, reached M0 independently. It is a control group, and two of its decisions are
straightforwardly better than ours.

**1. PRICE IS ANCHORED TO WHAT THE CUSTOMER CAN AFFORD, NOT TO A DESIGNED PRICE.** This is the
answer to our biggest open problem. Each segment carries a `BudgetPerCoverCents`; the model
takes 60% of it as what a main should cost, and conversion falls the moment your price exceeds
that, scaled by the segment's own `PriceSensitivityBp`:

```
budgetMain = segment.BudgetPerCover * 0.6
overBp     = max(0, price/budgetMain - 1)
priceFactor= clamp(1 - overBp * segment.PriceSensitivity, 0.08, 1.0)
```

`SuggestedPriceCents` exists in his fixtures but is **only a starting value in the CLI** — it
never enters the demand calculation. Ours makes it the anchor (`Markup = price / designed
price`), which is exactly why we have a dead zone: a dish priced as designed scores ~1.0
against a 0.40 walk-away, so the first chunk of any price rise is free. **Anchoring to budget
removes the dead zone by construction** — there is no "fair" price to be safely at, only a
customer who can or cannot afford you.

**THERE IS STILL ONE PRICE — the first version of this note said it badly.** Aaron pushed back
on exactly that: *"when you got a restaurant everyone pays the same price."* He is right, and
the note was wrong rather than the model. Nobody is charged differently. You set the risotto at
$34 and the food enthusiast on an $85 budget orders it while the value-lunch crowd on $22 reads
the menu and leaves. **One price, different answers.** What the model gives you is that the
RIGHT price depends on which crowd you are trying to win — one decision, made for everyone, and
exactly what choosing a price point does in real life. Demand segmentation, not price
discrimination. Our archetypes already carry a price sensitivity; what they lack is a BUDGET to
judge against, which is the whole difference.

**2. RNG STREAMS ARE PER-ENTITY, NOT PER-DOMAIN.** `RngStreams.For(stream, entityKey)` returns a
fresh SplitMix64 seeded from `Mix(Mix(rootSeed, streamSalt), entityKey)`. So a roll added to one
party cannot perturb any other party, ever. We have three global streams (`_rng`, `_mishaps`,
`_judgement`) and reached for the second and third precisely because adding a draw shifted
everything downstream — **his design solves the problem we kept patching around.** His ADR-003
names the exact failure we hit twice this session.

**Also worth stealing, in rough order:**
- **Menu complexity as an explicit throughput tax** (`Tuning.ComplexityBp`): breadth beyond four
  items adds ticket work and mistake chance. We measured breadth winning unconditionally and
  had no counterweight; he modelled one deliberately.
- **A pre-service forecast computed from the same model as the service**, then compared to
  actual. That is a management-game mechanic in its own right — you plan, then find out how
  wrong you were — and it is a strong argument for keeping demand logic in one shared place.
- **Tuning constants in one named file**, shared by simulator and forecaster so they cannot
  drift. Ours are scattered across the classes that use them.
- **Integer fixed-point (basis points) rather than `decimal`.** Both are deterministic on .NET;
  integers are portable anywhere and cheaper. Not worth retrofitting, worth knowing.

**Where ours is further along, so this is not a wholesale swap:** reputation with awareness and
a ceiling, spoilage/freshness/FIFO with pay-on-delivery and storage, staff who learn with
scouting error, menu engineering by category, the Advisor, save/load, and a browser build that
a human has actually played five times. His content is hardcoded fixtures; ours is data-driven
JSON per Architecture Rule 2.

**The honest summary: he built a better ECONOMIC MODEL, we built more GAME.** The budget anchor
and per-entity streams are the two things to take.

### The question we never asked: are there SEVERAL ways to run a restaurant?

`Sweep` asks "is this configuration profitable?" and answered 100/100 for months while Aaron
kept saying the game was too easy. **Profitability is not strategy.** A game with one dominant
plan can be profitable everywhere and still have nothing to decide.

`StrategyDiversity` asks the real question, borrowed from the parallel implementation whose
harness reports *"distinct winners across scenarios: 2/3, no single dominant strategy"*. Six
strategies across four markets:

**First run — one distinct winner out of four. Broad Menu won everywhere**, by a mile
(46,910 in the city against 27,677 for the nearest rival). **Menu breadth was free**: every
dish added found more guests something they wanted, and nothing pushed back. That is why Aaron
could not find a wrong move.

**Fixed: `Menu.ComplexityLoad`.** Four dishes are free; each one past that adds 9% to ticket
work and to how often a plate goes wrong, capped at 1.65x. A kitchen pays for breadth in prep,
in mise en place, and in what a cook has to hold in their head. Broad Menu fell from 46,910 to
27,214 and lost its crown.

**STILL FAILING, and honestly: one distinct winner out of four.** "Neighbourhood standard" —
four dishes, mid-tier supplier, 1.1x prices — now wins every market instead. The tax removed
one dominant strategy and revealed another underneath it.

**FIXED, and the cause was the same shape as everything else on this list: MENU FIT WAS
READ ONLY ON THE ORDERING SIDE.** A guest already sitting down chose a dish they liked, but
the card had no bearing on whether they came at all — so a fine-dining room and a pizzeria
drew the identical street, and specialising was strictly worse than hedging by construction.
The fifth instance of a field read on one side of the decision it exists to inform, after
`PriceSensitivity`, `IngredientQuality`, `PartiesTurnedAway` and `Employee.Skill`. **The
prediction that there would be a fifth was correct. Assume there is a sixth.**

`Menu.AppealTo(archetype)` scores a card against a sort of guest, normalised so a menu with
no opinion is exactly 1.0. It feeds two places, and both are needed:

- **How many come.** `MenuDrawAt` averages appeal across whoever is out at that hour and
  scales the street's footfall by `0.55 + avg x 0.45`. Damped deliberately — the card shifts
  traffic, it does not replace it. Without this, specialising only redistributed a fixed crowd
  and a fine-dining room at business lunch was never actually empty.
- **Who comes.** `PickWhoWalksIn` draws from the crowd weighted by appeal, in exactly one
  draw like the uniform pick it replaced, so chunk-size invariance is untouched. A truffle and
  sea-bass card fills with the people who came for truffle and sea bass.

**Result: 1 distinct winner out of 4, to 3 of 4.**

| Strategy | city | business | nightlife | suburban |
|---|---:|---:|---:|---:|
| Cheap and cheerful | 2,771 | -1,562 | 15,839 | 768 |
| Neighbourhood standard | 23,810 | **8,385** | **38,501** | 10,724 |
| Fine dining | 20,128 | -28,157 | 1,794 | **15,110** |
| High volume | -5,321 | -5,836 | -945 | -6,592 |
| Coffee and counter | -7,185 | 5,127 | -9,145 | -8,208 |
| Broad menu | **26,330** | 3,976 | 31,766 | 12,413 |

**PART OF THAT GAIN WAS A FIXTURE CORRECTION AND IS RECORDED AS SUCH, NOT CLAIMED FOR THE
MODEL.** Fine Dining ran 5 skilled cooks against 24 seats while the generalist ran 4 against
36 — so the comparison was concept against a badly-run version of another concept, and it was
losing on payroll rather than on fit. Restaffed to 4 cooks and 34 seats it goes -3,374 to
+1,794 in nightlife and wins suburban outright. **The model change alone bought 1/4 to 2/4;
the fixture correction bought the third.** Changing a fixture while hunting a better number is
exactly the trap this project has been caught in before, so: the defence is that 5 cooks for
24 covers is over-staffed on the model's own arithmetic (`PlatesPerCook` x skill), independent
of what it did to the score.

**ANSWERED, and the answer was not what either guess predicted. It is not the crowd — the
nightlife quarter gives fine dining the HIGHEST menu appeal in the entire game, 180%.** The
archetype pull is working exactly as intended. What kills it is the hours:

| Fine dining | covers/mo | $/cover | food % | of which binned | parties lost to menu |
|---|---:|---:|---:|---:|---:|
| city | 5,788 | $22 | 52% | 19,381 | 0 |
| business | 1,886 | $18 | **91%** | 21,188 | 3,898 |
| nightlife | 4,381 | $24 | 59% | 19,422 | **5,674** |
| **suburban** | 3,196 | $25 | **50%** | **5,531** | **0** |
| Neighbourhood standard, nightlife | 7,128 | $13 | **21%** | 3,414 | 0 |

**A menu that does not cover the hours you open is paid for twice** — once in the parties who
walk because there is nothing they want, and again in the stock that rots waiting for a service
it was never going to sell in. Fine dining opens a late service (23:00-02:00) with nothing
late-appropriate on the card. Suburban is its best market for the dull reason that it runs one
dinner window the menu actually covers.

**So the model is right and the reporting was not.** This is a good mechanic working correctly
and invisibly.

**`PartiesLostToMenu` has been counted since dayparts existed and the ADVISOR never read it.**
The autopsy does, but an autopsy speaks about last night rather than about what to change.
Eleven advice codes and not one said "your menu does not cover the hours you open" — which is
the single largest thing costing a strategy its own best market. **Near-miss on a sixth instance
of the recurring shape**, and the near-miss is only because the autopsy was built two commits
earlier; the actionable side was still unread.

`opportunity:menu` now fires above the room and below the runway brake, and **names the bare
service rather than reporting a number**: *"5,348 parties left without ordering — 100% of
everyone who walked in, and it is dinner and late where the card runs out. Either put something
on for that service, or stop opening for it — the stock you bought for it is spoiling either
way."*

**What this does NOT do, stated so it is not mistaken for a fix:** it tells the player, it does
not change the economics. Fine dining still loses the nightlife quarter. The open question is
now much sharper — is a 50-59% food cost correct for a premium concept, or is premium sourcing
simply not priced to survive? Note the food-cost gap is ingredient PRICE, not waste rate: both
strategies bin a similar share, but fine dining's ingredients cost four times as much, so the
same carelessness costs four times more. That is arguably exactly right.

**This is now the gate that matters more than the sweep.** A restaurant game where one build
wins everywhere is a spreadsheet with a theme.

### Forecast, commit, autopsy — the loop moved inside the game

Every number in this game was discovered by advancing time and reading what happened. The
player never committed to a belief, so they were never *wrong* in a way that taught them
anything — which is the difference between operating a restaurant and watching one.

`ServiceForecast.ForDay` works out the night ahead from the same properties the simulation
reads (traffic, reputation, menu draw, the price gate, seats, stations, plate capacity), taking
the expected value of every roll rather than rolling. `ServiceAutopsy` compares it to what
happened and names the largest cause in words. **It is a projection, not a prediction, and the
gap is the point** — a forecast that were always right would have nothing to say.

**Measured across four restaurant shapes and three seeds: median cover error 12%, worst 35%.**

Two errors on the way there, both instructive:

1. **Summing capacity across the whole night.** The first version predicted 202 covers against
   75 served, because it believed a pass with 378 plates of nightly throughput could absorb a
   dinner rush. **A restaurant cannot bank a quiet six o'clock and spend it at eight.** Worked
   hour by hour instead, so the peak is where the ceiling bites.
2. **Counting cooks and not stations.** Still 190 against 75. Three oven units cooking two of
   the three dishes on the card is a far harder limit than four cooks — a station is a physical
   object with a queue in front of it, and the pass moves at the speed of whichever queue is
   longest. Modelling the tightest station brought it to 87 against 75. **This is the same
   mistake Aaron made playing** ("I bought a ton of ovens and kept getting backed up"), which is
   a good sign the forecast is now modelling the thing that actually hurts.

`PracticalCapacity = 0.75` is the queueing haircut, and it is a real property rather than a
fudge: guests arrive in clumps, so waits build long before utilisation reaches 100%.

**`Constraint` is the most useful field on it** — "demand", "seats" or "kitchen". The three have
entirely opposite fixes and buying the wrong one is the classic way to lose here. The autopsy
stays silent when the night went to plan, held to the same standard as the Advisor: one that
always has an opinion stops being read.

**Now surfaced in the browser build**, which was the point — a forecast the player cannot see
before committing is a report, and this project already had too many of those. A card above the
tabs shows the projection, a `demand`/`seats`/`kitchen`-bound chip, and the three ceilings as
bars scaled against each other so which one is short is something you SEE. Underneath, after
the night, the autopsy. It is taken BEFORE the day advances, from the state the player last
looked at.

### THE BROWSER BUILD CAN NOW BE RUN HEADLESSLY, and it found a divergence immediately

`tools/headless.py` runs `pass.html` under the **JavaScriptCore CLI that ships with macOS**
(`/System/Library/Frameworks/JavaScriptCore.framework/Versions/A/Helpers/jsc`) — no node, no
install. The DOM is stubbed to a proxy that swallows everything; the simulation never reads
from the page, so the model half runs unmodified. `tools/probe-forecast.js` is the first probe.

**This closes a hole that has cost real time twice.** The port had drifted twice before —
invented equipment speeds, and `Markup` ported by NAME rather than by definition — and both
times the only detector was Aaron losing an evening to a broken game. The port can now be
MEASURED against the engine rather than trusted.

**It found one within minutes: the two services disagree about kitchens.**

| | C# service | browser service |
|---|---:|---:|
| Median cover error | **12%** | 18% |
| Worst | 35% | **66%** |

The same forecast, ported faithfully, is materially worse against the browser sim, and it
over-predicts *only* when the kitchen binds. The instrumented run says exactly why:

| shape | forecast | served | walked out | seated |
|---|---:|---:|---:|---:|
| balanced | 67 | 43 | 23 | 66 |
| short kitchen | 29 | 10 | 15 | **25 — the ceiling was exactly right** |

**The kitchen ceiling is not wrong. The browser sim seats those people and then loses them**,
and the forecast counts seated as served.

**A walkout term was tried and REVERTED, and that decision is the useful part.** Modelling
walkouts as a function of hourly load improved the kitchen-bound cases and made the seat-bound
one worse — it had been at 2% — and a sweep of the constant degraded the median monotonically
from 18% to 72%. **One global constant fitted across cases that fail for different reasons is
the "tune until the number pleases you" trap this project has already been caught in twice.**
The honest reading is that the two services differ in more than one place, and that is a
divergence to fix at the source rather than absorb into a fudge factor.

**So: the browser forecast is knowingly 18% out and says so in a comment.** Do not close that
gap by tuning the forecast. Close it by finding where `runDay` and `ServiceSimulation` actually
disagree — which is now a measurable question for the first time.

### Aaron's playtest, day 175 — three real defects, two of them in the forecast I had just shipped

He ran a quick sim on Suburban High Street and pasted the whole state. **12 seats, 175 days,
$5,434 cash, net -$6,966, prime cost 82% with LABOR at 56% of revenue**, and 17-28 parties a
night leaving over the wait. The forecast card and the autopsy directly contradicted each other
on screen, which is how all three were found.

**1. THE CONSTRAINT VERDICT BLAMED THE WRONG THING, AND ITS OWN BARS SAID SO.** His screen read
`seats-bound` — *"the dining room is the limit"* — directly above bars showing **pass 40 against
seats 80**, and directly above an autopsy saying *"17 parties saw the wait and went elsewhere —
the pass could not keep up."* Three readouts, one screen, two of them wrong.

The cause was the attribution logic applying the ceilings **in sequence**: seats were subtracted
first, so `lostKitchen` could never exceed `(seats - kitchen)` while `lostSeats` grew without
limit as footfall rose. **Past a certain demand the verdict always flipped to seats.** Reproduced
headlessly — at demand 70 it said kitchen, at demand 98 it said seats, with identical ceilings.
Now attributed to whichever ceiling is genuinely lowest, and it holds across the whole range.

**2. THE BUILD TAB GAVE FLATLY WRONG ADVICE: *"7 stations for 12 seats — kitchen is idle, buy
tables"*.** Wrong twice. Two of the seven were a fridge and a shelf — `kitchenCapacity().units`
counted storage as kitchen. And **stations are not fungible**: he had one second-hand deck oven
(0.75x speed) cooking two of the four dinner dishes, which allows about **8 covers an hour
against 16 the room could turn**. The kitchen was not idle; it was the bottleneck.

`bottleneck(daypart)` now finds the tightest station by covers-per-hour, weighted by the share
of the card it has to cook, and says so by name: *"the oven is the bottleneck — 1 unit cooking 2
of the card allows about 8 covers an hour, against 16 the room could turn. Another oven unit, or
a faster one, buys more than tables or cooks do."*

**Counting stations was the wrong unit all along.** A station is only as useful as the share of
the menu it is responsible for, which is the same insight that fixed the forecast's kitchen
ceiling one commit earlier — and it had not been carried across to the advice.

**3. Suggested pricing (Aaron's request).** Fifteen parties a night were deciding against him on
price before setting off and nothing on screen connected those two facts.

**Food-cost percentage could not answer it**, and that is the interesting part: his coffee ran a
4% food cost and his sea bass 20%, and **the trade's healthy 28-35% band is BLENDED across a
whole card**, not per dish. Applying it per dish would have told him to sell focaccia at $3.

`suggestedPosition()` sweeps the one thing a restaurant actually sets — where the whole card
sits against what the dishes are designed to sell for — and takes the peak, using the same
`wouldConsider` the service uses. **Scored on CONTRIBUTION, not takings.** Scoring takings put
the peak at exactly 1.00x every single time, because ingredients are paid per cover and the
price that maximises revenue is not the price that maximises what you keep.

Reported as a position with its reasoning rather than a price to copy, and it stays silent when
the current price is within 8% — per Binding Principle 5, and because a suggestion attached to
every dish every time is wallpaper.

**What his run says about balance, left as a finding rather than acted on:** labor at 56% of
revenue against a kitchen that could only ever send 8 covers an hour is the real story, and
**every one of the three defects above pointed him away from it.** He was told to buy tables he
did not need, told the room was his limit when it was not, and given no way to see that one
cheap oven was capping the whole restaurant.

### Tuning lives in one file, and a TEST is what keeps the two builds equal

`Tuning.cs` is the single named home for every number that decides how the game feels. The
classes that used to own them now reference it, so there is one C# copy rather than a dozen.

**Two independent sources landed on this.** The parallel implementation keeps a `Tuning` file
shared by its simulator and forecaster so the two cannot disagree; and Aaron brought back a
suggestion making the same point about configuration living in one place.

**But centralising the C# side is only half of it, and the smaller half.** The browser build
holds a second copy of every one of these numbers in JavaScript, and a constant that lives in
one place and is copied by hand into another is still two constants. `TuningDriftTests` reads
`web/pass.html` and fails when the two disagree — nine shared constants, plus the satisfaction
weights, plus the reputation ceiling shares summing to one. **Verified by breaking one on
purpose**: changing `PRACTICAL_CAPACITY` to 0.80 in the browser build fails the suite with
*"browser says 0.8, engine says 0.75"*.

That closes the loop the port has fallen through twice — invented equipment speeds, and
`Markup` ported by NAME rather than by definition.

**`web/pass.html` now lives IN THE REPOSITORY**, moved out of the session scratchpad. It had to,
for two reasons that turned out to be the same reason: a test cannot read a file that is not
there, and neither can another laptop. `tools/headless.py` finds it automatically.

**These stay `const` rather than loading from JSON, deliberately.** Architecture Rule 2 is about
CONTENT — recipes, furniture, equipment, events — which must be addable by writing a data file.
Tuning is not content: changing `PriceToleranceExponent` is a design decision that wants a
commit and a measurement, not a config edit.

**When porting a constant to the browser build, add it to `Shared()` in the drift test.** A
number that is duplicated and unguarded is the exact shape of every port bug so far.

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

- **The browser build is LEVEL WITH THE ENGINE as of menu fit deciding arrivals.** It carries
  `menuAppealTo` / `menuDrawAt` / `pickWhoWalksIn` and `complexityLoad`, so specialising pulls
  its own crowd there exactly as it does here, and breadth is paid for in ticket time and
  mistakes. A **"card suits the street"** readout sits under the reputation panel, averaged
  across the dayparts actually being served — green above 105%, red below 90% — because a
  mechanic the player cannot see is one they cannot play around. Both terms are in the hover
  glossary.
- **It was previously level as of arrival-side pricing.** It carries
  `wouldConsider` (who turns up at all, reputation-adjusted) and the city-shaped
  `MENU_AT_DOOR` remainder, so raising prices there thins the crowd before it arrives rather
  than filling the log with people storming out.
- **It was previously level as of the price-resistance change.** It carries
  the squared tolerance curve and the probabilistic walk-away, so pricing behaves there the way
  it behaves here. **Sync it before asking for a playtest** — testing a model the engine no
  longer has produces feedback about nothing, which nearly happened.
- **The browser build was previously level as of storage capacity.** It
  carries every system: awareness, staff with claimed-versus-actual skill, mistakes and comps
  by dish complexity, category menu engineering, the pantry with dated batches and FIFO,
  pay-on-delivery, freshness on the plate, the standing order, and storage caps with fridges
  as floor-consuming equipment. The Pantry tab shows freshness as a percentage, **how much
  turns within two days** (the thing you need in order to reorder before the hole appears), a
  per-ingredient **toss** button, and a switch for the standing order.
- **The browser build was previously synced as of the pantry work.** It now carries
  spoilage and pay-on-delivery too, which meant giving it an inventory at all — it had none,
  so ingredients were effectively infinite and free. There is a **Pantry** tab showing stock,
  what each thing keeps for, how old the oldest batch is (red when it is about to turn) and
  the measured run rate, with two deliberately contrasting buttons: *order what we need* and
  *fill the walk-in*. The books show what has been binned.
- **Verifying a JS port without node: strip `//` comments before counting braces.** A naive
  scanner treats the apostrophe in a comment like "tomorrow's" as opening a string and swallows
  every brace after it, which reads as a syntax error that is not there. Cost a false alarm.
- **The browser build was synced as of the staff work.** It now carries the
  awareness split, staff as people with claimed-versus-actual skill, mistakes and comps scaled
  by dish complexity, and category-based menu engineering. Two readouts were added to the pass
  so the new systems are visible while playing: **"heard of you"** (awareness) and
  **"kitchen"** (brigade skill out of five). Hiring is a list of applicants with what their CV
  reads as, and the Team tab shows who turned out better or worse than they claimed.
- **The port can now be measured, not trusted: `python3 tools/headless.py <probe.js>`.** See
  the headless section above. Write a probe rather than reasoning about whether a port is
  faithful — the first one found a divergence that had been invisible for the whole project.
- **A browser port of the sim is a playtest instrument, and it WILL drift.** `pass.html`
  reimplements the rules in JavaScript so the loop can be felt rather than read. Two drift
  bugs appeared within a day of writing it: the equipment table had invented footprints and
  speeds, and `Markup` was ported as price-over-ingredient-cost when the real definition is
  price over the price the dish was DESIGNED to sell at. The second one made every guest
  balk at every price, because a healthy 30% food cost is a 3x price/cost ratio and the port
  read that as gouging. **When porting, copy the definition, not the name** — and re-derive
  every constant from the C# rather than from memory. The C# core is the source of truth.
- **THE FAST PLAY LOOP IS THE POINT OF THE BROWSER BUILD, and Aaron said so explicitly after
  seeing the parallel implementation's harness:** *"I like that I can quickly play through, get
  results and feedback given to me, and then I can give that feedback to you."* His judgement
  was that Howard's game is the more flawed of the two and the loop around it is still the
  better instrument. **So the thing to protect is time-to-feedback, not fidelity** — a build he
  can run five configurations through in ten minutes finds more than a more accurate one he
  plays once. Nothing to build for it yet (he was clear it is not a request), but it is the
  standard to judge harness work against.
- **Tests protect what is already right; playing is what finds what is wrong.** 182 tests found
  none of the four defects that actually mattered — the walkout death spiral, the price-gouging
  exploit, the equipment/cook ratio, and the false win rate. All four came from Aaron playing.
  **The ratio of effort should keep shifting toward playing from here.** Tests are still written
  first for exit criteria and still pin every fix, but they are a ratchet, not a search.
- **PUSH AFTER EVERY COMMIT. Aaron asked for this explicitly:** *"I want it to be able to
  update all the time when we make updates."* That is standing authorisation for `git push` —
  it does not need asking each time. The remote is `github.com/aspector57/RestuarantEmpire`
  and the credential lives in the macOS keychain, so pushes are silent.
  **Commits still only happen when work is actually finished and tested**, which is unchanged;
  what this removes is the separate step of asking before publishing them.
  If a push ever fails on auth, the token has expired or been revoked — do NOT ask for a token
  in chat. See the note below.
- **NEVER ACCEPT A TOKEN PASTED INTO THE CONVERSATION.** It happened twice here, and both were
  burned the moment they were sent — a chat transcript is not a secret store. The correct
  recovery is always: Aaron runs ONE command in his own terminal with his own token, which
  never passes through here.

      printf "protocol=https\nhost=github.com\nusername=aspector57\npassword=<TOKEN>\n\n" | git credential approve

  The better setup is `gh auth login`, a browser handshake with nothing typed or pasted at all.
  It needs `gh`, which needs Homebrew, neither of which is installed on this machine — worth
  doing when convenient, not worth blocking on.
- **Write the test first**, especially for the exit tests above. M0 is verified by tests, not by playing.
- **Keep the simulation core free of presentation concerns.** Read surfaces (Dashboard/Advisor) are one component and are a lens over state — never a source of truth.
- **When the design doc and an implementation convenience conflict**, raise it rather than quietly diverging — several rules here exist specifically because a well-known game got them wrong.
- **Prefer small, verifiable increments.** Do not scaffold M1+ systems "while we're in here."
