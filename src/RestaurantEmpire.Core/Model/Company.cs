using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// The empire. Every Restaurant belongs to one of these from the very first milestone,
    /// even when there is exactly one restaurant and the "company" is one person.
    ///
    /// CLAUDE.md calls this non-negotiable, and the reason is migration cost: Economy's
    /// rollup, the Empire power ranking, and franchising all read from this container.
    /// Adding a parent entity after real save data exists is a painful migration;
    /// starting with it costs nothing.
    ///
    /// It is also where the single <see cref="SupplierPolicy"/> lives, which is what makes
    /// one supplier decision reach every location at once instead of per-restaurant.
    /// </summary>
    public sealed class Company
    {
        private readonly List<Restaurant> _restaurants;

        public Company(string id, string name, DefinitionRegistry definitions)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Company id is required.", nameof(id));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            Id = id;
            Name = name ?? id;
            Definitions = definitions;
            SupplierPolicy = new SupplierPolicy(definitions);
            _restaurants = new List<Restaurant>();
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>The loaded content database this company's restaurants read from.</summary>
        public DefinitionRegistry Definitions { get; }

        /// <summary>
        /// Company-wide purchasing policy — one assignment record, read live by every
        /// restaurant. Switching a supplier here is the single write that moves every
        /// dependent margin across the whole empire.
        /// </summary>
        public SupplierPolicy SupplierPolicy { get; }

        public IReadOnlyList<Restaurant> Restaurants { get { return _restaurants; } }

        public Restaurant OpenRestaurant(string id, string name, LocationType locationType)
        {
            foreach (var existing in _restaurants)
            {
                if (existing.Id == id)
                    throw new InvalidOperationException("This company already has a restaurant with id '" + id + "'.");
            }

            var restaurant = new Restaurant(id, name, locationType, this);
            _restaurants.Add(restaurant);

            return restaurant;
        }

        public Restaurant GetRestaurant(string id)
        {
            foreach (var restaurant in _restaurants)
            {
                if (restaurant.Id == id) return restaurant;
            }

            throw new InvalidOperationException("No restaurant with id '" + id + "' in company '" + Id + "'.");
        }
    }
}
