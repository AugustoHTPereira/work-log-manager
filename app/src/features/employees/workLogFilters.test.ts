import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
  getDefaultWorkLogFilters,
  parseWorkLogFiltersFromSearchParams,
  workLogFiltersToApiFilters,
  workLogFiltersToSearchParams,
} from "./workLogFilters"

describe("getDefaultWorkLogFilters", () => {
  it("returns the first and last day of the reference month with no type/origin restriction", () => {
    const filters = getDefaultWorkLogFilters(new Date(2026, 1, 10))

    expect(filters).toEqual({
      startDate: "2026-02-01",
      endDate: "2026-02-28",
      type: null,
      origin: null,
    })
  })
})

describe("parseWorkLogFiltersFromSearchParams", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] })
    vi.setSystemTime(new Date(2026, 8, 15))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it("falls back to the current month when startDate/endDate are missing", () => {
    const filters = parseWorkLogFiltersFromSearchParams(new URLSearchParams())

    expect(filters).toEqual({
      startDate: "2026-09-01",
      endDate: "2026-09-30",
      type: null,
      origin: null,
    })
  })

  it("reads startDate/endDate/type/origin from the query-string", () => {
    const filters = parseWorkLogFiltersFromSearchParams(
      new URLSearchParams("startDate=2026-08-01&endDate=2026-08-31&type=Overtime&origin=Manual"),
    )

    expect(filters).toEqual({
      startDate: "2026-08-01",
      endDate: "2026-08-31",
      type: "Overtime",
      origin: "Manual",
    })
  })

  it("ignores an invalid type/origin value", () => {
    const filters = parseWorkLogFiltersFromSearchParams(
      new URLSearchParams("type=NotAType&origin=NotAnOrigin"),
    )

    expect(filters.type).toBeNull()
    expect(filters.origin).toBeNull()
  })
})

describe("workLogFiltersToSearchParams", () => {
  it("always includes startDate/endDate and omits type/origin when not set", () => {
    const params = workLogFiltersToSearchParams({
      startDate: "2026-08-01",
      endDate: "2026-08-31",
      type: null,
      origin: null,
    })

    expect(params.toString()).toBe("startDate=2026-08-01&endDate=2026-08-31")
  })

  it("includes type/origin when set", () => {
    const params = workLogFiltersToSearchParams({
      startDate: "2026-08-01",
      endDate: "2026-08-31",
      type: "Overtime",
      origin: "Manual",
    })

    expect(params.get("type")).toBe("Overtime")
    expect(params.get("origin")).toBe("Manual")
  })
})

describe("workLogFiltersToApiFilters", () => {
  it("widens startDate/endDate to the full local day range and converts them to ISO strings", () => {
    const apiFilters = workLogFiltersToApiFilters({
      startDate: "2026-02-01",
      endDate: "2026-02-28",
      type: null,
      origin: null,
    })

    expect(apiFilters.startDate).toBe(new Date("2026-02-01T00:00:00").toISOString())
    expect(apiFilters.endDate).toBe(new Date("2026-02-28T23:59:59.999").toISOString())
    expect(apiFilters.type).toBeUndefined()
    expect(apiFilters.origin).toBeUndefined()
  })

  it("passes type/origin through when set", () => {
    const apiFilters = workLogFiltersToApiFilters({
      startDate: "2026-01-01",
      endDate: "2026-01-31",
      type: "Overtime",
      origin: "Manual",
    })

    expect(apiFilters.type).toBe("Overtime")
    expect(apiFilters.origin).toBe("Manual")
  })
})
