/**
 * Parses and formats human-readable, space-separated duration strings such as "1h 30m".
 * Mirrors the unit table and conventions of the back-end `WorkLogDurationParser`
 * (WorkLogManager.Application.Services.WorkLogDurationParser): unit is fixed and
 * case-sensitive (s/m/h/d/S/M/A), month = 30 days, year = 365 days (fixed approximations,
 * not calendar-accurate).
 */

const SECONDS_PER_SECOND = 1
const SECONDS_PER_MINUTE = 60
const SECONDS_PER_HOUR = 60 * SECONDS_PER_MINUTE
const SECONDS_PER_DAY = 24 * SECONDS_PER_HOUR
const SECONDS_PER_WEEK = 7 * SECONDS_PER_DAY
const SECONDS_PER_MONTH = 30 * SECONDS_PER_DAY
const SECONDS_PER_YEAR = 365 * SECONDS_PER_DAY

const UNIT_SECONDS_MAP: Record<string, number> = {
  s: SECONDS_PER_SECOND,
  m: SECONDS_PER_MINUTE,
  h: SECONDS_PER_HOUR,
  d: SECONDS_PER_DAY,
  S: SECONDS_PER_WEEK,
  M: SECONDS_PER_MONTH,
  A: SECONDS_PER_YEAR,
}

export class DurationParseError extends Error {}

export function parseDuration(input: string): number {
  if (!input || input.trim().length === 0) {
    throw new DurationParseError("Duration text is required.")
  }

  const tokens = input.split(" ").filter((token) => token.length > 0)
  if (tokens.length === 0) {
    throw new DurationParseError("Duration text is required.")
  }

  let totalSeconds = 0
  for (const token of tokens) {
    totalSeconds += parseToken(token)
  }

  return totalSeconds
}

function parseToken(token: string): number {
  const unit = token[token.length - 1]
  const numberPart = token.slice(0, -1)

  if (numberPart.length === 0 || !/^\d+$/.test(numberPart)) {
    throw new DurationParseError(`Invalid duration token: '${token}'.`)
  }

  const unitSeconds = UNIT_SECONDS_MAP[unit]
  if (unitSeconds === undefined) {
    throw new DurationParseError(`Unknown duration unit: '${unit}'.`)
  }

  return Number(numberPart) * unitSeconds
}

export function formatDuration(totalSeconds: number): string {
  if (totalSeconds === 0) {
    return "0s"
  }

  let remaining = totalSeconds
  const days = Math.floor(remaining / SECONDS_PER_DAY)
  remaining %= SECONDS_PER_DAY
  const hours = Math.floor(remaining / SECONDS_PER_HOUR)
  remaining %= SECONDS_PER_HOUR
  const minutes = Math.floor(remaining / SECONDS_PER_MINUTE)
  remaining %= SECONDS_PER_MINUTE
  const seconds = remaining

  const parts: string[] = []
  if (days !== 0) parts.push(`${days}d`)
  if (hours !== 0) parts.push(`${hours}h`)
  if (minutes !== 0) parts.push(`${minutes}m`)
  if (seconds !== 0) parts.push(`${seconds}s`)

  return parts.join(" ")
}
