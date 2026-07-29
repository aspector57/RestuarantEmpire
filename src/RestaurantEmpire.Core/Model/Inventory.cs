using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Stock of one ingredient at one location, with its par level band.
    ///
    /// Par levels are the real industry tool (design doc Phase 2): a min/max band per
    /// item. Below min, you risk running out mid-service and 86'ing a dish. Above max,
    /// cash is tied up in a pantry. The tension between those two is the actual decision.
    /// </summary>
    public sealed class IngredientStock
    {
        public string IngredientId { get; }
        public decimal Quantity { get; private set; }
        public decimal ParMin { get; private set; }
        public decimal ParMax { get; private set; }

        internal IngredientStock(string ingredientId, decimal quantity, decimal parMin, decimal parMax)
        {
            IngredientId = ingredientId;
            Quantity = quantity;
            ParMin = parMin;
            ParMax = parMax;
        }

        public bool IsBelowPar { get { return Quantity < ParMin; } }
        public bool IsAbovePar { get { return ParMax > 0m && Quantity > ParMax; } }

        /// <summary>Days this keeps, learned on the daily sweep. Zero means it keeps.</summary>
        public int ShelfLifeDays { get; private set; }

        /// <summary>Roughly what gets used in a day, smoothed. Zero until a day has passed.</summary>
        public decimal DailyUsage { get; private set; }

        private decimal _usedSinceSweep;

        /// <summary>
        /// How much to order. Zero when in band, and for anything that PERISHES, capped at
        /// what will realistically be used before it turns.
        ///
        /// Par levels are a policy for things that keep. Topping a four-day fish up to a full
        /// shelf every time it dips means buying a shelf, using a fraction and binning the
        /// rest — measured at 94% of all food cost before this cap existed. That is not a
        /// difficulty setting, it is a broken order. With the cap, ordering to par is sane and
        /// OVER-BUYING BECOMES A CHOICE, which is the whole point.
        /// </summary>
        public decimal SuggestedReorderQuantity
        {
            get
            {
                if (!IsBelowPar) return 0m;

                var toPar = ParMax - Quantity;
                if (ShelfLifeDays <= 0 || DailyUsage <= 0m) return toPar;

                // A FEW DAYS' WORTH, not a shelf life's worth.
                //
                // This ordered `usage x shelfLife x 1.5`, which for a ten-day tomato is
                // fifteen days of stock — so the oldest thing on the shelf was always most of
                // the way through its life and freshness never recovered. Measured: a
                // premium-sourced restaurant capped at 0.609 standing against a 0.890 ceiling
                // purely because everything it served was days old.
                //
                // Order little and often. Four days is enough cover for a busy night and
                // keeps what reaches the pass inside the first half of its life, which is
                // where it still counts as fresh.
                var daysToHold = ShelfLifeDays < 4 ? ShelfLifeDays : 4;
                var usableBeforeItTurns = (DailyUsage * daysToHold * 1.25m) - Quantity;

                if (usableBeforeItTurns <= 0m) return 0m;
                return usableBeforeItTurns < toPar ? usableBeforeItTurns : toPar;
            }
        }

        /// <summary>Called once a day: learn the run rate and what this keeps for.</summary>
        internal void EndOfDay(int shelfLifeDays)
        {
            ShelfLifeDays = shelfLifeDays;
            DailyUsage = DailyUsage <= 0m ? _usedSinceSweep : (DailyUsage * 0.7m) + (_usedSinceSweep * 0.3m);
            _usedSinceSweep = 0m;
        }

        internal void SetPar(decimal parMin, decimal parMax)
        {
            if (parMin < 0m) throw new ArgumentOutOfRangeException(nameof(parMin), "Par minimum cannot be negative.");
            if (parMax < parMin) throw new ArgumentOutOfRangeException(nameof(parMax), "Par maximum cannot be below par minimum.");

            ParMin = parMin;
            ParMax = parMax;
        }

        /// <summary>
        /// Deliveries as DATED BATCHES rather than one total. Aaron: *"you are going to be
        /// buying more before you get to 0 in stock, so you need a way to use the oldest stuff
        /// first."* Exactly — and a single number with an average age would let each new
        /// delivery quietly refresh the old stock underneath it.
        /// </summary>
        private readonly List<Batch> _batches = new List<Batch>();

        private struct Batch { public decimal Quantity; public int ReceivedOnDay; }

        internal void Receive(decimal quantity, int onDay)
        {
            if (quantity < 0m) throw new ArgumentOutOfRangeException(nameof(quantity), "Cannot receive a negative quantity.");
            if (quantity == 0m) return;

            Quantity += quantity;
            _batches.Add(new Batch { Quantity = quantity, ReceivedOnDay = onDay });
        }

        internal bool TryConsume(decimal quantity)
        {
            if (quantity < 0m) throw new ArgumentOutOfRangeException(nameof(quantity), "Cannot consume a negative quantity.");
            if (Quantity < quantity) return false;

            Quantity -= quantity;
            _usedSinceSweep += quantity;

            // OLDEST FIRST, the way a kitchen rotates. This is what makes topping up before
            // you hit zero safe: the new delivery goes behind what is already there.
            var left = quantity;
            for (var i = 0; i < _batches.Count && left > 0m; i++)
            {
                var batch = _batches[i];
                var taken = batch.Quantity <= left ? batch.Quantity : left;
                batch.Quantity -= taken;
                left -= taken;
                _batches[i] = batch;
            }

            _batches.RemoveAll(b => b.Quantity <= 0m);
            return true;
        }

        /// <summary>Stock you already hold when a run starts is stock you have NOW.</summary>
        internal void RedateTo(int day)
        {
            if (_batches.Count == 0 && Quantity > 0m)
            {
                _batches.Add(new Batch { Quantity = Quantity, ReceivedOnDay = day });
                return;
            }

            for (var i = 0; i < _batches.Count; i++)
            {
                var batch = _batches[i];
                batch.ReceivedOnDay = day;
                _batches[i] = batch;
            }
        }

        /// <summary>
        /// How much of this is within <paramref name="days"/> of turning.
        ///
        /// Aaron: *"we should be able to see how much is about to turn bad, because you may
        /// need to order more still."* Exactly — the old readout said "oldest: 6d" and that
        /// tells you nothing about the size of the hole coming. You cannot plan around a hole
        /// you cannot measure.
        /// </summary>
        internal decimal QuantityTurningWithin(int days, int today, int shelfLifeDays)
        {
            if (shelfLifeDays <= 0) return 0m;

            var atRisk = 0m;
            foreach (var batch in _batches)
            {
                var daysLeft = shelfLifeDays - (today - batch.ReceivedOnDay);
                if (daysLeft <= days) atRisk += batch.Quantity;
            }

            return atRisk;
        }

        /// <summary>
        /// How fresh what is about to be COOKED is, 1 down to 0. Consumption is oldest-first,
        /// so this is the state of the oldest batch on the shelf — the thing the kitchen will
        /// actually reach for.
        ///
        /// Full marks for the first half of its life, then a slide. Nothing that is still
        /// legally food scores zero: the worst a guest gets is "that didn't taste fresh",
        /// which is the point — a gradient, not a cliff.
        /// </summary>
        internal decimal Freshness(int today, int shelfLifeDays)
        {
            if (shelfLifeDays <= 0 || _batches.Count == 0) return 1m;

            var oldest = 0;
            foreach (var batch in _batches)
            {
                var age = today - batch.ReceivedOnDay;
                if (age > oldest) oldest = age;
            }

            var throughLife = (decimal)oldest / shelfLifeDays;
            if (throughLife <= 0.5m) return 1m;

            // 1.0 at halfway, 0.55 the day it turns.
            var fresh = 1m - ((throughLife - 0.5m) * 0.9m);
            return fresh < 0.55m ? 0.55m : fresh;
        }

        /// <summary>Bin it deliberately, oldest first. Returns what was thrown out.</summary>
        internal decimal DiscardOldest(decimal quantity)
        {
            var tossed = 0m;
            var left = quantity;

            for (var i = 0; i < _batches.Count && left > 0m; i++)
            {
                var batch = _batches[i];
                var taken = batch.Quantity <= left ? batch.Quantity : left;

                batch.Quantity -= taken;
                left -= taken;
                tossed += taken;
                _batches[i] = batch;
            }

            _batches.RemoveAll(b => b.Quantity <= 0m);
            Quantity -= tossed;
            if (Quantity < 0m) Quantity = 0m;

            return tossed;
        }

        internal decimal DiscardSpoiled(int today, int shelfLifeDays)
        {
            if (shelfLifeDays <= 0) return 0m;   // it keeps

            var binned = 0m;
            for (var i = _batches.Count - 1; i >= 0; i--)
            {
                if (today - _batches[i].ReceivedOnDay < shelfLifeDays) continue;
                binned += _batches[i].Quantity;
                _batches.RemoveAt(i);
            }

            Quantity -= binned;
            if (Quantity < 0m) Quantity = 0m;

            return binned;
        }
    }

    /// <summary>What one restaurant currently has in the walk-in, and the par band for each item.</summary>
    public sealed class Inventory
    {
        private readonly DefinitionRegistry _definitions;
        private readonly Dictionary<string, IngredientStock> _stock;

        internal Inventory(DefinitionRegistry definitions)
        {
            _definitions = definitions;
            _stock = new Dictionary<string, IngredientStock>();
        }

        public IEnumerable<IngredientStock> Items { get { return _stock.Values; } }

        public IngredientStock this[string ingredientId] { get { return GetOrCreate(ingredientId); } }

        public IngredientStock GetOrCreate(string ingredientId)
        {
            if (!_definitions.HasIngredient(ingredientId))
                throw new DefinitionNotFoundException("ingredient", ingredientId);

            IngredientStock stock;
            if (!_stock.TryGetValue(ingredientId, out stock))
            {
                stock = new IngredientStock(ingredientId, 0m, 0m, 0m);
                _stock[ingredientId] = stock;
            }

            return stock;
        }

        /// <summary>
        /// What day the pantry thinks it is; deliveries are dated with it.
        ///
        /// TRAP: the clock's tick is ABSOLUTE, so its day index runs to the tens of thousands
        /// while stock loaded before a run is dated zero. Without <see cref="StartOfRun"/> the
        /// first tick bins the entire pantry as decades old.
        /// </summary>
        public int Today { get; private set; }

        /// <summary>Join the calendar, treating what is on the shelf as being here now.</summary>
        public void StartOfRun(int day)
        {
            Today = day;
            foreach (var stock in _stock.Values) stock.RedateTo(day);
        }

        /// <summary>Move the calendar on without re-dating anything already in the pantry.</summary>
        public void AdvanceTo(int day) { Today = day; }

        /// <summary>Bin everything past its shelf life, reporting what was lost per ingredient.</summary>
        public IDictionary<string, decimal> DiscardSpoiled(int today, Definitions.DefinitionRegistry definitions)
        {
            var binned = new Dictionary<string, decimal>();
            if (definitions == null) return binned;

            foreach (var stock in _stock.Values)
            {
                // A missing definition must never take the game down (Architecture Rule 3).
                int shelfLife;
                try { shelfLife = definitions.GetIngredient(stock.IngredientId).ShelfLifeDays; }
                catch (Definitions.DefinitionNotFoundException) { continue; }

                var lost = stock.DiscardSpoiled(today, shelfLife);
                stock.EndOfDay(shelfLife);

                if (lost > 0m) binned[stock.IngredientId] = lost;
            }

            return binned;
        }

        /// <summary>How much of this ingredient turns within the next few days.</summary>
        public decimal TurningWithin(string ingredientId, int days, Definitions.DefinitionRegistry definitions)
        {
            IngredientStock stock;
            if (!_stock.TryGetValue(ingredientId ?? string.Empty, out stock)) return 0m;

            try { return stock.QuantityTurningWithin(days, Today, definitions.GetIngredient(ingredientId).ShelfLifeDays); }
            catch (Definitions.DefinitionNotFoundException) { return 0m; }
        }

        /// <summary>How fresh the stock the kitchen will reach for is, 0 to 1.</summary>
        public decimal FreshnessOf(string ingredientId, Definitions.DefinitionRegistry definitions)
        {
            IngredientStock stock;
            if (!_stock.TryGetValue(ingredientId ?? string.Empty, out stock)) return 1m;

            try { return stock.Freshness(Today, definitions.GetIngredient(ingredientId).ShelfLifeDays); }
            catch (Definitions.DefinitionNotFoundException) { return 1m; }
        }

        /// <summary>
        /// Throw something out on purpose, oldest first. Only a decision worth having because
        /// tired stock now tastes tired — otherwise serving it would always beat binning it.
        /// </summary>
        public decimal Discard(string ingredientId, decimal quantity)
        {
            IngredientStock stock;
            if (!_stock.TryGetValue(ingredientId ?? string.Empty, out stock)) return 0m;

            return stock.DiscardOldest(quantity);
        }

        public void SetPar(string ingredientId, decimal parMin, decimal parMax)
        {
            GetOrCreate(ingredientId).SetPar(parMin, parMax);
        }

        public void Receive(string ingredientId, decimal quantity)
        {
            GetOrCreate(ingredientId).Receive(quantity, Today);
        }

        /// <summary>Returns false rather than throwing when stock is short — an 86'd dish, not a crash.</summary>
        public bool TryConsume(string ingredientId, decimal quantity)
        {
            return GetOrCreate(ingredientId).TryConsume(quantity);
        }

        /// <summary>
        /// Consumes a whole recipe's ingredient list ATOMICALLY: every line is checked
        /// before anything is taken, so a dish that can't be made leaves the walk-in
        /// untouched rather than half-consumed.
        ///
        /// Reports which ingredient came up short, because "86'd — out of mozzarella" is a
        /// usable diagnosis and "couldn't make it" is not.
        /// </summary>
        public bool TryConsumeAll(IEnumerable<RecipeIngredient> lines, out string shortfallIngredientId)
        {
            shortfallIngredientId = null;
            if (lines == null) return true;

            var required = new Dictionary<string, decimal>(StringComparer.Ordinal);

            foreach (var line in lines)
            {
                decimal running;
                required.TryGetValue(line.IngredientId, out running);
                required[line.IngredientId] = running + line.Quantity;
            }

            foreach (var pair in required)
            {
                if (QuantityOf(pair.Key) < pair.Value)
                {
                    shortfallIngredientId = pair.Key;
                    return false;
                }
            }

            foreach (var pair in required) GetOrCreate(pair.Key).TryConsume(pair.Value);

            return true;
        }

        public decimal QuantityOf(string ingredientId)
        {
            IngredientStock stock;
            return _stock.TryGetValue(ingredientId ?? string.Empty, out stock) ? stock.Quantity : 0m;
        }

        /// <summary>Everything currently under its par minimum — the restock list.</summary>
        public IEnumerable<IngredientStock> BelowPar
        {
            get
            {
                foreach (var stock in _stock.Values)
                {
                    if (stock.IsBelowPar) yield return stock;
                }
            }
        }
    }
}
