using System;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// What time of day it is, from the guest's point of view.
    ///
    /// Derived from the hour rather than from what the operator named their service, so a
    /// window called "Dinner" that runs at 8am is still breakfast to everyone walking in.
    /// </summary>
    public enum Daypart
    {
        Breakfast = 0,
        Lunch = 1,
        Dinner = 2,
        LateNight = 3
    }

    public static class Dayparts
    {
        public static Daypart At(DateTime now) { return At(now.Hour); }

        public static Daypart At(int hour)
        {
            if (hour >= 5 && hour < 11) return Daypart.Breakfast;
            if (hour >= 11 && hour < 17) return Daypart.Lunch;
            if (hour >= 17 && hour < 23) return Daypart.Dinner;

            return Daypart.LateNight;
        }

        public static bool TryParse(string text, out Daypart daypart)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "breakfast": daypart = Daypart.Breakfast; return true;
                case "lunch": daypart = Daypart.Lunch; return true;
                case "dinner": daypart = Daypart.Dinner; return true;
                case "late-night":
                case "latenight":
                case "late": daypart = Daypart.LateNight; return true;
                default: daypart = Daypart.Dinner; return false;
            }
        }
    }
}
