# Restaurant Empire Successor

A restaurant management/tycoon game — cooking-themed, but a business/strategy game at heart.
You are a chef who opens one restaurant on a tight budget and builds it into an empire.

- **Design rationale:** [`docs/design.md`](docs/design.md) — ten planning phases. Read the section you need, not the whole thing.
- **Working rules for this repo:** [`CLAUDE.md`](CLAUDE.md) — architecture rules, scope discipline, current milestone.

## Running it

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (10.x).

```bash
dotnet test     # run every test — this is how M0 is verified
dotnet build    # compile only
```

There is nothing to *play* yet, by design. M0 is a headless simulation validated by tests
and logs, not by playing — proving the core math before spending an hour on rendering.

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

## Current status — M0, partially complete

M0 proves the one architectural claim the whole design rests on: **a supplier decision
propagates everywhere it applies, with zero manual editing.** Restaurant Empire II made
this a per-recipe chore and it was that game's most-criticised flaw.

Both M0 exit tests pass:

1. Switching a supplier assignment updates every dependent recipe's contribution margin
   with zero manual edits, across every location.
2. A new recipe can be added by writing a JSON file alone, with no code change.

**Built:** Company → Restaurant hierarchy · Suppliers (single assignment record, live
readers, no caching) · Ingredients with par levels · Recipes with live contribution
margin · Kasavana-Smith classification · JSON content loading.

**Still outstanding for M0:** Time · Economy · Kitchen throughput · Customers · save/load.

M0 is not finished until those exist. Do not start M1 before then.

## The one rule worth knowing before you touch anything

Nothing caches a cost. `RecipeDefinition` has no cost or margin property at all — there is
nowhere for a stale number to live, and a test enforces it. If you ever find yourself
adding `PlateCost` to a recipe, that is the exact bug this architecture exists to prevent.
