using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// THE assignment record: which supplier the company currently buys each ingredient from.
    ///
    /// This is the single most load-bearing class in the project. It exists because
    /// Restaurant Empire II made a supplier change a per-recipe editing chore, and that
    /// was its most-criticised flaw.
    ///
    /// The contract (CLAUDE.md Architecture Rule 1 / design doc Phase 6.3):
    ///   - ONE assignment per ingredient, held here and nowhere else.
    ///   - MANY live readers. Recipes, restaurants and the menu-engineering matrix all
    ///     read through this at the moment they need a number.
    ///   - NO snapshots. Nothing copies an assignment or a price out of here, so nothing
    ///     can go stale and there is no "refresh" step to forget.
    ///
    /// One policy lives on the Company, so a single write reaches every location at once.
    /// </summary>
    public sealed class SupplierPolicy
    {
        private readonly DefinitionRegistry _definitions;
        private readonly Dictionary<string, string> _assignments;

        internal SupplierPolicy(DefinitionRegistry definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _definitions = definitions;
            _assignments = new Dictionary<string, string>();
        }

        /// <summary>Current ingredient id -> supplier id assignments. Read-only; write via <see cref="Assign"/>.</summary>
        public IReadOnlyDictionary<string, string> Assignments { get { return _assignments; } }

        /// <summary>
        /// The single write. Points one ingredient at one supplier.
        ///
        /// Every recipe using that ingredient — in every restaurant, present and future —
        /// costs differently from the next read onward. Nothing else is touched, and no
        /// recipe needs editing. That is the entire point.
        /// </summary>
        public void Assign(string ingredientId, string supplierId)
        {
            if (!_definitions.HasIngredient(ingredientId))
                throw new DefinitionNotFoundException("ingredient", ingredientId);

            var supplier = _definitions.GetSupplier(supplierId);

            if (!supplier.Carries(ingredientId))
            {
                throw new InvalidOperationException(
                    "Supplier '" + supplierId + "' does not carry ingredient '" + ingredientId +
                    "', so it cannot be assigned to supply it.");
            }

            _assignments[ingredientId] = supplierId;
        }

        /// <summary>
        /// Convenience for setup and for "move my whole book to one supplier": assigns every
        /// ingredient that supplier carries. Still one write per ingredient, still no caching.
        /// </summary>
        public void AssignAll(string supplierId)
        {
            var supplier = _definitions.GetSupplier(supplierId);

            foreach (var ingredientId in supplier.CarriedIngredientIds)
            {
                if (_definitions.HasIngredient(ingredientId))
                    _assignments[ingredientId] = supplierId;
            }
        }

        public bool IsAssigned(string ingredientId)
        {
            return ingredientId != null && _assignments.ContainsKey(ingredientId);
        }

        public string GetSupplierIdFor(string ingredientId)
        {
            string supplierId;
            if (!_assignments.TryGetValue(ingredientId ?? string.Empty, out supplierId))
            {
                throw new InvalidOperationException(
                    "No supplier is assigned for ingredient '" + ingredientId +
                    "'. Assign one before costing a recipe that uses it.");
            }

            return supplierId;
        }

        public SupplierDefinition GetSupplierFor(string ingredientId)
        {
            return _definitions.GetSupplier(GetSupplierIdFor(ingredientId));
        }

        /// <summary>
        /// Live unit price for an ingredient under the current assignment. Computed on every
        /// call — there is intentionally no cached price field anywhere in this class.
        /// </summary>
        public decimal UnitPriceFor(string ingredientId)
        {
            return GetSupplierFor(ingredientId).UnitPriceFor(ingredientId);
        }

        /// <summary>Ingredients on the books with no supplier assigned — these block costing.</summary>
        public IEnumerable<string> UnassignedIngredientIds
        {
            get
            {
                foreach (var id in _definitions.IngredientIds)
                {
                    if (!_assignments.ContainsKey(id)) yield return id;
                }
            }
        }
    }
}
