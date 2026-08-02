using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// One brigade station — the oven, the sauté, the garde-manger.
    ///
    /// Modeled on the Escoffier brigade system the design doc researched in Phase 2: each
    /// station is a queue with a throughput rate. <see cref="ConcurrentCapacity"/> is how
    /// many plates it can work at once (two ovens = 2), and the speed multiplier is
    /// equipment quality. From M1 an assigned employee's skill multiplies this too.
    /// </summary>
    public sealed class KitchenStation
    {
        public KitchenStation(string id, string name, int concurrentCapacity = 1, decimal speedMultiplier = 1.0m,
            decimal cost = 0m, decimal footprintPerUnit = 0m, string equipmentId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Station id is required.", nameof(id));
            if (concurrentCapacity < 1) throw new ArgumentOutOfRangeException(nameof(concurrentCapacity), "A station must handle at least one plate at a time.");
            if (speedMultiplier <= 0m) throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "Speed multiplier must be positive.");
            if (cost < 0m) throw new ArgumentOutOfRangeException(nameof(cost));

            Id = id;
            Name = name ?? id;
            ConcurrentCapacity = concurrentCapacity;
            SpeedMultiplier = speedMultiplier;
            Cost = cost;
            FootprintPerUnit = footprintPerUnit;
            EquipmentId = equipmentId;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>How many plates this station can work simultaneously.</summary>
        public int ConcurrentCapacity { get; }

        /// <summary>Above 1.0 is faster than baseline. Better equipment, and later better staff.</summary>
        public decimal SpeedMultiplier { get; }

        /// <summary>
        /// What this cost to buy. Opening a breakfast service means buying the espresso
        /// machine it needs, which is precisely what makes longer hours a decision rather
        /// than free upside.
        /// </summary>
        public decimal Cost { get; }

        /// <summary>Square meters one unit takes up. The kitchen and the dining room share one building.</summary>
        public decimal FootprintPerUnit { get; }

        /// <summary>Total floor space this station occupies.</summary>
        public decimal Footprint { get { return FootprintPerUnit * ConcurrentCapacity; } }

        /// <summary>Which catalogue model this is, when it came from the shop.</summary>
        public string EquipmentId { get; }

        /// <summary>
        /// Actual minutes this station takes on a dish, after its speed multiplier and after
        /// whatever the breadth of the menu is costing the kitchen.
        /// </summary>
        public int MinutesFor(RecipeDefinition recipe, decimal complexityLoad = 1m)
        {
            if (complexityLoad < 1m) complexityLoad = 1m;

            var minutes = (int)Math.Ceiling((recipe.PrepMinutes * complexityLoad) / SpeedMultiplier);
            return minutes < 1 ? 1 : minutes;
        }

        public override string ToString()
        {
            return Name + " (" + Id + ", x" + ConcurrentCapacity + ")";
        }
    }

    /// <summary>Why a ticket ended the way it did — always a named cause, never a silent failure.</summary>
    public enum TicketOutcome
    {
        Served = 0,

        /// <summary>The kitchen has no station that can cook this. A build/layout problem.</summary>
        NoStation = 1,

        /// <summary>Ran out of an ingredient mid-service — the dish is 86'd.</summary>
        OutOfStock = 2
    }

    /// <summary>
    /// One order moving through the kitchen.
    ///
    /// Architecture Rule 6: a ticket does NOT know whether it came from a dine-in table or
    /// a delivery order. There is deliberately no channel field here. That costs nothing
    /// today and is what makes delivery an additive feature later rather than a rewrite of
    /// the kitchen.
    /// </summary>
    public sealed class Ticket
    {
        internal Ticket(string recipeId, string stationId, long placedTick, long startedTick, long completedTick)
        {
            RecipeId = recipeId;
            StationId = stationId;
            PlacedTick = placedTick;
            StartedTick = startedTick;
            CompletedTick = completedTick;
            Outcome = TicketOutcome.Served;
        }

        internal Ticket(string recipeId, string stationId, long placedTick, TicketOutcome outcome,
            string failureReason, string failedIngredientId = null)
        {
            RecipeId = recipeId;
            StationId = stationId;
            PlacedTick = placedTick;
            StartedTick = placedTick;
            CompletedTick = placedTick;
            Outcome = outcome;
            FailureReason = failureReason;
            FailedIngredientId = failedIngredientId;
        }

        public string RecipeId { get; }
        public string StationId { get; }

        public long PlacedTick { get; }
        public long StartedTick { get; }
        public long CompletedTick { get; }

        public TicketOutcome Outcome { get; }

        /// <summary>Plain-language reason when this ticket wasn't served. Null when it was.</summary>
        public string FailureReason { get; }

        /// <summary>
        /// Which ingredient ran out, when that is why this failed. Exposed as an ID rather
        /// than only inside the message so callers can group by cause — one stockout is one
        /// problem, however many tickets it takes down.
        /// </summary>
        public string FailedIngredientId { get; }

        public bool WasServed { get { return Outcome == TicketOutcome.Served; } }

        /// <summary>Total minutes the guest waited, order to plate.</summary>
        public int WaitMinutes { get { return (int)(CompletedTick - PlacedTick); } }

        /// <summary>Minutes spent waiting for the station to free up — this is the bottleneck signal.</summary>
        public int QueuedMinutes { get { return (int)(StartedTick - PlacedTick); } }

        /// <summary>Minutes actually being cooked.</summary>
        public int CookMinutes { get { return (int)(CompletedTick - StartedTick); } }
    }

    /// <summary>
    /// The stations installed at one location. Persistent kitchen configuration, not
    /// per-service state — see <see cref="OpenPass"/> for that.
    /// </summary>
    public sealed class Kitchen
    {
        private readonly Dictionary<string, KitchenStation> _stations;

        internal Kitchen()
        {
            _stations = new Dictionary<string, KitchenStation>(StringComparer.Ordinal);
        }

        public IEnumerable<KitchenStation> Stations { get { return _stations.Values; } }

        /// <summary>Floor space the whole kitchen occupies.</summary>
        public decimal Footprint
        {
            get
            {
                var total = 0m;
                foreach (var station in _stations.Values) total += station.Footprint;

                return total;
            }
        }
        public int StationCount { get { return _stations.Count; } }

        public void Install(KitchenStation station)
        {
            if (station == null) throw new ArgumentNullException(nameof(station));
            _stations[station.Id] = station;
        }

        /// <summary>
        /// Puts a station in without charging for it. This is the raw mechanism — restoring
        /// a save must not re-buy the ovens. To actually purchase one, use
        /// <see cref="Restaurant.BuyStation"/>, which bills the books.
        /// </summary>
        public KitchenStation Install(string id, string name, int concurrentCapacity = 1, decimal speedMultiplier = 1.0m,
            decimal cost = 0m, decimal footprintPerUnit = 0m, string equipmentId = null)
        {
            var station = new KitchenStation(id, name, concurrentCapacity, speedMultiplier, cost, footprintPerUnit, equipmentId);
            Install(station);

            return station;
        }

        public bool HasStation(string stationId)
        {
            return stationId != null && _stations.ContainsKey(stationId);
        }

        public KitchenStation Get(string stationId)
        {
            KitchenStation found;
            if (!_stations.TryGetValue(stationId ?? string.Empty, out found))
                throw new InvalidOperationException("No station installed with id '" + stationId + "'.");

            return found;
        }

        public bool TryGet(string stationId, out KitchenStation station)
        {
            return _stations.TryGetValue(stationId ?? string.Empty, out station);
        }

        /// <summary>
        /// Theoretical ceiling: how many of this dish this kitchen could produce in a
        /// service if it did nothing else. The hard limit the design says the kitchen
        /// cannot exceed, whatever the dining room seats.
        /// </summary>
        public int CapacityFor(RecipeDefinition recipe, int serviceMinutes)
        {
            KitchenStation station;
            if (!TryGet(recipe.StationId, out station)) return 0;

            return (serviceMinutes / station.MinutesFor(recipe)) * station.ConcurrentCapacity;
        }

        /// <summary>
        /// Opens a fresh pass for one service. Queue state is per-service, never carried over.
        /// Pass the brigade's PLATE CAPACITY (see <see cref="Payroll.PlateCapacity"/>) to have
        /// the staff limit throughput; zero means
        /// staffing is not being modeled and only the equipment constrains the kitchen.
        /// </summary>
        public KitchenPass OpenPass(long serviceStartTick, int plates = 0)
        {
            return new KitchenPass(this, serviceStartTick, plates);
        }
    }

    /// <summary>
    /// The expeditor's pass for a single service: routes each ticket to its station and
    /// tracks when that station next frees up.
    ///
    /// This is where bottlenecks become real rather than theoretical. Two dishes sharing
    /// one station contend for it, so a rush on the oven delays everything the oven cooks —
    /// and <see cref="Ticket.QueuedMinutes"/> reports exactly how much of a guest's wait
    /// was queueing rather than cooking. That is the drill-down the design's legibility
    /// contract requires: "grill station backed up, 11 minutes against your usual 6."
    /// </summary>
    public sealed class KitchenPass
    {
        /// <summary>How many plates one cook can have going at once. A line, not a pan.</summary>
        public const int PlatesPerCook = Tuning.PlatesPerCook;


        private readonly Kitchen _kitchen;
        private readonly Dictionary<string, long[]> _slotFreeAt;
        private readonly long[] _cookFreeAt;

        internal KitchenPass(Kitchen kitchen, long serviceStartTick, int plates = 0)
        {
            _kitchen = kitchen;
            _slotFreeAt = new Dictionary<string, long[]>(StringComparer.Ordinal);

            // A plate needs a free machine AND somebody to work it. Equipment you have not
            // staffed is equipment you are not using, which is what stops "buy another oven"
            // being a complete answer and makes hiring a real decision.
            //
            // But a cook works a LINE, not a single pan — they run several things at once,
            // which is the whole point of a brigade. Modeling one cook as one plate forced
            // a headcount that bankrupted every restaurant in a hundred-run sweep.
            // PLATES, not bodies. Rounding a skilled brigade back to a headcount and then
            // multiplying threw the skill away entirely — three excellent cooks came out at
            // 3.9, floored to 3, which is precisely three average ones.
            _cookFreeAt = new long[plates < 0 ? 0 : plates];
            for (var i = 0; i < _cookFreeAt.Length; i++) _cookFreeAt[i] = serviceStartTick;

            foreach (var station in kitchen.Stations)
            {
                var slots = new long[station.ConcurrentCapacity];
                for (var i = 0; i < slots.Length; i++) slots[i] = serviceStartTick;

                _slotFreeAt[station.Id] = slots;
            }
        }

        /// <summary>
        /// How long a dish ordered right now would take to reach the pass, without ordering
        /// it. Nothing is mutated and no ingredients are touched.
        ///
        /// This is what a guest can see from the door — the design says customers know the
        /// "visible wait state" but never the kitchen's internals. Without it, people keep
        /// sitting down at a kitchen that is an hour behind, walk out before their food
        /// lands, and the food gets cooked and binned anyway. That is a death spiral no
        /// amount of extra equipment can dig you out of.
        /// </summary>
        public int EstimatedWaitMinutes(RecipeDefinition recipe, long atTick, int plates = 1)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            KitchenStation station;
            if (!_kitchen.TryGet(recipe.StationId, out station)) return int.MaxValue;

            var slots = _slotFreeAt[station.Id];
            var earliest = slots[0];

            for (var i = 1; i < slots.Length; i++)
            {
                if (slots[i] < earliest) earliest = slots[i];
            }

            var startsAt = earliest > atTick ? earliest : atTick;

            // A guest at the door is waiting on whichever is scarcer, the machine or the cook.
            for (var i = 0; i < _cookFreeAt.Length; i++)
            {
                if (i == 0 || _cookFreeAt[i] < earliest) earliest = _cookFreeAt[i];
            }

            if (_cookFreeAt.Length > 0 && earliest > startsAt) startsAt = earliest;

            // A table of four puts FOUR plates in the queue, and it is the last one landing
            // that decides whether they stayed. Quoting the wait for a single dish is an
            // optimistic promise that seats parties who then walk out — which showed up in
            // a balance sweep as more walkouts than covers served.
            var atOnce = station.ConcurrentCapacity;
            if (_cookFreeAt.Length > 0 && _cookFreeAt.Length < atOnce) atOnce = _cookFreeAt.Length;
            if (atOnce < 1) atOnce = 1;

            var rounds = (plates + atOnce - 1) / atOnce;
            if (rounds < 1) rounds = 1;

            return (int)(startsAt - atTick) + (rounds * station.MinutesFor(recipe, ComplexityLoad));
        }

        /// <summary>
        /// Fires one order. Consumes ingredients, finds the earliest free slot at the
        /// required station, and returns a ticket with real timings.
        ///
        /// Never throws for gameplay reasons — a missing station or an empty walk-in comes
        /// back as a ticket with a named failure, because an 86'd dish is an event the
        /// player should see, not an exception.
        /// </summary>
        /// <summary>
        /// What a wide menu costs the pass, as a multiplier on cook time. Set by the caller
        /// each service; 1.0 unless told otherwise, so a bare kitchen behaves as it always did.
        /// </summary>
        public decimal ComplexityLoad { get; set; } = 1m;

        public Ticket Fire(RecipeDefinition recipe, long placedTick, Inventory inventory)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            KitchenStation station;
            if (!_kitchen.TryGet(recipe.StationId, out station))
            {
                return new Ticket(recipe.Id, recipe.StationId, placedTick, TicketOutcome.NoStation,
                    recipe.Name + " needs a '" + recipe.StationId + "' station and this kitchen has none installed.");
            }

            if (inventory != null)
            {
                string shortfall;
                if (!inventory.TryConsumeAll(recipe.Ingredients, out shortfall))
                {
                    return new Ticket(recipe.Id, recipe.StationId, placedTick, TicketOutcome.OutOfStock,
                        recipe.Name + " was 86'd — out of " + shortfall + ".", shortfall);
                }
            }

            var slots = _slotFreeAt[station.Id];

            // Earliest-available slot. With capacity 1 this is a plain queue.
            var chosen = 0;
            for (var i = 1; i < slots.Length; i++)
            {
                if (slots[i] < slots[chosen]) chosen = i;
            }

            var startedTick = slots[chosen] > placedTick ? slots[chosen] : placedTick;

            // ...and the earliest free pair of hands, when the brigade is being modeled.
            var cook = -1;
            if (_cookFreeAt.Length > 0)
            {
                cook = 0;
                for (var i = 1; i < _cookFreeAt.Length; i++)
                {
                    if (_cookFreeAt[i] < _cookFreeAt[cook]) cook = i;
                }

                if (_cookFreeAt[cook] > startedTick) startedTick = _cookFreeAt[cook];
            }

            var completedTick = startedTick + station.MinutesFor(recipe, ComplexityLoad);

            slots[chosen] = completedTick;
            if (cook >= 0) _cookFreeAt[cook] = completedTick;

            return new Ticket(recipe.Id, station.Id, placedTick, startedTick, completedTick);
        }
    }
}
