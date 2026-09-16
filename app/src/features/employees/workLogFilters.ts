import { endOfMonth, format, startOfMonth } from "date-fns";
import type { WorkLogOrigin, WorkLogType } from "@/lib/api/types";
import type { ListWorkLogsFilters } from "@/lib/api/workLogs";

const DATE_FORMAT = "yyyy-MM-dd";

export interface WorkLogFilters {
  startDate: string;
  endDate: string;
  type: WorkLogType | null;
  origin: WorkLogOrigin | null;
}

/**
 * Default filter: the first through the last day of the current calendar month, no type/origin
 * restriction. Used both to seed the query-string when the page is opened without any filter
 * and to reset the filter sheet.
 */
export function getDefaultWorkLogFilters(
  referenceDate: Date = new Date(),
): WorkLogFilters {
  return {
    startDate: format(startOfMonth(referenceDate), DATE_FORMAT),
    endDate: format(endOfMonth(referenceDate), DATE_FORMAT),
    type: null,
    origin: "Manual",
  };
}

const WORK_LOG_TYPES: WorkLogType[] = [
  "Absence",
  "Overtime",
  "RegularAttendance",
  "Break",
];
const WORK_LOG_ORIGINS: WorkLogOrigin[] = ["Automatic", "Manual"];

function readType(value: string | null): WorkLogType | null {
  return value && WORK_LOG_TYPES.includes(value as WorkLogType)
    ? (value as WorkLogType)
    : null;
}

function readOrigin(value: string | null): WorkLogOrigin | null {
  return value && WORK_LOG_ORIGINS.includes(value as WorkLogOrigin)
    ? (value as WorkLogOrigin)
    : null;
}

/**
 * Reads the work log filter state from the URL's query-string, falling back to
 * {@link getDefaultWorkLogFilters} for `startDate`/`endDate` when either is missing so the
 * default filter is always "current month" on a fresh page load.
 */
export function parseWorkLogFiltersFromSearchParams(
  searchParams: URLSearchParams,
): WorkLogFilters {
  const defaults = getDefaultWorkLogFilters();
  return {
    startDate: searchParams.get("startDate") || defaults.startDate,
    endDate: searchParams.get("endDate") || defaults.endDate,
    type: readType(searchParams.get("type")),
    origin: readOrigin(searchParams.get("origin")),
  };
}

/**
 * Writes the given filter state into a query-string, only including `type`/`origin` when set,
 * so the URL stays clean when those filters are not in use.
 */
export function workLogFiltersToSearchParams(
  filters: WorkLogFilters,
): URLSearchParams {
  const params = new URLSearchParams();
  params.set("startDate", filters.startDate);
  params.set("endDate", filters.endDate);
  if (filters.type) params.set("type", filters.type);
  if (filters.origin) params.set("origin", filters.origin);
  return params;
}

/**
 * Converts the page's filter state into the query params accepted by the
 * `GET /employees/{id}/work-logs` endpoint. `startDate`/`endDate` are widened to the full
 * `[00:00:00, 23:59:59.999]` range of their respective days (in the browser's local time zone)
 * so the back-end filter matches the same inclusive-day semantics the front-end used to apply
 * when filtering in memory.
 */
export function workLogFiltersToApiFilters(
  filters: WorkLogFilters,
): ListWorkLogsFilters {
  const rangeStart = new Date(`${filters.startDate}T00:00:00`);
  const rangeEnd = new Date(`${filters.endDate}T23:59:59.999`);

  return {
    startDate: rangeStart.toISOString(),
    endDate: rangeEnd.toISOString(),
    type: filters.type ?? undefined,
    origin: filters.origin ?? undefined,
  };
}
