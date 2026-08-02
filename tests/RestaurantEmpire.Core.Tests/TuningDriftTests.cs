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
