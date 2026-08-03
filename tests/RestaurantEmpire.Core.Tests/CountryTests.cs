using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// A COUNTRY HAS TO CHANGE WHAT YOU CAN DO, NOT WHAT THINGS COST.
    ///
    /// Expansion was measured before it was built and a second restaurant came out 0.4% from
    /// pure arithmetic — the flat-scaling anti-pattern. **More places to put a restaurant does
    /// not fix that on its own.** A site in Lyon that is only "different rent, different
    /// footfall" is another arithmetic restaurant with a flag on it.
    ///
    /// So these hold countries to the same bar the second restaurant failed: the decisions
    /// have to be genuinely different, not the numbers.
    /// </summary>
    public class CountryTests
    {
        private readonly ITestOutputHelper _out;
        public CountryTests(ITestOutputHelper o) { _out = o; }

        [Fact]
        public void TheShippedCountriesLoadAndEverySupplierTheyNameExists()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            Assert.True(definitions.CountryCount > 0, "No countries loaded.");
            Assert.DoesNotContain(definitions.LoadWarnings, w => w.Contains("Country"));

            foreach (var country in definitions.Countries)
            {
                Assert.False(string.IsNullOrWhiteSpace(country.Description), country.Id + " has no description.");
                Assert.True(country.LocalSupplierIds.Count > 0,
                    country.Name + " has no local supplier at all — everything would be an import.");
                Assert.True(country.NeighborhoodIds.Count > 0, country.Name + " has nowhere to build.");

                foreach (var id in country.LocalSupplierIds)
                    Assert.True(definitions.HasSupplier(id), country.Id + " names a supplier that does not exist: " + id);
            }
        }

        /// <summary>
        /// THE BAR. The same card must not suit every country equally, or a country is a
        /// reskin. Measured across the shipped concepts.
        /// </summary>
        [Fact]
        public void ACardThatWinsAtHomeDoesNotWinEverywhere()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var countries = definitions.Countries.ToList();

            _out.WriteLine("HOW EACH CONCEPT READS IN EACH MARKET (appetite of a typical local crowd)");
            _out.WriteLine("");

            var header = "  " + "concept".PadRight(28);
            foreach (var c in countries) header += c.Name.PadLeft(14);
            _out.WriteLine(header);

            var winners = new System.Collections.Generic.HashSet<string>();
            var perCountryBest = new System.Collections.Generic.Dictionary<string, string>();

            foreach (var concept in definitions.Concepts)
            {
                var line = "  " + concept.Name.PadRight(28);

                foreach (var country in countries)
                {
                    var score = Appetite(definitions, concept, country);
                    line += score.ToString("N2").PadLeft(14);

                    string bestSoFar;
                    if (!perCountryBest.TryGetValue(country.Id, out bestSoFar) ||
                        score > Appetite(definitions, definitions.GetConcept(bestSoFar), country))
                    {
                        perCountryBest[country.Id] = concept.Id;
                    }
                }

                _out.WriteLine(line);
            }

            _out.WriteLine("");
            foreach (var country in countries)
            {
                var best = definitions.GetConcept(perCountryBest[country.Id]);
                winners.Add(best.Id);
                _out.WriteLine("  " + country.Name.PadRight(18) + "-> " + best.Name);
            }

            _out.WriteLine("");
            _out.WriteLine("  distinct winners across " + countries.Count + " countries: " + winners.Count);

            Assert.True(winners.Count >= 2,
                "Every country wants the same concept, so a country is a reskin rather than a market. " +
                "That is the flat-scaling anti-pattern the second-restaurant measurement already failed.");
        }

        /// <summary>
        /// Your usual supplier is still available abroad, and is now a bad idea. That is what
        /// makes expansion RE-OPEN the sourcing decision rather than scale a settled one.
        /// </summary>
        [Fact]
        public void ShippingYourHomeSupplierAbroadCostsYouFreshness()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("c", "C", definitions, 200000m);

            // Budget Wholesale delivers in the US and not in Italy.
            var italy = definitions.GetCountry("italy");
            Assert.False(italy.SuppliesLocally("budget-wholesale"));
            Assert.True(italy.SuppliesLocally("valley-produce"));

            var abroad = company.CreateRegion("it", "Italy", italy);
            var home = company.OpenRestaurant("home", "Home", LocationType.BrickAndMortar);
            var overseas = company.OpenRestaurant("away", "Florence", LocationType.BrickAndMortar, abroad);

            company.SupplierPolicy.AssignAll("budget-wholesale");

            var atHome = home.TransitDaysFor("tomato");
            var shippedIn = overseas.TransitDaysFor("tomato");

            _out.WriteLine("budget-wholesale tomato — at home " + atHome + " days old, in Florence " + shippedIn);

            Assert.True(shippedIn > atHome,
                "Shipping your home supplier abroad cost nothing, so sourcing is the same decision " +
                "everywhere and expansion has not re-opened it.");

            // ...and switching to the local grower fixes it, which is the decision.
            overseas.SupplierPolicy.AssignAll("valley-produce");
            var local = overseas.TransitDaysFor("tomato");

            _out.WriteLine("switching Florence to the local grower — " + local + " days old");
            Assert.True(local < shippedIn);
        }

        [Fact]
        public void LaborCostsWhatTheMarketCharges()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("c", "C", definitions, 200000m);

            var france = company.CreateRegion("fr", "France", definitions.GetCountry("france"));
            var home = company.OpenRestaurant("home", "Home", LocationType.BrickAndMortar);
            var lyon = company.OpenRestaurant("lyon", "Lyon", LocationType.BrickAndMortar, france);

            foreach (var r in new[] { home, lyon })
                for (var i = 0; i < 3; i++) r.Payroll.Hire(new Employee(r.Id + i, "Cook", StaffRole.Cook, 16m));

            _out.WriteLine("same three cooks — home $" + home.HourlyWageBill + "/hr, Lyon $" + lyon.HourlyWageBill);

            Assert.True(lyon.HourlyWageBill > home.HourlyWageBill);
        }

        private static decimal Appetite(
            Definitions.DefinitionRegistry definitions,
            Definitions.ConceptDefinition concept,
            Definitions.CountryDefinition country)
        {
            // A neutral guest, so the COUNTRY is the only thing moving.
            var party = new CustomerParty("p", 2, 0, 30, 1m, CustomerArchetype.Local);

            var total = 0m;
            foreach (var id in concept.RecipeIds)
                total += party.AppetiteFor(definitions.GetRecipe(id), 1m, 0.6m, country);

            return concept.RecipeIds.Count == 0 ? 0m : total / concept.RecipeIds.Count;
        }
    }
}
