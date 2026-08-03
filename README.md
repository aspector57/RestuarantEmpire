# Restaurant Empire Successor

A restaurant management/tycoon game — cooking-themed, but a business/strategy game at heart.
You are a chef who opens one restaurant on a tight budget and builds it into an empire.

- **Design rationale:** [`docs/design.md`](docs/design.md) — ten planning phases. Read the section you need, not the whole thing.
- **Working rules for this repo:** [`CLAUDE.md`](CLAUDE.md) — architecture rules, scope discipline, current milestone.

## Picking this up on a new laptop, or a new Claude account

Everything needed is in this repository. Nothing lives in a chat.

```bash
git clone https://github.com/aspector57/RestuarantEmpire.git
cd RestuarantEmpire
dotnet test          # 256 tests. If these pass, the machine is ready.
claude               # CLAUDE.md loads itself
```

**`CLAUDE.md` is the handoff.** It carries the architecture rules, the current milestone, and
every measurement taken so far — including the reasoning behind decisions that look arbitrary
without it (why reputation moves per *meal* rather than per night; why a seat you cannot feed is
worse than no seat; why the browser forecast is knowingly 18% out). A new session starts knowing
all of it.

**You will need:**

| | |
|---|---|
| **.NET SDK 10** | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download). Without it you can read and edit but not run the tests, which is how everything here is verified. |
| **A GitHub token** | Keychain credentials do not travel. Make a classic token with `repo` scope, then run the one-liner below in *your* terminal — never paste it into a chat. |

```bash
printf "protocol=https\nhost=github.com\nusername=aspector57\npassword=<TOKEN>\n\n" | git credential approve
```

**The browser build lives at [`web/pass.html`](web/pass.html)** and is a plain file — open it in
any browser to play. Publishing it as a shareable artifact is tied to whichever Claude account
does it, so a new account will mint a new URL. The file is the source of truth; the link is not.

**Run these before trusting anything you change:**

```bash
dotnet test                                          # the engine
python3 tools/headless.py tools/probe-panels.js      # does every screen still render
python3 tools/headless.py tools/playthrough.js       # a full run, self-checked for contradictions
python3 tools/headless.py tools/levers.js            # is each decision real, a trap, or a purchase
python3 tools/headless.py tools/scenario.js          # a year, with changes given time to settle
```

## Working on this from another machine

The whole project is in git and self-contained — the only requirement is the .NET SDK.

```bash
# once, on this laptop — replace with your own repo
gh repo create restaurant-empire --private --source=. --push

# then on the other laptop
git clone https://github.com/<you>/restaurant-empire.git
cd restaurant-empire
dotnet test          # 249 tests; if these pass, the machine is set up
```

`bin/` and `obj/` are gitignored and regenerate on first build, so nothing else is needed.

**With Claude Code**, from the cloned directory:

| Where | How |
|---|---|
| Terminal (any OS) | `claude` in the repo root |
| VS Code / JetBrains | the Claude Code extension, repo open |
| Desktop app | Mac and Windows |
| Browser | [claude.ai/code](https://claude.ai/code) — needs the repo pushed to GitHub |

`CLAUDE.md` loads automatically wherever it runs, so the architecture rules, the current
milestone and every measurement recorded so far come with you. **That file is the handoff** —
it is why a session on another laptop does not start from nothing.

## Running it



Requires the [.NET SDK](https://dotnet.microsoft.com/download) (10.x).

```bash
dotnet test     # run every test — this is how M0 is verified
dotnet build    # compile only
```

### Driving it

No graphics yet — that is M1's Unity layer. But the loop is real and you can play it in
the terminal:

```bash
dotnet run --project src/RestaurantEmpire.Sim          # drive it yourself; it asks where to open
dotnet run --project src/RestaurantEmpire.Sim -- --help
```

You jump time forward and the restaurant interrupts you when something needs deciding:

```
[h]our [d]ay [w]eek [m]onth   [a]ct   [b]ooks [k]menu [x]matrix   [q]uit
```

When it stops you it says what happened and asks whether that was worth stopping for —
it keeps score and reports the tally when you quit, which is how M1's rhythm bar gets
answered. Press `a` to actually do something about it: buy a slot at a station, change
prices, switch supplier, or change your opening hours.

Set the world up with flags:

```bash
--location nightlife --hours 18-23,23-2   # a late service where there is actually a crowd
--location business  --hours 7-10,12-15   # breakfast and lunch, dead by evening
--menu dinner                             # open for breakfast with nothing anyone wants
--supplier premium-harvest --price 1.5    # buy better ingredients and charge for them
--stations 1                              # choke the kitchen and watch guests walk
--auto 30                                 # run a month non-interactively
```

`--help` lists them all.

## Layout

```
src/RestaurantEmpire.Core/     the simulation. Plain C#, no engine dependency.
  Definitions/                 immutable content loaded from JSON (ingredients, suppliers, recipes)
  Model/                       game state (Company, Restaurant, SupplierPolicy, costing)
  Content/                     JSON loading
tests/RestaurantEmpire.Core.Tests/
data/                          game content as editable JSON — no code change needed to add a dish
docs/                          design documents
```

**`src/RestaurantEmpire.Core` targets `netstandard2.1`, not the newest .NET.** That is
deliberate: it is what lets this simulation core drop into Unity at M1 without a rewrite.
Don't "modernise" the target framework without checking Unity still loads it.

## Current status — M0 complete, M1 in progress

M0 proves the one architectural claim the whole design rests on: **a supplier decision
propagates everywhere it applies, with zero manual editing.** Restaurant Empire II made
this a per-recipe chore and it was that game's most-criticised flaw.

Both M0 exit tests pass:

1. Switching a supplier assignment updates every dependent recipe's contribution margin
   with zero manual edits, across every location.
2. A new recipe can be added by writing a JSON file alone, with no code change.

**Built:** Company → Restaurant hierarchy · Suppliers, resolving up a
`Company → Restaurant` inheritance chain with no caching · Ingredients with par levels ·
Recipes with live contribution margin · Kasavana-Smith classification · JSON content
loading · Time (`GameClock`) · Kitchen throughput (brigade stations with real queueing) ·
Customers (arrival curve + satisfaction formula) · Economy (append-only ledger, live prime
cost) · save/load with version stamps and graceful degradation · menu pricing as a player
decision.

**M1 so far:** a continuously-running, resumable simulation (`SimulationRunner`) where the
clock runs 24 hours a day and pausing, interrupting and fast-forwarding are provably
lossless · demand driven by `Neighbourhood` rather than set by the player · menus that have
to suit the hour · a fit-out (stations, tables, decor) that costs real money.

**Deferred out of M0 on purpose, both needing the game loop rather than the core:** the
autosave *policy* (rolling slots, prompt-on-exit) — the save format and its degradation
behaviour are built, the scheduling is not; and labour cost *generation* — Economy tracks
labour, but nothing produces it until Employees arrive at M1, so prime cost is only as
complete as the labour figure booked against it.

Both remain outstanding. Labour is currently approximated by the harness rather than
generated by the simulation.

### Sourcing scope

Supplier assignments resolve up a chain: **Company → (Region, at M4) → Restaurant.** The
company-level assignment propagates everywhere by default; a restaurant may override, and
that override is a deliberate exception rather than the norm. A Region tier slots in at M4
without any read site changing, because resolution already walks a chain.

Without a regional tier, sourcing at ten restaurants would be the identical decision as at
one — the flat-scaling trap. "National contract vs. local sourcing" is a decision that
cannot exist before expansion, which is precisely what multi-location should add.

## The one rule worth knowing before you touch anything

Nothing caches a cost. `RecipeDefinition` has no cost or margin property at all — there is
nowhere for a stale number to live, and a test enforces it. If you ever find yourself
adding `PlateCost` to a recipe, that is the exact bug this architecture exists to prevent.
