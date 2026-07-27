using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// What the restaurant has done over some span of trading — a snapshot taken from
    /// <see cref="SimulationRunner.Snapshot"/>, not a thing that runs on its own.
    ///
    /// Every number here traces to a named cause; see <see cref="Diagnostics"/>.
    /// </summary>
    public sealed class ServiceResult
    {
        private readonly Dictionary<string, int> _unitsSold;

        internal ServiceResult(
            Dictionary<string, int> unitsSold, IList<Ticket> tickets, IList<string> diagnostics,
            decimal revenue, decimal foodCost, decimal wastedFoodCost,
            int partiesArrived, int partiesTurnedAway, int partiesLostToMenu, int partiesPutOffByTheWait,
            int partiesPutOffByThePrices, int coversServed,
            int walkouts, int eightySixed, decimal averageSatisfaction,
            int longestWaitMinutes, string busiestStationId)
        {
            PartiesLostToMenu = partiesLostToMenu;
            PartiesPutOffByTheWait = partiesPutOffByTheWait;
            PartiesPutOffByThePrices = partiesPutOffByThePrices;
            _unitsSold = unitsSold;
            Tickets = new List<Ticket>(tickets).AsReadOnly();
            Diagnostics = new List<string>(diagnostics).AsReadOnly();
            Revenue = revenue;
            FoodCost = foodCost;
            WastedFoodCost = wastedFoodCost;
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
        /// Covers sold per dish — what the Kasavana-Smith matrix classifies against, so the
        /// menu grades itself on real trading rather than on invented figures.
        /// </summary>
        public IReadOnlyDictionary<string, int> UnitsSoldByRecipeId { get { return _unitsSold; } }

        public IReadOnlyList<Ticket> Tickets { get; }

        /// <summary>The complaints and notes, each naming a specific cause.</summary>
        public IReadOnlyList<string> Diagnostics { get; }

        public decimal Revenue { get; }

        /// <summary>
        /// Ingredient cost of every plate the kitchen produced — including plates for guests
        /// who walked out before eating them. The food was bought and cooked either way.
        /// </summary>
        public decimal FoodCost { get; }

        /// <summary>
        /// The share of <see cref="FoodCost"/> that earned nothing because the guest had
        /// already left. What makes a walkout cost twice.
        /// </summary>
        public decimal WastedFoodCost { get; }

        public int PartiesArrived { get; }

        /// <summary>Turned away at the door because the dining room was full.</summary>
        public int PartiesTurnedAway { get; }

        /// <summary>
        /// Came in, read the menu, and left — because nothing on it suited the hour. The
        /// specific cost of opening a service you have no food for.
        /// </summary>
        public int PartiesLostToMenu { get; }

        /// <summary>
        /// Saw how backed up the room was and went elsewhere without sitting down. Lost
        /// trade, but far cheaper than a walkout: no food was cooked for them.
        /// </summary>
        public int PartiesPutOffByTheWait { get; }

        /// <summary>
        /// Read the menu, decided it was not worth the money, and left. The cost of
        /// overpricing — paid in trade you never see rather than in complaints.
        /// </summary>
        public int PartiesPutOffByThePrices { get; }

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
}
