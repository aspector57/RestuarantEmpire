using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Concepts are CONTENT, and this holds them to Architecture Rule 2's promise: a new one
    /// must be addable by writing a data file, with no engine change.
    ///
    /// Deliberately asserts invariants rather than counts. An earlier content test asserted
    /// "13 ingredients, 7 recipes" and broke the instant anyone added anything — hostile to
    /// the very rule it existed to protect. **A test that fails when you do the thing it is
    /// meant to permit is a bad test, not a passing content change.**
    /// </summary>
    public class ConceptContentTests
    {
        [Fact]
        public void TheShippedConceptsLoadAndEveryDishTheyNameExists()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            Assert.True(definitions.ConceptCount > 0, "No concepts loaded — data/concepts.json is missing or empty.");

            foreach (var concept in definitions.Concepts)
            {
                Assert.False(string.IsNullOrWhiteSpace(concept.Name), concept.Id + " has no name.");
                Assert.False(string.IsNullOrWhiteSpace(concept.Description),
                    concept.Id + " has no description — the player has to read something when choosing.");
                Assert.True(concept.RecipeIds.Count > 0, concept.Id + " has an empty card.");
                Assert.True(concept.Services.Count > 0, concept.Id + " never opens.");

                foreach (var id in concept.RecipeIds)
                    Assert.True(definitions.HasRecipe(id), concept.Id + " names a dish that does not exist: " + id);
            }

            // Nothing silently dropped on the way in.
            Assert.DoesNotContain(definitions.LoadWarnings, w => w.Contains("Concept"));
        }

        /// <summary>
        /// A concept must be PLAYABLE — its card has to be cookable by stations that exist,
        /// or picking it hands the player a restaurant that cannot serve its own menu.
        /// </summary>
        [Fact]
        public void EveryConceptCanBeCookedBySomethingInTheCatalogue()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            foreach (var concept in definitions.Concepts)
            {
                foreach (var recipeId in concept.RecipeIds)
                {
                    var station = definitions.GetRecipe(recipeId).StationId;
                    Assert.True(definitions.EquipmentFor(station).Any(),
                        concept.Name + " needs a '" + station + "' station for " + recipeId +
                        ", and the equipment catalogue has nothing that is one.");
                }
            }
        }

        /// <summary>
        /// ARCHITECTURE RULE 2's EXIT TEST, for concepts specifically: applying one is a data
        /// read, not a code path per concept. Whatever is in the file can be set up.
        /// </summary>
        [Fact]
        public void AConceptCanBeAppliedToARestaurantWithoutKnowingWhichOneItIs()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("c", "C", definitions, 100000m);

            foreach (var concept in definitions.Concepts)
            {
                var r = company.OpenRestaurant(concept.Id, concept.Name, LocationType.BrickAndMortar);
                r.Adopt(concept);

                Assert.Equal(concept.RecipeIds.Count, r.Menu.Count);
                Assert.Equal(concept.Services.Count, r.ServiceWindows.Count);

                foreach (var id in concept.RecipeIds)
                    Assert.Contains(id, r.Menu.RecipeIds);

                // And the card sits where the concept says it should.
                Assert.Equal(concept.PricePosition,
                    r.Costing.PricePosition(r.Menu.RecipeIds), 2);
            }
        }
    }
}
