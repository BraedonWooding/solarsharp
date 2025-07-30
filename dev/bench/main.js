/**
 * Benchmark Visualization Application
 * Displays performance benchmark data with interactive charts and change indicators
 */

// =============================================================================
// CONSTANTS AND CONFIGURATION
// =============================================================================

const IMPLEMENTATION_COLORS = {
  KeraImplementation: "#00add8",
  MoonSharpImplementation: "#f1e05a",
  NLuaImplementation: "#000080",
  NeoImplementation: "#dea584",
  SolarSharpImplementation: "#3572a5",
  LuaCSharpImplementation: "#b07219",
  _: "#333333",
};

const STATISTICAL_SIGNIFICANCE_CONFIG = {
  CONFIDENCE_LEVEL: 2, // 2 standard deviations (95% confidence)
  MIN_ABSOLUTE_CHANGE_MS: 1.5,
  MIN_PERCENTAGE_CHANGE: 3,
};

const GRID_CONFIG = {
  MIN_COLUMN_WIDTH: "500px",
  GAP: "40px",
  PADDING: "30px",
};

const DATE_FILTER_CONFIG = {
  DEFAULT_RANGE_DAYS: 30, // Default to last 30 days
  SCRUBBER_HEIGHT: 60, // Height of the scrubber in pixels
  HANDLE_WIDTH: 24, // Width of the resize handles
  MIN_SELECTION_WIDTH: 150, // Minimum width of selection in pixels
};

// =============================================================================
// UTILITY FUNCTIONS
// =============================================================================

/**
 * Groups items by a key generated from a keyGetter function
 * @param {Array} list - Array of items to group
 * @param {Function} keyGetter - Function that generates a key from each item
 * @returns {Map} Map with grouped items
 */
function groupBy(list, keyGetter) {
  const map = new Map();
  for (const item of list) {
    const key = keyGetter(item);
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
function convertToMilliseconds(value, unit) {
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
function getCleanImplementationName(implementationName) {
  const match = implementationName.match(/Implementation:\s*([^,)]+)/);
  return match ? match[1] : implementationName;
}

/**
 * Collects benchmark data per test case
 * @param {Array} entries - Array of benchmark entries
 * @returns {Map} Map of test cases with their benchmark results
 */
function collectBenchesPerTestCase(entries) {
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
function filterEntriesByDateRange(entries, startDate, endDate) {
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
function getCommitPointsFromEntries(dataEntries) {
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
function getDateRangeFromEntries(dataEntries) {
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

  console.log(minDate, maxDate);

  return { minDate, maxDate };
}

// =============================================================================
// INITIALIZATION
// =============================================================================

/**
 * Initializes the benchmark visualization application
 * @returns {Array} Array of datasets for rendering
 */
function init() {
  const data = window.BENCHMARK_DATA;

  // Initialize UI elements
  initializeHeader(data);
  initializeFooter(data);

  // Initialize date filter and get initial datasets
  const { filteredData, dateRange } = initializeDateFilter(data);

  // Prepare data points for charts
  return Object.keys(filteredData).map((name) => ({
    name,
    dataSet: collectBenchesPerTestCase(filteredData[name]),
  }));
}

/**
 * Initializes the header section with repository information and machine info
 * @param {Object} data - Benchmark data object
 */
function initializeHeader(data) {
  document.getElementById("last-update").textContent = new Date(
    data.lastUpdate
  ).toString();

  const repoLink = document.getElementById("repository-link");
  repoLink.href = data.repoUrl;
  repoLink.textContent = data.repoUrl;

  // Initialize machine info section if available
  if (data.machineInfo) {
    initializeMachineInfo(data.machineInfo);
  }
}

/**
 * Initializes the footer with download functionality
 * @param {Object} data - Benchmark data object
 */
function initializeFooter(data) {
  document.getElementById("dl-button").onclick = () => {
    const dataUrl = "data:," + JSON.stringify(data, null, 2);
    const a = document.createElement("a");
    a.href = dataUrl;
    a.download = "benchmark_data.json";
    a.click();
  };
}

/**
 * Initializes the machine info section
 * @param {Object} machineInfo - Machine information object
 */
function initializeMachineInfo(machineInfo) {
  // Find or create machine info container
  let machineInfoContainer = document.getElementById("machine-info");

  if (!machineInfoContainer) {
    // Create machine info container if it doesn't exist
    machineInfoContainer = document.createElement("div");
    machineInfoContainer.id = "machine-info";
    machineInfoContainer.className = "machine-info-section";

    // Insert after header or at the beginning of main content
    const main = document.getElementById("main");
    const header =
      document.querySelector("header") || document.querySelector(".header");

    if (header && header.nextSibling) {
      header.parentNode.insertBefore(machineInfoContainer, header.nextSibling);
    } else if (main) {
      main.parentNode.insertBefore(machineInfoContainer, main);
    } else {
      document.body.insertBefore(
        machineInfoContainer,
        document.body.firstChild
      );
    }
  }

  // Style the container
  Object.assign(machineInfoContainer.style, {
    backgroundColor: "#f8f9fa",
    border: "1px solid #dee2e6",
    borderRadius: "8px",
    padding: "20px",
    margin: "20px auto",
    maxWidth: "1200px",
    fontFamily: "Arial, sans-serif",
  });

  // Create title
  const title = document.createElement("h2");
  title.textContent = "Benchmark Environment";
  Object.assign(title.style, {
    margin: "0 0 15px 0",
    fontSize: "1.4em",
    color: "#333",
    borderBottom: "2px solid #3572a5",
    paddingBottom: "8px",
  });
  machineInfoContainer.appendChild(title);

  // Create info grid
  const infoGrid = document.createElement("div");
  Object.assign(infoGrid.style, {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
    gap: "15px",
    fontSize: "14px",
  });

  // Machine info items
  const infoItems = [
    { label: "Operating System", value: machineInfo.os },
    { label: "Processor", value: machineInfo.processor },
    { label: "Physical Cores", value: machineInfo.physicalCores },
    { label: "Logical Cores", value: machineInfo.logicalCores },
    { label: "Architecture", value: machineInfo.architecture },
    { label: "Runtime Version", value: machineInfo.runtimeVersion },
    {
      label: "BenchmarkDotNet Version",
      value: machineInfo.benchmarkDotnetVersion,
    },
  ];

  infoItems.forEach((item) => {
    if (item.value !== undefined && item.value !== null) {
      const infoItem = createMachineInfoItem(item.label, item.value);
      infoGrid.appendChild(infoItem);
    }
  });

  machineInfoContainer.appendChild(infoGrid);
}

/**
 * Creates a machine info item element
 * @param {string} label - The label for the info item
 * @param {string|number} value - The value for the info item
 * @returns {HTMLElement} Info item element
 */
function createMachineInfoItem(label, value) {
  const item = document.createElement("div");
  Object.assign(item.style, {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    padding: "8px 12px",
    backgroundColor: "white",
    border: "1px solid #e0e0e0",
    borderRadius: "4px",
  });

  const labelElement = document.createElement("span");
  labelElement.textContent = label + ":";
  Object.assign(labelElement.style, {
    fontWeight: "bold",
    color: "#555",
  });

  const valueElement = document.createElement("span");
  valueElement.textContent = value;
  Object.assign(valueElement.style, {
    color: "#333",
    textAlign: "right",
    wordBreak: "break-word",
  });

  item.appendChild(labelElement);
  item.appendChild(valueElement);

  return item;
}

/**
 * Initializes the date filter controls
 * @param {Object} data - Benchmark data object
 * @returns {Object} Object with filtered data and date range
 */
function initializeDateFilter(data) {
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
  console.log(startMs, endMs, ret);

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
 * Formats a date for short display
 * @param {Date} date - Date to format
 * @returns {string} Formatted date string
 */
function formatDateShort(date) {
  return date.toLocaleDateString("en-US", { month: "short", day: "numeric" });
}

/**
 * Formats a date for long display
 * @param {Date} date - Date to format
 * @returns {string} Formatted date string
 */
function formatDateLong(date) {
  return date.toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
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

  console.log(originalData);

  Object.keys(originalData.entries).forEach((key) => {
    filteredEntries[key] = filterEntriesByDateRange(
      originalData.entries[key],
      startDate,
      endDate
    );
  });

  console.log(filteredEntries);

  return filteredEntries;
}

/**
 * Applies the date filter and re-renders charts
 */
function applyDateFilter() {
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

  // Re-render with filtered data
  renderAllCharts(newDataSets);
}

/**
 * Resets the date filter to show all data
 */
function resetDateFilter() {
  const controls = window.dateFilterControls;

  // Reset scrubber to full range
  controls.scrubber.setDateRange(controls.minDate, controls.maxDate);

  // Apply filter with full range
  applyDateFilter();
}

/**
 * Clears all existing charts
 */
function clearCharts() {
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

// =============================================================================
// CHART.JS PLUGIN CONFIGURATION
// =============================================================================

/**
 * Finds a label at the given event coordinates
 * @param {Array} labels - Array of label objects with hitboxes
 * @param {Object} evt - Mouse event object
 * @returns {Array} [found, labelInfo] - Whether a label was found and its info
 */
const findLabel = (labels, evt) => {
  let found = false;
  let res = null;

  labels.forEach((l) => {
    l.labels.forEach((label, index) => {
      if (
        evt.x > label.x &&
        evt.x < label.x2 &&
        evt.y > label.y &&
        evt.y < label.y2
      ) {
        res = {
          label: label.label,
          index,
        };
        found = true;
      }
    });
  });

  return [found, res];
};

/**
 * Gets label hitboxes from chart scales
 * @param {Object} scales - Chart.js scales object
 * @returns {Array} Array of label hitbox objects
 */
const getLabelHitboxes = (scales) =>
  Object.values(scales)
    .filter((s) => !!s._labelItems)
    .map((s) => ({
      scaleId: s.id,
      labels: s._labelItems.map((e, i) => ({
        x: e.translation[0] - s._labelSizes.widths[i],
        x2: e.translation[0] + s._labelSizes.widths[i] / 2,
        y: e.translation[1] - s._labelSizes.heights[i] / 2,
        y2: e.translation[1] + s._labelSizes.heights[i] / 2,
        label: e.label,
        index: i,
      })),
    }));

/**
 * Custom Chart.js plugin for enhanced hover interactions
 */
const customHoverPlugin = {
  id: "customHover",
  afterEvent: (chart, event, opts) => {
    const evt = event.event;

    if (evt.type !== "click") {
      return;
    }

    const [found, labelInfo] = findLabel(getLabelHitboxes(chart.scales), evt);

    if (found) {
      console.log(labelInfo);
    }
  },
};

Chart.register(customHoverPlugin);

// =============================================================================
// CHART RENDERING
// =============================================================================

/**
 * Renders all benchmark charts
 * @param {Array} dataSets - Array of datasets to render
 */
function renderAllCharts(dataSets) {
  const main = document.getElementById("main");
  for (const { name, dataSet } of dataSets) {
    renderBenchmarkSet(name, dataSet, main);
  }
}

/**
 * Normalizes benchmark data units to milliseconds
 * @param {Array} dataset - Array of benchmark data
 */
function normalizeBenchmarkUnits(dataset) {
  for (const set of dataset) {
    for (const subset of set) {
      const currentUnit = subset.bench.unit;

      // Store original unit before conversion
      subset.bench.originalUnit = currentUnit;
      subset.bench.value = convertToMilliseconds(
        subset.bench.value,
        currentUnit
      );
      subset.bench.unit = "ms";
    }
  }
}

/**
 * Creates chart configuration options
 * @param {Array} dataset - Benchmark dataset
 * @returns {Object} Chart.js options object
 */
function createChartOptions(dataset) {
  return {
    responsive: true,
    plugins: {
      legend: {
        display: false, // Disable individual chart legends
      },
      tooltip: createTooltipConfig(dataset),
    },
    scales: {
      x: {
        display: true,
        title: {
          display: true,
          text: "Commit",
        },
      },
      y: {
        display: true,
        title: {
          display: true,
          text: "Time (ms)",
        },
        ticks: {
          beginAtZero: true,
        },
      },
    },
    onClick: (mouseEvent, activeElems) => {
      if (activeElems.length === 0) return;

      // Note: This uses undocumented Chart.js behavior
      const index = activeElems[0].index;
      const url = dataset[activeElems[0].datasetIndex][index].commit.url;
      window.open(url, "_blank");
    },
  };
}

/**
 * Creates tooltip configuration for charts
 * @param {Array} dataset - Benchmark dataset
 * @returns {Object} Tooltip configuration object
 */
function createTooltipConfig(dataset) {
  return {
    callbacks: {
      afterTitle: (items) => {
        const { dataIndex, datasetIndex } = items[0];
        const data = dataset[datasetIndex][dataIndex];
        return (
          "\n" +
          data.commit.message.split("\n")[0] +
          "\n\n" +
          data.commit.timestamp +
          " committed by @" +
          data.commit.committer.username +
          "\n"
        );
      },
      label: (item) => {
        let label = item.formattedValue;
        const { range, unit } =
          dataset[item.datasetIndex][item.dataIndex].bench;
        label += " " + unit;

        if (range) {
          const rangeValue = Number(range.replace("±", ""));
          const originalUnit =
            dataset[item.datasetIndex][item.dataIndex].bench.originalUnit ||
            unit;
          const convertedRange = convertToMilliseconds(
            rangeValue,
            originalUnit
          );
          label += " (± " + convertedRange.toFixed(2) + ")";
        }

        return label;
      },
      afterLabel: (item) => {
        const { name } = dataset[item.datasetIndex][item.dataIndex].bench;
        return name ? "\n" + name : "";
      },
    },
  };
}

/**
 * Renders a single benchmark graph
 * @param {HTMLElement} parent - Parent element to append the chart to
 * @param {string} name - Name of the benchmark
 * @param {Array} dataset - Benchmark dataset
 * @returns {Chart} Chart.js chart instance
 */
function renderGraph(parent, name, dataset) {
  const canvas = document.createElement("canvas");
  canvas.className = "benchmark-chart";
  parent.appendChild(canvas);

  normalizeBenchmarkUnits(dataset);

  const data = {
    labels: dataset[0].map((d) => d.commit.id.slice(0, 7)),
    datasets: dataset.map((d) => ({
      label: d[0].bench.simplifiedName,
      data: d.map((d) => ({
        y: d.bench.value,
        x: d.commit.id.slice(0, 7),
      })),
      borderColor: IMPLEMENTATION_COLORS[d[0].bench.simplifiedName],
      backgroundColor: IMPLEMENTATION_COLORS[d[0].bench.simplifiedName] + "60", // Add alpha
    })),
  };

  const chart = new Chart(canvas, {
    type: "line",
    data,
    options: createChartOptions(dataset),
  });

  // Store chart reference for legend control
  if (typeof window.globalCharts === "undefined") {
    window.globalCharts = [];
  }
  window.globalCharts.push(chart);

  return chart;
}

// =============================================================================
// LEGEND AND CONTROL COMPONENTS
// =============================================================================

/**
 * Creates and configures the shared legend container
 * @param {HTMLElement} main - Main container element
 * @returns {HTMLElement} Legend container element
 */
function createSharedLegend(main) {
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
function createSolarSharpToggleButton(charts, legendContainer) {
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
    updateChangeIndicatorsVisibility();
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
function createLegendItem(implementation, charts) {
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
    updateChangeIndicatorsVisibility();
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
// =============================================================================
// PERFORMANCE CHANGE INDICATORS
// =============================================================================

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
function createPerformanceChangeIndicators(benches) {
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
function updateChangeIndicatorsVisibility() {
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

// =============================================================================
// MAIN RENDERING FUNCTION
// =============================================================================

/**
 * Renders a complete benchmark set with legend, controls, and charts
 * @param {string} name - Name of the benchmark set
 * @param {Map} benchSet - Map of benchmark data
 * @param {HTMLElement} main - Main container element
 */
function renderBenchmarkSet(name, benchSet, main) {
  // Create shared legend container
  const legendContainer = createSharedLegend(main);

  // Collect all unique implementations across all benchmark sets
  const allImplementations = new Set();
  const benchmarkRegex =
    /Benchmark\.Benchmarks\.Benchmark\(Implementation: (.*?), Test: (.*?)\)/;

  for (const [benchName, benches] of groupBy(benchSet.entries(), function (k) {
    const match = k[0].match(benchmarkRegex);
    if (match) {
      k[1][0].bench.simplifiedName = match[1];
      allImplementations.add(match[1]);
      return match[2];
    }
    return k[0]; // fallback
  }).entries()) {
    // Implementation collection happens in this loop
  }

  // Store chart references for legend control
  const charts = [];

  // Add "Show Only SolarSharp" button
  const showOnlyButton = createSolarSharpToggleButton(charts, legendContainer);
  legendContainer.appendChild(showOnlyButton);

  // Create legend items for each implementation
  allImplementations.forEach((implementation) => {
    const legendItem = createLegendItem(implementation, charts);
    legendContainer.appendChild(legendItem);
  });

  // Create grid container for benchmark charts
  const gridContainer = createBenchmarkGrid(main);

  // Render individual benchmark charts
  for (const [benchName, benches] of groupBy(benchSet.entries(), function (k) {
    const match = k[0].match(benchmarkRegex);
    if (match) {
      k[1][0].bench.simplifiedName = match[1];
      return match[2];
    }
    return k[0]; // fallback
  }).entries()) {
    const benchmarkContainer = createBenchmarkContainer(
      gridContainer,
      benchName
    );
    const changesContainer = createPerformanceChangeIndicators(benches);
    benchmarkContainer.appendChild(changesContainer);

    // Create graphs container and render chart
    const graphsElement = document.createElement("div");
    graphsElement.className = "benchmark-graphs";
    benchmarkContainer.appendChild(graphsElement);

    const chart = renderGraph(
      graphsElement,
      benchName,
      benches.map((b) => b[1])
    );
    charts.push(chart);
  }

  // Store the update function globally for legend handlers
  window.updateChangeIndicatorsVisibility = updateChangeIndicatorsVisibility;
}

/**
 * Creates the benchmark grid container
 * @param {HTMLElement} main - Main container element
 * @returns {HTMLElement} Grid container element
 */
function createBenchmarkGrid(main) {
  const gridContainer = document.createElement("div");
  gridContainer.className = "benchmark-grid";
  Object.assign(gridContainer.style, {
    display: "grid",
    gridTemplateColumns: `repeat(auto-fit, minmax(${GRID_CONFIG.MIN_COLUMN_WIDTH}, 1fr))`,
    gap: GRID_CONFIG.GAP,
    padding: GRID_CONFIG.PADDING,
  });
  main.appendChild(gridContainer);
  return gridContainer;
}

/**
 * Creates a container for an individual benchmark
 * @param {HTMLElement} gridContainer - Grid container element
 * @param {string} benchName - Name of the benchmark
 * @returns {HTMLElement} Benchmark container element
 */
function createBenchmarkContainer(gridContainer, benchName) {
  const setElement = document.createElement("div");
  setElement.className = "benchmark-set";
  Object.assign(setElement.style, {
    border: "1px solid #ddd",
    borderRadius: "8px",
    padding: "15px",
    backgroundColor: "#fafafa",
  });
  gridContainer.appendChild(setElement);

  const nameElement = document.createElement("h1");
  nameElement.className = "benchmark-title";
  nameElement.textContent = benchName;
  Object.assign(nameElement.style, {
    marginTop: "0",
    fontSize: "1.2em",
    marginBottom: "15px",
  });
  setElement.appendChild(nameElement);

  return setElement;
}

// =============================================================================
// APPLICATION ENTRY POINT
// =============================================================================

// Initialize and start the application
renderAllCharts(init());
