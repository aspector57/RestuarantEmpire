using System;
using System.Collections.Generic;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// ADDING CAPACITY MUST NEVER REDUCE OUTPUT.
    ///
    /// `tools/levers.js` measured the browser build going 2 cooks -> 50.6 covers/day,
    /// 3 -> 43.7, 6 -> 39.5, with walkouts climbing 34.8 -> 54.0. Hiring made the
    /// restaurant worse, which breaks Binding Principle 2 outright: the player takes an
    /// action, output falls, and nothing anywhere names a cause.
    ///
    /// The browser build's cause was a door quote that took `min(stations, cooks)` when a
    /// plate needs a free station AND a free cook — so the more hands you had, the blinder
    /// the host got, and parties who would have been turned away at the door were seated
    /// and then lost. A walkout is strictly worse than a balk: you cook the plate, bin it,
    /// hold the table for the wait, and take the reputation hit.
    ///
    /// This fixture is the engine-side control. It deliberately mirrors the levers.js
    /// baseline — suburban, one dinner service, 24 seats, two second-hand ovens carrying
    /// two of the three dishes — so the two builds can be compared rather than each
    /// trusted on its own. Two instruments that disagree is a bug report.
    /// </summary>
    public class BrigadeScalingTests
    {
        private readonly ITestOutputHelper _out;
        public BrigadeScalingTests(ITestOutputHelper o) { _out = o; }

        private static readonly string[] Card = { "margherita", "house-focaccia", "caprese-salad" };

        private sealed class Night
        {
            public int Cooks, Covers, Walkouts, BalkedWait, TurnedAway;
            public decimal Revenue;
        }

        /// <summary>
        /// One restaurant, staffed with <paramref name="cooks"/> cooks and otherwise
        /// identical every time. Same seed, same street, same kitchen.
        /// </summary>
        private static Night Trade(int cooks, int days, long seed)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("c", "C", definitions, 400000m);
            var r = company.OpenRestaurant("s", "Brigade Probe", LocationType.BrickAndMortar);

            r.Location = Neighborhood.SuburbanHighStreet();
            r.FloorArea = 3000m;                       // floor is not the variable
            company.SupplierPolicy.AssignAll("valley-produce");

            foreach (var id in Card) r.Menu.Add(id);

            r.ServiceWindows.Clear();
            r.ServiceWindows.Add(new ServiceWindow("Dinner", 18, 23));

            // Two second-hand ovens cooking two of the three dishes, and a garde-manger
            // pair for the third — the levers.js baseline, station for station.
            r.BuyEquipment(definitions.EquipmentFor("oven").First(x => x.Id == "oven-secondhand"), 2);
            r.BuyEquipment(definitions.EquipmentFor("garde-manger").First(x => x.Id == "gm-refrigerated"), 2);
            r.BuyEquipment(definitions.EquipmentFor("cold-storage").First(x => x.Id == "cold-walkin"), 1);
            r.BuyEquipment(definitions.EquipmentFor("dry-storage").First(x => x.Id == "dry-stockroom"), 1);

            r.BuyTables("t", "Tables", 24 * 120m, 24);

            for (var i = 0; i < cooks; i++) r.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m));
            for (var i = 0; i < 2; i++) r.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m));

            // Stock only what this card actually cooks. Don't stock what you don't cook —
            // three fixtures on this project have measured a famine or a bin fire instead
            // of a menu by getting that wrong.
            foreach (var id in r.Menu.Recipes.SelectMany(x => x.Ingredients).Select(x => x.IngredientId).Distinct())
            {
                r.Inventory.SetPar(id, 60m, 400m);
                r.Inventory.Receive(id, 150m);
            }

            // Established, so awareness is not the variable either.
            r.Reputation.Restore(0.5m, 12000);

            var clock = new GameClock();
            clock.AdvanceHours(18);

            var runner = new SimulationRunner(r, clock, seed, InterruptPolicy.None());
            runner.Advance((long)days * GameClock.TicksPerDay);

            var m = runner.Snapshot();

            return new Night
            {
                Cooks = cooks,
                Covers = m.CoversServed,
                Walkouts = m.Walkouts,
                BalkedWait = m.PartiesPutOffByTheWait,
                TurnedAway = m.PartiesTurnedAway,
                Revenue = m.Revenue
            };
        }

        /// <summary>
        /// THE RATCHET. Hiring one more cook must never reduce covers.
        ///
        /// Stated step-by-step rather than against the peak, because a lean brigade SHOULD
        /// serve fewer people — that is what makes hiring worth doing. What must never
        /// happen is the slide: paying for hands and getting less trade for it.
        ///
        /// And a tolerance, because the pass genuinely saturates. Past the point where the
        /// stations bind, another cook buys nothing real and seed noise moves covers a
        /// little either way. Anything past that is capacity being destroyed by adding
        /// capacity, which the player has no way to explain and no way to see coming.
        /// </summary>
        [Theory]
        [InlineData(20240802L)]
        [InlineData(11L)]
        [InlineData(90210L)]
        public void HiringAnotherCookNeverReducesCovers(long seed)
        {
            var runs = new[] { 1, 2, 3, 4, 6 }.Select(n => Trade(n, 20, seed)).ToList();

            foreach (var run in runs)
            {
                _out.WriteLine(string.Format("{0} cook(s): {1,5:N0} covers  {2,5:N0} walkouts  {3,5:N0} balked at the door",
                    run.Cooks, run.Covers, run.Walkouts, run.BalkedWait));
            }

            for (var i = 1; i < runs.Count; i++)
            {
                var before = runs[i - 1];
                var after = runs[i];

                Assert.True(after.Covers >= before.Covers * 0.96,
                    "Going from " + before.Cooks + " cooks to " + after.Cooks + " took covers from " +
                    before.Covers + " down to " + after.Covers +
                    " — the player paid more wages for less trade, and nothing names the cause.");
            }
        }

        /// <summary>
        /// MECHANISM 1: the quote and the fire must agree.
        ///
        /// The door quote used to be a second implementation of the scheduler — earliest
        /// free slot plus a guess at rounds — and it drifted from the real thing in the
        /// direction that hurt: the bigger the brigade, the more optimistic it got. This
        /// pins them together, which is the only reason the numbers above hold.
        /// </summary>
        [Fact]
        public void TheDoorQuoteIsWhatTheKitchenActuallyDoes()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("c", "C", definitions, 400000m);
            var r = company.OpenRestaurant("s", "Quote Probe", LocationType.BrickAndMortar);
            r.FloorArea = 3000m;

            // One oven and a brigade far bigger than it — the exact shape that used to make
            // the quote lie, because the hands stopped being the constraint.
            r.BuyEquipment(definitions.EquipmentFor("oven").First(x => x.Id == "oven-secondhand"), 1);

            var pass = r.Kitchen.OpenPass(0, plates: 12);
            var margherita = definitions.GetRecipe("margherita");

            // Bury the oven, so the queue is real rather than empty.
            for (var i = 0; i < 5; i++) pass.Fire(margherita, 0, null);

            const int partySize = 4;
            var quoted = pass.EstimatedWaitMinutes(margherita, 0, partySize);

            var actual = 0;
            for (var i = 0; i < partySize; i++)
            {
                var ticket = pass.Fire(margherita, 0, null);
                if (ticket.WaitMinutes > actual) actual = ticket.WaitMinutes;
            }

            _out.WriteLine("quoted " + quoted + " min, the kitchen took " + actual + " min");

            Assert.Equal(actual, quoted);
        }

        /// <summary>
        /// MECHANISM 2: a table that walks frees the queue behind it.
        ///
        /// Cooking for people who have left burns the scarcest thing in the building at the
        /// moment it is scarcest. That is a loop that feeds itself, and it is the other half
        /// of why hiring used to hurt. A plate already in the pan is NOT recovered — only
        /// the queue is refundable.
        /// </summary>
        [Fact]
        public void PlatesForATableThatWalkedComeOffTheBoard()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("c", "C", definitions, 400000m);
            var r = company.OpenRestaurant("s", "Abandon Probe", LocationType.BrickAndMortar);
            r.FloorArea = 3000m;

            r.BuyEquipment(definitions.EquipmentFor("oven").First(x => x.Id == "oven-secondhand"), 1);

            var pass = r.Kitchen.OpenPass(0, plates: 4);
            var margherita = definitions.GetRecipe("margherita");

            var giveUp = new List<Ticket>();
            for (var i = 0; i < 3; i++) giveUp.Add(pass.Fire(margherita, 0, null));

            var behind = pass.Fire(margherita, 0, null);
            var wasDue = behind.CompletedTick;

            // The first plate is in the pan at minute 1; the other two have not gone on.
            var dropped = pass.Abandon(giveUp, 1);

            _out.WriteLine(dropped + " plates came off; the one behind moved from " +
                           wasDue + " to " + behind.CompletedTick);

            Assert.Equal(2, dropped);
            Assert.True(behind.CompletedTick < wasDue,
                "The plate queued behind an abandoned table did not move up — the pass is still " +
                "cooking for people who left, which is the loop that makes hiring counterproductive.");

            // The plate already cooking keeps its place. You cannot un-cook it.
            Assert.True(giveUp[0].StartedTick <= 1);
        }

        [Fact(Skip = "Measuring instrument, not a test. Run by removing this Skip.")]
        public void SweepTheBrigade()
        {
            _out.WriteLine("COOKS, 20 days each, everything else fixed (24 seats, 2 second-hand ovens)");
            _out.WriteLine("cooks  covers/day  walkouts/day  balked@door  turnedAway  revenue/day");

            foreach (var n in new[] { 1, 2, 3, 4, 5, 6 })
            {
                var run = Trade(n, 20, 20240802L);
                _out.WriteLine(string.Format("{0,5} {1,11:N1} {2,13:N1} {3,12:N1} {4,11:N1} {5,12:N0}",
                    n, run.Covers / 20.0, run.Walkouts / 20.0, run.BalkedWait / 20.0,
                    run.TurnedAway / 20.0, run.Revenue / 20m));
            }
        }
    }
}
