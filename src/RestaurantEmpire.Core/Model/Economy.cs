using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>What a ledger entry is. Categories exist so prime cost can never be fudged.</summary>
    public enum LedgerCategory
    {
        /// <summary>Money taken from guests.</summary>
        Revenue = 0,

        /// <summary>Cost of ingredients actually used. The "food" half of prime cost.</summary>
        FoodCost = 1,

        /// <summary>Wages. The "labor" half of prime cost.</summary>
        LaborCost = 2,

        /// <summary>Rent, utilities, insurance — real but not controllable week to week.</summary>
        Overhead = 3,

        /// <summary>Build-out, equipment, opening a location. Not an operating cost.</summary>
        CapitalExpenditure = 4,

        /// <summary>Owner's money in, or a starting balance.</summary>
        CapitalContribution = 5
    }

    /// <summary>One line in the books. Immutable — the ledger is append-only.</summary>
    public sealed class LedgerEntry
    {
        internal LedgerEntry(long tick, LedgerCategory category, decimal amount, string description, string restaurantId)
        {
            Tick = tick;
            Category = category;
            Amount = amount;
            Description = description;
            RestaurantId = restaurantId;
        }

        public long Tick { get; }
        public LedgerCategory Category { get; }

        /// <summary>Always a positive magnitude. Direction comes from <see cref="Category"/>.</summary>
        public decimal Amount { get; }

        /// <summary>Plain-language reason, so any figure can be drilled into.</summary>
        public string Description { get; }

        /// <summary>Which location this belongs to, or null for company-level entries.</summary>
        public string RestaurantId { get; }

        public bool IsIncome
        {
            get { return Category == LedgerCategory.Revenue || Category == LedgerCategory.CapitalContribution; }
        }

        /// <summary>Signed effect on cash.</summary>
        public decimal CashEffect { get { return IsIncome ? Amount : -Amount; } }

        public override string ToString()
        {
            return Category + " " + (IsIncome ? "+" : "-") +
                   Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                   " — " + Description;
        }
    }

    /// <summary>
    /// How a prime cost figure reads against the real industry bands (design doc Phase 2:
    /// 55-60% quick service, 60-65% casual, up to 68% fine dining).
    /// </summary>
    public enum PrimeCostBand
    {
        /// <summary>No revenue in the period — nothing to judge.</summary>
        NoData = 0,

        /// <summary>Under 55%. Better than most real operators manage.</summary>
        Excellent = 1,

        /// <summary>55-65%. Where a healthy restaurant lives.</summary>
        Healthy = 2,

        /// <summary>65-70%. Survivable for fine dining, uncomfortable otherwise.</summary>
        Tight = 3,

        /// <summary>Over 70%. The business is losing on every cover.</summary>
        Unsustainable = 4
    }

    /// <summary>
    /// A period's P&amp;L, computed from ledger entries. Holds no state of its own — ask the
    /// Economy for a new one whenever you need current figures.
    /// </summary>
    public sealed class FinancialSummary
    {
        internal FinancialSummary(
            long fromTick, long toTick, string restaurantId,
            decimal revenue, decimal foodCost, decimal laborCost,
            decimal overhead, decimal capitalExpenditure, int entryCount)
        {
            FromTick = fromTick;
            ToTick = toTick;
            RestaurantId = restaurantId;
            Revenue = revenue;
            FoodCost = foodCost;
            LaborCost = laborCost;
            Overhead = overhead;
            CapitalExpenditure = capitalExpenditure;
            EntryCount = entryCount;
        }

        public long FromTick { get; }
        public long ToTick { get; }

        /// <summary>Null when this summary covers the whole company.</summary>
        public string RestaurantId { get; }

        public decimal Revenue { get; }
        public decimal FoodCost { get; }
        public decimal LaborCost { get; }
        public decimal Overhead { get; }
        public decimal CapitalExpenditure { get; }
        public int EntryCount { get; }

        /// <summary>Food plus labor. The one number the industry watches hardest.</summary>
        public decimal PrimeCost { get { return FoodCost + LaborCost; } }

        /// <summary>
        /// Prime cost as a share of revenue — the headline health metric the design says
        /// should always be visible. Unlike rent, it is controllable week to week, which is
        /// exactly why it earns that place.
        /// </summary>
        public decimal PrimeCostRatio { get { return Revenue == 0m ? 0m : PrimeCost / Revenue; } }

        public decimal FoodCostRatio { get { return Revenue == 0m ? 0m : FoodCost / Revenue; } }
        public decimal LaborCostRatio { get { return Revenue == 0m ? 0m : LaborCost / Revenue; } }

        /// <summary>Operating profit. Capital expenditure is deliberately excluded.</summary>
        public decimal NetProfit { get { return Revenue - PrimeCost - Overhead; } }

        public PrimeCostBand Band
        {
            get
            {
                if (Revenue == 0m) return PrimeCostBand.NoData;

                var ratio = PrimeCostRatio;
                if (ratio < 0.55m) return PrimeCostBand.Excellent;
                if (ratio <= 0.65m) return PrimeCostBand.Healthy;
                if (ratio <= 0.70m) return PrimeCostBand.Tight;

                return PrimeCostBand.Unsustainable;
            }
        }

        public override string ToString()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return "revenue " + Revenue.ToString("0.00", ci) +
                   ", prime cost " + (PrimeCostRatio * 100m).ToString("0.0", ci) + "% (" + Band + ")" +
                   ", net " + NetProfit.ToString("0.00", ci);
        }
    }

    /// <summary>
    /// The company's books: cash on hand and an append-only ledger.
    ///
    /// Lives on the Company rather than on each Restaurant, which is the Phase 9
    /// requirement made real — entries carry a restaurant id, so the same ledger answers
    /// both "how did the flagship do?" and "how did the group do?" without a second system.
    /// That is the rollup layer corporate ownership needs, present from the start rather
    /// than migrated in later.
    ///
    /// SCOPE, per the design doc's explicit narrowing: this is the player's own P&amp;L, not
    /// a macroeconomy. There is no modelled inflation, no interest-rate cycle, no
    /// market-wide boom or bust. Every swing here must trace back to a decision the player
    /// made — a supplier switch, a staffing call, a price change — never to invisible
    /// background noise. Loans arrive at M3.
    /// </summary>
    public sealed class Economy
    {
        private readonly List<LedgerEntry> _entries;

        internal Economy(decimal openingCash)
        {
            _entries = new List<LedgerEntry>();
            CashOnHand = 0m;

            if (openingCash != 0m)
                Record(0, LedgerCategory.CapitalContribution, openingCash, "Opening balance", null);
        }

        /// <summary>Money available right now. This is also the capital an expansion is gated on.</summary>
        public decimal CashOnHand { get; private set; }

        public IReadOnlyList<LedgerEntry> Entries { get { return _entries; } }

        /// <summary>True once the business has spent money it does not have.</summary>
        public bool IsInsolvent { get { return CashOnHand < 0m; } }

        /// <summary>
        /// Appends one line and moves cash. Amount must be a positive magnitude; whether it
        /// adds or subtracts is decided by the category, so a cost can never be booked as
        /// income by accident.
        /// </summary>
        public LedgerEntry Record(long tick, LedgerCategory category, decimal amount, string description, string restaurantId = null)
        {
            if (amount < 0m)
                throw new ArgumentOutOfRangeException(nameof(amount), "Ledger amounts are positive magnitudes; the category decides direction.");

            var entry = new LedgerEntry(tick, category, amount, description ?? category.ToString(), restaurantId);

            _entries.Add(entry);
            CashOnHand += entry.CashEffect;

            return entry;
        }

        /// <summary>
        /// Books a finished service: revenue in, food cost out, both tagged to the location.
        ///
        /// Note the food cost includes plates cooked for guests who walked out. Wasting food
        /// on someone who left is a real cost, and hiding it would let prime cost lie —
        /// which the design explicitly forbids.
        /// </summary>
        public void RecordService(Restaurant restaurant, ServiceResult result, long tick)
        {
            if (restaurant == null) throw new ArgumentNullException(nameof(restaurant));
            if (result == null) throw new ArgumentNullException(nameof(result));

            if (result.Revenue > 0m)
            {
                Record(tick, LedgerCategory.Revenue, result.Revenue,
                    result.CoversServed + " covers served", restaurant.Id);
            }

            if (result.FoodCost > 0m)
            {
                var note = "Ingredients for " + result.Tickets.Count + " tickets";
                if (result.WastedFoodCost > 0m)
                {
                    note += " (including " + result.WastedFoodCost.ToString("0.00",
                        System.Globalization.CultureInfo.InvariantCulture) + " cooked for walkouts)";
                }

                Record(tick, LedgerCategory.FoodCost, result.FoodCost, note, restaurant.Id);
            }

            if (result.LabourCost > 0m)
            {
                Record(tick, LedgerCategory.LaborCost, result.LabourCost,
                    restaurant.Payroll.Headcount + " on the payroll", restaurant.Id);
            }
        }

        /// <summary>
        /// P&amp;L for a tick range, optionally for one location. Computed fresh from the
        /// entries every call — no cached totals, same rule as everything else here.
        /// </summary>
        public FinancialSummary Summarize(long fromTick, long toTick, string restaurantId = null)
        {
            decimal revenue = 0m, food = 0m, labor = 0m, overhead = 0m, capex = 0m;
            var count = 0;

            foreach (var entry in _entries)
            {
                if (entry.Tick < fromTick || entry.Tick > toTick) continue;
                if (restaurantId != null && entry.RestaurantId != restaurantId) continue;

                count++;

                switch (entry.Category)
                {
                    case LedgerCategory.Revenue: revenue += entry.Amount; break;
                    case LedgerCategory.FoodCost: food += entry.Amount; break;
                    case LedgerCategory.LaborCost: labor += entry.Amount; break;
                    case LedgerCategory.Overhead: overhead += entry.Amount; break;
                    case LedgerCategory.CapitalExpenditure: capex += entry.Amount; break;
                }
            }

            return new FinancialSummary(fromTick, toTick, restaurantId, revenue, food, labor, overhead, capex, count);
        }

        /// <summary>P&amp;L across everything recorded so far.</summary>
        public FinancialSummary SummarizeAll(string restaurantId = null)
        {
            return Summarize(long.MinValue, long.MaxValue, restaurantId);
        }
    }
}
