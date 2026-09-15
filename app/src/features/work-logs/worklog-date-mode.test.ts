import { describe, expect, it } from "vitest"
import { advancedToSimple, isMultiDay, simpleToAdvanced } from "./worklog-date-mode"

describe("simpleToAdvanced", () => {
  it("keeps the end date on the same day when there is no day rollover", () => {
    const result = simpleToAdvanced({ date: "2026-01-01", startTime: "08:00", endTime: "17:00" })

    expect(result).toEqual({ startDate: "2026-01-01T08:00", endDate: "2026-01-01T17:00" })
  })

  it("rolls the end date over to the next day when the end time is earlier than the start time", () => {
    const result = simpleToAdvanced({ date: "2026-01-01", startTime: "22:00", endTime: "02:00" })

    expect(result).toEqual({ startDate: "2026-01-01T22:00", endDate: "2026-01-02T02:00" })
  })

  it("does not roll over when start and end times are equal", () => {
    const result = simpleToAdvanced({ date: "2026-01-01", startTime: "08:00", endTime: "08:00" })

    expect(result).toEqual({ startDate: "2026-01-01T08:00", endDate: "2026-01-01T08:00" })
  })
})

describe("advancedToSimple", () => {
  it("decomposes a same-day interval without losing information", () => {
    const result = advancedToSimple("2026-01-01T08:00", "2026-01-01T17:00")

    expect(result).toEqual({ date: "2026-01-01", startTime: "08:00", endTime: "17:00" })
  })

  it("extracts the time-of-day correctly when the end date falls on the next day", () => {
    const result = advancedToSimple("2026-01-01T22:00", "2026-01-02T02:00")

    expect(result).toEqual({ date: "2026-01-01", startTime: "22:00", endTime: "02:00" })
  })
})

describe("round-trip conversions", () => {
  it("preserves the three simple fields through simple -> advanced -> simple", () => {
    const original = { date: "2026-01-01", startTime: "22:00", endTime: "02:00" }
    const advanced = simpleToAdvanced(original)
    const roundTripped = advancedToSimple(advanced.startDate, advanced.endDate)

    expect(roundTripped).toEqual(original)
  })

  it("preserves startDate/endDate through advanced -> simple -> advanced for a 1-day-diff interval", () => {
    const original = { startDate: "2026-01-01T22:00", endDate: "2026-01-02T02:00" }
    const simple = advancedToSimple(original.startDate, original.endDate)
    const roundTripped = simpleToAdvanced(simple)

    expect(roundTripped).toEqual(original)
  })
})

describe("isMultiDay", () => {
  it("returns false when start and end fall on the same calendar day", () => {
    expect(isMultiDay("2026-01-01T08:00", "2026-01-01T17:00")).toBe(false)
  })

  it("returns true when start and end fall on different calendar days", () => {
    expect(isMultiDay("2026-01-01T22:00", "2026-01-02T02:00")).toBe(true)
  })
})
