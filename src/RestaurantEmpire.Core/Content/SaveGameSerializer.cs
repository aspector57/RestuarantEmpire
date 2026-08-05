using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using RestaurantEmpire.Core.Definitions;
using RestaurantEmpire.Core.Model;

namespace RestaurantEmpire.Core.Content
{
    /// <summary>A loaded save: the restored world, plus everything that had to be dropped to restore it.</summary>
    public sealed class LoadResult
    {
        internal LoadResult(Company company, GameClock clock, IList<string> warnings,
            int saveFormatVersion, string gameVersion, IList<string> contentPacks)
        {
            Company = company;
            Clock = clock;
            Warnings = new List<string>(warnings).AsReadOnly();
            SaveFormatVersion = saveFormatVersion;
            GameVersion = gameVersion;
            ContentPacks = new List<string>(contentPacks ?? new List<string>()).AsReadOnly();
        }

        public Company Company { get; }
        public GameClock Clock { get; }

        /// <summary>
        /// Plain-language notes about anything the load could not restore. Never empty for
        /// dramatic effect — each line names a specific thing that went missing, so the
        /// player can be told "3 recipes from a mod you no longer have were removed".
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        public bool LoadedCleanly { get { return Warnings.Count == 0; } }

        public int SaveFormatVersion { get; }
        public string GameVersion { get; }
        public IReadOnlyList<string> ContentPacks { get; }
    }

    /// <summary>
    /// Writes and reads save files (Architecture Rule 3).
    ///
    /// The hard problem here is not serialisation, it is TIME. Content is data-driven so
    /// mods can add recipes and ingredients, and a career runs up to forty in-game years.
    /// Those two decisions collide: a save written today will be reopened against a
    /// definition set that has been edited, versioned, or had a mod removed entirely.
    /// RimWorld — the explicit model for the data-driven approach — is notorious for
    /// breaking saves in exactly this way.
    ///
    /// So the rules here are strict:
    ///   - Everything references definitions BY STABLE STRING ID, never by index or load
    ///     order, because indices shift when content is added or removed and IDs don't.
    ///   - A missing or invalid definition DROPS THAT OBJECT AND LOGS IT. It never throws,
    ///     and it never fails the rest of the load. Losing a mod's three recipes must not
    ///     cost someone a forty-hour save.
    ///   - Every file carries a version stamp and the content packs that were active.
    /// </summary>
    public static class SaveGameSerializer
    {
        private static Dictionary<string, List<string>> ExtrasOf(Model.Restaurant restaurant)
        {
            var result = new Dictionary<string, List<string>>();
            foreach (var dish in restaurant.Extras.All())
                if (dish.Value.Count > 0) result[dish.Key] = new List<string>(dish.Value);
            return result;
        }

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        // ---- Writing ----

        public static SaveGame Capture(Company company, GameClock clock, IEnumerable<string> contentPacks = null)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));

            var save = new SaveGame
            {
                SaveFormatVersion = SaveGame.CurrentFormatVersion,
                GameVersion = SaveGame.CurrentGameVersion,
                SavedAtUtc = DateTime.UtcNow,
                ContentPacks = new List<string>(contentPacks ?? new[] { "core" }),
                Clock = clock == null ? null : new ClockState
                {
                    StartDate = clock.StartDate,
                    Tick = clock.Tick,
                    Speed = clock.Speed.ToString()
                },
                Company = new CompanyState
                {
                    Id = company.Id,
                    Name = company.Name,
                    SupplierAssignments = new Dictionary<string, string>(company.SupplierPolicy.LocalAssignments),
                    Prices = new Dictionary<string, decimal>(company.Pricing.LocalPrices),
                    Ledger = new List<LedgerEntryState>(),
                    Restaurants = new List<RestaurantState>()
                }
            };

            foreach (var entry in company.Economy.Entries)
            {
                save.Company.Ledger.Add(new LedgerEntryState
                {
                    Tick = entry.Tick,
                    Category = entry.Category.ToString(),
                    Amount = entry.Amount,
                    Description = entry.Description,
                    RestaurantId = entry.RestaurantId
                });
            }

            foreach (var restaurant in company.Restaurants)
            {
                var state = new RestaurantState
                {
                    Id = restaurant.Id,
                    Name = restaurant.Name,
                    LocationType = restaurant.LocationType.ToString(),
                    Menu = new List<string>(restaurant.Menu.RecipeIds),
                    SupplierAssignments = new Dictionary<string, string>(restaurant.SupplierPolicy.LocalAssignments),
                    Prices = new Dictionary<string, decimal>(restaurant.Pricing.LocalPrices),
                    Stations = new List<StationState>(),
                    Fittings = new List<FittingState>(),
                    Inventory = new List<StockState>(),
                    ReputationStanding = restaurant.Reputation.Standing,
                    DishExtras = ExtrasOf(restaurant),
                    ReputationMeals = restaurant.Reputation.MealsRemembered,
                    ReputationWordOfMouth = restaurant.Reputation.WordOfMouth,
                    Staff = new List<StaffState>()
                };

                foreach (var station in restaurant.Kitchen.Stations)
                {
                    state.Stations.Add(new StationState
                    {
                        Id = station.Id,
                        Name = station.Name,
                        ConcurrentCapacity = station.ConcurrentCapacity,
                        SpeedMultiplier = station.SpeedMultiplier,
                        Cost = station.Cost
                    });
                }

                foreach (var fitting in restaurant.DiningRoom.Fittings)
                {
                    state.Fittings.Add(new FittingState
                    {
                        Id = fitting.Id,
                        Name = fitting.Name,
                        Cost = fitting.Cost,
                        Seats = fitting.Seats,
                        Comfort = fitting.Comfort
                    });
                }

                foreach (var person in restaurant.Payroll.Staff)
                {
                    state.Staff.Add(new StaffState
                    {
                        Id = person.Id,
                        Name = person.Name,
                        Role = person.Role.ToString(),
                        HourlyWage = person.HourlyWage,
                        Skill = person.Skill,
                        Potential = person.Potential
                    });
                }

                foreach (var stock in restaurant.Inventory.Items)
                {
                    state.Inventory.Add(new StockState
                    {
                        IngredientId = stock.IngredientId,
                        Quantity = stock.Quantity,
                        ParMin = stock.ParMin,
                        ParMax = stock.ParMax
                    });
                }

                save.Company.Restaurants.Add(state);
            }

            return save;
        }

        public static string ToJson(Company company, GameClock clock, IEnumerable<string> contentPacks = null)
        {
            return JsonConvert.SerializeObject(Capture(company, clock, contentPacks), Settings);
        }

        public static void SaveToFile(string path, Company company, GameClock clock, IEnumerable<string> contentPacks = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is required.", nameof(path));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(path, ToJson(company, clock, contentPacks));
        }

        // ---- Reading ----

        public static LoadResult LoadFromFile(string path, DefinitionRegistry definitions)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Save file not found.", path);

            return FromJson(File.ReadAllText(path), definitions);
        }

        /// <summary>
        /// Restores a world from JSON, dropping anything whose definitions have gone away
        /// and reporting each drop. Throws only when the file itself is unreadable — a
        /// corrupt or truncated file is a genuinely different problem from missing content.
        /// </summary>
        public static LoadResult FromJson(string json, DefinitionRegistry definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            SaveGame save;
            try
            {
                save = JsonConvert.DeserializeObject<SaveGame>(json, Settings);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Save file could not be parsed: " + ex.Message, ex);
            }

            if (save == null || save.Company == null)
                throw new InvalidDataException("Save file is empty or has no company in it.");

            var warnings = new List<string>();

            if (save.SaveFormatVersion > SaveGame.CurrentFormatVersion)
            {
                warnings.Add("This save was written by a newer version of the game (format " +
                             save.SaveFormatVersion + ", this build reads " + SaveGame.CurrentFormatVersion +
                             "). Some things may be missing.");
            }

            var clock = RestoreClock(save.Clock, warnings);
            var company = new Company(save.Company.Id ?? "company", save.Company.Name, definitions);

            RestoreSupplierAssignments(company.SupplierPolicy, save.Company.SupplierAssignments,
                definitions, "the company", warnings);
            RestorePrices(company.Pricing, save.Company.Prices, definitions, "the company", warnings);

            RestoreLedger(company, save.Company.Ledger, warnings);

            if (save.Company.Restaurants != null)
            {
                foreach (var state in save.Company.Restaurants)
                    RestoreRestaurant(company, state, definitions, warnings);
            }

            return new LoadResult(company, clock, warnings,
                save.SaveFormatVersion, save.GameVersion, save.ContentPacks);
        }

        private static GameClock RestoreClock(ClockState state, List<string> warnings)
        {
            if (state == null) return new GameClock();

            var clock = new GameClock(state.StartDate == default(DateTime) ? GameClock.DefaultStartDate : state.StartDate);

            if (state.Tick > 0) clock.Advance(state.Tick);

            GameSpeed speed;
            if (!string.IsNullOrWhiteSpace(state.Speed) && Enum.TryParse(state.Speed, out speed))
            {
                clock.Speed = speed;
            }
            else if (!string.IsNullOrWhiteSpace(state.Speed))
            {
                warnings.Add("Unrecognized game speed '" + state.Speed + "'; defaulted to Normal.");
            }

            return clock;
        }

        private static void RestoreLedger(Company company, List<LedgerEntryState> ledger, List<string> warnings)
        {
            if (ledger == null) return;

            foreach (var entry in ledger)
            {
                LedgerCategory category;
                if (!Enum.TryParse(entry.Category, out category))
                {
                    warnings.Add("Dropped a ledger entry with unrecognized category '" + entry.Category + "'.");
                    continue;
                }

                if (entry.Amount < 0m)
                {
                    warnings.Add("Dropped a ledger entry with a negative amount (" + entry.Description + ").");
                    continue;
                }

                // Replaying the entries reconstructs cash exactly, which is why the balance
                // is not stored separately and so cannot drift from the books.
                company.Economy.Record(entry.Tick, category, entry.Amount, entry.Description, entry.RestaurantId);
            }
        }

        private static void RestoreRestaurant(
            Company company, RestaurantState state, DefinitionRegistry definitions, List<string> warnings)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.Id))
            {
                warnings.Add("Dropped a restaurant with no id.");
                return;
            }

            LocationType locationType;
            if (!Enum.TryParse(state.LocationType, out locationType))
            {
                locationType = LocationType.BrickAndMortar;
                warnings.Add("'" + state.Name + "' had an unrecognized location type '" +
                             state.LocationType + "'; treated as brick-and-mortar.");
            }

            Restaurant restaurant;
            try
            {
                restaurant = company.OpenRestaurant(state.Id, state.Name, locationType);
            }
            catch (InvalidOperationException ex)
            {
                warnings.Add("Could not restore restaurant '" + state.Id + "': " + ex.Message);
                return;
            }

            var where = "'" + restaurant.Name + "'";

            if (state.Menu != null)
            {
                foreach (var recipeId in state.Menu)
                {
                    if (!definitions.HasRecipe(recipeId))
                    {
                        warnings.Add("Removed '" + recipeId + "' from " + where +
                                     "'s menu — that recipe is no longer installed.");
                        continue;
                    }

                    restaurant.Menu.Add(recipeId);
                }
            }

            RestoreSupplierAssignments(restaurant.SupplierPolicy, state.SupplierAssignments, definitions, where, warnings);
            RestorePrices(restaurant.Pricing, state.Prices, definitions, where, warnings);

            if (state.Stations != null)
            {
                foreach (var station in state.Stations)
                {
                    if (string.IsNullOrWhiteSpace(station.Id))
                    {
                        warnings.Add("Dropped a station with no id in " + where + ".");
                        continue;
                    }

                    // Install, never Buy — reloading a save must not charge for the ovens again.
                    restaurant.Kitchen.Install(
                        station.Id, station.Name,
                        station.ConcurrentCapacity < 1 ? 1 : station.ConcurrentCapacity,
                        station.SpeedMultiplier <= 0m ? 1.0m : station.SpeedMultiplier,
                        station.Cost < 0m ? 0m : station.Cost);
                }
            }

            // Accumulated history, restored as-is. A save written before reputation existed
            // has no value here and lands at neutral — an unknown restaurant, not a hated one.
            restaurant.Reputation.Restore(state.ReputationStanding, state.ReputationMeals, state.ReputationWordOfMouth);

            // A save written before extras existed simply has none, and loads as plain food —
            // which is what that save meant. Architecture Rule 3.
            if (state.DishExtras != null)
                foreach (var dish in state.DishExtras)
                    if (dish.Value != null)
                        foreach (var extraId in dish.Value)
                            restaurant.Extras.Set(dish.Key, extraId, true);

            if (state.Staff != null)
            {
                foreach (var person in state.Staff)
                {
                    StaffRole role;
                    if (!Enum.TryParse(person.Role, out role)) role = StaffRole.Cook;

                    restaurant.Payroll.Hire(new Employee(
                        person.Id, person.Name, role,
                        person.HourlyWage < 0m ? 0m : person.HourlyWage,
                        person.Skill <= 0m ? 0.5m : person.Skill,
                        person.Potential));
                }
            }

            if (state.Fittings != null)
            {
                foreach (var fitting in state.Fittings)
                {
                    if (string.IsNullOrWhiteSpace(fitting.Id))
                    {
                        warnings.Add("Dropped a fitting with no id in " + where + ".");
                        continue;
                    }

                    try
                    {
                        restaurant.DiningRoom.Add(new Fitting(
                            fitting.Id, fitting.Name, fitting.Cost < 0m ? 0m : fitting.Cost,
                            fitting.Seats < 0 ? 0 : fitting.Seats,
                            fitting.Comfort < 0m || fitting.Comfort > 1m ? 0.5m : fitting.Comfort));
                    }
                    catch (ArgumentException ex)
                    {
                        warnings.Add("Dropped fitting '" + fitting.Id + "' in " + where + ": " + ex.Message);
                    }
                }
            }

            if (state.Inventory != null)
            {
                foreach (var stock in state.Inventory)
                {
                    if (!definitions.HasIngredient(stock.IngredientId))
                    {
                        warnings.Add("Discarded " + where + "'s stock of '" + stock.IngredientId +
                                     "' — that ingredient is no longer installed.");
                        continue;
                    }

                    restaurant.Inventory.SetPar(stock.IngredientId, stock.ParMin, stock.ParMax);
                    restaurant.Inventory.Receive(stock.IngredientId, stock.Quantity);
                }
            }
        }

        private static void RestoreSupplierAssignments(
            SupplierPolicy policy, Dictionary<string, string> assignments,
            DefinitionRegistry definitions, string where, List<string> warnings)
        {
            if (assignments == null) return;

            foreach (var pair in assignments)
            {
                if (!definitions.HasIngredient(pair.Key))
                {
                    warnings.Add("Dropped " + where + "'s supplier choice for '" + pair.Key +
                                 "' — that ingredient is no longer installed.");
                    continue;
                }

                if (!definitions.HasSupplier(pair.Value))
                {
                    warnings.Add("Dropped " + where + "'s supplier choice for '" + pair.Key +
                                 "' — supplier '" + pair.Value + "' is no longer installed.");
                    continue;
                }

                try
                {
                    policy.Assign(pair.Key, pair.Value);
                }
                catch (InvalidOperationException)
                {
                    // The supplier still exists but no longer carries this ingredient —
                    // a content edit rather than a removal. Same treatment: drop and say so.
                    warnings.Add("Dropped " + where + "'s supplier choice for '" + pair.Key +
                                 "' — '" + pair.Value + "' no longer carries it.");
                }
            }
        }

        private static void RestorePrices(
            PricingPolicy pricing, Dictionary<string, decimal> prices,
            DefinitionRegistry definitions, string where, List<string> warnings)
        {
            if (prices == null) return;

            foreach (var pair in prices)
            {
                if (!definitions.HasRecipe(pair.Key))
                {
                    warnings.Add("Dropped " + where + "'s price for '" + pair.Key +
                                 "' — that recipe is no longer installed.");
                    continue;
                }

                if (pair.Value < 0m)
                {
                    warnings.Add("Dropped " + where + "'s negative price for '" + pair.Key + "'.");
                    continue;
                }

                pricing.SetPrice(pair.Key, pair.Value);
            }
        }
    }
}
