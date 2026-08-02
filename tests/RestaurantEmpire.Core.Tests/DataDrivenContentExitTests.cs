using System.IO;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// M0 EXIT TEST #2 (CLAUDE.md, "M0 exit tests"):
    ///
    ///   "A new Recipe can be added purely by writing a data file, with no engine/code changes."
    ///
    /// This is what proves the data-driven claim is real rather than stated. If adding a
    /// dish ever requires touching C#, modding is dead and the claim was a fiction.
    /// </summary>
    public class DataDrivenContentExitTests
    {
        /// <summary>Copies the real data directory somewhere temporary so tests can add files to it safely.</summary>
        private static string CloneDataDirectory()
        {
            var temp = Path.Combine(Path.GetTempPath(), "re-m0-" + Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            Directory.CreateDirectory(Path.Combine(temp, "recipes"));

            File.Copy(Path.Combine(TestData.DataDirectory, "ingredients.json"), Path.Combine(temp, "ingredients.json"));
            File.Copy(Path.Combine(TestData.DataDirectory, "suppliers.json"), Path.Combine(temp, "suppliers.json"));

            foreach (var recipe in Directory.GetFiles(Path.Combine(TestData.DataDirectory, "recipes"), "*.json"))
                File.Copy(recipe, Path.Combine(temp, "recipes", Path.GetFileName(recipe)));

            return temp;
        }

        [Fact]
        public void ANewRecipe_CanBeAddedByWritingAFileAlone_WithNoCodeChange()
        {
            var dataDir = CloneDataDirectory();

            var before = JsonDefinitionLoader.LoadFromDirectory(dataDir);
            Assert.False(before.HasRecipe("arrabbiata"));

            // The only thing that happens here is a file appearing on disk.
            File.WriteAllText(Path.Combine(dataDir, "recipes", "arrabbiata.json"), @"{
  ""id"": ""arrabbiata"",
  ""name"": ""Penne all'Arrabbiata"",
  ""menuPrice"": 15.00,
  ""ingredients"": [
    { ""ingredientId"": ""flour"",  ""quantity"": 0.18 },
    { ""ingredientId"": ""tomato"", ""quantity"": 0.22 },
    { ""ingredientId"": ""basil"",  ""quantity"": 0.02 }
  ]
}");

            var after = JsonDefinitionLoader.LoadFromDirectory(dataDir);

            Assert.True(after.HasRecipe("arrabbiata"));
            Assert.Equal("Penne all'Arrabbiata", after.GetRecipe("arrabbiata").Name);

            // And the brand-new dish is a full citizen: it costs, it sells, and it responds
            // to supplier policy exactly like the hand-written ones.
            var company = new Company("acme-group", "Acme Restaurant Group", after);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            restaurant.Menu.Add("arrabbiata");
            company.SupplierPolicy.AssignAll("valley-produce");

            // 0.18*1.80 + 0.22*3.00 + 0.02*1.60 = 0.324 + 0.66 + 0.032 = 1.016
            Assert.Equal(15.00m - 1.016m, restaurant.Costing.ContributionMargin("arrabbiata"));

            company.SupplierPolicy.Assign("tomato", "premium-harvest");

            // 0.18*1.80 + 0.22*5.00 + 0.02*1.60 = 0.324 + 1.10 + 0.032 = 1.456
            Assert.Equal(15.00m - 1.456m, restaurant.Costing.ContributionMargin("arrabbiata"));

            Directory.Delete(dataDir, true);
        }

        [Fact]
        public void ARecipeReferencingMissingContent_IsDroppedWithAWarning_NotCrashed()
        {
            // Architecture Rule 3, "degrade gracefully". A player who uninstalls a mod must
            // get a plain warning and a working game, not a stack trace.
            var dataDir = CloneDataDirectory();

            File.WriteAllText(Path.Combine(dataDir, "recipes", "broken.json"), @"{
  ""id"": ""wagyu-special"",
  ""name"": ""Wagyu Special"",
  ""menuPrice"": 90.00,
  ""ingredients"": [ { ""ingredientId"": ""wagyu-beef"", ""quantity"": 0.3 } ]
}");

            var registry = JsonDefinitionLoader.LoadFromDirectory(dataDir);

            Assert.False(registry.HasRecipe("wagyu-special"));
            Assert.Contains(registry.LoadWarnings, w => w.Contains("wagyu-beef"));

            // Crucially: everything else still loaded — the ONE bad file is dropped and
            // nothing else is. Counted against the shipped catalogue rather than a literal,
            // so adding content never fails this for the wrong reason.
            Assert.True(registry.HasRecipe("margherita"));

            var shipped = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            Assert.Equal(shipped.RecipeCount, registry.RecipeCount);

            Directory.Delete(dataDir, true);
        }

        [Fact]
        public void TheShippedContentFiles_LoadCleanlyWithNoWarnings()
        {
            var registry = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            Assert.Empty(registry.LoadWarnings);

            // ASSERTS THE INVARIANT, NOT THE INVENTORY. This used to check exact counts
            // (13 ingredients, 7 recipes), which broke the moment any content was added —
            // hostile to the rule it exists to protect, since Architecture Rule 2's whole
            // claim is that new content needs no code change. Adding a drinks list failed it
            // for entirely the wrong reason.
            //
            // What actually matters is that nothing was silently dropped and everything
            // shipped can really be costed.
            Assert.True(registry.IngredientCount > 0);
            Assert.True(registry.SupplierCount > 0);
            Assert.True(registry.RecipeCount > 0);

            foreach (var recipe in registry.Recipes)
            {
                Assert.False(string.IsNullOrWhiteSpace(recipe.StationId),
                    recipe.Id + " has no station, so nothing could ever cook it");
                Assert.True(recipe.MenuPrice > 0m, recipe.Id + " has no price");

                foreach (var line in recipe.Ingredients)
                {
                    Assert.True(registry.HasIngredient(line.IngredientId),
                        recipe.Id + " needs '" + line.IngredientId + "', which no ingredient file defines");

                    // Every supplier must be able to quote every ingredient, or switching
                    // supplier would throw on a dish that costed fine a moment earlier.
                    foreach (var supplier in registry.Suppliers)
                        Assert.True(supplier.Carries(line.IngredientId),
                            supplier.Id + " has no price for '" + line.IngredientId + "'");
                }
            }
        }

        [Fact]
        public void RecipesUsing_ReportsExactlyWhatASupplierSwitchWillMove()
        {
            var registry = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            var tomatoDishes = registry.RecipesUsing("tomato").Select(r => r.Id).OrderBy(id => id).ToList();

            Assert.Equal(new[] { "caprese-salad", "margherita" }, tomatoDishes);
        }
    }
}
