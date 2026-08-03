using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Definitions
{
    /// <summary>
    /// A country you can trade in. A country IS a <see cref="Model.Region"/> — the tier that
    /// already exists — with a market attached.
    ///
    /// THE BAR THIS HAD TO CLEAR. Expansion was measured before it was built and a second
    /// restaurant came out 0.4% from arithmetic, which is the flat-scaling anti-pattern:
    /// "bigger numbers are not new decisions". **More places to put a restaurant does not fix
    /// that on its own.** A site in Lyon that is only "different rent, different footfall" is
    /// another arithmetic restaurant with a flag on it.
    ///
    /// So a country here changes what you can DO, on three axes, and each one is a decision
    /// that did not exist before:
    ///
    ///   - **The card does not travel.** <see cref="TastePulls"/> shifts what the local crowd
    ///     wants, so a concept that won at home can be a hard sell abroad. This is what makes
    ///     the scouting report worth reading for a country rather than only for a street.
    ///   - **Sourcing flips.** Each country has its own local suppliers. Italian produce is
    ///     excellent and cheap AT HOME; shipping your usual supply chain in makes it dear and
    ///     old, which `SupplierDefinition.DaysInTransit` already models. Expanding abroad
    ///     therefore re-opens a decision you thought you had settled.
    ///   - **Labor works differently.** <see cref="LaborCostMultiplier"/> is not a difficulty
    ///     dial; it changes which concepts are viable, because a prep-heavy card in an
    ///     expensive labor market is a different proposition from the same card at home.
    ///
    /// Written in American English throughout, per the project's language rule — which is a
    /// LANGUAGE rule and never was a restriction on setting.
    /// </summary>
    public sealed class CountryDefinition
    {
        private readonly Dictionary<string, decimal> _tastePulls;

        public CountryDefinition(string id, string name, string description,
            decimal laborCostMultiplier, IEnumerable<string> localSupplierIds,
            IDictionary<string, decimal> tastePulls, IEnumerable<string> neighborhoodIds)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Country id is required.", nameof(id));

            Id = id;
            Name = name ?? id;
            Description = description ?? string.Empty;
            LaborCostMultiplier = laborCostMultiplier <= 0m ? 1m : laborCostMultiplier;

            var suppliers = new List<string>();
            if (localSupplierIds != null) suppliers.AddRange(localSupplierIds);
            LocalSupplierIds = suppliers;

            var hoods = new List<string>();
            if (neighborhoodIds != null) hoods.AddRange(neighborhoodIds);
            NeighborhoodIds = hoods;

            _tastePulls = tastePulls == null
                ? new Dictionary<string, decimal>(StringComparer.Ordinal)
                : new Dictionary<string, decimal>(tastePulls, StringComparer.Ordinal);
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        /// <summary>
        /// What a wage costs here against the home market. Not a difficulty dial — it decides
        /// which concepts are viable, because a prep-heavy card is a different proposition in
        /// an expensive labor market.
        /// </summary>
        public decimal LaborCostMultiplier { get; }

        /// <summary>
        /// Suppliers who will deliver here without shipping it in. Anything not on this list
        /// is an import, and an import arrives old — see <see cref="ImportTransitDays"/>.
        /// </summary>
        public IReadOnlyList<string> LocalSupplierIds { get; }

        /// <summary>Sites available in this country.</summary>
        public IReadOnlyList<string> NeighborhoodIds { get; }

        /// <summary>
        /// How much extra life an ingredient loses when it is shipped in from abroad rather
        /// than bought locally. This is the reason expanding re-opens the sourcing decision:
        /// your usual supplier is still available and is now a bad idea.
        /// </summary>
        public int ImportTransitDays { get { return 4; } }

        /// <summary>
        /// What this country's diners lean toward, by recipe tag. Above 1.0 is a pull toward,
        /// below is away. Anything unlisted is 1.0 — neutral, no opinion.
        /// </summary>
        public decimal TasteFor(string tag)
        {
            decimal pull;
            return tag != null && _tastePulls.TryGetValue(tag, out pull) ? pull : 1m;
        }

        public IEnumerable<KeyValuePair<string, decimal>> TastePulls { get { return _tastePulls; } }

        public bool SuppliesLocally(string supplierId)
        {
            if (supplierId == null) return false;
            foreach (var id in LocalSupplierIds)
            {
                if (string.Equals(id, supplierId, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
