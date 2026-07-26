using System;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// One physical location. Always owned by a <see cref="Model.Company"/> — there is no
    /// way to construct a free-floating restaurant, which is what keeps the hierarchy
    /// honest rather than aspirational.
    ///
    /// Note what this class does NOT own: supplier assignments. Those live on the parent
    /// company, so a restaurant reads current prices rather than holding its own copy.
    /// </summary>
    public sealed class Restaurant
    {
        internal Restaurant(string id, string name, LocationType locationType, Company company)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Restaurant id is required.", nameof(id));
            if (company == null) throw new ArgumentNullException(nameof(company));

            Id = id;
            Name = name ?? id;
            LocationType = locationType;
            Company = company;
            Menu = new Menu(company.Definitions);
            Inventory = new Inventory(company.Definitions);
            Kitchen = new Kitchen();
            SupplierPolicy = new SupplierPolicy(company.Definitions, Name, company.SupplierPolicy);
            Pricing = new PricingPolicy(company.Definitions, Name, company.Pricing);
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Brick-and-mortar, food truck, ghost kitchen... a parameter, not a subclass.</summary>
        public LocationType LocationType { get; }

        /// <summary>The parent company. Never null.</summary>
        public Company Company { get; }

        public Menu Menu { get; }
        public Inventory Inventory { get; }

        /// <summary>
        /// The brigade stations installed here. This is the hard ceiling on how much this
        /// location can actually produce — the design's dominant layout failure mode is an
        /// impressive dining room fed by an undersized kitchen.
        /// </summary>
        public Kitchen Kitchen { get; }

        /// <summary>
        /// How many guests the dining room can seat at once. Zero means unset; the food
        /// truck and ghost kitchen cases simply use small or zero values rather than a
        /// different class (Architecture Rule 5).
        /// </summary>
        public int SeatingCapacity { get; set; }

        /// <summary>
        /// This location's own sourcing scope, which INHERITS FROM the company's.
        ///
        /// Normally empty — an empty local scope means "use the company default for
        /// everything," which is the healthy default and the whole point of propagation.
        /// Assigning here is the deliberate, rare exception: this one kitchen buys its
        /// tomatoes from the farm down the road, everything else still follows the company.
        /// </summary>
        public SupplierPolicy SupplierPolicy { get; }

        /// <summary>
        /// Live costing for this restaurant's menu.
        ///
        /// Deliberately returns a NEW instance on every access and holds no state. There is
        /// no cached costing object that could go stale, and no invalidation step anyone
        /// could forget to call — the numbers are recomputed from this restaurant's current
        /// sourcing chain each time you ask.
        /// </summary>
        /// <summary>
        /// What this location charges, inheriting from the company's brand pricing. Empty
        /// normally; setting a price here is the deliberate local exception — a pricier
        /// neighbourhood, a flagship that can command more.
        /// </summary>
        public PricingPolicy Pricing { get; }

        public MenuCosting Costing
        {
            get { return new MenuCosting(Company.Definitions, SupplierPolicy, Pricing); }
        }

        public override string ToString()
        {
            return Name + " (" + Id + ", " + LocationType + ")";
        }
    }
}
