using System;
using System.Collections.Generic;

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
            DiningRoom = new DiningRoom();
            Location = Neighbourhood.SuburbanHighStreet();
            ServiceWindows = new List<ServiceWindow>(ServiceWindow.DefaultDay());
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
        /// Where this restaurant sits, and therefore how much passing trade exists at each
        /// hour. The player chooses the hours; the neighbourhood decides whether anyone is
        /// out there. Demand is an output of location, never a number the player sets.
        /// </summary>
        public Neighbourhood Location { get; set; }

        /// <summary>
        /// When the doors are open. The clock runs continuously around these; outside them
        /// nobody arrives, which is exactly what makes most of a day compressible.
        /// Defaults to a conventional lunch and dinner — edit freely.
        /// </summary>
        public IList<ServiceWindow> ServiceWindows { get; }

        /// <summary>
        /// Potential parties per hour on the street right now — what the location offers,
        /// before anything about this restaurant is considered.
        /// </summary>
        public double TrafficAt(DateTime now)
        {
            return Location == null ? 0.0 : Location.TrafficAt(now);
        }

        /// <summary>Everything installed out front: tables, chairs, decor.</summary>
        public DiningRoom DiningRoom { get; }

        /// <summary>
        /// How many guests can sit down at once — DERIVED from the furniture actually
        /// bought, not declared. A bigger room is something you pay for.
        ///
        /// Zero means nothing has been installed, which the simulation reads as "capacity
        /// not modelled" and lets everyone in. That keeps a ghost kitchen or a bare test
        /// fixture from having to furnish itself first (Architecture Rule 5: location type
        /// is a parameter, not a subclass).
        /// </summary>
        public int SeatingCapacity { get { return DiningRoom.Seats; } }

        /// <summary>
        /// Buys and installs a kitchen station, billing the company. This is how equipment
        /// becomes a real decision: opening for breakfast means buying the espresso machine
        /// that breakfast needs.
        /// </summary>
        public KitchenStation BuyStation(
            string id, string name, decimal cost, int concurrentCapacity = 1, decimal speedMultiplier = 1.0m, long tick = 0)
        {
            var station = new KitchenStation(id, name, concurrentCapacity, speedMultiplier, cost);
            Kitchen.Install(station);

            if (cost > 0m)
                Company.Economy.Record(tick, LedgerCategory.CapitalExpenditure, cost, "Bought " + station.Name, Id);

            return station;
        }

        /// <summary>Buys and installs a piece of furniture or decor, billing the company.</summary>
        public Fitting Buy(Fitting fitting, long tick = 0)
        {
            if (fitting == null) throw new ArgumentNullException(nameof(fitting));

            DiningRoom.Add(fitting);

            if (fitting.Cost > 0m)
                Company.Economy.Record(tick, LedgerCategory.CapitalExpenditure, fitting.Cost, "Bought " + fitting.Name, Id);

            return fitting;
        }

        /// <summary>Convenience for the common case: buy seating.</summary>
        public Fitting BuyTables(string id, string name, decimal cost, int seats, decimal comfort = 0.5m, long tick = 0)
        {
            return Buy(new Fitting(id, name, cost, seats, comfort), tick);
        }

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
