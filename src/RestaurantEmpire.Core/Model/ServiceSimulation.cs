using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>What one service actually did. Every number here traces to a named cause.</summary>
    public sealed class ServiceResult
    {
        private readonly Dictionary<string, int> _unitsSold;

        internal ServiceResult(
            Dictionary<string, int> unitsSold, IList<Ticket> tickets, IList<string> diagnostics,
            decimal revenue, int partiesArrived, int partiesTurnedAway, int coversServed,
            int walkouts, int eightySixed, decimal averageSatisfaction,
            int longestWaitMinutes, string busiestStationId)
        {
            _unitsSold = unitsSold;
            Tickets = new List<Ticket>(tickets).AsReadOnly();
            Diagnostics = new List<string>(diagnostics).AsReadOnly();
            Revenue = revenue;
            PartiesArrived = partiesArrived;
            PartiesTurnedAway = partiesTurnedAway;
            CoversServed = coversServed;
            Walkouts = walkouts;
            EightySixed = eightySixed;
            AverageSatisfaction = averageSatisfaction;
            LongestWaitMinutes = longestWaitMinutes;
            BusiestStationId = busiestStationId;
        }

        /// <summary>
        /// Covers sold per dish. THIS is what feeds the Kasavana-Smith matrix — the menu
        /// now classifies itself against what the restaurant actually sold, rather than
        /// against numbers a test invented.
        /// </summary>
        public IReadOnlyDictionary<string, int> UnitsSoldByRecipeId { get { return _unitsSold; } }

        public IReadOnlyList<Ticket> Tickets { get; }

        /// <summary>The night's complaints and notes, each naming a specific cause.</summary>
        public IReadOnlyList<string> Diagnostics { get; }

        public decimal Revenue { get; }
        public int PartiesArrived { get; }

        /// <summary>Turned away at the door because the dining room was full.</summary>
        public int PartiesTurnedAway { get; }

        public int CoversServed { get; }

        /// <summary>Guests who gave up waiting. Lost revenue and, from M1, a reputation hit.</summary>
        public int Walkouts { get; }

        /// <summary>Dishes that couldn't be made — no station, or out of stock.</summary>
        public int EightySixed { get; }

        /// <summary>Mean satisfaction across served covers, 0 to 1.</summary>
        public decimal AverageSatisfaction { get; }

        public int LongestWaitMinutes { get; }

        /// <summary>The station that spent the most minutes cooking — the bottleneck candidate.</summary>
        public string BusiestStationId { get; }

        public int TotalUnitsSold
        {
            get
            {
                var total = 0;
                foreach (var pair in _unitsSold) total += pair.Value;

                return total;
            }
        }
    }

    /// <summary>
    /// Runs one service, headlessly.
    ///
    /// This is the connective tissue between Kitchen and Customers, and the point where M0
    /// stops being separate pieces of arithmetic and becomes a simulation: guests arrive on
    /// a curve, order, contend for stations, wait, and either enjoy it or don't — and the
    /// numbers that fall out are real outputs rather than test fixtures.
    ///
    /// Deliberately headless and deterministic. There is no rendering, no pacing, and no
    /// player input; the watchable Sims-style version of this is M1's job. Same seed, same
    /// night, every time.
    /// </summary>
    public static class ServiceSimulation
    {
        /// <summary>How long a party lingers after their food arrives, holding their table.</summary>
        public const int DwellMinutesAfterService = 35;

        public static ServiceResult Run(
            Restaurant restaurant, long serviceStartTick, int serviceMinutes,
            DemandModel demand, long seed)
        {
            if (restaurant == null) throw new ArgumentNullException(nameof(restaurant));
            if (demand == null) throw new ArgumentNullException(nameof(demand));
            if (restaurant.Menu.Count == 0)
                throw new InvalidOperationException("Cannot run a service: '" + restaurant.Name + "' has nothing on the menu.");

            var definitions = restaurant.Company.Definitions;
            var costing = restaurant.Costing;
            var pass = restaurant.Kitchen.OpenPass(serviceStartTick);
            var rng = new DeterministicRandom(seed);

            var unitsSold = new Dictionary<string, int>(StringComparer.Ordinal);
            var tickets = new List<Ticket>();
            var diagnostics = new List<string>();
            var stationMinutes = new Dictionary<string, int>(StringComparer.Ordinal);
            var seatFreeAt = new List<long>();   // one entry per occupied seat

            decimal revenue = 0m, satisfactionTotal = 0m;
            int partiesTurnedAway = 0, coversServed = 0, walkouts = 0, eightySixed = 0, longestWait = 0;

            var arrivals = demand.ArrivalsFor(serviceStartTick, serviceMinutes);

            foreach (var party in arrivals)
            {
                // Release tables that have finished.
                for (var i = seatFreeAt.Count - 1; i >= 0; i--)
                {
                    if (seatFreeAt[i] <= party.ArrivalTick) seatFreeAt.RemoveAt(i);
                }

                // The dining room is a hard cap, when one is set.
                if (restaurant.SeatingCapacity > 0 && seatFreeAt.Count + party.Size > restaurant.SeatingCapacity)
                {
                    partiesTurnedAway++;
                    diagnostics.Add("Turned away a party of " + party.Size + " — dining room full (" +
                                    restaurant.SeatingCapacity + " seats).");
                    continue;
                }

                var partyFreeAt = party.ArrivalTick;

                for (var cover = 0; cover < party.Size; cover++)
                {
                    var recipeId = restaurant.Menu.RecipeIds[rng.Next(restaurant.Menu.Count)];
                    var recipe = definitions.GetRecipe(recipeId);

                    var ticket = pass.Fire(recipe, party.ArrivalTick, restaurant.Inventory);
                    tickets.Add(ticket);

                    if (!ticket.WasServed)
                    {
                        eightySixed++;
                        diagnostics.Add(ticket.FailureReason);
                        continue;
                    }

                    int running;
                    stationMinutes.TryGetValue(ticket.StationId, out running);
                    stationMinutes[ticket.StationId] = running + ticket.CookMinutes;

                    if (ticket.WaitMinutes > longestWait) longestWait = ticket.WaitMinutes;
                    if (ticket.CompletedTick > partyFreeAt) partyFreeAt = ticket.CompletedTick;

                    var satisfaction = SatisfactionModel.Evaluate(
                        party, ticket, recipe.Name,
                        costing.IngredientQuality(recipeId),
                        costing.FoodCostRatio(recipeId));

                    if (satisfaction.WalkedOut)
                    {
                        walkouts++;
                        diagnostics.Add(satisfaction.Diagnosis);
                        continue;
                    }

                    // Paid for and eaten.
                    unitsSold.TryGetValue(recipeId, out running);
                    unitsSold[recipeId] = running + 1;

                    revenue += recipe.MenuPrice;
                    coversServed++;
                    satisfactionTotal += satisfaction.Overall;

                    if (satisfaction.Overall < 0.6m) diagnostics.Add(satisfaction.Diagnosis);
                }

                for (var seat = 0; seat < party.Size; seat++)
                    seatFreeAt.Add(partyFreeAt + DwellMinutesAfterService);
            }

            return new ServiceResult(
                unitsSold, tickets, diagnostics, revenue,
                arrivals.Count, partiesTurnedAway, coversServed, walkouts, eightySixed,
                coversServed == 0 ? 0m : satisfactionTotal / coversServed,
                longestWait, BusiestStation(stationMinutes));
        }

        private static string BusiestStation(Dictionary<string, int> stationMinutes)
        {
            string busiest = null;
            var most = -1;

            foreach (var pair in stationMinutes)
            {
                if (pair.Value > most)
                {
                    most = pair.Value;
                    busiest = pair.Key;
                }
            }

            return busiest;
        }
    }
}
