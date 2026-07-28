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
            Payroll = new Payroll();
            Location = Neighborhood.SuburbanHighStreet();
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
        /// hour. The player chooses the hours; the neighborhood decides whether anyone is
        /// out there. Demand is an output of location, never a number the player sets.
        /// </summary>
        public Neighborhood Location { get; set; }

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
        /// What the neighborhood thinks, and therefore how many people turn up. Genuine
        /// accumulated state rather than a live computation — it remembers what you served
        /// last month, which is the entire point — so it is saved with the game.
        /// </summary>
        public Reputation Reputation { get; } = new Reputation();

        /// <summary>
        /// The best this place could ever be thought of, from what it is actually attempting:
        /// the ingredients across its menu, and the room they are eaten in.
        ///
        /// Competence is free and gets you to the middle. Being loved is bought.
        /// </summary>
        public decimal ReputationCeiling
        {
            get
            {
                var quality = 0m;
                var counted = 0;

                foreach (var recipe in Menu.Recipes)
                {
                    quality += Costing.IngredientQuality(recipe.Id);
                    counted++;
                }

                if (counted > 0) quality /= counted;

                return Model.Reputation.CeilingFor(quality, DiningRoom.Comfort);
            }
        }

        /// <summary>
        /// Who works here. Hiring and firing are the player's call at any moment — and staff
        /// are what make the assets work, so an unstaffed kitchen is idle equipment and an
        /// unstaffed floor is empty seats.
        /// </summary>
        public Payroll Payroll { get; }

        /// <summary>
        /// How many covers the floor staff can actually look after at once. One server
        /// handles roughly fourteen. Zero servers means nobody gets seated at all.
        /// </summary>
        public int ServableSeats { get { return Payroll.CountOf(StaffRole.Server) * 14; } }

        /// <summary>
        /// Square meters of building. Zero means unmeasured, and nothing is constrained —
        /// which keeps a bare test fixture or a ghost kitchen from having to lease a unit.
        ///
        /// This is what stops "buy another oven" being the answer to everything. The kitchen
        /// and the dining room share one floor, so fifteen ovens is not a strategy, it is a
        /// dining room you no longer have. Getting both is what makes moving to a bigger
        /// building the design's primary early-game growth axis.
        /// </summary>
        public decimal FloorArea { get; set; }

        public decimal UsedFloorArea { get { return Kitchen.Footprint + DiningRoom.Footprint; } }

        /// <summary>
        /// How much bigger this building could ever get, given where it is. Zero means the
        /// site is already built out to its limit.
        /// </summary>
        public decimal ExpansionHeadroom
        {
            get { return Location == null ? decimal.MaxValue : Location.ExpansionHeadroom(FloorArea); }
        }

        /// <summary>
        /// Builds out into more of the site — the crude first form of build mode.
        ///
        /// It is capital-gated and, more interestingly, LOCATION-gated. In a city center you
        /// cannot simply knock through into the building next door, so a wonderful pitch can
        /// be one you outgrow and cannot fix. On a suburban high street there is a car park
        /// behind you and land is a third of the price.
        ///
        /// That is the trade the location choice is really making: footfall against ceiling.
        /// </summary>
        public void ExtendBuilding(decimal extraSquareMeters, long tick = 0)
        {
            if (extraSquareMeters <= 0m)
                throw new ArgumentOutOfRangeException(nameof(extraSquareMeters), "Extend by something.");

            if (Location == null)
                throw new InvalidOperationException("This restaurant has no location, so there is nothing to build into.");

            var headroom = ExpansionHeadroom;
            if (extraSquareMeters > headroom)
            {
                throw new InvalidOperationException(
                    "Cannot extend: " + Location.Name + " allows this site up to " +
                    Location.MaxFloorArea.ToString("0.0") + "m2 and you are at " + FloorArea.ToString("0.0") +
                    "m2, so there is only " + (headroom < 0m ? 0m : headroom).ToString("0.0") +
                    "m2 to build into. You cannot knock through into the building next door.");
            }

            var cost = extraSquareMeters * Location.ExtensionCostPerSquareMeter;

            FloorArea += extraSquareMeters;
            Company.Economy.Record(tick, LedgerCategory.CapitalExpenditure, cost,
                "Extended into " + extraSquareMeters.ToString("0.0") + "m2 more of the site", Id);
        }

        public decimal FreeFloorArea { get { return FloorArea - UsedFloorArea; } }

        /// <summary>True when there is room for something of this size (always, if unmeasured).</summary>
        public bool HasRoomFor(decimal squareMeters)
        {
            return FloorArea <= 0m || squareMeters <= FreeFloorArea;
        }

        /// <summary>
        /// Buys equipment from the catalogue and installs it, billing the company.
        ///
        /// Adding units to a station you already have keeps the existing model; buying a
        /// different model REPLACES the station, because you do not run two different ovens
        /// as one line. That is the upgrade path: when the floor is full, a faster machine
        /// in less space is the only way left to add throughput.
        /// </summary>
        public KitchenStation BuyEquipment(Definitions.EquipmentDefinition equipment, int units = 1, long tick = 0)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            if (units < 1) throw new ArgumentOutOfRangeException(nameof(units), "Buy at least one.");

            KitchenStation existing;
            var replacing = Kitchen.TryGet(equipment.StationId, out existing) && existing.EquipmentId != equipment.Id;
            var keeping = Kitchen.TryGet(equipment.StationId, out existing) && existing.EquipmentId == equipment.Id;

            var totalUnits = keeping ? existing.ConcurrentCapacity + units : units;
            var spaceNeeded = (equipment.Footprint * totalUnits) - (existing == null ? 0m : existing.Footprint);

            if (!HasRoomFor(spaceNeeded))
            {
                throw new InvalidOperationException(
                    "No room: that needs " + spaceNeeded.ToString("0.0") + "m2 and only " +
                    FreeFloorArea.ToString("0.0") + "m2 of " + FloorArea.ToString("0.0") + "m2 is free. " +
                    "Sell something, buy a smaller model, or find a bigger building.");
            }

            var station = new KitchenStation(
                equipment.StationId, equipment.Name, totalUnits,
                equipment.SpeedMultiplier, equipment.Cost, equipment.Footprint, equipment.Id);

            Kitchen.Install(station);

            var charge = equipment.Cost * units;
            if (charge > 0m)
            {
                Company.Economy.Record(tick, LedgerCategory.CapitalExpenditure, charge,
                    (replacing ? "Replaced " + equipment.StationId + " with " : "Bought ") +
                    units + "x " + equipment.Name, Id);
            }

            return station;
        }

        /// <summary>
        /// How many guests can sit down at once — DERIVED from the furniture actually
        /// bought, not declared. A bigger room is something you pay for.
        ///
        /// Zero means nothing has been installed, which the simulation reads as "capacity
        /// not modeled" and lets everyone in. That keeps a ghost kitchen or a bare test
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

            if (!HasRoomFor(fitting.Footprint))
            {
                throw new InvalidOperationException(
                    "No room: " + fitting.Name + " needs " + fitting.Footprint.ToString("0.0") +
                    "m2 and only " + FreeFloorArea.ToString("0.0") + "m2 is free. " +
                    "The kitchen and the dining room are competing for the same floor.");
            }

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
        /// neighborhood, a flagship that can command more.
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
