﻿using System;
using System.Collections.Generic;

namespace SolarSharp.Interpreter.DataStructs
{
    /// <summary>
    /// Extension methods used in the whole project.
    /// </summary>
    internal static class Extension_Methods
    {
        /// <summary>
        /// Gets a value from the dictionary or returns the default value
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
        {

            if (dictionary.TryGetValue(key, out TValue v))
                return v;

            return default;
        }


        /// <summary>
        /// Gets a value from the dictionary or creates it
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key.</param>
        /// <param name="creator">A function which will create the value if it doesn't exist.</param>
        /// <returns></returns>
        public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TValue> creator)
        {

            if (!dictionary.TryGetValue(key, out TValue v))
            {
                v = creator();
                dictionary.Add(key, v);
            }

            return v;
        }


    }
}
