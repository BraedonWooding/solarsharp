/**
 * Date filtering and scrubber functionality
 */

import { DATE_FILTER_CONFIG } from "./config.js";
import { formatDateShort, formatDateLong } from "./utils.js";
import {
  filterEntriesByDateRange,
  collectBenchesPerTestCase,
  getDateRangeFromEntries,
  getCommitPointsFromEntries,
} from "./data-processing.js";

/**
 * Initializes the date filter controls
 * @param {Object} data - Benchmark data object
 * @returns {Object} Object with filtered data and date range
 */
export function initializeDateFilter(data) {
  // Get date range from data
  const { minDate, maxDate } = getDateRangeFromEntries(data.entries);

  // Get all commit points
  const commitPoints = getCommitPointsFromEntries(data.entries);

  if (!minDate || !maxDate) {
    return { filteredData: data, dateRange: { minDate, maxDate } };
  }

  // Create date filter container
  const dateFilterContainer = createDateFilterContainer();

  // Create the date scrubber with commit points
  const scrubber = createDateScrubber(
    minDate,
    maxDate,
    minDate,
    maxDate,
    commitPoints
  );
  dateFilterContainer.appendChild(scrubber.container);

  // Create reset button
  const resetButton = createResetButton();
  dateFilterContainer.appendChild(resetButton);

  // Store references for global access
  window.dateFilterControls = {
    scrubber,
    minDate,
    maxDate,
    commitPoints,
    originalData: data,
  };

  // Set up automatic filtering when timeline changes
  scrubber.onDateChange(applyDateFilter);

  // Set up event listeners
  resetButton.addEventListener("click", resetDateFilter);

  // Apply initial filter
  const filteredData = getFilteredData();

  return { filteredData, dateRange: { minDate, maxDate } };
}

/**
 * Creates the date filter container
 * @returns {HTMLElement} Date filter container element
 */
function createDateFilterContainer() {
  let container = document.getElementById("date-filter");

  if (!container) {
    container = document.createElement("div");
    container.id = "date-filter";
    container.className = "date-filter-section";

    const main = document.getElementById("main");
    main.parentNode.insertBefore(container, main);
  }

  // Style the container
  Object.assign(container.style, {
    backgroundColor: "#fff",
    border: "1px solid #dee2e6",
    borderRadius: "8px",
    padding: "20px",
    margin: "20px auto",
    maxWidth: "1200px",
    fontFamily: "Arial, sans-serif",
  });

  // Add title
  const title = document.createElement("h3");
  title.textContent = "Commit Range Filter:";
  Object.assign(title.style, {
    margin: "0 0 15px 0",
    fontSize: "1.1em",
    color: "#333",
  });
  container.appendChild(title);

  return container;
}

/**
 * Creates an enhanced timeline with commit cards
 * @param {Date} minDate - Minimum date
 * @param {Date} maxDate - Maximum date
 * @param {Date} initialStart - Initial start date
 * @param {Date} initialEnd - Initial end date
 * @param {Array} commitPoints - Array of commit point objects
 * @returns {Object} Timeline object with container and methods
 */
function createDateScrubber(
  minDate,
  maxDate,
  initialStart,
  initialEnd,
  commitPoints = []
) {
  const container = document.createElement("div");
  Object.assign(container.style, {
    position: "relative",
    width: "100%",
    minHeight: "50px",
    backgroundColor: "#fff",
    border: "1px solid #dee2e6",
    borderRadius: "8px",
    marginBottom: "15px",
  });

  // Create title
  const title = document.createElement("div");
  title.textContent = "Select Commit Range";
  Object.assign(title.style, {
    fontSize: "14px",
    fontWeight: "bold",
    color: "#333",
    marginBottom: "15px",
    textAlign: "center",
  });
  container.appendChild(title);

  // Create timeline line
  const timelineLine = document.createElement("div");
  Object.assign(timelineLine.style, {
    position: "relative",
    width: "100%",
    height: "4px",
    backgroundColor: "#dee2e6",
    borderRadius: "2px",
    margin: "20px 0",
  });
  container.appendChild(timelineLine);

  // State for tracking selected commits
  const timelineState = {
    selectedCommits: new Set(),
    commitElements: new Map(),
    minDate,
    maxDate,
    commitPoints,
    onDateChange: null,
  };

  // Create commit points along the timeline
  if (commitPoints.length > 0) {
    createCommitCardsOnTimeline(timelineLine, commitPoints, timelineState);
  }

  // Create selection display
  const selectionDisplay = document.createElement("div");
  Object.assign(selectionDisplay.style, {
    textAlign: "center",
    fontSize: "12px",
    color: "#495057",
    fontWeight: "bold",
    marginTop: "10px",
  });
  container.appendChild(selectionDisplay);

  // Initialize with all commits selected
  commitPoints.forEach((commit) => {
    timelineState.selectedCommits.add(commit.id);
    updateCommitPointStyle(timelineState.commitElements.get(commit.id), true);
  });
  updateSelectionDisplay(selectionDisplay, timelineState);

  return {
    container,
    getDateRange: () => getSelectedDateRange(timelineState),
    setDateRange: (start, end) =>
      setSelectedDateRange(start, end, timelineState, selectionDisplay),
    onDateChange: (callback) => {
      timelineState.onDateChange = callback;
    },
  };
}

/**
 * Creates commit cards along the timeline
 * @param {HTMLElement} timelineLine - The timeline line element
 * @param {Array} commitPoints - Array of commit objects
 * @param {Object} timelineState - Timeline state object
 */
function createCommitCardsOnTimeline(
  timelineLine,
  commitPoints,
  timelineState
) {
  if (commitPoints.length === 0) return;

  // Calculate positions for commits along the timeline
  const padding = 10; // 10% padding on each side
  const usableWidth = 100 - padding * 2;
  const spacing =
    commitPoints.length === 1 ? 0 : usableWidth / (commitPoints.length - 1);

  commitPoints.forEach((commit, index) => {
    const positionPercent =
      commitPoints.length === 1 ? 50 : padding + spacing * index;

    // Create commit point
    const commitPoint = document.createElement("div");
    Object.assign(commitPoint.style, {
      position: "absolute",
      left: `${positionPercent}%`,
      top: "-9px",
      width: "16px",
      height: "16px",
      backgroundColor: "#3572a5",
      borderRadius: "50%",
      transform: "translateX(-50%)",
      cursor: "pointer",
      border: "3px solid #fff",
      boxShadow: "0 2px 4px rgba(0,0,0,0.2)",
      transition: "all 0.2s ease",
      zIndex: "3",
    });

    // Create commit card (initially hidden)
    const commitCard = createCommitCard(commit);
    Object.assign(commitCard.style, {
      position: "absolute",
      left: `${positionPercent}%`,
      top: "30px",
      transform: "translateX(-50%)",
      display: "none",
      zIndex: "10",
    });

    // Add hover effects
    commitPoint.addEventListener("mouseenter", () => {
      commitCard.style.display = "block";
      commitPoint.style.backgroundColor = "#2c5aa0";
      commitPoint.style.transform = "translateX(-50%) scale(1.2)";
    });

    commitPoint.addEventListener("mouseleave", () => {
      commitCard.style.display = "none";
      updateCommitPointStyle(
        commitPoint,
        timelineState.selectedCommits.has(commit.id)
      );
    });

    // Add click handler for selection
    commitPoint.addEventListener("click", (e) => {
      e.stopPropagation();
      toggleCommitSelection(commit, timelineState);
      updateCommitPointStyle(
        commitPoint,
        timelineState.selectedCommits.has(commit.id)
      );
      updateSelectionDisplay(timelineState.selectionDisplay, timelineState);
      triggerTimelineChange(timelineState);
    });

    // Store reference to commit element
    timelineState.commitElements.set(commit.id, commitPoint);

    // Add to timeline
    timelineLine.appendChild(commitPoint);
    timelineLine.appendChild(commitCard);

    // Set initial style
    updateCommitPointStyle(
      commitPoint,
      timelineState.selectedCommits.has(commit.id)
    );
  });
}

/**
 * Creates a commit card with commit information
 * @param {Object} commit - Commit object
 * @returns {HTMLElement} Commit card element
 */
function createCommitCard(commit) {
  const card = document.createElement("div");
  Object.assign(card.style, {
    backgroundColor: "#fff",
    border: "1px solid #dee2e6",
    borderRadius: "8px",
    padding: "12px",
    minWidth: "200px",
    maxWidth: "300px",
    boxShadow: "0 4px 12px rgba(0,0,0,0.1)",
    fontSize: "12px",
    lineHeight: "1.4",
  });

  // Commit ID and short message
  const header = document.createElement("div");
  Object.assign(header.style, {
    fontWeight: "bold",
    color: "#3572a5",
    marginBottom: "6px",
  });
  header.textContent = `${commit.id.slice(0, 7)} - ${commit.message
    .split("\n")[0]
    .slice(0, 50)}${commit.message.split("\n")[0].length > 50 ? "..." : ""}`;

  // Date
  const date = document.createElement("div");
  Object.assign(date.style, {
    color: "#6c757d",
    fontSize: "11px",
    marginBottom: "6px",
  });
  date.textContent = formatDateLong(commit.date);

  // Author
  const author = document.createElement("div");
  Object.assign(author.style, {
    color: "#495057",
    fontSize: "11px",
  });
  author.textContent = `By ${commit.author.name}`;

  card.appendChild(header);
  card.appendChild(date);
  card.appendChild(author);

  return card;
}

/**
 * Updates the visual style of a commit point based on selection state
 * @param {HTMLElement} commitPoint - The commit point element
 * @param {boolean} isSelected - Whether the commit is selected
 */
function updateCommitPointStyle(commitPoint, isSelected) {
  if (isSelected) {
    commitPoint.style.backgroundColor = "#3572a5";
    commitPoint.style.borderColor = "#fff";
    commitPoint.style.transform = "translateX(-50%) scale(1)";
  } else {
    commitPoint.style.backgroundColor = "#dee2e6";
    commitPoint.style.borderColor = "#fff";
    commitPoint.style.transform = "translateX(-50%) scale(0.8)";
  }
}

/**
 * Toggles the selection state of a commit
 * @param {Object} commit - Commit object
 * @param {Object} timelineState - Timeline state object
 */
function toggleCommitSelection(commit, timelineState) {
  if (timelineState.selectedCommits.has(commit.id)) {
    timelineState.selectedCommits.delete(commit.id);
  } else {
    timelineState.selectedCommits.add(commit.id);
  }
}

/**
 * Updates the selection display text
 * @param {HTMLElement} display - Display element
 * @param {Object} timelineState - Timeline state object
 */
function updateSelectionDisplay(display, timelineState) {
  // Store reference to display element
  timelineState.selectionDisplay = display;

  const selectedCount = timelineState.selectedCommits.size;
  const totalCount = timelineState.commitPoints.length;

  if (selectedCount === 0) {
    display.textContent = "No commits selected";
  } else if (selectedCount === totalCount) {
    display.textContent = `All ${totalCount} commits selected`;
  } else {
    display.textContent = `${selectedCount} of ${totalCount} commits selected`;
  }
}

/**
 * Gets the date range from selected commits
 * @param {Object} timelineState - Timeline state object
 * @returns {Object} Object with startDate and endDate
 */
function getSelectedDateRange(timelineState) {
  if (timelineState.selectedCommits.size === 0) {
    return {
      startDate: timelineState.minDate,
      endDate: timelineState.maxDate,
    };
  }

  const selectedCommitObjects = timelineState.commitPoints.filter((commit) =>
    timelineState.selectedCommits.has(commit.id)
  );

  const dates = selectedCommitObjects.map((commit) => commit.date);
  const minDate = new Date(Math.min(...dates));
  const maxDate = new Date(Math.max(...dates));

  return {
    startDate: minDate,
    endDate: maxDate,
  };
}

/**
 * Sets the selected date range by selecting commits within the range
 * @param {Date} startDate - Start date
 * @param {Date} endDate - End date
 * @param {Object} timelineState - Timeline state object
 * @param {HTMLElement} display - Selection display element
 */
function setSelectedDateRange(startDate, endDate, timelineState, display) {
  // Clear current selection
  timelineState.selectedCommits.clear();

  // Select commits within the date range
  timelineState.commitPoints.forEach((commit) => {
    if (commit.date >= startDate && commit.date <= endDate) {
      timelineState.selectedCommits.add(commit.id);
    }
  });

  // Update visual states
  timelineState.commitElements.forEach((element, commitId) => {
    updateCommitPointStyle(
      element,
      timelineState.selectedCommits.has(commitId)
    );
  });

  updateSelectionDisplay(display, timelineState);
}

/**
 * Triggers the timeline change callback
 * @param {Object} timelineState - Timeline state object
 */
function triggerTimelineChange(timelineState) {
  if (timelineState.onDateChange) {
    // Debounce the callback to avoid too frequent updates
    clearTimeout(timelineState.changeTimeout);
    timelineState.changeTimeout = setTimeout(() => {
      timelineState.onDateChange();
    }, 100);
  }
}

/**
 * Creates the reset button
 * @returns {HTMLElement} Reset button element
 */
function createResetButton() {
  const button = document.createElement("button");
  button.textContent = "Select All Commits";
  button.id = "reset-date-filter";
  Object.assign(button.style, {
    padding: "8px 16px",
    backgroundColor: "#6c757d",
    color: "white",
    border: "none",
    borderRadius: "4px",
    cursor: "pointer",
    fontSize: "14px",
    fontWeight: "bold",
    marginTop: "10px",
  });

  button.addEventListener("mouseenter", () => {
    button.style.backgroundColor = "#5a6268";
  });

  button.addEventListener("mouseleave", () => {
    button.style.backgroundColor = "#6c757d";
  });

  return button;
}

/**
 * Gets filtered data based on selected commits
 * @returns {Object} Filtered data object
 */
function getFilteredData() {
  const controls = window.dateFilterControls;
  const originalData = controls.originalData;
  const filteredEntries = {};

  // If no commits are selected, return empty data
  if (controls.scrubber && controls.scrubber.getDateRange) {
    const { startDate, endDate } = controls.scrubber.getDateRange();

    Object.keys(originalData.entries).forEach((key) => {
      filteredEntries[key] = filterEntriesByDateRange(
        originalData.entries[key],
        startDate,
        endDate
      );
    });
  } else {
    // Fallback to original data if scrubber is not available
    return originalData.entries;
  }

  return filteredEntries;
}

/**
 * Applies the date filter and re-renders charts
 */
export function applyDateFilter() {
  const controls = window.dateFilterControls;

  // Get filtered data
  const filteredData = getFilteredData();

  // Prepare new datasets
  const newDataSets = Object.keys(filteredData).map((name) => ({
    name,
    dataSet: collectBenchesPerTestCase(filteredData[name]),
  }));

  // Clear existing charts
  clearCharts();

  // Re-render with filtered data (imported from app.js)
  if (window.renderAllCharts) {
    window.renderAllCharts(newDataSets);
  }
}

/**
 * Resets the date filter to show all data
 */
export function resetDateFilter() {
  const controls = window.dateFilterControls;

  // Reset scrubber to full range
  controls.scrubber.setDateRange(controls.minDate, controls.maxDate);

  // Apply filter with full range
  applyDateFilter();
}

window.clearCharts = clearCharts;

/**
 * Clears all existing charts
 */
export function clearCharts() {
  // Destroy existing Chart.js instances
  if (window.globalCharts) {
    window.globalCharts.forEach((chart) => {
      chart.destroy();
    });
    window.globalCharts = [];
  }

  // Clear the main container content
  const main = document.getElementById("main");
  if (main) {
    main.innerHTML = "";
  }
}
