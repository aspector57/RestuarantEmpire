using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Content
{
    /// <summary>
    /// Reads game content from external JSON files (Architecture Rule 2).
    ///
    /// Nothing in this project hardcodes a recipe, an ingredient or a supplier. Adding a
    /// dish means dropping a file into data/recipes/ — no code change, no rebuild of game
    /// logic. That is what makes modding possible later, and it is the reason this is
    /// built now rather than retrofitted: RimWorld's data-driven def system is the model,
    /// and bolting one on after the fact is a rewrite, not an addition.
    ///
    /// Content that fails validation is DROPPED WITH A WARNING, never thrown on. A single
    /// bad file — or a mod the player uninstalled — must not stop the game loading.
    /// </summary>
    public static class JsonDefinitionLoader
    {
        public const string IngredientsFileName = "ingredients.json";
        public const string SuppliersFileName = "suppliers.json";
        public const string RecipesDirectoryName = "recipes";

        public static DefinitionRegistry LoadFromDirectory(string dataDirectory)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
                throw new ArgumentException("Data directory is required.", nameof(dataDirectory));

            if (!Directory.Exists(dataDirectory))
                throw new DirectoryNotFoundException("Content directory not found: " + dataDirectory);

            var warnings = new List<string>();

            var ingredients = LoadIngredients(Path.Combine(dataDirectory, IngredientsFileName), warnings);
            var suppliers = LoadSuppliers(Path.Combine(dataDirectory, SuppliersFileName), ingredients, warnings);
            var recipes = LoadRecipes(Path.Combine(dataDirectory, RecipesDirectoryName), ingredients, warnings);

            return new DefinitionRegistry(ingredients.Values, suppliers, recipes, warnings);
        }

        private static Dictionary<string, IngredientDefinition> LoadIngredients(string path, List<string> warnings)
        {
            var result = new Dictionary<string, IngredientDefinition>(StringComparer.Ordinal);

            if (!File.Exists(path))
            {
                warnings.Add("No ingredients file at '" + path + "'; loaded zero ingredients.");
                return result;
            }

            var file = JsonConvert.DeserializeObject<IngredientFileDto>(File.ReadAllText(path));
            if (file == null || file.Ingredients == null) return result;

            foreach (var dto in file.Ingredients)
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    warnings.Add("Skipped an ingredient with no id in " + Path.GetFileName(path) + ".");
                    continue;
                }

                if (result.ContainsKey(dto.Id))
                {
                    warnings.Add("Duplicate ingredient id '" + dto.Id + "'; kept the first one.");
                    continue;
                }

                result[dto.Id] = new IngredientDefinition(dto.Id, dto.Name, dto.Unit);
            }

            return result;
        }

        private static List<SupplierDefinition> LoadSuppliers(
            string path, Dictionary<string, IngredientDefinition> ingredients, List<string> warnings)
        {
            var result = new List<SupplierDefinition>();

            if (!File.Exists(path))
            {
                warnings.Add("No suppliers file at '" + path + "'; loaded zero suppliers.");
                return result;
            }

            var file = JsonConvert.DeserializeObject<SupplierFileDto>(File.ReadAllText(path));
            if (file == null || file.Suppliers == null) return result;

            foreach (var dto in file.Suppliers)
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    warnings.Add("Skipped a supplier with no id in " + Path.GetFileName(path) + ".");
                    continue;
                }

                var prices = new Dictionary<string, decimal>(StringComparer.Ordinal);

                if (dto.Prices != null)
                {
                    foreach (var pair in dto.Prices)
                    {
                        // Graceful degradation: a price for an ingredient that no longer exists
                        // (removed content, uninstalled mod) is dropped, not fatal.
                        if (!ingredients.ContainsKey(pair.Key))
                        {
                            warnings.Add("Supplier '" + dto.Id + "' prices unknown ingredient '" + pair.Key + "'; dropped that line.");
                            continue;
                        }

                        prices[pair.Key] = pair.Value;
                    }
                }

                result.Add(new SupplierDefinition(dto.Id, dto.Name, dto.QualityTier, prices));
            }

            return result;
        }

        private static List<RecipeDefinition> LoadRecipes(
            string directory, Dictionary<string, IngredientDefinition> ingredients, List<string> warnings)
        {
            var result = new List<RecipeDefinition>();

            if (!Directory.Exists(directory))
            {
                warnings.Add("No recipes directory at '" + directory + "'; loaded zero recipes.");
                return result;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var files = Directory.GetFiles(directory, "*.json");
            Array.Sort(files, StringComparer.Ordinal); // deterministic load order

            foreach (var file in files)
            {
                RecipeDto dto;

                try
                {
                    dto = JsonConvert.DeserializeObject<RecipeDto>(File.ReadAllText(file));
                }
                catch (JsonException ex)
                {
                    warnings.Add("Could not parse recipe file '" + Path.GetFileName(file) + "': " + ex.Message);
                    continue;
                }

                if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                {
                    warnings.Add("Recipe file '" + Path.GetFileName(file) + "' has no id; skipped.");
                    continue;
                }

                if (!seenIds.Add(dto.Id))
                {
                    warnings.Add("Duplicate recipe id '" + dto.Id + "' in '" + Path.GetFileName(file) + "'; kept the first one.");
                    continue;
                }

                var lines = new List<RecipeIngredient>();
                var missingIngredient = false;

                if (dto.Ingredients != null)
                {
                    foreach (var lineDto in dto.Ingredients)
                    {
                        if (string.IsNullOrWhiteSpace(lineDto.IngredientId) || !ingredients.ContainsKey(lineDto.IngredientId))
                        {
                            warnings.Add("Recipe '" + dto.Id + "' references unknown ingredient '" +
                                         lineDto.IngredientId + "'; recipe dropped.");
                            missingIngredient = true;
                            break;
                        }

                        if (lineDto.Quantity <= 0m)
                        {
                            warnings.Add("Recipe '" + dto.Id + "' has a non-positive quantity for '" +
                                         lineDto.IngredientId + "'; recipe dropped.");
                            missingIngredient = true;
                            break;
                        }

                        lines.Add(new RecipeIngredient(lineDto.IngredientId, lineDto.Quantity));
                    }
                }

                // A recipe that cannot be costed is worse than a missing one — drop it.
                if (missingIngredient) continue;

                if (lines.Count == 0)
                {
                    warnings.Add("Recipe '" + dto.Id + "' has no ingredients; dropped.");
                    continue;
                }

                result.Add(new RecipeDefinition(dto.Id, dto.Name, dto.MenuPrice, lines));
            }

            return result;
        }

        // ---- File shapes. Kept private: these mirror the JSON, the public model does not. ----

        private sealed class IngredientFileDto
        {
            public List<IngredientDto> Ingredients { get; set; }
        }

        private sealed class IngredientDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Unit { get; set; }
        }

        private sealed class SupplierFileDto
        {
            public List<SupplierDto> Suppliers { get; set; }
        }

        private sealed class SupplierDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int QualityTier { get; set; }
            public Dictionary<string, decimal> Prices { get; set; }
        }

        private sealed class RecipeDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public decimal MenuPrice { get; set; }
            public List<RecipeIngredientDto> Ingredients { get; set; }
        }

        private sealed class RecipeIngredientDto
        {
            public string IngredientId { get; set; }
            public decimal Quantity { get; set; }
        }
    }
}
