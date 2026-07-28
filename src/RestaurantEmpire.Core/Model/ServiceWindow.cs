using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// A period of the day when the restaurant is open — lunch, dinner, a late service.
    ///
    /// A window says WHEN you unlock the door. It does NOT say how busy you are: that comes
    /// from the <see cref="Neighborhood"/> you're in. Opening a window over hours when the
    /// street is empty is entirely allowed and entirely your problem — you pay the labor
    /// and the lights either way.
    ///
    /// The clock runs continuously around these; outside them nobody arrives, which is what
    /// makes most of a day compressible by jump-ahead.
    /// </summary>
    public sealed class ServiceWindow
    {
        public ServiceWindow(string name, int startHour, int endHour)
        {
            if (startHour < 0 || startHour > 23) throw new ArgumentOutOfRangeException(nameof(startHour));
            if (endHour < 0 || endHour > 24) throw new ArgumentOutOfRangeException(nameof(endHour));
            if (endHour == startHour) throw new ArgumentOutOfRangeException(nameof(endHour), "A window needs a length. For all day, use 0 to 24.");

            Name = string.IsNullOrWhiteSpace(name) ? "Service" : name;
            StartHour = startHour;
            EndHour = endHour;
        }

        public string Name { get; }
        public int StartHour { get; }
        public int EndHour { get; }

        /// <summary>
        /// True for a late service that runs past midnight, like 22:00-02:00. Hours are the
        /// operator's choice, so this has to be expressible.
        /// </summary>
        public bool WrapsMidnight { get { return EndHour < StartHour; } }

        public int LengthMinutes
        {
            get { return WrapsMidnight ? ((24 - StartHour) + EndHour) * 60 : (EndHour - StartHour) * 60; }
        }

        public bool IsOpenAt(DateTime now)
        {
            var minuteOfDay = (now.Hour * 60) + now.Minute;
            var opens = StartHour * 60;
            var closes = EndHour * 60;

            return WrapsMidnight
                ? minuteOfDay >= opens || minuteOfDay < closes
                : minuteOfDay >= opens && minuteOfDay < closes;
        }

        /// <summary>
        /// Total potential parties this window could see in the neighborhood it sits in.
        /// This is how a player can tell, before committing to the labor, whether a service
        /// is worth opening at all.
        /// </summary>
        public double PotentialPartiesIn(Neighborhood neighborhood)
        {
            if (neighborhood == null) throw new ArgumentNullException(nameof(neighborhood));

            var total = 0.0;
            var hour = StartHour;

            for (var i = 0; i < LengthMinutes / 60; i++)
            {
                total += neighborhood.TrafficAtHour(hour);
                hour = (hour + 1) % 24;
            }

            return total;
        }

        public override string ToString()
        {
            return Name + " " + StartHour.ToString("00") + ":00-" + EndHour.ToString("00") + ":00";
        }

        /// <summary>A conventional two-service day, used when a restaurant hasn't set its own hours.</summary>
        public static IEnumerable<ServiceWindow> DefaultDay()
        {
            yield return new ServiceWindow("Lunch", 12, 15);
            yield return new ServiceWindow("Dinner", 18, 23);
        }
    }
}
