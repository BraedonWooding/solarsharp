/**
 * Utility functions for the benchmark visualization application
 */

/**
 * Groups items by a key generated from a keyGetter function
 * @param {Array} list - Array of items to group
 * @param {Function} keyGetter - Function that generates a key from each item
 * @returns {Map} Map with grouped items
 */
export function groupBy(list, keyGetter) {
  const map = new Map();
  for (const item of list) {
    const key = keyGetter(item);
    if (!key) {
      continue; // Skip items without a key
    }

    const collection = map.get(key);
    if (!collection) {
      map.set(key, [item]);
    } else {
      collection.push(item);
    }
  }
  return map;
}

/**
 * Converts time values to milliseconds from various units
 * @param {number} value - The time value to convert
 * @param {string} unit - The current unit (s, ms, us, ns)
 * @returns {number} Value converted to milliseconds
 */
export function convertToMilliseconds(value, unit) {
  switch (unit) {
    case "s":
      return value * 1000; // seconds to milliseconds
    case "ms":
      return value; // already in milliseconds
    case "us":
      return value / 1000; // microseconds to milliseconds
    case "ns":
      return value / 1000000; // nanoseconds to milliseconds
    default:
      return value;
  }
}

/**
 * Extracts clean implementation name from benchmark name
 * @param {string} implementationName - Raw implementation name
 * @returns {string} Clean implementation name
 */
export function getCleanImplementationName(implementationName) {
  const match = implementationName.match(/Implementation:\s*([^,)]+)/);
  return match ? match[1] : implementationName;
}

/**
 * Formats a date for short display
 * @param {Date} date - Date to format
 * @returns {string} Formatted date string
 */
export function formatDateShort(date) {
  return date.toLocaleDateString("en-US", { month: "short", day: "numeric" });
}

/**
 * Formats a date for long display
 * @param {Date} date - Date to format
 * @returns {string} Formatted date string
 */
export function formatDateLong(date) {
  return date.toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}
