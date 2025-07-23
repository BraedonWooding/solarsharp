using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// High-performance string cache for common conversions
    /// </summary>
    internal static class StringCache
    {
        // Cache for small integer to string conversions (0-1000)
        private static readonly string[] SmallIntegerCache = new string[1001];
        
        // Thread-safe cache for other number to string conversions
        private static readonly ConcurrentDictionary<double, string> NumberStringCache = 
            new ConcurrentDictionary<double, string>();
        
        // Maximum size of the number string cache to prevent unbounded growth
        private const int MaxCacheSize = 10000;
        
        static StringCache()
        {
            // Pre-populate small integer cache
            for (var i = 0; i <= 1000; i++)
            {
                SmallIntegerCache[i] = i.ToString(CultureInfo.InvariantCulture);
            }
        }
        
        /// <summary>
        /// Get cached string representation of a number
        /// </summary>
        public static string NumberToString(double number)
        {
            // Fast path for small integers
            if (number >= 0 && number <= 1000 && number == Math.Floor(number))
            {
                return SmallIntegerCache[(int)number];
            }
            
            // Check if already cached
            if (NumberStringCache.TryGetValue(number, out var cached))
            {
                return cached;
            }
            
            // Convert and cache if under limit
            var str = number.ToString(CultureInfo.InvariantCulture);
            if (NumberStringCache.Count < MaxCacheSize)
            {
                NumberStringCache.TryAdd(number, str);
            }
            
            return str;
        }
        
        /// <summary>
        /// Clear the dynamic cache (keeps small integer cache)
        /// </summary>
        public static void ClearCache()
        {
            NumberStringCache.Clear();
        }
    }
}