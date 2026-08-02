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

        /// <summary>
        /// A SEPARATE stream for things going wrong, deliberately.
        ///
        /// Drawing mishaps from the main sequence would shift every arrival and every dish
        /// choice after it, so adding the mechanic would silently rewrite the outcome of every
        /// seeded test in the project and none of the differences would mean anything. A
        /// second stream keeps who walks in and what they order exactly as they were.
        /// </summary>
        private readonly DeterministicRandom _mishaps;

        /// <summary>
        /// A third stream, for a guest's own judgement at the door. Kept apart from arrivals
        /// for the same reason mishaps are: a draw added to the main sequence shifts every
        /// arrival after it and rewrites the outcome of every seeded test for no reason.
        /// </summary>
        private readonly DeterministicRandom _judgement;
        private readonly KitchenPass _pass;

        private readonly List<Table> _tables = new List<Table>();
        private readonly Dictionary<string, int> _unitsSold = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Ticket> _tickets = new List<Ticket>();
        private readonly List<string> _diagnostics = new List<string>();
        private readonly Dictionary<string, int> _stationMinutes = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly HashSet<string> _stockoutsReported = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _recentWalkoutStations = new Dictionary<string, int>(StringComparer.Ordinal);

        private decimal _revenue, _foodCost, _wastedFoodCost, _satisfactionTotal, _laborCost;
        private int _remakes, _comped;
        private int _lastSpoilageDay = -1;
        private decimal _spoiledCost;
        private decimal _compedValue;
        private int _partiesArrived, _partiesTurnedAway, _coversServed, _walkouts, _eightySixed, _longestWait;
        private int _partiesLostToMenu, _partiesPutOffByTheWait, _partiesPutOffByThePrices;
        private int _lostTradeStreak, _partyCounter, _occupiedSeats;
        private bool _cashFloorBreached, _walkoutAlarmRaisedThisService;
        private string _serviceOnLastTick;

        public SimulationRunner(Restaurant restaurant, GameClock clock, long seed, InterruptPolicy interrupts = null)
        {
            if (restaurant == null) throw new ArgumentNullException(nameof(restaurant));
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            _restaurant = restaurant;
            _definitions = restaurant.Company.Definitions;
            _rng = new DeterministicRandom(seed);
            _mishaps = new DeterministicRandom(seed ^ 0x5EED1E);
            _judgement = new DeterministicRandom(seed ^ 0x1DEA5);

            _lastSpoilageDay = (int)(clock.Tick / GameClock.TicksPerDay);
            restaurant.Inventory.StartOfRun(_lastSpoilageDay);
            // Plate capacity rather than headcount, so who you hired decides how much of the
            // kitchen actually runs.
            _pass = restaurant.Kitchen.OpenPass(clock.Tick,
                restaurant.Payroll.PlateCapacity(KitchenPass.PlatesPerCook));

            // A wide card slows the pass down. Set once for the run; changing the menu
            // mid-service is not a thing anyone does.
            _pass.ComplexityLoad = restaurant.Menu.ComplexityLoad;

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
            get { return _restaurant.Company.Economy.CashOnHand + _revenue - _foodCost - _laborCost; }
        }

        /// <summary>Everything that has happened since this runner started.</summary>
        public ServiceResult Snapshot()
        {
            return new ServiceResult(
                new Dictionary<string, int>(_unitsSold), _tickets, _diagnostics,
                _revenue, _foodCost, _wastedFoodCost, _laborCost,
                _partiesArrived, _partiesTurnedAway, _partiesLostToMenu, _partiesPutOffByTheWait, _partiesPutOffByThePrices,
                _coversServed, _walkouts, _eightySixed,
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

            // A new day: bin whatever went off overnight. Once a day at a fixed boundary, so a
            // month advanced in one call spoils exactly as thirty separate days do.
            var today = (int)(tick / GameClock.TicksPerDay);
            if (today != _lastSpoilageDay)
            {
                _lastSpoilageDay = today;
                _restaurant.Inventory.AdvanceTo(today);
                BinWhatWentOff(today);

                // The standing order goes in before service. Ordering is a policy you set,
                // not a chore you perform — see Restaurant.StandingOrder.
                if (_restaurant.StandingOrder) _restaurant.OrderStockToPar(tick);
            }

            Interrupt interrupt = null;

            // A new service is a clean slate for the alarms that are per-service.
            var serviceNow = CurrentWindow() == null ? null : CurrentWindow().Name;
            if (serviceNow != _serviceOnLastTick)
            {
                _serviceOnLastTick = serviceNow;
                _walkoutAlarmRaisedThisService = false;
                _lostTradeStreak = 0;
                _recentWalkoutStations.Clear();
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
                    _lostTradeStreak++;

                    // People who leave hungry talk too, and a place that seats you and then
                    // loses you is exactly the sort of thing that gets talked about.
                    _restaurant.Reputation.RecordWalkout(_restaurant.ReputationCeiling);

                    int blamed;
                    _recentWalkoutStations.TryGetValue(order.Ticket.StationId, out blamed);
                    _recentWalkoutStations[order.Ticket.StationId] = blamed + 1;
                    _diagnostics.Add("Walked out after " + (tick - table.Party.ArrivalTick) +
                        " min waiting for " + _definitions.GetRecipe(order.RecipeId).Name +
                        " (patience " + table.Party.PatienceMinutes + " min; the " + order.Ticket.StationId +
                        " station was still " + Math.Max(0, order.Ticket.StartedTick - tick) +
                        " min from even starting it).");
                }

                // Once per service, not once per streak. A kitchen that is underwater all
                // night is ONE problem the player needs told about, not a fire alarm that
                // will not stop. Rearms when the next service opens.
                if (interrupt == null && !_walkoutAlarmRaisedThisService &&
                    Interrupts.WalkoutStreakThreshold > 0 &&
                    _lostTradeStreak >= Interrupts.WalkoutStreakThreshold)
                {
                    interrupt = new Interrupt(InterruptKind.WalkoutStreak, tick, now,
                        DescribeTheBottleneck(_lostTradeStreak), BusiestWalkoutStation());

                    _lostTradeStreak = 0;
                    _walkoutAlarmRaisedThisService = true;
                    _recentWalkoutStations.Clear();
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

            // 5. Wages tick along for every minute the doors are open. This is the first
            // time labor is GENERATED by the simulation rather than approximated by a
            // caller, which is what finally makes prime cost honest.
            var window = CurrentWindow();
            if (window != null) _laborCost += _restaurant.Payroll.HourlyWageBill / 60m;

            // 6. New arrivals, but only while the doors are open.
            if (window != null)
            {
                // How busy it is comes from the street, not from the window. Opening over
                // dead hours is allowed and simply produces nobody.
                //
                // Exactly one draw per open minute, whatever the chunk size — this is what
                // keeps a month-long jump identical to a minute-by-minute one.
                // Word of mouth scales the street's own footfall. Exactly one draw per open
                // minute still, whatever the chunk size, so a month-long jump stays identical
                // to a minute-by-minute one — the multiplier changes the odds, never the
                // number of rolls.
                var footfall = _restaurant.TrafficAt(now)
                             * (double)_restaurant.Reputation.TrafficMultiplier
                             * (double)MenuDrawAt(now);

                if (_rng.Chance(footfall / 60.0))
                {
                    var stockoutInterrupt = Seat(tick, now);
                    if (interrupt == null) interrupt = stockoutInterrupt;
                }
            }

            // 7. Money.
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
                else if (cash >= Interrupts.CashFloor.Value + Interrupts.CashRearmMargin)
                {
                    _cashFloorBreached = false;   // rearm once recovered
                }
            }

            return interrupt;
        }

        private Interrupt Seat(long tick, DateTime now)
        {
            var party = RollParty(tick, now);

            // Null means this sort of person would not have come here at these prices, so
            // there is nobody at the door to seat. Already counted where the decision was made.
            if (party == null) return null;
            _partiesArrived++;

            // "No seats" means unlimited ONLY in a building whose floor was never measured —
            // a food truck, a ghost kitchen, a bare test fixture. In a real unit, no tables
            // means nobody sits down. Without that distinction, failing to fit your tables
            // silently removed the constraint and looked like a triumph.
            var seats = _restaurant.SeatingCapacity;

            // Tables you cannot staff are tables nobody sits at. A payroll with no servers
            // caps the room at zero however many chairs you bought.
            if (_restaurant.Payroll.Headcount > 0)
            {
                var servable = _restaurant.ServableSeats;
                if (servable < seats) seats = servable;
            }

            var unconstrained = seats == 0 && _restaurant.FloorArea <= 0m && _restaurant.Payroll.Headcount == 0;

            if (!unconstrained && _occupiedSeats + party.Size > seats)
            {
                _partiesTurnedAway++;
                _lostTradeStreak += party.Size;
                var staffBound = _restaurant.Payroll.Headcount > 0 &&
                                 _restaurant.ServableSeats < _restaurant.SeatingCapacity;

                if (seats == 0)
                    _diagnostics.Add("Turned away a party of " + party.Size +
                        " — nowhere to sit, or nobody to serve them.");
                else if (staffBound)
                    _diagnostics.Add("Turned away a party of " + party.Size + " — only " + seats +
                        " covers is all the floor staff can handle, though you have " +
                        _restaurant.SeatingCapacity + " seats.");
                else
                    _diagnostics.Add("Turned away a party of " + party.Size +
                        " — dining room full (" + seats + " seats).");
                return null;
            }

            // What this restaurant can actually put in front of someone right now: on the
            // menu, wanted at this hour, and cookable with the equipment installed. A dish
            // whose station was never bought is not really on the menu at all.
            var wanted = new List<string>();
            var appetites = new List<decimal>();
            string missingStation = null;

            // What the average dish here costs, so a price can be judged against its menu
            // rather than against an absolute the guest could not know.
            var averagePrice = 0m;
            var priced = 0;
            foreach (var recipeId in _restaurant.Menu.RecipeIds)
            {
                averagePrice += _restaurant.Costing.MenuPrice(recipeId);
                priced++;
            }
            if (priced > 0) averagePrice /= priced;

            foreach (var recipeId in _restaurant.Menu.RecipeIds)
            {
                var candidate = _definitions.GetRecipe(recipeId);

                if (!candidate.SuitsDaypart(Dayparts.At(now))) continue;

                if (!_restaurant.Kitchen.HasStation(candidate.StationId))
                {
                    missingStation = candidate.StationId;
                    continue;
                }

                var price = _restaurant.Costing.MenuPrice(recipeId);
                var relative = averagePrice <= 0m ? 1m : price / averagePrice;

                var appetite = party.AppetiteFor(candidate, relative,
                    _restaurant.Costing.IngredientQuality(recipeId));
                if (_restaurant.Menu.IsFeatured(recipeId)) appetite *= Menu.FeaturedWeight;

                wanted.Add(recipeId);
                appetites.Add(appetite);
            }

            if (wanted.Count == 0)
            {
                // They came in, read the menu, and left. You paid the labor anyway.
                _partiesLostToMenu++;

                _diagnostics.Add(missingStation != null
                    ? "A party of " + party.Size + " left without ordering — the only thing they wanted needs a '" +
                      missingStation + "' station and this kitchen has none."
                    : "A party of " + party.Size + " left without ordering — nothing on the menu suits " +
                      Dayparts.At(now).ToString().ToLowerInvariant() + ".");

                return null;
            }

            // Two things a guest can judge from the doorway, before committing to anything.
            var costing = _restaurant.Costing;
            var totalWait = 0L;
            var totalValue = 0m;

            foreach (var recipeId in wanted)
            {
                totalWait += _pass.EstimatedWaitMinutes(_definitions.GetRecipe(recipeId), tick, party.Size);
                totalValue += SatisfactionModel.ScoreValue(costing.Markup(recipeId),
                    party.PriceSensitivity, costing.IngredientQuality(recipeId),
                    _restaurant.Reputation.Standing);
            }

            // The typical wait for whatever they end up ordering, not the luckiest case —
            // they pick at random, so an optimistic estimate just seats people who will walk.
            //
            // Then it is quoted OPTIMISTICALLY, because that is what actually happens: the
            // host says twenty minutes and means it, and the kitchen disagrees. Without this
            // nobody ever walks out at all — with a perfect quote a guest either waits
            // happily or never sits down, and "the kitchen is losing the room" becomes
            // impossible. Over-promising is the mechanism that makes walkouts real.
            var expectedWait = (totalWait / wanted.Count) * QuotedWaitOptimism / 100;

            if (expectedWait > party.PatienceMinutes)
            {
                _partiesPutOffByTheWait++;
                _lostTradeStreak += party.Size;

                // Blame whichever station is making them wait, so the interrupt can name it.
                var slowest = wanted[0];
                foreach (var recipeId in wanted)
                {
                    if (_pass.EstimatedWaitMinutes(_definitions.GetRecipe(recipeId), tick, party.Size) >
                        _pass.EstimatedWaitMinutes(_definitions.GetRecipe(slowest), tick, party.Size)) slowest = recipeId;
                }

                var blamedStation = _definitions.GetRecipe(slowest).StationId;
                int seen;
                _recentWalkoutStations.TryGetValue(blamedStation, out seen);
                _recentWalkoutStations[blamedStation] = seen + party.Size;

                _diagnostics.Add("A party of " + party.Size + " saw the wait (about " +
                                 expectedWait + " min) and went somewhere else.");
                return null;
            }

            // And they can read the prices. Nobody has to eat an overpriced dinner to work
            // out it is overpriced — which is what makes gouging cost you trade rather than
            // being free money.
            // A chance rather than a wall, so a dear menu bleeds custom instead of emptying
            // the room the moment it crosses a line.
            var looksWorthIt = totalValue / wanted.Count;
            var readsTheDoor = _restaurant.Location == null ? 0.5 : _restaurant.Location.MenuReadAtTheDoor;

            if (_judgement.Chance((double)SatisfactionModel.WalkAwayChance(looksWorthIt) * readsTheDoor))
            {
                _partiesPutOffByThePrices++;
                _diagnostics.Add("A party of " + party.Size + " read the prices and left.");
                return null;
            }

            _occupiedSeats += party.Size;

            var table = new Table(party);
            Interrupt interrupt = null;

            for (var cover = 0; cover < party.Size; cover++)
            {
                var recipeId = PickByAppetite(wanted, appetites);
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

                // THINGS GO WRONG, AND THEY GO WRONG MORE OFTEN IN A CHEAP KITCHEN.
                //
                // Aaron: "cheap is not accounting for like bad attitude or mistakes, for
                // example someone's food is bad and requests a refund, or they burn the food
                // and have to remake it." Exactly right, and it was why hiring badly was free:
                // a weak brigade only meant a slightly smaller multiplier, never a bill.
                //
                // A burnt plate costs the ingredients twice, the pass the time to do it again,
                // and — if it reached the table before anyone noticed — the cover itself.
                if (_mishaps.Chance((double)MistakeChance(recipe)))
                {
                    _foodCost += _restaurant.Costing.PlateCost(recipeId);
                    _wastedFoodCost += _restaurant.Costing.PlateCost(recipeId);
                    _remakes++;

                    // Doing it again takes the pass as long as doing it the first time, so a
                    // sloppy kitchen backs itself up and the queue is its own punishment.
                    _pass.Fire(recipe, tick, _restaurant.Inventory);

                    if (_mishaps.NextDouble() < 0.35)
                    {
                        // It got as far as the guest. That one is not being charged for.
                        _comped++;
                        _compedValue += _restaurant.Costing.MenuPrice(recipeId);
                        _revenue -= _restaurant.Costing.MenuPrice(recipeId);
                        _diagnostics.Add("Sent back — " + recipe.Name + " went out wrong and was comped.");
                    }
                }

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
                SatisfactionModel.PlateQuality(
                    _restaurant.Costing.IngredientQuality(order.RecipeId),
                    _restaurant.Payroll.AverageSkill(StaffRole.Cook),
                    _restaurant.Costing.Freshness(order.RecipeId, _restaurant.Inventory)),
                _restaurant.Costing.Markup(order.RecipeId),
                _restaurant.DiningRoom.Comfort,
                _restaurant.Reputation.Standing);

            int sold;
            _unitsSold.TryGetValue(order.RecipeId, out sold);
            _unitsSold[order.RecipeId] = sold + 1;

            _revenue += _restaurant.Costing.MenuPrice(order.RecipeId);
            _coversServed++;
            _satisfactionTotal += satisfaction.Overall;
            _restaurant.Reputation.RecordMeal(satisfaction.Overall, _restaurant.ReputationCeiling);

            // Hours on the pass. A busy restaurant trains people faster than a quiet one,
            // which is true and means the same hire is worth more to a place that trades.
            _restaurant.Payroll.Worked();
            _lostTradeStreak = 0;              // a served cover breaks the streak
            _recentWalkoutStations.Clear();  // and the blame tally that describes it

            if (satisfaction.Overall < 0.6m) _diagnostics.Add(satisfaction.Diagnosis);
        }

        /// <summary>
        /// How much this menu pulls the crowd that is out at this hour — the reward for being
        /// a particular kind of restaurant rather than a bit of everything.
        ///
        /// Menu fit used to change only what a SEATED guest ordered, never whether anyone came,
        /// so a fine-dining room and a pizzeria drew the identical street and specialising was
        /// strictly worse than hedging. Measured across six strategies and four markets, one
        /// generalist won all four. Fitting the neighborhood now brings people in, and failing
        /// to fit it leaves the room quiet.
        /// </summary>
        private decimal MenuDrawAt(DateTime now)
        {
            if (_restaurant.Menu.Count == 0) return 1m;

            var likely = ArchetypeProfile.LikelyAt(Dayparts.At(now),
                _restaurant.Location == null ? "" : _restaurant.Location.Id);
            if (likely.Length == 0) return 1m;

            var total = 0m;
            for (var i = 0; i < likely.Length; i++) total += _restaurant.Menu.AppealTo(likely[i]);

            // Damped: the card shifts the street's traffic, it does not replace it. A menu
            // nobody out tonight wants still gets passers-by; a perfect fit is not a queue
            // around the block.
            return 0.55m + ((total / likely.Length) * 0.45m);
        }

        /// <summary>
        /// Draws from the crowd that is out, weighted by how much each sort of person likes
        /// the look of the menu. Consumes exactly one number, like the uniform draw it
        /// replaced, so chunk-size invariance is unaffected.
        /// </summary>
        private CustomerArchetype PickWhoWalksIn(CustomerArchetype[] likely)
        {
            if (likely.Length == 1) return likely[0];

            var weights = new decimal[likely.Length];
            var total = 0m;
            for (var i = 0; i < likely.Length; i++)
            {
                var w = _restaurant.Menu.AppealTo(likely[i]);
                weights[i] = w;
                total += w;
            }

            if (total <= 0m) return likely[_rng.Next(likely.Length)];

            var roll = (decimal)_rng.NextDouble() * total;
            var running = 0m;
            for (var i = 0; i < likely.Length; i++)
            {
                running += weights[i];
                if (roll < running) return likely[i];
            }

            return likely[likely.Length - 1];
        }

        private CustomerParty RollParty(long tick, DateTime now)
        {
            _partyCounter++;

            // Who is out at this hour, in this sort of place. A business district at one
            // o'clock is not a nightlife quarter at midnight.
            var likely = ArchetypeProfile.LikelyAt(Dayparts.At(now),
                _restaurant.Location == null ? "" : _restaurant.Location.Id);

            // WHICH of them walks in is decided by the card. A room serving truffle and sea
            // bass fills with the people who came for truffle and sea bass; the same street
            // with a pizza list fills with families. One draw, exactly as before, so the
            // arrival sequence keeps its shape.
            var archetype = PickWhoWalksIn(likely);
            var profile = ArchetypeProfile.For(archetype);

            // Would this sort of person come here at all, at these prices? Decided BEFORE they
            // arrive, because that is when people decide — they know roughly what a place
            // costs. Drawn from the judgement stream so it cannot shift arrivals or mishaps.
            var pricePosition = _restaurant.Costing.PricePosition(_restaurant.Menu.RecipeIds);
            if (!_judgement.Chance((double)profile.WouldConsider(pricePosition, _restaurant.Reputation.Standing)))
            {
                _partiesPutOffByThePrices++;
                return null;
            }

            // Most people have one thing they particularly love.
            var tastes = new List<string>();
            if (_rng.Chance(0.45))
                tastes.Add(ArchetypeProfile.TastesWorthHaving[_rng.Next(ArchetypeProfile.TastesWorthHaving.Length)]);

            var sensitivity = profile.PriceSensitivity * (0.9m + ((decimal)_rng.NextDouble() * 0.2m));

            return new CustomerParty(
                "party-" + _partyCounter,
                RollPartySize(),
                tick,
                _rng.Next(profile.PatienceLow, profile.PatienceHigh + 1),
                sensitivity,
                archetype,
                tastes);
        }

        /// <summary>Weighted choice — a dish twice as appealing is ordered twice as often.</summary>
        private string PickByAppetite(List<string> options, List<decimal> weights)
        {
            var total = 0m;
            for (var i = 0; i < weights.Count; i++) total += weights[i];

            if (total <= 0m) return options[_rng.Next(options.Count)];

            var roll = (decimal)_rng.NextDouble() * total;
            var running = 0m;

            for (var i = 0; i < options.Count; i++)
            {
                running += weights[i];
                if (roll < running) return options[i];
            }

            return options[options.Count - 1];
        }

        /// <summary>
        /// How often THIS plate goes wrong, from who is on the pass and what they were asked
        /// to make.
        ///
        /// Aaron: *"cheap labor can also be good... maybe they excel with simpler dishes and
        /// struggle with more complex ones."* That is the version worth having, and prep time
        /// is already a complexity measure sitting in the recipe. A weak brigade plating a
        /// four-minute caprese is very nearly fine; the same brigade on a sixteen-minute
        /// truffle risotto is not, and the shortfall is squared so that being a bit cheap is
        /// survivable and being very cheap is not.
        ///
        /// **This is what makes a small menu a real strategy rather than the strictly worse
        /// one it measured as before.** A cheap kitchen can run pizza and salad honestly. It
        /// cannot run a tasting menu, and finding that out costs ingredients.
        /// </summary>
        private decimal MistakeChance(Definitions.RecipeDefinition recipe)
        {
            var skill = _restaurant.Payroll.AverageSkill(StaffRole.Cook);
            var shortfall = 1m - skill;

            // 1.0 at twelve minutes, so most of the card sits either side of "demanding".
            var demand = recipe.PrepMinutes / 12m;

            // A wide card is more to hold in your head, so more goes wrong on it.
            var chance = (0.01m + (shortfall * shortfall * 0.10m * demand)) * _restaurant.Menu.ComplexityLoad;
            return chance < 0m ? 0m : chance > 0.30m ? 0.30m : chance;
        }

        /// <summary>
        /// Throw out what has gone off, and charge it. Booked to food cost, because it is
        /// food you bought and did not sell, and reported separately so the player can see
        /// the number rather than only feel it.
        /// </summary>
        private void BinWhatWentOff(int today)
        {
            var lost = _restaurant.Inventory.DiscardSpoiled(today, _definitions);
            if (lost.Count == 0) return;

            var cost = 0m;
            var worst = "";
            var most = 0m;

            foreach (var pair in lost)
            {
                cost += pair.Value * _restaurant.SupplierPolicy.UnitPriceFor(pair.Key);
                if (pair.Value > most) { most = pair.Value; worst = pair.Key; }
            }

            if (cost <= 0m) return;

            _spoiledCost += cost;
            _wastedFoodCost += cost;
            _foodCost += cost;

            _diagnostics.Add("Threw out " + cost.ToString("N2") + " of stock that had gone off" +
                (worst.Length > 0 ? " — mostly " + worst + "." : "."));
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

        /// <summary>Which station most of the recent walkouts were waiting on.</summary>
        private string BusiestWalkoutStation()
        {
            string worst = null;
            var most = 0;

            foreach (var pair in _recentWalkoutStations)
            {
                if (pair.Value > most) { most = pair.Value; worst = pair.Key; }
            }

            return worst;
        }

        /// <summary>
        /// The interrupt's reasoning, and the specific move available.
        ///
        /// The design's Tier-2 Advisor pattern: a stop should carry WHY and WHAT CAN BE DONE,
        /// not just WHAT WENT WRONG. "The kitchen is losing the room" is true and useless;
        /// the player then has to infer the bottleneck from complaint text and go hunting for
        /// a price. Everything below was already known at the moment the alarm fired.
        /// </summary>
        private string DescribeTheBottleneck(int streak)
        {
            var opening = "You have lost " + streak + " covers in a row — walked out, put off by the wait, or turned away.";

            var station = BusiestWalkoutStation();
            if (station == null) return opening;

            int blamed;
            _recentWalkoutStations.TryGetValue(station, out blamed);

            var why = " The " + station + " is the bottleneck — " + blamed + " of them were waiting on it.";

            // What it would cost to do something about it, straight from the catalogue.
            EquipmentDefinition cheapest = null;
            foreach (var option in _definitions.EquipmentFor(station))
            {
                if (cheapest == null || option.Cost < cheapest.Cost) cheapest = option;
            }

            if (cheapest == null) return opening + why;

            var room = _restaurant.HasRoomFor(cheapest.Footprint)
                ? ""
                : " — but the floor is full, so it would mean upgrading rather than adding.";

            return opening + why + " Another " + cheapest.Name + " is " +
                   cheapest.Cost.ToString("N0") + "; you have " + ProjectedCash.ToString("N0") + room;
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

        /// <summary>
        /// How honest the quoted wait is, as a percentage of the real one. Hosts under-quote
        /// slightly, which is why the occasional guest still walks out.
        ///
        /// Deliberately only SLIGHTLY. A big optimism gap turns every undersized kitchen into
        /// mass walkouts, which a hundred-run sweep showed produces five times more walkouts
        /// than covers served and a 500% prime cost. That is not how restaurants fail. A
        /// slammed restaurant stops seating people — you lose the trade at the door, which
        /// costs you the revenue but not the food.
        /// </summary>
        public const int QuotedWaitOptimism = 95;

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
