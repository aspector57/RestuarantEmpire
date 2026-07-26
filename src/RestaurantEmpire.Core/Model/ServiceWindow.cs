using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// A period of the day when the restaurant is open and guests arrive — lunch, dinner.
    ///
    /// The clock runs continuously, 24 hours rolling from one day into the next. Service
    /// windows are what make most of that time uneventful: outside them nobody arrives, so
    /// the small hours are exactly the stretches jump-ahead should compress away. That is
    /// the design's Time contract — "active service windows (when Kitchen and Customers are
    /// live)" — rather than services being discrete blocks that appear from nowhere.
    /// </summary>
    public sealed class ServiceWindow
    {
        public ServiceWindow(string name, int startHour, int endHour, double peakPartiesPerHour)
        {
            if (startHour < 0 || startHour > 23) throw new ArgumentOutOfRangeException(nameof(startHour));
            if (endHour < 0 || endHour > 24) throw new ArgumentOutOfRangeException(nameof(endHour));
            if (endHour == startHour) throw new ArgumentOutOfRangeException(nameof(endHour), "A window needs a length. For all day, use 0 to 24.");
            if (peakPartiesPerHour < 0) throw new ArgumentOutOfRangeException(nameof(peakPartiesPerHour));

            Name = string.IsNullOrWhiteSpace(name) ? "Service" : name;
            StartHour = startHour;
            EndHour = endHour;
            PeakPartiesPerHour = peakPartiesPerHour;
        }

        public string Name { get; }
        public int StartHour { get; }
        public int EndHour { get; }

        /// <summary>Busiest arrival rate, reached in the middle of the window.</summary>
        public double PeakPartiesPerHour { get; }

        /// <summary>
        /// True for a late-night service that runs past midnight, like 22:00-02:00.
        /// Hours are the operator's choice, so this has to be expressible.
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
        /// Chance of a party arriving in this particular minute. Triangular: quiet at the
        /// doors, a peak mid-service, tapering to close — which is what produces a rush the
        /// kitchen has to survive rather than a flat trickle.
        ///
        /// Note what this means for a single all-day window: one long, shallow hump. A real
        /// round-the-clock restaurant does not work like that — it has a breakfast rush, a
        /// lunch rush and a late-night one. Express that as several windows with their own
        /// peaks rather than one 24-hour window, which is both more accurate and gives each
        /// service its own demand to staff against.
        /// </summary>
        public double ArrivalChanceAt(DateTime now)
        {
            if (!IsOpenAt(now)) return 0.0;

            var minuteOfDay = (now.Hour * 60) + now.Minute;
            var opens = StartHour * 60;
            var minutesIn = minuteOfDay >= opens ? minuteOfDay - opens : minuteOfDay + ((24 * 60) - opens);

            var through = (double)minutesIn / LengthMinutes;
            var peakWeight = 1.0 - Math.Abs((2.0 * through) - 1.0);

            return (PeakPartiesPerHour / 60.0) * peakWeight;
        }

        public override string ToString()
        {
            return Name + " " + StartHour.ToString("00") + ":00-" + EndHour.ToString("00") + ":00";
        }

        /// <summary>A conventional two-service day, used when a restaurant hasn't set its own hours.</summary>
        public static IEnumerable<ServiceWindow> DefaultDay()
        {
            yield return new ServiceWindow("Lunch", 12, 15, 14);
            yield return new ServiceWindow("Dinner", 18, 23, 25);
        }
    }
}
