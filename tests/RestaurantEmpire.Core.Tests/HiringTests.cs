using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Hiring as a decision with risk in it, rather than a button that adds a unit.
    ///
    /// Aaron: *"in this model, it was hire a cook. In the real game, there will be profiles of
    /// cooks with their own rates, you can hire good cooks or bad cooks, they can do a good
    /// job or bad job, things can go wrong."*
    ///
    /// `Employee.Skill` existed, validated, from the first commit and was read by NOTHING for
    /// the whole of M1 — the fourth time that shape has appeared here after PriceSensitivity,
    /// IngredientQuality and PartiesTurnedAway.
    /// </summary>
    public class HiringTests
    {
        private static Restaurant Build(out Company company, decimal cookSkill, int cooks = 4, int servers = 3)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme", definitions, 200000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Location = Neighborhood.SuburbanHighStreet();
            restaurant.FloorArea = 2150m;
            restaurant.Menu.Add("margherita", "caprese-salad", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");
            restaurant.Reputation.Restore(Reputation.Neutral, Reputation.MealsToBecomeKnown);

            restaurant.BuyEquipment(definitions.GetEquipment("oven-commercial"), 4);
            restaurant.BuyEquipment(definitions.GetEquipment("gm-refrigerated"), 3);
            restaurant.BuyTables("t", "Tables", 4000m, 32);

            for (var i = 0; i < cooks; i++)
                restaurant.Payroll.Hire(new Employee("c" + i, "Cook " + i, StaffRole.Cook, 16m, cookSkill));
            for (var i = 0; i < servers; i++)
                restaurant.Payroll.Hire(new Employee("s" + i, "Server " + i, StaffRole.Server, 12m, 0.5m));

            foreach (var id in definitions.IngredientIds)
            {
                restaurant.Inventory.SetPar(id, 300m, 4000m);
                restaurant.Inventory.Receive(id, 4000m);
            }

            return restaurant;
        }

        // ---- What a CV is worth ----

        [Fact]
        public void ACandidateIsPricedOnWhatTheyCLAIM_NotOnWhatTheyAre()
        {
            // The gap is the whole mechanic. You pay for the CV; you find out afterwards.
            var applicants = HiringPool.Applicants(4242, 12);

            Assert.All(applicants, c => Assert.InRange(c.Advertises, 0m, 1m));
            Assert.Contains(applicants, c => c.HourlyWage > HiringPool.CookFloorWage);

            // Somebody in a pool this size is not what their CV says.
            Assert.Contains(applicants, c => c.Accept().Skill != c.Advertises);
        }

        [Fact]
        public void TheSameDayAlwaysShowsTheSamePeople()
        {
            var monday = HiringPool.Applicants(99);
            var alsoMonday = HiringPool.Applicants(99);

            Assert.Equal(monday.Select(c => c.Name), alsoMonday.Select(c => c.Name));
            Assert.Equal(monday.Select(c => c.HourlyWage), alsoMonday.Select(c => c.HourlyWage));
            Assert.NotEqual(monday.Select(c => c.Name), HiringPool.Applicants(100).Select(c => c.Name));
        }

        [Fact]
        public void ADearHireCanDisappointAndACheapOneCanBeAFind()
        {
            // Across a decent sample both directions must occur, or the wage is just skill
            // wearing a different hat and there is no risk in hiring at all.
            var pool = HiringPool.Applicants(7, 40);

            Assert.Contains(pool, c => c.Accept().Skill < c.Advertises - 0.05m);
            Assert.Contains(pool, c => c.Accept().Skill > c.Advertises + 0.05m);
        }

        [Fact]
        public void ACandidateReadsAsWordsRatherThanANumber()
        {
            Assert.All(HiringPool.Applicants(3, 20), c => Assert.False(string.IsNullOrWhiteSpace(c.Reads)));
        }

        // ---- What skill actually does ----

        [Fact]
        public void AStrongBrigadeWorksMoreOfThePassThanAWeakOne()
        {
            var poor = Build(out _, cookSkill: 0.15m);
            var strong = Build(out _, cookSkill: 0.95m);

            Assert.True(strong.Payroll.PlateCapacity(KitchenPass.PlatesPerCook)
                      > poor.Payroll.PlateCapacity(KitchenPass.PlatesPerCook));

            // Bodies still matter — a great cook is worth more than a poor one, never two.
            Assert.True(strong.Payroll.PlateCapacity(KitchenPass.PlatesPerCook)
                      < poor.Payroll.PlateCapacity(KitchenPass.PlatesPerCook) * 2);
        }

        [Fact]
        public void AnAverageBrigadeChangesNothing()
        {
            // 0.5 is the neutral point on purpose, so consulting skill did not silently
            // rebalance every restaurant that had not chosen its staff.
            var average = Build(out _, cookSkill: 0.5m, cooks: 3);

            Assert.Equal(3 * KitchenPass.PlatesPerCook,
                         average.Payroll.PlateCapacity(KitchenPass.PlatesPerCook));
            Assert.Equal(0.5m, average.Payroll.AverageSkill(StaffRole.Cook));
        }

        [Fact]
        public void AGoodCookElevatesWhatTheyAreGiven_AndAPoorOneWastesIt()
        {
            const decimal midMarketStock = 0.6m;

            var wasted = SatisfactionModel.PlateQuality(midMarketStock, 0.1m);
            var neutral = SatisfactionModel.PlateQuality(midMarketStock, 0.5m);
            var elevated = SatisfactionModel.PlateQuality(midMarketStock, 0.95m);

            Assert.True(wasted < neutral);
            Assert.True(elevated > neutral);
            Assert.Equal(midMarketStock, neutral);   // average cooking is exactly what you bought

            // And the pointed case: a strong kitchen on mid stock beats a weak one on the best
            // money can buy. Buying premium and staffing badly is an expensive mediocre dinner.
            Assert.True(SatisfactionModel.PlateQuality(0.6m, 0.95m)
                      > SatisfactionModel.PlateQuality(1.0m, 0.1m));
        }

        [Fact]
        public void ABetterServerHoldsMoreTables()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("a", "A", definitions, 100000m);

            var weak = company.OpenRestaurant("w", "Weak", LocationType.BrickAndMortar);
            weak.BuyTables("t", "Tables", 5000m, 60);
            weak.Payroll.Hire(new Employee("s", "Server", StaffRole.Server, 10m, 0.1m));

            var strong = company.OpenRestaurant("s", "Strong", LocationType.BrickAndMortar);
            strong.BuyTables("t", "Tables", 5000m, 60);
            strong.Payroll.Hire(new Employee("s", "Server", StaffRole.Server, 18m, 0.95m));

            Assert.True(strong.ServableSeats > weak.ServableSeats);
        }

        [Fact]
        public void HiringWellShowsUpInTheNight()
        {
            // The same restaurant, same equipment, same ingredients — staffed differently.
            var poor = Build(out _, cookSkill: 0.15m);
            var strong = Build(out _, cookSkill: 0.95m);

            var poorNight = Dinner.Run(poor, 30, 99);
            var strongNight = Dinner.Run(strong, 30, 99);

            // Quality always shows, because every plate passes through their hands.
            Assert.True(strongNight.AverageSatisfaction > poorNight.AverageSatisfaction);

            // Covers deliberately NOT asserted here. This fixture has four cooks against
            // thirty-two covers, so the kitchen is not what is holding it back — the room is,
            // and the night shows nought walkouts and nought wait-balks either way. Extra
            // plate capacity cannot serve people who have nowhere to sit, and demanding that
            // it does would be asserting something the model correctly refuses to do.
            Assert.Equal(0, strongNight.PartiesPutOffByTheWait);
        }

        [Fact]
        public void WhenTheKitchenIsTheBottleneck_ABetterBrigadeServesMorePeople()
        {
            // One cook against thirty-two covers, so the pass is genuinely the constraint.
            var poor = Build(out _, cookSkill: 0.15m, cooks: 1);
            var strong = Build(out _, cookSkill: 0.95m, cooks: 1);

            var poorNight = Dinner.Run(poor, 30, 99);
            var strongNight = Dinner.Run(strong, 30, 99);

            // Measured: 44 covers against 73, and wait-balks falling from 46 to 28. One
            // person, same wage line, two-thirds more trade.
            Assert.True(strongNight.CoversServed > poorNight.CoversServed * 1.3m);
            Assert.True(strongNight.PartiesPutOffByTheWait < poorNight.PartiesPutOffByTheWait);
        }

        // ---- People learn (Aaron) ----

        [Fact]
        public void AGreenHireGetsBetterWithHoursOnThePass()
        {
            // Aaron: "cheap labor can also be good, like have high potential to learn but
            // start off not great."
            var green = new Employee("g", "Green", StaffRole.Cook, 13m, skill: 0.25m, potential: 0.85m);

            Assert.True(green.StillLearning);
            var started = green.Skill;

            green.Worked(20000);   // a season or so of steady trade

            Assert.True(green.Skill > started + 0.2m, "grew only to " + green.Skill);
            Assert.True(green.Skill <= green.Potential);
        }

        [Fact]
        public void NobodyLearnsPastWhatTheyCouldBecome()
        {
            var capped = new Employee("c", "Capped", StaffRole.Cook, 14m, skill: 0.4m, potential: 0.5m);

            capped.Worked(1000000);

            Assert.Equal(0.5m, decimal.Round(capped.Skill, 2));
            Assert.False(capped.StillLearning);
        }

        [Fact]
        public void SomeoneAlreadyAtTheirCeilingDoesNotImprove()
        {
            // Which is what makes a dear, finished hire a different bet from a cheap, green
            // one: you are buying output now against output later.
            var finished = new Employee("f", "Finished", StaffRole.Cook, 27m, skill: 0.9m);

            finished.Worked(50000);

            Assert.Equal(0.9m, finished.Skill);
            Assert.False(finished.StillLearning);
        }

        [Fact]
        public void PotentialIsNeverBelowWhatSomeoneCanAlreadyDo()
        {
            var person = new Employee("p", "P", StaffRole.Cook, 15m, skill: 0.7m, potential: 0.3m);
            Assert.Equal(0.7m, person.Potential);
        }

        [Fact]
        public void ABusyRestaurantTrainsPeopleFasterThanAQuietOne()
        {
            var busy = new Employee("b", "Busy", StaffRole.Cook, 13m, skill: 0.3m, potential: 0.9m);
            var quiet = new Employee("q", "Quiet", StaffRole.Cook, 13m, skill: 0.3m, potential: 0.9m);

            busy.Worked(10000);
            quiet.Worked(1000);

            Assert.True(busy.Skill > quiet.Skill);
        }

        // ---- And they have to survive a save ----

        [Fact]
        public void ThePayrollSurvivesSavingAndLoading()
        {
            // This was missing entirely: loading a save emptied the payroll, leaving a
            // restaurant with equipment nobody could work. It went unnoticed while a cook was
            // a wage and nothing else. Skill that grows makes people irreplaceable.
            var restaurant = Build(out var company, cookSkill: 0.4m, cooks: 2, servers: 1);
            restaurant.Payroll.Worked(5000);

            var before = restaurant.Payroll.Staff
                .Select(p => new { p.Id, p.Name, p.Skill, p.Potential, p.HourlyWage, p.Role }).ToList();

            var loaded = SaveGameSerializer.FromJson(
                SaveGameSerializer.ToJson(company, new GameClock()),
                JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory));

            var after = loaded.Company.GetRestaurant("flagship").Payroll.Staff;

            Assert.Equal(before.Count, after.Count);
            for (var i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i].Id, after[i].Id);
                Assert.Equal(before[i].Name, after[i].Name);
                Assert.Equal(before[i].Role, after[i].Role);
                Assert.Equal(before[i].HourlyWage, after[i].HourlyWage);
                Assert.Equal(before[i].Skill, after[i].Skill);       // what they have become
                Assert.Equal(before[i].Potential, after[i].Potential); // and what they still could
            }
        }

        [Fact]
        public void AGreenCandidateCanBeWorthTraining()
        {
            // Across a pool, somebody cheap has real headroom — otherwise "hire green and
            // teach them" is not a strategy, it is just a worse hire.
            var pool = HiringPool.Applicants(11, 40);

            Assert.Contains(pool, c =>
            {
                var person = c.Accept();
                return person.Skill < 0.45m && person.Potential > person.Skill + 0.15m;
            });
        }
    }
}
