using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Definitions;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Save/load, and specifically its behaviour when the content underneath a save has
    /// changed — the failure the design doc calls out by name, because RimWorld (the model
    /// for the data-driven approach) is notorious for it.
    ///
    /// The bar: losing a mod's three recipes must cost you three recipes, never a
    /// forty-hour career.
    /// </summary>
    public class SaveLoadTests
    {
        private static Company BuildPlayedGame(out GameClock clock)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme", "Acme Restaurant Group", definitions, 20000m);

            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            flagship.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            flagship.BuyTables("tables", "Walnut tables", 5040m, 42, 0.75m);
            flagship.Kitchen.Install("oven", "Wood Oven", 2);
            flagship.Kitchen.Install("garde-manger", "Garde Manger");
            flagship.Kitchen.Install("saute", "Saute", 1, 1.5m);
            flagship.Inventory.SetPar("tomato", 10m, 40m);
            flagship.Inventory.Receive("tomato", 27.5m);

            var truck = company.OpenRestaurant("truck", "Acme Truck", LocationType.FoodTruck);
            truck.Menu.Add("margherita");
            truck.SupplierPolicy.Assign("tomato", "premium-harvest");   // a local exception
            truck.Pricing.SetPrice("margherita", 14.50m);               // and a local price

            company.SupplierPolicy.AssignAll("valley-produce");
            company.Pricing.SetPrice("margherita", 13.00m);

            company.Economy.Record(0, LedgerCategory.Revenue, 923.00m, "Opening night", flagship.Id);
            company.Economy.Record(0, LedgerCategory.FoodCost, 338.64m, "Ingredients", flagship.Id);

            clock = new GameClock();
            clock.AdvanceDays(9);
            clock.AdvanceHours(19);
            clock.Speed = GameSpeed.Fast;

            return company;
        }

        private static DefinitionRegistry Definitions()
        {
            return JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
        }

        [Fact]
        public void AFullRoundTripRestoresEverythingThatMatters()
        {
            var company = BuildPlayedGame(out var clock);
            var json = SaveGameSerializer.ToJson(company, clock);

            var loaded = SaveGameSerializer.FromJson(json, Definitions());

            Assert.True(loaded.LoadedCleanly);
            Assert.Equal("acme", loaded.Company.Id);
            Assert.Equal(2, loaded.Company.Restaurants.Count);

            var flagship = loaded.Company.GetRestaurant("flagship");
            Assert.Equal(42, flagship.SeatingCapacity);
            Assert.Equal(4, flagship.Menu.Count);
            Assert.Equal(3, flagship.Kitchen.StationCount);
            Assert.Equal(2, flagship.Kitchen.Get("oven").ConcurrentCapacity);
            Assert.Equal(1.5m, flagship.Kitchen.Get("saute").SpeedMultiplier);
            Assert.Equal(27.5m, flagship.Inventory.QuantityOf("tomato"));
            Assert.Equal(10m, flagship.Inventory["tomato"].ParMin);

            // The clock came back exactly, including speed.
            Assert.Equal(clock.Tick, loaded.Clock.Tick);
            Assert.Equal(clock.Now, loaded.Clock.Now);
            Assert.Equal(GameSpeed.Fast, loaded.Clock.Speed);
        }

        [Fact]
        public void CashIsReplayedFromTheLedger_SoTheBalanceCanNeverDisagreeWithTheBooks()
        {
            var company = BuildPlayedGame(out var clock);
            var expectedCash = company.Economy.CashOnHand;

            var loaded = SaveGameSerializer.FromJson(SaveGameSerializer.ToJson(company, clock), Definitions());

            Assert.Equal(expectedCash, loaded.Company.Economy.CashOnHand);
            Assert.Equal(company.Economy.Entries.Count, loaded.Company.Economy.Entries.Count);
            Assert.Equal(923.00m, loaded.Company.Economy.SummarizeAll("flagship").Revenue);
        }

        [Fact]
        public void BothPolicyChainsSurvive_IncludingLocalExceptions()
        {
            var company = BuildPlayedGame(out var clock);
            var loaded = SaveGameSerializer.FromJson(SaveGameSerializer.ToJson(company, clock), Definitions());

            var flagship = loaded.Company.GetRestaurant("flagship");
            var truck = loaded.Company.GetRestaurant("truck");

            // The truck's deliberate exceptions came back as exceptions, not as flattened values.
            Assert.True(truck.SupplierPolicy.HasLocalOverride("tomato"));
            Assert.False(flagship.SupplierPolicy.HasLocalOverride("tomato"));
            Assert.Equal("Acme Restaurant Group", flagship.SupplierPolicy.ResolvedFromScopeName("tomato"));
            Assert.Equal("Acme Truck", truck.SupplierPolicy.ResolvedFromScopeName("tomato"));

            Assert.Equal(13.00m, flagship.Costing.MenuPrice("margherita"));
            Assert.Equal(14.50m, truck.Costing.MenuPrice("margherita"));
        }

        [Fact]
        public void ARestoredGameStillSimulatesIdentically()
        {
            // The real test of a save: the world behaves the same after reloading it.
            var company = BuildPlayedGame(out var clock);
            var original = company.GetRestaurant("flagship");
            foreach (var id in company.Definitions.IngredientIds) original.Inventory.Receive(id, 10000m);

            // SAVE FIRST, then run the same night in both worlds.
            //
            // This used to save AFTER running the original's night, which quietly compared two
            // different starting states — the save captured a restaurant that had already
            // served the night being measured. It passed only because nothing persistent
            // survived a service: stock was topped up well past what one night could use, and
            // there was nothing else to carry over. Reputation is the first thing that
            // genuinely accumulates, so the flaw stopped being harmless.
            var loaded = SaveGameSerializer.FromJson(SaveGameSerializer.ToJson(company, clock), Definitions());

            var before = Dinner.Run(original, 25, 99);
            var after = Dinner.Run(loaded.Company.GetRestaurant("flagship"), 25, 99);

            Assert.Equal(before.Revenue, after.Revenue);
            Assert.Equal(before.CoversServed, after.CoversServed);
            Assert.Equal(before.AverageSatisfaction, after.AverageSatisfaction);
            Assert.Equal(before.UnitsSoldByRecipeId, after.UnitsSoldByRecipeId);
        }

        [Fact]
        public void EverySaveCarriesAVersionStampAndItsContentPacks()
        {
            var company = BuildPlayedGame(out var clock);

            var loaded = SaveGameSerializer.FromJson(
                SaveGameSerializer.ToJson(company, clock, new[] { "core", "tuscan-classics" }), Definitions());

            Assert.Equal(SaveGame.CurrentFormatVersion, loaded.SaveFormatVersion);
            Assert.Equal(SaveGame.CurrentGameVersion, loaded.GameVersion);
            Assert.Equal(new[] { "core", "tuscan-classics" }, loaded.ContentPacks);
        }

        [Fact]
        public void TheFileIsInspectableJson_NotAnOpaqueBlob()
        {
            var company = BuildPlayedGame(out var clock);
            var json = SaveGameSerializer.ToJson(company, clock);

            // A human, a bug report, or a community tool can read this.
            Assert.Contains("\"saveFormatVersion\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("margherita", json);
            Assert.Contains("Acme Truck", json);
            Assert.Contains("\n", json);   // indented, not minified
        }

        [Fact]
        public void ReferencesAreByStableStringId_NeverByIndexOrLoadOrder()
        {
            var company = BuildPlayedGame(out var clock);
            var save = SaveGameSerializer.Capture(company, clock);

            Assert.Contains("margherita", save.Company.Restaurants[0].Menu);
            Assert.Equal("valley-produce", save.Company.SupplierAssignments["tomato"]);
            Assert.Contains(save.Company.Restaurants[0].Inventory, s => s.IngredientId == "tomato");
        }

        // ---- Graceful degradation: the part that actually matters ----

        [Fact]
        public void ARecipeThatVanished_IsDroppedWithAPlainWarning_NotACrash()
        {
            var company = BuildPlayedGame(out var clock);
            var json = SaveGameSerializer.ToJson(company, clock);

            // Simulate the player uninstalling the pack that added the risotto.
            var reduced = WithoutRecipe("truffle-risotto");

            var loaded = SaveGameSerializer.FromJson(json, reduced);

            Assert.False(loaded.LoadedCleanly);
            Assert.Contains(loaded.Warnings, w => w.Contains("truffle-risotto") && w.Contains("no longer installed"));

            // Everything else survived. That is the whole point.
            var flagship = loaded.Company.GetRestaurant("flagship");
            Assert.Equal(3, flagship.Menu.Count);
            Assert.False(flagship.Menu.Contains("truffle-risotto"));
            Assert.True(flagship.Menu.Contains("margherita"));
            Assert.Equal(2, loaded.Company.Restaurants.Count);
            Assert.Equal(company.Economy.CashOnHand, loaded.Company.Economy.CashOnHand);
        }

        [Fact]
        public void AnIngredientThatVanished_TakesOnlyItsOwnStockAndSourcingWithIt()
        {
            var company = BuildPlayedGame(out var clock);
            var json = SaveGameSerializer.ToJson(company, clock);

            var reduced = WithoutIngredient("tomato");
            var loaded = SaveGameSerializer.FromJson(json, reduced);

            Assert.False(loaded.LoadedCleanly);
            Assert.Contains(loaded.Warnings, w => w.Contains("tomato"));

            var flagship = loaded.Company.GetRestaurant("flagship");
            Assert.Equal(0m, flagship.Inventory.QuantityOf("tomato"));

            // Other stock, other suppliers, and the books are all untouched.
            Assert.True(loaded.Company.SupplierPolicy.IsAssigned("mozzarella"));
            Assert.Equal(company.Economy.CashOnHand, loaded.Company.Economy.CashOnHand);
        }

        [Fact]
        public void ASupplierThatVanished_LosesOnlyThatChoice()
        {
            var company = BuildPlayedGame(out var clock);
            var json = SaveGameSerializer.ToJson(company, clock);

            var reduced = WithoutSupplier("premium-harvest");   // the truck's local exception used this
            var loaded = SaveGameSerializer.FromJson(json, reduced);

            Assert.Contains(loaded.Warnings, w => w.Contains("premium-harvest"));

            var truck = loaded.Company.GetRestaurant("truck");
            Assert.False(truck.SupplierPolicy.HasLocalOverride("tomato"));

            // It falls back up the chain rather than becoming unsourced.
            Assert.Equal("Acme Restaurant Group", truck.SupplierPolicy.ResolvedFromScopeName("tomato"));
        }

        [Fact]
        public void ASaveFromANewerBuild_LoadsWithAWarningRatherThanRefusing()
        {
            var company = BuildPlayedGame(out var clock);
            var save = SaveGameSerializer.Capture(company, clock);
            save.SaveFormatVersion = SaveGame.CurrentFormatVersion + 5;

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(save);
            var loaded = SaveGameSerializer.FromJson(json, Definitions());

            Assert.Contains(loaded.Warnings, w => w.Contains("newer version"));
            Assert.Equal(2, loaded.Company.Restaurants.Count);   // still loaded what it could
        }

        [Fact]
        public void ACorruptFileThrows_BecauseThatIsADifferentProblemFromMissingContent()
        {
            Assert.Throws<InvalidDataException>(
                () => SaveGameSerializer.FromJson("{ this is not json", Definitions()));

            Assert.Throws<InvalidDataException>(
                () => SaveGameSerializer.FromJson("{ }", Definitions()));
        }

        [Fact]
        public void SavingAndLoadingThroughAFileWorksEndToEnd()
        {
            var company = BuildPlayedGame(out var clock);
            var directory = Path.Combine(Path.GetTempPath(), "re-save-" + Path.GetRandomFileName());
            var path = Path.Combine(directory, "career.json");

            SaveGameSerializer.SaveToFile(path, company, clock);
            Assert.True(File.Exists(path));

            var loaded = SaveGameSerializer.LoadFromFile(path, Definitions());
            Assert.Equal(company.Economy.CashOnHand, loaded.Company.Economy.CashOnHand);

            Directory.Delete(directory, true);
        }

        // ---- Helpers that simulate content disappearing between sessions ----

        private static DefinitionRegistry WithoutRecipe(string recipeId)
        {
            var full = Definitions();
            return new DefinitionRegistry(full.Ingredients, full.Suppliers,
                full.Recipes.Where(r => r.Id != recipeId));
        }

        private static DefinitionRegistry WithoutIngredient(string ingredientId)
        {
            var full = Definitions();
            return new DefinitionRegistry(
                full.Ingredients.Where(i => i.Id != ingredientId), full.Suppliers,
                full.Recipes.Where(r => !r.Uses(ingredientId)));
        }

        private static DefinitionRegistry WithoutSupplier(string supplierId)
        {
            var full = Definitions();
            return new DefinitionRegistry(full.Ingredients,
                full.Suppliers.Where(s => s.Id != supplierId), full.Recipes);
        }
    }
}
