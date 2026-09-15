import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { describe, expect, it, vi } from "vitest"
import type { EmployeeWorkLog } from "@/lib/api/types"
import { WorkLogFormModal } from "./WorkLogFormModal"
import { advancedToSimple, toDatetimeLocalValue } from "./worklog-date-mode"

vi.mock("@/lib/api/workLogs", () => ({
  createWorkLog: vi.fn(),
  updateWorkLog: vi.fn(),
  deleteWorkLog: vi.fn(),
}))

function renderWithProviders(ui: ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>)
}

function buildWorkLog(overrides: Partial<EmployeeWorkLog> = {}): EmployeeWorkLog {
  return {
    id: "work-log-1",
    employeeId: "emp-1",
    type: "Overtime",
    startDate: "2026-01-01T08:00:00.000Z",
    endDate: "2026-01-01T09:30:00.000Z",
    durationSeconds: 5400,
    ...overrides,
  }
}

// The dialog content is rendered into a Radix portal appended to `document.body`, not
// inside the RTL `container` for this render call, so we query the whole document
// instead of the local container.
function getAdvancedDateInputs() {
  const inputs = document.querySelectorAll<HTMLInputElement>('input[type="datetime-local"]')
  return { startInput: inputs[0], endInput: inputs[1] }
}

function getSimpleDateInputs() {
  const dateInput = document.querySelector<HTMLInputElement>('input[type="date"]')
  const timeInputs = document.querySelectorAll<HTMLInputElement>('input[type="time"]')
  return { dateInput, startTimeInput: timeInputs[0], endTimeInput: timeInputs[1] }
}

function getAdvancedModeCheckbox() {
  return screen.getByRole("checkbox", { name: /editar data inicial e final separadamente/i })
}

function getDurationInput() {
  return screen.getByPlaceholderText("Ex.: 1h 30m") as HTMLInputElement
}

/**
 * Covers the most delicate acceptance criteria of the feature: editing the start/end
 * date inputs must recalculate the duration text field, and editing the duration text
 * field must recalculate the end date input, without either direction looping back into
 * the other (the `lastEditedField` guard in `WorkLogFormModal`). Also covers the simple
 * vs. advanced date mode toggle introduced alongside it.
 */
describe("WorkLogFormModal", () => {
  describe("advanced mode (datetime-local inputs)", () => {
    async function openInAdvancedMode() {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)
      fireEvent.click(getAdvancedModeCheckbox())
      return getAdvancedDateInputs()
    }

    it("recalculates the duration text field when the start/end date inputs change", async () => {
      const { startInput, endInput } = await openInAdvancedMode()

      fireEvent.change(startInput, { target: { value: "2026-01-01T08:00" } })
      fireEvent.change(endInput, { target: { value: "2026-01-01T09:30" } })

      expect(getDurationInput().value).toBe("1h 30m")
    })

    it("recalculates the end date input when the duration text field changes (debounced)", async () => {
      const { startInput, endInput } = await openInAdvancedMode()

      fireEvent.change(startInput, { target: { value: "2026-01-01T08:00" } })
      fireEvent.change(endInput, { target: { value: "2026-01-01T08:00" } })

      fireEvent.change(getDurationInput(), { target: { value: "2h" } })

      await waitFor(
        () => {
          expect(endInput.value).toBe("2026-01-01T10:00")
        },
        { timeout: 1000 },
      )
    })

    it("does not change the start date input when the duration text field changes", async () => {
      const { startInput, endInput } = await openInAdvancedMode()

      fireEvent.change(startInput, { target: { value: "2026-01-01T08:00" } })
      fireEvent.change(endInput, { target: { value: "2026-01-01T08:00" } })

      fireEvent.change(getDurationInput(), { target: { value: "30m" } })

      await waitFor(
        () => {
          expect(endInput.value).toBe("2026-01-01T08:30")
        },
        { timeout: 1000 },
      )

      expect(startInput.value).toBe("2026-01-01T08:00")
    })

    it("shows an inline error and keeps the dates unchanged for an invalid duration token", async () => {
      const { startInput, endInput } = await openInAdvancedMode()
      fireEvent.change(startInput, { target: { value: "2026-01-01T08:00" } })
      fireEvent.change(endInput, { target: { value: "2026-01-01T08:00" } })

      fireEvent.change(getDurationInput(), { target: { value: "1H" } })

      expect(await screen.findByText(/Unknown duration unit/i)).toBeInTheDocument()
      expect(endInput.value).toBe("2026-01-01T08:00")
    })
  })

  describe("simple mode (Data/Início/Fim inputs)", () => {
    it("recalculates the duration text field when the date/start time/end time inputs change", () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()

      fireEvent.change(dateInput!, { target: { value: "2026-01-01" } })
      fireEvent.change(startTimeInput!, { target: { value: "08:00" } })
      fireEvent.change(endTimeInput!, { target: { value: "09:30" } })

      expect(getDurationInput().value).toBe("1h 30m")
    })

    it("recalculates the visible simple-mode end time when the duration text field changes (debounced), without needing to toggle to advanced mode", async () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      fireEvent.change(dateInput!, { target: { value: "2026-01-01" } })
      fireEvent.change(startTimeInput!, { target: { value: "08:00" } })
      fireEvent.change(endTimeInput!, { target: { value: "08:00" } })

      fireEvent.change(getDurationInput(), { target: { value: "2h" } })

      await waitFor(
        () => {
          expect(getSimpleDateInputs().endTimeInput!.value).toBe("10:00")
        },
        { timeout: 1000 },
      )

      // Confirm the underlying advanced-mode state (used on submit) is consistent too.
      fireEvent.click(getAdvancedModeCheckbox())
      const { endInput } = getAdvancedDateInputs()
      expect(endInput.value).toBe("2026-01-01T10:00")
    })

    it("does not change the displayed simple-mode end time when the duration text field changes to an invalid token", async () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      fireEvent.change(dateInput!, { target: { value: "2026-01-01" } })
      fireEvent.change(startTimeInput!, { target: { value: "08:00" } })
      fireEvent.change(endTimeInput!, { target: { value: "09:30" } })

      fireEvent.change(getDurationInput(), { target: { value: "1H" } })

      expect(await screen.findByText(/Unknown duration unit/i)).toBeInTheDocument()
      expect(getSimpleDateInputs().endTimeInput!.value).toBe("09:30")
    })

    it("rolling over to the next day via the duration field keeps the displayed Data unchanged and shows only the time in Fim", async () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      fireEvent.change(dateInput!, { target: { value: "2026-01-01" } })
      fireEvent.change(startTimeInput!, { target: { value: "22:00" } })
      fireEvent.change(endTimeInput!, { target: { value: "22:00" } })

      fireEvent.change(getDurationInput(), { target: { value: "4h" } })

      await waitFor(
        () => {
          expect(getSimpleDateInputs().endTimeInput!.value).toBe("02:00")
        },
        { timeout: 1000 },
      )
      expect(getSimpleDateInputs().dateInput!.value).toBe("2026-01-01")

      fireEvent.click(getAdvancedModeCheckbox())
      const { endInput } = getAdvancedDateInputs()
      expect(endInput.value).toBe("2026-01-02T02:00")
    })

    it("toggling the advanced mode checkbox after editing the duration field does not regress the already-corrected end time", async () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      fireEvent.change(dateInput!, { target: { value: "2026-01-01" } })
      fireEvent.change(startTimeInput!, { target: { value: "08:00" } })
      fireEvent.change(endTimeInput!, { target: { value: "08:00" } })

      fireEvent.change(getDurationInput(), { target: { value: "2h" } })

      await waitFor(
        () => {
          expect(getSimpleDateInputs().endTimeInput!.value).toBe("10:00")
        },
        { timeout: 1000 },
      )

      fireEvent.click(getAdvancedModeCheckbox())
      fireEvent.click(getAdvancedModeCheckbox())

      expect(getSimpleDateInputs().endTimeInput!.value).toBe("10:00")
    })
  })

  describe("acceptance criteria #1-#5", () => {
    it("#1 opens in simple mode when creating a new worklog (checkbox unchecked, simple inputs visible)", () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      expect(getAdvancedModeCheckbox()).not.toBeChecked()
      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      expect(dateInput).toBeTruthy()
      expect(startTimeInput).toBeTruthy()
      expect(endTimeInput).toBeTruthy()
      expect(document.querySelectorAll('input[type="datetime-local"]')).toHaveLength(0)
    })

    it("#2 checks the advanced mode checkbox automatically when editing a multi-day worklog", () => {
      // Two full calendar days apart in UTC, so this stays multi-day regardless of the
      // machine's local timezone.
      const workLog = buildWorkLog({
        startDate: "2026-01-01T10:00:00.000Z",
        endDate: "2026-01-03T10:00:00.000Z",
      })
      renderWithProviders(
        <WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" workLog={workLog} />,
      )

      expect(getAdvancedModeCheckbox()).toBeChecked()
      const { startInput, endInput } = getAdvancedDateInputs()
      expect(startInput).toBeTruthy()
      expect(endInput).toBeTruthy()
    })

    it("#2 (complement) leaves the advanced mode checkbox unchecked when editing a same-day worklog", () => {
      const workLog = buildWorkLog({
        startDate: "2026-01-01T08:00:00.000Z",
        endDate: "2026-01-01T09:30:00.000Z",
      })
      renderWithProviders(
        <WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" workLog={workLog} />,
      )

      expect(getAdvancedModeCheckbox()).not.toBeChecked()
      const expectedSimple = advancedToSimple(
        toDatetimeLocalValue(workLog.startDate),
        toDatetimeLocalValue(workLog.endDate),
      )
      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      expect(dateInput!.value).toBe(expectedSimple.date)
      expect(startTimeInput!.value).toBe(expectedSimple.startTime)
      expect(endTimeInput!.value).toBe(expectedSimple.endTime)
    })

    it("#3 applies the day-rollover rule in simple mode: end time earlier than start time rolls to the next day", () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      fireEvent.change(dateInput!, { target: { value: "2026-01-01" } })
      fireEvent.change(startTimeInput!, { target: { value: "22:00" } })
      fireEvent.change(endTimeInput!, { target: { value: "02:00" } })

      fireEvent.click(getAdvancedModeCheckbox())

      const { startInput, endInput } = getAdvancedDateInputs()
      expect(startInput.value).toBe("2026-01-01T22:00")
      expect(endInput.value).toBe("2026-01-02T02:00")
    })

    it("#4 preserves data through a simple -> advanced -> simple round-trip via the UI", () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      fireEvent.change(dateInput!, { target: { value: "2026-01-01" } })
      fireEvent.change(startTimeInput!, { target: { value: "22:00" } })
      fireEvent.change(endTimeInput!, { target: { value: "02:00" } })

      fireEvent.click(getAdvancedModeCheckbox())
      const { startInput, endInput } = getAdvancedDateInputs()
      expect(startInput.value).toBe("2026-01-01T22:00")
      expect(endInput.value).toBe("2026-01-02T02:00")

      fireEvent.click(getAdvancedModeCheckbox())
      const restored = getSimpleDateInputs()
      expect(restored.dateInput!.value).toBe("2026-01-01")
      expect(restored.startTimeInput!.value).toBe("22:00")
      expect(restored.endTimeInput!.value).toBe("02:00")
    })

    it("#5 keeps duration synchronization working through both modes", async () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      // Simple mode: editing Início/Fim recalculates the duration text field.
      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      fireEvent.change(dateInput!, { target: { value: "2026-01-01" } })
      fireEvent.change(startTimeInput!, { target: { value: "08:00" } })
      fireEvent.change(endTimeInput!, { target: { value: "08:00" } })
      expect(getDurationInput().value).toBe("0s")

      fireEvent.change(endTimeInput!, { target: { value: "09:30" } })
      expect(getDurationInput().value).toBe("1h 30m")

      // Simple mode: editing the duration field recalculates the underlying end date,
      // reflected immediately in the simple mode fields (no toggle needed).
      fireEvent.change(getDurationInput(), { target: { value: "2h" } })
      await new Promise((resolve) => setTimeout(resolve, 500))

      expect(getSimpleDateInputs().endTimeInput!.value).toBe("10:00")

      fireEvent.click(getAdvancedModeCheckbox())
      const { startInput, endInput } = getAdvancedDateInputs()
      expect(startInput.value).toBe("2026-01-01T08:00")
      expect(endInput.value).toBe("2026-01-01T10:00")

      // Advanced mode: editing the end date input recalculates the duration text field.
      fireEvent.change(endInput, { target: { value: "2026-01-01T11:00" } })
      expect(getDurationInput().value).toBe("3h")
    })
  })
})
