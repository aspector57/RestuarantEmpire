using System;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Where the restaurant is, expressed as how many people are around at each hour.
    ///
    /// This is the answer to "who decides how busy you are?" — and it is NOT the player.
    /// The player picks the hours; the neighbourhood decides whether anybody is out there
    /// to walk through the door. A business district empties at 8pm no matter how much you
    /// would like a late service, and a nightlife quarter is dead at 8am however good your
    /// breakfast is.
    ///
    /// That asymmetry is the whole point. Before this, demand was a number the player typed
    /// in, so every trade-off sitting on top of it — the espresso machine you buy for
    /// breakfast, the labour of a long day — could be justified by simply declaring the
    /// traffic was there.
    ///
    /// Traffic here is potential parties per hour. What share of them you actually capture
    /// is a separate question that Reputation and Marketing will answer later; for now you
    /// get what the street gives you.
    /// </summary>
    public sealed class Neighbourhood
    {
        private readonly double[] _trafficByHour;

        public Neighbourhood(string id, string name, double[] trafficByHour)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Neighbourhood id is required.", nameof(id));
            if (trafficByHour == null || trafficByHour.Length != 24)
                throw new ArgumentException("A traffic profile needs exactly 24 hourly values.", nameof(trafficByHour));

            foreach (var value in trafficByHour)
            {
                if (value < 0) throw new ArgumentException("Traffic cannot be negative.", nameof(trafficByHour));
            }

            Id = id;
            Name = name ?? id;
            _trafficByHour = (double[])trafficByHour.Clone();
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>The hourly profile, for saving and for showing the player what they're buying into.</summary>
        public double[] TrafficByHour { get { return (double[])_trafficByHour.Clone(); } }

        public double TrafficAtHour(int hour)
        {
            return _trafficByHour[((hour % 24) + 24) % 24];
        }

        /// <summary>
        /// Potential parties per hour at this exact moment, interpolated between hourly
        /// figures so a rush builds and fades rather than switching on at the top of the hour.
        /// </summary>
        public double TrafficAt(DateTime now)
        {
            var thisHour = _trafficByHour[now.Hour];
            var nextHour = _trafficByHour[(now.Hour + 1) % 24];
            var throughHour = now.Minute / 60.0;

            return thisHour + ((nextHour - thisHour) * throughHour);
        }

        public double BusiestHourTraffic
        {
            get
            {
                var most = 0.0;
                foreach (var value in _trafficByHour) { if (value > most) most = value; }

                return most;
            }
        }

        /// <summary>
        /// Whether it is worth unlocking the door at this hour at all. Fewer than two
        /// parties an hour means a service that cannot come close to covering its own
        /// staffing, so it counts as dead rather than merely quiet.
        /// </summary>
        public bool IsDeadAtHour(int hour)
        {
            return TrafficAtHour(hour) < 2.0;
        }

        public override string ToString()
        {
            return Name;
        }

        // ---- Real places ----

        /// <summary>
        /// Offices and shops. Commuters at breakfast, a hard lunch rush, a decent dinner,
        /// and it thins out steadily after 10pm rather than dying outright.
        /// </summary>
        public static Neighbourhood CityCentre()
        {
            return new Neighbourhood("city-centre", "City Centre", new double[]
            {
            //  00   01   02   03   04   05   06   07   08   09   10   11
                2,   1,   0,   0,   0,   1,   4,  10,  16,  10,   7,  12,
            //  12   13   14   15   16   17   18   19   20   21   22   23
               24,  26,  16,   8,   7,  12,  20,  26,  24,  18,  11,   5
            });
        }

        /// <summary>
        /// Pure offices. Enormous breakfast and lunch, and then everyone goes home — after
        /// 8pm there is genuinely nobody there, so a late service is labour spent on an
        /// empty room.
        /// </summary>
        public static Neighbourhood BusinessDistrict()
        {
            return new Neighbourhood("business-district", "Business District", new double[]
            {
                0,   0,   0,   0,   0,   0,   5,  16,  22,  12,   6,  14,
               30,  32,  14,   5,   4,   6,   8,   5,   2,   1,   0,   0
            });
        }

        /// <summary>
        /// Where people live. Quiet by day, a real dinner trade, and it stops dead at 10pm.
        /// This is the case that makes "should I stay open late?" answer itself.
        /// </summary>
        public static Neighbourhood SuburbanHighStreet()
        {
            return new Neighbourhood("suburban-high-street", "Suburban High Street", new double[]
            {
                0,   0,   0,   0,   0,   0,   1,   3,   5,   4,   5,   8,
               13,  12,   7,   5,   6,  10,  19,  23,  20,  12,   3,   1
            });
        }

        /// <summary>
        /// Bars and clubs. Nothing before noon, and the busiest hours are the ones every
        /// other neighbourhood is asleep for.
        /// </summary>
        public static Neighbourhood NightlifeQuarter()
        {
            return new Neighbourhood("nightlife-quarter", "Nightlife Quarter", new double[]
            {
               22,  20,  14,   6,   2,   1,   0,   0,   0,   1,   2,   4,
                9,  11,   8,   6,   7,  11,  17,  22,  26,  28,  27,  25
            });
        }

        /// <summary>
        /// The same traffic at every hour. Not a real place — it exists so tests and
        /// balance work can hold location constant while varying something else.
        /// </summary>
        public static Neighbourhood Flat(double partiesPerHour)
        {
            var profile = new double[24];
            for (var i = 0; i < 24; i++) profile[i] = partiesPerHour;

            return new Neighbourhood("flat", "Flat " + partiesPerHour + "/hr", profile);
        }

        /// <summary>
        /// A single hump peaking in the middle of the given hours, zero outside them.
        /// Useful for isolating one service without the rest of a day's traffic.
        /// </summary>
        public static Neighbourhood PeakedBetween(string name, int startHour, int endHour, double peak)
        {
            var profile = new double[24];
            var length = endHour - startHour;
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(endHour));

            for (var hour = startHour; hour < endHour; hour++)
            {
                var through = (double)(hour - startHour) / length;
                profile[hour] = peak * (1.0 - Math.Abs((2.0 * through) - 1.0));
            }

            return new Neighbourhood("peaked", name, profile);
        }
    }
}
