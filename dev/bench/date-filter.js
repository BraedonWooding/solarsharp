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

  // Create latest commit button
  const latestButton = createLatestCommitButton();
  dateFilterContainer.appendChild(latestButton);

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
  latestButton.addEventListener("click", selectLatestCommit);

  selectLatestCommit();

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
    userSelect: "none", // Prevent text selection during drag
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
    isDragging: false,
    dragStartIndex: -1,
    dragEndIndex: -1,
    startSliderIndex: 0,
    endSliderIndex: commitPoints.length - 1,
    sliderElements: {},
    selectionBar: null,
  };

  // Create commit points and slider along the timeline
  if (commitPoints.length > 0) {
    createCommitSliderOnTimeline(timelineLine, commitPoints, timelineState);
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
  });

  // Update all commit point styles after elements are created
  setTimeout(() => {
    timelineState.commitElements.forEach((element, commitId) => {
      updateCommitPointStyle(
        element,
        timelineState.selectedCommits.has(commitId)
      );
    });
  }, 0);

  updateSelectionDisplay(selectionDisplay, timelineState);

  // Add global mouse event listeners for drag functionality
  setupGlobalDragListeners(timelineState);

  return {
    container,
    timelineState, // Expose timeline state for external access
    getDateRange: () => getSelectedDateRange(timelineState),
    setDateRange: (start, end) =>
      setSelectedDateRange(start, end, timelineState, selectionDisplay),
    onDateChange: (callback) => {
      timelineState.onDateChange = callback;
    },
  };
}

/**
 * Creates commit slider along the timeline with two-headed range selection
 * @param {HTMLElement} timelineLine - The timeline line element
 * @param {Array} commitPoints - Array of commit objects
 * @param {Object} timelineState - Timeline state object
 */
function createCommitSliderOnTimeline(
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

  // Create selection bar that shows the selected range
  const selectionBar = document.createElement("div");
  Object.assign(selectionBar.style, {
    position: "absolute",
    height: "4px",
    backgroundColor: "#3572a5",
    borderRadius: "2px",
    top: "0",
    zIndex: "1",
    transition: "all 0.2s ease",
  });
  timelineLine.appendChild(selectionBar);
  timelineState.selectionBar = selectionBar;

  // Create commit points as markers
  commitPoints.forEach((commit, index) => {
    const positionPercent =
      commitPoints.length === 1 ? 50 : padding + spacing * index;

    // Create commit point marker
    const commitPoint = document.createElement("div");
    Object.assign(commitPoint.style, {
      position: "absolute",
      left: `${positionPercent}%`,
      top: "-6px",
      width: "6px",
      height: "16px",
      backgroundColor: "#6c757d",
      borderRadius: "1px",
      transform: "translateX(-50%)",
      cursor: "pointer",
      zIndex: "2",
      transition: "all 0.2s ease",
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

    // Add hover effects for commit info
    commitPoint.addEventListener("mouseenter", () => {
      commitCard.style.display = "block";
      commitPoint.style.backgroundColor = "#495057";
    });

    commitPoint.addEventListener("mouseleave", () => {
      commitCard.style.display = "none";
      commitPoint.style.backgroundColor = "#6c757d";
    });

    // Click handler for single commit selection
    commitPoint.addEventListener("click", (e) => {
      e.preventDefault();
      e.stopPropagation();

      // Set both sliders to this commit (single selection)
      timelineState.startSliderIndex = index;
      timelineState.endSliderIndex = index;

      updateSliderSelection(timelineState);
      updateSelectionDisplay(timelineState.selectionDisplay, timelineState);
      triggerTimelineChange(timelineState);
    });

    // Store reference to commit element
    timelineState.commitElements.set(commit.id, commitPoint);

    // Add to timeline
    timelineLine.appendChild(commitPoint);
    timelineLine.appendChild(commitCard);
  });

  // Create start slider handle
  const startSlider = createSliderHandle(
    "start",
    commitPoints,
    timelineState,
    padding,
    spacing
  );
  timelineLine.appendChild(startSlider);
  timelineState.sliderElements.start = startSlider;

  // Create end slider handle
  const endSlider = createSliderHandle(
    "end",
    commitPoints,
    timelineState,
    padding,
    spacing
  );
  timelineLine.appendChild(endSlider);
  timelineState.sliderElements.end = endSlider;

  // Initialize slider positions and selection
  updateSliderPositions(timelineState, padding, spacing);
  updateSliderSelection(timelineState);
}

/**
 * Creates a slider handle for range selection
 * @param {string} type - "start" or "end"
 * @param {Array} commitPoints - Array of commit objects
 * @param {Object} timelineState - Timeline state object
 * @param {number} padding - Padding percentage
 * @param {number} spacing - Spacing between commits
 * @returns {HTMLElement} Slider handle element
 */
function createSliderHandle(
  type,
  commitPoints,
  timelineState,
  padding,
  spacing
) {
  const handle = document.createElement("div");
  Object.assign(handle.style, {
    position: "absolute",
    top: "-14px",
    width: "4px",
    height: "24px",
    backgroundColor: type === "start" ? "#28a745" : "#dc3545",
    borderRadius: "2px",
    transform: "translateX(-50%)",
    cursor: "grab",
    border: "2px solid #fff",
    boxShadow: "0 2px 6px rgba(0,0,0,0.3)",
    zIndex: "4",
    transition: "all 0.2s ease",
  });

  // Add small grip lines for better visual indication
  for (let i = 0; i < 3; i++) {
    const gripLine = document.createElement("div");
    Object.assign(gripLine.style, {
      position: "absolute",
      left: "50%",
      top: `${30 + i * 25}%`,
      width: "1px",
      height: "3px",
      backgroundColor: "#fff",
      transform: "translateX(-50%)",
      userSelect: "none",
      pointerEvents: "none",
    });
    handle.appendChild(gripLine);
  }

  let isDragging = false;
  let initialIndex = 0;

  // Mouse down - start dragging
  handle.addEventListener("mousedown", (e) => {
    e.preventDefault();
    e.stopPropagation();
    isDragging = true;
    dragStartX = e.clientX;
    initialIndex =
      type === "start"
        ? timelineState.startSliderIndex
        : timelineState.endSliderIndex;
    handle.style.cursor = "grabbing";

    // Apply scale effect - add to existing transform or create new one
    const currentTransform = handle.style.transform || "";
    if (currentTransform.includes("translateX")) {
      handle.style.transform = currentTransform + " scale(1.1)";
    } else {
      handle.style.transform = "translateX(-50%) scale(1.1)";
    }

    // Bring the dragged handle to front when both handles are on same commit
    if (timelineState.startSliderIndex === timelineState.endSliderIndex) {
      handle.style.zIndex = "5";
      const otherHandle =
        type === "start"
          ? timelineState.sliderElements.end
          : timelineState.sliderElements.start;
      if (otherHandle) {
        otherHandle.style.zIndex = "4";
      }
    }
  });

  // Global mouse move - handle dragging
  document.addEventListener("mousemove", (e) => {
    if (!isDragging) return;

    const timelineRect = handle.parentElement.getBoundingClientRect();
    const relativeX = e.clientX - timelineRect.left;
    const percentX = (relativeX / timelineRect.width) * 100;

    // Find the nearest commit index
    let nearestIndex = 0;
    let minDistance = Infinity;

    commitPoints.forEach((_, index) => {
      const commitPercent =
        commitPoints.length === 1 ? 50 : padding + spacing * index;
      const distance = Math.abs(percentX - commitPercent);
      if (distance < minDistance) {
        minDistance = distance;
        nearestIndex = index;
      }
    });

    // Normal behavior when sliders are not on the same commit
    if (type === "start") {
      timelineState.startSliderIndex = Math.min(
        nearestIndex,
        timelineState.endSliderIndex
      );
    } else {
      timelineState.endSliderIndex = Math.max(
        nearestIndex,
        timelineState.startSliderIndex
      );
    }

    updateSliderPositions(timelineState, padding, spacing);
    updateSliderSelection(timelineState);
    updateSelectionDisplay(timelineState.selectionDisplay, timelineState);
    triggerTimelineChange(timelineState);
  });

  // Global mouse up - stop dragging
  document.addEventListener("mouseup", () => {
    if (isDragging) {
      isDragging = false;
      handle.style.cursor = "grab";

      // Reset scale and let updateSliderPositions handle the correct transform
      // Don't try to preserve transforms here - let the positioning logic handle it
      handle.style.transform = ""; // Clear any existing transform

      // Force a position update to ensure proper spacing
      updateSliderPositions(timelineState, padding, spacing);

      // Reset z-index to default when dragging stops
      handle.style.zIndex = "4";
      const otherHandle =
        type === "start"
          ? timelineState.sliderElements.end
          : timelineState.sliderElements.start;
      if (otherHandle) {
        otherHandle.style.zIndex = "4";
      }
    }
  });

  return handle;
}

/**
 * Updates the positions of slider handles
 * @param {Object} timelineState - Timeline state object
 * @param {number} padding - Padding percentage
 * @param {number} spacing - Spacing between commits
 */
function updateSliderPositions(timelineState, padding, spacing) {
  const { commitPoints, sliderElements, startSliderIndex, endSliderIndex } =
    timelineState;

  if (commitPoints.length === 0) return;

  const startPercent =
    commitPoints.length === 1 ? 50 : padding + spacing * startSliderIndex;
  const endPercent =
    commitPoints.length === 1 ? 50 : padding + spacing * endSliderIndex;

  if (sliderElements.start) {
    sliderElements.start.style.left = `${startPercent}%`;
  }

  if (sliderElements.end) {
    sliderElements.end.style.left = `${endPercent}%`;

    // When both handles are on the same position, offset them and adjust spacing
    if (startSliderIndex === endSliderIndex && sliderElements.start) {
      // Position handles side by side horizontally
      sliderElements.start.style.transform = "translateX(-150%)";
      sliderElements.end.style.transform = "translateX(50%)";

      // Make the handles narrower when overlapping
      sliderElements.end.style.width = "4px";
      sliderElements.start.style.width = "4px";

      // Adjust positioning to account for narrower bars
      sliderElements.start.style.top = "-14px";
      sliderElements.end.style.top = "-14px";
    } else {
      // Reset transforms to normal position - this is crucial for preventing spacing issues
      sliderElements.end.style.transform = "translateX(-50%)";
      sliderElements.start.style.transform = "translateX(-50%)";

      // Reset to normal size when not overlapping
      sliderElements.end.style.width = "6px";
      sliderElements.start.style.width = "6px";

      // Reset positioning
      sliderElements.start.style.top = "-14px";
      sliderElements.end.style.top = "-14px";
    }
  }

  // Update selection bar
  if (timelineState.selectionBar) {
    const leftPercent = Math.min(startPercent, endPercent);
    const rightPercent = Math.max(startPercent, endPercent);
    const width = Math.max(rightPercent - leftPercent, 0.5); // Minimum width for single commit

    timelineState.selectionBar.style.left = `${leftPercent}%`;
    timelineState.selectionBar.style.width = `${width}%`;
  }
}

/**
 * Updates the selected commits based on slider positions
 * @param {Object} timelineState - Timeline state object
 */
function updateSliderSelection(timelineState) {
  const { commitPoints, startSliderIndex, endSliderIndex } = timelineState;

  // Clear current selection
  timelineState.selectedCommits.clear();

  // Select commits in the range
  const minIndex = Math.min(startSliderIndex, endSliderIndex);
  const maxIndex = Math.max(startSliderIndex, endSliderIndex);

  for (let i = minIndex; i <= maxIndex; i++) {
    if (commitPoints[i]) {
      timelineState.selectedCommits.add(commitPoints[i].id);
    }
  }
}

/**
 * Sets up global mouse event listeners for drag functionality
 * @param {Object} timelineState - Timeline state object
 */
function setupGlobalDragListeners(timelineState) {
  // This function is now handled within the slider handle creation
  // Keep for compatibility but functionality moved to createSliderHandle
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
 * Updates the selection display text
 * @param {HTMLElement} display - Display element
 * @param {Object} timelineState - Timeline state object
 */
function updateSelectionDisplay(display, timelineState) {
  // Store reference to display element
  timelineState.selectionDisplay = display;

  const selectedCount = timelineState.selectedCommits.size;
  const totalCount = timelineState.commitPoints.length;
  const { startSliderIndex, endSliderIndex, commitPoints } = timelineState;

  if (selectedCount === 0) {
    display.textContent = "No commits selected";
  } else if (selectedCount === totalCount) {
    display.textContent = `All ${totalCount} commits selected`;
  } else if (startSliderIndex === endSliderIndex) {
    // Single commit selected
    const commit = commitPoints[startSliderIndex];
    if (commit) {
      display.textContent = `Single commit: ${commit.id.slice(
        0,
        7
      )} (${formatDateShort(commit.date)})`;
    }
  } else {
    // Range of commits selected
    const startCommit = commitPoints[startSliderIndex];
    const endCommit = commitPoints[endSliderIndex];
    if (startCommit && endCommit) {
      display.textContent = `Range: ${startCommit.id.slice(
        0,
        7
      )} to ${endCommit.id.slice(0, 7)} (${selectedCount} commits)`;
    }
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
  // Find commit indices that fall within the date range
  let startIndex = 0;
  let endIndex = timelineState.commitPoints.length - 1;

  // Find the first commit >= startDate
  for (let i = 0; i < timelineState.commitPoints.length; i++) {
    if (timelineState.commitPoints[i].date >= startDate) {
      startIndex = i;
      break;
    }
  }

  // Find the last commit <= endDate
  for (let i = timelineState.commitPoints.length - 1; i >= 0; i--) {
    if (timelineState.commitPoints[i].date <= endDate) {
      endIndex = i;
      break;
    }
  }

  // Update slider positions
  timelineState.startSliderIndex = startIndex;
  timelineState.endSliderIndex = endIndex;

  // Calculate spacing for position updates
  const padding = 10;
  const usableWidth = 100 - padding * 2;
  const spacing =
    timelineState.commitPoints.length === 1
      ? 0
      : usableWidth / (timelineState.commitPoints.length - 1);

  // Update visual elements
  updateSliderPositions(timelineState, padding, spacing);
  updateSliderSelection(timelineState);
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
    marginRight: "10px",
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
 * Creates the latest commit button
 * @returns {HTMLElement} Latest commit button element
 */
function createLatestCommitButton() {
  const button = document.createElement("button");
  button.textContent = "Select Latest Commit";
  button.id = "latest-commit-filter";
  Object.assign(button.style, {
    padding: "8px 16px",
    backgroundColor: "#28a745",
    color: "white",
    border: "none",
    borderRadius: "4px",
    cursor: "pointer",
    fontSize: "14px",
    fontWeight: "bold",
    marginTop: "10px",
  });

  button.addEventListener("mouseenter", () => {
    button.style.backgroundColor = "#218838";
  });

  button.addEventListener("mouseleave", () => {
    button.style.backgroundColor = "#28a745";
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
 * Resets the date filter to show all commits
 */
export function resetDateFilter() {
  const controls = window.dateFilterControls;

  // Reset timeline to select all commits
  controls.scrubber.setDateRange(controls.minDate, controls.maxDate);

  // Apply filter with full range
  applyDateFilter();
}

/**
 * Selects only the latest commit
 */
export function selectLatestCommit() {
  const controls = window.dateFilterControls;
  const commitPoints = controls.commitPoints;

  if (commitPoints.length === 0) return;

  // Get the timeline state from the scrubber
  const timelineState = controls.scrubber.timelineState;
  if (!timelineState) return;

  // Set both sliders to the latest commit (single selection)
  const latestIndex = commitPoints.length - 1;
  timelineState.startSliderIndex = latestIndex;
  timelineState.endSliderIndex = latestIndex;

  // Calculate spacing for position updates
  const padding = 10;
  const usableWidth = 100 - padding * 2;
  const spacing =
    commitPoints.length === 1 ? 0 : usableWidth / (commitPoints.length - 1);

  // Update visual elements
  updateSliderPositions(timelineState, padding, spacing);
  updateSliderSelection(timelineState);
  updateSelectionDisplay(timelineState.selectionDisplay, timelineState);

  // Trigger the change callback to update charts
  triggerTimelineChange(timelineState);
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
