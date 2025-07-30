/**
 * Configuration constants for the benchmark visualization application
 */

export const IMPLEMENTATION_COLORS = {
  KeraImplementation: "#00add8",
  MoonSharpImplementation: "#f1e05a",
  NLuaImplementation: "#000080",
  NeoImplementation: "#dea584",
  SolarSharpImplementation: "#3572a5",
  LuaCSharpImplementation: "#b07219",
};

export const STATISTICAL_SIGNIFICANCE_CONFIG = {
  CONFIDENCE_LEVEL: 2, // 2 standard deviations (95% confidence)
  MIN_ABSOLUTE_CHANGE_MS: 1.5,
  MIN_PERCENTAGE_CHANGE: 3,
};

export const GRID_CONFIG = {
  MIN_COLUMN_WIDTH: "500px",
  GAP: "40px",
  PADDING: "30px",
};

export const DATE_FILTER_CONFIG = {
  SCRUBBER_HEIGHT: 100, // Height of the scrubber in pixels (increased for date labels)
  HANDLE_WIDTH: 24, // Width of the resize handles
  MIN_SELECTION_WIDTH: 5, // Minimum width of selection in pixels
  COMMIT_PADDING_PERCENT: 10, // Padding on each side to ensure commit IDs are visible (10% = 20% total padding)
};
