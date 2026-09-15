import { describe, expect, it } from "vitest"
import { DurationParseError, formatDuration, parseDuration } from "./worklog-duration"

describe("parseDuration", () => {
  it("combines hours and minutes into total seconds", () => {
    expect(parseDuration("1h 30m")).toBe(5400)
  })

  it("parses months as 30 days", () => {
    expect(parseDuration("2M")).toBe(2 * 30 * 86400)
  })

  it("parses years as 365 days", () => {
    expect(parseDuration("1A")).toBe(365 * 86400)
  })

  it("parses weeks as 7 days", () => {
    expect(parseDuration("1S")).toBe(7 * 86400)
  })

  it("throws for an invalid token", () => {
    expect(() => parseDuration("abc")).toThrow(DurationParseError)
  })

  it("throws for an unknown unit", () => {
    expect(() => parseDuration("1x")).toThrow(DurationParseError)
  })

  it("throws for the wrong case of a valid unit", () => {
    expect(() => parseDuration("1H")).toThrow(DurationParseError)
  })

  it("throws for empty input", () => {
    expect(() => parseDuration("")).toThrow(DurationParseError)
  })
})

describe("formatDuration", () => {
  it("formats hours and minutes", () => {
    expect(formatDuration(5400)).toBe("1h 30m")
  })

  it("formats all non-zero components from days to seconds", () => {
    expect(formatDuration(90061)).toBe("1d 1h 1m 1s")
  })

  it("formats zero seconds", () => {
    expect(formatDuration(0)).toBe("0s")
  })
})
