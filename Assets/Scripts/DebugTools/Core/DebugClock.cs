using System;
using System.Globalization;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public static class DebugClock
    {
        public static string TimestampForDirectory(DateTime localTime)
        {
            return localTime.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        }

        public static string ToIsoUtc(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
    }
}
