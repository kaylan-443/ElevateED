using System;

namespace ElevateED.Services
{
    /// <summary>
    /// Central place for the school's GPS coordinates and the geofence radius
    /// used to auto-verify deliveries.
    ///
    /// ⚠️ PLACEHOLDER — replace with the actual school location before go-live.
    /// Current values point roughly at 47 Mallingson Road, Overport, Durban.
    /// </summary>
    public static class SchoolLocation
    {
        // ── Change these two numbers to the real school coordinates ──
        public static readonly double Latitude = -29.832893;
        public static readonly double Longitude = 30.986404;
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// How close (in metres) the donor's GPS must be to the school
        /// for the delivery to auto-confirm.
        /// 200 m is generous enough to cover a large school campus and
        /// GPS jitter on phones, but tight enough to catch "I confirmed
        /// from home" cheating.
        /// </summary>
        public const double RadiusMeters = 200;

        /// <summary>
        /// Haversine distance between two coordinates, in metres.
        /// </summary>
        public static double DistanceInMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0; // Earth radius in metres

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double deg) => deg * Math.PI / 180.0;
    }
}