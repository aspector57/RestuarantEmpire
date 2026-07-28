using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Content
{
    /// <summary>
    /// The on-disk shape of a save file.
    ///
    /// These types exist separately from the simulation model on purpose. The model is free
    /// to be refactored; this is a wire format that old files were written against and must
    /// keep being readable. Keeping them apart is what stops an innocent rename in
    /// <see cref="Model.Restaurant"/> from silently invalidating everyone's saves.
    ///
    /// Deliberately plain JSON rather than a binary blob (Architecture Rule 3): a player can
    /// open it, a bug report can include it, and community tooling can read it, all for
    /// essentially no cost at this scale.
    /// </summary>
    public sealed class SaveGame
    {
        /// <summary>
        /// Bumped whenever the shape of this file changes incompatibly. Read on load so a
        /// future version can migrate an old file instead of guessing at it.
        /// </summary>
        public const int CurrentFormatVersion = 1;

        /// <summary>The build that wrote this file.</summary>
        public const string CurrentGameVersion = "0.1.0-m0";

        public int SaveFormatVersion { get; set; }
        public string GameVersion { get; set; }
        public DateTime SavedAtUtc { get; set; }

        /// <summary>
        /// Content packs active when this was written. Recorded so a load can tell the
        /// player plainly *why* something vanished — "3 recipes from a mod you no longer
        /// have were removed" — rather than silently dropping it.
        /// </summary>
        public List<string> ContentPacks { get; set; }

        public ClockState Clock { get; set; }
        public CompanyState Company { get; set; }
    }

    public sealed class ClockState
    {
        public DateTime StartDate { get; set; }
        public long Tick { get; set; }
        public string Speed { get; set; }
    }

    public sealed class CompanyState
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>Ingredient id -> supplier id. Stable string IDs, never indices.</summary>
        public Dictionary<string, string> SupplierAssignments { get; set; }

        /// <summary>Recipe id -> price set at company level.</summary>
        public Dictionary<string, decimal> Prices { get; set; }

        /// <summary>
        /// The full ledger. Cash is NOT stored — it is replayed from these entries on load,
        /// so the books can never disagree with the balance.
        /// </summary>
        public List<LedgerEntryState> Ledger { get; set; }

        public List<RestaurantState> Restaurants { get; set; }
    }

    public sealed class LedgerEntryState
    {
        public long Tick { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string RestaurantId { get; set; }
    }

    public sealed class RestaurantState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string LocationType { get; set; }

        /// <summary>Recipe ids on the menu.</summary>
        public List<string> Menu { get; set; }

        public Dictionary<string, string> SupplierAssignments { get; set; }
        public Dictionary<string, decimal> Prices { get; set; }
        public List<StationState> Stations { get; set; }

        /// <summary>
        /// The dining room fit-out. Seating capacity is NOT stored — it is derived from
        /// these, so a save can never claim more seats than it has furniture for.
        /// </summary>
        public List<FittingState> Fittings { get; set; }

        public List<StockState> Inventory { get; set; }

        /// <summary>
        /// What the neighbourhood thinks, 0 to 1, and how many meals built that opinion.
        ///
        /// Stored, unlike almost everything else here, because it genuinely cannot be
        /// recomputed — it is accumulated history rather than a value derived from current
        /// policy. An older save without it loads at neutral, which is the correct
        /// degradation: an unknown restaurant, not a hated one.
        /// </summary>
        public decimal ReputationStanding { get; set; } = Model.Reputation.Neutral;

        public int ReputationMeals { get; set; }
    }

    public sealed class StationState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int ConcurrentCapacity { get; set; }
        public decimal SpeedMultiplier { get; set; }
        public decimal Cost { get; set; }
    }

    public sealed class FittingState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public int Seats { get; set; }
        public decimal Comfort { get; set; }
    }

    public sealed class StockState
    {
        public string IngredientId { get; set; }
        public decimal Quantity { get; set; }
        public decimal ParMin { get; set; }
        public decimal ParMax { get; set; }
    }
}
