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

  // Calculate initial selection position using equally-spaced commits
  let startPercent, endPercent;

  if (commitPoints.length > 0 && initialStartCommit && initialEndCommit) {
    // Use commit positions for equally-spaced layout
    const startCommitIndex = findCommitIndex(commitPoints, initialStartCommit);
    const endCommitIndex = findCommitIndex(commitPoints, initialEndCommit);

    startPercent = getCommitPosition(startCommitIndex, commitPoints.length) - 5;
    endPercent = getCommitPosition(endCommitIndex, commitPoints.length) + 5;

    // Ensure proper order (start <= end)
    if (startPercent > endPercent) {
      [startPercent, endPercent] = [endPercent, startPercent];
    }
  } else {
    // Fallback to time-based positioning if no commits
    const totalMs = maxDate.getTime() - minDate.getTime();
    const startMs = startDate.getTime() - minDate.getTime();
    const endMs = endDate.getTime() - minDate.getTime();

    startPercent = (startMs / totalMs) * 100;
    endPercent = (endMs / totalMs) * 100;
  }

  // Create selection area
  const selection = document.createElement("div");
  Object.assign(selection.style, {
    position: "absolute",
    top: "0",
    bottom: "0",
    left: startPercent + "%",
    width: Math.max(endPercent - startPercent, 10) + "%", // Minimum 10% width
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
 * Creates commit point indicators on the timeline with equal spacing
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

  if (commitPoints.length === 0) {
    return commitPointsContainer;
  }

  // Calculate equal spacing for commits with padding on sides
  const padding = DATE_FILTER_CONFIG.COMMIT_PADDING_PERCENT || 10;
  const usableAreaStart = padding; // Padding from left
  const usableAreaEnd = 100 - padding; // Padding from right
  const usableWidth = usableAreaEnd - usableAreaStart; // Usable width

  // Handle single commit case
  const spacing =
    commitPoints.length === 1 ? 0 : usableWidth / (commitPoints.length - 1);

  commitPoints.forEach((commit, index) => {
    const positionPercent =
      commitPoints.length === 1
        ? 50 // Center single commit
        : usableAreaStart + spacing * index; // Start from padding with spacing

    // Create commit point dot
    const commitPoint = document.createElement("div");
    Object.assign(commitPoint.style, {
      position: "absolute",
      left: positionPercent + "%",
      top: "25%",
      width: "12px",
      height: "12px",
      backgroundColor: "#3572a5",
      borderRadius: "50%",
      transform: "translate(-50%, -50%)",
      border: "2px solid #fff",
      boxShadow: "0 0 3px rgba(0,0,0,0.3)",
      zIndex: "2",
    });

    // Add tooltip with commit info
    commitPoint.title = `${commit.id.slice(0, 7)} - ${
      commit.message.split("\n")[0]
    }`;

    // Create date label below the commit point
    const dateLabel = document.createElement("div");
    dateLabel.textContent = formatDateShort(commit.date);
    Object.assign(dateLabel.style, {
      position: "absolute",
      left: positionPercent + "%",
      top: "60%",
      transform: "translate(-50%, 0)",
      fontSize: "10px",
      color: "#495057",
      fontWeight: "bold",
      textAlign: "center",
      whiteSpace: "nowrap",
      zIndex: "1",
    });

    // Create a vertical line connecting the dot to the label
    const connectingLine = document.createElement("div");
    Object.assign(connectingLine.style, {
      position: "absolute",
      left: positionPercent + "%",
      top: "35%",
      width: "1px",
      height: "20%",
      backgroundColor: "#6c757d",
      transform: "translateX(-50%)",
      zIndex: "1",
    });

    commitPointsContainer.appendChild(commitPoint);
    commitPointsContainer.appendChild(connectingLine);
    commitPointsContainer.appendChild(dateLabel);
  });

  return commitPointsContainer;
}

/**
 * Finds the nearest commit point to a given position percentage (for equally-spaced commits)
 * @param {Array} commitPoints - Array of commit objects
 * @param {number} positionPercent - Position percentage (0-100)
 * @returns {Object|null} Nearest commit object or null if no commits
 */
function findNearestCommitByPosition(commitPoints, positionPercent) {
  if (!commitPoints || commitPoints.length === 0) {
    return null;
  }

  if (commitPoints.length === 1) {
    return commitPoints[0];
  }

  const padding = DATE_FILTER_CONFIG.COMMIT_PADDING_PERCENT || 10;
  const usableAreaStart = padding;
  const usableAreaEnd = 100 - padding;
  const usableWidth = usableAreaEnd - usableAreaStart;
  const spacing = usableWidth / (commitPoints.length - 1);

  let nearestIndex = 0;
  let minDistance = Math.abs(positionPercent - usableAreaStart);

  for (let i = 0; i < commitPoints.length; i++) {
    const commitPosition = usableAreaStart + spacing * i;
    const distance = Math.abs(positionPercent - commitPosition);
    if (distance < minDistance) {
      minDistance = distance;
      nearestIndex = i;
    }
  }

  return commitPoints[nearestIndex];
}

/**
 * Gets the position percentage for a commit index
 * @param {number} commitIndex - Index of the commit
 * @param {number} totalCommits - Total number of commits
 * @returns {number} Position percentage
 */
function getCommitPosition(commitIndex, totalCommits) {
  if (totalCommits === 1) {
    return 50; // Center single commit
  }

  const padding = DATE_FILTER_CONFIG.COMMIT_PADDING_PERCENT || 10;
  const usableAreaStart = padding;
  const usableAreaEnd = 100 - padding;
  const usableWidth = usableAreaEnd - usableAreaStart;
  const spacing = usableWidth / (totalCommits - 1);
  return usableAreaStart + spacing * commitIndex;
}

/**
 * Finds the commit index for a given commit
 * @param {Array} commitPoints - Array of commit objects
 * @param {Object} targetCommit - Target commit object
 * @returns {number} Index of the commit or -1 if not found
 */
function findCommitIndex(commitPoints, targetCommit) {
  return commitPoints.findIndex((commit) => commit.id === targetCommit.id);
}

/**
 * Snaps a position percentage to the nearest commit position
 * @param {Array} commitPoints - Array of commit objects
 * @param {number} positionPercent - Position percentage to snap
 * @returns {number} Snapped position percentage
 */
function snapPositionToNearestCommit(commitPoints, positionPercent) {
  if (!commitPoints || commitPoints.length === 0) {
    return positionPercent;
  }

  const nearestCommit = findNearestCommitByPosition(
    commitPoints,
    positionPercent
  );
  if (!nearestCommit) {
    return positionPercent;
  }

  const commitIndex = findCommitIndex(commitPoints, nearestCommit);
  return getCommitPosition(commitIndex, commitPoints.length);
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
 * Creates date labels for the timeline (simplified since we show dates under commits)
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
    justifyContent: "center",
    fontSize: "14px",
    color: "#6c757d",
    pointerEvents: "none",
    zIndex: "1",
    fontWeight: "bold",
  });

  const rangeLabel = document.createElement("span");
  rangeLabel.textContent = "Select commit range by clicking and dragging";
  labelsContainer.appendChild(rangeLabel);

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

      // Snap to nearest commit position instead of date
      const snappedPercent = snapPositionToNearestCommit(
        state.commitPoints,
        percent
      );

      // Set new selection at snapped point with minimum width
      const minWidthPercent = Math.max(
        (DATE_FILTER_CONFIG.MIN_SELECTION_WIDTH / rect.width) * 100,
        10 // Minimum 10% width
      );

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
  const minWidthPercent = Math.max(
    (DATE_FILTER_CONFIG.MIN_SELECTION_WIDTH / rect.width) * 100,
    10 // Minimum 10% width
  );

  if (state.resizeHandle === "left") {
    const newLeft = Math.max(0, state.initialLeft + deltaPercent);
    const newWidth = state.initialWidth - deltaPercent;

    if (newWidth >= minWidthPercent) {
      // Snap to nearest commit position
      const snappedLeft = snapPositionToNearestCommit(
        state.commitPoints,
        newLeft
      );
      const maxLeft = Math.max(
        0,
        parseFloat(selection.style.left) +
          parseFloat(selection.style.width) -
          minWidthPercent
      );

      const finalLeft = Math.min(snappedLeft - 5, maxLeft);

      const finalWidth =
        parseFloat(selection.style.left) +
        parseFloat(selection.style.width) -
        finalLeft;

      selection.style.left = finalLeft + "%";
      selection.style.width = finalWidth + "%";
    }
  } else if (state.resizeHandle === "right") {
    const newWidth = Math.max(
      minWidthPercent,
      state.initialWidth + deltaPercent
    );
    const rightEdgePercent = state.initialLeft + newWidth;

    // Snap the right edge to nearest commit
    const snappedRightPercent = snapPositionToNearestCommit(
      state.commitPoints,
      rightEdgePercent
    );
    const finalWidth = Math.max(
      minWidthPercent,
      snappedRightPercent - state.initialLeft + 5
    );

    if (state.initialLeft + finalWidth <= 100) {
      selection.style.width = finalWidth + "%";
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
  const right = left + width;

  // Get commit points from global state
  const commitPoints = window.dateFilterControls?.commitPoints || [];

  if (commitPoints.length === 0) {
    // Fallback to time-based calculation if no commits
    const totalMs = maxDate.getTime() - minDate.getTime();
    const startMs = minDate.getTime() + (left / 100) * totalMs;
    const endMs = minDate.getTime() + (right / 100) * totalMs;

    return {
      startDate: new Date(startMs),
      endDate: new Date(endMs),
    };
  }

  // Find commits at the left and right edges of selection
  const startCommit = findNearestCommitByPosition(commitPoints, left);
  const endCommit = findNearestCommitByPosition(commitPoints, right);

  const startDate = startCommit ? startCommit.date : minDate;
  const endDate = endCommit ? endCommit.date : maxDate;

  return {
    startDate: startDate,
    endDate: endDate,
  };
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
  if (commitPoints.length > 0) {
    // Find nearest commits for the dates
    const startCommit = findNearestCommitPoint(commitPoints, startDate);
    const endCommit = findNearestCommitPoint(commitPoints, endDate);

    if (startCommit && endCommit) {
      // Use commit positions for equally-spaced layout
      const startCommitIndex = findCommitIndex(commitPoints, startCommit);
      const endCommitIndex = findCommitIndex(commitPoints, endCommit);

      const startPercent = getCommitPosition(
        startCommitIndex,
        commitPoints.length
      );
      const endPercent = getCommitPosition(endCommitIndex, commitPoints.length);

      // Ensure proper order (start <= end)
      const finalStartPercent = Math.min(startPercent - 5, endPercent);
      const finalEndPercent = Math.max(startPercent, endPercent + 5);

      selection.style.left = finalStartPercent + "%";
      selection.style.width =
        Math.max(finalEndPercent - finalStartPercent, 10) + "%";

      updateDateDisplay(dateDisplay, startCommit.date, endCommit.date);
      return;
    }
  }

  // Fallback to time-based positioning if no commits
  const totalMs = maxDate.getTime() - minDate.getTime();
  const startMs = startDate.getTime() - minDate.getTime();
  const endMs = endDate.getTime() - minDate.getTime();

  const startPercent = (startMs / totalMs) * 100;
  const endPercent = (endMs / totalMs) * 100;

  selection.style.left = startPercent + "%";
  selection.style.width = endPercent - startPercent + "%";

  updateDateDisplay(dateDisplay, startDate, endDate);
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
