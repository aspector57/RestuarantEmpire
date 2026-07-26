using System;
using System.Collections.Generic;
using System.Linq;
using RestaurantEmpire.Core.Model;

namespace RestaurantEmpire.Sim
{
    /// <summary>What changed between two snapshots — what happened during one stretch of sim.</summary>
    internal sealed class Delta
    {
        public Delta(ServiceResult before, ServiceResult after)
        {
            Covers = after.CoversServed - before.CoversServed;
            Walkouts = after.Walkouts - before.Walkouts;
            EightySixed = after.EightySixed - before.EightySixed;
            TurnedAway = after.PartiesTurnedAway - before.PartiesTurnedAway;
            Revenue = after.Revenue - before.Revenue;
            FoodCost = after.FoodCost - before.FoodCost;
            Complaints = after.Diagnostics.Skip(before.Diagnostics.Count).ToList();
        }

        public int Covers { get; }
        public int Walkouts { get; }
        public int EightySixed { get; }
        public int TurnedAway { get; }
        public decimal Revenue { get; }
        public decimal FoodCost { get; }
        public IReadOnlyList<string> Complaints { get; }

        public bool Nothing { get { return Covers == 0 && Walkouts == 0 && EightySixed == 0; } }
    }

    internal static class Report
    {
        public static void Header(SimulationRunner runner)
        {
            var r = runner.Restaurant;

            Console.WriteLine();
            Console.WriteLine("  " + r.Name + " — " + runner.Clock.Now.ToString("ddd d MMM yyyy, HH:mm") +
                              (runner.IsOpen ? "   [" + runner.CurrentWindow().Name + " service]" : "   [closed]"));
            Console.WriteLine("  cash " + runner.ProjectedCash.ToString("N2") +
                              "   ·   " + r.Menu.Count + " dishes   ·   " +
                              string.Join(", ", r.ServiceWindows.Select(w => w.ToString())));
        }

        public static void Happened(Delta delta, GameClock clock)
        {
            if (delta.Nothing)
            {
                Console.WriteLine("  nothing much happened.");
                return;
            }

            Console.Write("  served " + delta.Covers + " covers, took " + delta.Revenue.ToString("N2"));
            if (delta.Walkouts > 0) Console.Write("   ·   " + delta.Walkouts + " walked out");
            if (delta.EightySixed > 0) Console.Write("   ·   " + delta.EightySixed + " dishes 86'd");
            if (delta.TurnedAway > 0) Console.Write("   ·   " + delta.TurnedAway + " turned away");
            Console.WriteLine();
        }

        public static void TheInterrupt(Interrupt interrupt)
        {
            Console.WriteLine();
            Console.WriteLine("  ##  PAUSED — " + interrupt.At.ToString("ddd HH:mm"));
            Console.WriteLine("  ##  " + interrupt.Message);
        }

        public static void Books(Company company, Restaurant restaurant)
        {
            var books = company.Economy.SummarizeAll(restaurant.Id);

            Console.WriteLine();
            Console.WriteLine("  THE BOOKS (everything booked so far)");
            Console.WriteLine("    revenue    " + books.Revenue.ToString("N2").PadLeft(12));
            Console.WriteLine("    food       " + books.FoodCost.ToString("N2").PadLeft(12) +
                              "   " + books.FoodCostRatio.ToString("P0").PadLeft(6));
            Console.WriteLine("    labour     " + books.LaborCost.ToString("N2").PadLeft(12) +
                              "   " + books.LaborCostRatio.ToString("P0").PadLeft(6));
            Console.WriteLine("    overhead   " + books.Overhead.ToString("N2").PadLeft(12));
            Console.WriteLine("    prime cost " + books.PrimeCost.ToString("N2").PadLeft(12) +
                              "   " + books.PrimeCostRatio.ToString("P1").PadLeft(6) + "   " + Band(books.Band));
            Console.WriteLine("    net        " + books.NetProfit.ToString("N2").PadLeft(12));
            Console.WriteLine("    cash       " + company.Economy.CashOnHand.ToString("N2").PadLeft(12));
        }

        public static void Menu(Restaurant restaurant)
        {
            var costing = restaurant.Costing;

            Console.WriteLine();
            Console.WriteLine("  MENU                      price     cost   food%   margin");
            foreach (var recipe in restaurant.Menu.Recipes)
            {
                Console.WriteLine(string.Format("    {0,-22} {1,7:N2}  {2,7:N2}  {3,6:P0}  {4,7:N2}",
                    recipe.Name, costing.MenuPrice(recipe.Id), costing.PlateCost(recipe.Id),
                    costing.FoodCostRatio(recipe.Id), costing.ContributionMargin(recipe.Id)));
            }
        }

        public static void Matrix(Restaurant restaurant, ServiceResult trading)
        {
            if (trading.TotalUnitsSold == 0) { Console.WriteLine("  nothing sold yet."); return; }

            var analysis = MenuEngineering.Analyze(
                restaurant, trading.UnitsSoldByRecipeId.ToDictionary(p => p.Key, p => p.Value));

            Console.WriteLine();
            Console.WriteLine("  MENU MATRIX (from everything sold so far)");
            foreach (var item in analysis.Items.OrderByDescending(i => i.TotalContribution))
            {
                Console.WriteLine(string.Format("    {0,-22} {1,-10} sold {2,5}   earned {3,10:N2}",
                    item.Name, item.Classification, item.UnitsSold, item.TotalContribution));
            }
        }

        public static void Complaints(Delta delta)
        {
            if (delta.Complaints.Count == 0) return;

            Console.WriteLine();
            Console.WriteLine("  recent complaints:");
            foreach (var line in delta.Complaints.Take(4)) Console.WriteLine("    · " + line);
        }

        private static string Band(PrimeCostBand band)
        {
            switch (band)
            {
                case PrimeCostBand.Excellent: return "excellent";
                case PrimeCostBand.Healthy: return "healthy";
                case PrimeCostBand.Tight: return "tight";
                case PrimeCostBand.Unsustainable: return "UNSUSTAINABLE";
                default: return "no data";
            }
        }
    }
}
