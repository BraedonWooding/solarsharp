/**
 * Legend and control components
 */

import { IMPLEMENTATION_COLORS } from './config.js';

/**
 * Creates and configures the shared legend container
 * @param {HTMLElement} main - Main container element
 * @returns {HTMLElement} Legend container element
 */
export function createSharedLegend(main) {
  const legendContainer = document.createElement("div");
  legendContainer.className = "shared-legend";
  Object.assign(legendContainer.style, {
    display: "flex",
    justifyContent: "center",
    flexWrap: "wrap",
    gap: "20px",
    padding: "20px",
    backgroundColor: "#f8f9fa",
    border: "1px solid #dee2e6",
    borderRadius: "8px",
    marginBottom: "20px",
  });
  main.appendChild(legendContainer);
  return legendContainer;
}

/**
 * Creates the "Show Only SolarSharp" toggle button
 * @param {Array} charts - Array of chart instances
 * @param {HTMLElement} legendContainer - Legend container element
 * @returns {HTMLElement} Button element
 */
export function createSolarSharpToggleButton(charts, legendContainer) {
  const showOnlyButton = document.createElement("button");
  showOnlyButton.textContent = "Show Only SolarSharp";
  Object.assign(showOnlyButton.style, {
    padding: "8px 16px",
    backgroundColor: "#3572a5",
    color: "white",
    border: "none",
    borderRadius: "4px",
    cursor: "pointer",
    fontSize: "14px",
    fontWeight: "bold",
    marginRight: "20px",
  });

  showOnlyButton.addEventListener("click", () => {
    const { onlySolarSharpVisible, hasSolarSharp } =
      checkSolarSharpVisibility(charts);
    const showAll = onlySolarSharpVisible && hasSolarSharp;

    toggleChartVisibility(charts, showAll);
    updateLegendAppearance(legendContainer, showAll);
    updateButtonText(showOnlyButton, showAll);
    if (window.updateChangeIndicatorsVisibility) {
      window.updateChangeIndicatorsVisibility();
    }
  });

  return showOnlyButton;
}

/**
 * Checks if only SolarSharp implementation is currently visible
 * @param {Array} charts - Array of chart instances
 * @returns {Object} Object with visibility status
 */
function checkSolarSharpVisibility(charts) {
  let onlySolarSharpVisible = true;
  let hasSolarSharp = false;

  if (charts.length > 0) {
    charts[0].data.datasets.forEach((dataset, index) => {
      const isVisible = charts[0].isDatasetVisible(index) !== false;
      if (dataset.label === "SolarSharpImplementation") {
        hasSolarSharp = true;
        if (!isVisible) {
          onlySolarSharpVisible = false;
        }
      } else if (isVisible) {
        onlySolarSharpVisible = false;
      }
    });
  }

  return { onlySolarSharpVisible, hasSolarSharp };
}

/**
 * Toggles chart dataset visibility
 * @param {Array} charts - Array of chart instances
 * @param {boolean} showAll - Whether to show all or only SolarSharp
 */
function toggleChartVisibility(charts, showAll) {
  charts.forEach((chart) => {
    chart.data.datasets.forEach((dataset, index) => {
      if (showAll) {
        chart.setDatasetVisibility(index, true);
      } else {
        const shouldShow = dataset.label === "SolarSharpImplementation";
        chart.setDatasetVisibility(index, shouldShow);
      }
    });
    chart.update();
  });
}

/**
 * Updates legend item appearances
 * @param {HTMLElement} legendContainer - Legend container element
 * @param {boolean} showAll - Whether all items should be shown
 */
function updateLegendAppearance(legendContainer, showAll) {
  const legendItems = legendContainer.querySelectorAll("div:not(:first-child)");
  legendItems.forEach((item) => {
    const label = item.querySelector("span").textContent;
    item.style.opacity =
      showAll || label === "SolarSharpImplementation" ? "1" : "0.5";
  });
}

/**
 * Updates the toggle button text
 * @param {HTMLElement} button - Button element
 * @param {boolean} showAll - Current visibility state
 */
function updateButtonText(button, showAll) {
  button.textContent = showAll ? "Show Only SolarSharp" : "Show All";
}

/**
 * Creates a legend item for an implementation
 * @param {string} implementation - Implementation name
 * @param {Array} charts - Array of chart instances
 * @returns {HTMLElement} Legend item element
 */
export function createLegendItem(implementation, charts) {
  const legendItem = document.createElement("div");
  Object.assign(legendItem.style, {
    display: "flex",
    alignItems: "center",
    cursor: "pointer",
    userSelect: "none",
  });

  const colorBox = document.createElement("div");
  Object.assign(colorBox.style, {
    width: "20px",
    height: "20px",
    backgroundColor: IMPLEMENTATION_COLORS[implementation] || "#333333",
    marginRight: "8px",
    border: "1px solid #ccc",
  });

  const label = document.createElement("span");
  label.textContent = implementation;
  label.style.fontSize = "14px";

  legendItem.appendChild(colorBox);
  legendItem.appendChild(label);

  // Add click handler for show/hide functionality
  legendItem.addEventListener("click", () => {
    toggleImplementationVisibility(charts, implementation);
    updateLegendItemAppearance(legendItem, charts, implementation);
    if (window.updateChangeIndicatorsVisibility) {
      window.updateChangeIndicatorsVisibility();
    }
  });

  return legendItem;
}

/**
 * Toggles visibility for a specific implementation
 * @param {Array} charts - Array of chart instances
 * @param {string} implementation - Implementation name to toggle
 */
function toggleImplementationVisibility(charts, implementation) {
  charts.forEach((chart) => {
    const datasetIndex = chart.data.datasets.findIndex(
      (dataset) => dataset.label === implementation
    );
    if (datasetIndex !== -1) {
      const isHidden = chart.isDatasetVisible(datasetIndex) === false;
      chart.setDatasetVisibility(datasetIndex, isHidden);
      chart.update();
    }
  });
}

/**
 * Updates the appearance of a legend item based on visibility
 * @param {HTMLElement} legendItem - Legend item element
 * @param {Array} charts - Array of chart instances
 * @param {string} implementation - Implementation name
 */
function updateLegendItemAppearance(legendItem, charts, implementation) {
  const isHidden =
    charts.length > 0 &&
    charts[0].data.datasets.some(
      (dataset) =>
        dataset.label === implementation &&
        charts[0].isDatasetVisible(charts[0].data.datasets.indexOf(dataset)) ===
          false
    );
  legendItem.style.opacity = isHidden ? "0.5" : "1";
}
