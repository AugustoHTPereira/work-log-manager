import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import type { ReactNode } from "react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/lib/api/client"
import type { EmployeeSummary, EmployeeWorkLog } from "@/lib/api/types"
import { WorkLogFormModal } from "./WorkLogFormModal"
import { advancedToSimple, toDatetimeLocalValue } from "./worklog-date-mode"

vi.mock("@/lib/api/workLogs", () => ({
  createWorkLog: vi.fn(),
  updateWorkLog: vi.fn(),
  deleteWorkLog: vi.fn(),
}))

vi.mock("@/lib/api/employees", () => ({
  getEmployees: vi.fn(),
}))

import { createWorkLog } from "@/lib/api/workLogs"
import { getEmployees } from "@/lib/api/employees"

const employees: EmployeeSummary[] = [
  { id: "emp-1", name: "Ana Silva", role: "Developer", hireDate: "2020-01-01" },
  { id: "emp-2", name: "Bruno Costa", role: "Designer", hireDate: "2020-01-01" },
  { id: "emp-3", name: "Carla Souza", role: "Manager", hireDate: "2020-01-01" },
]

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
    monthClosingId: null,
    note: null,
    origin: "Manual",
    ...overrides,
  }
}

function getNoteInput() {
  return screen.getByPlaceholderText("Observação (opcional)") as HTMLInputElement
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

function getBulkModeCheckbox() {
  return screen.getByRole("checkbox", { name: /aplicar a múltiplos funcionários/i })
}

function getKeepOpenCheckbox() {
  return screen.getByRole("checkbox", { name: /novo/i })
}

/**
 * Covers the most delicate acceptance criteria of the feature: editing the start/end
 * date inputs must recalculate the duration text field, and editing the duration text
 * field must recalculate the end date input, without either direction looping back into
 * the other (the `lastEditedField` guard in `WorkLogFormModal`). Also covers the simple
 * vs. advanced date mode toggle introduced alongside it.
 */
describe("WorkLogFormModal", () => {
  beforeEach(() => {
    vi.mocked(getEmployees).mockResolvedValue(employees)
    vi.mocked(createWorkLog).mockReset()
  })

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

  describe("note field", () => {
    it("prefills the note input when editing an existing worklog with a note", () => {
      const workLog = buildWorkLog({ note: "Justificativa da falta" })
      renderWithProviders(
        <WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" workLog={workLog} />,
      )

      expect(getNoteInput().value).toBe("Justificativa da falta")
    })

    it("shows a validation error and does not submit when the note exceeds 255 characters", async () => {
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      fireEvent.change(getNoteInput(), { target: { value: "a".repeat(256) } })
      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      expect(
        await screen.findByText(/observação não pode ter mais de 255 caracteres/i),
      ).toBeInTheDocument()
      expect(createWorkLog).not.toHaveBeenCalled()
    })

    it("includes the note in the payload for a single worklog creation", async () => {
      vi.mocked(createWorkLog).mockResolvedValue(buildWorkLog())

      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      fireEvent.change(getNoteInput(), { target: { value: "Chegou atrasado" } })
      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      await waitFor(() => expect(createWorkLog).toHaveBeenCalledTimes(1))
      expect(createWorkLog).toHaveBeenCalledWith(
        "emp-1",
        expect.objectContaining({ note: "Chegou atrasado" }),
      )
    })

    it("sends null for the note when left empty", async () => {
      vi.mocked(createWorkLog).mockResolvedValue(buildWorkLog())

      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      await waitFor(() => expect(createWorkLog).toHaveBeenCalledTimes(1))
      expect(createWorkLog).toHaveBeenCalledWith(
        "emp-1",
        expect.objectContaining({ note: null }),
      )
    })

    it("replicates the note across every employee in a bulk creation payload", async () => {
      vi.mocked(createWorkLog).mockResolvedValue(buildWorkLog())
      const user = userEvent.setup()

      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      fireEvent.click(getBulkModeCheckbox())
      await user.click(screen.getByRole("combobox", { name: "Funcionários" }))
      const listbox = await screen.findByRole("listbox")
      await user.click(within(listbox).getByText("Bruno Costa"))

      fireEvent.change(getNoteInput(), { target: { value: "Nota em massa" } })
      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      await waitFor(() => expect(createWorkLog).toHaveBeenCalledTimes(2))
      const payloads = vi.mocked(createWorkLog).mock.calls.map(([, payload]) => payload)
      expect(payloads[0]).toEqual(payloads[1])
      expect(payloads[0]).toEqual(expect.objectContaining({ note: "Nota em massa" }))
    })
  })

  describe("bulk create (multiple employees)", () => {
    it("#1 with the bulk checkbox unchecked, submitting calls createWorkLog once for the current employee", async () => {
      vi.mocked(createWorkLog).mockResolvedValue(buildWorkLog())

      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      await waitFor(() => expect(createWorkLog).toHaveBeenCalledTimes(1))
      expect(createWorkLog).toHaveBeenCalledWith(
        "emp-1",
        expect.objectContaining({ type: "Overtime" }),
      )
    })

    it("#2 with 2 additional employees selected, submitting calls createWorkLog once per selected employee with the same payload", async () => {
      vi.mocked(createWorkLog).mockResolvedValue(buildWorkLog())
      const user = userEvent.setup()

      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      fireEvent.click(getBulkModeCheckbox())

      await user.click(screen.getByRole("combobox", { name: "Funcionários" }))
      const listbox = await screen.findByRole("listbox")
      await user.click(within(listbox).getByText("Bruno Costa"))
      await user.click(within(listbox).getByText("Carla Souza"))

      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      await waitFor(() => expect(createWorkLog).toHaveBeenCalledTimes(3))
      const calledIds = vi.mocked(createWorkLog).mock.calls.map(([id]) => id).sort()
      expect(calledIds).toEqual(["emp-1", "emp-2", "emp-3"])

      const payloads = vi.mocked(createWorkLog).mock.calls.map(([, payload]) => payload)
      expect(payloads[0]).toEqual(payloads[1])
      expect(payloads[0]).toEqual(payloads[2])
    })

    it("#3 with the bulk checkbox checked and no employees selected, submitting shows a validation error and does not call createWorkLog", async () => {
      const user = userEvent.setup()
      renderWithProviders(<WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" />)

      fireEvent.click(getBulkModeCheckbox())

      const badge = await screen.findByRole("button", { name: /remover ana silva/i })
      await user.click(badge)

      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      expect(await screen.findByText(/selecione ao menos um funcionário/i)).toBeInTheDocument()
      expect(createWorkLog).not.toHaveBeenCalled()
    })

    it("#4 keeps the modal open and renders per-employee results when there is a partial failure", async () => {
      vi.mocked(createWorkLog).mockImplementation((id) => {
        if (id === "emp-2") {
          return Promise.reject(new ApiError("Mês fechado.", 409))
        }
        return Promise.resolve(buildWorkLog({ employeeId: id }))
      })
      const user = userEvent.setup()
      const onOpenChange = vi.fn()

      renderWithProviders(<WorkLogFormModal open onOpenChange={onOpenChange} employeeId="emp-1" />)

      fireEvent.click(getBulkModeCheckbox())
      await user.click(screen.getByRole("combobox", { name: "Funcionários" }))
      const listbox = await screen.findByRole("listbox")
      await user.click(within(listbox).getByText("Bruno Costa"))

      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      expect(await screen.findByText(/falhou: mês fechado\./i)).toBeInTheDocument()
      expect(screen.getByText(/ana silva — criado com sucesso/i)).toBeInTheDocument()
      expect(onOpenChange).not.toHaveBeenCalledWith(false)
    })

    it("#5 does not render the 'keep open after save' checkbox when editing an existing worklog", () => {
      const workLog = buildWorkLog()
      renderWithProviders(
        <WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" workLog={workLog} />,
      )

      expect(
        screen.queryByRole("checkbox", { name: /novo/i }),
      ).not.toBeInTheDocument()
    })

    it("#6 does not render the bulk checkbox or the employee selector when editing an existing worklog", () => {
      const workLog = buildWorkLog()
      renderWithProviders(
        <WorkLogFormModal open onOpenChange={() => {}} employeeId="emp-1" workLog={workLog} />,
      )

      expect(
        screen.queryByRole("checkbox", { name: /aplicar a múltiplos funcionários/i }),
      ).not.toBeInTheDocument()
      expect(screen.queryByRole("combobox", { name: "Funcionários" })).not.toBeInTheDocument()
    })
  })

  describe("keep modal open after save", () => {
    it("with the checkbox unchecked, saving a single worklog closes the modal as before", async () => {
      vi.mocked(createWorkLog).mockResolvedValue(buildWorkLog())
      const onOpenChange = vi.fn()

      renderWithProviders(<WorkLogFormModal open onOpenChange={onOpenChange} employeeId="emp-1" />)

      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false))
    })

    it("with the checkbox checked, saving a single worklog keeps the modal open and resets type/date/duration fields", async () => {
      vi.mocked(createWorkLog).mockResolvedValue(buildWorkLog())
      const onOpenChange = vi.fn()

      renderWithProviders(<WorkLogFormModal open onOpenChange={onOpenChange} employeeId="emp-1" />)

      fireEvent.click(getKeepOpenCheckbox())

      const { dateInput, startTimeInput, endTimeInput } = getSimpleDateInputs()
      fireEvent.change(dateInput!, { target: { value: "2026-01-01" } })
      fireEvent.change(startTimeInput!, { target: { value: "08:00" } })
      fireEvent.change(endTimeInput!, { target: { value: "09:30" } })
      expect(getDurationInput().value).toBe("1h 30m")

      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      await waitFor(() => expect(createWorkLog).toHaveBeenCalledTimes(1))
      expect(onOpenChange).not.toHaveBeenCalledWith(false)

      expect(getDurationInput().value).toBe("0s")
      expect(getSimpleDateInputs().dateInput!.value).not.toBe("2026-01-01")

      // The checkbox itself stays checked so consecutive saves keep behaving the same way.
      expect(getKeepOpenCheckbox()).toBeChecked()
    })

    it("with the checkbox checked, saving in bulk mode keeps the modal open and preserves the selected employees", async () => {
      vi.mocked(createWorkLog).mockResolvedValue(buildWorkLog())
      const onOpenChange = vi.fn()
      const user = userEvent.setup()

      renderWithProviders(<WorkLogFormModal open onOpenChange={onOpenChange} employeeId="emp-1" />)

      fireEvent.click(getBulkModeCheckbox())
      await user.click(screen.getByRole("combobox", { name: "Funcionários" }))
      const listbox = await screen.findByRole("listbox")
      await user.click(within(listbox).getByText("Bruno Costa"))

      fireEvent.click(getKeepOpenCheckbox())

      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      await waitFor(() => expect(createWorkLog).toHaveBeenCalledTimes(2))
      expect(onOpenChange).not.toHaveBeenCalledWith(false)

      // Both previously selected employees remain shown in the multi-select.
      expect(screen.getByRole("button", { name: /remover ana silva/i })).toBeInTheDocument()
      expect(screen.getByRole("button", { name: /remover bruno costa/i })).toBeInTheDocument()

      vi.mocked(createWorkLog).mockClear()
      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      await waitFor(() => expect(createWorkLog).toHaveBeenCalledTimes(2))
    })

    it("with the checkbox checked and a partial bulk failure, still keeps the modal open with the per-employee results panel", async () => {
      vi.mocked(createWorkLog).mockImplementation((id) => {
        if (id === "emp-2") {
          return Promise.reject(new ApiError("Mês fechado.", 409))
        }
        return Promise.resolve(buildWorkLog({ employeeId: id }))
      })
      const user = userEvent.setup()
      const onOpenChange = vi.fn()

      renderWithProviders(<WorkLogFormModal open onOpenChange={onOpenChange} employeeId="emp-1" />)

      fireEvent.click(getBulkModeCheckbox())
      await user.click(screen.getByRole("combobox", { name: "Funcionários" }))
      const listbox = await screen.findByRole("listbox")
      await user.click(within(listbox).getByText("Bruno Costa"))

      fireEvent.click(getKeepOpenCheckbox())

      fireEvent.click(screen.getByRole("button", { name: "Adicionar" }))

      expect(await screen.findByText(/falhou: mês fechado\./i)).toBeInTheDocument()
      expect(screen.getByText(/ana silva — criado com sucesso/i)).toBeInTheDocument()
      expect(onOpenChange).not.toHaveBeenCalledWith(false)
    })
  })
})
