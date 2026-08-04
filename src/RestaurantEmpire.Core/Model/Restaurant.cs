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
        internal Restaurant(string id, string name, LocationType locationType, Company company, Region region = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Restaurant id is required.", nameof(id));
            if (company == null) throw new ArgumentNullException(nameof(company));

            Id = id;
            Name = name ?? id;
            LocationType = locationType;
            Company = company;
            Menu = new Menu(company.Definitions);
            Inventory = new Inventory(company.Definitions);
            Licence = new LiquorLicense();
            Kitchen = new Kitchen();
            DiningRoom = new DiningRoom();
            Payroll = new Payroll();
            Location = Neighborhood.SuburbanHighStreet();
            ServiceWindows = new List<ServiceWindow>(ServiceWindow.DefaultDay());
            // Company -> (Region) -> Restaurant. A site with no region inherits straight from
            // the company, which is every restaurant until the group is big enough to have one.
            Region = region;

            SupplierPolicy = new SupplierPolicy(company.Definitions, Name,
                region == null ? company.SupplierPolicy : region.SupplierPolicy);
            Pricing = new PricingPolicy(company.Definitions, Name, company.Pricing);
        }

        /// <summary>Which group of restaurants this one buys with, or null if it buys alone.</summary>
        public Region Region { get; }

        /// <summary>The market this restaurant trades in, or null for the home market.</summary>
        public Definitions.CountryDefinition Country { get { return Region == null ? null : Region.Country; } }

        /// <summary>
        /// What the payroll costs here. A wage in Lyon is not a wage in the suburbs, and this
        /// is not a difficulty dial — it changes which CONCEPTS are viable, because a
        /// prep-heavy card in an expensive labor market is a different proposition.
        /// </summary>
        public decimal HourlyWageBill
        {
            get
            {
                var country = Country;
                return Payroll.HourlyWageBill * (country == null ? 1m : country.LaborCostMultiplier);
            }
        }

        /// <summary>
        /// How much life an ingredient has lost by the time it lands here — the supplier's own
        /// transit, plus a long haul if they do not deliver in this country.
        ///
        /// THIS IS WHAT RE-OPENS THE SOURCING DECISION ABROAD. Your usual supplier is still
        /// available in Italy and is now a bad idea, because everything they send arrives four
        /// days older than it would at home. Meanwhile the local grower down the road is
        /// excellent and cheap. Expansion does not scale a settled decision — it unsettles it.
        /// </summary>
        public int TransitDaysFor(string ingredientId)
        {
            var days = SupplierPolicy.DaysInTransitFor(ingredientId);
            var country = Country;
            if (country == null) return days;

            var supplier = SupplierPolicy.ResolveSupplierId(ingredientId);
            return country.SuppliesLocally(supplier) ? days : days + country.ImportTransitDays;
        }

        /// <summary>
        /// Sets this restaurant up as a concept describes it: the card, the hours, and where
        /// the prices sit.
        ///
        /// ONE CODE PATH FOR EVERY CONCEPT, which is the whole of Architecture Rule 2's claim
        /// — a new concept is a JSON file and nothing here changes. It reads the definition
        /// and does what it says; it does not know a pizzeria from a wine bar.
        ///
        /// Deliberately does NOT staff, equip or seat the restaurant. A concept says what you
        /// are attempting; running it properly stays the player's job, or picking a concept
        /// would pick the whole restaurant and solve the strategy for them.
        /// </summary>
        public void Adopt(Definitions.ConceptDefinition concept)
        {
            if (concept == null) throw new ArgumentNullException(nameof(concept));

            foreach (var existing in new List<string>(Menu.RecipeIds)) Menu.Remove(existing);
            foreach (var id in concept.RecipeIds) Menu.Add(id);

            ServiceWindows.Clear();
            foreach (var service in concept.Services)
                ServiceWindows.Add(new ServiceWindow(service.Name, service.From, service.To));

            // Prices are set against what each dish was DESIGNED to sell for, so a position
            // means the same thing on every card. Per-dish overrides remain the player's move.
            foreach (var recipe in Menu.Recipes)
                Pricing.SetPrice(recipe.Id, recipe.MenuPrice * concept.PricePosition);
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Brick-and-mortar, food truck, ghost kitchen... a parameter, not a subclass.</summary>
        public LocationType LocationType { get; }

        /// <summary>The parent company. Never null.</summary>
        public Company Company { get; }

        public Menu Menu { get; }
        public Inventory Inventory { get; }

        /// <summary>Permission to sell alcohol. Bought with <see cref="ApplyForLiquorLicense"/>.</summary>
        public LiquorLicense Licence { get; }

        /// <summary>
        /// True when this restaurant may sell a given dish at all — the licence gate, sitting
        /// alongside the station gate. A cocktail list with no licence is exactly as unsellable
        /// as a risotto with no range.
        /// </summary>
        public bool MayServe(Definitions.RecipeDefinition recipe)
        {
            if (recipe == null) return false;
            return !recipe.RequiresLicense || Licence.Held;
        }

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
            return Location == null ? 0.0 : Location.TrafficAt(now) * (double)ShareOfTheStreet;
        }

        /// <summary>
        /// A STREET ONLY HOLDS SO MANY PEOPLE, AND YOUR OWN SECOND RESTAURANT IS DRINKING
        /// FROM THE SAME WELL.
        ///
        /// Measured before this existed: two restaurants under one company earned 131,903
        /// against 131,439 for the two of them run separately — 0.4% apart. A second site was
        /// arithmetic, which is exactly the flat-scaling anti-pattern. Nothing anywhere made
        /// two restaurants on one street contend for the same crowd, so opening next door to
        /// yourself was free.
        ///
        /// The split is BY APPEAL, not down the middle, and that is what makes it a decision
        /// rather than a tax. Put a second restaurant on a street and it takes the share its
        /// card and its name deserve — so a strong new site mostly eats a rival's trade, and
        /// a weak one mostly eats your own. Cloning your best restaurant next door to itself
        /// is the worst possible use of the money, and spreading out is what expansion is FOR.
        ///
        /// Only restaurants sharing a neighborhood contend. Two sites in different
        /// neighborhoods do not see each other at all, which is the whole point of a map.
        /// </summary>
        public decimal ShareOfTheStreet
        {
            get
            {
                if (Location == null || Company == null) return 1m;

                var mine = Pull;
                var total = 0m;

                foreach (var other in Company.Restaurants)
                {
                    if (other.Location == null) continue;
                    if (!string.Equals(other.Location.Id, Location.Id, StringComparison.Ordinal)) continue;

                    total += other.Pull;
                }

                if (total <= 0m) return 1m;

                // A street that is not saturated is not divided. One restaurant alone keeps
                // the whole crowd; the division only bites once somebody else is on it.
                var contenders = total / (mine <= 0m ? 1m : mine);
                if (contenders <= 1m) return 1m;

                return mine / total;
            }
        }

        /// <summary>
        /// How much of a street's attention this restaurant commands — its standing and how
        /// well its card suits the people walking past. Never zero, because even a bad
        /// restaurant on a good street takes some trade.
        /// </summary>
        public decimal Pull
        {
            get { return 0.25m + Reputation.Standing; }
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
        public int ServableSeats
        {
            get
            {
                // Fourteen covers is what an average server holds. A good one holds more and
                // a poor one fewer, so the floor is who you hired rather than how many.
                var servers = Payroll.CountOf(StaffRole.Server);
                if (servers == 0) return 0;

                var each = 14m * (0.7m + (Payroll.AverageSkill(StaffRole.Server) * 0.6m));
                return (int)(servers * each);
            }
        }

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
                    Location.MaxFloorArea.ToString("0.0") + " sq ft and you are at " + FloorArea.ToString("0.0") +
                    " sq ft, so there is only " + (headroom < 0m ? 0m : headroom).ToString("0.0") +
                    " sq ft to build into. You cannot knock through into the building next door.");
            }

            var cost = extraSquareMeters * Location.ExtensionCostPerSquareFoot;

            FloorArea += extraSquareMeters;
            Company.Economy.Record(tick, LedgerCategory.CapitalExpenditure, cost,
                "Extended into " + extraSquareMeters.ToString("0.0") + " sq ft more of the site", Id);
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

            // THROUGHPUT SCALES WITH PLATES, FLOOR SPACE SCALES WITH BOXES, and conflating them
            // is a real trap: `KitchenStation.Footprint` is `FootprintPerUnit x ConcurrentCapacity`,
            // so making capacity mean plates instantly tripled the room an oven takes up.
            //
            // Dividing the model's footprint by its batch keeps that identity true — a
            // three-pizza oven is stored as three plate-slots of a third the footprint each,
            // which multiplies back to exactly one oven's worth of floor.
            var platesPerUnit = equipment.PlatesAtOnce < 1 ? 1 : equipment.PlatesAtOnce;
            var totalUnits = keeping ? existing.ConcurrentCapacity + (units * platesPerUnit) : units * platesPerUnit;
            var footprintPerPlate = equipment.Footprint / platesPerUnit;
            var spaceNeeded = (footprintPerPlate * totalUnits) - (existing == null ? 0m : existing.Footprint);

            if (!HasRoomFor(spaceNeeded))
            {
                throw new InvalidOperationException(
                    "No room: that needs " + spaceNeeded.ToString("0.0") + " sq ft and only " +
                    FreeFloorArea.ToString("0.0") + " sq ft of " + FloorArea.ToString("0.0") + " sq ft is free. " +
                    "Sell something, buy a smaller model, or find a bigger building.");
            }

            var station = new KitchenStation(
                equipment.StationId, equipment.Name, totalUnits,
                equipment.SpeedMultiplier, equipment.Cost, footprintPerPlate, equipment.Id, platesPerUnit);

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

        /// <summary>
        /// Applies for a liquor licence, billing the company. Refused if the money is not
        /// there — a licence you cannot afford is not a licence.
        ///
        /// This is the capital gate that makes drinks a decision rather than a free upgrade:
        /// real money up front, a renewal every month whether you sell a drop, and a whole
        /// revenue line on the other side of it.
        /// </summary>
        public bool ApplyForLiquorLicense(long tick = 0, int today = 0)
        {
            if (Licence.Held) return false;
            if (Company.Economy.CashOnHand < LiquorLicense.ApplicationFee) return false;

            Licence.Grant(today);
            Company.Economy.Record(tick, LedgerCategory.CapitalExpenditure,
                LiquorLicense.ApplicationFee, "Liquor licence", Id);

            return true;
        }

        /// <summary>Buys and installs a piece of furniture or decor, billing the company.</summary>
        public Fitting Buy(Fitting fitting, long tick = 0)
        {
            if (fitting == null) throw new ArgumentNullException(nameof(fitting));

            if (!HasRoomFor(fitting.Footprint))
            {
                throw new InvalidOperationException(
                    "No room: " + fitting.Name + " needs " + fitting.Footprint.ToString("0.0") +
                    " sq ft and only " + FreeFloorArea.ToString("0.0") + " sq ft is free. " +
                    "The kitchen and the dining room are competing for the same floor.");
            }

            DiningRoom.Add(fitting);

            if (fitting.Cost > 0m)
                Company.Economy.Record(tick, LedgerCategory.CapitalExpenditure, fitting.Cost, "Bought " + fitting.Name, Id);

            return fitting;
        }

        /// <summary>Convenience for the common case: buy seating.</summary>
        /// <summary>
        /// Buy ingredients. **The money leaves now**, which is the whole point.
        ///
        /// Aaron: *"you should pay when you buy it and then make money when you sell a dish
        /// right?"* Right — and until this existed the game charged for ingredients at the
        /// moment they were COOKED, so filling a walk-in cost nothing until the food was sold.
        /// A pantry was free to hold, which made par levels a slider rather than a decision
        /// and made it impossible to be profitable on paper and still short of rent.
        ///
        /// Returns what it cost.
        /// </summary>
        public decimal OrderStock(string ingredientId, decimal quantity, long tick = 0)
        {
            if (quantity <= 0m) return 0m;

            // YOU CANNOT BUY WHAT YOU CANNOT PUT ANYWHERE. Storage is the thing that finally
            // makes "order deep" cost something visible rather than only being punished by
            // spoilage after the fact — and because storage is equipment, buying more of it
            // competes for the same floor as the kitchen and the dining room.
            var room = RoomInStorageFor(ingredientId);
            if (room <= 0m) return 0m;
            if (quantity > room) quantity = room;

            var cost = quantity * SupplierPolicy.UnitPriceFor(ingredientId);

            Company.Economy.Record(tick, LedgerCategory.FoodCost, cost,
                "Ingredients — " + ingredientId, Id);

            // What arrives is as old as the journey made it. A local grower drops fresh; a
            // national contract ships through a depot and a four-day fish lands with two days
            // left. That single number is what stops bulk sourcing being a free discount.
            Inventory.Receive(ingredientId, quantity, TransitDaysFor(ingredientId));
            return cost;
        }

        /// <summary>
        /// Whether the kitchen orders for itself, topping up to par each day.
        ///
        /// ON BY DEFAULT, and that is the point. Aaron: *"you don't want to constantly be
        /// ordering because things are spoiling... it shouldn't be a huge daily thing you need
        /// to always be monitoring, then you are basically playing a stocking game."* Right —
        /// and as first built it was exactly that, because perishables need topping up every
        /// few days and nothing did it for you.
        ///
        /// **The decision is the PAR POLICY, not the daily act.** You set how deep you want to
        /// run, once, and revisit it when the menu or the trade changes. Spoilage then
        /// punishes a standing order that is too deep — which is a judgement about how you run
        /// the place — rather than punishing you for looking away for a week.
        /// </summary>
        public bool StandingOrder { get; set; } = true;

        /// <summary>
        /// What a restaurant can hold before it owns any storage at all — a couple of
        /// under-counter fridges and a shelf, the sort of thing that comes with the unit.
        /// Enough to trade on, nowhere near enough to buy in bulk.
        /// </summary>
        /// Sized from what a working restaurant actually holds: a 32-cover dinner service
        /// carries roughly 250 units of chilled stock across its perishables and rather more
        /// dry. Generous enough that a normal kitchen never thinks about it, tight enough that
        /// buying in bulk means buying somewhere to put it.
        public const decimal BaseColdStorage = 1000m;
        public const decimal BaseDryStorage = 3000m;

        /// <summary>Units of perishable stock this restaurant can keep. Chilled space.</summary>
        public decimal ColdStorageCapacity { get { return BaseColdStorage + CapacityOf("cold-storage"); } }

        /// <summary>Units of dry goods. Cheaper per unit of space, and nothing in it rots.</summary>
        public decimal DryStorageCapacity { get { return BaseDryStorage + CapacityOf("dry-storage"); } }

        private decimal CapacityOf(string stationId)
        {
            // Kitchen.Get THROWS when nothing is installed rather than returning null, which
            // is worth knowing: assuming null here took out a dozen tests at once.
            KitchenStation station = null;
            foreach (var installed in Kitchen.Stations)
            {
                if (installed.Id != stationId) continue;
                station = installed;
                break;
            }

            if (station == null) return 0m;

            try { return Company.Definitions.GetEquipment(station.EquipmentId).Capacity * station.ConcurrentCapacity; }
            catch (Definitions.DefinitionNotFoundException) { return 0m; }
        }

        /// <summary>How much chilled and dry space is already spoken for.</summary>
        public decimal ColdStorageUsed { get { return Inventory.HeldOfKind(Company.Definitions, true); } }
        public decimal DryStorageUsed { get { return Inventory.HeldOfKind(Company.Definitions, false); } }

        /// <summary>Space left for this ingredient, in its own kind of storage.</summary>
        public decimal RoomInStorageFor(string ingredientId)
        {
            var perishable = true;
            try { perishable = Company.Definitions.GetIngredient(ingredientId).Perishable; }
            catch (Definitions.DefinitionNotFoundException) { }

            var left = perishable
                ? ColdStorageCapacity - ColdStorageUsed
                : DryStorageCapacity - DryStorageUsed;

            return left < 0m ? 0m : left;
        }

        /// <summary>Top every ingredient back into its par band, and pay for the lot.</summary>
        public decimal OrderStockToPar(long tick = 0)
        {
            var spent = 0m;

            foreach (var stock in new List<IngredientStock>(Inventory.Items))
            {
                if (!stock.IsBelowPar) continue;

                var wanted = stock.SuggestedReorderQuantity;
                if (wanted <= 0m) continue;

                // Never order food you cannot pay for. A restaurant with no money stops
                // getting deliveries, which is a truer failure than an overdraft that grows
                // quietly in the background.
                var affordable = Company.Economy.CashOnHand / SupplierPolicy.UnitPriceFor(stock.IngredientId);
                if (affordable <= 0m) continue;
                if (wanted > affordable) wanted = affordable;

                spent += OrderStock(stock.IngredientId, wanted, tick);
            }

            return spent;
        }

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
