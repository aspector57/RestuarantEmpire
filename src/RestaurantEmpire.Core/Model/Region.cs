using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// A group of restaurants that buy together.
    ///
    /// THIS TIER WAS DESIGNED AT M0 AND DELIBERATELY LEFT UNBUILT, for a reason recorded in
    /// CLAUDE.md: *"without a regional tier, sourcing at ten restaurants is the identical
    /// decision as at one, which is the flat-scaling anti-pattern."* It had nothing to
    /// override until multi-location existed.
    ///
    /// Measured before building it, and the anti-pattern was real: two restaurants under one
    /// company earned 131,903 against 131,439 for the two of them run separately. A **0.4%
    /// difference** — a second restaurant was arithmetic, which is precisely "bigger numbers
    /// are not new decisions".
    ///
    /// What a Region adds is the one decision that CANNOT EXIST AT ONE RESTAURANT: a national
    /// distributor will not deal with you until you are buying enough, and when they will,
    /// what they send is cheaper, lower grade, and days older on arrival because it came
    /// through a depot. So expansion buys a new KIND of tradeoff rather than a bigger number,
    /// which is what the anti-pattern list demands of scale.
    ///
    /// Architecturally it is nothing new: <see cref="SupplierPolicy"/> already resolved up a
    /// parent chain, so this slots in between Company and Restaurant without a single read
    /// site changing. That was the whole point of building the chain first.
    /// </summary>
    public sealed class Region
    {
        private readonly List<Restaurant> _restaurants = new List<Restaurant>();

        internal Region(Company company, string id, string name, Definitions.CountryDefinition country = null)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Region id is required.", nameof(id));

            Company = company;
            Country = country;
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;

            // Inherits from the company, and is inherited BY its restaurants. One write here
            // moves every site in the region and leaves the rest of the group alone.
            SupplierPolicy = new SupplierPolicy(company.Definitions, Name, company.SupplierPolicy);
        }

        public Company Company { get; }

        /// <summary>
        /// Which market this region trades in, or null for the home market.
        ///
        /// A COUNTRY IS A REGION. That is not a shortcut — a country is exactly "a group of
        /// restaurants that buy together and share a market", which is what this tier already
        /// was. Nothing needed inventing for it.
        /// </summary>
        public Definitions.CountryDefinition Country { get; }
        public string Id { get; }
        public string Name { get; }

        /// <summary>Sourcing for every restaurant in this region, overriding the company default.</summary>
        public SupplierPolicy SupplierPolicy { get; }

        public IReadOnlyList<Restaurant> Restaurants { get { return _restaurants; } }

        internal void Add(Restaurant restaurant)
        {
            if (restaurant == null) throw new ArgumentNullException(nameof(restaurant));
            if (!_restaurants.Contains(restaurant)) _restaurants.Add(restaurant);
        }

        /// <summary>
        /// What this region gets through in a week, measured from what its restaurants have
        /// actually been using rather than from what they hold.
        ///
        /// USAGE, NOT STOCK, and the distinction is the mechanic. A distributor cares what you
        /// SHIFT — you cannot qualify for a national contract by filling a walk-in once, which
        /// would make the gate a cash test rather than a scale one, and cash is not the thing
        /// expansion is supposed to be proving.
        /// </summary>
        public decimal WeeklyVolume
        {
            get
            {
                var total = 0m;
                foreach (var restaurant in _restaurants)
                {
                    foreach (var stock in restaurant.Inventory.Items) total += stock.DailyUsage * 7m;
                }

                return total;
            }
        }

        /// <summary>
        /// Whether this region shifts enough for a supplier to take it on.
        ///
        /// Reported as a plain question with a plain answer, because a gate the player cannot
        /// see the far side of is indistinguishable from a bug — that lesson cost two sessions
        /// on the liquor licence.
        /// </summary>
        public bool CanContractWith(Definitions.SupplierDefinition supplier)
        {
            if (supplier == null) return false;
            return supplier.MinimumWeeklyVolume <= 0m || WeeklyVolume >= supplier.MinimumWeeklyVolume;
        }

        /// <summary>Why not, in words, or null when they will deal with you.</summary>
        public string WhyNotContractWith(Definitions.SupplierDefinition supplier)
        {
            if (supplier == null) return "No such supplier.";
            if (CanContractWith(supplier)) return null;

            return supplier.Name + " will not take an account under " +
                   supplier.MinimumWeeklyVolume.ToString("N0") + " units a week. " + Name +
                   " is getting through about " + WeeklyVolume.ToString("N0") +
                   " — that is roughly " + Math.Ceiling(supplier.MinimumWeeklyVolume / Math.Max(1m, WeeklyVolume)) +
                   "x the trade, so it wants more restaurants rather than a bigger order.";
        }
    }
}
