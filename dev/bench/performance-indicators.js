/**
 * Performance change indicators
 */

import { IMPLEMENTATION_COLORS, STATISTICAL_SIGNIFICANCE_CONFIG } from './config.js';
import { convertToMilliseconds, getCleanImplementationName } from './utils.js';

/**
 * Determines if a performance change is statistically significant
 * @param {Object} latest - Latest benchmark result
 * @param {Object} previous - Previous benchmark result
 * @param {number} change - Percentage change
 * @param {number} latestValueMs - Latest value in milliseconds
 * @param {number} previousValueMs - Previous value in milliseconds
 * @returns {boolean} Whether the change is significant
 */
function isStatisticallySignificant(
  latest,
  previous,
  change,
  latestValueMs,
  previousValueMs
) {
  let isSignificant = false;

  if (latest.bench.range && previous.bench.range) {
    // Extract standard deviation from range (format: "± 64768.29586634039")
    const latestStdevRaw = parseFloat(latest.bench.range.replace(/[±\s]/g, ""));
    const previousStdevRaw = parseFloat(
      previous.bench.range.replace(/[±\s]/g, "")
    );

    // Convert standard deviations to milliseconds
    const latestStdev = convertToMilliseconds(
      latestStdevRaw,
      latest.bench.unit
    );
    const previousStdev = convertToMilliseconds(
      previousStdevRaw,
      previous.bench.unit
    );

    // Calculate the absolute difference between values (in ms)
    const absoluteChange = Math.abs(latestValueMs - previousValueMs);

    // Use combined standard error (sqrt of sum of variances)
    const combinedStdev = Math.sqrt(
      latestStdev * latestStdev + previousStdev * previousStdev
    );

    // Consider significant if change meets all criteria
    isSignificant =
      absoluteChange >
        STATISTICAL_SIGNIFICANCE_CONFIG.CONFIDENCE_LEVEL * combinedStdev &&
      absoluteChange >=
        STATISTICAL_SIGNIFICANCE_CONFIG.MIN_ABSOLUTE_CHANGE_MS &&
      Math.abs(change) >= STATISTICAL_SIGNIFICANCE_CONFIG.MIN_PERCENTAGE_CHANGE;
  } else {
    // Fallback: consider significant if change meets basic criteria
    const absoluteChange = Math.abs(latestValueMs - previousValueMs);
    isSignificant =
      Math.abs(change) >=
        STATISTICAL_SIGNIFICANCE_CONFIG.MIN_PERCENTAGE_CHANGE &&
      absoluteChange >= STATISTICAL_SIGNIFICANCE_CONFIG.MIN_ABSOLUTE_CHANGE_MS;
  }

  return isSignificant;
}

/**
 * Creates a performance change indicator element
 * @param {Object} latest - Latest benchmark result
 * @param {Object} previous - Previous benchmark result
 * @param {number} change - Percentage change
 * @param {number} latestValueMs - Latest value in milliseconds
 * @param {number} previousValueMs - Previous value in milliseconds
 * @returns {HTMLElement} Change indicator element
 */
function createChangeIndicator(
  latest,
  previous,
  change,
  latestValueMs,
  previousValueMs
) {
  const changeIndicator = document.createElement("div");
  Object.assign(changeIndicator.style, {
    display: "inline-flex",
    alignItems: "center",
    padding: "4px 8px",
    borderRadius: "12px",
    fontSize: "12px",
    fontWeight: "bold",
    border: "1px solid",
  });

  const implementation = latest.bench.name;
  const color = IMPLEMENTATION_COLORS[implementation] || "#333333";
  const cleanImplementationName = getCleanImplementationName(implementation);

  if (change > 0) {
    // Performance regression (slower = bad)
    Object.assign(changeIndicator.style, {
      backgroundColor: "#fff5f5",
      color: "#dc3545",
      borderColor: "#dc3545",
    });
    const deltaMs = (latestValueMs - previousValueMs).toFixed(2);
    changeIndicator.textContent = `${cleanImplementationName}: +${change.toFixed(
      1
    )}% (+${deltaMs}ms)`;
  } else {
    // Performance improvement (faster = good)
    Object.assign(changeIndicator.style, {
      backgroundColor: "#f0fff4",
      color: "#28a745",
      borderColor: "#28a745",
    });
    const deltaMs = Math.abs(latestValueMs - previousValueMs).toFixed(2);
    changeIndicator.textContent = `${cleanImplementationName}: ${change.toFixed(
      1
    )}% (-${deltaMs}ms)`;
  }

  // Add implementation color dot
  const colorDot = document.createElement("span");
  Object.assign(colorDot.style, {
    display: "inline-block",
    width: "8px",
    height: "8px",
    borderRadius: "50%",
    backgroundColor: color,
    marginRight: "6px",
  });
  changeIndicator.insertBefore(colorDot, changeIndicator.firstChild);

  // Add tooltip and data attribute
  changeIndicator.title = `Statistically significant change (>2σ confidence)`;
  changeIndicator.setAttribute("data-implementation", cleanImplementationName);

  return changeIndicator;
}

/**
 * Calculates and creates performance change indicators
 * @param {Array} benches - Array of benchmark data
 * @returns {HTMLElement} Container with change indicators
 */
export function createPerformanceChangeIndicators(benches) {
  const changesContainer = document.createElement("div");
  changesContainer.className = "performance-changes";
  Object.assign(changesContainer.style, {
    display: "flex",
    flexWrap: "wrap",
    gap: "8px",
    marginBottom: "15px",
  });

  benches
    .map((b) => b[1])
    .forEach((implementationData) => {
      if (implementationData.length >= 2) {
        const latest = implementationData[implementationData.length - 1];
        const previous = implementationData[implementationData.length - 2];

        const latestValueMs = convertToMilliseconds(
          latest.bench.value,
          latest.bench.unit
        );
        const previousValueMs = convertToMilliseconds(
          previous.bench.value,
          previous.bench.unit
        );
        const change =
          ((latestValueMs - previousValueMs) / previousValueMs) * 100;

        if (
          isStatisticallySignificant(
            latest,
            previous,
            change,
            latestValueMs,
            previousValueMs
          )
        ) {
          const changeIndicator = createChangeIndicator(
            latest,
            previous,
            change,
            latestValueMs,
            previousValueMs
          );
          changesContainer.appendChild(changeIndicator);
        }
      }
    });

  return changesContainer;
}

/**
 * Updates change indicators visibility based on chart visibility
 */
export function updateChangeIndicatorsVisibility() {
  const changeIndicators = document.querySelectorAll("[data-implementation]");
  changeIndicators.forEach((indicator) => {
    const implementation = indicator.getAttribute("data-implementation");
    let isVisible = true;

    if (window.globalCharts && window.globalCharts.length > 0) {
      const datasetIndex = window.globalCharts[0].data.datasets.findIndex(
        (dataset) => dataset.label === implementation
      );
      if (datasetIndex !== -1) {
        isVisible =
          window.globalCharts[0].isDatasetVisible(datasetIndex) !== false;
      }
    }

    indicator.style.display = isVisible ? "inline-flex" : "none";
  });
}
