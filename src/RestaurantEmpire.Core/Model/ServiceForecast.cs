using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// What tonight SHOULD look like, worked out before it happens — and then, afterwards,
    /// how wrong that was and which part of the restaurant caused the gap.
    ///
    /// WHY THIS IS A MECHANIC AND NOT A REPORT. Every number in this game is currently
    /// discovered by advancing time and reading what happened. The player never commits to a
    /// belief, so they are never *wrong* in a way that teaches them anything — which is the
    /// difference between operating a restaurant and watching one. A forecast makes the player
    /// state what they expect, and the autopsy tells them which assumption broke.
    ///
    /// Aaron, on what he liked about the parallel implementation's harness: *"I like that I can
    /// quickly play through, get results and feedback given to me, and then I can give that
    /// feedback to you."* That loop is what makes the thing legible. This is the same loop
    /// moved inside the game, where it belongs.
    ///
    /// IT IS DELIBERATELY A PROJECTION, NOT A PREDICTION. It cannot know the dice, and it does
    /// not try — it takes the expected value of every roll and the hard capacity ceilings, and
    /// reports the result. **The gap between this and the night is the information**, so a
    /// forecast that were always right would have nothing to say.
    ///
    /// It reads its inputs from the same properties the simulation reads (traffic, reputation,
    /// menu draw, the price gate, seats, plate capacity) precisely so the two cannot disagree
    /// about the model. Where the simulation rolls, this multiplies by the probability.
    /// </summary>
    public sealed class ServiceForecast
    {
        /// <summary>
        /// Party sizes are 1/2/3/4/5-7 at 15/45/20/15/5 percent, which averages to this.
        /// Kept here rather than recomputed so the forecast and `RollPartySize` cannot drift.
        /// </summary>
        public const decimal AveragePartySize = Tuning.AveragePartySize;

        /// <summary>
        /// The share of a kitchen's theoretical throughput a real service actually gets.
        ///
        /// A pass running flat out has an unbounded queue in front of it — guests arrive in
        /// clumps, not on a metronome, so waits build long before utilisation reaches 100% and
        /// people balk. This is the standard "practical capacity" haircut, and it is a real
        /// property of queues rather than a fudge factor: without it the forecast promises
        /// covers that only a perfectly smoothed arrival stream could deliver.
        /// </summary>
        public const decimal PracticalCapacity = Tuning.PracticalCapacity;

        private ServiceForecast(
            decimal covers, decimal revenue, decimal foodCost, decimal laborCost,
            int seatCeiling, int kitchenCeiling, decimal demand, string constraint)
        {
            Covers = covers;
            Revenue = revenue;
            FoodCost = foodCost;
            LaborCost = laborCost;
            SeatCeiling = seatCeiling;
            KitchenCeiling = kitchenCeiling;
            DemandCovers = demand;
            Constraint = constraint;
        }

        /// <summary>Covers we expect to serve, after every ceiling has been applied.</summary>
        public decimal Covers { get; }

        public decimal Revenue { get; }
        public decimal FoodCost { get; }
        public decimal LaborCost { get; }

        /// <summary>Takings less what the food and the staff cost. Rent is not in here.</summary>
        public decimal GrossProfit { get { return Revenue - FoodCost - LaborCost; } }

        /// <summary>Covers the street would give us if nothing at all got in the way.</summary>
        public decimal DemandCovers { get; }

        /// <summary>Most covers the dining room could turn in these hours.</summary>
        public int SeatCeiling { get; }

        /// <summary>Most plates the brigade could send in these hours.</summary>
        public int KitchenCeiling { get; }

        /// <summary>
        /// Which of the three is actually binding — "demand", "seats" or "kitchen". This is
        /// the single most useful line in the forecast, because the three have entirely
        /// different fixes and buying the wrong one is the mistake Aaron made playing.
        /// </summary>
        public string Constraint { get; }

        /// <summary>Said in the way an operator would say it, not as a table of figures.</summary>
        public string Reads
        {
            get
            {
                var covers = Math.Round(Covers);
                var money = Math.Round(GrossProfit);
                var verb = money >= 0 ? "clearing" : "losing";
                var amount = Math.Abs(money).ToString("N0");

                var because =
                    Constraint == "seats" ? "the dining room is the limit — we could sell more than we can seat"
                    : Constraint == "kitchen" ? "the pass is the limit — we could seat more than we can cook"
                    : "the street is the limit — we have room and hands to spare";

                return "About " + covers.ToString("N0") + " covers, " + verb + " $" + amount + ". " + because + ".";
            }
        }

        /// <summary>
        /// Work out the day ahead. <paramref name="on"/> sets which hours are being forecast;
        /// only the date part matters.
        /// </summary>
        public static ServiceForecast ForDay(Restaurant restaurant, DateTime on)
        {
            if (restaurant == null) throw new ArgumentNullException(nameof(restaurant));

            var day = on.Date;
            var pricePosition = restaurant.Costing.PricePosition(restaurant.Menu.RecipeIds);
            var standing = restaurant.Reputation.Standing;

            var plates = restaurant.Payroll.PlateCapacity(KitchenPass.PlatesPerCook);
            var prepMinutes = AveragePrepMinutes(restaurant);

            var openMinutes = 0;
            var demandCovers = 0m;
            var seatCeiling = 0;
            var kitchenCeiling = 0;
            var covers = 0m;

            var lostToSeats = 0m;
            var lostToKitchen = 0m;

            // WORKED HOUR BY HOUR, and that is the whole accuracy of this thing. A restaurant
            // cannot bank a quiet six o'clock and spend it at eight — the peak is where a
            // kitchen jams and people leave, and a ceiling summed across the whole night
            // hides exactly that. Measured: the flat version predicted 202 covers against 75
            // served, because it believed a pass with 378 plates of nightly throughput could
            // absorb a dinner rush it could not.
            for (var hour = 0; hour < 24; hour++)
            {
                var minutesThisHour = 0;
                var partiesThisHour = 0m;
                var gateThisHour = 0m;
                var gateSamples = 0;

                for (var minute = 0; minute < 60; minute++)
                {
                    var now = day.AddHours(hour).AddMinutes(minute);
                    if (!IsOpen(restaurant, now)) continue;

                    minutesThisHour++;

                    // The same three factors the simulation multiplies, then the expected
                    // value of the roll rather than the roll itself.
                    var footfall = restaurant.TrafficAt(now)
                                 * (double)restaurant.Reputation.TrafficMultiplier
                                 * (double)MenuDraw(restaurant, now);

                    var chance = footfall / 60.0;
                    if (chance > 1.0) chance = 1.0;
                    partiesThisHour += (decimal)chance;

                    if (minute % 30 == 0)
                    {
                        gateThisHour += ConsiderRate(restaurant, now, pricePosition, standing);
                        gateSamples++;
                    }
                }

                if (minutesThisHour == 0) continue;
                openMinutes += minutesThisHour;

                var gate = gateSamples == 0 ? 1m : gateThisHour / gateSamples;
                var wanted = partiesThisHour * gate * AveragePartySize;
                demandCovers += wanted;

                // A table is held for the whole sitting, so the room turns a finite number
                // of times an hour.
                var seatsThisHour = restaurant.SeatingCapacity * ((decimal)minutesThisHour / SimulationRunner.DwellMinutes);

                // And the pass can only send so many plates in that time.
                var kitchenThisHour = KitchenThroughput(restaurant, minutesThisHour, plates, prepMinutes);

                seatCeiling += (int)Math.Floor(seatsThisHour);
                kitchenCeiling += (int)Math.Floor(kitchenThisHour);

                var served = wanted;
                if (seatsThisHour < served) { lostToSeats += served - seatsThisHour; served = seatsThisHour; }
                if (kitchenThisHour < served) { lostToKitchen += served - kitchenThisHour; served = kitchenThisHour; }

                covers += served;
            }

            if (openMinutes == 0)
                return new ServiceForecast(0m, 0m, 0m, 0m, 0, 0, 0m, "closed");

            var constraint = "demand";
            if (lostToSeats > 0m || lostToKitchen > 0m)
                constraint = lostToKitchen > lostToSeats ? "kitchen" : "seats";

            var revenue = covers * AverageSpendPerCover(restaurant);
            var food = covers * AverageFoodCostPerCover(restaurant);
            var labor = restaurant.Payroll.HourlyWageBill * ((decimal)openMinutes / 60m);

            return new ServiceForecast(
                covers, revenue, food, labor, seatCeiling, kitchenCeiling, demandCovers, constraint);
        }

        /// <summary>
        /// Plates this kitchen can actually send in a stretch of minutes — governed by the
        /// TIGHTEST STATION, not by the brigade.
        ///
        /// Counting cooks alone was the forecast's biggest error: it predicted 190 covers
        /// against 75 served, because three oven units cooking two of the three dishes on the
        /// card is a far harder limit than four cooks. A station is a physical object with a
        /// queue in front of it, and the pass moves at the speed of whichever one has the
        /// longest one. This is also exactly the mistake Aaron made playing — buying ovens
        /// past the point where they were the constraint.
        /// </summary>
        private static decimal KitchenThroughput(
            Restaurant restaurant, int minutes, int plates, decimal prepMinutes)
        {
            if (plates <= 0 || prepMinutes <= 0m || restaurant.Menu.Count == 0) return 0m;

            var complexity = restaurant.Menu.ComplexityLoad;
            var tightest = decimal.MaxValue;

            // Group the card by station: each one gets the share of tickets its dishes take,
            // so a station cooking most of the menu needs proportionally more throughput.
            foreach (var station in restaurant.Kitchen.Stations)
            {
                var dishesHere = 0;
                var minutesHere = 0m;

                foreach (var recipe in restaurant.Menu.Recipes)
                {
                    if (recipe.StationId != station.Id) continue;
                    dishesHere++;
                    minutesHere += station.MinutesFor(recipe, complexity);
                }

                if (dishesHere == 0) continue;

                var share = (decimal)dishesHere / restaurant.Menu.Count;
                var averageHere = minutesHere / dishesHere;

                // Plates this station could send in the time, scaled up by the share of the
                // menu it is NOT responsible for — the whole service can only be as big as
                // this station's output divided by its share of it.
                var throughputHere = (decimal)minutes / averageHere * station.ConcurrentCapacity;
                var serviceCeiling = throughputHere / share;

                if (serviceCeiling < tightest) tightest = serviceCeiling;
            }

            if (tightest == decimal.MaxValue) return 0m;

            // The brigade is the other half — a station nobody is working sends nothing.
            var byBrigade = (decimal)minutes / prepMinutes * plates;
            var limit = tightest < byBrigade ? tightest : byBrigade;

            return limit * PracticalCapacity;
        }

        private static bool IsOpen(Restaurant restaurant, DateTime now)
        {
            for (var i = 0; i < restaurant.ServiceWindows.Count; i++)
                if (restaurant.ServiceWindows[i].IsOpenAt(now)) return true;

            return false;
        }

        private static decimal MenuDraw(Restaurant restaurant, DateTime now)
        {
            if (restaurant.Menu.Count == 0) return 1m;

            var likely = ArchetypeProfile.LikelyAt(Dayparts.At(now),
                restaurant.Location == null ? "" : restaurant.Location.Id);
            if (likely.Length == 0) return 1m;

            var total = 0m;
            for (var i = 0; i < likely.Length; i++) total += restaurant.Menu.AppealTo(likely[i]);

            return 0.55m + ((total / likely.Length) * 0.45m);
        }

        /// <summary>
        /// The share of the crowd that would come at these prices — weighted by menu appeal,
        /// because appeal is what decides which of them turns up in the first place.
        /// </summary>
        private static decimal ConsiderRate(
            Restaurant restaurant, DateTime now, decimal pricePosition, decimal standing)
        {
            var likely = ArchetypeProfile.LikelyAt(Dayparts.At(now),
                restaurant.Location == null ? "" : restaurant.Location.Id);
            if (likely.Length == 0) return 1m;

            var weighted = 0m;
            var weight = 0m;

            for (var i = 0; i < likely.Length; i++)
            {
                var w = restaurant.Menu.Count == 0 ? 1m : restaurant.Menu.AppealTo(likely[i]);
                weighted += ArchetypeProfile.For(likely[i]).WouldConsider(pricePosition, standing) * w;
                weight += w;
            }

            return weight <= 0m ? 1m : weighted / weight;
        }

        private static decimal AveragePrepMinutes(Restaurant restaurant)
        {
            var total = 0m;
            var counted = 0;

            foreach (var recipe in restaurant.Menu.Recipes) { total += recipe.PrepMinutes; counted++; }
            if (counted == 0) return 0m;

            return (total / counted) * restaurant.Menu.ComplexityLoad;
        }

        private static decimal AverageSpendPerCover(Restaurant restaurant)
        {
            var total = 0m;
            var counted = 0;

            foreach (var id in restaurant.Menu.RecipeIds) { total += restaurant.Costing.MenuPrice(id); counted++; }

            return counted == 0 ? 0m : total / counted;
        }

        private static decimal AverageFoodCostPerCover(Restaurant restaurant)
        {
            var total = 0m;
            var counted = 0;

            foreach (var id in restaurant.Menu.RecipeIds) { total += restaurant.Costing.PlateCost(id); counted++; }

            return counted == 0 ? 0m : total / counted;
        }
    }

    /// <summary>
    /// What the night actually did against what you thought it would do, and — the part that
    /// matters — WHICH ASSUMPTION BROKE.
    ///
    /// A variance table on its own is just two columns of numbers. The value is in naming the
    /// single largest cause in words the player can act on, which is the same standard the
    /// Advisor is held to: never "your covers were down 34%", always "the pass could not keep
    /// up and people left waiting".
    /// </summary>
    public sealed class ServiceAutopsy
    {
        private readonly List<string> _surprises = new List<string>();

        public ServiceAutopsy(ServiceForecast forecast, ServiceResult actual)
        {
            if (forecast == null) throw new ArgumentNullException(nameof(forecast));
            if (actual == null) throw new ArgumentNullException(nameof(actual));

            Forecast = forecast;
            Actual = actual;

            CoverVariance = actual.CoversServed - forecast.Covers;
            ProfitVariance = (actual.Revenue - actual.FoodCost - actual.LaborCost) - forecast.GrossProfit;

            Explain();
        }

        public ServiceForecast Forecast { get; }
        public ServiceResult Actual { get; }

        /// <summary>Covers over (positive) or under (negative) what was expected.</summary>
        public decimal CoverVariance { get; }
        public decimal ProfitVariance { get; }

        /// <summary>How far off the covers were, as a share. 0.1 is ten percent out.</summary>
        public decimal CoverError
        {
            get
            {
                if (Forecast.Covers <= 0m) return Actual.CoversServed > 0 ? 1m : 0m;
                return Math.Abs(CoverVariance) / Forecast.Covers;
            }
        }

        /// <summary>True when the night landed close enough that nothing needs explaining.</summary>
        public bool AsExpected { get { return CoverError < 0.12m; } }

        /// <summary>
        /// What went differently, biggest cause first. Empty when the night went to plan —
        /// an autopsy that always has something to say stops being read, the same reason the
        /// Advisor is allowed to stay quiet.
        /// </summary>
        public IReadOnlyList<string> Surprises { get { return _surprises; } }

        /// <summary>One line, for a header. Never a bare percentage.</summary>
        public string Headline
        {
            get
            {
                if (AsExpected) return "The night went roughly to plan.";

                var direction = CoverVariance < 0 ? "quieter" : "busier";
                var by = Math.Round(Math.Abs(CoverVariance)).ToString("N0");
                var lead = _surprises.Count > 0 ? " " + _surprises[0] : "";

                return by + " covers " + direction + " than expected." + lead;
            }
        }

        private void Explain()
        {
            // Ordered by how much trade each one actually cost, so the biggest hole is named
            // first rather than whichever check happens to run first.
            var causes = new List<KeyValuePair<int, string>>();

            if (Actual.PartiesPutOffByThePrices > 0)
                causes.Add(new KeyValuePair<int, string>(
                    Actual.PartiesPutOffByThePrices,
                    Actual.PartiesPutOffByThePrices + " parties decided against us on price before setting off."));

            if (Actual.PartiesPutOffByTheWait > 0)
                causes.Add(new KeyValuePair<int, string>(
                    Actual.PartiesPutOffByTheWait,
                    Actual.PartiesPutOffByTheWait + " parties saw the wait and went elsewhere — the pass could not keep up."));

            if (Actual.PartiesTurnedAway > 0)
                causes.Add(new KeyValuePair<int, string>(
                    Actual.PartiesTurnedAway,
                    Actual.PartiesTurnedAway + " parties were turned away with nowhere to sit."));

            if (Actual.PartiesLostToMenu > 0)
                causes.Add(new KeyValuePair<int, string>(
                    Actual.PartiesLostToMenu,
                    Actual.PartiesLostToMenu + " parties found nothing on the card they wanted at that hour."));

            if (Actual.Walkouts > 0)
                causes.Add(new KeyValuePair<int, string>(
                    Actual.Walkouts,
                    Actual.Walkouts + " walked out after sitting down, which is a cover paid for and lost."));

            if (Actual.EightySixed > 0)
                causes.Add(new KeyValuePair<int, string>(
                    Actual.EightySixed,
                    Actual.EightySixed + " orders were 86'd — we ran out of something mid-service."));

            causes.Sort((a, b) => b.Key.CompareTo(a.Key));

            // Only worth saying if the night was genuinely off. A busy night still turns some
            // people away, and reciting that every time is noise.
            if (AsExpected) return;

            foreach (var cause in causes) _surprises.Add(cause.Value);

            if (_surprises.Count == 0 && CoverVariance > 0)
                _surprises.Add("Simply a better night than the street usually gives.");
        }
    }
}
