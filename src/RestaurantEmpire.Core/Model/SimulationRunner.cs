using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>What one call to <see cref="SimulationRunner.Advance"/> did.</summary>
    public sealed class AdvanceResult
    {
        internal AdvanceResult(long requested, long advanced, Interrupt interrupt, ElapsedPeriods elapsed)
        {
            TicksRequested = requested;
            TicksAdvanced = advanced;
            Interrupt = interrupt;
            Elapsed = elapsed;
        }

        public long TicksRequested { get; }

        /// <summary>How far it actually got. Less than requested when an interrupt stopped it.</summary>
        public long TicksAdvanced { get; }

        /// <summary>What stopped it, or null if it ran the whole way.</summary>
        public Interrupt Interrupt { get; }

        public bool StoppedEarly { get { return Interrupt != null; } }

        /// <summary>Day/week/month boundaries crossed — payroll, rent, the weekly review.</summary>
        public ElapsedPeriods Elapsed { get; }

        /// <summary>Ticks still owed if the player wants to carry on to where they were headed.</summary>
        public long TicksRemaining { get { return TicksRequested - TicksAdvanced; } }
    }

    /// <summary>
    /// The continuously-running restaurant.
    ///
    /// The clock runs 24 hours a day, rolling from one day into the next; guests only arrive
    /// during the restaurant's <see cref="Restaurant.ServiceWindows"/>, so the quiet stretches
    /// are exactly what jump-ahead compresses. This replaces the old model of discrete,
    /// atomic 3-hour services that appeared from nowhere.
    ///
    /// EVERY TICK IS A MINUTE AND EVERY MINUTE IS RESUMABLE. All live state — parties
    /// mid-meal, plates in the oven, seats occupied, the RNG — lives on this object, so:
    ///
    ///   - Pausing is simply not calling Advance. Nothing is lost, at any moment.
    ///   - An interrupt stops mid-service and the next Advance carries on from that exact
    ///     tick, mid-plate, mid-meal.
    ///   - Advancing a month in one call and advancing it one minute at a time produce
    ///     IDENTICAL results, because the RNG is consumed strictly in tick order.
    ///
    /// That last property is the M1 mechanism bar, and it is what makes the other two true
    /// rather than approximately true.
    /// </summary>
    public sealed class SimulationRunner
    {
        private readonly Restaurant _restaurant;
        private readonly DefinitionRegistry _definitions;
        private readonly DeterministicRandom _rng;
        private readonly KitchenPass _pass;

        private readonly List<Table> _tables = new List<Table>();
        private readonly Dictionary<string, int> _unitsSold = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Ticket> _tickets = new List<Ticket>();
        private readonly List<string> _diagnostics = new List<string>();
        private readonly Dictionary<string, int> _stationMinutes = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly HashSet<string> _stockoutsReported = new HashSet<string>(StringComparer.Ordinal);

        private decimal _revenue, _foodCost, _wastedFoodCost, _satisfactionTotal;
        private int _partiesArrived, _partiesTurnedAway, _coversServed, _walkouts, _eightySixed, _longestWait;
        private int _walkoutStreak, _partyCounter, _occupiedSeats;
        private bool _cashFloorBreached, _walkoutAlarmRaisedThisService;
        private string _serviceOnLastTick;

        public SimulationRunner(Restaurant restaurant, GameClock clock, long seed, InterruptPolicy interrupts = null)
        {
            if (restaurant == null) throw new ArgumentNullException(nameof(restaurant));
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            _restaurant = restaurant;
            _definitions = restaurant.Company.Definitions;
            _rng = new DeterministicRandom(seed);
            _pass = restaurant.Kitchen.OpenPass(clock.Tick);

            Clock = clock;
            Interrupts = interrupts ?? new InterruptPolicy();
        }

        public GameClock Clock { get; }
        public Restaurant Restaurant { get { return _restaurant; } }
        public InterruptPolicy Interrupts { get; }

        /// <summary>Whether the doors are open right now.</summary>
        public bool IsOpen { get { return CurrentWindow() != null; } }

        /// <summary>The service currently running, or null when closed.</summary>
        public ServiceWindow CurrentWindow()
        {
            var now = Clock.Now;

            foreach (var window in _restaurant.ServiceWindows)
            {
                if (window.IsOpenAt(now)) return window;
            }

            return null;
        }

        /// <summary>Guests currently in the room, eating or waiting.</summary>
        public int GuestsInside { get { return _occupiedSeats; } }

        /// <summary>
        /// Cash as it stands right now, mid-service: what the books said when this run
        /// started, plus what has been taken and spent since.
        ///
        /// The ledger is only written at the end of a run, because one entry per plate
        /// would bury the books in thousands of lines. But "you are running out of money
        /// tonight" has to be answerable *tonight*, not in tomorrow's accounts — so the
        /// cash interrupt watches this rather than the ledger.
        /// </summary>
        public decimal ProjectedCash
        {
            get { return _restaurant.Company.Economy.CashOnHand + _revenue - _foodCost; }
        }

        /// <summary>Everything that has happened since this runner started.</summary>
        public ServiceResult Snapshot()
        {
            return new ServiceResult(
                new Dictionary<string, int>(_unitsSold), _tickets, _diagnostics,
                _revenue, _foodCost, _wastedFoodCost,
                _partiesArrived, _partiesTurnedAway, _coversServed, _walkouts, _eightySixed,
                _coversServed == 0 ? 0m : _satisfactionTotal / _coversServed,
                _longestWait, BusiestStation());
        }

        // ---- Advancing ----

        public AdvanceResult AdvanceHours(int hours) { return Advance((long)hours * GameClock.TicksPerHour); }
        public AdvanceResult AdvanceDays(int days) { return Advance((long)days * GameClock.TicksPerDay); }
        public AdvanceResult AdvanceWeeks(int weeks) { return Advance((long)weeks * GameClock.TicksPerWeek); }

        /// <summary>Runs until the given tick, or until something needs the player.</summary>
        public AdvanceResult AdvanceTo(long targetTick)
        {
            return Advance(targetTick - Clock.Tick);
        }

        /// <summary>
        /// Steps the world forward, one minute at a time, stopping early if an interrupt
        /// fires. Advancing zero is legal and does nothing — which is all "paused" means.
        /// </summary>
        public AdvanceResult Advance(long ticks)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), "The simulation cannot run backwards.");

            var startTick = Clock.Tick;
            Interrupt interrupt = null;
            long advanced = 0;

            while (advanced < ticks)
            {
                Clock.Advance(1);
                advanced++;

                interrupt = Tick();
                if (interrupt != null) break;
            }

            // Report boundaries across the whole span actually covered, so a caller that
            // asked for a month still learns about every payroll it passed through.
            var elapsed = ElapsedBetween(startTick, Clock.Tick);

            return new AdvanceResult(ticks, advanced, interrupt, elapsed);
        }

        // ---- One minute ----

        private Interrupt Tick()
        {
            var now = Clock.Now;
            var tick = Clock.Tick;

            Interrupt interrupt = null;

            // A new service is a clean slate for the alarms that are per-service.
            var serviceNow = CurrentWindow() == null ? null : CurrentWindow().Name;
            if (serviceNow != _serviceOnLastTick)
            {
                _serviceOnLastTick = serviceNow;
                _walkoutAlarmRaisedThisService = false;
                _walkoutStreak = 0;
            }

            // 1. Guests who have run out of patience give up and leave. Their food is
            //    already in the oven and will still be cooked, and still be binned.
            foreach (var table in _tables)
            {
                if (table.WalkedOut || table.Settled) continue;
                if (tick - table.Party.ArrivalTick <= table.Party.PatienceMinutes) continue;

                table.WalkedOut = true;

                foreach (var order in table.Orders)
                {
                    if (order.Resolved) continue;

                    _walkouts++;
                    _walkoutStreak++;
                    _diagnostics.Add("Walked out after " + (tick - table.Party.ArrivalTick) +
                        " min waiting for " + _definitions.GetRecipe(order.RecipeId).Name +
                        " (patience " + table.Party.PatienceMinutes + " min; " + order.Ticket.QueuedMinutes +
                        " of that was the " + order.Ticket.StationId + " station backed up).");
                }

                // Once per service, not once per streak. A kitchen that is underwater all
                // night is ONE problem the player needs told about, not a fire alarm that
                // will not stop. Rearms when the next service opens.
                if (interrupt == null && !_walkoutAlarmRaisedThisService &&
                    Interrupts.WalkoutStreakThreshold > 0 &&
                    _walkoutStreak >= Interrupts.WalkoutStreakThreshold)
                {
                    interrupt = new Interrupt(InterruptKind.WalkoutStreak, tick, now,
                        _walkoutStreak + " guests have walked out in a row — the kitchen is losing the room.",
                        _restaurant.Id);

                    _walkoutStreak = 0;
                    _walkoutAlarmRaisedThisService = true;
                }
            }

            // 2. Plates that finish this minute reach the pass.
            foreach (var table in _tables)
            {
                foreach (var order in table.Orders)
                {
                    if (order.Resolved || order.Ticket.CompletedTick > tick) continue;

                    order.Resolved = true;
                    Deliver(table, order);
                }
            }

            // 3. Tables that have finished eating give up their seats.
            for (var i = _tables.Count - 1; i >= 0; i--)
            {
                var table = _tables[i];
                if (!table.AllResolved) continue;

                if (!table.Settled)
                {
                    table.Settled = true;
                    table.SeatsFreeAt = table.WalkedOut ? tick : tick + DwellMinutes;
                }

                if (table.SeatsFreeAt <= tick)
                {
                    _occupiedSeats -= table.Party.Size;
                    _tables.RemoveAt(i);
                }
            }

            // 4. Anything that has been restocked can raise the alarm again next time.
            if (_stockoutsReported.Count > 0)
            {
                _stockoutsReported.RemoveWhere(id => _restaurant.Inventory.QuantityOf(id) > 0m);
            }

            // 5. New arrivals, but only while the doors are open.
            var window = CurrentWindow();
            if (window != null)
            {
                // Exactly one draw per open minute, whatever the chunk size — this is what
                // keeps a month-long jump identical to a minute-by-minute one.
                if (_rng.Chance(window.ArrivalChanceAt(now)))
                {
                    var stockoutInterrupt = Seat(tick, now);
                    if (interrupt == null) interrupt = stockoutInterrupt;
                }
            }

            // 6. Money.
            if (interrupt == null && Interrupts.CashFloor.HasValue)
            {
                var cash = ProjectedCash;

                if (cash < Interrupts.CashFloor.Value && !_cashFloorBreached)
                {
                    _cashFloorBreached = true;
                    interrupt = new Interrupt(InterruptKind.CashThreshold, tick, now,
                        "Cash has fallen to " + cash.ToString("0.00") + ", through your " +
                        Interrupts.CashFloor.Value.ToString("0.00") + " floor.", _restaurant.Id);
                }
                else if (cash >= Interrupts.CashFloor.Value)
                {
                    _cashFloorBreached = false;   // rearm once recovered
                }
            }

            return interrupt;
        }

        private Interrupt Seat(long tick, DateTime now)
        {
            var party = RollParty(tick);
            _partiesArrived++;

            if (_restaurant.SeatingCapacity > 0 && _occupiedSeats + party.Size > _restaurant.SeatingCapacity)
            {
                _partiesTurnedAway++;
                _diagnostics.Add("Turned away a party of " + party.Size + " — dining room full (" +
                                 _restaurant.SeatingCapacity + " seats).");
                return null;
            }

            _occupiedSeats += party.Size;

            var table = new Table(party);
            Interrupt interrupt = null;

            for (var cover = 0; cover < party.Size; cover++)
            {
                var recipeId = _restaurant.Menu.RecipeIds[_rng.Next(_restaurant.Menu.Count)];
                var recipe = _definitions.GetRecipe(recipeId);
                var ticket = _pass.Fire(recipe, tick, _restaurant.Inventory);

                _tickets.Add(ticket);

                if (!ticket.WasServed)
                {
                    _eightySixed++;
                    _diagnostics.Add(ticket.FailureReason);

                    // One stockout is ONE problem, however many tickets it takes down. Without
                    // this, running out of truffle at 19:00 interrupts on every order for the
                    // rest of the night — which is not a pulse, it is a stuck alarm. Rearmed
                    // the moment that ingredient is back on the shelf.
                    if (interrupt == null && Interrupts.StopOnStockout &&
                        ticket.Outcome == TicketOutcome.OutOfStock &&
                        ticket.FailedIngredientId != null &&
                        _stockoutsReported.Add(ticket.FailedIngredientId))
                    {
                        interrupt = new Interrupt(InterruptKind.IngredientStockout, tick, now,
                            ticket.FailureReason, ticket.FailedIngredientId);
                    }

                    continue;
                }

                // Ingredients are spent the moment the plate is fired, whatever happens next.
                _foodCost += _restaurant.Costing.PlateCost(recipeId);

                int running;
                _stationMinutes.TryGetValue(ticket.StationId, out running);
                _stationMinutes[ticket.StationId] = running + ticket.CookMinutes;

                table.Orders.Add(new Order(ticket, recipeId));
            }

            if (table.Orders.Count == 0)
            {
                // Nothing could be cooked for them at all — they leave immediately.
                _occupiedSeats -= party.Size;
            }
            else
            {
                _tables.Add(table);
            }

            return interrupt;
        }

        private void Deliver(Table table, Order order)
        {
            var recipe = _definitions.GetRecipe(order.RecipeId);
            var plateCost = _restaurant.Costing.PlateCost(order.RecipeId);

            if (table.WalkedOut)
            {
                _wastedFoodCost += plateCost;   // cooked, binned, unpaid for
                return;
            }

            if (order.Ticket.WaitMinutes > _longestWait) _longestWait = order.Ticket.WaitMinutes;

            var satisfaction = SatisfactionModel.Evaluate(
                table.Party, order.Ticket, recipe.Name,
                _restaurant.Costing.IngredientQuality(order.RecipeId),
                _restaurant.Costing.FoodCostRatio(order.RecipeId));

            int sold;
            _unitsSold.TryGetValue(order.RecipeId, out sold);
            _unitsSold[order.RecipeId] = sold + 1;

            _revenue += _restaurant.Costing.MenuPrice(order.RecipeId);
            _coversServed++;
            _satisfactionTotal += satisfaction.Overall;
            _walkoutStreak = 0;   // a served cover breaks the streak

            if (satisfaction.Overall < 0.6m) _diagnostics.Add(satisfaction.Diagnosis);
        }

        private CustomerParty RollParty(long tick)
        {
            _partyCounter++;

            return new CustomerParty(
                "party-" + _partyCounter,
                RollPartySize(),
                tick,
                _rng.Next(20, 36),
                0.8m + (decimal)_rng.NextDouble() * 0.4m);
        }

        private int RollPartySize()
        {
            var roll = _rng.NextDouble();

            if (roll < 0.15) return 1;
            if (roll < 0.60) return 2;
            if (roll < 0.80) return 3;
            if (roll < 0.95) return 4;

            return _rng.Next(5, 8);
        }

        private static ElapsedPeriods ElapsedBetween(long fromTick, long toTick)
        {
            var probe = new GameClock(GameClock.DefaultStartDate);
            if (fromTick > 0) probe.Advance(fromTick);

            return probe.Advance(toTick - fromTick);
        }

        private string BusiestStation()
        {
            string busiest = null;
            var most = -1;

            foreach (var pair in _stationMinutes)
            {
                if (pair.Value > most) { most = pair.Value; busiest = pair.Key; }
            }

            return busiest;
        }

        /// <summary>Minutes a party lingers after their last plate lands.</summary>
        public const int DwellMinutes = 35;

        // ---- Live state for guests in the room ----

        private sealed class Table
        {
            public Table(CustomerParty party)
            {
                Party = party;
                Orders = new List<Order>();
            }

            public CustomerParty Party { get; }
            public List<Order> Orders { get; }
            public bool WalkedOut { get; set; }
            public bool Settled { get; set; }
            public long SeatsFreeAt { get; set; }

            public bool AllResolved
            {
                get
                {
                    foreach (var order in Orders) { if (!order.Resolved) return false; }
                    return true;
                }
            }
        }

        private sealed class Order
        {
            public Order(Ticket ticket, string recipeId)
            {
                Ticket = ticket;
                RecipeId = recipeId;
            }

            public Ticket Ticket { get; }
            public string RecipeId { get; }
            public bool Resolved { get; set; }
        }
    }
}
