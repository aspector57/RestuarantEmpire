# Restaurant Empire Successor — Design Document

Status: living document, updated phase by phase per the planning charter.
Process: phase-by-phase, interactive. Each phase is drafted, then checked with the designer (Aaron) before the next phase starts.

Project constraint noted up front (from kickoff): the eventual deliverable should be one cohesive game that can be shared as a download or hosted online. This doesn't lock a tech stack yet, but it rules out designs that only make sense as a purely local, single-machine, non-distributable prototype. Revisited properly in Phase 9.

---

## Phase 1 — Competitive Analysis

Method: for each game — core loop, strongest mechanics, weakest mechanics, replayability drivers, common complaints, what to steal, what to avoid. Sourced from critic reviews, Steam user reviews, and design retrospectives (linked at bottom).

### Restaurant Empire I & II (Enlight/Gameplay House)

**Core loop:** Sims-style placement of kitchen equipment, tables, and décor → hire staff with skill stats (chef, server, captain, porter) → design menu and set prices → run service → watch customer satisfaction and P&L → reinvest in expansion.

**Strongest mechanics:** Genuine end-to-end coverage — layout, staffing, menu, pricing, supplier quality, and expansion into chains all exist in one system. This breadth is exactly why it's the reference point for this project; nothing else on the list matches its scope.

**Weakest mechanics:** Buggy customer/staff AI, clunky UI, and — critically — updating one variable (e.g., swapping to a better supplier) required manually re-editing every recipe in every restaurant. A single decision at the "empire" level didn't propagate down; the player paid an administrative tax for every strategic choice. Campaign writing and cutscenes were widely seen as filler, not motivation.

**Replayability drivers:** Sandbox mode and chain-building gave players a reason to keep going, but reviewers consistently said the moment-to-moment loop couldn't sustain interest — "only so much clicking, watching and waiting one can take."

**Common complaints:** Micromanagement without corresponding depth; RE2 was seen as barely-changed RE1 with recycled art; the fun of the systems didn't compound into a fun *game*.

**Lesson — steal:** the full-stack ownership fantasy (layout + staff + menu + supply + expansion) is the right scope for "empire." Don't shrink it.
**Lesson — avoid:** never make a strategic decision (better supplier, price change, recipe update) require manual per-instance editing across every location. If the player decides something once, the simulation must know how to propagate or explicitly ask about exceptions — not force repetition. This is a direct architecture requirement for Phase 9 (a "policy" layer above individual restaurant instances).

### Pizza Connection (1 & 2) / Pizza Connection 3

**Core loop:** Pick a location using demographics → build and staff a pizzeria → design pizzas from ingredients → set price and marketing → optionally lean on organized-crime pressure against competitors → expand across the map.

**Strongest mechanics:** Demographics-driven location choice and pizza customization (both praised even in the weaker sequel) create real tradeoffs — a topping combo that wins over one neighborhood alienates another.

**Weakest mechanics:** Time crawled with no way to accelerate it (PC2); PC3 had technical problems and spiked difficulty without matching depth, and was seen as trying to recapture PC2's charm without doing the design work.

**Replayability drivers:** Character build (merchant vs. gangster) and market discovery — figuring out what a neighborhood wants is closer to a puzzle than a spreadsheet.

**Common complaints:** Sequels chased nostalgia over iteration; pacing that isn't tunable kills sessions.

**Lesson — steal:** menu design as a demographics-matching puzzle (not just a quality/cost slider) is a genuinely fun decision space, distinct from Restaurant Empire's more mechanical menu system.
**Lesson — avoid:** don't ship a simulation clock the player can't control the pace of. Speed controls are not a "nice to have," they're load-bearing for a management sim.

### Chef: A Restaurant Tycoon Game

**Core loop:** Create a chef character → design restaurant and menu to match a target social class → cook via minigames → hire and upgrade staff → survive on margin until you can expand.

**Strongest mechanics:** Class/decor/menu alignment is a clean, legible constraint system — it tells the player what "supporting evidence" looks like for a given customer segment.

**Weakest mechanics:** Minigames are disconnected from outcomes — engaging with them or ignoring them "doesn't seem to matter much," so the moment-to-moment skill expression is decorative, not causal.

**Replayability drivers:** Character/recipe customization, early access content cadence.

**Common complaints:** Lack of direction — players reported not knowing what to do beyond "spend money, upgrade, or go bankrupt," and a persistent identity confusion (game markets itself as "be a chef" but plays like "be an owner who occasionally cooks").

**Lesson — steal:** legible class/décor/menu fit as a comprehension tool for new players.
**Lesson — avoid:** never build a skill-expression layer (minigame, cooking action, whatever) whose outcome doesn't feed back into the simulation. If skilled play and unskilled play produce the same result, cut the minigame or wire it into consequences. Also: pick one fantasy (owner or chef) and commit — Phase 3 exists specifically to prevent this failure.

### Big Ambitions

**Core loop:** Start a business (restaurant is one of several) → manage inventory/staff/logistics manually → grow revenue → reinvest into new locations/business types.

**Strongest mechanics:** Broad economic sandbox — feels like "build any business," which is appealing as a fantasy even when execution is thin.

**Weakest mechanics:** Shallow simulation underneath a broad surface: no real customer-occupancy tracking (customers "in the store" aren't modeled coherently — leaving and re-entering can erase revenue for the hour); staff have no needs simulation at all (50-hour shifts, no complaint); relocating a business is 30–60 minutes of tedious manual busywork with no gameplay value; hired managers for entire business lines (import/export) don't functionally do their job.

**Replayability drivers:** Breadth of business types to try; the "what if I ran a different kind of business" curiosity.

**Common complaints:** "A few hours of scratching the surface reveals there is very little underneath" — width without depth; several reviewers explicitly said it fails as a tycoon game despite succeeding as a sandbox toy.

**Lesson — steal:** nothing structurally — this is the clearest "what not to do" reference in the set. Breadth is not a substitute for simulation depth.
**Lesson — avoid:** don't let logistics tasks (moving locations, managing supply chains) become time-cost busywork with no decision in them. If an activity takes real playtime but the player isn't deciding anything during it, automate it or cut it. Also: if you hire a manager to own a function, that manager must actually do the function — "management" that doesn't manage breaks the delegation fantasy central to "empire" games.

### Two Point Hospital / Two Point Campus

**Core loop:** Build rooms → hire staff with traits → treat/serve visitors → manage happiness (staff and visitor) → unlock new room types/illnesses (or courses) → repeat in a new named level.

**Strongest mechanics:** Humor and visual polish carry tone; staff personality traits create a real happiness/performance tradeoff; modular room-building tools are satisfying independent of the sim underneath.

**Weakest mechanics:** The core loop does not evolve past roughly the first dozen hours — new content is more of the same verbs, not new decisions. Levels restart the same setup ritual (place reception, staff room, basic wards) before reaching anything novel, which reviewers called fatiguing. Campus was seen as Hospital with a reskin and less mechanical innovation, and some players found it had *less* replay pull than Hospital's aging but deeper DLC-supported systems.

**Replayability drivers:** Level variety (named hospitals/campuses with different constraints), sandbox mode, staff trait combinatorics.

**Common complaints:** Repetition of setup ritual across levels; humor/charm doing more work than systems; diminishing novelty as the campaign progresses.

**Lesson — steal:** discrete named levels with distinct constraints (space, disease/demand mix, terrain) are a strong structure for teaching the sim gradually and creating variety without new mechanics.
**Lesson — avoid:** don't force the player through an identical cold-open ritual every single time (build reception, build staff room, hire the same three roles) — either let them template/copy a previous setup, or make the early game itself vary meaningfully. Watch for "verb count doesn't grow" — new content should introduce new *decisions*, not just new skins on old ones.

### Dave the Diver (Bancho Sushi)

**Core loop:** Dive during the day to gather ingredients (a separate, non-restaurant game) → at night, run Bancho Sushi: build a menu from what you caught, price it, staff kitchen and floor roles, and manage live service (seat, order, deliver, bus tables) against customer patience and a "Cooksta" reputation score that gates how many customers show up → profits fund better diving gear, which funds better ingredients, closing the loop.

**Strongest mechanics:** The day/night loop makes sourcing and selling two genuinely different gameplay modes that feed each other — the restaurant isn't a menu of numbers, it's the payoff for a different kind of play. Live service (seat/serve/bus, patience bars, a depleting shared resource like wasabi) makes a single night tense and readable without being a full CSD-style action game.

**Weakest mechanics:** Restaurant management is intentionally shallow relative to a dedicated tycoon game — it's one system among several in a genre-blending game, not built to hold up alone for 100+ hours.

**Replayability drivers:** Variety from the diving side bleeds into menu variety; Cooksta rating as a visible, legible progress metric.

**Common complaints:** Reviewers note the game "never topples over" specifically because no single system (including the restaurant) is pushed far enough to become the whole game — a compliment for Dave the Diver, a warning for a game that wants restaurant management to be the *entire* point.

**Lesson — steal:** a depleting shared kitchen resource (wasabi) that all dishes draw from is a clean, legible tension-generator during live service, distinct from Restaurant Empire's per-recipe supplier micromanagement. Also: reputation directly gating customer volume (not just customer type) is a simple, powerful lever.
**Lesson — avoid:** don't mistake "shallow but charming" for a viable depth strategy when the restaurant *is* the whole game rather than one mode among several — this game's restaurant loop is a good subsystem reference, not a template for the full game.

### Cook, Serve, Delicious! (series)

**Core loop:** Take orders → execute precise keypress sequences against a timer to prep/cook/plate → chain "perfects" for score/tips → between-service menu and equipment strategy → narrative/business layer via email flavor text.

**Strongest mechanics:** Direct, skill-based execution where mastery is legible and immediately rewarded (combo system) — genuinely rare among management sims, most of which resolve service passively. Menu-planning-as-strategy plus execution-as-skill is a two-layer structure (think, then perform) that's uncommonly tight.
**Weakest mechanics:** Execution skill is real-time twitch input, which is a different genre muscle than "management sim" — great for the game it is, risky to import wholesale into a slower ownership/strategy game (mismatched pacing expectations).

**Replayability drivers:** Difficulty scaling, score-chasing, "one more shift" session structure; zen mode as an accessibility release valve for the same content.

**Common complaints:** Relatively few — it's the most positively-reviewed game in this list — but its scope is narrower than a full tycoon (one restaurant, no empire layer).

**Lesson — steal:** offering an explicit difficulty/pace toggle (chaotic vs. zen) for the same underlying content is a strong accessibility pattern worth adapting for live-service intensity in general.
**Lesson — avoid:** don't assume twitch-execution is the right resolution mechanic for *this* game's service phase — Restaurant Empire's audience is buying an ownership/strategy fantasy, not a reflex test; if a service-resolution minigame is used at all it should be optional/skippable and never gate the strategic layer the way it gates score here.

### Project Highrise

**Core loop:** Place floors and units in a tower → attract and retain tenants → manage utilities/services → raise tower value/height → repeat at larger scale.

**Strongest mechanics:** Clean, readable systems (a good template for "each unit type has needs and outputs"); DLC/expansion structure for adding tenant types.

**Weakest mechanics:** Repeated across virtually every review: it's *boring*. Once you understand the model, later floors are the same decision repeated with bigger numbers, not new decisions. Minimalist visuals meant density never felt alive — a 10-story tower "felt deserted." Never reaches "one more turn."

**Replayability drivers:** Weak — this is the cautionary tale in the set. Content variety (new tenant types via DLC) without new decision types.

**Common complaints:** Flat difficulty curve, no compelling late-game tension, aesthetic emptiness undermining emotional investment in the simulation.

**Lesson — steal:** its need/output unit model is a fine low-level reference for how to structure a "room/unit has needs and produces value" contract (relevant to Phase 6).
**Lesson — avoid:** the central failure mode to design against for this whole project: scaling up must introduce *new kinds* of decisions (new constraints, new tradeoffs), not just bigger versions of the first hour's decisions. Also: don't underestimate how much a "populated, alive" presentation matters to emotional investment in a management sim — even a good simulation can feel dead if presented too minimally.

### RimWorld

**Core loop:** Not directly restaurant-relevant, but structurally important — colonists with needs/traits/relationships live under a procedural "AI storyteller" (Cassandra/Phoebe/Randy) that injects escalating or random events, and the player's own past decisions (who they took in, what they built, who they lost) become the raw material other systems react to.

**Why it works:** The storyteller doesn't write a story — it tunes *pressure* (raids, disease, resource scarcity) using the player's own state as input, and lets pre-existing systems (combat, mood, relationships, needs) generate the story as a side effect of resolving that pressure. No system exists solely for narrative; narrative is what falls out of legible systems colliding under pressure.

**Lesson — steal:** an event/pressure director that reads current simulation state (reputation, cash reserves, staff morale, seating pressure) and selects/weights events accordingly is a much stronger "story generator" than scripted campaign missions (which Restaurant Empire leaned on and got criticized for). This is a direct, high-value pattern to adopt for Phase 4/8 — a "GM" system that watches the restaurant sim and injects health inspections, critic visits, rival openings, supply shocks, etc., weighted by current state rather than fixed script order.
**Lesson — avoid:** RimWorld tolerates (even wants) brutal, sometimes-unfair outcomes because the fantasy is survival tragedy. A restaurant empire fantasy tolerates less arbitrary unfairness — the pressure system needs to feel "the market/critics/competitors are responding to me," never "the dice hate me." Fairness legibility matters more here than in RimWorld.

### Factorio

**Core loop:** Build a production step → it bottlenecks on an input → build the system that feeds that input → that system bottlenecks on something else → repeat, with increasing automation reducing manual attention over time.

**Why it works:** Multiple loops feed into each other (power → mining → smelting → crafting → logistics → power again), so solving one problem reliably surfaces the next one already primed and legible. The satisfaction isn't any single system — it's designing something once and then not having to touch it again, i.e., the payoff for good decisions is *reduced future workload*, which is a rare and powerful reward structure.

**Lesson — steal:** the "good decisions reduce future micromanagement" reward structure is close to a perfect antidote to Restaurant Empire II's worst flaw (good decisions, like a better supplier, *increasing* micromanagement). Any system in this project that lets the player build reusable infrastructure — standing purchase orders, staff schedules/templates, menu templates that propagate to new locations — should follow Factorio's model: set it up once, trust it, revisit only when conditions change.
**Lesson — avoid:** Factorio's complexity ceiling (dozens of interacting resource chains) is appropriate for a genre whose whole promise is engineering mastery; a restaurant game's complexity ceiling needs to stay legible to a much broader, more casual audience. Don't import raw system count — import the *reward structure*.

---

### Cross-Cutting Findings (Phase 1 Synthesis)

Three failure patterns show up repeatedly across otherwise-different games and should be treated as hard design constraints from here on:

1. **Micromanagement tax on good decisions** (Restaurant Empire II, Big Ambitions). A correct strategic choice must never cost the player linear-in-restaurant-count manual labor. This is an architecture requirement, not just a UX nicety — it needs a "policy propagates down, exceptions are opt-in" data model (Phase 6/9).
2. **Flat scaling** (Project Highrise, and Two Point's late-campaign fatigue). Bigger numbers are not new decisions. Every milestone that adds scale (more seats, more locations) must also add a *new kind* of tradeoff, or it's not worth building.
3. **Disconnected skill/agency layers** (Chef's minigames, and to a lesser extent any twitch-execution layer borrowed from Cook, Serve, Delicious). If the player's action doesn't feed back into simulation state, cut it — it's theater, not gameplay.

And two patterns worth actively adopting:

1. **State-driven event director, not scripted campaign** (RimWorld) — replaces Restaurant Empire's disliked scripted campaign/cutscene structure with events that read the player's actual restaurant state and respond to it.
2. **"Set it up once, trust it" infrastructure** (Factorio) — standing orders, templates, and delegation should reduce attention cost over time as a direct reward for good strategic play, mirroring what Big Ambitions and Restaurant Empire both failed to deliver (a manager who doesn't actually manage; a supplier change that doesn't actually propagate).

---

## Phase 2 — Restaurant Industry Research

Method: for each real-world system — the reality (grounded in current industry data), then the gameplay abstraction (how much of that reality should be simulated explicitly vs. compressed into a single dial, per the charter's instruction that the goal is *believable simulation*, not realism).

### Ownership & Financing

**Reality:** Three structurally different ownership models exist. Independent: full profit retention, full risk, financing depends on the owner's personal credit/collateral since there's no proven track record to point to. Franchise: lower failure risk to lenders (proven model, brand recognition) in exchange for an initial franchise fee plus ongoing royalty (4–8% of gross) and marketing-fund (1–5%) payments — 8–12% of revenue leaves the building before the owner sees it. Chain (corporate-owned): locations are financed and owned centrally by the parent company; no local owner at all. Total startup cost for a franchise ranges roughly $200K–$3M+ depending on brand and footprint.

**Gameplay abstraction:** This maps directly onto a progression axis rather than three separate game modes — independent operator → franchisor/franchisee relationships → corporate multi-unit ownership is a natural expansion arc (ties directly to Phase 8's milestone roadmap and the "franchising/corporate ownership" requirement in Phase 9). The royalty/marketing-fund cut (8–12%) is a ready-made tradeoff lever if we let the player *become* a franchisor later: sell the brand, take a cut, lose direct control — a different kind of empire-building decision than opening company-owned locations. Full realism on financing/credit mechanics isn't needed; a simplified loan/interest system anchored to these real percentages is enough to feel legitimate.

### Staffing, Scheduling & Turnover

**Reality:** Restaurant turnover runs roughly 75–80% annually industry-wide, and over 100% in QSR — most positions turn over more than once a year. Leading causes: low pay, inconsistent/last-minute scheduling, physically demanding work, poor management, no advancement path. Labor cost benchmarks: roughly 25–35% of revenue for full-service, with profitable operators holding it nearer 30–34%. Scheduling itself is a matching problem — under-schedule and service collapses, over-schedule and margin evaporates, against inherently unpredictable customer flow.

**Gameplay abstraction:** Staff needs and morale must be a real, simulated resource — this is a direct callback to Big Ambitions' worst flaw (Phase 1), where staff had no needs model at all and worked 50-hour shifts without complaint. Turnover shouldn't be a random tax; it should be a legible output of (pay vs. market rate, schedule predictability/fairness, workload, advancement offered), so a player who understaffs or under-pays *sees* it coming as rising quit-risk before it happens — no arbitrary punishment, per the RimWorld fairness-legibility lesson from Phase 1. Scheduling itself is a strong native decision space (who works which shift, at what cost, covering what demand curve) and should not be automated away entirely, but a "standing schedule template" (Factorio's "set it up once" pattern) should exist so a stable restaurant doesn't require rebuilding the schedule from scratch every week.

### Kitchen Workflow

**Reality:** Professional kitchens are organized by the Escoffier brigade system — dedicated stations (sauté, grill, garde manger, etc.), each staffed by a specialist, coordinated by an expeditor who manages the pass, controls ticket timing, and bridges front-of-house/back-of-house. Modern Kitchen Display Systems route tickets to the right station screen, fire dishes based on prep time, and use color-coded urgency. Floor plan layout (zone/island/assembly configuration) has a direct, measurable effect on ticket times and labor efficiency.

**Gameplay abstraction:** Stations-with-specialists-and-a-pass is a clean, simulatable structure: each station is a queue with a throughput rate gated by equipment + assigned staff skill, and the pass/expeditor role is where bottlenecks become visible to the player. This is a good candidate for the "smart object" pattern flagged in Phase 1's GitHub notes (each station advertises its own capacity/state) and gives layout design (Phase 4/6) a real mechanical stake — not just aesthetics, but literal throughput, which is the one thing Project Highrise never gave its towers (Phase 1 lesson: scaling must create new tradeoffs, and kitchen layout under order-volume pressure is exactly that).

### Food Cost & Menu Engineering

**Reality:** Food cost benchmark is 28–35% of revenue; combined with labor, "prime cost" (food + labor) should sit at 55–65% of revenue depending on service style (55–60% QSR, 60–65% casual, up to 68% fine dining) — the single most-watched number in the industry because, unlike rent, it's controllable week to week. The Kasavana & Smith menu engineering matrix (1982, still the industry standard) classifies every dish on two axes — contribution margin (profit) and sales volume (popularity) — into four quadrants: **Stars** (high margin, high volume — protect, don't touch), **Plowhorses** (high volume, low margin — popular but barely profitable; fix via price or portion, don't cut), **Puzzles** (high margin, low volume — profitable but underordered; needs marketing/menu placement, not removal), **Dogs** (low margin, low volume — cut or relaunch).

**Gameplay abstraction:** This is close to a ready-made core mechanic, not just flavor research — the Kasavana-Smith matrix is a legible, teachable, genuinely strategic tool the player can use in-game exactly as real operators do: run the numbers on the actual menu, see which dishes are Stars/Plowhorses/Puzzles/Dogs, and act. This is a strong candidate for one of the central recurring decisions in the whole game (see Phase 5/7) because it's inherently about *tradeoffs* (a Star today can decay into a Plowhorse as ingredient costs rise or a competitor undercuts it) and it directly punishes Restaurant Empire II's failure mode (Phase 1: changing a supplier had to be done recipe-by-recipe) if we build supplier/cost changes to auto-update contribution margins and let the matrix react live rather than requiring the player to recompute anything by hand.

### Purchasing, Suppliers & Inventory

**Reality:** Par levels (the min/max stock band per ingredient) are the standard tool — set from historical usage plus a safety margin, tuned per item by how fast it moves and how quickly it spoils (perishables get tight pars, shelf-stable gets looser ones), and treated as *dynamic*, adjusted as sales patterns and waste data change, not fixed once. FIFO (first-in-first-out) rotation is standard practice to fight spoilage.

**Gameplay abstraction:** This is exactly the domain where Restaurant Empire II failed (policy-vs-instance propagation) and Dave the Diver succeeded with a much simpler abstraction (a single shared depleting resource per service). The right level of depth is in between: per-ingredient par levels the player sets (a real decision with real tradeoffs — tie up cash in a deep pantry vs. risk running out mid-service), but supplier/price changes must be a *policy* object that every recipe referencing that ingredient reads from live, never a per-recipe edit chore. Waste (over-ordering/spoilage) and stockouts (under-ordering, forced 86'd items mid-service) are the two failure modes to make visible and costly in opposite directions — that tension is the actual game here, not the bookkeeping.

### Marketing, Reviews & Customer Psychology

**Reality:** 90% of diners research a restaurant online before visiting — the highest of any business category — and about a third won't try a restaurant under 4 stars. Trust effects are large and measurable: a one-star rating improvement correlates with a 5–9% revenue increase, and customers report spending ~31% more at businesses with excellent reviews. Owner responses to reviews measurably help: restaurants that respond see a 35% higher return-customer rate, and diners are more likely to leave a review at all when the owner replies.

**Gameplay abstraction:** Reputation should function the way Dave the Diver's Cooksta rating does (Phase 1 lesson) — a visible, legible score that directly gates customer volume and mix (which segments show up), not a vague flavor stat. But real-world dynamics suggest more texture than a single number: a review-response mechanic (acknowledge/handle a bad experience after the fact) is a cheap, well-evidenced addition that creates a genuine post-hoc decision point distinct from "cook better food" — it's a second chance mechanic with real precedent. This is also the natural hook for the RimWorld-style state-driven event director from Phase 1: a scathing review, a viral moment, or a critic visit are exactly the kind of "pressure events" that should be selected based on current reputation/performance state, not scripted.

### Expansion & Franchising

**Reality:** Multi-unit growth models range from single-unit franchising up through area development (a franchisee committing to open several units in a region on a timeline). The core risk across all of them is quality erosion — as attention spreads across locations, small compromises compound into systemic problems, and not every successful single restaurant is actually scalable: if success depends on one personality/chef, expansion risk is high. Multi-unit development is explicitly described as a *scaling* strategy that only works once the underlying concept and operations are already proven — it is not itself a growth strategy.

**Gameplay abstraction:** This is a strong, underused tension: expansion should be gated not just by capital (the obvious resource) but by *provable repeatability* — can this concept run without the founder physically present, without wide quality variance? That's a natural, simulatable check (does location B's satisfaction/reputation hold up without direct player attention?) rather than an arbitrary "unlock chain mode at $X net worth" gate, and it's the single most direct answer available to "how do we make the empire-building phase feel earned rather than just numeric," which is exactly the kind of late-game depth Project Highrise and Two Point's campaigns failed to deliver (Phase 1).

### Restaurant Finance & Failure

**Reality:** The "90% fail in year one" claim is a myth; real first-year failure is closer to 17%, though roughly 49% close within five years. The leading causes, in order of how consistently they're cited: poor financial management/running out of capital, underestimating startup and ongoing capital needs, rising food/labor/rent costs, a weak or undifferentiated concept, poor location fit, and poor leadership/culture. Prime cost (food + labor as % of revenue) is treated as the single most important controllable number because, unlike rent, the operator can actually move it week to week.

**Gameplay abstraction:** Prime cost is the right top-line health metric to surface to the player at all times — it's real, it's legible, and it's the one number that ties food cost, menu engineering, staffing, and pricing decisions together into a single readable signal. Failure should come from the same causes it does in reality (cash runway, cost creep, weak concept-market fit, location mismatch) rather than from an opaque "bankruptcy" trigger — this supports the Phase 7 requirement that every mechanic produce a legible tradeoff, and it gives the RimWorld-style event director real signal to escalate pressure against (a restaurant with thin cash reserves and rising prime cost is exactly the state that should draw harder events, mirroring Cassandra's rising-tension design).

---

### Cross-Cutting Findings (Phase 2 Synthesis)

Three real-world levers are strong enough, well-evidenced enough, and legible enough that they should become central, recurring gameplay systems rather than background flavor: **prime cost** (food + labor as % of revenue) as the always-visible health metric; **the Kasavana-Smith menu matrix** (Stars/Plowhorses/Puzzles/Dogs) as a recurring strategic tool the player actively uses, not just a lore reference; and **reputation as a volume/mix gate** in the Cooksta mold, with a review-response mechanic layered on for a legitimate second-chance decision.

Two real-world systems are better *abstracted heavily* than simulated in full detail, because the interesting decision lives one level up from the raw mechanic: financing/credit (real-world lending criteria are not fun; a simplified loan system anchored to real interest/royalty percentages is enough) and detailed per-shift labor law/compliance minutiae (real but not a source of interesting decisions — a background constraint at most, never a minigame).

One real-world system directly repeats a Phase 1 architecture warning and must be designed around it from the start: purchasing/supplier policy has to propagate from a single decision point to every recipe/location that depends on it, exactly the fix Restaurant Empire II needed and never got.

---

## Phase 3 — Core Fantasy

**Status: locked. Genre clarified further during Phase 5 (below); binding retroactively on all prior phases.**

### The Fantasy

*You are a chef who opens your first restaurant on a tight budget and builds it — through personal culinary mastery, sharp hiring, and strategic business decisions — into a respected, then dominant, restaurant empire.*

Your identity never stops being "a chef" — that thread runs the whole game — but the decisions that matter most, especially as you grow, are business and strategic ones: who you hire, what you build, how you price, how you expand. This is Restaurant Empire's scope and ambition, rebuilt: same size of dream, sharper systems, a world that reacts to you, and no random unfairness.

### Genre Clarification (added Phase 5, per Aaron)

**This is a tycoon/empire management game, cooking-themed — not a cooking game.** Surfaced explicitly while stress-testing the core loop in Phase 5, but binding back on everything above: the primary gameplay genre is business/strategy management (staffing, sourcing, menu strategy, pricing, expansion), not culinary execution. Cooking and "being a chef" are the theme and the permanent personal-identity thread (per principle 1 below), not the core gameplay verb. This is what justified making Service a watchable, skippable simulation rather than a hands-on execution phase (Phase 5) — if this were a cooking game, service would have to be the main event; since it's a tycoon game about running a restaurant, the decisions before and after service (Prep and Review) are correctly the main event. The live risk this creates — cooking/chef identity receding into pure flavor and feeling vestigial, the same failure named in Chef (Phase 1), just from the opposite direction — is being actively guarded against by keeping the culinary systems specific and mechanically real (the menu engineering matrix, kitchen brigade stations, R&D-as-dish-creation, tasting menus) rather than generic business-sim systems with a restaurant skin. This needs to stay true through Phase 6 and Phase 8 for the genre call to hold up.

### Founding Design Principles (locked from this phase, binding on everything after)

1. **Dual, permanent progression — chef skill and empire skill, weighted toward empire.** Personal cooking/skill upleveling is a real, always-present system that never degrades into a cutscene or a vestigial minigame (directly resolves the Chef-the-game identity-confusion flaw from Phase 1). But it is explicitly *not* the dominant axis of success — hiring, staffing, layout, menu strategy, and expansion decisions carry more strategic weight than raw cooking skill, by design and by the player's own framing ("your overall cooking skills will not be the most important thing").

**Made honest per Phase 10 finding 2.5 (Aaron aligned).** Phase 10 found that by M4, chef skill had quietly been reduced to a stat — a baseline modifier at a station, an R&D tier gate, an occasional opt-in boost — while R&D chefs and General Managers absorbed everything else, which is weaker than this principle's language claims. The fix: **signature dish creation requires the player-chef personally, and permanently.** Hired R&D chefs can develop incremental recipes and add parallel capacity, but the dishes that define the restaurant's brand — the ones that earn the signature flag, drive Marketing hooks, and anchor a Chef's Tasting Menu — can only come from the player. One irreplaceable, non-delegable function makes the principle true rather than aspirational, and it does so without contradicting the tycoon-first genre call, since it's a small number of high-impact moments rather than a continuous demand on the player's attention.

2. **Hard and strategic, never unfair.** Difficulty should come from real, legible cause and effect — cash runway, cost creep, concept/market mismatch, understaffing — exactly the real failure causes surfaced in Phase 2, never an opaque or random bankruptcy/failure trigger. Losable, but always explicable after the fact.

3. **Rivals as relationship-bearing NPCs, not fixed antagonists.** Default posture is background pressure — competitions, awards, critic events, new businesses opening nearby — not constant direct interference. Pressure escalates naturally once the player's empire is big enough to threaten a rival's, mirroring RimWorld's rising-tension storytellers rather than a flat difficulty curve. Critically: the player can opt out of specific competitive content, and relationships with rivals are bidirectional and built through interactions — a given rival can become a friend/ally as plausibly as an enemy, based on player choices. This is a genuine departure from every reference game in Phase 1, none of which modeled competitor relationships as something other than fixed antagonism.

4. **Expand in place first by economics, not by artificial gate; multi-location is a distinct later layer, not more of the same.** There is no quest-flag or milestone unlock for buying a second building — the moment the player can genuinely afford one (real capital requirement, sized so it's naturally out of reach early), they can buy it. In practice this still means the early-to-mid game is entirely about growing one restaurant — bigger building, more seats/floors, deeper kitchen capacity, leveled-up staff and chef skill — because that's what's actually affordable, not because the game artificially blocks expansion. Once affordable, multi-location/empire mechanics kick in as a genuinely different kind of decision-making (policy propagation across locations, quality-erosion risk when the founder isn't physically present, eventually franchisor/franchisee choices per Phase 2's ownership research) — this directly avoids the Project Highrise trap of scaling being "the same decisions, bigger numbers," and it sets the actual sequencing for Phase 8's milestones. It also means a skilled/lucky player could rush a second location earlier than intended — that's a feature (rewards skill), not a bug, as long as the new-layer mechanics hold up whenever they arrive.

5. **The game suggests, you decide.** (Added during Phase 4, but binding retroactively as a founding principle.) Depth should come from real systems interacting, not from requiring the player to manually discover optimal configurations through trial and error. Wherever a system produces enough state to have an "optimal-ish" move (which dish to feature, which staff script to run, when to place an R&D bet), the game should surface it as a suggestion the player can accept, ignore, or override — never as a silent trap the player only learns about after getting it wrong. This is what lets the game carry real strategic depth (per principle 1) without becoming a chore (per Aaron's repeated feedback that systems shouldn't demand constant manual tuning).

6. **Design deliberately for moments of joy and accomplishment, not just balanced systems.** (Added after Phase 6, during a direct critical pass on what was still missing — binding retroactively.) A system can be well-balanced, legible, and tradeoff-rich and still fail to produce anything a player would actually remember or tell someone about. This project should actively design for specific, personal, retellable moments — not leave them to chance as a side effect of otherwise-solid systems. Two concrete answers to this principle now exist: the hiring/scouting/promotion system in Employees (a cheap, uncertain hire developing into someone trusted with a whole second restaurant), and the Regulars & Restaurant Legacy Log system in Customers (a named regular's history, and an auto-generated timeline collecting every such moment across the game). The latter is explicitly flagged as the one system in this design whose long-term payoff depends on ongoing content/writing variety rather than pure systems design — size it honestly in Phase 8, keep it deliberately small.

### Flagged open question — RESOLVED (Reputation's Power Rankings, this session)

Since expansion is capital-gated rather than milestone-gated (per Aaron's clarification), a player can simply choose not to spend on a second building — which already answered "is single-location mastery a valid endgame?" with a soft yes. The Power Rankings addition to Reputation (a Prestige rank driven by quality alone, running parallel to an Empire rank driven by scale) makes that explicit and rewarded rather than just unincentivized-but-allowed: "world's best restaurant" and "biggest empire" are now two named, separately trackable win-conditions, neither one the implicit "real" ending.

## Phase 4 — Simulation Map

Method: every major simulation, with purpose, what it knows/can/cannot do, inputs, outputs, interactions with other systems, failure modes, and expansion hooks. Built directly on the Phase 1 lessons, Phase 2 industry research, and Phase 3 founding principles — every system below is traceable back to something specific from those phases, not invented fresh.

### Restaurant

**Purpose:** the container for a single physical location — identity, capacity, and the frame everything else operates inside. **Architecture note, closed in Phase 9:** every Restaurant instance belongs to a **Company/Empire** parent entity from the very first milestone, even when there's exactly one restaurant. This isn't optional scaffolding for later — it's the container Economy's rollup, Power Rankings' Empire track, and franchising all read from, and retrofitting it after real game data exists in production would be a genuinely painful migration rather than a clean addition.

**Inputs:** building/lot chosen, build-out budget, layout decisions, neighborhood demographics (Pizza Connection lesson: location choice should be a real demographics-matching decision, not just a price tag).

**Outputs:** seating capacity, kitchen throughput ceiling, a location-specific demand pool, per-location P&L. (Ambiance/decor is a minor, bounded input to Customer satisfaction — see Furniture/Layout's scope note — not a standalone score at the Restaurant level either.)

**Interactions:** hosts Kitchen and Employees; draws Economy for rent/build costs; exposed to Competitors (nearby rival density); generates Reputation; targeted by Marketing.

**Failure modes:** kitchen throughput can't match seating demand (the dominant, mechanical layout risk); segment mismatch on menu and pricing (the Chef-game lesson — target segment must be legible and matched — this is driven mainly by Recipes/pricing, with decor only a mild secondary nudge); over-leveraged build-out relative to realistic revenue.

**Expansion hooks:** multi-location turns Restaurant from a singleton into a managed collection; food trucks as a lightweight mobile variant; delivery as a virtual, seatless extension; "grow your building" content (new floors/wings) as the primary early-game expansion axis per Phase 3.

### Customers

**Purpose:** the demand signal and the judge of everything else — the entity whose satisfaction is the actual scoreboard.

**Knows:** hunger/appetite, patience, budget/price sensitivity, cuisine and dietary preferences, party size, occasion, the restaurant's current reputation, visible wait state.

**Can:** arrive, queue, wait, get seated, order, eat, tip, review, leave, return as a regular.

**Cannot:** see the kitchen's internal state (only visible wait time), ignore their own patience threshold, exceed the restaurant's advertised capacity, teleport.

**Outputs:** revenue (check + tip), a satisfaction score, a public review feeding Reputation, passive word-of-mouth feeding Marketing.

**Interactions:** gated in volume and segment mix by Reputation (the Cooksta-style lesson from Dave the Diver); served by Employees; fed by Kitchen/Recipes (the dominant satisfaction drivers); lightly nudged by Restaurant's ambiance/comfort (a minor, bounded input — see Furniture/Layout's scope note); price-sensitive to Economy.

**Failure modes:** patience exceeded → walkout (revenue loss + reputation hit); segment mismatch → dissatisfaction even when service is technically fast and correct.

**Expansion hooks:** delivery customers with a different patience/expectation model; large-party/event bookings; a distinct critic/celebrity customer type that carries outsized reputation weight (now formalized below and in Events).

**Regulars & Restaurant Legacy (added per Aaron — the customer-side answer to Phase 3, principle 6's "moments of joy" gap):** a customer who keeps returning while staying satisfied is automatically flagged as a Regular — no player curation required, the same "the game notices, you don't manage a roster" pattern as the Advisor. Only a small, fixed number of Regulars are ever promoted to *named* status at once, auto-picked by tenure, sentiment, and notable milestones, the same discipline that keeps the Dashboard short instead of ever-growing. What's tracked per named Regular is deliberately shallow — first visit, their usual order, a sentiment trend, and the occasional real milestone (an anniversary, a proposal, eventually bringing their kids back years later) — not a second life-simulation layered on top of Customers.

It surfaces through the Advisor as occasional, optional prompts ("the Hendersons are celebrating their anniversary tonight — comp a dessert or feature something for them?"), accept or ignore with no penalty, and a Regular who quietly stops showing up is a legible, diagnosable event (per Phase 6's "every outcome traces to a cause" rule), not a silent disappearance. It plugs into systems that already exist rather than being bolted on: Regulars are more forgiving of one bad night than a first-timer but more sensitive to a stale menu over time (the second half already written into R&D); they disproportionately drive passive word-of-mouth (an existing Customers output); they provide a resilient revenue floor during a rough patch, which is the mechanical justification for Reputation's recovery not needing to be purely punishing; and occasionally one is the source of a great hire referral (Employees).

A companion **Restaurant Legacy Log** — a lightweight, auto-generated, browsable timeline — is the natural home for every "moment of joy" thread designed this session: a Regular's milestone, an employee's promotion, a won Opportunity Pitch or Marquee bidding war, a Power Rankings climb, a great VIP visit. It costs very little to build since it's just a filtered read of events that already happened, and it's the direct, concrete answer to the charter's "would players discuss this online?" test.

**Scope caveat, stated honestly:** this is the one system in the whole design so far whose long-term payoff depends on ongoing content/writing variety, not just good systems design — unlike the Kasavana-Smith matrix or prime cost, which stay interesting because they're driven by numbers and player strategy, Regulars stays interesting only if there are enough distinct, well-written moment types that it doesn't start repeating the same three vignettes by year five. That's a different kind of cost than the rest of this document, and it should be sized honestly in Phase 8, not assumed free. Keep it deliberately small and Advisor-mediated — resist the pull to grow it into its own relationship-sim subsystem, the same discipline already applied to Competitors and Employees.

**Customer archetypes (added per Aaron):** rather than one abstract preference vector, customers arrive as legible archetypes with concrete environmental demands the player can design around — an Influencer/Critic wants strong visual presentation, fast service, and a trendy service-style match, and posts a public review with outsized reach; a Romantic Couple wants a quiet, intimate service style and a slower pace; a Business Luncher wants fast turnaround and proximity to the entrance. Each archetype reads directly against the Furniture/Layout ambiance presets below, so the player is matching real, nameable guest types to a real, nameable service style rather than tuning abstract stats.

### Employees

**Purpose:** the labor resource that executes the work, and — critically — the delegation layer that has to actually let the empire scale past the player's personal attention (the single biggest thing Big Ambitions got wrong, per Phase 1).

**Knows:** own skill per role/station, morale and personal needs (must be real and simulated, never a Big Ambitions-style non-model), pay vs. market rate, assigned schedule, tenure (time in role, feeding retirement eligibility).

**Can:** work an assigned station/shift, train and level up, quit or be poached, or retire after long tenure. **Frequency note per Aaron:** these are meant to be infrequent, meaningful events, not constant simulated churn — real restaurant turnover runs 75%+ annually (Phase 2), but porting that directly into the game would make headcount a constant background worry, which is exactly the "painful, too-realistic" territory Phase 3/4 already ruled out for Economy and should apply here too. Quit/poach/retirement should be state-driven and comparatively rare (weighted similarly to the Events system — low base frequency, rising only when pay/morale/schedule genuinely justify it), so a well-run team is something the player can mostly stop thinking about, not something demanding constant attention. Staff can also request a raise or schedule change, and can be promoted, including into a General Manager role (below).

**Cannot:** work unlimited hours without morale decay, exceed their own skill ceiling, be scheduled in two places at once.

**Outputs:** station/floor throughput, a quality modifier on dishes and service, a (rare, legible) turnover/retirement event, payroll cost.

**Interactions:** staffed onto Kitchen stations or Restaurant floor roles; managed via standing policy/schedule templates (the Factorio "set it up once" pattern, so a stable restaurant doesn't need its schedule rebuilt weekly); combined with the player's own chef skill to determine kitchen output (the Phase 3 dual-progression principle in mechanical form).

**Hiring profiles, scouting & the promotion ladder (added per Aaron — direct answer to the "moments of joy" gap):** every candidate comes with a profile of named traits, not a single skill number — **Experience** (current skill floor), **Smart** (how fast they learn and how well they handle judgment-heavy moments — an R&D project, a VIP night), **Loyal** (resistance to poaching, and whether they stay with you through a hard stretch or once promoted to run a delegated location), and **Hardworking** (resilience under rush and long hours, resistance to burnout-driven turnover). Critically, not everything is known for certain at hire time — a cheap, green candidate can show real promise without a guarantee it pans out, which is the deliberate point: a proven veteran is a safe, expensive, known quantity with a lower long-term ceiling, while a green hire is a genuine bet — cheaper, riskier, real upside if the read was right. The player can spend time or money (a trial shift, a reference check) to reduce, never eliminate, that uncertainty before committing — a real tradeoff between certainty and cost.

Employees who are worked with and invested in climb a named ladder — Line Cook → Station Lead → Sous Chef → Head Chef on the kitchen side, Server → Captain → Floor Manager on the floor side — either track eventually eligible for General Manager of a new or existing location. Promotion is an earned moment, not a currency spend: watching someone you took a chance on become someone you'd trust with an entire second restaurant. **The tradeoff, stated explicitly (Phase 7 audit):** your strongest hands-on staff are also your best GM candidates, and promoting one out of the kitchen to run a new location means your flagship loses its best cook to gain the ability to expand. This is a real, felt cost, not just an upside — the player has to decide whether a given person is worth more running the floor in front of them or running a whole restaurant on their own.

**The protégé hook, directly tied to Competitors:** an employee who's grown enough, and for whom the player has no bigger role to offer (or who wants more), can leave to open their own place — becoming a new rival with a real backstory (they trained under the player), rather than a generic, faceless competitor. Whether that starts as a friendly rivalry or a bitter one can depend on how the departure actually went (a fair send-off vs. feeling undervalued or blocked), giving the Competitors relationship system real texture and history instead of every rival starting from zero. This is also a natural, low-cost expression of the chef-as-mentor angle of the core fantasy, even though the genre itself is tycoon-first (Phase 3's genre clarification) — training people up is itself a chef-identity activity, not just a business function.

**General Manager (added per Aaron):** a specific, hireable role that formalizes delegation. Once a location has a GM assigned, that restaurant runs autonomously against its current standing policies (schedule templates, supplier choices, menu) without requiring the player's ongoing attention — the GM's own skill/competence determines whether quality holds up over time, not a rubber-stamp abstraction (the direct fix for the Big Ambitions "manager who doesn't manage" flaw). The player can check in on or directly intervene in a GM-run location at any time (inspect, override a policy, replace the GM), but is never required to. This is the concrete mechanism that makes multi-location play viable without turning into simultaneous full-attention management of every restaurant at once — exactly the relief Aaron flagged as necessary once you're running more than one location.

**Porter / Dishwasher role (added per Aaron):** a hireable staff role (present in the real kitchen brigade studied in Phase 2) whose staffing level and skill produce a **cleanliness/sanitation score** for the restaurant. This is what health-inspection Events actually weight against — sanitation is a staffing and coverage decision like everything else, never a manual dish-cleaning chore for the player.

**Failure modes:** understaffing (service collapse), overstaffing (margin erosion), a GM who is under-skilled or mismatched to a location (slow, visible quality drift rather than a sudden collapse — giving the player time to notice and intervene), delegation failure more broadly if a GM's competence doesn't actually matter (must not repeat the Big Ambitions flaw), understaffed sanitation leading to inspection risk.

**Expansion hooks:** celebrity chef hires, a training pipeline/culinary school system, personality traits affecting morale and team chemistry (Two Point Hospital lesson), GM personality/leadership style affecting how a delegated restaurant drifts over months (a slow, legible story generator in its own right), corporate HR tooling at empire scale.

### Recipes

**Purpose:** the menu-level unit that converts ingredients, labor, and equipment into sellable dishes, and the direct vehicle for the Kasavana-Smith menu engineering system flagged as a central mechanic in Phase 2.

**Knows:** its ingredient list, prep complexity (station and time requirements), price, target segment/cuisine tag, and a *live-computed* contribution margin.

**Can:** be added to or dropped from the menu, priced, enhanced/leveled (the Dave the Diver recipe-enhancement lesson), and — critically — have its margin recompute automatically the instant a linked ingredient's supplier or cost changes, which is the direct architectural fix for Restaurant Empire II's worst flaw (Phase 1): a supplier decision must never require the player to manually re-edit every recipe that uses it.

**Cannot:** exist without its required ingredients being sourced; be prepared faster than its assigned station and staff skill allow.

**Outputs:** its Star/Plowhorse/Puzzle/Dog classification, per-dish margin, contribution to segment fit and reputation.

**Interactions:** reads live cost from Ingredients/Suppliers; consumed by Kitchen via station assignment; judged by Customers against their preferences; a source of Marketing hooks (signature dishes); the central object of Phase 7's tradeoff audit.

**Failure modes:** menu bloat (too many recipes fragments kitchen efficiency and station assignment — an echo of Restaurant Empire II's supplier-propagation pain if not guarded against); a stale menu with unaddressed Dogs; segment mismatch with the restaurant's actual customer base.

**Expansion hooks:** seasonal/limited-time dishes, regional cuisine content packs, a signature chef's-table system, community recipe sharing (ties to the modding goal in Phase 9).

**Featured slots, menu cohesion & tasting menus (added per Aaron):** rather than simulating a full spatial menu-layout/eye-tracking system (too much modeling effort for an unclear payoff), the menu has a small number of "Featured" slots (2–3 to start, more as the restaurant grows) that the player assigns dishes to; a Featured dish gets an order-rate boost, giving the player the real menu-engineering move of promoting a Puzzle into visibility as one clean decision rather than a layout tool. Separately, the whole menu carries a **cohesion signal** against the restaurant's declared class/identity — mixing fine-dining and fast-food price points and styles on one menu dings it. **Refined per Aaron's later direction:** this is not a standalone tracked score any more than Furniture/Layout's ambiance is — it feeds in as the same kind of small, bounded nudge on Customer satisfaction/Reputation, no dashboard tile of its own. The one difference from furniture: because a confused, undifferentiated menu is one of the most real, well-evidenced restaurant failure causes (Phase 2's research, unlike decor quality, which was never flagged as a top failure cause), this is specifically one of the signals the **Advisor** calls out directly when it drifts ("your menu's spanning some very different price points — might be worth narrowing focus"), rather than something the player only discovers after satisfaction has already quietly dropped. A **Chef's Tasting Menu** is a deliberate, limited-time special the player can launch (fed by R&D/signature-dish output) for a reputation and revenue spike — the "combo" payoff happens as an event the player chooses to launch, not a calculation running under every order.

### Research & Development (added per Aaron — Phase 4 addendum)

**Purpose:** the deliberate, investable mechanism for creating new recipes and signature dishes and for tracking shifting food trends, so the menu doesn't quietly calcify — this is the actual answer to Phase 2's "stale menu" failure mode, distinct from and slower-moving than the per-dish Kasavana-Smith classification. Explicitly designed to be a periodic, weighty decision rather than a constant chore, per Aaron's direction: "it doesn't need to be a constant always thought about thing, but you should have to stay relevant."

**Knows:** available R&D budget and time, the player's/chef's current skill level (which gates what tier of dish is researchable — the mechanical form of Phase 3's dual chef/empire progression), current trend data (which cuisines/styles/ingredients are rising or fading, tracked as a slowly-shifting market state rather than something that changes service-to-service), and the menu's overall freshness/relevance score relative to those trends.

**Can:** invest budget and time into a new recipe or signature-dish project (a real project with duration and cost, not an instant unlock); chase a rising trend early for higher risk/reward (a trend can fade before the dish ships); sit dormant for long stretches without immediate punishment, since relevance decays slowly, not per-service.

**Cannot:** produce a market-ready dish instantly; guarantee a trend bet pays off — trend forecasting is uncertain by design, a genuine decision under incomplete information; bypass the need for actual kitchen capacity to execute whatever it produces.

**Outputs:** new Recipe candidates; a menu freshness/relevance score distinct from any single dish's margin or volume (a menu can be full of individually strong Stars and still read as stale if nothing has changed in a long time); a "signature dish" flag that a recipe can earn, which becomes tied to the restaurant's brand.

**Interactions:** gated by Economy (R&D budget) and by Employees/chef skill (who's actually capable of leading a given R&D project). **Clarified per Aaron, updated Phase 7:** while solo with one restaurant, the player character personally *is* the R&D function. The original framing said this competes against "time spent cooking," but that's stale now that Phase 5 made Service watchable/skippable rather than a hands-on requirement — the real cost is **calendar time and capacity**: an R&D project occupies real in-game days or weeks during which that capacity isn't free for something else (another R&D bet, closer attention to Prep-phase decisions elsewhere). Once affordable, a dedicated R&D chef role can be hired to add parallel capacity, the same delegation logic already governing General Managers. R&D produces directly into Recipes; a signature dish is a strong Marketing hook and a Reputation contributor; regular/returning Customers are more sensitive to menu staleness than first-time visitors, so trend-drift should influence Customers' preference model over the long run, not just at the Recipe level; Competitors can also chase the same trends and occasionally beat the player to one.

**Failure modes:** ignoring R&D lets the whole menu decay in perceived relevance even while individual dishes still score well on paper — a distinct, slower failure mode than any single Dog; chasing a trend that fades before the dish ships (wasted investment); over-investing in R&D at the expense of operating cash flow.

**Expansion hooks:** seasonal trend cycles tied to Time; regional trend variation once the empire spans multiple markets (a dish trendy in one city, not another); a food-critic circuit that scores innovation/creativity as its own Reputation sub-score; celebrity-chef collaborations as a fast-tracked R&D option.

### Ingredients

**Purpose:** the raw-input layer — stock, freshness, and cost tracked per item, the direct game-mechanical form of Phase 2's par-level research.

**Knows:** current stock, par level (min/max, tuned per item by spoilage rate and demand), spoilage rate, quality tier (set by the chosen Supplier), unit cost.

**Can:** be ordered via Suppliers, consumed by Recipes during prep, spoil/waste if over-stocked, run out (forcing an 86'd item) if under-stocked.

**Cannot:** be conjured on demand; skip FIFO rotation without a waste penalty.

**Outputs:** waste cost, stockout risk, a quality ceiling on any dish that uses it.

**Interactions:** sourced from Suppliers under the propagating policy model; consumed by Recipes/Kitchen; tracked in Economy as COGS.

**Failure modes:** over-ordering (cash tied up, spoilage losses); under-ordering (mid-service stockouts, lost sales, reputation hit).

**Expansion hooks:** seasonal availability windows, a local-sourcing/farm-to-table reputation bonus, supply-chain shock events (a direct Events hook).

### Suppliers

**Purpose:** the policy layer for ingredient quality, cost, and reliability — this system exists specifically to prevent Restaurant Empire II's central failure (Phase 1): a strategic decision must propagate everywhere it applies, not require per-instance manual editing.

**Knows:** price and quality tier per ingredient, delivery reliability, relationship/loyalty standing with the player.

**Can:** be switched — a single decision that automatically updates every recipe and location currently depending on that ingredient, with any exceptions requiring explicit opt-in rather than every instance requiring opt-in by default; offer bulk discounts as empire purchasing power grows (Phase 2's franchising research); suffer shocks/disruptions (an Events hook).

**Cannot:** be pressured into lower pricing without either a real relationship or real volume behind it.

**Outputs:** ingredient cost and quality, feeding directly into Recipes' live margin calculation.

**Interactions:** this is the concrete "policy object" the whole architecture is built around; it must read as a single source of truth for every dependent Recipe and Restaurant location.

**Failure modes:** poor reliability causing surprise stockouts; over-commitment to a single supplier with no diversification, exposed to shocks.

**Expansion hooks:** exclusive/celebrity ingredient partnerships, empire-wide purchasing contracts, local-vs-national supplier tradeoffs as the empire grows.

### Furniture / Layout

**Purpose:** the physical placement layer — decor, seating, and station equipment — that primarily sets capacity and the kitchen's throughput ceiling. This is the build-mode system flagged in Phase 1's GitHub research (FreeSims/FreeSO's grid placement and object systems) as directly reusable as a *reference*.

**Scope note per Aaron:** this is explicitly *not* a standalone score for the player to manage or optimize. There is no separate "decor/class score" to track. Layout and furniture quality feed in as a small, bounded modifier on Customer satisfaction, not a headline metric — uncomfortable chairs or bare walls should nudge sentiment mildly, never tank the restaurant. Starting out having to cut corners on a tight budget is the intended early-game experience (the Phase 3 fantasy), so this input is deliberately capped low enough that it's forgiving, not a trap.

**Knows:** object type, cost, capacity/throughput contribution, a rough comfort/quality tier, placement position.

**Can:** be placed, moved, removed, or upgraded; advertise its own available interactions and a priority score for how attractive using it is right now — the SimAntics smart-object pattern from FreeSO, applied here so a stove advertises "cook" and a table advertises "seat and serve," rather than every interaction being hard-coded bespoke logic.

**Cannot:** violate placement/spacing constraints or exceed the restaurant's physical footprint; swing Customer satisfaction by more than a small, capped amount on its own.

**Outputs:** seating capacity, station throughput, a small bounded comfort/ambiance contribution to Customer satisfaction.

**Interactions:** contributes lightly to Customers' satisfaction calculation alongside food quality, service speed, and price (which remain the dominant factors); read by Kitchen (station throughput); read by Employees (station assignment).

**Ambiance presets:** rather than independent lighting/music/acoustic sliders (real effort to tune, easy to get wrong, and a maintenance burden over time), the restaurant has a small set of named **service-style presets** — for example Quick & Casual, Relaxed & Intimate, Trendy & Energetic, Business-Friendly — each bundling a genuine mechanical pacing/spend tradeoff (fast turnover and more covers vs. slow lingering and bigger checks) and loosely matched against the Customer archetypes above. A mismatch here is also a mild sentiment nudge, not a scored penalty — one choice per restaurant, changeable, not something that needs constant retuning.

**Failure modes:** layout bottlenecks (an impressive dining room fed by an undersized kitchen) — this remains a real, mechanical failure mode since it affects actual throughput; wasted footprint. Decor/ambiance mismatch is deliberately *not* listed as a serious failure mode — at most a small satisfaction nudge, by design.

**Expansion hooks:** physical building expansion — new floors/wings, the literal mechanical form of "grow your building" from Phase 3; themed remodels; outdoor seating; a minimal food-truck-scale layout variant.

### Kitchen

**Purpose:** the production engine that converts staffed stations and ingredients into finished dishes at a real throughput rate — the primary site of moment-to-moment tension during live service, built on the Escoffier brigade model from Phase 2.

**Knows:** per-station queue state, ticket backlog, station-to-recipe compatibility, the expeditor/pass bottleneck state.

**Can:** fire and route tickets to stations by brigade role; visibly bottleneck when under-resourced; optionally draw against a shared depleting resource for tension during a given service (the Dave the Diver wasabi pattern, usable per-restaurant where it fits the concept).

**Cannot:** exceed combined station-plus-staff throughput; produce a dish missing a required ingredient or station.

**Outputs:** ticket completion time (feeds Customer patience directly), dish quality (feeds satisfaction and Reputation), labor cost incurred.

**Interactions:** staffed by Employees; built from Furniture (stations); fed by Ingredients; produces against Recipes; its performance is read indirectly by Customers via wait time.

**Failure modes:** bottleneck cascades (one slow station backs up the entire pass); running out of a key ingredient mid-service; an understaffed station during peak demand.

**Expansion hooks:** distinct kitchen "modes" (fine-dining plating time vs. QSR speed), specialty equipment unlocks, a compact food-truck kitchen variant, a delivery-only ghost-kitchen variant.

### Economy

**Purpose:** the financial ledger and the always-visible health layer — prime cost is the headline metric per Phase 2, surfaced constantly rather than buried in a menu. **Scope note per Aaron:** this is the player's own restaurant P&L, not a surrounding macroeconomy. Money and budget are genuinely central to the game, but there is no simulated external economic layer — no modeled inflation, interest-rate cycles, or market-wide booms/busts driven by a separate world-economy system. Real financial pressure comes from the player's own choices (staffing, sourcing, pricing, expansion timing), not from background macro noise the player can't see or act on.

**Knows:** cash on hand, revenue, COGS, labor cost, rent/overhead, loan terms, and a live prime-cost percentage.

**Can:** take and service loans (simplified, but anchored to real interest/royalty percentages from Phase 2, held basically fixed rather than fluctuating with a simulated market); go negative, triggering a real and legible failure risk — never an opaque bankruptcy trigger; fund expansion, build-out, and hiring; gate the capital requirement for a second building (Phase 3's expansion-by-economics principle); carry ongoing revenue-share obligations from a won Marquee Opportunity (Events) as a distinct, long-running commitment alongside loans — a different kind of financial exposure (low upfront cost, uncapped long-term cost) worth surfacing on the Dashboard the same way prime cost is.

**Cannot:** be gamed by hiding costs — prime cost must always reflect true combined food and labor spend; simulate external market forces the player has no visibility into or control over — every cost swing in the game should trace back to a decision (a supplier switch, a staffing choice, a menu change), never an invisible economic cycle.

**Outputs:** prime cost %, net profit, available expansion capital, a failure-risk signal.

**Interactions:** reads from every cost-generating system (Ingredients, Employees, Furniture, Suppliers); reads revenue from Customers; gates Restaurant expansion.

**Failure modes:** cash-runway exhaustion (the real-world #1 failure cause per Phase 2); unnoticed prime-cost creep; over-leveraged expansion.

**The failure state — rival buyout (added per Phase 10, finding 1.3, per Aaron).** Phase 10 found that this design committed to failure being legible and non-opaque without ever specifying what actually *happens* when you run out of money. The answer: **a rival buys you out.** You lose ownership; the restaurant itself persists under their name and flips onto the Power Rankings as theirs, and the buyout is written permanently into the Restaurant Legacy Log.

This is the right answer for several reasons at once, not just tonal fit. It uses the Competitors system already built rather than inventing a new endgame mechanic. It is non-total — the place you built continues to exist, which is far more affecting than a wiped save. It converts the game's harshest moment into its most retellable one, directly serving Phase 3's principle 6 ("design deliberately for moments of joy" — and its inverse: designed, memorable defeat beats an anonymous fail screen). And it lands hardest, in the best way, when the buyer is a rival with real history — a former employee's protégé restaurant (Employees) buying out the mentor who trained them is a story the player will tell.

Mechanically it must stay legible and pre-announced, per Phase 3's "hard but never unfair" principle: as cash and prime cost cross risk thresholds, the Advisor issues escalating financial-health warnings (already in the trigger table) and, before the end, an explicit acquisition-interest signal — a rival circling is visible before it's terminal. **Which rival** buys you should be determined by their relationship, posture, and relative size, so the identity of the buyer is itself an earned consequence of how the player played, not a random draw. A buyout offer accepted *voluntarily* while still solvent — cashing out on your own terms — should also be available, which turns the same mechanism into a legitimate strategic exit rather than only a punishment.

*Still open for a later pass:* what the player does next after a buyout — start fresh with reputation and relationships carried over, keep playing as a smaller operator elsewhere, or end the run. Worth deciding before M3, but not blocking M0/M1.

**Note on the two endings.** With the career clock and retirement added to Time, the game now has a matched pair of terminal states: **retirement** (you choose when you're finished; the Legacy Log becomes a career retrospective) and **buyout** (a rival decides for you). Having both is worth more than either alone — it means every run ends with a story about whether the player got out on their own terms, which is a far better frame than a win/lose screen and costs almost nothing extra to build given both mechanisms already exist.

**Expansion hooks:** franchisor royalty/cut mechanics if the player becomes a franchisor; consolidated multi-unit financials at empire scale; investor/equity mechanics for major expansion pushes.

### Competitors / Rivals

**Purpose:** relationship-bearing NPC restaurateurs who generate background market pressure, competitive events, and — per Phase 3's founding principle — genuine alliance or rivalry arcs, not fixed antagonism. This is a real departure from every reference game studied in Phase 1, none of which modeled competitors this way.

**Knows:** a rough proxy of their own restaurant's state (not full player-level detail), a relationship score with the player, a competitive posture (friendly/neutral/aggressive), their own ambitions.

**Can:** open new locations (background demand pressure); enter competitions and awards; escalate to actively poaching staff or contesting a lease once posture turns aggressive; respond to player interactions (collaborations, gestures, direct confrontation) that shift the relationship score; become an ally (shared promotions, referrals) as plausibly as an enemy. **Tradeoff made explicit per Phase 7's audit:** cultivating a friendly relationship isn't free upside — it costs the time, gestures, and resources spent maintaining it, which could have gone elsewhere, and it forecloses the more aggressive plays available against a rival you're not on good terms with (undercutting them, contesting a lease, competing harder for a shared Opportunity Pitch or Marquee bid). Without that cost, "always be friendly" would be a strictly dominant strategy rather than a real choice.

**Cannot:** mirror the player's full simulation depth (they're intentionally lighter-weight); escalate without a legible trigger tied to relationship score or relative empire size — no arbitrary aggression, per Phase 3's fairness principle.

**Outputs:** local demand pressure, competitive event triggers, relationship-gated bonuses or penalties.

**Interactions:** read by Events (weighting rival-related events by relationship and relative size); read by Customers (rival reputation affects local demand split); can interact directly with Employees (poaching) once posture escalates.

**Failure modes:** escalation that feels arbitrary if not tied to legible triggers; a rival that goes static and loses narrative value over a long game.

**Expansion hooks:** a visible rival map at city/world scale; buyout/acquisition of a failing rival (a literal, earned version of "acquire new restaurants"); celebrity-chef rivalries with their own storylines.

### Marketing

**Purpose:** the demand-generation lever — converts budget and effort into visibility, review recovery, and word-of-mouth amplification, without ever substituting for real quality.

**Knows:** available channels and budget, active campaigns, review-response opportunities (the evidenced Phase 2 mechanic: responding to reviews measurably improves return-customer rate).

**Can:** run campaigns (a real cost-vs-reach tradeoff), respond publicly to reviews, sponsor or enter competitions, target specific customer segments.

**Cannot:** substitute for actual food or service quality — marketing can get a first-time customer in the door, but Reputation, driven by real experience, governs whether they return. This boundary exists specifically so Marketing never becomes a "manager who doesn't manage" (the Big Ambitions flaw) — spend without operational substance should not work.

**Outputs:** incremental customer volume and segment targeting, reputation recovery after a bad event.

**Interactions:** spends from Economy; targets Customers; read and reacted to by Reputation and by Competitors (marketing can become a competitive front too).

**Failure modes:** overspending on marketing beyond the restaurant's actual capacity to serve the resulting demand (a real, instructive failure tying straight back to Kitchen/Restaurant capacity); propping up a fundamentally weak concept, mirroring the real "undifferentiated concept" failure cause from Phase 2.

**Expansion hooks:** an influencer/critic relationship system, empire-wide brand campaigns as a franchisor tool, viral/social-media event chains.

### Reputation

**Purpose:** the single legible score that gates customer volume and segment mix — the Cooksta-style mechanic from Phase 1/2 — and the primary state the Event director reads to decide pressure.

**Knows:** current score plus sub-scores (food quality, service speed, value, ambiance), segment-specific perception (a luxury diner and a budget diner don't weight the same experience identically), trend direction.

**Can:** rise from good service, reviews, and marketing; fall from bad experiences, walkouts, or negative events; be partially recovered through review-response; decay slowly toward a "true quality" baseline over time, so it's a living signal, not a one-way ratchet.

**Cannot:** be bought outright — marketing can influence visibility, never manufacture reputation wholesale, matching the real "no shortcuts to trust" finding from Phase 2.

**Outputs:** customer volume, customer segment mix, an effective price ceiling, eligibility for awards, competitions, and certain Events.

**Interactions:** computed from Customer satisfaction outcomes; read by Customers (arrival decisions), by Marketing, by Events, and by Competitors.

**Failure modes:** a death spiral (bad reviews → fewer/wrong customers → can't afford fixes → worse reviews) — must stay recoverable, but genuinely hard, matching real-world stakes from Phase 2.

**Power Rankings (added per Aaron — promoted from expansion hook to a real, designed feature):** Reputation gets a visible, competitive expression through nested leaderboards at City/Town, State/Region, Country, and World tiers, ranking named restaurants against each other. Deliberately **two parallel rankings, not one** — this directly resolves the open question flagged back in Phase 3 about whether single-location mastery is a valid endgame in its own right: a **Prestige rank** (driven purely by Reputation/quality — a single extraordinary restaurant can climb this alone, no empire required) sits alongside an **Empire rank** (driven by scale — revenue, location count, footprint), so "world's best restaurant" and "biggest empire" are both visible, legitimate, separately trackable goals rather than one implicitly being the "real" ending. Climbing from one geographic tier to the next is gated by actually reaching a competitive threshold at the tier below, never purchased or artificially unlocked. Known Competitors appear by name on these lists, so overtaking a specific rival with a real history (possibly a former employee's protégé restaurant — see Employees) carries personal stakes a raw leaderboard number never would.

**Top-tier eligibility, refined per Aaron:** City and State/Regional tiers are open to any single restaurant on Reputation alone — a great neighborhood spot can legitimately top its local rankings. Country and World tiers, though, need more than a high Reputation score to enter, or reaching "world's best" becomes trivially fast for a tiny, unexpanded restaurant and undercuts the whole reason to ever engage with expansion (M4/M5). Rather than a new tracked stat (a fifth score to manage, which this design has actively cut elsewhere), eligibility for the top two tiers is an explicit, legible checklist with multiple valid paths — for example, any of: sustained top-tier Reputation held for a long stretch, two or more locations, or a couple of Marquee Opportunity wins. The Advisor states plainly which doors are closest ("not yet eligible for World ranking — here's what would get you there"), so it's never an invisible wall. This keeps single-location "world's best" genuinely possible — a real, rare, hard-won outlier run, the Noma/El Bulli case in real life — while making expansion the fast, reliable route rather than the only one. Fully the player's choice either way.

**Expansion hooks:** celebrity-endorsement spikes; seasonal/annual awards ceremonies as recurring set-pieces layered on top of the Power Rankings (see Events).

### Events

**Purpose:** the state-driven director, modeled directly on RimWorld's storyteller system (Phase 1) — surfaces health inspections, critic visits, rival competitions, supply shocks, and awards, weighted by current simulation state rather than a fixed campaign script (the direct alternative to Restaurant Empire's disliked scripted-campaign structure).

**Knows:** current Economy state (prime cost, cash), Reputation (score and trend), Competitors (relationship and posture), time since the last event of each type.

**Can:** select and fire an event weighted by state (a restaurant with thin cash and rising prime cost draws harder events, mirroring Cassandra's rising-tension design); present real tradeoff choices, not just outcomes; be partially opt-out-able for competitive/rival-sourced content specifically, per Phase 3's principle that rivalry should never feel mandatory or in-your-face.

**Cannot:** fire an event that *punishes the player for playing well* or that arrives with no plausible in-world explanation — the "no arbitrary unfairness" founding principle from Phase 3.

**Loosened per Phase 10 finding 2.4 (Aaron aligned).** The original wording forbade anything "state-disconnected," which over-corrected: an event system that only ever responds to the player's own stats is a *consequence engine*, not a story generator. It produces "I had it coming," never "you won't believe what happened" — and it quietly forbade the very thing the storyteller-tone dial (listed above as an expansion hook) would control, despite RimWorld, the explicit model, working precisely because Randy Random exists.

The operative distinction is **unfair vs. unforeseen.** Unfair is forbidden. Unforeseen is not only allowed but necessary: a supplier's warehouse fire, months of construction closing the street outside, a food trend collapsing faster than anyone predicted, a rival's protégé opening across the street, a neighborhood shifting demographics. These are external, they don't scale with how well the player is doing, they're explicable after the fact, and — critically — they create genuinely novel situations rather than reflecting the player's own numbers back at them. Events should therefore mix **state-weighted pressure** (the Cassandra model, escalating with real risk) with a real minority of **unforeseen external events**, tunable via the storyteller-tone dial.

**Outputs:** one-off narrative/mechanical moments — a critic visit that can spike or tank Reputation, a health inspection, a chance to collaborate with a rival, an award nomination.

**Interactions:** reads nearly every other system's state; writes back into Reputation, Economy, Employees (e.g., a poaching attempt), and Competitors (relationship shifts).

**Failure modes:** event fatigue if fired too often; a static, dead-feeling world if fired too rarely — needs the same kind of tunable weighting RimWorld uses.

**Expansion hooks:** a full storyteller-style selectable pacing/tone dial (aggressive/relaxed/chaotic, echoing Cassandra/Phoebe/Randy) as a player-facing difficulty option; recurring citywide awards ceremonies as set-pieces; seasonal event content.

**Two named event types confirmed per Aaron:**

*Health/Fire Inspection* — weighted specifically off the Employees system's cleanliness/sanitation score (itself a function of porter/dishwasher staffing, not a manual chore), so a restaurant that's actually kept clean rarely gets flagged, and one that's neglected its sanitation staffing draws inspections more often — a legible, earned consequence rather than a random ambush. When it fires, the player chooses: maintain standards and pass on the merits, attempt a rushed mid-service cleanup, or bribe the inspector — bribery must carry real downside risk (a bigger penalty if caught) so it's a genuine gamble, not a free pass, consistent with Phase 3's "hard but never unfair" principle.

*VIP / Critic Visit* — a rare special event, either pre-booked (advance notice, letting the player specifically prepare: clean up, staff up, feature their best dish) or a surprise walk-in that tests the restaurant's baseline standards as-is. Ties directly to the Influencer/Critic customer archetype (Customers) and carries an outsized, direct swing on Reputation. Deliberately rare — a special occasion, not a recurring mechanic to plan the whole week around.

*Opportunity Pitch (added per Aaron)* — a rare event offering a genuine bid-or-pass decision: a prime new lot, an exclusive supplier partnership, a trending concept, a festival or collaboration slot. Bidding costs real money and carries real risk of flopping. Passing is free, and **critically, passing usually has no consequence at all** — most of the time nobody else takes the opportunity either, because plenty of real ones are genuinely mediocre, confirming a reasonable pass rather than punishing it. Only occasionally does a Competitor pick up a passed-on opportunity, and even then it can flop for them too — the rare, real sting is specifically when it *is* taken and it *does* pay off, visible as their rank climbing on the Power Rankings (Reputation). **Whatever the outcome, it's meant to be a lasting fixture, not a transient bonus that fades in a week** — a won opportunity becomes a permanent part of the winner's restaurant or story (a new location, a lasting signature partnership, a rival's rank shift that sticks), the same way "the corner lot I passed on is now my biggest rival's flagship" should still be true and referenceable many hours later, not forgotten after the event resolves. This generalizes the same trend-timing tension already built into R&D (chase a rising trend early for risk/reward, a rival can beat you to it) out to locations and partnerships, not just dishes, and it's a natural feeder into the "restaurant history/legacy" system still flagged as open (Phase 3, principle 6).

*Marquee Opportunities & bidding wars (added per Aaron)* — occasionally an Opportunity Pitch is marquee-tier: a celebrity or well-known personality wants to open a chain or lend their name to a concept. Rare and high-stakes enough to draw more than one bidder, turning it into a real back-and-forth rather than a single bid-or-pass call — the player can raise a cash offer, or get creative with the deal structure itself (a share of ongoing profits, an exclusivity term, a marketing commitment) instead of just outbidding on price. This is a genuinely different tradeoff from a standard pitch: a bigger upfront cash offer is an immediate, real hit to cash reserves (Economy), while a profit-share-style incentive costs little now but becomes a long-running commitment against that venture's future earnings — a steal if the venture stays modest, a real regret if it turns into a runaway hit. The likely cost of a given incentive should be roughly legible before the player commits (a projection, not a guarantee), so a generous deal is a real judgment call, not a hidden trap, per Phase 3's "hard but never unfair" principle. Losing a bidding war to a Competitor is one of the more dangerous ways a rival can get stronger — a named rival landing a marquee partnership is a visible, lasting shift on the Power Rankings, not a shrug-worthy miss.

### Time

**Purpose:** the clock that paces live service, day/week/season cycles, and long-run progression — must be player-adjustable, per Pizza Connection 2's direct lesson from Phase 1 that an uncontrollable pace kills a management sim.

**Knows:** current tick/day/week/season, service hours vs. off-hours, current speed setting.

**Can:** run in either of two player-selected modes (both confirmed with Aaron, detailed in Phase 5) — **live play** at 1x/2x/3x where the player is present and can act at any moment, the Sims model; or **jump ahead** by a day, week, or month, where the sim resolves on its own and pauses only when a decision needs the player. Also: drive recurring cycles (weekly business review, payroll, rent); gate longer-run unlocks (staff training completion, construction time); and track the career clock below.

**Career length & retirement (added per Aaron).** A run has a soft horizon rather than an open-ended one: the player can play as long as they like, up to a **maximum career of roughly 40 in-game years**, and can choose to **retire at any point** before that. This does a surprising amount of work for the design:

- It gives the game a **non-failure ending**. Until now the only defined terminal state was the rival buyout (Economy) — losing. Retirement is the earned counterpart: you decide when you're done, and the Restaurant Legacy Log becomes a career retrospective rather than an open-ended feed.
- It makes the **generational content coherent**. Employees retiring, regulars returning years later with their kids, a protégé leaving to become a rival and building their own empire over decades — all of these need a multi-decade canvas, and 40 years provides one without being literally unbounded.
- It creates **mild, healthy time pressure**. Not a countdown, but real opportunity cost: the years spent perfecting one restaurant are years not spent expanding, and vice versa. That gives the Phase 3 expand-vs-perfect choice actual weight instead of letting the player eventually have everything simply by playing long enough.
- It provides a natural home for a **capstone decision at retirement** — worth designing properly later, but the obvious candidate given systems already built: hand the empire to a protégé you developed (Employees), sell it, or break it up. That closes the mentor thread the hiring/promotion system opens.

*Open for a later pass (not blocking M0/M1):* whether the player character ages mechanically over those 40 years (skill ceilings, stamina) or whether the clock is purely a calendar. Simplest defensible answer is a calendar with cosmetic aging — mechanical decline risks punishing long play, which cuts against the fantasy.

**Cannot:** demand constant granular attention — routine recurring costs and reviews should run against standing policy/templates (the Factorio "set it up once" pattern), not require re-input every cycle.

**Outputs:** active service windows (when Kitchen and Customers are live), billing cycles, the long-run pacing input Events and Competitors read.

**Interactions:** paces essentially everything; is the backbone Events reads for "time since last event of this type."

**Failure modes:** pacing that's either tedious (Pizza Connection 2's uncontrollable crawl) or too fast to plan around.

**Expansion hooks:** seasonal menus and events, multi-timezone pacing for international expansion (Phase 9), a historical/replay mode.

### Advisor + Dashboard — one system, two faces (merged per Phase 10 finding 2.2)

**Merge decision.** These were written as two systems and are not two systems. Their contracts already referenced each other circularly (the Dashboard "knows the Advisor's top suggestions"; the Advisor "outputs a digest — see Dashboard"), neither holds any state of its own, neither is a simulation, and both are read-surfaces over the same underlying data. The charter explicitly asks "can two systems become one?" — this is the clearest yes in the document. They are one implementation unit: **the Advisor is the logic layer** (reading state, generating tiered suggestions and interrupts) and **the Dashboard is its presentation surface**, with a fixed field list. Merging them prevents the two drifting apart in implementation and corrects Phase 8's sizing, which listed them as peers of Kitchen and Economy — they are meaningfully smaller and cheaper than either. Both sections below remain accurate as written; they describe two halves of one component.

### Advisor (logic layer)

**Purpose:** a proactive, in-fiction recommendation layer — framed as your sous chef/GM/team giving you advice — that answers a pattern in almost every piece of feedback this phase: don't make the player discover the optimal move through trial and error, suggest it. This is the mechanism that keeps depth from becoming homework.

**Knows:** current state across every other system (Recipes' Star/Plowhorse/Puzzle/Dog classifications, Reputation's trend and sub-scores, Employees' morale/turnover-risk signals, R&D's trend data, Economy's prime cost).

**Can:** surface specific, actionable suggestions; propose a staff service-script change and let the player test it before committing; be ignored entirely with no penalty — it's advice, never a requirement.

**Three tiers of Advisor authority (revised per Aaron — supersedes the earlier prescriptions-vs-observations split).** Phase 10's finding 2.1 was that an Advisor which says "feature this dish" is *performing* the Kasavana-Smith analysis and handing over the conclusion, dissolving one of the game's three central strategic systems. The first fix was a two-way split: prescriptions for chores, bare observations for strategy. Aaron's refinement corrects a real weakness in that: a bare observation ("your risotto has the highest margin and lowest order count") risks being unhelpful — it hands the player a data point and makes them work out what to do with it, which is the homework problem principle 5 exists to prevent. His model is better: the Advisor **asks, with its concrete reasoning visible, and the player answers** — "we're sitting on a lot of fish — want to feature the fish dish tonight?", "dessert sales are low — should we try upselling?"

What changes is not how much the Advisor *knows* but how much authority it has, and over what. Three tiers:

1. **Chores and hygiene — the Advisor just handles it, or flags it flatly.** Porter staffing thin ahead of a rush; a par level drifting; a small overdue raise. If the right answer is nearly always the same, don't spend the player's attention on it — automate it under standing policy, or state it plainly as a warning. These should rarely be interrupts.

2. **Tactical proposals — a question, with reasoning, answered yes or no.** This is Aaron's model and it should be the bulk of what the player sees, and the bulk of the Time Control interrupts (Phase 5). The rule that keeps it honest: **only prompt where "no" is genuinely defensible.** Featuring the fish dish costs a scarce Featured slot that something else wanted; pushing dessert upsells costs server time and cuts against the Romantic Couple archetype's slow-meal expectation. If "yes" is always correct, it isn't a tactical proposal — it's a chore, and belongs in tier 1.

3. **Strategic decisions — never a yes/no prompt at all.** Menu overhauls, pricing strategy, R&D direction, expansion, Marquee bids, GM assignments. The Advisor may surface an *opportunity* ("you could afford a second location now," "this trend window closes in about three weeks") but the player initiates and configures the action themselves. This tier is the direct protection against the failure mode that a game made entirely of yes/no prompts is not a strategy game — it's a notification inbox. The player must retain a category of decisions they reach for unprompted.

The distinction Aaron's framing gets right, and worth stating explicitly: the Advisor is **your staff, not an oracle.** "We have too much fish, want to feature it?" is a sous chef noticing something concrete on the ground and proposing a response. "Feature this dish — it's a Puzzle" is the game handing down its own strategic verdict. Same information, entirely different relationship, and only the first one leaves the player as the person actually running the restaurant.

**Opportunities, not just problems (added per Phase 10, finding 1.1).** The original trigger table was entirely warnings and remediation, which made the Advisor a problem-flagger and the game a maintenance exercise — badly mismatched to a fantasy about building an empire. The Advisor must also surface *opportunities*: "you're sitting on idle cash with no R&D running," "this trend window closes in about three weeks," "you're within reach of the regional Prestige top ten." These are observations by definition (they name a possibility, not an action), and they're what give the player a reason to engage that isn't damage control.

**Cannot:** make a decision for the player, or replace the underlying systems it reads from — it's a lens on existing state, not a new source of truth.

**Outputs:** a running list of current suggestions; a periodic digest (see Dashboard) of top issues and top wins.

**Interactions:** reads everything; the natural home for the "top 5 complaints, top 5 wins" digest the player asked for; a candidate to eventually narrate the GM-run locations' status too ("Location B is drifting — the GM might need backup").

**Failure modes:** suggestions that are wrong or generic often enough to be ignored — the whole system fails if the player stops trusting it; too many suggestions at once recreating the exact signal-overload problem it exists to solve.

**Expansion hooks:** a personality/voice for the advisor tied to a specific staff character (ties to Employees' personality traits); tunable suggestion frequency; a "why" explanation attached to every suggestion so the player learns the underlying systems over time rather than just clicking accept.

### Dashboard (presentation surface of the Advisor — see merge note above)

**Purpose:** the single home screen that collapses Economy, Reputation, and Advisor output into one legible view — the direct, concrete fix for the "signal overload / spreadsheet, not a game" risk flagged earlier in this phase.

**Knows:** live prime cost and revenue (Economy), Reputation score and trend, the Advisor's current top suggestions, a weekly digest of top 5 customer complaints and top 5 wins (pulled from Customer reviews via Reputation), and any rare Employees-side alerts (a resignation, a GM drift warning).

**Can:** be the default view the player returns to between services; let the player drill into any one metric for detail (e.g., tap a complaint to see which dish/table/shift it's tied to) without requiring that depth by default.

**Cannot:** replace the underlying systems — it's a read surface, not a control surface (changes still happen in Recipes, Employees, Furniture, etc.); demand attention it doesn't need — most weeks, checking it should take seconds, not become its own chore.

**Outputs:** the player's actual moment-to-moment situational awareness — this is arguably the most important UI-adjacent system in the whole design, since it's what makes fourteen-plus simulations feel like one coherent restaurant rather than fourteen separate screens.

**Interactions:** reads from every system; is the natural landing point for Events notifications when they fire.

**Failure modes:** becoming cluttered as more systems get added over the roadmap — needs active curation in Phase 6/7 to keep it to a genuinely small, high-signal set of numbers, not "one line per system."

**Expansion hooks:** per-location dashboards once multi-location play unlocks, with a rolled-up empire-level summary sitting above them; customizable widgets for players who want more detail.

---

### Cross-Cutting Findings (Phase 4 Synthesis)

Three systems act as backbones the rest of the map is organized around, and are worth naming explicitly: **Economy** is read by every cost- or revenue-generating system and is the one number (prime cost) that should always be visible; **Reputation** is the single gate every customer-facing system ultimately feeds into and reads from; and **Suppliers→Recipes** is the concrete policy-propagation pair that exists specifically so this project never repeats Restaurant Empire II's worst flaw.

Two systems carry an architecture recommendation, not just a design one: **Furniture/Layout** and **Kitchen** are strong candidates for the SimAntics-style smart-object pattern (objects advertise interactions and a priority score; agents pick the best available one) flagged in Phase 1 — this is a Phase 9 decision, but it's flagged here because it shapes how these two systems' contracts should be written in Phase 6.

One system is a deliberate, explicit departure from every reference game studied: **Competitors/Rivals** as relationship-bearing NPCs capable of alliance, not fixed antagonists, directly implementing Phase 3's founding principle and something none of Restaurant Empire, Pizza Connection, Big Ambitions, or Two Point modeled.

**Research & Development** was added mid-phase at Aaron's direction and is worth flagging as its own category: it's the system that keeps the menu from calcifying without turning menu maintenance into a constant chore, and it's the clearest mechanical expression yet of the Phase 3 dual-progression principle — chef skill gates what's researchable, empire-scale budget gates how much can be researched at once.

**Economy's scope was deliberately narrowed** at Aaron's direction: no simulated macroeconomy (inflation, interest-rate cycles, external booms/busts). Money stays central to the game, but every financial swing must trace back to a player-visible decision — a supplier choice, a staffing call, a pricing move — never invisible background noise. This is a hard boundary against the project drifting into "painful, too-realistic" territory, and it should be read as governing every future finance-adjacent decision (loans, franchising royalties, expansion costs) in later phases.

---

### Risks & Watch-Items (running list, revisit explicitly at Phase 7)

1. **Signal overload / "spreadsheet, not a game."** As currently mapped, the player could be looking at up to four separate "is this doing well" signals at once — the Kasavana-Smith margin/volume classification (Recipes), R&D's menu freshness score, Reputation's sub-scores, and whatever Events is reacting to. **Status: largely mitigated** by two new systems added in direct response to Aaron's feedback — the **Advisor** (surfaces specific, actionable suggestions instead of requiring the player to interpret raw stats) and the **Dashboard** (one home screen collapsing Economy, Reputation, and the Advisor's digest into a single view). Still needs active curation in Phase 6/7 as more systems get added, so the Dashboard doesn't slowly recreate the exact problem it was built to solve.

2. **Employees becoming a second, HR-sim-scale game bolted onto the restaurant sim.** Original concern: needs/morale/scheduling/turnover, done in enough depth to matter, risks turning into its own full mini-game and demanding constant headcount attention, especially at multi-location scale. **Status: largely mitigated** by two changes made directly in response to Aaron's feedback — turnover/poaching/retirement are now explicitly rare, state-driven events rather than constant simulated churn, and the General Manager role formally delegates day-to-day operation of a location so multi-restaurant play doesn't require simultaneous full-attention management everywhere at once. Worth re-checking once Phase 6 contracts pin down exact interaction frequency.

3. **Flat scaling within a single restaurant.** Since expansion is capital-gated and "expand in place first" (Phase 3), the single-restaurant growth arc (more floors, more seats, more stations) needs to introduce genuinely new kinds of decisions at each tier, not just bigger numbers on the same decision — otherwise it repeats Project Highrise's exact failure mode over many hours before multi-location ever becomes available. **Status: partially addressed** — a food-truck/pop-up/farmers-market pre-expansion mechanic (Aaron's addition, this session) gives the player a genuinely different kind of decision (test a new market/cuisine at low risk) before a second brick-and-mortar location is affordable, which breaks up the single-restaurant stretch with something other than "bigger numbers." Still needs explicit tier-by-tier content design in Phase 6/8.

4. **"The game suggests, you decide" needs to be applied consistently, not just where it's convenient.** The Advisor and Dashboard are strong answers for menu/reputation/staffing signals, but several other systems added this session (featured slots, ambiance presets, tasting menus, R&D bets) will also generate "what should I do" moments — these should route through the same Advisor pattern rather than each growing its own bespoke hint system. **Status: open — treat as a design constraint in Phase 6.**

---

## Phase 5 — Core Gameplay Loop

Method: identify the smallest playable decide → react → observe → adapt loop, then stress-test it on paper — text only, no graphics — per the charter's own bar: if it's not fun described in plain language, it's not ready to build.

### Two nested loops, not one

The simulation map produces two loops at different time scales, and conflating them was a risk worth naming up front. The **Service Loop** is the smallest atomic loop — it repeats every shift, every night. The **Business Loop** wraps around it — it repeats every day/week and is where menu, staffing, sourcing, and expansion decisions live. The Service Loop is what makes any given night tense and alive; the Business Loop is what makes the empire fantasy real. Both have to work, but the Service Loop is the one the charter's fun-test bites hardest on, since it's what happens most often.

### The Service Loop — revised per Aaron: Prep → Run → Review, not hands-on-every-shift

**Reframe:** the first draft of this loop implicitly demanded active, moment-to-moment attention every single shift (reassign staff, decide whether to cook, watch patience bars in real time). Aaron's direction is clear and correct: that would contradict nearly everything else this design has committed to — rare turnover, GM delegation, an Advisor/Dashboard that exist specifically so most weeks take seconds of attention. Service happens far too often to demand active piloting every time. The real player decisions live in **Prep** (before service) and **Review** (after service); Service itself is the simulation *running* against those decisions — watchable at whatever speed the player wants, fast-forwardable, or skippable straight to a summary. Service is how the player sees whether Prep was right, not a second labor phase layered on top of it.

**Decide (Prep, before service):** staffing/schedule for tonight (usually already set via standing policy, only touched when something's different), which dishes are Featured, a par-level/prep check, pricing, and — optionally, occasionally — whether the player wants to personally staff a station tonight. This is a deliberate choice for a specific night (a big event, a critic visit, or just wanting to), never a default expectation.

**React (Run, during service):** the simulation resolves customer arrivals (gated by Reputation), ticket flow through stations (per the brigade model and assigned staff skill), and satisfaction outcomes. **Pacing model per Aaron:** this is meant to be genuinely watchable, not just skippable — real-time, visual, at adjustable speed (1x/2x/3x), the same model The Sims uses for watching a household's day play out rather than a coarse watch-or-skip binary. A jump-straight-to-summary option should still exist for players who never want to watch, but watching at a sped-up multiplier is the expected default experience, not a fallback. This also reinforces the case for the Furniture/Layout build-mode and character-movement reference architecture flagged in Phase 1 (FreeSims/FreeSO) — service needs to be worth looking at, not just worth having happen. The player can pause and intervene at any point — reassign a stressed station, step onto the line personally — but nothing about a normal night requires it.

**Observe (Review, after service):** the Dashboard updates — revenue, satisfaction, any standout moments (a walkout, a great table, an Advisor flag) — which is explicitly how the player finds out whether their Prep decisions were sound, per Aaron's framing: service is the verification, not the work.

**Adapt:** adjust staffing, menu, pricing, or prep before the next service based on what Review showed, closing the loop.

**Where the earlier "cook or manage" tension now lives:** as optional, occasional depth rather than the default expectation of every shift — something the player can choose to dip into for a specific night, or that naturally surfaces when they choose to intervene mid-service. This actually resolves last round's flagged risk more cleanly than a design constraint could have: a mechanic doesn't need to be lightweight enough to repeat every single shift forever if the player is opting into it rather than being required to perform it.

**How "chef skill always core" (Phase 3, principle 1) still holds without hands-on piloting every night:** the player's personal skill sets the baseline quality/speed for whichever station they're nominally assigned to even in auto-resolved service, gates what R&D projects are possible, and determines how much of a boost stepping in personally provides on the nights the player chooses to. Chef identity stays real and permanent — it just doesn't require manual piloting every single night to stay meaningful.

**Revised stress-test verdict:** this now sits much closer to the pacing of the reference games already informing this design — Two Point Hospital, RimWorld: set up systems, watch them run, intervene when it actually matters — than to a live-service arcade game, and it's consistent with everything else decided this session. The real design weight shifts onto **Prep** and **Review** being substantive enough to carry the loop on their own, since Service is mostly a watchable/skippable resolution phase rather than the site of the core decision. Whether Prep's decisions (staffing, menu, pricing, sourcing) and Review's legibility (does the player actually understand *why* last night went the way it did) are rich enough to be fun without a hands-on Service phase is the real open question for Phase 6 — not "is Service fun," but "are Prep and Review fun."

### The Business Loop

**Decide:** menu changes (add/drop/feature a dish, launch a tasting menu), supplier and par-level policy, hiring/scheduling/GM delegation, R&D investment, pricing, marketing spend, whether to save toward the next building tier or a second location.

**React:** the Dashboard updates (prime cost, revenue trend, Reputation trend), the Advisor surfaces suggestions against current state, rare Events fire (a health inspection, a critic visit, a rival's move), staff morale/turnover resolves rarely and legibly, ingredients restock against par levels.

**Observe:** the Dashboard as the single home screen, the Advisor's digest (top complaints, top wins, active suggestions) — deliberately designed in Phase 4 so this takes seconds, not a session, in a normal week.

**Adapt:** accept or ignore an Advisor suggestion, adjust a standing policy (schedule template, supplier choice, par level), decide this is the week to launch R&D on a new dish or finally afford that second floor.

**Relationship between the two loops:** the Business Loop mostly manages *policy* (schedules, suppliers, menu, pricing) that the Service Loop then executes against without needing re-input every night — this is the Factorio "set it up once" pattern applied at the seam between the two loops, and it's the direct mechanism that keeps the Business Loop from becoming a nightly chore layered on top of the Service Loop's nightly chore.

### Time Control & Interrupts — resolving Phase 10 findings 1.1 and 1.2 together (per Aaron)

Aaron's answers to two separate Phase 10 findings — "where is the player actually required?" and "how long is a run, and how much service do you watch?" — turned out to describe a single mechanism. Writing it down here as the definitive time model for the game, superseding the looser "watchable/skippable" language above.

**The model — two distinct time mechanisms, not one (clarified per Aaron).** These serve different purposes and both are needed:

1. **Live play at adjustable speed — the Sims model.** The player is present in the restaurant with time running at 1x/2x/3x, watching service unfold and free to act at any moment: pause, reassign a station, step onto the line, comp a table, change what's featured. Speed is a comfort control, not a skip — the player is still playing. This is the default mode early on, and the mode to return to on any night that matters.

2. **Jump ahead — day, week, or month.** The player is *not* present; the sim resolves the intervening time on its own and **stops whenever a decision genuinely needs them** — an Opportunity Pitch arrives, a critic books a table, a valued staff member is weighing an outside offer, an R&D trend window is closing, a rival makes a move, cash crosses a risk threshold. Choosing to skip a month does not skip the decisions inside it; it skips the uneventful stretches and stops at the meaningful ones. An **auto-decide** toggle exists for players who want an uninterrupted run.

Layered across both: a **weekly business review** as the recurring anchor beat — the one predictable moment the player is expected to show up, read the Dashboard, and set direction.

The practical shape this gives a session: live play while learning the systems and on high-stakes nights (a critic visit, a menu launch, an opening), jump-ahead through the stretches where the restaurant is simply running well — which is exactly the rhythm a decades-long career needs in order to stay playable.

**Why this resolves 1.1 (no required-attention phase).** The game now has both a predictable heartbeat (the weekly review) and an unpredictable one (interrupts). The player is genuinely required — not every night, which Aaron correctly rejected, but at real decision points that arrive on their own schedule. "Nothing flagged, nothing to do" stops being a structural hole and becomes what it should have been all along: the *quiet* between interrupts, not the whole experience.

**Why this resolves 1.2 (timescale vs. watchable service).** The contradiction dissolves because the player controls granularity directly. A run can span many in-game years without asking anyone to sit through thousands of services, and the visual layer stops being dead weight — it's what you drop into deliberately, which makes the nights you *do* watch feel chosen rather than endured. Retirements, regulars returning years later, and long reputation arcs all remain viable because months can pass in seconds when nothing needs deciding.

**Precedent.** This is a well-proven pattern rather than a novel bet — Football Manager, the Crusader Kings series, and RimWorld's speed-plus-pause-on-event all run on it, and all three are games about long timescales where the player's real work is judgment at decision points, not continuous input. It fits this project's tycoon-first genre call (Phase 3) considerably better than the original per-night framing did.

**The consequence, stated plainly: interrupt quality is now the game.** If most of the playtime is fast-forward punctuated by decisions, then the decisions *are* the experience, and everything rests on Events and the Advisor generating interrupts that are varied, meaningful, and non-repetitive. This substantially raises the stakes on both systems, and it means M2/M3 playtesting should measure interrupt quality directly ("was that worth stopping for?") rather than treating it as flavor.

**Guardrail on auto-decide.** Auto-decide should cover *routine* interrupts only — a par-level adjustment, a modest raise request, a small supplier substitution. Genuinely consequential decisions (Marquee bids, expansion commitments, GM assignments, anything with lasting or irreversible impact) should always stop the sim regardless of the toggle. Without that boundary, auto-decide becomes a way to accidentally skip the actual game, which would reintroduce Phase 10's finding 1.1 through the back door.

---

### Cross-Cutting Findings (Phase 5 Synthesis)

Service is a **watchable, skippable simulation**, not a second labor phase — the real decisions live in Prep (before) and Review (after), with hands-on cooking available as optional, occasional depth rather than a nightly requirement. This is a better fit for everything else this design has committed to (rare turnover, GM delegation, an Advisor/Dashboard built specifically to keep attention costs low) than the original draft, which implicitly demanded active management every single shift.

The design weight has shifted, and Phase 6 needs to treat it accordingly: it's no longer "make the input model for cooking non-twitchy," it's **"make Prep and Review substantive enough to carry the loop on their own."** Prep needs real, meaningfully different decisions night to night (not just re-confirming the same staffing/menu setup), and Review needs to make cause and effect legible enough that the player learns something concrete from every service, not just a revenue number. This is now the single most important open question heading into Phase 6.

The Business Loop's health still depends entirely on the policy/template pattern actually working as designed across Suppliers, Schedules, and GM delegation — if any of those quietly require re-input every cycle instead of running on standing policy, the Business Loop stops being a once-a-week check-in and becomes exactly the kind of chore this whole design has been working to avoid. Worth explicit verification in Phase 6.

---

## Phase 6 — System Contracts

Method: Phase 4's simulation map already produced knows/can/cannot contracts for every system — that's what a Purpose/Knows/Can/Cannot/Outputs entry *is*. Redoing all sixteen here would be redundant. Phase 6's actual job is to close the specific open questions Phases 4 and 5 left hanging and to write down, precisely enough to hand to an implementer, the handful of contracts that were implied but never made concrete.

### 1. Is Prep substantive enough to carry the loop? (resolving Phase 5's top open question)

Contract: **Prep is never a mandatory screen the player must clear before every service.** Most standing decisions (staffing, supplier choice, par levels) run on templates and require zero daily input — that's intentional, not a gap. Substantiveness comes from *when* real decisions surface, not from forcing one every night: when the Advisor flags something worth a look, when a known event is coming (a pre-booked VIP visit, an expected weekend rush, a closing trend window on an R&D bet), or when the player proactively wants to act (launch a tasting menu, invest in R&D, adjust pricing, finally afford that second floor).

**Updated after Phase 10 (finding 1.1).** The original version of this contract ended by asserting that "a day with nothing flagged is correctly a day with nothing to decide — that's the system working, not failing." Phase 10 found that reasoning to be the single load-bearing rationalization behind a real structural problem: taken together with every other attention-reducing decision in this design, it left the game with no phase that required the player at all. The corrected position: quiet days are fine *because they are compressed away, not sat through* — the Time Control & Interrupts model (Phase 5) lets the player sim past them at day/week/month granularity, while the weekly business review provides a predictable anchor beat and interrupts provide unpredictable ones. Substantiveness is still event/state-driven rather than calendar-mandatory, but it is now backed by a mechanism that guarantees the player is genuinely, regularly needed, instead of an argument for why they might not be.

### 2. Is Review legible enough? (resolving Phase 5's other top open question)

Contract: **every outcome the Dashboard/Advisor surfaces must trace to a specific, named cause, not just an aggregate score.** A complaint in the weekly digest carries a tag (which dish, which station, which shift, which staff member if relevant) and a one-line diagnosis generated from the actual simulation event behind it — "Grill station backed up during Friday 7–9pm rush, average ticket time 11 minutes against your usual 6" — never just "service was slow Friday." Reputation's sub-scores (food quality, service speed, value, ambiance) each need to independently support this kind of drill-down, so a reputation dip is always explicable, never a mystery the player has to reverse-engineer. This is what makes Service feel like real verification of Prep decisions instead of a black box, and it's a direct, load-bearing expression of Phase 3's "hard but never unfair" principle.

### 3. The policy-propagation contract (Suppliers → Recipes, Schedule templates, GM delegation)

This has been promised since Phase 1 ("a decision must propagate, not require per-instance editing") but never specified precisely enough to build against. Concrete contract, in implementation terms without writing actual code: a Supplier is a first-class object with a stable ID. Every Recipe references its ingredients by ID, never by a cached cost value. A Recipe's contribution margin is computed live, at read-time, whenever anything needs it (the Kasavana-Smith classification, the Dashboard), pulling whatever cost the currently-assigned Supplier has right now. Switching Suppliers is a single write to one assignment record — every Recipe and every location reading that ingredient sees the new cost on its next computation, with no per-recipe edit possible even if someone tried. The identical "single assignment, many live readers, no cached snapshots" pattern applies to Schedule templates (a shift slot references a template, it doesn't copy it) and to GM delegation (a GM reads a location's current standing policies directly; they are never handed a frozen snapshot at assignment time, which is what makes their competence — or incompetence — show up as real, live drift rather than a one-time dice roll).

### 4. The Dashboard's exact contents (closing Risk #1 concretely, not just declaring it closed)

To keep "signal overload" actually resolved rather than just asserted, the Dashboard's contents need to be an exact, fixed, small list — not "relevant metrics," which is how scope creeps back in. Proposed final list: cash on hand and prime cost % (Economy); Reputation score plus a one-line trend; this week's top 5 complaints and top 5 wins (Aaron's original ask, kept as-is); the Advisor's current top 1–2 suggestions, not a full backlog; and any rare alert (a resignation, a GM-location drift warning, an upcoming known event). Everything else lives one click away. This exact list is what Phase 7/8 should hold the line against expanding without a deliberate decision to cut something else first.

### 5. The Advisor trigger table (making "the game suggests" implementable, not just a vibe)

A representative first-pass set of concrete triggers, sized for M1/M2 (Phase 8) and expandable later. **Updated after Phase 10 and Aaron's follow-up**, sorted into the three Advisor authority tiers defined in Phase 4.

*Tier 1 — chores: automate under standing policy or flag flatly; rarely worth an interrupt.* Porter/dishwasher staffing dropping below threshold ahead of a known high-volume period; a par level drifting out of band; a small overdue raise where market rate has moved; routine restocking.

*Tier 2 — tactical proposals: a question with visible reasoning, answered yes/no. Only fires where "no" is genuinely defensible.* Surplus inventory on an ingredient → "we're sitting on a lot of fish — feature the fish dish tonight?" (costs a scarce Featured slot); low dessert attach rate → "dessert sales are soft — have the floor push them this week?" (costs server time, cuts against slow-meal archetypes); a Recipe sitting as a Puzzle for two-plus weeks → "the risotto's our best margin and nobody orders it — want it featured?"; a staff member's morale sliding toward a resignation → "Marco's been unhappy — offer a raise or a schedule change?"; a Regular's milestone approaching → "the Hendersons' anniversary is Friday — comp something?"

*Tier 3 — strategic: surfaced as opportunities or observations, never as a yes/no.* Cash reserves and prime cost crossing a risk threshold together (financial health, also feeds Events' weighting and the buyout warning ladder); a GM-run location's reputation diverging downward for two-plus reporting periods; menu price-tier spread crossing the cohesion threshold; an R&D trend window closing; idle cash with no R&D running; proximity to the next Power Rankings tier or eligibility threshold; a rival newly vulnerable, or newly worth allying with; enough capital accumulating to afford a second location. In every one of these the Advisor states the situation and stops — the player goes and acts on it themselves.

This table is the concrete scope boundary for what the Advisor needs to do at first versus what can wait — useful directly for Phase 8's milestone sizing. It doubles as the first-pass **interrupt table** for the Time Control model (Phase 5): these are the triggers that should pause a day/week/month sim, which makes their quality and variety the highest-leverage tuning surface in the game.

---

### Cross-Cutting Findings (Phase 6 Synthesis)

Both of Phase 5's flagged open questions now have concrete answers rather than open risk: Prep is substantive because it's event/state-driven rather than calendar-mandatory, and Review is legible because every surfaced outcome is contractually required to trace to a named cause. Neither of these was true by default in Phase 4/5's looser language — they had to be specified here to actually be buildable.

The policy-propagation contract (section 3) is the single most load-bearing piece of writing in this phase — it's the concrete fix for the exact flaw that made Restaurant Empire II tedious (Phase 1), and it's now specified precisely enough that "did we actually avoid that flaw" is a testable question in Phase 7, not a hope.

The Dashboard's fixed field list and the Advisor's trigger table both exist for the same reason: to make sure "the game suggests, you decide" and "collapse signals into one legible view" are commitments with actual edges, not just good intentions that quietly expand every time a new system gets added. Phase 7 and 8 should treat both lists as things to actively defend, not passively grow.

---

## Phase 7 — Tradeoff Audit

Method: every mechanic in the design, asked the same question — what tradeoff does this actually create? Mechanics that pass get confirmed. Mechanics that don't get flagged for a fix, not just noted and left alone.

### Confirmed — real, meaningful tradeoffs

Menu size (more dishes reach more segments and fill more Featured slots, but load more stations and complicate par levels); ingredient quality tier (premium cost vs. quality ceiling and Reputation); Featured slots (scarce slots force prioritizing one Puzzle or one Star over another, not "feature everything"); menu cohesion (a focused identity vs. a broad one that covers more Customer archetypes); ambiance presets (volume/turnover vs. margin-per-table, and a real match-or-mismatch against your actual customer mix); layout capacity vs. kitchen throughput (a bigger dining room is a liability if the kitchen can't back it up); staffing veteran-vs-green hires (cost and safety vs. risk and ceiling); standing schedule/supplier templates (set-and-forget efficiency vs. getting caught flat by an unusual day the template didn't anticipate); expansion timing (capital saved toward a second location vs. spent on immediate quality, staff, or marketing); GM oversight (trust and free attention vs. active monitoring that catches drift early); Opportunity Pitch (spend and risk vs. conserve and accept a usually-small chance a rival benefits instead); Marquee bidding wars (upfront cash vs. long-run revenue share); Regulars/Advisor prompts (a small real cost — a comped item, tight in the early game — against goodwill and a memorable moment, not a free action).

### Findings requiring action (the audit's actual value)

**Supplier policy propagation cuts both ways, and the design should say so honestly rather than only celebrating the upside.** The whole point of the propagation fix (Phase 1/6) is that one decision updates every dependent Recipe and location at once — which also means a *bad* supplier switch hurts everywhere at once, not just in one recipe. This isn't a flaw to fix; it's a real property worth stating plainly, since it changes how risky a supplier switch should feel to the player (a bigger, more consequential decision than in a game where mistakes stay contained).

**Promotion needs its cost made explicit, not just its benefit.** Promoting your best line cook to GM of a new location frees the player's attention and enables expansion — but it also means your flagship kitchen loses its best hands-on cook. Right now the Employees write-up describes the upside of promotion clearly and leaves this cost implicit. It should be a stated, felt tradeoff: your strongest people are also your best GM candidates, and taking them out of the kitchen to do it costs you locally.

**R&D's "time cost" framing needs updating — it was written before Phase 5 changed what it competes against.** The original contract said solo R&D competes with "time spent cooking or managing," written when Service was assumed to demand active attention. Since Phase 5 made Service watchable/skippable and hands-on cooking optional, that framing is stale. The fix: R&D should cost calendar time and capacity — a project takes real in-game days/weeks during which that capacity isn't free for something else — and hiring a dedicated R&D chef adds parallel capacity, the same delegation logic already governing General Managers. Needs a small rewrite in Phase 4's R&D section.

**Rival friendliness needs an explicit downside, or "always be friendly" becomes the strictly dominant strategy.** As written, cultivating a friendly rival relationship has clear upside (referrals, shared promotions, reduced pressure) and no stated cost. A real tradeoff needs a real cost: time/resources spent cultivating the relationship that could have gone elsewhere, and forgoing the more aggressive plays available against a rival you're not on good terms with (undercutting them, contesting a lease, competing harder for a shared Opportunity Pitch). Needs a small addition to Competitors' contract.

**Porter/dishwasher staffing is a weak tradeoff as currently scoped, and needs a balancing flag for Phase 8.** If a porter is cheap relative to the inspection risk it mitigates, hiring one becomes a no-brainer rather than a real decision — insurance so cheap there's no reason not to buy it. This isn't a design flaw, it's a numbers question: Phase 8 needs to size porter cost against a tight early budget seriously enough that skipping one is a real, felt choice, not an obvious mistake.

### Exempt — not tradeoff mechanics by design, and that's fine

The two-track Power Rankings (Prestige vs. Empire) are parallel goals, not a tradeoff — the charter's mandate is about mechanics, not every feature needing to be a binary choice. Dashboard/Time speed controls (1x/2x/3x) carry only a soft, optional tradeoff (watching closely catches a developing problem sooner; fast-forwarding is convenient but risks missing the window to intervene) — real, but deliberately low-stakes by design, not a mechanic to force a hard choice out of.

---

### Cross-Cutting Findings (Phase 7 Synthesis)

The design holds up well under audit — the large majority of mechanics built across Phases 4–6 have genuine, meaningful tradeoffs, not decoration. That's a good sign this late in planning, since it means the volume of systems added this session (hiring/scouting, Regulars, Power Rankings, Opportunity Pitch) was additive rather than just heavier.

Five concrete fixes came out of this pass, and none of them are large: state the supplier-propagation risk honestly, make promotion's cost explicit, update R&D's time-cost framing to match Phase 5's revised Service Loop, give rival friendliness a real downside, and flag porter staffing for careful balancing rather than assuming the numbers will work out. All five are small edits or Phase 8 balancing notes, not redesigns — which is itself a useful signal that the underlying architecture from Phases 4–6 was sound.

---

## Phase 8 — Milestone Roadmap

Method: sequence everything designed in Phases 4–7 into milestones, each one shippable and testable on its own, with no system arriving before what it depends on is proven. Two things from the Phase 7 audit specifically drove sequencing decisions here: Regulars/Legacy is explicitly content-heavy (not just systems work), so it's pushed later rather than competing for early build time; and the supplier-propagation contract is the single most load-bearing piece of the whole design, so it gets validated before anything is built on top of it, not alongside.

### M0 — Headless Simulation (no graphics, logs only)

**Goal:** prove the core math and the one architectural claim this entire design leans on — supplier-policy propagation — before spending a single hour on rendering.

**Scope:** Time (ticks and speed multipliers as data only), Economy (cash, prime cost calculation), Ingredients (stock, par levels, spoilage), Suppliers (the full propagation contract from Phase 6 — one assignment, many live readers, no cached snapshots), Recipes (live contribution-margin calculation, Kasavana-Smith classification), a basic Kitchen throughput model (station capacity math, no visuals), a basic Customer arrival/satisfaction formula. **Plus three architecture decisions built in now rather than retrofitted later:** every Restaurant instance is created under a Company/Empire parent entity from day one, even with a single restaurant; Recipes, Furniture/object types, Employee traits, and Event definitions are all built as external, data-driven files (JSON or an equivalent def format) rather than hardcoded in engine logic; and the **save/load system** ships in M0 to the spec in Phase 9 — version-stamped saves, definitions referenced by stable string ID, graceful degradation when a definition is missing, inspectable format. All validated via logs and automated tests, not play.

**Engine note:** M0 is written as **plain C# with no engine dependency** (per Phase 9's Unity decision) — it can begin before any engine is installed, and drops into the Unity project at M1 without a rewrite.

**Exit test:** switch a Supplier assignment in a test scenario and confirm every dependent Recipe's margin updates with zero manual edits — the concrete, testable version of the fix Restaurant Empire II never got. **Second exit test, added per Phase 9's follow-through:** add a new Recipe or Furniture object purely by writing a new data file, with zero engine-code changes required — confirming the data-driven architecture is real and not just a stated intention.

### M1 — Single Restaurant, Placeholder Graphics, the Core Loop

**Goal:** the smallest playable loop from Phase 5 — Prep → Run → Review — actually exists and is fun on its own, with the simplest version of everything it needs and nothing else.

**Scope:** Furniture/Layout (placeholder art, basic grid placement, capacity/throughput outputs only — no smart-object interaction system yet); basic Employees (hire, assign to a station, a single skill number — no hiring profiles or promotion ladder yet); Kitchen brigade stations with real ticket flow; Customers with patience/satisfaction and simple, watchable movement at 1x/2x/3x speed (placeholder Sims-style rendering per Phase 5); Reputation as a single score gating volume; a minimal Dashboard (cash, prime cost, reputation only). **Added after Phase 10:** the **Time Control & Interrupts** model from Phase 5 — sim-a-day/week/month with pause-on-decision, plus the weekly business review beat — belongs in M1, not later. It is now the game's primary time interface rather than a convenience feature, and building M1 around per-night play would validate the wrong loop entirely.

**Exit test:** revised after Phase 10. The original bar — "is a full night of service fun, described in plain language" — is necessary but no longer sufficient, since Phase 10 found that the design's real risk (no required player attention) is invisible at single-night scale. Add a second bar: **sim a full in-game month and count how many times the game stopped for a decision that felt worth stopping for.** If that number is near zero, or the interrupts feel repetitive or trivial, the loop has failed regardless of how good one night looks. Do not proceed to M2 until both bars pass.

### M2 — Depth: Menu Engineering, Real Staffing, the Advisor

**Goal:** make Prep and Review substantive enough to carry the loop long-term (Phase 6's central resolved question), now that the loop itself is proven fun in M1.

**Scope:** full Recipes (Featured slots, live Kasavana-Smith matrix, menu cohesion signal); R&D (calendar-time-gated per the Phase 7 fix, tasting menus); full Employees (hiring profiles — Smart/Loyal/Hardworking/Experience — scouting uncertainty, and the promotion ladder up through Head Chef/Floor Manager, *not* General Manager yet — there's nowhere to send one until M4); rare turnover/retirement (state-driven, not constant churn); Porter/Dishwasher role (sized carefully per the Phase 7 balancing flag, so skipping one is a real choice); Ambiance presets and Customer archetypes; the Advisor, now that there's enough state complexity for its suggestions to be worth anything.

**Exit test:** a player who ignores the Advisor entirely should still be able to diagnose problems from the Dashboard alone (Phase 6's Review-legibility contract), and one who follows it should clearly do better — proving the suggestions are genuinely useful, not decorative.

### M3 — Business Systems: Full Economy, Events, Competitors, Marketing

**Goal:** bring the "living world" pressure systems online against a single restaurant, before expansion adds more surface area to test them against.

**Scope:** full Economy (loans, anchored interest/royalty rates); the Events director with Health/Fire Inspection, VIP/Critic Visit, and basic Opportunity Pitch (bid/pass only — no bidding wars yet, that needs Competitors' relationship depth); full Competitors/Rivals (relationship score, friendly/neutral/aggressive posture, the friendliness-cost tradeoff from Phase 7); Marketing (campaigns, review-response); the **Prestige** track of Power Rankings specifically — it only needs Reputation and Competitors, both landing here, so a single restaurant can start climbing City and State rankings before any expansion exists. Country and World tiers stay gated behind the eligibility checklist refined this session (sustained excellence over a long stretch, Marquee wins, or expansion) — reachable single-location, but correctly rare and hard-won rather than trivially fast, so M4/M5 keep their pull.

**Exit test:** a player who never expands past one restaurant should have a full, satisfying game already — competitions, rivalries, awards, a real shot at topping the local and regional Prestige rankings — before multi-location is even unlocked. Reaching the very top (Country/World) without ever expanding should still be *possible* but should feel like a genuine, rare achievement, not the expected default outcome.

### M4 — Expansion: Multi-Location, GM Delegation, the Empire Rank

**Goal:** the actual "empire" fantasy becomes real, capital-gated per Phase 3, not milestone-gated.

**Scope:** food truck/pop-up pre-expansion testing (arrives first, since its whole purpose is de-risking the decision that follows); the capital-gated second location; General Manager promotion (now meaningful, since there's finally somewhere to send someone); the **Empire** track of Power Rankings (needs real scale/revenue-across-locations data, which only exists once multi-location does); Marquee Opportunities and bidding wars (the most dependency-heavy feature in the whole design — needs Competitors' relationship system, the Power Rankings, and Economy's revenue-share obligation type all already working together).

**Exit test:** delegating a location to a GM and walking away for an in-game month should produce a legible, gradual drift (good or bad) tied to that GM's actual competence — not a black box — per the Big Ambitions failure this whole role was built to avoid.

### M5 — Legacy & Long Tail: Regulars, Franchising, Expansion Beyond

**Goal:** the content-heavy, long-tail layer — deliberately last, since it depends on ongoing writing investment (per Phase 7's honest flag) rather than systems work, and shouldn't compete with core-loop development time.

**Scope:** Regulars and the Restaurant Legacy Log; franchisor/franchisee mechanics (Phase 2's ownership research); regional/international expansion pacing (Time's multi-timezone hook); celebrity-chef content and deeper Marquee storylines.

**Exit test:** none required to consider the "core" game complete — M5 is enrichment on an already-whole game, not a dependency for it.

#### M5 addendum — Scaling costs something: prestige erosion, bulk sourcing, and the franchise offer (Aaron)

**DO NOT BUILD THIS YET.** Recorded here so it is captured rather than lost. Every part of it depends on Reputation→volume and the Prestige rank being real and meaningful, which is M3 at the earliest, and the project's largest named risk (Phase 10) is building late-milestone content before the early bars pass. This section exists to be read at M4/M5, not now.

The premise: **growth should threaten the thing that made you worth growing.** Today the two Power Rankings tracks (Prestige and Empire) are parallel ladders climbed independently — Phase 7's audit explicitly exempted them from the tradeoff requirement as "parallel goals, not a tradeoff," which was accurate and is a soft spot. This addendum converts them into a genuine tension, which is why it earns a place despite being late-milestone content.

**1. Prestige erosion under scale.** A restaurant known for something specific loses that distinctiveness as it replicates — the real-world pattern behind acclaimed restaurants that expanded and became ordinary. Mechanically this should be a *derived value, not a new tracked score* (the discipline applied to furniture and menu cohesion elsewhere in this document): as location count rises, Prestige faces a drag that can be offset by spending — the founder's personal presence, higher-tier GMs, keeping sourcing quality up, per-location menu variation rather than a single replicated template. Scale cheaply, and you converge on cookie-cutter and your Prestige rank slides even as your Empire rank climbs. Scale expensively and deliberately, and you hold both. **This is the mechanism that makes "biggest empire" and "world's best" a real choice rather than two scoreboards.**

**2. Bulk sourcing — the cheapest piece to build, and it needs no new system.** A boutique supplier cannot feed forty locations. Growth should force a switch to bulk distribution: lower unit cost, lower quality tier. That flows through the *existing* chain — `SupplierPolicy` → `MenuCosting.IngredientQuality` → satisfaction — with no new machinery at all. It also reuses the propagation contract exactly as intended: one company-scope assignment change, felt everywhere, with per-location overrides available at a price for the flagship you want to protect. Expansion therefore threatens food quality through a mechanism the player already understands from hour one, which is far better than introducing a new penalty they have to learn.

**3. The franchise offer is NOT a new mechanic.** An investor approaching the player to franchise the concept is a **Marquee Opportunity** (Events) — the bidding-war and deal-structure system already designed, with Economy's revenue-share obligation as the tradeoff: a lump sum and rapid reach now, against a permanent cut and reduced control over quality (which feeds straight back into prestige erosion above). Building it as its own system would duplicate three things that already exist.

**Ratio worth noting, and the reason this is a healthy addition rather than scope creep:** of the three ideas, two are reskins of existing systems and only prestige erosion is genuinely new — and that one is a single derived value, not a subsystem.

**One architectural confirmation this produces early, which is the real reason to write it down now:** Phase 9 claimed franchising would generalize the Supplier propagation pattern — a Brand/Concept as a first-class object referenced by many Restaurant instances, with bounded per-instance overrides. This addendum stress-tests that claim and it holds: prestige erosion reads location count and per-location divergence from the Brand; bulk sourcing is a company-scope supplier assignment with local overrides; the franchise deal is a Brand licensed to an owner who is not the player. Nothing here needs a pattern that does not already exist. That is worth knowing before M4 builds on it.

---

### Cross-Cutting Findings (Phase 8 Synthesis)

The sequencing surfaced one genuinely pleasing structural result on its own: because the **Prestige** rank only needs Reputation and Competitors, it lands in M3, before multi-location exists at all — which means "master one restaurant and climb toward being the best" is a complete, satisfying game on its own, fully playable before "empire" (M4) ever unlocks, even though (per the eligibility-checklist refinement) actually reaching the very top without expanding stays a rare, hard-won outlier run rather than the easy default. That's not a coincidence; it's the direct payoff of Phase 3's expand-in-place-first principle and the two-track rankings decision actually holding up under real sequencing pressure, not just sounding good in the abstract.

The two items flagged as risks in Phase 7 (porter/dishwasher balancing, honest framing of supplier-propagation risk) both land in milestones early enough to be caught in playtesting (M2 and M0 respectively) rather than discovered late. Regulars — the one system explicitly flagged as content-cost-heavy rather than systems-heavy — is correctly isolated in M5, where it can't compete with core-loop build time or delay the milestones that actually prove the game is fun.

---

## Phase 9 — Architecture Review

Method: run the design against every expansion capability the charter names, verdict each one (already supported vs. needs a concrete architectural decision now), then address the kickoff constraint (shippable as a download or hosted online) directly.

### Expansion capability audit

**Franchises** — already designed conceptually (Phase 2/8). Architecturally this generalizes the exact Supplier-propagation pattern already committed to in Phase 6: a **Concept/Brand** needs to be its own first-class object, separate from any single Restaurant instance, so a franchise is a Brand referenced by many Restaurant instances (with defined, bounded per-instance overrides for local menu/pricing). No new pattern needed — just apply the one already built for Suppliers one level up.

**Delivery** — already flagged (virtual, seatless Restaurant extension; a distinct Customer patience/expectation model). Concrete recommendation: design Kitchen's ticket/order object to be **channel-agnostic from M1** (a ticket doesn't need to know if it came from a dine-in table or a delivery order), even though delivery itself is an M5+ feature — this is cheap to build in from day one and expensive to retrofit later.

**Food trucks** — already flagged as a lightweight Restaurant variant. Recommendation: make "location type" (brick-and-mortar / food truck / ghost kitchen / delivery-only) a **parameter on the same Restaurant object**, not a separate class hierarchy, so Kitchen, Employees, and Recipes all work uniformly across location types with different capacity constraints.

**Celebrity chefs** — no architecture gap. A celebrity chef is an Employee with an exceptional trait profile, acquired through a Marquee event (Events), with outsized Marketing/Reputation effects — fully covered by systems that already exist.

**Michelin stars** — no architecture gap. This is another tier/threshold on the existing Power Rankings/Reputation system, layered with the recurring awards-ceremony content already flagged under Events.

**Corporate ownership** — real architectural gap, worth fixing now rather than later. Economy needs a rollup layer above individual Restaurant P&Ls. Recommendation: introduce a **Company/Empire entity as a parent container over Restaurant instances starting in M1** — even when there's exactly one restaurant, it belongs to a Company of one. Retrofitting this hierarchy after multiple locations already exist in production data is a much more painful migration than starting with it.

**International expansion** — mostly flagged already (multi-timezone pacing, regional trend variation). Recommendation: keep Economy's currency and regional-market parameters **configurable per region from the start**, not hardcoded to a single locale — cheap now, expensive to retrofit.

**Acquisitions** — flagged conceptually (buying out a failing rival). Real architectural requirement: Competitors currently run as lightweight proxies (Phase 4: "a rough proxy of their own restaurant's state, not full player-level detail"), so an acquisition needs a defined **upgrade path from lightweight NPC proxy to full player-controlled Restaurant object** — both should share a common base schema even though NPC instances run a simplified simulation, so the upgrade is a data promotion, not a rebuild.

**Multiplayer** — not addressed until now, and worth being honest rather than hopeful about scope. Given Service is designed to be watchable at a player-controlled individual speed (Phase 5), true synchronous co-op (two players inside the same live simulation moment) would require a substantial rework of the pacing model this whole design depends on. Recommendation: design for **asynchronous multiplayer first** — shared Power Rankings/leaderboards against friends, visiting or browsing a friend's restaurant and Legacy Log, maybe trading R&D recipes — which is low-risk and fits everything already built. Treat true synchronous co-op as a distant, maybe-never stretch goal, not a target this architecture needs to support from day one.

**Mods** — already flagged repeatedly (community recipe sharing). Recommendation: define Recipes, Furniture/object types, Employee trait definitions, and Event definitions as **external, data-driven files from M0/M1** (JSON or an equivalent def format — the same pattern RimWorld itself uses, one of the Phase 1 reference games, specifically because it's proven to support a thriving mod community) rather than hardcoding them into engine logic. This is a day-one architecture decision — retrofitting a data-driven layer after the engine is built around hardcoded content is a large, painful rework, not a small one.

### Technical architecture recommendation (addressing the kickoff constraint: downloadable or hostable online)

Two of Phase 1's GitHub references remain the strongest architectural precedents: FreeSO's **SimAntics smart-object pattern** (objects advertise interactions and a priority score) for Kitchen/Furniture, and its client/server split as a real precedent for eventual hosting. But FreeSO itself took years of dedicated open-source effort to reach its current state, which is a realistic cost to weigh against building fully custom.

**DECIDED: Unity.** Build on an established engine rather than a from-scratch engine in the style of FreeSO. Unity is the choice, for reasons that weigh especially heavily on a first game project:

- **Ecosystem depth is the deciding factor.** Unity has by far the largest volume of tutorials, forum answers, and worked examples of any engine. That matters concretely when an implementer (human or AI) hits an unfamiliar problem — the answer usually already exists and is findable.
- **It's C#**, which keeps the M0 headless core (below) directly reusable rather than needing a rewrite.
- **Built-in NavMesh pathfinding**, which Phase 1's Idle-Restaurant reference already proved is sufficient for restaurant customer movement — no custom pathfinding needed.
- **Asset Store** provides placeholder and production art, meaning M1's "placeholder graphics" milestone doesn't require producing art from scratch.
- **Straightforward desktop packaging** for the downloadable-build constraint set at kickoff, plus well-supported paths to backend services for the asynchronous leaderboard/visiting features.
- One of the three GitHub references Aaron supplied (Idle-Restaurant) is itself a Unity restaurant tycoon — an inspectable, directly comparable implementation.

*Godot* remains a legitimate alternative — genuinely free and open-source with no revenue licensing at all, and lighter to install — and it would be the right call if licensing terms become a concern. Its ecosystem is smaller, which is precisely the tradeoff that argues against it for a first project.

**Important, and it reduces the stakes of this decision considerably: M0 is engine-agnostic.** The headless simulation core (Economy, Suppliers, Recipes, Ingredients, Kitchen math, the Company/Restaurant hierarchy, save/load) should be written as **plain C# with no engine dependency**, tested with standard unit tests, and only later referenced by the Unity project. This means M0 can begin immediately without installing or learning an engine at all, and that the work is not lost even if the engine decision is revisited before M1.

Either way, the data-driven-content requirement (for mods), the Company/Restaurant hierarchy (for corporate ownership), and the save format (below) should all be settled at the start of M0, since all three are far cheaper to build in from the beginning than to retrofit once real game data exists.

### Save / load architecture (closing Phase 10 finding 2.6, per Aaron)

**Player-facing behavior (Aaron's direction):** the player can save manually at any time; closing the game with unsaved progress prompts to save first; and the game autosaves automatically after any long jump-ahead simulation (a week or month), since that's precisely when a large amount of state has changed unattended. A small rolling set of autosaves should be retained rather than a single overwriting slot, so a bad stretch is recoverable.

**The architectural requirement underneath it, which is the part that actually needs deciding at M0.** Two decisions already made collide here: content is data-driven (for mods, Phase 9) and runs last up to 40 in-game years (Time). That means save files will reference content definitions — recipes, furniture, traits, events — that may be edited, versioned, or removed entirely between sessions, especially with mods installed. RimWorld, the direct precedent cited for the data-driven approach, has well-known save-breakage problems from exactly this.

Requirements, to be built in M0 rather than discovered at M4:

- Every save carries a **version stamp** (game version, and the set of content packs/mods active when written).
- Saved objects reference content definitions **by stable string ID**, never by array index or load order — indices shift when content is added or removed; IDs don't.
- Loading a save whose referenced definition is **missing or changed must degrade gracefully** — drop the affected object, log it, and warn the player plainly ("3 recipes from a mod you no longer have were removed") — never crash, and never fail the whole load because one definition went away.
- The save format should be **inspectable** (JSON or equivalent) rather than an opaque binary blob, which makes debugging, testing, and community tooling dramatically easier and costs essentially nothing at this scale.

---

### Cross-Cutting Findings (Phase 9 Synthesis)

Most of the charter's expansion list needed no new architecture at all — franchises, celebrity chefs, Michelin stars, food trucks, and acquisitions all fall out of patterns already committed to in Phases 4–8 (the Supplier-propagation model, the Events/Marquee system, Power Rankings, and the Competitors proxy-to-full-object idea). That's a good sign the architecture has been sound all along, not something bolted on now.

Two real, concrete changes come out of this phase and belong in M0, not later: introduce the **Company/Empire entity** as a parent container over Restaurant instances from the very first milestone (even with just one restaurant), and commit to a **data-driven content format** (Recipes, Furniture, Employee traits, Events) from the start rather than hardcoding and refactoring later. Both are cheap now and expensive after the fact — the entire reason to do an architecture review before implementation begins. **Both are now actually closed, not just recommended:** Restaurant's contract (Phase 4) and M0's scope and exit tests (Phase 8) were updated directly to reflect them, rather than leaving the fix isolated in this phase where it would have been easy to lose track of by the time M0 actually starts.

Multiplayer is the one area getting a narrower answer than the charter's full ambition: asynchronous (leaderboards, visiting, trading) is well-supported by everything already designed; true synchronous co-op would require reworking the pacing model this whole design is built on, and should be treated as out of scope unless explicitly revisited.

---

## Deliverables Checklist: Risk Assessment, Prototyping Needs & Open Questions

The charter names these as their own deliverables. Most of the substance already exists scattered through Phases 4–9; this section pulls it together rather than leaving it implicit. Reserved for Aaron/Sonnet, ahead of the Opus-run Phase 10 below.

### Consolidated risk assessment

Refreshed from the running Risks & Watch-Items list (Phase 4) plus everything surfaced since: **signal overload** — resolved (Advisor/Dashboard). **Employees becoming an HR-sim** — resolved (rare turnover, GM delegation). **Flat scaling in a single restaurant** — partially addressed (food truck as a genuinely different pre-expansion decision), still needs real tier-by-tier content design. **"The game suggests, you decide" applied inconsistently** — addressed as new mechanics landed, worth re-checking once real content is written. **Regulars/Legacy going stale** — open by nature, flagged as a content-investment cost rather than a systems risk, sized honestly in Phase 8 (M5). **Porter/dishwasher being too cheap to be a real decision** — open, a Phase 8 balancing flag, needs actual numbers and playtesting to resolve, not a design fix. **Multiplayer ambition vs. actual architecture** — resolved by narrowing scope (asynchronous only) rather than by solving the harder problem.

### Areas requiring prototyping (not yet validated by anything beyond design reasoning)

The Service Loop's actual watchability — Phase 5's fun-test was a text-only walkthrough; whether watching customers and tickets resolve at 2x/3x speed is genuinely engaging (versus needing more visual detail/spectacle than currently specified) can only be answered by building it. The Advisor's suggestion quality — whether its recommendations actually feel useful and trustworthy rather than naggy or wrong needs real suggestion-generation logic against real playtest data, not just the trigger table from Phase 6. The hiring/scouting uncertainty model — how much hidden information about a candidate feels like a fun gamble versus frustrating guesswork is a tuning question, not a design one. Regulars/Legacy vignette variety — the content-cost risk flagged in Phase 7 can only really be assessed once a real batch of vignette types is written and played against. The SimAntics-style smart-object interaction pattern for Kitchen/Furniture — recommended in Phase 9 on the strength of FreeSO's precedent, but never actually built in this project; worth an early technical spike in M0/M1 to confirm it's tractable in whatever engine gets chosen. The exact Power Rankings eligibility thresholds (how long is "sustained," how many Marquee wins is "a couple") — real numbers, need balancing data, not just the checklist structure.

### Questions still open, requiring Aaron's decision (not Claude's)

**Resolved since this section was written:** engine choice is now **decided — Unity** (Phase 9, with the note that M0 is engine-agnostic plain C# and can start before any engine is installed). Save/load behavior and architecture are specified (Phase 9). Chef skill's non-delegable function (signature dishes), the Events randomness loosening, and the Advisor/Dashboard merge are all decided and written in.

**Still genuinely open, none blocking M0:**

- *Multiplayer backend/hosting* for the asynchronous features (leaderboards, visiting friends' restaurants) — scope is narrowed to asynchronous-only, but no concrete service or hosting decision made. Not needed until well after M4.
- *Advisor personality* — whether it speaks as a specific named character (a sous chef, a GM) or stays a neutral UI voice. Aaron's framing of Advisor prompts as "we have too much fish — want to feature it?" leans strongly toward a character voice, since "we" already implies a person; worth confirming deliberately at M2 when the Advisor is actually built.
- *Marketing's scope* (Phase 10 finding 2.3) — whether it's a genuine system or two features belonging to Economy and Reputation. Deferred by agreement; Marketing isn't built until M3, so this can be answered then.
- *Franchisor mechanics* (M5) — flagged conceptually from Phase 2's research but never designed to the depth of everything else here. Needs its own real design pass before M5 starts, not a retrofit of a one-line mention.
- *Post-buyout continuation* and *retirement capstone* — what happens after each of the two endings (Economy/Time). Worth settling before M3.
- *Balancing numbers throughout* — porter cost, Power Rankings eligibility thresholds, interrupt frequency. These need playtest data, not more design.

---

## Phase 10 — Critical Review Pass

Method: adversarial read of the full document, hunting for contradictions between phases, principles that were stated but not delivered, systems that exist by inertia, and — most importantly — problems that emerge only from the *accumulation* of individually-correct decisions. Findings are ranked by severity, not by phase order. The charter asked for healthy disagreement; this section is written to that standard rather than as a victory lap.

### Severity 1 — RESOLVED (all three answered by Aaron immediately after this pass; fixes written into Phases 4, 5, 6 and 8)

**Resolution summary.** All three Severity 1 findings now have explicit answers threaded back into the phases they affect, rather than sitting as open flags here. Two of them turned out to share a single mechanism: Aaron's answer to 1.2 (sim a day/week/month, interrupted by decisions) also answers 1.1 (where the player is genuinely required), and is now written up as **Time Control & Interrupts** in Phase 5, with M1's scope and exit test in Phase 8 updated to match. 1.3 is answered by **rival buyout** as the failure state, written into Economy. The original findings are preserved below unedited, since the reasoning behind them still governs how the fixes should be judged.

### Severity 1 — the original findings (preserved; see resolution above)

**1.1 The agency subtraction problem: no phase of this game is designated as the one that requires the player.**

Trace every decision made across this project. Service: watchable, skippable, no required input (Phase 5). Prep: "never a mandatory screen," standing policies mean zero daily input, and "a day with nothing flagged is correctly a day with nothing to decide" (Phase 6). Review: the Dashboard tags and diagnoses what went wrong. The Advisor: tells you what to do about it. Turnover: rare. GM delegation: locations run themselves. Suppliers and schedules: standing policy. Cleanliness: a staffing outcome, not an action. Ambiance: a preset. Menu cohesion and furniture: bounded nudges, Advisor-flagged.

Every one of those was a correct fix to a real tedium complaint. But summed, this design has systematically removed *required* player input from every phase of its own loop, and at no point did any phase designate where the player is genuinely, non-optionally needed. The document never answers "what must the player do?" — only, repeatedly, what they're spared from. In principle, the game as currently specified can be described as one that plays itself and occasionally asks you to click accept.

Critically, **M1's exit test will not catch this.** "Is one night of service fun in plain language" is a single-night question; this failure only emerges over dozens of hours, as the aggregate feel of a game with no obligatory beat.

There's also a structural asymmetry worth naming: the Advisor's entire trigger table (Phase 6, section 5) is warnings and remediation — puzzle dishes, thin staffing, morale dips, drift alerts, financial risk. It is a problem-flagger. A game whose only prompts are "something is wrong" is a maintenance game, not an ambition game, and that is squarely at odds with a fantasy about building an empire.

*Recommended fix:* explicitly designate the weekly Business Loop beat as the required-attention phase, and rebalance the Advisor to surface **opportunities as well as problems** ("you have idle cash and no R&D running — this trend window closes in three weeks"). Neither adds chores; both give the player a reason to show up that isn't damage control.

**1.2 Timescale is undefined, and the two halves of the design imply incompatible answers.**

The design promises employees who retire after long tenure, regulars who return "years later" with their kids, GM drift measured in months, trends that shift slowly, and Power Rankings eligibility requiring reputation "sustained over a long stretch." That implies a run spanning years, probably decades, of in-game time.

Simultaneously, Service is specified as a per-night, visually watchable, 1x/2x/3x real-time simulation of individual customers moving through a dining room — the single most expensive thing in the Phase 9 engine recommendation.

Ten in-game years is roughly 3,650 services. Nobody watches 3,650 nights. So either the player skips the overwhelming majority of services — in which case the watchable layer is mostly unused and M1's exit test is validating something rarely done — or the timescale is far shorter, and the retirement/legacy/multi-year content doesn't fit. In 961 lines, the intended length of a full playthrough and the ratio of real time to game time are never stated once.

*Recommended fix:* decide this explicitly before M1. A coherent resolution exists: build expecting the player to watch closely early (while learning the systems), then increasingly skip to summary, with full watching reserved for high-stakes nights — a critic visit, a menu launch, opening night at a new location. That makes the visual layer's job "be available, and make special nights feel special" rather than "be the default consumption mode." That's a good answer, but it changes M1's presentation budget and exit test materially, so it must be a decision, not an assumption.

**1.3 There is no defined failure state.**

The design commits to failure being legible, caused by cash exhaustion or cost creep, "never an opaque bankruptcy trigger." It never says what actually *happens*. Game over? Restart? Debt spiral you play through? Nothing in the document answers it.

This matters more than it appears. The entire difficulty framing — "hard and strategic, losable but always explicable" — depends on real stakes, and stakes require a defined consequence. Worse, this design has spent enormous effort making loss emotionally heavy: named regulars, promoted employees, a Legacy Log. Losing will land hard. Whether that's the game's most memorable moment or a rage-quit depends entirely on handling that doesn't exist yet.

*Direction worth considering (not prescriptive):* **rival buyout** as the failure state — thematically perfect, uses the Competitors system already built, non-total (the restaurant persists, you just don't own it), and generates exactly the kind of story the Legacy Log exists to hold: "I lost my first place to the cook I trained."

### Severity 2 — STATUS: five of six closed; findings preserved below

Closed after this pass: **2.1** (Advisor authority — superseded by the stronger three-tier model in Phase 4, per Aaron); **2.2** (Advisor/Dashboard merged into one component); **2.4** (Events randomness rule loosened to permit unforeseen-but-fair external events); **2.5** (chef skill given a permanent non-delegable function — signature dishes require the player personally); **2.6** (save/load architecture specified in Phase 9 and added to M0's scope). **Still open by agreement: 2.3** (Marketing's scope) — deferred to M3, when Marketing is actually built.

### Severity 2 — the original findings (preserved; see status above)

**2.1 The Advisor may solve the game's central strategic mechanic on the player's behalf.** Phase 2 named the Kasavana-Smith matrix one of three central mechanics. Phase 6's trigger table then says a Puzzle dish triggers the suggestion "feature this dish." That *is* menu engineering — performed by the game, handed to the player as a conclusion. Principle 5 ("the game suggests") and "menu engineering is a core player-facing strategic system" cannot both be maximally true.

*Fix, clean and implementable:* split Advisor output by kind. **Prescriptions** for maintenance and hygiene (porter staffing, morale, sanitation) — tell the player what to do, this is chore-avoidance. **Observations** for the strategic layer (menu, pricing, expansion) — "your truffle risotto has the highest margin on the menu and sells the least" leaves featuring, repricing, re-costing, or cutting as the player's call. Same anti-tedium benefit, without dissolving the strategy.

**2.2 Advisor and Dashboard should merge.** I flagged this mid-project and it never got done. Their contracts already overlap circularly (Dashboard "knows the Advisor's top suggestions"; Advisor "outputs a digest — see Dashboard"). Neither holds state, neither is a simulation, both are read-surfaces over other systems. The charter explicitly asks "can two systems become one?" — this is the clearest yes in the document. Merging also stops them drifting apart in implementation and corrects Phase 8's sizing, which currently lists them as peers of Kitchen and Economy.

**2.3 Marketing is the weakest system here and survives partly on inertia.** Its stated ceiling is "cannot substitute for quality"; its outputs (volume, reputation recovery) are things Reputation already governs; its most interesting mechanic (review-response) is really a Reputation mechanic. Phase 7's audit — which found genuine tradeoffs almost everywhere — barely engages with it. In a design that aggressively cut redundant scores, this one deserves the same scrutiny: is Marketing a system, or two features (campaigns as a cash-for-volume lever; review-response) belonging to Economy and Reputation respectively? Campaigns-as-a-lever is real, so this is a question rather than a confident cut — but it should be answered deliberately.

**2.4 The "no randomness" rule may have killed the story generator it was modeled on.** Events cannot fire anything "state-disconnected." Inspections only when dirty, VIPs when reputation is high, rivals escalate only when you're large. But RimWorld — the explicit model — works *because* Randy Random exists; the doc even lists a storyteller-tone dial as an expansion hook while forbidding the thing that dial would control. A world that only ever responds to your own stats is a consequence engine, not a story generator: it produces "I had it coming," never "you won't believe what happened."

The instinct behind the rule is right; the wording over-forbids. The real distinction is **unfair vs. unforeseen**. A supply shock, neighborhood construction, a food trend collapsing, a rival's protégé opening across the street — unforeseen, but not unfair: external, not punishments for playing well, and generators of genuinely novel situations. Loosen the rule to permit unforeseen-but-fair external events, deliberately.

**2.5 Chef skill doesn't deliver what principle 1 claims.** Principle 1 says chef skill is "always-present" and "never degrades into a vestigial minigame." Trace what it actually does by M4: sets a baseline modifier at a station you're nominally assigned to, gates R&D tiers, and boosts the rare nights you opt in — while R&D chefs and GMs absorb the rest. That's a stat, not a system, and principle 1's language is now stronger than what the design delivers.

*Fix, small and effective:* make **signature dish creation require the player-chef personally and permanently.** Hire R&D chefs for incremental recipes; the dishes that define the brand can only come from you. One irreplaceable, non-delegable function makes principle 1 honest again.

**2.6 Save/load and mod-version compatibility were never addressed.** The charter named save systems explicitly as a research topic; nothing in nine phases covers it. It isn't trivial here, precisely because of two Phase 9 decisions colliding: data-driven content (for mods) plus long multi-year runs means save files referencing content definitions that can change or vanish between versions or when a mod is removed. RimWorld — the direct precedent cited for the data-driven approach — has well-known save-breakage problems from exactly this. This belongs in M0 (define the save format and its handling of missing or changed definitions), not discovered at M4 by someone with a 40-hour save.

### Scope realism — the thing the document never says

Sixteen systems, six milestones, a 3D Sims-style build mode with character locomotion, a watchable service simulation, data-driven modding, asynchronous multiplayer, and a content-heavy legacy layer. Team size, budget, and timeline appear nowhere in 961 lines. For calibration, the reference games here were built by funded studios — Two Point Hospital by ex-Bullfrog/Lionhead veterans, Project Highrise by an experienced small team, and it still shipped to "boring" reviews.

Stated plainly: **M0–M2 is the project. M3–M5 is a roadmap, not a plan.** M0 alone is real work; M0 through M2 is the actual viability test and likely many months of full-time effort for an experienced developer. The largest risk to this game ever existing is not any design flaw in this document — it's building M3–M5 content before M1's exit test has been honestly passed and failed forward from.

### What genuinely holds up

Not everything needs challenging, and manufacturing criticism would be its own failure. The supplier-propagation contract (Phase 6.3) is the strongest piece of engineering thinking in the document and correctly identified as load-bearing. Phase 8's sequencing genuinely earns its structure — proving propagation headlessly before rendering, and Prestige landing in M3 so a single-restaurant game is complete before expansion exists, are both real results rather than tidy narrative. Phase 7's audit was not a rubber stamp; tradeoff density across the design is real. The competitor-relationship system and the protégé hook are legitimately novel against all ten reference games studied. And the repeated discipline of *cutting* scores — the furniture score, the standalone cohesion score, the proposed Reach stat — is rarer and more valuable than the additions.

### The single most important recommendation — now satisfied

The original recommendation was: do not start M0 until 1.1, 1.2, and 1.3 have explicit answers written into this document, because all three are foundational (what the player is required to do, how long a game is, what losing means) and none would have been surfaced by the existing milestone exit tests.

**That condition is now met.** All three were answered directly and the fixes are threaded into Phases 4, 5, 6 and 8 rather than left as recommendations here. The revised M1 exit test — sim a full in-game month and count the interrupts that felt worth stopping for — exists specifically so the failure mode behind finding 1.1 becomes catchable in playtesting instead of emerging thirty hours in.

**What remains before M0 is now smaller and more tractable:** the Severity 2 items (Advisor/Dashboard merge, Marketing's scope, loosening the Events randomness rule, giving chef skill one non-delegable function, and defining the save format's handling of missing content definitions), plus the four open decisions in the Deliverables Checklist above — engine choice being the most consequential. None of these are structural in the way 1.1–1.3 were; they are decisions to make deliberately rather than problems to solve.

### Post-resolution note: what to watch during M1

The Time Control & Interrupts model resolves the structural problem, but it also relocates the game's entire risk surface onto one thing: **interrupt quality.** If most playtime is fast-forward punctuated by decisions, then the decisions are the game. Two specific things to watch for during M1/M2 playtesting, neither of which the old design could even have surfaced:

*Interrupt fatigue vs. interrupt drought.* Too many stops and the sim controls feel useless; too few and the game feels empty at speed. This is a tuning problem with no correct answer in advance — RimWorld's storyteller-pacing dial exists for exactly this reason, and the expansion hook already flagged under Events (a selectable pacing/tone setting) should probably be promoted from "nice later" to "needed for tuning."

*Repetition surfacing faster than expected.* Fast-forward compresses in-game time, which means the player sees the same interrupt types far sooner in real playtime than a per-night design would have. Variety in the interrupt table matters more, and earlier, than the original roadmap assumed — worth carrying into M2's scope rather than treating as M5 content polish.

---

### GitHub Architecture Notes (contributed by Aaron, flagged for full treatment in Phase 9)

Three repos were shared as reference. Per the charter, these are studied for architecture/patterns, not copied — no code from them will be reused.

**[FreeSims / SimsVille](https://github.com/francot514/FreeSims)** (C#, MonoGame, ~300 stars) — an open re-implementation of The Sims 1's engine. Requires the original game's asset files to run; it's an engine, not a content package. Notably modular: `sims.parser` (reads Maxis's original file formats), `sims.files` (asset/file abstraction), `sims.common` (shared types), `sims.debug` (tooling), and `SimsVille` (the actual client), with a separate `SimsNet` module. **Lesson:** cleanly separating "read/write game data formats" from "core simulation types" from "client/rendering" from "networking" — as four-plus independent modules rather than one monolith — is exactly the boundary discipline this project should copy structurally (not literally) for its own engine.

**[FreeSO](https://github.com/riperiperi/FreeSO)** (C#, MonoGame, ~960 stars, 2000+ commits, active contributor community with a coding-standards wiki) — a full client/server re-implementation of The Sims Online. The load-bearing architectural idea, inherited from the original Maxis engine, is **SimAntics**: a small interpreted VM that runs per-object behavior scripts. In the original design, every object (a stove, a jukebox, a chair) *advertises* the interactions it offers and a priority/score for how attractive that interaction is right now; characters pick the best available advertised interaction rather than the game hard-coding "customer walks to table X." **Lesson — directly applicable:** this "smart object" pattern (object advertises interactions + priority; agent picks highest-scoring available one) is a strong candidate architecture for how customers, staff, and kitchen equipment should interact in this game, and is a much more scalable, moddable pattern than scripting each interaction bespoke. It's also inherently extensible — new furniture/equipment types just need to implement the advertise-interaction contract, which matters for the mod-support goal in Phase 9. FreeSO's client/server split (a real network-authoritative simulation with a separate rendering client) is also a concrete precedent for the "hostable online" requirement noted at the top of this doc — worth revisiting directly in Phase 9.

**[Idle-Restaurant](https://github.com/eminkarakaya/Idle-Restaurant)** (C#, Unity, small solo/hobby project, ~21 stars) — a simple 3D idle restaurant tycoon (also shipped on Google Play). Uses Unity's built-in NavMesh for customer pathing and a finite state machine for customer/staff AI; monetizes via ads; includes localization. **Lesson:** this is a useful lower bound, not a pattern to emulate structurally — it shows the minimum viable stack (engine navmesh + FSM) that a solo dev can ship idle-tycoon content with, which is a fine reference for early prototyping (M1) but has none of the depth (menu/supply/reputation systems) this project needs. Its main value is confirming that off-the-shelf pathing/FSM tooling is sufficient for customer movement — we don't need to build custom pathfinding.

**Net effect on Phase 9:** the SimAntics smart-object pattern and FreeSO's client/server separation are now flagged as leading architecture candidates for the simulation core and multiplayer/hosting question respectively; both will be evaluated properly against the modding/franchise/multiplayer requirements when we get there.

**Additional note (Aaron):** beyond the object-interaction pattern above, The Sims lineage is also the right reference for two other systems this project needs, independent of SimAntics: the free-form 3D building/layout editor (walls, floors, object placement, rotation — exactly the "Sims-style placement" already noted under Restaurant Empire in Phase 1) and grid/navmesh-based character movement through a player-built space. Both FreeSims (SimsVille) and FreeSO ship working, inspectable implementations of a build-mode editor and Sim locomotion through arbitrary player-designed floor plans, which is more directly reusable as a *reference* than SimAntics is, since our restaurant's dining room/kitchen layout editor is functionally the same problem (place objects on a grid, validate placement, route characters around them). This gets folded into Phase 4 (Restaurant simulation, Customers/Employees simulation) and Phase 9 (do we build our own layout editor + locomotion, or lean on engine-native tools as Idle-Restaurant did).

---

### Sources (Phase 1)

- [Trevor Chan's Restaurant Empire PC Review – GameWatcher](https://www.gamewatcher.com/reviews/trevor-chans-restaurant-empire-review/10424)
- [Restaurant Empire Review – GameSpot](https://www.gamespot.com/reviews/restaurant-empire-review/1900-6025000/)
- [Restaurant Empire 2 Review – GameGrin](https://www.gamegrin.com/reviews/restaurant-empire-2-review/)
- [Restaurant Empire II critic reviews – Metacritic](https://www.metacritic.com/game/restaurant-empire-ii/critic-reviews/)
- [Restaurant Empire II – Wikipedia](https://en.wikipedia.org/wiki/Restaurant_Empire_II)
- [Restaurant Empire II – Steam Community discussions](https://steamcommunity.com/app/32900/discussions/0/1471969431591567057/)
- [Pizza Connection 3 Review – Cubed3](https://www.cubed3.com/games/reviews/pc/pizza-connection-3)
- [Economic strategy with a twist – Pizza Connection 2 retrospective – gamepressure.com](https://www.gamepressure.com/newsroom/economic-strategy-with-a-twist-when-top-tier-pizza-wasnt-enough-p/z27d2e)
- [Chef: A Restaurant Tycoon Game – Steam](https://store.steampowered.com/app/886900/Chef_A_Restaurant_Tycoon_Game/)
- [Chef: A Restaurant Tycoon Game Review – Gaming Respawn](https://gamingrespawn.com/reviews/38754/chef-game-review/)
- [Big Ambitions – Steam](https://store.steampowered.com/app/1331550/Big_Ambitions/)
- [Big Ambitions Review – Movies Games and Tech](https://moviesgamesandtech.com/2023/03/26/big-ambitions-review/)
- [Big Ambitions – Steam Community discussions (feedback threads)](https://steamcommunity.com/app/1331550/discussions/0/632295800477595935/)
- [Two Point Hospital Review – SEGAbits](https://segabits.com/blog/2025/05/03/two-point-hospital-review-laughter-and-good-gameplay-is-the-best-medicine-pc-steam/)
- [Two Point Hospital – SomeAwesome Game Review](https://www.someawesome.com/game-review/oldscotland/two-point-hospital)
- [Two Point Campus Review – PC Invasion](https://pcinvasion.com/two-point-campus-review)
- [Two Point Campus Review – DiamondLobby](https://diamondlobby.com/two-point-campus/two-point-campus-review/)
- [Bancho Sushi Management – Dave the Diver Wiki/Fandom](https://dave-the-diver.fandom.com/wiki/Bancho_Sushi)
- [DAVE THE DIVER Guides – Managing your Sushi Bar – Neoseeker](https://www.neoseeker.com/dave-the-diver/Managing_your_Sushi_Bar)
- [Dave the Diver review – Sportskeeda](https://sportskeeda.com/esports/dave-diver-review-an-often-outlandish-management-sim-never-topples)
- [Game Review – Cook, Serve, Delicious! – GameMakerBlog](https://gamemakerblog.com/2014/04/06/game-review-cook-serve-delicious/)
- [Cook, Serve, Delicious! Review – Choicest Games](https://www.choicestgames.com/2014/10/cook-serve-delicious-review.html)
- [Why The Cook, Serve, Delicious! Creator Hates Cooking – GameMaker](https://gamemaker.io/en/blog/cook-serve-delicious-interview)
- [Project Highrise Review (Noobreview) – Gameffine](https://www.gameffine.com/project-highrise-review-pc-noobreview-not-rising-enough/)
- [Project Highrise: Architect's Edition Review – mspoweruser](https://mspoweruser.com/review-project-highrise-architects-edition-is-a-well-made-but-repetitive-experience/)
- [Project Highrise: Architect's Edition Review – COGconnected](https://cogconnected.com/review/project-highrise-review/)
- ["The Story Generator: A Game Design Analysis of RimWorld" – Substack](https://substack.com/home/post/p-155708844)
- [AI Storytellers – RimWorld Wiki](https://rimworldwiki.com/wiki/AI_Storytellers)
- [The Factorio Mindset – Byrne Hobart, The Diff](https://www.thediff.co/archive/the-factorio-mindset/)
- [Factorio Taught Me Systems Thinking (Part I) – Medium](https://medium.com/gaming-is-good/factorio-taught-me-systems-thinking-part-i-f8a1d2a8a349)
- [Case Study: Why you should (maybe) play Factorio](https://www.deprocrastination.co/blog/case-study-why-you-should-play-factorio)

### Sources (Phase 2)

- [Restaurant Prime Cost in 2026 – NOVA Platform](https://www.novatab.com/blog/restaurant-prime-cost)
- [Restaurant Food Cost Percentage 2026 – VantaInsights](https://vantainsights.com/insights/restaurant-food-cost-percentage)
- [Restaurant Labor Cost Percentage – Restaurant Inventory Tools](https://restaurantinventorytools.com/restaurant-labor-cost-percentage/)
- [Restaurant & Hospitality Finance Benchmarks 2026 – Eagle Rock CFO](https://www.eaglerockcfo.com/blog/research/restaurant-hospitality-finance-2026)
- [Menu Engineering 101 – Foodics](https://www.foodics.com/menu-engineering-matrix-for-restaurants/)
- [Menu classification chart (Kasavana and Smith 1982) – ResearchGate](https://www.researchgate.net/figure/Menu-classification-chart-Kasavana-and-Smith-1982_fig4_349534704)
- [The Power of Menu Engineering, Part One – AHLEI/ServSafe Brands](https://ahlei.servsafebrands.com/resources-overview/news-and-insights/the-power-of-menu-engineering-part-one)
- [Kitchen Stations in a Restaurant – TILIT NYC](https://www.tilitnyc.com/blogs/restaurant-business-operations/restaurant-kitchen-stations-guide)
- [Kitchen Brigade System – Toast POS](https://pos.toasttab.com/blog/on-the-line/kitchen-brigade)
- [What is BOH in a Restaurant? – OrderingStack](https://orderingstack.com/blog/what-is-boh-in-a-restaurant-the-2026-guide-to-back-of-house-operations)
- [How to calculate inventory PAR levels – Apicbase](https://get.apicbase.com/calculate-inventory-par-levels/)
- [A WISK's Guide: What are PAR Level Inventory – WISK](https://www.wisk.ai/blog/a-wisks-guide-what-are-par-level-inventory)
- [The Impact Of Online Reviews On Restaurants – ChowNow](https://get.chownow.com/blog/impact-of-online-reviews-on-restaurants/)
- [How Restaurant Ratings & Reviews Affect Business – Bloom Intelligence](https://bloomintelligence.com/blog/how-restaurant-ratings-reviews-affect-business/)
- [Multi-unit restaurant franchising – Restaurant Business Online](https://www.restaurantbusinessonline.com/operations/multi-unit-restaurant-franchising-critical-components-when-seeking-diversify)
- [Manage Growth with Smart Multi-Unit Restaurant Strategies – Metrobi](https://metrobi.com/blog/multi-unit-restaurant-manage-growth-with-strategies/)
- [Restaurant Failure Rate Statistics and Management Insights – Oregon State Nexus](https://blogs.oregonstate.edu/nexus/2024/11/27/restaurant-failure-rate-statistics-and-management-insights/)
- [The Top 22 Reasons Why Restaurants Fail – MarketMan](https://www.marketman.com/blog/the-top-5-reasons-why-restaurants-fail)
- [Restaurant Turnover Rate – Netchex](https://netchex.com/blog/restaurant-turnover-rate-understanding-costs-benchmarks-and-proven-retention-strategies/)
- [Restaurant Staffing Statistics: 79.6% Turnover Rate – Turnozo](https://turnozo.com/blog/restaurant-staffing-statistics)
- [Independent Ownership vs. Restaurant Franchising – WebstaurantStore](https://www.webstaurantstore.com/article/3/independent-ownership-franchise-pros-cons.html)
- [Restaurant Franchise vs Independent: Complete Guide 2026 – Breadless Franchise](https://franchise.eatbreadless.com/blog/restaurant-franchise-vs-independent/)
