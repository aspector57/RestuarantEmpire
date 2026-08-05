using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// THE BROWSER BUILD IS A SECOND IMPLEMENTATION, AND SECOND IMPLEMENTATIONS DRIFT.
    ///
    /// `web/pass.html` re-implements this core in JavaScript so the loop can be played rather
    /// than read. It has diverged twice, and both times the only detector was Aaron losing an
    /// evening to a broken game: once on invented equipment speeds, and once on `Markup`, which
    /// was ported by NAME rather than by definition and made every guest balk at every price.
    ///
    /// Centralising the C# constants into <see cref="Tuning"/> does not prevent that on its
    /// own — a number that lives in one place and is copied by hand into another is still two
    /// numbers. THIS is the guard: it reads the JavaScript and fails when the two disagree.
    ///
    /// It deliberately checks values rather than parsing JavaScript properly. A regex over
    /// `const NAME = value` is enough, and anything cleverer would be a second thing to
    /// maintain.
    /// </summary>
    public class TuningDriftTests
    {
        private readonly ITestOutputHelper _out;
        public TuningDriftTests(ITestOutputHelper o) { _out = o; }

        /// <summary>
        /// Every pair here is a number that exists twice: once in <see cref="Tuning"/> and once
        /// in the browser build. Add to this list whenever a tuning constant gets ported.
        /// </summary>
        private static IEnumerable<(string js, double expected, string note)> Shared()
        {
            yield return ("WALK_AWAY", (double)Tuning.WalkAwayValueThreshold, "value below which a meal reads as poor");
            yield return ("COMFY_WAIT", (double)Tuning.ComfortableWaitShare, "share of patience spent waiting happily");
            yield return ("PLATES_PER_COOK", Tuning.PlatesPerCook, "a cook works a line, not a pan");
            yield return ("SEATS_PER_SERVER", 14, "covers one server can hold");
            yield return ("FREE_MENU_SIZE", Tuning.FreeMenuSize, "dishes before breadth costs the kitchen");
            yield return ("PRICE_TOLERANCE_EXPONENT", Tuning.PriceToleranceExponent, "how fast price resistance builds");
            yield return ("PRACTICAL_CAPACITY", (double)Tuning.PracticalCapacity, "throughput a real service gets");
            yield return ("AVG_PARTY", (double)Tuning.AveragePartySize, "mean party size");
            yield return ("QUOTE_OPTIMISM", Tuning.QuotedWaitOptimism / 100.0, "kitchens quote under the truth");
            yield return ("GRUMBLE", (double)Tuning.GrumbleThreshold, "below this, a guest mentions it");
            yield return ("EXTRA_DIMINISH", (double)Tuning.ExtraDiminishing, "what each further thing on a plate is worth");
            yield return ("WOM_FLOOR", (double)Tuning.WordOfMouthFloor, "what a forgettable meal still does for word of mouth");
            yield return ("WOM_FROM", (double)Tuning.WordOfMouthFrom, "where word of mouth starts to grow");
            yield return ("WOM_DELIGHT", (double)Tuning.WordOfMouthDelight, "the meal that spreads fully");
            yield return ("LICENCE_FEE", (double)LiquorLicense.ApplicationFee, "what a liquor licence costs to get");
            yield return ("LICENCE_RENEWAL", (double)LiquorLicense.MonthlyRenewal, "and to keep, monthly");
        }

        private static string BrowserBuildPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "web", "pass.html");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            return null;
        }

        [Fact]
        public void TheBrowserBuildAgreesWithTheTuningItWasPortedFrom()
        {
            var path = BrowserBuildPath();
            Assert.True(path != null,
                "web/pass.html not found. The browser build must live in the repository — while it " +
                "sat in a scratchpad it could not be checked and could not be worked on elsewhere.");

            var source = File.ReadAllText(path);
            var wrong = new List<string>();

            foreach (var (js, expected, note) in Shared())
            {
                var match = Regex.Match(source, @"\b(?:const|var|let)\s+" + Regex.Escape(js) + @"\s*=\s*(-?[0-9.]+)");
                if (!match.Success)
                {
                    wrong.Add($"{js} — not found in the browser build ({note})");
                    continue;
                }

                var actual = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var agrees = Math.Abs(actual - expected) < 0.0001;
                _out.WriteLine($"  {(agrees ? "ok  " : "DRIFT")} {js,-26} js {actual,-8} engine {expected}");

                if (!agrees) wrong.Add($"{js} — browser says {actual}, engine says {expected} ({note})");
            }

            Assert.True(wrong.Count == 0,
                "The browser build has drifted from the engine:\n  " + string.Join("\n  ", wrong) +
                "\n\nThe C# core is the source of truth. Copy the DEFINITION across, not the name — " +
                "that distinction is what the Markup bug turned on.");
        }

        /// <summary>
        /// THE SITES MUST BE THE SAME SITES IN BOTH BUILDS.
        ///
        /// Found by a sweep, not by reading: City Center's floor cap was 1,400 sq ft in the
        /// engine and 15,500 in the browser build. An eleven-fold typo, and it inverted the
        /// design's central tension — "the best traffic comes with the least room to grow"
        /// — so in the build a human actually plays, the city had by far the MOST room. A
        /// sweep looking for degenerate strategies read "more ovens is always better" partly
        /// off the back of it.
        ///
        /// Rent and key money are here for the same reason: they decide how much capital
        /// survives opening day, which is a quarter of the location decision.
        /// </summary>
        [Fact]
        public void TheBrowserBuildHasTheSameSitesAsTheEngine()
        {
            var path = BrowserBuildPath();
            Assert.True(path != null, "web/pass.html not found");

            var source = File.ReadAllText(path);
            var wrong = new List<string>();

            var sites = new[]
            {
                Neighborhood.CityCenter(), Neighborhood.BusinessDistrict(),
                Neighborhood.SuburbanHighStreet(), Neighborhood.NightlifeQuarter()
            };

            foreach (var site in sites)
            {
                var match = Regex.Match(source,
                    @"id:""" + Regex.Escape(site.Id) + @""".*?key:\s*(-?[0-9.]+).*?rent:\s*(-?[0-9.]+).*?maxArea:\s*(-?[0-9.]+)");

                if (!match.Success)
                {
                    wrong.Add(site.Id + " — not found in the browser build");
                    continue;
                }

                Check(wrong, site.Id, "key money", match.Groups[1].Value, site.LeasePremium);
                Check(wrong, site.Id, "monthly rent", match.Groups[2].Value, site.MonthlyRent);
                Check(wrong, site.Id, "floor cap", match.Groups[3].Value, site.MaxFloorArea);

                _out.WriteLine($"  {site.Id,-20} key {site.LeasePremium,-8} rent {site.MonthlyRent,-8} cap {site.MaxFloorArea}");
            }

            Assert.True(wrong.Count == 0,
                "The browser build's sites have drifted from the engine's:\n  " + string.Join("\n  ", wrong));
        }

        private static void Check(List<string> wrong, string site, string field, string text, decimal expected)
        {
            var actual = decimal.Parse(text, CultureInfo.InvariantCulture);
            if (actual != expected)
                wrong.Add($"{site} {field} — browser says {actual}, engine says {expected}");
        }

        /// <summary>
        /// THE DRIFT GUARD WAS WATCHING THE CONSTANTS AND NOT THE CONTENT.
        ///
        /// `web/pass.html` carries its own hardcoded `RECIPES`, under a comment reading
        /// "content, mirrored from data/" — and that mirroring is done by hand. A whole food
        /// economics recalibration was written into `data/recipes/*.json` and never reached the
        /// browser build: Eggs Benedict got a protein in the engine and stayed a plate of eggs
        /// and flour in the port, sea bass was repriced $21 -> $29 in one build only, and the
        /// two disagreed for a full session.
        ///
        /// The tell was that every browser probe came back BYTE-IDENTICAL after the change,
        /// which is the same signature as the probe that missed `billTheMonth()` and reported
        /// no effect from four new pressure systems. **Identical output after a real change is
        /// a bug report, not a null result.**
        ///
        /// This checks menu prices, because that is what the drift actually was and a full
        /// structural comparison would be a JSON parser living in a test. Ingredient lists are
        /// checked for PRESENCE of each id, which catches a dish losing a component — the
        /// Benedict case — without pinning quantities in two places.
        ///
        /// The same rule as every other entry here: the C# core is the source of truth.
        /// </summary>
        [Fact]
        public void TheBrowserBuildServesTheSameDishesAtTheSamePrices()
        {
            var path = BrowserBuildPath();
            Assert.True(path != null, "web/pass.html not found");

            var source = File.ReadAllText(path);
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data", "recipes")))
                dir = dir.Parent;
            Assert.True(dir != null, "data/recipes not found");

            var wrong = new List<string>();

            foreach (var file in Directory.GetFiles(Path.Combine(dir.FullName, "data", "recipes"), "*.json"))
            {
                var json = File.ReadAllText(file);
                var id = Regex.Match(json, @"""id""\s*:\s*""([^""]+)""").Groups[1].Value;
                var price = decimal.Parse(Regex.Match(json, @"""menuPrice""\s*:\s*([0-9.]+)").Groups[1].Value,
                                          CultureInfo.InvariantCulture);

                // The browser entry runs from `id:"<id>"` to the end of its `ing:{...}` block.
                var entry = Regex.Match(source, @"id:""" + Regex.Escape(id) + @""".*?ing:\{[^}]*\}", RegexOptions.Singleline);
                if (!entry.Success)
                {
                    wrong.Add($"{id} — the browser build does not serve this dish at all");
                    continue;
                }

                var basePrice = Regex.Match(entry.Value, @"base:\s*([0-9.]+)");
                if (!basePrice.Success)
                {
                    wrong.Add($"{id} — no base price found in the browser build");
                }
                else
                {
                    var actual = decimal.Parse(basePrice.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (actual != price)
                        wrong.Add($"{id} menu price — browser says {actual}, engine says {price}");
                }

                foreach (Match ing in Regex.Matches(json, @"""ingredientId""\s*:\s*""([^""]+)"""))
                {
                    var ingredient = ing.Groups[1].Value;
                    if (!Regex.IsMatch(entry.Value, @"[{,\s]""?" + Regex.Escape(ingredient) + @"""?\s*:"))
                        wrong.Add($"{id} — the browser build's version has no {ingredient} in it");
                }

                _out.WriteLine($"  ok   {id,-18} {price,8:C}");
            }

            Assert.True(wrong.Count == 0,
                "The browser build's menu has drifted from the content files:\n  " + string.Join("\n  ", wrong) +
                "\n\nweb/pass.html mirrors data/ BY HAND. Recalibrating one build and not the other " +
                "is why every browser probe came back byte-identical after a real change.");
        }

        /// <summary>
        /// EXTRAS EXIST TWICE, so they drift. `data/extras.json` in the engine, `EXTRAS` in
        /// the browser build — and the browser had them first, alone, for a session. That is
        /// precisely the gap the food recalibration fell through.
        ///
        /// Checks the lift on every extra and the per-category ceilings, which are the two
        /// numbers that decide whether dressing a dish up pays.
        /// </summary>
        [Fact]
        public void TheBrowserBuildDressesDishesUpByTheSameAmounts()
        {
            var path = BrowserBuildPath();
            Assert.True(path != null, "web/pass.html not found");

            var source = File.ReadAllText(path);
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "extras.json")))
                dir = dir.Parent;
            Assert.True(dir != null, "data/extras.json not found");

            var json = File.ReadAllText(Path.Combine(dir.FullName, "data", "extras.json"));
            var wrong = new List<string>();

            foreach (Match m in Regex.Matches(json,
                @"""recipeId""\s*:\s*""([^""]+)""\s*,\s*""id""\s*:\s*""([^""]+)""[^}]*?""lift""\s*:\s*([0-9.]+)"))
            {
                var recipe = m.Groups[1].Value;
                var id = m.Groups[2].Value;
                var lift = decimal.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);

                var block = Regex.Match(source,
                    @"""" + Regex.Escape(recipe) + @""":\s*\[(.*?)\]", RegexOptions.Singleline);
                if (!block.Success)
                {
                    wrong.Add($"{recipe} — the browser build offers nothing to add to this dish");
                    continue;
                }

                var entry = Regex.Match(block.Groups[1].Value,
                    @"id:""" + Regex.Escape(id) + @""".*?lift:\s*([0-9.]+)", RegexOptions.Singleline);
                if (!entry.Success)
                {
                    wrong.Add($"{recipe}/{id} — not offered in the browser build");
                    continue;
                }

                var actual = decimal.Parse(entry.Groups[1].Value, CultureInfo.InvariantCulture);
                if (actual != lift)
                    wrong.Add($"{recipe}/{id} lift — browser says {actual}, engine says {lift}");
            }

            // Only the liftCeilings block — scanning the whole file picks up every "lift" and
            // "quantity" key in the extras array itself.
            var ceilingBlock = Regex.Match(json, @"""liftCeilings""\s*:\s*\{(.*?)\}", RegexOptions.Singleline);
            Assert.True(ceilingBlock.Success, "extras.json has no liftCeilings block");

            foreach (Match m in Regex.Matches(ceilingBlock.Groups[1].Value, @"""([a-z ]+)""\s*:\s*([0-9.]+)"))
            {
                var category = m.Groups[1].Value;
                var ceiling = decimal.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);

                var js = Regex.Match(source, @"LIFT_CEILING[^;]*?""" + Regex.Escape(category) + @""":\s*([0-9.]+)");
                if (!js.Success) { wrong.Add($"ceiling for '{category}' — not found in the browser build"); continue; }

                var actual = decimal.Parse(js.Groups[1].Value, CultureInfo.InvariantCulture);
                if (actual != ceiling)
                    wrong.Add($"'{category}' lift ceiling — browser says {actual}, engine says {ceiling}");
            }

            Assert.True(wrong.Count == 0,
                "Dish extras have drifted between the two builds:\n  " + string.Join("\n  ", wrong));
            _out.WriteLine("  extras and ceilings agree across both builds");
        }

        /// <summary>
        /// CONCEPTS EXIST TWICE NOW — data/concepts.json and CONCEPTS in the browser build.
        ///
        /// They were fixtures in a test file before they were data, and consolidating them
        /// caught a real bug: the wine bar's late service was written 23-&gt;26, the browser
        /// build's convention, where the engine expresses a midnight wrap as 23-&gt;2. Two
        /// builds, two conventions for the same idea.
        ///
        /// Checks the card and the price position, which are what a concept IS. The opening
        /// shape (seats, kit, comfort) is browser-only for now and deliberately not checked —
        /// the engine has no fit-out.
        /// </summary>
        [Fact]
        public void TheBrowserBuildOffersTheSameConcepts()
        {
            var path = BrowserBuildPath();
            Assert.True(path != null, "web/pass.html not found");

            var source = File.ReadAllText(path);
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "concepts.json")))
                dir = dir.Parent;
            Assert.True(dir != null, "data/concepts.json not found");

            var json = File.ReadAllText(Path.Combine(dir.FullName, "data", "concepts.json"));
            var wrong = new List<string>();

            foreach (Match m in Regex.Matches(json,
                @"""id""\s*:\s*""([^""]+)""(.*?)""pricePosition""\s*:\s*([0-9.]+)", RegexOptions.Singleline))
            {
                var id = m.Groups[1].Value;
                var body = m.Groups[2].Value;
                var position = decimal.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);

                var entry = Regex.Match(source,
                    @"id:""" + Regex.Escape(id) + @""".*?card:\[(.*?)\].*?pricePosition:\s*([0-9.]+)",
                    RegexOptions.Singleline);
                if (!entry.Success) { wrong.Add(id + " — not offered in the browser build"); continue; }

                var actual = decimal.Parse(entry.Groups[2].Value, CultureInfo.InvariantCulture);
                if (actual != position)
                    wrong.Add($"{id} price position — browser says {actual}, engine says {position}");

                foreach (Match dish in Regex.Matches(body, @"""([a-z-]+)""(?=\s*[,\]])"))
                {
                    var recipeId = dish.Groups[1].Value;
                    if (recipeId == "recipeIds") continue;
                    if (!entry.Groups[1].Value.Contains("\"" + recipeId + "\""))
                        wrong.Add($"{id} — the browser build's card is missing {recipeId}");
                }

                _out.WriteLine($"  ok   {id,-24} at {position}x");
            }

            Assert.True(wrong.Count == 0,
                "Concepts have drifted between the two builds:\n  " + string.Join("\n  ", wrong));
        }

        /// <summary>
        /// The satisfaction weights must sum to exactly one, in both builds. They are shares of
        /// a single judgement, and a set that sums to 0.98 silently makes every meal worse.
        /// </summary>
        [Fact]
        public void TheSatisfactionWeightsAreShares()
        {
            var total = Tuning.FoodQualityWeight + Tuning.ServiceSpeedWeight
                      + Tuning.ValueWeight + Tuning.AmbianceWeight;
            Assert.Equal(1.0m, total);

            var path = BrowserBuildPath();
            if (path == null) return;

            var match = Regex.Match(File.ReadAllText(path),
                @"const\s+W\s*=\s*\{\s*food:\s*([0-9.]+),\s*speed:\s*([0-9.]+),\s*value:\s*([0-9.]+),\s*room:\s*([0-9.]+)");
            Assert.True(match.Success, "could not find the weight table in the browser build");

            var js = new[]
            {
                (match.Groups[1].Value, Tuning.FoodQualityWeight, "food"),
                (match.Groups[2].Value, Tuning.ServiceSpeedWeight, "speed"),
                (match.Groups[3].Value, Tuning.ValueWeight, "value"),
                (match.Groups[4].Value, Tuning.AmbianceWeight, "room"),
            };

            foreach (var (text, expected, name) in js)
                Assert.True(decimal.Parse(text, CultureInfo.InvariantCulture) == expected,
                    $"the browser build weighs {name} at {text}, the engine at {expected}");
        }

        /// <summary>
        /// The reputation ceiling's three shares must also sum to one, or a restaurant doing
        /// everything available to it is told it is capped — which is the defect Aaron found:
        /// *"this is the best supplier possible so would I never be able to reach 100?"*
        /// </summary>
        [Fact]
        public void APerfectRestaurantCanReachAPerfectStanding()
        {
            var total = Tuning.CompetenceCeiling + Tuning.AmbitionFromIngredients + Tuning.AmbianceWeight;
            Assert.Equal(1.0m, total);
        }
    }
}
