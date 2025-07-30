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

  // Set up automatic filtering when scrubber changes
  scrubber.onDateChange(applyDateFilter);

  // Set up event listeners
  resetButton.addEventListener("click", resetDateFilter);

  // Apply initial filter
  const filteredData = getFilteredData(minDate, maxDate);

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

    // Insert after machine info or header
    const machineInfo = document.getElementById("machine-info");
    const main = document.getElementById("main");

    if (machineInfo && machineInfo.nextSibling) {
      machineInfo.parentNode.insertBefore(container, machineInfo.nextSibling);
    } else if (main) {
      main.parentNode.insertBefore(container, main);
    } else {
      document.body.insertBefore(container, document.body.firstChild);
    }
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
  title.textContent = "Date Range Filter:";
  Object.assign(title.style, {
    margin: "0 0 15px 0",
    fontSize: "1.1em",
    color: "#333",
  });
  container.appendChild(title);

  return container;
}

/**
 * Creates an interactive date scrubber component
 * @param {Date} minDate - Minimum date
 * @param {Date} maxDate - Maximum date
 * @param {Date} initialStart - Initial start date
 * @param {Date} initialEnd - Initial end date
 * @param {Array} commitPoints - Array of commit point objects
 * @returns {Object} Scrubber object with container and methods
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
    height: DATE_FILTER_CONFIG.SCRUBBER_HEIGHT + "px",
    backgroundColor: "#f8f9fa",
    border: "1px solid #dee2e6",
    borderRadius: "4px",
    cursor: "crosshair",
    marginBottom: "15px",
  });

  // Create timeline background
  const timeline = document.createElement("div");
  Object.assign(timeline.style, {
    position: "absolute",
    top: "0",
    left: "0",
    right: "0",
    bottom: "0",
    background:
      "linear-gradient(to right, #e9ecef 0%, #dee2e6 50%, #e9ecef 100%)",
  });
  container.appendChild(timeline);

  // Create commit points
  if (commitPoints.length > 0) {
    const commitPointsContainer = createCommitPoints(
      minDate,
      maxDate,
      commitPoints
    );
    container.appendChild(commitPointsContainer);
  }

  // Create date labels
  const dateLabels = createDateLabels(minDate, maxDate);
  container.appendChild(dateLabels);

  // Find initial commit points for snapping
  const initialStartCommit = findNearestCommitPoint(commitPoints, initialStart);
  const initialEndCommit = findNearestCommitPoint(commitPoints, initialEnd);

  // Use commit dates if available, otherwise use original dates
  const startDate = initialStartCommit ? initialStartCommit.date : initialStart;
  const endDate = initialEndCommit ? initialEndCommit.date : initialEnd;

  // Calculate initial selection position
  const totalMs = maxDate.getTime() - minDate.getTime();
  const startMs = startDate.getTime() - minDate.getTime();
  const endMs = endDate.getTime() - minDate.getTime();

  const startPercent = (startMs / totalMs) * 100;
  const endPercent = (endMs / totalMs) * 100;

  // Create selection area
  const selection = document.createElement("div");
  Object.assign(selection.style, {
    position: "absolute",
    top: "0",
    bottom: "0",
    left: startPercent + "%",
    width: endPercent - startPercent + "%",
    backgroundColor: "rgba(53, 114, 165, 0.3)",
    border: "2px solid #3572a5",
    cursor: "move",
    zIndex: "2",
  });
  container.appendChild(selection);

  // Create resize handles
  const leftHandle = createResizeHandle("left");
  const rightHandle = createResizeHandle("right");
  selection.appendChild(leftHandle);
  selection.appendChild(rightHandle);

  // Create date display
  const dateDisplay = createDateDisplay(startDate, endDate);
  container.appendChild(dateDisplay);

  // Set up interaction handlers
  const scrubberState = {
    isDragging: false,
    isResizing: false,
    resizeHandle: null,
    startX: 0,
    initialLeft: 0,
    initialWidth: 0,
    minDate,
    maxDate,
    commitPoints,
    onDateChange: null,
  };

  setupScrubberInteractions(
    container,
    selection,
    leftHandle,
    rightHandle,
    dateDisplay,
    scrubberState
  );

  return {
    container,
    getDateRange: () => getCurrentDateRange(selection, minDate, maxDate),
    setDateRange: (start, end) =>
      setDateRange(
        selection,
        start,
        end,
        minDate,
        maxDate,
        dateDisplay,
        commitPoints
      ),
    onDateChange: (callback) => {
      scrubberState.onDateChange = callback;
    },
  };
}

/**
 * Creates commit point indicators on the timeline
 * @param {Date} minDate - Minimum date
 * @param {Date} maxDate - Maximum date
 * @param {Array} commitPoints - Array of commit objects
 * @returns {HTMLElement} Commit points container
 */
function createCommitPoints(minDate, maxDate, commitPoints) {
  const commitPointsContainer = document.createElement("div");
  Object.assign(commitPointsContainer.style, {
    position: "absolute",
    top: "0",
    left: "0",
    right: "0",
    bottom: "0",
    pointerEvents: "none",
    zIndex: "1",
  });

  const totalMs = maxDate.getTime() - minDate.getTime();

  commitPoints.forEach((commit, index) => {
    const commitMs = commit.date.getTime() - minDate.getTime();
    const positionPercent = (commitMs / totalMs) * 100;

    const commitPoint = document.createElement("div");
    Object.assign(commitPoint.style, {
      position: "absolute",
      left: positionPercent + "%",
      top: "50%",
      width: "8px",
      height: "8px",
      backgroundColor: "#3572a5",
      borderRadius: "50%",
      transform: "translate(-50%, -50%)",
      border: "2px solid #fff",
      boxShadow: "0 0 3px rgba(0,0,0,0.3)",
      zIndex: "1",
    });

    // Add tooltip with commit info
    commitPoint.title = `${commit.id.slice(0, 7)} - ${
      commit.message.split("\n")[0]
    }`;

    commitPointsContainer.appendChild(commitPoint);
  });

  return commitPointsContainer;
}

/**
 * Finds the nearest commit point to a given date
 * @param {Array} commitPoints - Array of commit objects
 * @param {Date} targetDate - Target date to find nearest commit for
 * @returns {Object|null} Nearest commit object or null if no commits
 */
function findNearestCommitPoint(commitPoints, targetDate) {
  if (!commitPoints || commitPoints.length === 0) {
    return null;
  }

  let nearest = commitPoints[0];
  let minDifference = Math.abs(targetDate.getTime() - nearest.date.getTime());

  for (const commit of commitPoints) {
    const difference = Math.abs(targetDate.getTime() - commit.date.getTime());
    if (difference < minDifference) {
      minDifference = difference;
      nearest = commit;
    }
  }

  return nearest;
}

/**
 * Snaps a date to the nearest commit point
 * @param {Array} commitPoints - Array of commit objects
 * @param {Date} date - Date to snap
 * @returns {Date} Snapped date
 */
function snapToNearestCommit(commitPoints, date) {
  if (!commitPoints || commitPoints.length === 0) {
    return date;
  }
  const nearestCommit = findNearestCommitPoint(commitPoints, date);
  return nearestCommit ? nearestCommit.date : date;
}

/**
 * Creates date labels for the timeline
 * @param {Date} minDate - Minimum date
 * @param {Date} maxDate - Maximum date
 * @returns {HTMLElement} Date labels container
 */
function createDateLabels(minDate, maxDate) {
  const labelsContainer = document.createElement("div");
  Object.assign(labelsContainer.style, {
    position: "absolute",
    top: "8px",
    left: "15px",
    right: "15px",
    display: "flex",
    justifyContent: "space-between",
    fontSize: "20px",
    color: "#6c757d",
    pointerEvents: "none",
    zIndex: "1",
  });

  const startLabel = document.createElement("span");
  startLabel.textContent = formatDateShort(minDate);

  const endLabel = document.createElement("span");
  endLabel.textContent = formatDateShort(maxDate);

  labelsContainer.appendChild(startLabel);
  labelsContainer.appendChild(endLabel);

  return labelsContainer;
}

/**
 * Creates a resize handle
 * @param {string} side - 'left' or 'right'
 * @returns {HTMLElement} Handle element
 */
function createResizeHandle(side) {
  const handle = document.createElement("div");
  Object.assign(handle.style, {
    position: "absolute",
    top: "0",
    bottom: "0",
    width: DATE_FILTER_CONFIG.HANDLE_WIDTH + "px",
    backgroundColor: "#3572a5",
    cursor: side === "left" ? "w-resize" : "e-resize",
    zIndex: "3",
  });

  if (side === "left") {
    handle.style.left = -DATE_FILTER_CONFIG.HANDLE_WIDTH / 2 + "px";
  } else {
    handle.style.right = -DATE_FILTER_CONFIG.HANDLE_WIDTH / 2 + "px";
  }

  handle.dataset.side = side;
  return handle;
}

/**
 * Creates the date display element
 * @param {Date} startDate - Start date
 * @param {Date} endDate - End date
 * @returns {HTMLElement} Date display element
 */
function createDateDisplay(startDate, endDate) {
  const display = document.createElement("div");
  Object.assign(display.style, {
    position: "absolute",
    bottom: "-25px",
    left: "0",
    right: "0",
    textAlign: "center",
    fontSize: "12px",
    color: "#495057",
    fontWeight: "bold",
  });

  updateDateDisplay(display, startDate, endDate);
  return display;
}

/**
 * Updates the date display text
 * @param {HTMLElement} display - Display element
 * @param {Date} startDate - Start date
 * @param {Date} endDate - End date
 */
function updateDateDisplay(display, startDate, endDate) {
  display.textContent = `${formatDateLong(startDate)} — ${formatDateLong(
    endDate
  )}`;
}

/**
 * Sets up all scrubber interactions
 * @param {HTMLElement} container - Container element
 * @param {HTMLElement} selection - Selection element
 * @param {HTMLElement} leftHandle - Left resize handle
 * @param {HTMLElement} rightHandle - Right resize handle
 * @param {HTMLElement} dateDisplay - Date display element
 * @param {Object} state - Scrubber state object
 */
function setupScrubberInteractions(
  container,
  selection,
  leftHandle,
  rightHandle,
  dateDisplay,
  state
) {
  // Mouse down on handles
  [leftHandle, rightHandle].forEach((handle) => {
    handle.addEventListener("mousedown", (e) => {
      e.preventDefault();
      e.stopPropagation();
      state.isResizing = true;
      state.resizeHandle = handle.dataset.side;
      state.startX = e.clientX;
      state.initialLeft = parseFloat(selection.style.left);
      state.initialWidth = parseFloat(selection.style.width);
    });
  });

  // Mouse down on selection (for dragging)
  selection.addEventListener("mousedown", (e) => {
    if (e.target === selection) {
      e.preventDefault();
      state.isDragging = true;
      state.startX = e.clientX;
      state.initialLeft = parseFloat(selection.style.left);
    }
  });

  // Mouse down on container (for creating new selection)
  container.addEventListener("mousedown", (e) => {
    if (e.target === container || e.target.parentNode === container) {
      const rect = container.getBoundingClientRect();
      const x = e.clientX - rect.left;
      const percent = (x / rect.width) * 100;

      // Convert click position to date
      const totalMs = state.maxDate.getTime() - state.minDate.getTime();
      const clickMs = state.minDate.getTime() + (percent / 100) * totalMs;
      const clickDate = new Date(clickMs);

      // Snap to nearest commit point
      const snappedDate = snapToNearestCommit(state.commitPoints, clickDate);
      const snappedPercent = getPercentFromDate(
        snappedDate,
        state.minDate,
        state.maxDate
      );

      // Set new selection at snapped point with minimum width
      const minWidthPercent =
        (DATE_FILTER_CONFIG.MIN_SELECTION_WIDTH / rect.width) * 100;
      selection.style.left =
        Math.max(0, snappedPercent - minWidthPercent / 2) + "%";
      selection.style.width = minWidthPercent + "%";

      updateDateDisplayFromSelection(
        selection,
        state.minDate,
        state.maxDate,
        dateDisplay
      );
      triggerDateChange(state);
    }
  });

  // Mouse move
  document.addEventListener("mousemove", (e) => {
    if (state.isResizing) {
      handleResize(e, container, selection, state, dateDisplay);
    } else if (state.isDragging) {
      handleDrag(e, container, selection, state, dateDisplay);
    }
  });

  // Mouse up
  document.addEventListener("mouseup", () => {
    state.isDragging = false;
    state.isResizing = false;
    state.resizeHandle = null;
  });
}

/**
 * Handles resize interactions
 * @param {MouseEvent} e - Mouse event
 * @param {HTMLElement} container - Container element
 * @param {HTMLElement} selection - Selection element
 * @param {Object} state - Scrubber state
 * @param {HTMLElement} dateDisplay - Date display element
 */
function handleResize(e, container, selection, state, dateDisplay) {
  const rect = container.getBoundingClientRect();
  const deltaX = e.clientX - state.startX;
  const deltaPercent = (deltaX / rect.width) * 100;

  if (state.resizeHandle === "left") {
    const newLeft = Math.max(0, state.initialLeft + deltaPercent);
    const newWidth = state.initialWidth - deltaPercent;
    const minWidthPercent =
      (DATE_FILTER_CONFIG.MIN_SELECTION_WIDTH / rect.width) * 100;

    if (newWidth >= minWidthPercent) {
      // Snap to nearest commit point
      const { startDate } = getCurrentDateRangeFromPercent(
        newLeft,
        newWidth,
        state.minDate,
        state.maxDate
      );
      const snappedStartDate = snapToNearestCommit(
        state.commitPoints,
        startDate
      );
      const snappedLeft = getPercentFromDate(
        snappedStartDate,
        state.minDate,
        state.maxDate
      );

      selection.style.left = snappedLeft + "%";
      selection.style.width =
        state.initialLeft + state.initialWidth - snappedLeft + "%";
    }
  } else if (state.resizeHandle === "right") {
    const newWidth = Math.max(
      (DATE_FILTER_CONFIG.MIN_SELECTION_WIDTH / rect.width) * 100,
      state.initialWidth + deltaPercent
    );
    const maxLeft = 100 - newWidth;

    if (state.initialLeft <= maxLeft) {
      // Snap to nearest commit point
      const { endDate } = getCurrentDateRangeFromPercent(
        state.initialLeft,
        newWidth,
        state.minDate,
        state.maxDate
      );
      const snappedEndDate = snapToNearestCommit(state.commitPoints, endDate);
      const snappedRight = getPercentFromDate(
        snappedEndDate,
        state.minDate,
        state.maxDate
      );

      selection.style.width = snappedRight - state.initialLeft + "%";
    }
  }

  updateDateDisplayFromSelection(
    selection,
    state.minDate,
    state.maxDate,
    dateDisplay
  );
  triggerDateChange(state);
}

/**
 * Handles drag interactions
 * @param {MouseEvent} e - Mouse event
 * @param {HTMLElement} container - Container element
 * @param {HTMLElement} selection - Selection element
 * @param {Object} state - Scrubber state
 * @param {HTMLElement} dateDisplay - Date display element
 */
function handleDrag(e, container, selection, state, dateDisplay) {
  const rect = container.getBoundingClientRect();
  const deltaX = e.clientX - state.startX;
  const deltaPercent = (deltaX / rect.width) * 100;
  const width = parseFloat(selection.style.width);

  const newLeft = Math.max(
    0,
    Math.min(100 - width, state.initialLeft + deltaPercent)
  );

  // Snap to nearest commit point
  const { startDate } = getCurrentDateRangeFromPercent(
    newLeft,
    width,
    state.minDate,
    state.maxDate
  );
  const snappedStartDate = snapToNearestCommit(state.commitPoints, startDate);
  const snappedLeft = getPercentFromDate(
    snappedStartDate,
    state.minDate,
    state.maxDate
  );

  // Ensure we don't go out of bounds
  const finalLeft = Math.max(0, Math.min(100 - width, snappedLeft));
  selection.style.left = finalLeft + "%";

  updateDateDisplayFromSelection(
    selection,
    state.minDate,
    state.maxDate,
    dateDisplay
  );
  triggerDateChange(state);
}

/**
 * Gets date range from position percentages
 * @param {number} leftPercent - Left position percentage
 * @param {number} widthPercent - Width percentage
 * @param {Date} minDate - Minimum date
 * @param {Date} maxDate - Maximum date
 * @returns {Object} Object with startDate and endDate
 */
function getCurrentDateRangeFromPercent(
  leftPercent,
  widthPercent,
  minDate,
  maxDate
) {
  const totalMs = maxDate.getTime() - minDate.getTime();
  const startMs = minDate.getTime() + (leftPercent / 100) * totalMs;
  const endMs =
    minDate.getTime() + ((leftPercent + widthPercent) / 100) * totalMs;

  return {
    startDate: new Date(startMs),
    endDate: new Date(endMs),
  };
}

/**
 * Gets percentage position from a date
 * @param {Date} date - Date to convert
 * @param {Date} minDate - Minimum date
 * @param {Date} maxDate - Maximum date
 * @returns {number} Percentage position
 */
function getPercentFromDate(date, minDate, maxDate) {
  const totalMs = maxDate.getTime() - minDate.getTime();
  const dateMs = date.getTime() - minDate.getTime();
  return (dateMs / totalMs) * 100;
}

/**
 * Updates date display from current selection position
 * @param {HTMLElement} selection - Selection element
 * @param {Date} minDate - Minimum date
 * @param {Date} maxDate - Maximum date
 * @param {HTMLElement} dateDisplay - Date display element
 */
function updateDateDisplayFromSelection(
  selection,
  minDate,
  maxDate,
  dateDisplay
) {
  const { startDate, endDate } = getCurrentDateRange(
    selection,
    minDate,
    maxDate
  );
  updateDateDisplay(dateDisplay, startDate, endDate);
}

/**
 * Gets the current date range from selection position
 * @param {HTMLElement} selection - Selection element
 * @param {Date} minDate - Minimum date
 * @param {Date} maxDate - Maximum date
 * @returns {Object} Object with startDate and endDate
 */
function getCurrentDateRange(selection, minDate, maxDate) {
  const left = parseFloat(selection.style.left);
  const width = parseFloat(selection.style.width);

  const totalMs = maxDate.getTime() - minDate.getTime();
  const startMs = minDate.getTime() + (left / 100) * totalMs;
  const endMs = minDate.getTime() + ((left + width) / 100) * totalMs;

  var ret = {
    startDate: new Date(startMs),
    endDate: new Date(endMs),
  };

  return ret;
}

/**
 * Sets the date range programmatically
 * @param {HTMLElement} selection - Selection element
 * @param {Date} startDate - Start date
 * @param {Date} endDate - End date
 * @param {Date} minDate - Minimum date
 * @param {Date} maxDate - Maximum date
 * @param {HTMLElement} dateDisplay - Date display element
 * @param {Array} commitPoints - Array of commit points (optional)
 */
function setDateRange(
  selection,
  startDate,
  endDate,
  minDate,
  maxDate,
  dateDisplay,
  commitPoints = []
) {
  // Snap dates to nearest commits if commit points are available
  const snappedStartDate =
    commitPoints.length > 0
      ? snapToNearestCommit(commitPoints, startDate)
      : startDate;
  const snappedEndDate =
    commitPoints.length > 0
      ? snapToNearestCommit(commitPoints, endDate)
      : endDate;

  const totalMs = maxDate.getTime() - minDate.getTime();
  const startMs = snappedStartDate.getTime() - minDate.getTime();
  const endMs = snappedEndDate.getTime() - minDate.getTime();

  const startPercent = (startMs / totalMs) * 100;
  const endPercent = (endMs / totalMs) * 100;

  selection.style.left = startPercent + "%";
  selection.style.width = endPercent - startPercent + "%";

  updateDateDisplay(dateDisplay, snappedStartDate, snappedEndDate);
}

/**
 * Triggers the date change callback
 * @param {Object} state - Scrubber state
 */
function triggerDateChange(state) {
  if (state.onDateChange) {
    // Debounce the callback to avoid too frequent updates
    clearTimeout(state.changeTimeout);
    state.changeTimeout = setTimeout(() => {
      state.onDateChange();
    }, 100);
  }
}

/**
 * Creates the reset button
 * @returns {HTMLElement} Reset button element
 */
function createResetButton() {
  const button = document.createElement("button");
  button.textContent = "Reset to Full Range";
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
 * Gets filtered data based on date range
 * @param {Date} startDate - Start date
 * @param {Date} endDate - End date
 * @returns {Object} Filtered data object
 */
function getFilteredData(startDate, endDate) {
  const originalData = window.dateFilterControls.originalData;
  const filteredEntries = {};

  Object.keys(originalData.entries).forEach((key) => {
    filteredEntries[key] = filterEntriesByDateRange(
      originalData.entries[key],
      startDate,
      endDate
    );
  });

  return filteredEntries;
}

/**
 * Applies the date filter and re-renders charts
 */
export function applyDateFilter() {
  const controls = window.dateFilterControls;
  const { startDate, endDate } = controls.scrubber.getDateRange();

  // Get filtered data
  const filteredData = getFilteredData(startDate, endDate);

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
