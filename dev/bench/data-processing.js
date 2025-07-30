/**
 * Data processing and filtering functions
 */

/**
 * Collects benchmark data per test case
 * @param {Array} entries - Array of benchmark entries
 * @returns {Map} Map of test cases with their benchmark results
 */
export function collectBenchesPerTestCase(entries) {
  const map = new Map();
  for (const entry of entries) {
    const { commit, date, tool, benches } = entry;
    for (const bench of benches) {
      const result = { commit, date, tool, bench };
      const arr = map.get(bench.name);
      if (arr === undefined) {
        map.set(bench.name, [result]);
      } else {
        arr.push(result);
      }
    }
  }
  return map;
}

/**
 * Filters benchmark entries by date range
 * @param {Array} entries - Array of benchmark entries
 * @param {Date} startDate - Start date for filtering
 * @param {Date} endDate - End date for filtering
 * @returns {Array} Filtered entries within the date range
 */
export function filterEntriesByDateRange(entries, startDate, endDate) {
  if (!startDate && !endDate) {
    return entries;
  }

  return entries.filter((entry) => {
    const entryDate = new Date(entry.commit.timestamp || entry.date);

    if (startDate && entryDate < startDate) {
      return false;
    }

    if (endDate && entryDate > endDate) {
      return false;
    }

    return true;
  });
}

/**
 * Gets all unique commit points from benchmark entries
 * @param {Object} dataEntries - Data entries object
 * @returns {Array} Array of commit objects sorted by date
 */
export function getCommitPointsFromEntries(dataEntries) {
  const commitMap = new Map();

  Object.values(dataEntries).forEach((entries) => {
    entries.forEach((entry) => {
      const commitId = entry.commit.id;
      if (!commitMap.has(commitId)) {
        commitMap.set(commitId, {
          id: commitId,
          timestamp: entry.commit.timestamp || entry.date,
          date: new Date(entry.commit.timestamp || entry.date),
          message: entry.commit.message,
          author: entry.commit.author,
          url: entry.commit.url,
        });
      }
    });
  });

  // Sort commits by date
  return Array.from(commitMap.values()).sort((a, b) => a.date - b.date);
}

/**
 * Gets the date range from all benchmark entries
 * @param {Object} dataEntries - Data entries object
 * @returns {Object} Object with minDate and maxDate
 */
export function getDateRangeFromEntries(dataEntries) {
  let minDate = null;
  let maxDate = null;

  Object.values(dataEntries).forEach((entries) => {
    entries.forEach((entry) => {
      const entryDate = new Date(entry.commit.timestamp || entry.date);

      if (!minDate || entryDate < minDate) {
        minDate = entryDate;
      }

      if (!maxDate || entryDate > maxDate) {
        maxDate = entryDate;
      }
    });
  });

  return { minDate, maxDate };
}
