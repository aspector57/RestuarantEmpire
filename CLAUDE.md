# Restaurant Empire Successor

A restaurant management/tycoon game. Full design rationale lives in `docs/design.md` — read the relevant section before implementing anything, but do not load the whole document unless you need to.

**American English.** US spellings throughout (`Neighborhood`, `labor`, `center`,
`cannibalize`, `specialize`), dollars, and square feet — including in identifiers and data IDs,
not just prose.

**THIS IS A LANGUAGE RULE, NOT A SETTING RULE.** An earlier wording said "Setting: American...
overseas expansion is a later feature", and it was read (by me) as deferring non-US content on
purpose. Aaron: *"I meant to use like american english."* Restaurants in Lyon or Florence are
fine and always were — they are simply written with US spellings, like everything else here.

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

### Aaron's balance bar, and it changes how every strategy result must be read

> *"You should be able to win with any concept anywhere if you run the restaurant properly,
> unless the concept just totally sucks."*

**This makes an underperforming strategy a QUESTION rather than a finding**, and applied to the
fine-dining result it dissolved it entirely. The probe was running that concept badly on two
counts, neither of which is a property of fine dining:

| Fine dining, nightlife quarter, dinner only | net/month |
|---|---:|
| 1.35x (what the probe used) | 15,428 |
| 1.60x | 32,668 |
| **1.90x** | **38,484** |
| 2.20x | -9,639 |
| *Neighbourhood standard, same market* | *38,866* |

Closing the late service it could not feed: **2,166 -> 15,428**. Pricing premium ingredients
like premium ingredients: **-> 38,484, within 1% of the generalist.** Fine dining is competitive
in its own market when run properly. It was losing to operating mistakes.

**THE INSTRUMENT IS THEREFORE MEASURING THE WRONG THING.** `StrategyDiversity` hands every
strategy a fixed price multiplier and a fixed set of hours, so it compares concepts run at
arbitrary settings rather than concepts run well. Under Aaron's bar the honest question is
*"what is this concept's best result in this market?"* — each strategy tuned to its own optimum,
then compared. **Do not read the existing distinct-winner count as a balance verdict until that
is fixed**; it currently rewards whichever concept happens to suit the fixture's defaults.

Two things worth keeping from the measurement regardless:

- **A menu that cannot feed its hours is paid for twice** — the parties who walk, and the stock
  that rots behind them. Now surfaced as `opportunity:menu`.
- **The price cliff above the optimum is steep**: 38,484 at 1.9x to -9,639 at 2.2x. An interior
  optimum exists, which is the design goal, but the fall past it is sharp enough to be worth
  watching — a player who overshoots gets very little warning.

### Drinks and the liquor licence — the answer to the late-service problem

Aaron: *"we also probably want to add like alcohol and wine, maybe you need to buy a liquor
licence to sell it? idk what do you think?"* Built, and it is the right thing to have built
before more dishes, for a reason the strategy probe had already handed us.

**Fine dining could not run a late service because there was nothing on the card anyone wanted
at one in the morning — and that is TRUE, not a content gap.** Nobody orders sea bass at
midnight. They order drinks. So a drinks list is not more content; it is the thing that makes a
daypart exist.

**Drinks are ADDITIVE, and that is the whole design.** A guest orders one ALONGSIDE their food,
never instead of it. A drinks list that merely competed for the same order would cannibalise
the kitchen rather than lift the check, which is backwards.

| One month, nightlife, same food menu | covers | $/cover | food cost |
|---|---:|---:|---:|
| No bar | 4,775 | $13.43 | **37%** |
| Licensed, wine and cocktails | 4,949 | **$22.39** | **28%** |

**Spend per cover +67%, and the food-cost ratio falls into the healthy band.** That is exactly
how the trade solves the premium concept: the wine programme blends the kitchen's cost down.
Without it the only lever an expensive restaurant has is charging more for food, and the
measured cliff past the price optimum is brutal (38,484 at 1.9x to -9,639 at 2.2x).

**And it makes a late service worth opening**, which is what it was for:

| Dinner + late, nightlife | covers | revenue | found nothing |
|---|---:|---:|---:|
| Kitchen only | 4,576 | $76,733 | **2,506** |
| With a bar | 5,941 | **$135,227** | **0** |

**THE FIRST VERSION OF THAT BARELY WORKED, AND THE WEAK TEST NEARLY HID IT.** Parties lost went
2,506 -> 2,498 and the assertion said only "fewer", so it passed. The cause: a late crowd still
counted as finding nothing because no FOOD suited the hour. **People come out just to drink**,
and a bar that cannot seat someone who only wants a drink is not a bar. When the kitchen has
nothing for the hour but the bar does, they now sit down anyway. **An assertion that only checks
direction will pass on noise — say what the number has to BE.**

**The licence is a capital gate, Binding Principle 4 in its cleanest form:** $6,500 up front
against an opening bankroll of 18,000-27,000, plus $340 a month whether you sell a drop.
Refused outright when the cash is not there. It is not a tax — it is what gives the decision
weight, and a quiet restaurant holding a licence is bleeding.

Margins are the trade's: house wine 25% cost, cellar wine 27%, cocktails far lower — which is
why bars love cocktails and why prep minutes, not ingredients, are their real constraint.
`bar` joins the station list with three tiers like every other station.

**Two brittle tests were fixed rather than updated, and the distinction matters.**
`TheShippedContentFiles_LoadCleanlyWithNoWarnings` asserted exact counts (13 ingredients,
7 recipes), so it broke the instant any content was added — **hostile to the very rule it
exists to protect**, since Architecture Rule 2's claim is that new content needs no code
change. It now asserts the invariant: nothing silently dropped, every ingredient resolves,
every supplier can quote every ingredient. **A test that fails when you do the thing it is
meant to permit is a bad test, not a passing content change.**

**Ported to the browser build**, and the two agree: spend per cover $13.45 -> $20.60 and food
cost 31% -> 27% there, against $13.43 -> $22.39 and 37% -> 28% in the engine. Same direction,
same magnitude, from two separate implementations.

**A false finding was caught by the headless probe, and the cause is worth remembering.** The
first run showed spend per cover FALLING with drinks (12.91 -> 12.25), the opposite of the
engine. The probe had never bought storage — `COLD_BASE` and `DRY_BASE` are 0 — so the standing
order silently refused every delivery and the food-only restaurant starved: sea bass hit zero
on day five, everything by day thirty. **A fixture with no fridge does not measure a menu, it
measures a famine.** Two instruments disagreeing is a bug report, and this time the bug was in
the instrument.

### Pricing the whole card in one go (Aaron)

> *"we should build in like a button on the menu for use suggested price, because if we are
> going to make a robust drink menu and food menu that would be a lot to price."*

Right, and pricing sixty dishes by hand is the **micromanagement tax on good decisions**
anti-pattern precisely. But a one-click OPTIMAL button would solve the menu for the player,
which Binding Principle 5 forbids.

**The resolution is that the real decision is WHERE THE CARD SITS, not sixty separate numbers.**
So: *"Price everything at 1.10x"* and *"Back to designed"* set the position in one action, and a
per-dish *"use $15.40"* appears only where a dish is more than 8% off it. The player still
deviates dish by dish where they have a reason — **which is what menu engineering actually is.**
The boring part is automated; the interesting part is not.

### Aaron's playtest of the drinks build — three defects, one of them the bad kind

> *"the menu jumps around, now there is also more alcohol than anything else, I also didn't see
> the option to buy the liquor licence, I just added the drinks to the menu."*

**1. SILENT FAILURE, and this is the one that matters.** He put alcohol on the card without a
licence and **nothing said a word** — the drinks simply never sold, with no explanation anywhere.
A dish that cannot be served must say so where the player is looking at it, not fail quietly at
service. Every licensed item now carries its own line: *"Needs a liquor licence before it can be
sold"*, or, if it is already on the card, *"On the menu, but it cannot be sold."*

The licence card existed, but it was below the whole dish list where he never scrolled. **A gate
the player cannot find is not a gate, it is a bug.**

**2. The menu jumped around, and it was not the sort.** The price slider ran `render()` on every
`input` event — which fires continuously through a drag — so each pixel of movement tore down
the entire list and rebuilt it. The row moved out from under the cursor and the slider lost
focus. Now the readout updates live and the rebuild happens on `change`, when the drag ends.
**A full re-render is not free just because it is correct.**

**3. Four drinks in a seven-dish catalogue read as a bar that serves food.** `menuOrder` sorted
by daypart then price, which put a Negroni between two mains. The card is now GROUPED BY COURSE
— small plates, mains, sides, desserts, drinks — which is both the fix and **the first half of
the course structure a large catalogue needs.** Grouping is what makes a big menu legible;
without it, hundreds of dishes is just a longer list.

### Jump-ahead now stops, and the browser build finally has an Advisor

> *"loved the advisor letting me know that... then recommended things to do to fix it, that is
> good. The only time I got this though was when I clicked sim until something happens — if I
> did 1w, 1m, it would not stop me. Also we need more insights to be able to make changes."*

**1. THE FAST-FORWARD LOOP WAS ONLY HALF WIRED.** The day buttons passed `stopOnInterrupt:
false` and ran blind through every problem; only "run until something happens" ever stopped.
That is the M1(a) mechanism bar failing in the build a human actually plays — **jumping a month
is a bet that nothing needs you, and the game owes you the moment that bet is wrong.** All jumps
now stop, and say how far they got: *"Stopped after 4 days of 30 — something needs you."*

**2. THE BROWSER BUILD HAD NO ADVISOR AT ALL.** Zero occurrences. It had INTERRUPTS (something
broke, here is the fix) and nothing that answers *"what should I work on?"* — which is exactly
the gap Aaron named. An interrupt speaks when something breaks; an advisor speaks when you ask.

Ported with the three rules that make it advice rather than a readout: it **never names the
mechanism**, it **says what matters first** (a correct list in a ruinous order is worthless),
and it is **allowed to say nothing** — a healthy restaurant gets *"Nothing needs you right now.
Trade on."* Where there is one obvious purchase it offers the button inline, so the decision
stays the player's and only the walking is removed.

**The runway brake came with it**, and it is the subtle part: below two months of money the
suggestions that SPEND are suppressed, while everything free — the menu, the prices, the
standing order, letting someone go — still gets said. Suppressing all spending is a death trap
(poor because small, then advised to stay small); suppressing none tells a restaurant three
weeks from closing to buy an oven.

**A guard worth keeping: the Advisor reported "669% of the food bill has gone in the bin."**
The probe's pantry was loaded directly rather than bought, so the denominator was near zero.
**Nonsense in one line destroys trust in every other line**, so shares are now clamped to 1 and
suppressed below a meaningful denominator. Third fixture bug this session found by the headless
probe (after the missing fridge and the storage-less starvation) — **the pattern is that a
fixture which skips the game's own economy measures something that is not the game.**

### The licence could not be found because HALF THE MENU TAB WAS CRASHING

Aaron, twice: *"I didn't see the option to buy the liquor licence"*, then *"where do I buy the
liquor licence? I can't find it."* The first time this was treated as a PLACEMENT problem and
warnings were added to the dishes. **That was the wrong diagnosis and the second report is what
forced a real look.**

**`wrap.appendChild(pc)` referenced a variable that never existed.** `card()` appends to
`#panel` itself and returns the node; there is no wrapper. So the line threw a `ReferenceError`
**immediately after the dish list**, and everything below it — the pricing controls, the licence
card, the sourcing switcher — never rendered at all. Shipped in the drinks commit and live for
two sessions.

**The brace counter said the file was fine, and it was right and useless.** Balanced braces do
not mean the page runs. That check has been the only verification the browser build had, and it
cannot catch a ReferenceError, a typo'd property, or a null dereference.

**`tools/probe-panels.js` closes that hole**: it renders every panel, licensed and unlicensed,
and reports which throw. Run against the shipped build it says exactly what the player
experienced:

    THROW panelMenu (unlicensed) -> ReferenceError: Can't find variable: wrap
    THROW panelMenu (licensed)   -> ReferenceError: Can't find variable: wrap

**RUN IT AFTER ANY UI CHANGE.** The pattern this session is consistent and worth stating
plainly: every browser-build defect that reached Aaron was invisible to static checking and
obvious the moment something actually executed the code.

The licence card is now also a reusable `licenceCard()` built at the TOP of the menu, so its
position comes from call order rather than from where it happened to be written.

### Sales over time, and why the menu matrix could never answer it (Aaron)

> *"it would be nice to see how things are selling over time, I guess the plowhorse thing is
> meant to do that."*

**It is not, and that is the useful distinction.** Kasavana-Smith is a SNAPSHOT: it says what a
dish IS right now, on two axes. It has no history in it at all, so it cannot answer *"what did
my last decision do?"* — which is the question a player actually has after moving a price. A
classification with no time in it has no causes in it either.

Each dish now carries its own sparkline, a run rate, and the direction of travel — **and, more
importantly, what you did to it.** Every price change, bulk repricing, and menu add/remove is
recorded against the day it happened, so the trend reads:

    Margherita   21.3/day -> 10.7/day   down 39%   after you price set to $28.00, day 20
    Caprese      30.1/day               steady     no changes lately — this is the market moving

**That second line is the half that makes it honest.** Without it, every wobble looks like
something the player caused. Separating *your doing* from *the market moving* is what turns a
chart into evidence — and it is Binding Principle 2 ("every outcome must trace to a specific
named cause") in its cheapest working form.

Deliberately not built: a full charting view. A sparkline on the row you are already looking at
answers the question where it is asked; a separate analytics tab would be somewhere else to go.

### "I made a bad decision early and couldn't really bounce back" (Aaron)

Two things he has said pull against each other, and this is where they met:

> *"the math needs to be realistic but we want to be able to win. It shouldn't be easy but it
> should definitely be doable."*
> *"you should be able to win with any concept anywhere if you run the restaurant properly."*

**The cause was concrete: EQUIPMENT COULD NEVER BE SOLD.** Every capital purchase was one-way —
cash went in and never came out. Furniture could already be cleared (`Clear 10 covers — recover
$480`), but equipment could not, and **buying too much kitchen is precisely the mistake players
actually make.** Aaron's own first playtest was *"I bought a ton of ovens and kept getting
backed up."* Four ovens at $1,600 was $6,400 gone forever, in a game that opens with 18-27k.

`sellEquip` returns **45%**, and the loss is the entire point:

    bought 4 ovens ($6,400)  ->  sold 3 back ($2,160)  ->  the mistake cost $4,240

Getting it all back would make every purchase risk-free and delete the decision. Getting nothing
back makes one early error terminal, and a game decided in its first ten minutes is not a
management game. **A haircut turns a mistake into a cost you can trade out of**, which is
exactly "hard but doable".

One guard: it refuses to sell the last unit of a station the menu still needs, naming why —
that is not a recovery, it is 86'ing half the card by accident.

**What this does NOT do, and should not be mistaken for a balance fix:** it adds a LEVER, not
easier numbers. Whether the early game is now recoverable is a measurement nobody has taken.
The honest next step is a probe that deliberately makes a bad opening decision and asks whether
the run can be saved — the same shape as `CanFineDiningWinItsOwnMarketIfRunProperly`, which
dissolved a "finding" that turned out to be an operating mistake.

### The death spiral, diagnosed from Aaron's day-149 log — and the game caused it

His log is the clearest evidence this project has produced, because the same three lines repeat
for seventeen days:

    d132  Bought a Second-hand Deck Oven — $1,600     ...  23 walked out after waiting
    d133  Bought a Prep Bench — $700                  ...  32 walked out after waiting
    d134  Bought a Second-hand Deck Oven — $1,600     ...  35 walked out after waiting
    d141  Bought a Second-hand Deck Oven — $1,600     ...  29 walked out after waiting
    d145  Bought a Prep Bench — $700                  ...  41 walked out after waiting
    d147  Bought a Second-hand Deck Oven — $1,600     ...  43 walked out after waiting
    d149  Bought a Prep Bench — $700                  ...  39 walked out, 21 covers served

**Seven purchases, roughly $8,500, and walkouts never fell below 23.** More guests walked out
after sitting down than were served. **He was doing exactly what the game told him to**, and the
game was wrong.

**BUG 1: the interrupt blamed a station without ever checking whether the BRIGADE was the
limit.** `busiestStation()` returns the busiest station unconditionally, so the interrupt always
named equipment and always offered another one — with a button on it. His constraint was two
cooks at 1.2/5. **No quantity of ovens fixes a shortage of hands**, and the stations he kept
buying stood idle waiting for someone to work them.

The forecast already had this right: `kitchenThroughput` takes `min(tightest station, brigade)`.
**The component that knew was not the component with the button.** `passLimit()` is now the
single shared answer, used by both, and the interrupt says:

> *"It is not the equipment — it is the people. 2 cooks at 1.3/5 can move about 28 covers an
> hour between them, and the stations are sitting idle waiting for hands. Another oven would
> change nothing."* — and offers only **Go to hiring**.

**BUG 2: MY RUNWAY BRAKE SILENCED THE DIAGNOSIS, NOT JUST THE SPENDING.** Below two months of
money it dropped the kitchen advice entirely, so his Advisor read **"1 thing"** — the runway
warning — while the pass strangled him unmentioned. **The moment a restaurant got into trouble,
the Advisor stopped explaining why.** That is precisely backwards, and it left the interrupt
(which had no brake) as the only voice, still selling him ovens.

The brake now removes the ASK and never the cause: no buy button, and the advice is reframed as
something to fix without spending — *"there is not the money to hire right now, so the move is
to shorten the card or the hours until it can be worked with who you have."*

**The wider lesson, and it has now cost a full playthrough: two components answering the same
question from different data will eventually disagree, and the player believes whichever one has
a button.** Same shape as the C#/JS drift. Any question worth answering twice should be answered
once and shared.

`tools/probe-spiral.js` reproduces his position and asserts the advice flips between "people"
and "equipment" as the brigade changes.

### A PARTIAL FIX READS AS A FINISHED ONE — the same bug, one commit later

The previous entry ends with *"any question worth answering twice should be answered once and
shared."* It was written, committed, and then **applied to two of the three callers.** Aaron's
next run showed both answers on one screen:

> Forecast: *"kitchen-bound — the pass is the limit, we could seat more than we can cook"*
> Build tab: *"the room is the bottleneck. **Buy tables before anything else.**"*

`balanceNote()` was still on the old station-only `bottleneck()`, which ignores the brigade —
and he had **one cook**. `bottleneck()` is now deleted rather than left available, because a
helper that gives the wrong answer will be called again by whoever writes the next panel.

**Two more false alarms in the same screen, both mine:**

**The runway alarm was wrong, and it was suppressing good advice.** `monthlyBurn()` counted
gross outgoings and ignored takings entirely, so a restaurant turning $11,314 a month against
$11,640 of costs — **roughly break-even** — was told it had "less than two months of money
left", and the brake then stripped the buy buttons off everything. Runway is how long cash
lasts at the current rate of LOSS; a restaurant that is not losing money has no runway problem
however large its wage bill. Measured from the last fortnight's actual trading: **10.4 months,
not two.**

**"We are open late with nothing late people actually want"** fired on a **18:00–23:00 dinner
service**, because the test was `w.to > 22`. Closing at eleven is not trading into the small
hours. It now checks for a window that genuinely wraps past midnight.

**The pattern across all three: every one was a readout confidently telling the player something
false.** A wrong number is worse than a missing one — it is acted on. `tools/probe-agreement.js`
now reproduces his position and asserts the three capacity views agree.

### "12 tables" was 12 SEATS — a labelling failure that cost a whole playthrough

> *"what I'm showing is that we only have like 12 tables in there and can't keep up with it,
> but I guess each table has like 4 seats or something?"*

He had **twelve seats — about three tables.** He had been playing as though he had twelve
tables and roughly fifty covers. **Every capacity readout was correct and every one was being
read as four times its real size**, which makes every downstream judgement wrong: the room looks
big, so the kitchen must be broken, so buy another oven.

The model counts COVERS because parties are counted in people, and that is right. The label has
to say so. `seatsAndTables()` is now used everywhere: *"12 seats — about 3 tables of 4"*, the
header carries `12 (~3 tables)`, and furniture reads *"+10 seats (about 3 more tables)"*.

**A correct number with an ambiguous unit is a wrong number.** This is the third defect in three
sessions where the simulation was right and the readout misled — after the seats/kitchen
contradiction and the false runway alarm.

### And the follow-up question was the right one: could one cook keep up with twelve seats?

> *"well if it is only 12 seats, they should be able to keep up with the demand right?"*

Measured rather than assumed, 20 days each:

| 12 seats, one 5-hour dinner | covers/day | walkouts |
|---|---:|---:|
| **1 cook, 3 second-hand ovens** (his) | 38.6 | **11.4** |
| 2 cooks, 3 second-hand ovens | **56.8** | **1.4** |
| 1 cook, 1 second-hand oven | 25.4 | 5.6 |
| 1 cook, 1 fast oven | 50.0 | 3.2 |

**The arithmetic holds up.** A seat turns every 45 minutes, so twelve seats offer ~80 seatings a
night. One cook holds two plates; a margherita on a 0.75x oven takes 12 minutes — 10 plates an
hour against a room turning 16. **He had three ovens and one cook, so two ovens stood idle.**

Either a second cook or one faster oven fixes it; a third second-hand oven never could. **The
model is defensible and the intuition was half right** — the room was not the problem, but even
that small room needed more than he had. Kept as `tools/probe-onecook.js`.

### Day one told every new restaurant it was dying — four more readout bugs

Aaron's fresh save, **zero covers served**, before opening:

**1. "There is less than two months of money left."** With no trading history `monthlyBurn()`
falls back to gross outgoings, so **every game began with a death warning before a single cover
was served** — and the brake riding on it stripped the buy buttons off the advice a new
restaurant most needs. You cannot be running out of money at a rate nobody has measured. The
alarm now waits for five days of trading.

**2. "Kitchen units 4"** on one oven and one prep bench — it was counting the fridge and the
shelving. **This is the storage-counting bug fixed in `balanceNote` and missed here**, which is
the third partial fix to ship this session. **When a wrong helper is found, grep every caller
before calling it done.** Now reads *"Cooking stations 2 (storage not counted)"*, with the
brigade on its own line and the grammar repaired (*"1 cook work 2 plates"*).

**3. The Advisor was silent about the kitchen on day one** because its capacity advice was gated
on trading history — while the Build tab said the oven was the bottleneck. The forecast never
needed that data, so neither should this. **The cheapest moment to fix an under-built kitchen is
before it has cost you anything.**

**4. Capacity readouts were stock-dependent.** `passLimit` filtered through `menuFor`, which
checks `canCook` — right for service, wrong for capacity. Being out of tomatoes this morning
does not change what the kitchen is capable of, and mid-restock the Build tab would announce
*"no kitchen at all"*. `cardFor()` answers the structural question; stock is a separate problem
with its own readout.

**Four sessions running, the simulation has been right and the READOUTS have been wrong.** Every
defect Aaron has hit recently is a display or advice bug, not a model bug. That is worth taking
as a standing hint about where to look first — and `tools/probe-dayzero.js` now pins the opening
state, which nothing had ever checked.

### The same cook described as doing 19 covers an hour in one place and 14 in another

> *"Is a cover a seat? bc if so, they should be able to serve 12 seats with 2 people right?"*

**A cover is a seat is one person, and the question was nearly right.** But two readouts on his
screen disagreed about the same cook: the interrupt said **19 covers an hour**, the Build tab
said **14**. `passLimit()` returns theoretical throughput and each caller was expected to apply
`PRACTICAL_CAPACITY` itself. One did, one forgot.

**If two readers can disagree by forgetting a multiplier, the multiplier belongs behind the
door.** `allows` is now the figure to quote, haircut already applied; `raw` is kept for anything
that genuinely wants the theoretical number. This is the fourth instance this session of one
question having two answers, and the fix is always the same: **make the shared thing return the
answer, not the ingredients for it.**

**Both now also state what they are comparing against**, because "14 covers an hour" means
nothing without the room beside it: *"1 cook moves about 14 covers an hour, and 12 seats turn
about 16 an hour."*

**The measured answer to his question**, 20 days each:

| brigade | cook does/hr | room turns/hr | covers/day | walkouts/day |
|---|---:|---:|---:|---:|
| **1 cook** | **14** | 16 | 37.6 | **13.8** |
| **2 cooks** | **20** | 16 | **57.0** | **2.0** |
| 3 cooks | 20 | 16 | 57.6 | 1.6 |

Twelve seats with a 45-minute sitting turn ~16 covers an hour; one cook does 14. **A small gap
producing most of the walkouts, because arrivals clump and the queue never clears.** Two cooks
closes it; a third is wasted because the ovens become the limit — so there is a real right
answer, and it is legible. Kept as `tools/probe-onecook-vs-room.js`.

### A SEAT YOU CANNOT FEED IS WORSE THAN NO SEAT AT ALL

The Advisor's room advice said *"tables earn more than another cook would"* and had no button.
Adding one was obvious. **Measuring first was what stopped it shipping**, 30 days per
configuration, fresh state each time, same kitchen throughout:

| seats | room/hr | pass/hr | covers/day | walkouts | turned away | revenue/day |
|---:|---:|---:|---:|---:|---:|---:|
| **12** | 16 | 20 | **68.8** | **6.1** | 33.2 | **$717** |
| 20 | 27 | 20 | 56.8 | 26.2 | 0.7 | $582 |
| 60 | 56 | 20 | 56.5 | 26.4 | 0.0 | $579 |

**Adding tables made it worse — 68.8 covers a day down to 56.8.** A guest turned away at the
door costs the sale. A guest who sits down and gives up costs the plate you cooked, the food,
the table they held while waiting, and your reputation — and burns kitchen capacity on plates
nobody pays for. **The model is right and the advice was wrong.**

**AARON'S NEXT RUN CONFIRMED IT LIVE, which is the part worth keeping.** He went 12 -> 32 seats
on day 283 and the log reads: **79 covers and 3 walkouts, then 55, 38, 35 covers with 33, 44, 50
walkouts.** He hired two cooks on day 287 and it recovered to 84-93. The measurement predicted
his playthrough three days before he played it.

**The real trap is granularity.** His headroom was ~4 covers an hour; the smallest block of
tables adds 13. So the advice now only offers seats when the kitchen can absorb a whole block,
and otherwise says so plainly — *"that sounds like a reason to buy tables and it is not... lift
the kitchen first, then the room."*

**And a dead button, found on the same screen.** At 14 sq ft free the interrupt correctly
withheld the oven purchase; the Advisor offered it anyway, and clicking did nothing because
`buyEquip` refuses silently. **A button that does nothing is worse than no button.** It now
checks floor — and when there is none, it surfaces the move the equipment ladder was designed
around and which nothing had ever pointed at:

> *"There is no floor left for another — but a Stone Hearth Oven runs at 1.6x against the 0.75x
> you have now and takes 20 sq ft LESS. Sell one of the old ones and put that in its place."*

**Premium being faster AND smaller has been in the catalogue since M1 and was undiscoverable**
while every readout pointed at buying another cheap box. Offered as one action, since selling
and re-buying is two moves the player would have to infer.

### The rail is an audit log, and `tools/playthrough.js` replaces most of the playtesting

> *"we should have literally everything in here, price changes, food added, etc, this way you
> have a complete audit log."*
> *"we need a way for you to see everything and how it impacts the gameplay so we can reduce
> testing time."*

**The rail recorded what the NIGHT did and never what the PLAYER did.** Price changes and menu
edits went into `G.decisions` for the sales-trend attribution and surfaced nowhere the player
reads — **a log of consequences without their causes cannot be audited.** Every decision now
lands there too, the cap went 90 -> 400 entries, and four filters make it usable:
*Everything / What I did / Trade lost / Money*.

**`tools/playthrough.js` is the bigger one.** It plays a full 240-day run acting only on what
the Advisor says, prints a transcript, and **asserts invariants every single day** — each one a
bug that actually shipped this session:

- the forecast and the Build tab must not disagree about what is binding
- every button offered must be affordable AND fit on the floor
- never recommend equipment while the brigade is what is short
- never recommend seats the pass cannot feed
- a share cannot exceed 1
- a solvent trading restaurant must not be told it is dying

**Six of the last eight defects were of exactly these shapes**, and none were simulation bugs.
They are mechanically checkable, so a machine should find them rather than Aaron losing an
evening. Current state: **no contradictions across 240 days.**

**It immediately found something worse than the bugs it was built for.** The Advisor said
*"nothing needs you"* for two hundred of those days while the restaurant sat on 12 seats, one
cook and **14.6 walkouts a night**, standing sliding to 38. Every capacity rule was inside its
threshold, so nobody spoke — **while a seventh of the guests were leaving without eating.**

`walkouts` now fires on what is HAPPENING rather than on ratios: *"about 24 in every 100 who sit
down are leaving before they eat."* **Thresholds describe the shape of a restaurant; walkouts
describe what is going on inside it, and when they disagree, believe the walkouts.**

It also had to carry the FIX, not just the diagnosis — the harness showed it firing for 220 days
straight with no action attached, so an obedient player following it changed nothing.

**Result, same seed, same opening, following the advice:**

| 240 days | before | after |
|---|---:|---:|
| Cash | $46,841 | **$86,528** |
| Covers/day | 48.6 | **76.9** |
| Walkouts/day | 14.6 | **5.1** |
| Standing | 38 | **56** |

**Still open, and the harness names it:** the run says `[roomtight]` for 220 consecutive days
and finishes on 12 seats with $86,528 in the bank. The advice is correct — the kitchen cannot
feed more seats — but it never suggests growing BOTH together, so a rich restaurant stays tiny.
**The Advisor can only ever recommend one lever, and some problems need two.**

**Also fixed:** prime cost read **346% on day one**, because food is paid on delivery and the
opening order lands against a single night's takings. True and meaningless. It shows `—` until a
week of trading catches up, same as the runway alarm.

### Testing the whole game without playing it — four harnesses

Aaron: *"we need you to be able to test like everything — price changes, hiring, adding seats,
lowering costs, upgrading and downgrading our ingredients"* and *"simulate a year but be able to
look at daily logs, and give the changes enough time to breathe."*

| harness | question it answers |
|---|---|
| `probe-panels.js` | does every screen still render, licensed and unlicensed |
| `playthrough.js` | 240 days played by the Advisor, with contradictions asserted daily |
| `levers.js` | one lever at a time across its range — real decision, trap, or just a purchase |
| `scenario.js` | a year of a RUNNING restaurant, changes 45 days apart, each given time to settle |

**`levers.js` asks the design question, not the balance one.** A lever whose best setting is at
the top of its range is not a choice, it is a purchase you make when you can afford it; one
whose best is at the bottom is a trap. Only an interior optimum asks the player anything —
"flat scaling: bigger numbers are not new decisions" is on this project's own anti-pattern list.

**It found a real bug in its first run: `monthlyBurn()` charged `HOURS_PER_SHIFT` (8) for a
five-hour dinner service**, overstating wages by 60%. The runway therefore looked far shorter
than it was, and the brake stripped buy buttons off advice a healthy restaurant needed. `runDay`
had it right all along — **only the estimate was wrong, which is the same shape as every other
divergence this session.** My own harness made the identical mistake, so it now takes the labour
the simulation booked rather than recomputing it: **a harness that recalculates what the game
already knows is a second implementation waiting to disagree.**

**FINDING, NOT YET FIXED: more cooks makes things WORSE.** 2 cooks -> 50.6 covers/day; 3 cooks ->
43.7; 6 cooks -> 39.5, with walkouts climbing 34.8 -> 54.0. Extra hands fire more tickets into a
station that is already the constraint, so waits grow and guests leave — and the abandoned
plates burn the very capacity that was short. **Adding capacity should never reduce output**, so
the pass is missing any sense of pacing. This is the largest open model defect and it is
measurable now, which it was not before.

**`scenario.js` answers what a snapshot cannot: how long before you can tell?**

    hire a second cook     $340/day after a fortnight, $782/day two months on
    Stone Hearth Oven      50 -> 74 covers, walkouts 15.9 -> 2.9
    premium ingredients    standing 49 -> 55, profit slightly down at first
    back to budget         standing 60 -> 53 and falling, profit up immediately

**The budget/premium pair is the designed arc working**: cheap ingredients pay today and cost
your name over months, and only a run long enough to let reputation move can show it. The
report prints the fortnight and the two-month figure side by side and flags every change whose
sign flips between them, because **the first answer and the eventual answer are often different
and the fortnight is the one that lies.**

**Still open:** cuisine (the other half of the structure), and the bulk content itself.

### FIXED: hiring a cook made the restaurant worse — and it was two bugs, in both builds

The largest open model defect, and it broke Binding Principle 2 outright: the player took an
action, output fell, and nothing named a cause. Measured on the browser build at 24 seats and
two second-hand ovens, only the brigade moving:

| cooks | covers/day | walkouts/day | balked at door |
|---:|---:|---:|---:|
| 1 | 43.1 | 28.0 | 41.6 |
| **2** | **51.6** | 34.0 | 35.1 |
| 6 | 39.6 | **54.9** | **26.9** |

**The middle columns were the diagnosis and they were not in the original finding.** Door-balks
FALL as walkouts RISE. Extra hands were not creating trade, they were **converting cheap losses
into expensive ones** — a balk costs you the sale, a walkout costs the sale plus the plate you
cooked, the table they held while waiting, and a mark against your name.

**The engine had it too.** 1 cook 1,090 covers, 6 cooks 1,043. So not a port bug — a shared
root cause, found only because the C# side was measured before anything was changed rather
than reasoned about. `BrigadeScalingTests` is that instrument, kept.

**Cause 1: the quote was a SECOND IMPLEMENTATION of the scheduler, and it drifted the way that
hurt.** `EstimatedWaitMinutes` approximated — earliest free slot, plus a guess at how many
"rounds" of plates a party needed — where `Fire` did the real thing. The guess divided the
party by how many plates could run at once, so a big brigade quoted a table of four two rounds
where a small one quoted four. **Half the wait, from the same kitchen, on the strength of hands
that were not the constraint.** The browser build had the same disease in a different spot:
`Math.min(...slots, ...cookFree)` quoted against whichever resource was MORE available, so with
twelve cook-slots against two ovens the quote stopped seeing the oven queue at all.

Both now deal plates through one shared `Place` / `placePlate`, against clones of the boards to
ask and the real boards to commit. **It cannot drift, because it is the same code.**

**Cause 2: a flat average across the card hid a buried station behind a free one.** A jammed
oven and an idle garde-manger averaged to a comfortable quote, so the party sat down, ordered
the pizza they came for, and walked out. The quote is now weighted by `appetites` — which
already decided which dish they order and **was never consulted about the wait for it.** Sixth
instance of the recurring shape, after `PriceSensitivity`, `IngredientQuality`,
`PartiesTurnedAway`, `Employee.Skill` and `PartiesLostToMenu`. **Assume there is a seventh.**

**Cause 3: plates for a table that walked were still cooked.** That burns the scarcest thing in
the building at the moment it is scarcest, and it is a loop that feeds itself — the busier the
pass, the more it wastes, so the busier it gets. `KitchenPass.Abandon` takes unstarted plates
back off the board and re-deals everything queued behind them. **A plate already in the pan is
NOT recovered**, deliberately: the ingredients and the minutes are spent, only the queue is
refundable, and that is what keeps the loss real.

**Aaron's Sims framing is why this one matters more than the numbers say.** The game is to be
*watched*, not fast-forwarded — so a chef plating food for a table that left five minutes ago
would have been glaring on screen and was invisible in a text log. Worth applying as a standing
test: **would this look absurd if you could see it happen?**

Result — covers now rise and hold flat instead of sliding, on both builds, and
`probe-capacity-monotonic.js` pins it across cooks, ovens, seats and equipment tier.

**IT ALSO REVERSED A RECORDED FINDING, and the old one was a symptom of this bug.** "A seat you
cannot feed is worse than no seat at all" measured 12 seats -> 68.8 covers and 20 -> 56.8.
It is now 12 -> 69.0, 18 -> 76.1, flat above that. Adding tables no longer hurts; it simply
stops helping. **The granularity guard in the Advisor is still right** — seats above what the
pass can feed buy nothing and cost money — but the harsh version of that claim is gone.

**Knock-on: `PracticalCapacity` 0.75 -> 0.90, and this is calibration rather than tuning.** That
number was bundling two things: clumped arrivals, and the pass cooking for people who had left.
The second is gone, and charging for it twice made the forecast under-predict every
kitchen-bound night by 17-30%. All three failing cases were wrong in the SAME direction, which
is the signature of a mis-calibrated constant rather than a modelling error — as against
fitting one constant across cases that fail in different directions, which is the trap this
project has been caught in twice. Median forecast error 28% -> **11%** (it was 12% before any
of this). Set below the measured 94% conversion on purpose: error keeps falling to ~0.95 and
then goes flat, and flat means the kitchen has stopped binding, so a value in there would be
fitted to the test rather than to the model.

**And the seat ceiling was wrong for a nameable reason:** the room turns every dwell PLUS the
wait for food, not every dwell. A guest waiting twenty minutes for a pizza is holding the table
just as surely as while they eat it. Twelve seats forecast 88 covers against 62 served; now 76.

### The Advisor went quiet exactly when the restaurant could afford to grow

**A regression this session caused, caught by `playthrough.js`, and worth recording because the
cause generalises.** Every capacity rule detects IMBALANCE — the kitchen fires below 80% of the
room, the room fires below 80% of the pass — so a restaurant sitting between them is declared
healthy. **A place perfectly balanced at twelve seats is perfectly balanced and far too small.**
The walkouts rule used to drag such a restaurant forward by accident; once abandoned plates
stopped burning the pass, walkouts fell under its threshold and the last voice telling anyone
to grow went silent. 240 days, **$72,009 in the bank, still 12 seats and one cook**, and the
forecast said `seats-bound` on every one of those days.

**The forecast already knew.** It reports `constraint` from the same state and had been saying
so all along — two components answering the same question from different data, again. Rule `3c`
now defers to it: if a ceiling binds and no other rule found anything, the forecast wins.

**Two lessons in how it had to be built:**

- **It offers ONE button, not two.** The first version offered tables and a cooker together and
  let the player choose. The harness took the tables every time, finished on 42 seats and one
  cook, and tripped its own invariant four times. **Two buttons where only one gets pressed is
  a coin flip with a wrong side.** So the ORDER is the advice: lift the kitchen, and the
  ordinary room rule buys the tables once the pass is genuinely a whole block ahead.
- **`roomtight` said "lift the kitchen first, then the room" for 235 days and offered nothing to
  do it with.** Same lesson the walkouts rule already learned and it did not carry across.
  Naming the move without offering it is half an Advisor.

| `playthrough.js`, 240 days | recorded | now |
|---|---:|---:|
| Cash | $86,528 | **$115,346** |
| Covers/day | 76.9 | **94.1** |
| Walkouts/day | 5.1 | **1.4** |
| Ends at | 12 seats, 1 cook | 42 seats, 2 cooks |

Still no contradictions across 240 days. And a `ReferenceError` on the way — hoisting two
locals out of an `if` — which `probe-panels.js` did NOT catch because that path only runs mid-
game. **A brace-balanced file is not a file that runs**, and panel coverage is not run coverage.

### `tools/matrix.js` — every lever against every other, and the stupid patterns it found

Aaron: *"try simulations of every scenario possible... we're looking for stupid patterns, for
example, its better to stay with 12 seats rather than grow."*

**Why `levers.js` was not enough, and this is the methodological point:** it moves ONE thing at
a time, which structurally cannot see an interaction — **and the most important decision in the
game is one.** Swept alone, sourcing reads as a flat trap: budget wholesale beats premium by
$20,816 over 120 days and the verdict is "always buy the cheapest", in the system Architecture
Rule 1 exists entirely to serve.

**A FIRST VERSION OF THIS NOTE CLAIMED PREMIUM WINS ONCE YOU CHARGE FOR IT. THAT WAS WRONG,
AND THE WAY IT WAS WRONG IS THE INSTRUCTIVE PART.** It compared every supplier AT 1.5x —
premium's optimum, and well past budget's cliff — from a two-point sweep of 1.0x and 1.5x.
Two points is evidence that 1.5 beat 1.0, which is not the same claim as an optimum. It is
also exactly the error already recorded against `StrategyDiversity` one section up: *"hands
every strategy a fixed price multiplier... compares concepts run at arbitrary settings rather
than concepts run well."* **The written rule did not stop the same mistake being made again,
so: when comparing concepts, sweep each one to ITS OWN optimum before reporting a winner.**

Swept finely, 240 days, each supplier at its own best price:

| | small kitchen (capacity-bound) | big kitchen (demand-bound) |
|---|---:|---:|
| Budget | **$98,239** at 1.4x | $165,107 at 1.2x |
| Valley | $92,297 at 1.4x | **$168,665** at 1.3x |
| Premium | $77,247 at 1.5x | $151,583 at 1.3x |

**Premium never wins, at any capacity, run properly.** Mid-tier edges budget once the kitchen
is big enough to convert the extra footfall ($168,665 against $165,107), so a hint of the
intended arc exists — but it does not reach the top tier. **Sourcing is a genuine trap, not a
coupling problem**, and an Advisor change would have been solving a problem that is not there.

Note also the optimum MOVES with capacity — 1.4x when capacity-bound, 1.2-1.3x when
demand-bound — because covers lost to a price rise are free while you are turning people away
and expensive once you are not. Nothing in the game says this.

**And a separate defect the same sweep isolated: `suggestedPosition()` is far too timid.**
It suggests 1.04x / 1.12x / 1.20x for budget / valley / premium where the measured optima are
1.4x / 1.4x / 1.5x. The direction is right and the magnitude is not, and a player following it
leaves a great deal on the table. **The cause is that it scores a lost cover as a real loss
even when the kitchen was full and would have turned that guest away anyway** — it has no
concept of the capacity ceiling, which is why its answer does not move between the two regimes
above when the true answer does.

**One attempt at this was made and REVERTED, and the failure is worth keeping.** Clamping
served covers to `min(demand x conversion, ceiling)` is correct arithmetic and produced a
suggestion of 1.54x for BUDGET — because once you are over the ceiling the score rises
monotonically with price, so the model recommends pushing until you fall off the cliff. In a
static snapshot that is right; over 240 days it is ruinous, because falling off costs the
price-sensitive archetypes, the crowd composition and the reputation with them. **A capacity
term is needed and it must not be a hard clamp.** Left measured and unfixed rather than
shipped at a third attempt.

Also found, and left as findings rather than acted on:

- **Sourcing cannot pay while the kitchen binds.** At 2 ovens all three suppliers serve within
  1% of the same covers (68.3 / 68.6 / 69.2), so a 26-point standing gap buys nothing. Standing
  buys footfall, and footfall needs somewhere to sit.
- **Seats above 18 are a pure cash sink** on this build — 18/24/32/48 give identical covers.
  Growing does not hurt any more, but nothing tells you where it stops helping.
- **Servers are a trap**, 1 beating 2/3/4. Real, small, and worth a look.
- **Menu breadth and oven count still read "more is always better"** inside the ranges swept.
  Breadth does turn over at 6 dishes, so the optimum is real and the grid was too short.
- **A sign flip**: 4 cooks x 2 ovens reads −$1,551 over 30 days and +$11,829 over 240.

### A port bug the sweep found: the city had ELEVEN TIMES the floor it should

`City Center` caps at **1,400 sq ft** in the engine and was **15,500** in the browser build.
That inverts the design's central tension — *"the best traffic comes with the least room to
grow"* — so in the build a human actually plays, the city had by far the most room, and a sweep
hunting degenerate strategies read "more ovens is always better" partly off the back of it.

`TuningDriftTests` now checks **key money, rent and floor cap for all four sites**, not just the
tuning scalars. The drift guard existed and was watching the wrong list. **When a guard catches
one class of bug, ask what else is duplicated that it is not looking at.**

*(The site table further up this file — 110/150/140/280 sq ft — is stale prose. The real figures
are 1400 / 1650 / 1550 / 3000.)*

### Aaron's day-6,994 run: NINETEEN YEARS, $2.4M, TWELVE SEATS, "I didn't really have to change much"

The most important playtest result this project has produced, and it answers the question that
was put to him — *has the walkout fix made the game too easy?* It has, but the cause is far
older and far bigger than that fix.

He fast-forwarded to **day 6,994** on the opening restaurant. Twelve seats the whole way.
**$2,402,611 in cash.** Standing 66, awareness 100%, **48 parties turned away with nowhere to
sit every single night, forever**, and none of it ever became a problem.

**THE GAME HAS NO FAILURE MODE AND NO REASON TO GROW.** Not a balance problem — a missing-
systems problem, and the missing system has a name: **this is an empire game with no empire.**
`Company -> Restaurant` has existed since M0 and multi-location is M4, so the single largest
sink for capital does not exist. There is nothing to spend $2.4M on. Rent does not scale, no
competitor takes your trade, nothing breaks, nobody quits, and demand is unbounded and free.
A restaurant serving 40% of the people who want a table accumulates money forever.

**Do not respond to this by making the numbers harsher.** CLAUDE.md has said since M1 that "the
game is easy because the systems that create pressure do not exist yet"; this is that
prediction arriving at full scale, with a number on it. The fix is the sinks and the pressures,
not the dials.

**Two real bugs the same run exposed, both measured on his save:**

**1. The Advisor steered him to the worse move for 128 days.** At his day-128 position the
`roomtight` rule said *"lift the kitchen first"* and offered an oven, while the forecast one
panel above said `seats-bound — the dining room is the limit`. Measured over 90 days from that
exact state: the oven was worth $3,414, and the seats it told him NOT to buy were worth
**$6,659**. Both together, $11,896.

The granularity guard behind it — refuse seats unless a whole block fits inside the headroom —
was built on the measurement *"adding tables made it worse"* (12 seats 68.8 covers, 20 seats
56.8). **That was a symptom of the abandoned-plate bug**, and the same sweep now reads 12 ->
69.0 and 18 -> 76.1. The premise was measured away and the rule outlived it. **When a fix
reverses a finding, go and find every rule and every assertion built on it** — this one had
three descendants: the Advisor rule, a `playthrough.js` invariant, and a paragraph in this file.

**2. Chairs nobody can wait on are not seats.** `servableSeats()` caps the room at what the
floor staff can hold, and the room advice did not know. At 12 seats and one server it offered
a $600 block of ten that bought two usable covers — and 22 seats vs 32 seats measured
byte-identical, both capped at 14. There is now a `floorstaff` rule that says so.

**And a harness bug worth more than either.** `playthrough.js` acted on `needsWage` by
hardcoding `role: "cook"`, so the new floor-staff advice hired **seven cooks** and drove prime
cost to 94%. Its own comment three lines above reads *"the harness must follow the advice as
given, or it is testing my memory of the Advisor rather than the Advisor"* — and it was
inventing the role. **Every wage ask now carries its role and the harness reads it.** A comment
warning about a failure does not prevent the failure.

| `playthrough.js`, 240 days | recorded | after the brigade fix | after these |
|---|---:|---:|---:|
| Cash | $86,528 | $115,346 | **$203,702** |
| Covers/day | 76.9 | 94.1 | **194.8** |
| Ends at | 12 seats, 1 cook | 42 seats, 2 cooks | 32 seats, 4 cooks, hearth oven |

### Expansion, measured before it was built: a second restaurant was ARITHMETIC

Aaron chose multi-location as the answer to the day-6,994 problem — nineteen years and $2.4M
with nothing to spend it on. Before building any of it, `SecondRestaurant` asked the only
question that decides what the feature should be: **is a second restaurant a new decision, or
a bigger number?**

| portfolio, 180 days | net |
|---|---:|
| suburban alone | 59,800 |
| city alone | 71,638 |
| **the two together** | **131,903** |
| *the two run separately, added up* | *131,439* |

**0.4%.** And two SUBURBAN sites came to 120,858 against 119,600 for twice one — so sites do
not even compete for the same street. Expansion as it stood was pure flat scaling, the exact
anti-pattern, and building the UI first would have shipped a spreadsheet with a theme.
**Measuring first is what stopped a week of work going into the wrong feature.**

### The Region tier, finally built, and the decision it unlocks

`SupplierPolicy` has resolved up a parent chain since M0 specifically so this could slot in,
and it did: `Company -> Region -> Restaurant`, with **no read site changing anywhere**. That
is the M0 architecture bet paying off exactly as written.

`Atlantic National Foodservice` is the point of it. Two new fields on a supplier, both data:

- **`daysInTransit: 3`** — bulk ships through a depot, so what lands has already spent three
  days of its life. `Inventory.Receive` dates the batch backwards and FIFO, freshness and
  spoilage all follow from that one line without a rule of their own.
- **`minimumWeeklyVolume: 1000`** — they will not open an account for less. Measured on
  USAGE, not stock, so you cannot qualify by filling a walk-in once; that would make it a cash
  test, and cash is not what expansion is supposed to prove.

| four restaurants, 180 days | local | national |
|---|---:|---:|
| a card built on things that keep | 238,930 | **256,562** |
| a card built on fish | −625,038 | **−886,329**, covers 32,628 -> 11,956 |

**So it is a genuine decision and not a discount** — cheaper and lower grade is free on flour
and fatal on sea bass, and the right answer depends on the menu you chose. And the gate lands
where expansion reaches it: refused at one and two sites, **open at four**.

**The first gate was set to 3,000 and NOBODY could ever reach it** — six restaurants shift
about 1,600 a week. Measured rather than guessed, which is how a piece of dead content was
caught before it shipped rather than after Aaron found it.

**Architecture Rule 2 got a free exit test out of it:** the whole supplier arrived as a JSON
edit, and 263 tests passed with no code change.

**Known bad fixture, recorded rather than tuned away:** the perishable-card rows lose money
heavily on BOTH sides, because the probe stocks sea bass to a par of 400 on a four-day fish —
the over-ordering the spoilage system exists to punish. The comparison is still valid since
both sides share it, but the absolute numbers are not a balance finding.

**Still not built, and still the reason two sites are arithmetic:** nothing makes nearby
restaurants compete for the same crowd. Two suburban sites should cannibalise and do not.
That is the other half of making a portfolio a decision, and it is next.

### The scouting report — "would people be excited about this concept?" (Aaron, via NBA 2K)

Aaron on 2K's relocation screen: *"you can see if people would be excited about your concept
or team."* The uniform designer is not the interesting half; the market readout is.

**Nearly all of it already existed.** `menuAppealTo` scores a card against a sort of guest,
`likelyAt` says who is out on a street at an hour, `wouldConsider` says whether they would set
off at that price — and the browser build already prints the answer as *"card suits the street
116%"*, pointed at the site you own. **A scouting report is those three functions pointed at a
site you do NOT own, before you have spent anything.** `tools/scout.js`.

It also extends the loop this project already decided is good. Forecast, commit, autopsy —
you commit to a belief and then find out how wrong you were. **Choosing a site is the largest
commitment in the game and was the one made with the least information.**

| street | what it wants | who is excited |
|---|---|---|
| City Center | Neighborhood standard (130%) | Families |
| Business District | Coffee and counter (137%) | Business lunchers |
| Suburban High Street | Wine bar and small plates (121%) | Locals |
| Nightlife Quarter | Fine dining (136%) | Influencers |

**Four distinct winners across four sites**, so a site is a real choice rather than a rent
bill. That is the same anti-pattern check that failed for a second restaurant (0.4% from
arithmetic) and passes here.

**The second column is the good part.** Fine dining reads 136% appeal in the nightlife quarter
and **27% would actually come**, because the price gate filters who sets off. Appeal and
affordability are separate numbers and the gap between them IS the fine-dining problem —
visible before signing a lease rather than six months after. A single blended score would have
hidden exactly the thing worth knowing, which is Binding Principle 2 again.

**What it is not: a profit forecast.** It surveys the market, and the concept a street likes
best is not automatically the one that earns most — fine dining proves that in its own row.
Keep the two separate; blending them would solve the strategy for the player, which Binding
Principle 5 forbids.

**Concepts are fixtures in `StrategyDiversity` and should become content.** "Select a concept
or build your own" is Aaron's other half, and the data-driven loader already supports the
first part. Not built.

**On countries (USA / France / Italy / England), raised in the same breath and NOT built:** a
country is a Region, so the tier now exists for it. But the expansion measurement is the
warning — **more places to put a restaurant does not fix flat scaling.** A site in Lyon that
is only "different rent, different footfall" is another arithmetic restaurant. It earns its
place only if it changes what you can DO: a card that does not travel, sourcing that flips
(local excellent and cheap, imports dear and old — which `daysInTransit` already models), and
labour that works differently. Note also that CLAUDE.md's "Setting: American" rule defers
overseas expansion deliberately, so this is a decision to take rather than drift into.

### Cannibalization, concepts as content, and countries — all three, measured

**1. A STREET IS FINITE, AND YOUR OWN SECOND RESTAURANT DRINKS FROM IT.**

This is what made a second site arithmetic. Nothing made two restaurants on one street contend,
so opening next door to yourself was free. `Restaurant.ShareOfTheStreet` splits footfall **by
appeal, not down the middle** — a strong new site takes the share its card and name deserve,
so cloning your best restaurant beside itself is the worst use of the money and spreading out
is what expansion is FOR.

| 180 days | net | covers | turned away |
|---|---:|---:|---:|
| one suburban restaurant | 59,800 | 15,598 | 3,260 |
| *twice that, on paper* | *119,600* | — | — |
| two, both suburban | **23,615** | 19,763 | 527 |
| two, suburban + city | **131,903** | 35,703 | 9,076 |

Spreading beats clustering by **108,287**. The clustered pair does capture the overflow
(turn-aways 3,260 -> 527) and only buys 27% more covers for double the fixed costs, which is
the right answer rather than a punishment. **Single-site play is untouched** — one restaurant
alone keeps the whole street, so the division only bites once somebody else is on it.

**2. CONCEPTS WERE FIXTURES IN A TEST FILE.** Six of them, hardcoded in `StrategyDiversity`,
doing real work (the whole distinct-winners measurement runs on them) while being invisible to
the game and unmoddable. They are `data/concepts.json` now, and `Restaurant.Adopt` applies any
of them through **one code path that does not know a pizzeria from a wine bar**.

A concept is a **card, a price position and hours — deliberately no staffing, equipment or
floor plan.** It says what you are attempting, not how well you execute it. Aaron's bar is
*"you should be able to win with any concept anywhere if you run the restaurant properly"*,
which only means something if running it properly stays the player's job; bundling a build in
would make picking a concept pick the whole restaurant.

**A data bug the tests caught immediately:** the wine bar's late service was written `23->26`,
the browser build's convention. The engine expresses a midnight wrap as `23->2`. Two builds,
two conventions for the same idea — worth watching when porting content rather than code.

**3. COUNTRIES — USA, FRANCE, ITALY, ENGLAND.** A country **is** a Region; nothing needed
inventing. What earns it a place is that it changes what you can DO on three axes:

- **The card does not travel.** `tastePulls` shifts the whole local crowd by tag.
- **Sourcing re-opens.** Your usual supplier is still available abroad and is now a bad idea —
  anything not local ships in and lands **4 days older**, straight through the `daysInTransit`
  machinery built for the national distributor. Measured: budget-wholesale tomato is 0 days old
  at home and 4 in Florence, and switching to the local grower fixes it.
- **Labor costs what the market charges.** The same three cooks are $48/hr at home and
  **$69.60 in Lyon** — not a difficulty dial, a reason a prep-heavy card is a different
  proposition there.

| concept | USA | France | Italy | England |
|---|---:|---:|---:|---:|
| Neighborhood standard | 2.45 | 2.44 | 3.14 | 2.24 |
| Pizza and sharing plates | **2.73** | 1.43 | **3.79** | 2.65 |
| Fine dining | 2.12 | **3.38** | 2.32 | 1.84 |
| Coffee and counter | 2.54 | 1.74 | 2.61 | **2.72** |
| Wine bar and small plates | 2.27 | 3.27 | 2.61 | 2.28 |

**3 distinct winners across 4 markets.** France wants fine dining, Italy wants pizza, England
wants the counter.

**England only became a real market when drinks got a culture.** On the first pass it read
almost identically to the US — pizza won both — because taste pulls covered food tags and not
`beer`, `wine`, `cocktail`, so the one thing England is actually known for was invisible. Each
country now has an opinion about the bar, written from what the places are like rather than
fitted to the scoreboard.

**Stated honestly: this is measured on APPETITE ALONE, not on money.** It says what a crowd
wants, not what earns most — those differ, as fine dining proves in its own row on the site
scouting report. A full country-by-country profit sweep has not been run, and the labor and
sourcing axes above will move it.

### Two duplicates the new content created, and what they cost

Adding countries and concepts created two second copies of things that already existed. Both
are the shape this project keeps getting caught by, and both were found by going to look
rather than by a failure.

**1. THE FORECAST WAS STILL READING THE RAW PAYROLL.** Labor became country-priced, the
simulation started charging the local rate, and `ServiceForecast` kept using
`Payroll.HourlyWageBill` — so a restaurant in Lyon would have been projected at home wages and
then billed French ones. Sixth instance of two components answering one question from
different data. Every reader now goes through `Restaurant.HourlyWageBill`, and
`TheForecastChargesTheSameWagesTheNightDoes` pins it (forecast $319.00 against $319.00 paid).

**The only reason this was caught before shipping is that the pattern is now something to grep
for after any change that moves a shared number.** Worth doing every time.

**2. THE CONCEPTS EXISTED TWICE.** `StrategyDiversity` kept its six hardcoded while
`data/concepts.json` held the same idea, which is how the C#/JS constants drifted and how
`Markup` got ported by name. The harness now names a concept and supplies only a BUILD —
supplier, kitchen, brigade — and that split is the point rather than a tidy-up: a concept is
what you are attempting, a build is how you run it.

**Consolidating changed the measurement, and the honest fix was not to accept it.** Folding
"Cheap and cheerful" onto the standard card handed it a fourth dish and made it strictly
stronger; distinct winners fell 3/4 -> 2/4 with one strategy winning three markets. It was
genuinely its own concept — a short cheap card — so it became one in data, and the count went
back to 3/4. **Collapsing two different things while consolidating is its own bug**, and the
tell was the score moving at all.

| strategy | city | business | nightlife | suburban |
|---|---:|---:|---:|---:|
| Cheap and cheerful | 10,692 | 847 | 19,427 | 5,327 |
| Neighborhood standard | 34,475 | **12,109** | **42,496** | 15,527 |
| Fine dining | **39,641** | −11,401 | 30,648 | 5,094 |
| Broad menu | 31,480 | 11,648 | 37,254 | **16,241** |

### The multi-restaurant port, and the trick that made it reviewable

The browser build had a single-restaurant assumption threaded through 3,000 lines — close to
**four hundred `G.something` references**, all meaning "the restaurant". Rewriting them to
`R.something` is a diff nobody can review, and **a half-applied rename is the single most
common way this project has shipped a bug**: the `wrap` ReferenceError hid the liquor license
for two sessions, and a partial fix to `bottleneck()` put contradictory advice on one screen.

**So the data moved and the call sites did not.** `G` now holds the company — cash, the clock,
the RNG, the rail — plus `G.sites[]` and `G.active`. Every per-restaurant field is a
**property on `G` forwarding to the active site**, so `G.seats` still reads and writes, and it
is now impossible to miss one by construction rather than by care. `runDay` did not change at
all to become multi-restaurant; `advance` moves the pointer and calls it again.

**What genuinely had to move is the DAY.** `G.day++` and the monthly rent lived inside
`runDay`, which would have advanced the calendar once per restaurant and billed rent N times
over. Both went up into `advance`, which ticks once whatever the size of the group. There is a
check for exactly this in `probe-multisite.js`, because it is the kind of thing that looks
fine and silently triples your rent.

**A latent bug the port surfaced: `stopOnInterrupt` had been ACCEPTED AND IGNORED** for as
long as `advance` existed — every path broke out regardless. Both UI callers pass `true`, so
nobody noticed until the first harness passed `false` and got one day of trading and four
failed assertions. **A parameter that does not do what it says is worse than no parameter**,
because a caller is confident about behavior it is not getting.

Cannibalization came across with it, and the browser numbers mirror the engine's shape:

| 120 days | net |
|---|---:|
| one suburban | $33,104 |
| *twice that, on paper* | *$66,208* |
| two, both suburban | $40,175 |
| two, suburban + city | **$72,867** |

**Single-site play is untouched** — one restaurant keeps the whole street, and `playthrough.js`
still finishes with no contradictions across 240 days.

**The site strip** appears only when there is a choice to make, and shows sites you cannot
afford **disabled with their price** rather than hidden — knowing what you are saving toward
is the point, and it is Binding Principle 4's "capital-gated, not milestone-gated" in its
plainest form.

**Not ported yet:** the Region tier and national sourcing, countries, and concepts as a
starting choice. The browser build can run a portfolio; it cannot yet source for one or trade
abroad.

### A new restaurant was a rent bill, not a restaurant

Aaron opened a City Center site for $12,000 and it served **zero covers in sixty-five days**
while paying $7,800 a month, which is what took him to −$11,377. *"I couldn't get service to
start at the new location, idk if I missed anything."* He missed nothing.

`start()` fitted the first restaurant out — oven, garde-manger, twelve seats, a cook, a
server, storage, stock. `openSite()` handed back a **bare shell**: `stations: {}`, `seats: 0`,
nobody on the payroll, an empty pantry. It could never serve anybody. The Advisor even said
*"nothing on the card suits dinner"*, which was true and useless — there was no kitchen to
cook any of it.

There is one `fitOutOpening()` now, used by both, and `canOpenSite` asks for key money plus
what the fit-out actually costs rather than a made-up $4,000.

**THE REASON THE HARNESS MISSED IT IS THE LESSON.** `probe-multisite.js` had five checks and
all five passed while this was broken, because every one of them **built its sites by hand**
and never touched `openSite` — the button the player actually presses. A harness that sets up
its own fixture is not testing the thing the player touches, which is the same shape as the
missing-fridge famine and the 669% food bill. It now opens one the way a player does and
asserts it trades: 1,106 covers in thirty days, against zero.

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
