using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Which supplier a scope buys each ingredient from — and the inheritance chain that
    /// makes one decision reach everything below it.
    ///
    /// This is the most load-bearing class in the project. It exists because Restaurant
    /// Empire II made a supplier change a per-recipe editing chore, and that was its
    /// most-criticised flaw.
    ///
    /// THE CONTRACT (CLAUDE.md Architecture Rule 1 / design doc Phase 6.3), which the
    /// design doc states as "a single decision that automatically updates every recipe and
    /// location, with any exceptions requiring explicit opt-in rather than every instance
    /// requiring opt-in by default":
    ///
    ///   - Assignments are made at a SCOPE. Company is the base scope; each Restaurant has
    ///     its own scope that inherits from it. A Region scope slots in between at M4
    ///     without any other code changing.
    ///   - Reads RESOLVE UP THE CHAIN: restaurant override first, then whatever it inherits
    ///     from, until someone answers. So the company-wide default is what almost
    ///     everything uses, and an override is a deliberate, rare exception.
    ///   - NOTHING IS CACHED. No price or resolved supplier is ever stored, so there is no
    ///     stale value and no refresh step anyone could forget.
    ///
    /// The propagation cuts both ways, and that is intended (Phase 7 audit): a bad
    /// company-level switch gets worse everywhere at once, which is what makes sourcing a
    /// genuinely consequential decision rather than a contained one.
    /// </summary>
    public sealed class SupplierPolicy
    {
        private readonly DefinitionRegistry _definitions;
        private readonly SupplierPolicy _inheritsFrom;
        private readonly Dictionary<string, string> _assignments;

        internal SupplierPolicy(DefinitionRegistry definitions, string scopeName, SupplierPolicy inheritsFrom)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _definitions = definitions;
            _inheritsFrom = inheritsFrom;
            _assignments = new Dictionary<string, string>(StringComparer.Ordinal);

            ScopeName = string.IsNullOrWhiteSpace(scopeName) ? "unnamed scope" : scopeName;
        }

        /// <summary>Human-readable name of this scope ("Acme Restaurant Group", "The Flagship").</summary>
        public string ScopeName { get; }

        /// <summary>The scope this one falls back to, or null if this is the top of the chain.</summary>
        public SupplierPolicy InheritsFrom { get { return _inheritsFrom; } }

        /// <summary>
        /// Assignments made AT THIS SCOPE only — not what this scope resolves to.
        /// A restaurant with an empty set here is using the company default for everything,
        /// which is the normal, healthy case.
        /// </summary>
        public IReadOnlyDictionary<string, string> LocalAssignments { get { return _assignments; } }

        // ---- Writing ----

        /// <summary>
        /// The single write. Points one ingredient at one supplier, at this scope.
        ///
        /// Done on a Company, every restaurant that hasn't overridden it costs differently
        /// from the next read onward, with no recipe edited. Done on a Restaurant, only
        /// that location diverges — the explicit opt-in exception.
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

        /// <summary>Assigns every ingredient this supplier carries, at this scope.</summary>
        public void AssignAll(string supplierId)
        {
            var supplier = _definitions.GetSupplier(supplierId);

            foreach (var ingredientId in supplier.CarriedIngredientIds)
            {
                if (_definitions.HasIngredient(ingredientId))
                    _assignments[ingredientId] = supplierId;
            }
        }

        /// <summary>
        /// Drops a local override so this scope goes back to inheriting. Returns false if
        /// there was no override to drop.
        /// </summary>
        public bool ClearOverride(string ingredientId)
        {
            return ingredientId != null && _assignments.Remove(ingredientId);
        }

        /// <summary>True when this exact scope overrides the ingredient, rather than inheriting it.</summary>
        public bool HasLocalOverride(string ingredientId)
        {
            return ingredientId != null && _assignments.ContainsKey(ingredientId);
        }

        // ---- Reading: resolution walks up the chain, live, every time ----

        /// <summary>
        /// The scope that actually answers for this ingredient — this one, or the nearest
        /// ancestor that has an assignment. Null when nobody does.
        ///
        /// Exposed because "every outcome must trace to a specific named cause"
        /// (CLAUDE.md principle 2): the player can always be told *why* a dish costs what
        /// it costs, down to which level of the business made the call.
        /// </summary>
        public SupplierPolicy ResolveScope(string ingredientId)
        {
            var key = ingredientId ?? string.Empty;
            var scope = this;

            while (scope != null)
            {
                if (scope._assignments.ContainsKey(key)) return scope;
                scope = scope._inheritsFrom;
            }

            return null;
        }

        /// <summary>Name of the scope that decided this ingredient's supplier, or null if unassigned.</summary>
        public string ResolvedFromScopeName(string ingredientId)
        {
            var scope = ResolveScope(ingredientId);
            return scope == null ? null : scope.ScopeName;
        }

        public bool IsAssigned(string ingredientId)
        {
            return ResolveScope(ingredientId) != null;
        }

        public string ResolveSupplierId(string ingredientId)
        {
            var scope = ResolveScope(ingredientId);

            if (scope == null)
            {
                throw new InvalidOperationException(
                    "No supplier is assigned for ingredient '" + ingredientId + "' at scope '" +
                    ScopeName + "' or anything it inherits from. Assign one before costing a " +
                    "recipe that uses it.");
            }

            return scope._assignments[ingredientId];
        }

        public SupplierDefinition ResolveSupplier(string ingredientId)
        {
            return _definitions.GetSupplier(ResolveSupplierId(ingredientId));
        }

        /// <summary>
        /// Live unit price under whatever is assigned right now. Recomputed on every call —
        /// there is deliberately no cached price field anywhere in this class.
        /// </summary>
        public decimal UnitPriceFor(string ingredientId)
        {
            return ResolveSupplier(ingredientId).UnitPriceFor(ingredientId);
        }

        /// <summary>Ingredients nothing in the chain has assigned — these block costing.</summary>
        public IEnumerable<string> UnassignedIngredientIds
        {
            get
            {
                foreach (var id in _definitions.IngredientIds)
                {
                    if (!IsAssigned(id)) yield return id;
                }
            }
        }
    }
}
