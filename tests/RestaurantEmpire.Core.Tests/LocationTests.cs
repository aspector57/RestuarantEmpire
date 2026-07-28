using System;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Demand comes from WHERE the restaurant is, not from a number the player sets.
    ///
    /// The player picks the hours; the neighborhood decides whether anybody is out there.
    /// That asymmetry is the point — before this, every trade-off built on top of demand
    /// could be justified by simply declaring the traffic was there.
    /// </summary>
    public class LocationTests
    {
        private static Restaurant Build(Neighborhood where)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme", "Acme Restaurant Group", definitions, 200000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");
            restaurant.Location = where;

            restaurant.Kitchen.Install("oven", "Wood Oven", 6);
            restaurant.Kitchen.Install("garde-manger", "Garde Manger", 6);
            restaurant.Kitchen.Install("saute", "Saute", 6);

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 1000000m);

            return restaurant;
        }

        private static ServiceResult RunOneDay(Restaurant restaurant, params ServiceWindow[] windows)
        {
            restaurant.ServiceWindows.Clear();
            foreach (var window in windows) restaurant.ServiceWindows.Add(window);

            var runner = new SimulationRunner(restaurant, new GameClock(), 4242, InterruptPolicy.None());
            runner.AdvanceDays(1);

            return runner.Snapshot();
        }

        [Fact]
        public void ThePlayerCannotSimplyDeclareThatTheyAreBusy()
        {
            // There is no knob for demand anywhere on a restaurant or a service window.
            Assert.DoesNotContain(typeof(ServiceWindow).GetProperties(), p => p.Name.Contains("Parties"));
            Assert.DoesNotContain(typeof(ServiceWindow).GetProperties(), p => p.Name.Contains("Demand"));

            // The only lever is which neighborhood you are in.
            Assert.NotNull(typeof(Restaurant).GetProperty("Location"));
        }

        [Fact]
        public void StayingOpenLateInTheSuburbsIsPointless()
        {
            // Aaron's case exactly: some areas have nobody about after 10pm, so a late
            // service is labor and lighting spent on an empty room.
            var suburb = Neighborhood.SuburbanHighStreet();

            Assert.True(suburb.TrafficAtHour(20) > 15);   // a real dinner trade
            Assert.True(suburb.IsDeadAtHour(23));         // and then it stops
            Assert.True(suburb.IsDeadAtHour(1));

            var restaurant = Build(suburb);

            var dinnerOnly = RunOneDay(restaurant, new ServiceWindow("Dinner", 18, 22));
            var dinnerCovers = dinnerOnly.CoversServed;

            var withLateService = RunOneDay(Build(suburb),
                new ServiceWindow("Dinner", 18, 22), new ServiceWindow("Late", 22, 2));

            // Staying open four extra hours barely moves the needle, because there is
            // nobody out there to serve.
            var extra = withLateService.CoversServed - dinnerCovers;
            Assert.True(extra < dinnerCovers * 0.15,
                "late service in the suburbs added " + extra + " covers on top of " + dinnerCovers);
        }

        [Fact]
        public void TheSameLateServiceIsWorthwhileInANightlifeQuarter()
        {
            // The identical decision, somewhere else, is a good one.
            var nightlife = Neighborhood.NightlifeQuarter();

            Assert.True(nightlife.TrafficAtHour(23) > 20);
            Assert.True(nightlife.IsDeadAtHour(7));

            var dinnerOnly = RunOneDay(Build(nightlife), new ServiceWindow("Dinner", 18, 22));
            var withLate = RunOneDay(Build(nightlife),
                new ServiceWindow("Dinner", 18, 22), new ServiceWindow("Late", 22, 2));

            Assert.True(withLate.CoversServed > dinnerOnly.CoversServed * 1.5,
                "late service in a nightlife quarter should roughly transform the night");
        }

        [Fact]
        public void BreakfastPaysInABusinessDistrictAndNotInTheSuburbs()
        {
            var business = Neighborhood.BusinessDistrict();
            var suburb = Neighborhood.SuburbanHighStreet();

            Assert.True(business.TrafficAtHour(8) > 4 * suburb.TrafficAtHour(8));

            var breakfast = new ServiceWindow("Breakfast", 7, 10);

            var inTheCity = RunOneDay(Build(business), breakfast);
            var inTheSuburbs = RunOneDay(Build(suburb), breakfast);

            Assert.True(inTheCity.CoversServed > inTheSuburbs.CoversServed * 3);
        }

        [Fact]
        public void ABusinessDistrictDiesInTheEvening_WhereACityCenterDoesNot()
        {
            var business = Neighborhood.BusinessDistrict();
            var city = Neighborhood.CityCenter();

            Assert.True(business.TrafficAtHour(13) > city.TrafficAtHour(13));   // lunch is bigger
            Assert.True(city.TrafficAtHour(20) > business.TrafficAtHour(20) * 3); // dinner is not
        }

        [Fact]
        public void PotentialPartiesLetsYouCheckAServiceBeforeCommittingToIt()
        {
            // The player should be able to see a bad idea coming rather than discover it
            // after a month of paying staff to stand around.
            var suburb = Neighborhood.SuburbanHighStreet();

            var dinner = new ServiceWindow("Dinner", 18, 22);
            var late = new ServiceWindow("Late", 22, 2);

            Assert.True(dinner.PotentialPartiesIn(suburb) > 50);
            Assert.True(late.PotentialPartiesIn(suburb) < 8);
        }

        [Fact]
        public void TrafficRisesAndFallsSmoothly_RatherThanSwitchingAtTheTopOfTheHour()
        {
            var city = Neighborhood.CityCenter();
            var atNoon = city.TrafficAt(new DateTime(2026, 3, 2, 12, 0, 0));
            var halfPast = city.TrafficAt(new DateTime(2026, 3, 2, 12, 30, 0));
            var atOne = city.TrafficAt(new DateTime(2026, 3, 2, 13, 0, 0));

            Assert.True(halfPast > atNoon && halfPast < atOne);   // interpolated, so a rush builds
        }

        [Fact]
        public void ATrafficProfileNeedsAFullDay()
        {
            Assert.Throws<ArgumentException>(() => new Neighborhood("x", "X", new double[12]));
            Assert.Throws<ArgumentException>(() => new Neighborhood("x", "X", null));

            var negative = new double[24];
            negative[3] = -1;
            Assert.Throws<ArgumentException>(() => new Neighborhood("x", "X", negative));
        }
    }
}
