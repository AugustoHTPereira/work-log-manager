/**
 * Pure conversion helpers between the two ways `WorkLogFormModal` lets a user fill in a
 * worklog's start/end: the "advanced" mode (two `datetime-local` inputs, `startDate`/
 * `endDate`, which remain the single source of truth for the form) and the "simple" mode
 * (one `date` input plus `startTime`/`endTime` inputs, which only represents an interval
 * of at most ~24h).
 *
 * Day-rollover rule (simple -> advanced): given `date`, `startTime` and `endTime` (all
 * "HH:mm"), `start` is always `date + startTime`. If `endTime` is lexicographically
 * smaller than `startTime` (valid because `<input type="time">` always yields a
 * zero-padded 24h "HH:mm" string), `end` rolls over to the day after `date`; otherwise
 * `end` stays on `date`. Only the two time-of-day strings are compared - never the full
 * `start` timestamp - matching the acceptance criteria.
 */

export interface SimpleModeValue {
  date: string // "YYYY-MM-DD"
  startTime: string // "HH:mm"
  endTime: string // "HH:mm"
}

export interface AdvancedModeValue {
  startDate: string // datetime-local, "YYYY-MM-DDTHH:mm"
  endDate: string // datetime-local, "YYYY-MM-DDTHH:mm"
}

/** Converts an ISO 8601 string to a `datetime-local` input value in the browser's local time. */
export function toDatetimeLocalValue(isoValue: string): string {
  const date = new Date(isoValue)
  const offsetMs = date.getTimezoneOffset() * 60 * 1000
  const localDate = new Date(date.getTime() - offsetMs)
  return localDate.toISOString().slice(0, 16)
}

/**
 * Converts a `datetime-local` input value to an ISO 8601 string with an explicit offset
 * (the browser gives local time without offset info; we treat it as the user's local time).
 */
export function toIsoString(datetimeLocalValue: string): string {
  return new Date(datetimeLocalValue).toISOString()
}

function addDays(date: string, days: number): string {
  const [year, month, day] = date.split("-").map(Number)
  const utcDate = new Date(Date.UTC(year, month - 1, day))
  utcDate.setUTCDate(utcDate.getUTCDate() + days)
  return utcDate.toISOString().slice(0, 10)
}

/**
 * Combines Data + Início/Fim into full `datetime-local` values, applying the day-rollover
 * rule described above.
 */
export function simpleToAdvanced(value: SimpleModeValue): AdvancedModeValue {
  const { date, startTime, endTime } = value
  const startDate = `${date}T${startTime}`
  const endDateDay = endTime < startTime ? addDays(date, 1) : date
  const endDate = `${endDateDay}T${endTime}`

  return { startDate, endDate }
}

/**
 * Decomposes a `startDate`/`endDate` (`datetime-local`) pair into the simple mode shape:
 * `date`/`startTime` come from `startDate`, `endTime` comes from `endDate`'s time-of-day.
 * The information about `endDate` possibly falling on the next day is intentionally
 * dropped when entering simple mode, and reconstituted when leaving it via the same
 * day-rollover rule.
 */
export function advancedToSimple(startDate: string, endDate: string): SimpleModeValue {
  const [date, startTime] = startDate.split("T")
  const [, endTime] = endDate.split("T")

  return { date, startTime, endTime }
}

/**
 * True if `startDate` and `endDate` (`datetime-local`) fall on different calendar days.
 * Used to decide the initial state of the "advanced mode" checkbox when editing an
 * existing worklog.
 */
export function isMultiDay(startDate: string, endDate: string): boolean {
  const [startDay] = startDate.split("T")
  const [endDay] = endDate.split("T")
  return startDay !== endDay
}
