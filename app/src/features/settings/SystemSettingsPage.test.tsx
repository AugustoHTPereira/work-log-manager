import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import type { SystemParameter, WorkSchedulePeriod } from "@/lib/api/types"
import { SystemSettingsPage } from "./SystemSettingsPage"

vi.mock("@/lib/api/workSchedules", () => ({
  getGeneralWorkSchedule: vi.fn(),
  updateGeneralWorkSchedule: vi.fn(),
}))

vi.mock("@/lib/api/systemParameters", () => ({
  listSystemParameters: vi.fn(),
  updateSystemParameter: vi.fn(),
  addSystemParameterValue: vi.fn(),
  removeSystemParameterValue: vi.fn(),
}))

const toastSuccess = vi.fn()
const toastError = vi.fn()
vi.mock("sonner", () => ({
  toast: {
    success: (...args: unknown[]) => toastSuccess(...args),
    error: (...args: unknown[]) => toastError(...args),
  },
}))

import { getGeneralWorkSchedule } from "@/lib/api/workSchedules"
import {
  addSystemParameterValue,
  listSystemParameters,
  removeSystemParameterValue,
  updateSystemParameter,
} from "@/lib/api/systemParameters"

function renderWithProviders(ui: ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>)
}

function buildSystemParameter(overrides: Partial<SystemParameter> = {}): SystemParameter {
  return {
    id: "param-1",
    param: "AutoWorkLogTypes",
    value: "RegularAttendance",
    valueType: "Array",
    createdAtUtc: "2026-01-01T00:00:00.000Z",
    updatedAtUtc: "2026-01-01T00:00:00.000Z",
    ...overrides,
  }
}

describe("SystemSettingsPage", () => {
  afterEach(() => {
    vi.mocked(getGeneralWorkSchedule).mockReset()
    vi.mocked(listSystemParameters).mockReset()
    vi.mocked(updateSystemParameter).mockReset()
    vi.mocked(addSystemParameterValue).mockReset()
    vi.mocked(removeSystemParameterValue).mockReset()
    toastSuccess.mockClear()
    toastError.mockClear()
  })

  it("renders the auto work log type checkboxes and the allow-manage switch based on the current parameters", async () => {
    vi.mocked(getGeneralWorkSchedule).mockResolvedValue([] as WorkSchedulePeriod[])
    vi.mocked(listSystemParameters).mockResolvedValue([
      buildSystemParameter({ id: "row-regular", param: "AutoWorkLogTypes", value: "RegularAttendance" }),
      buildSystemParameter({ id: "row-break", param: "AutoWorkLogTypes", value: "Break" }),
      buildSystemParameter({ id: "row-allow", param: "AllowManageClosedWorkLogs", value: "true", valueType: "Bool" }),
    ])

    renderWithProviders(<SystemSettingsPage />)

    const regularAttendanceCheckbox = await screen.findByRole("checkbox", { name: "Presença regular" })
    const breakCheckbox = screen.getByRole("checkbox", { name: "Intervalo" })
    const allowManageSwitch = screen.getByRole("switch", { name: "Permitir gerenciar worklogs em meses fechados" })

    expect(regularAttendanceCheckbox).toBeChecked()
    expect(breakCheckbox).toBeChecked()
    expect(allowManageSwitch).toBeChecked()
  })

  it("checking a checkbox adds the corresponding value row", async () => {
    vi.mocked(getGeneralWorkSchedule).mockResolvedValue([] as WorkSchedulePeriod[])
    vi.mocked(listSystemParameters).mockResolvedValue([
      buildSystemParameter({ id: "row-regular", param: "AutoWorkLogTypes", value: "RegularAttendance" }),
    ])
    vi.mocked(addSystemParameterValue).mockResolvedValue(
      buildSystemParameter({ id: "row-break", param: "AutoWorkLogTypes", value: "Break" }),
    )

    renderWithProviders(<SystemSettingsPage />)

    const breakCheckbox = await screen.findByRole("checkbox", { name: "Intervalo" })
    fireEvent.click(breakCheckbox)

    await waitFor(() => {
      expect(addSystemParameterValue).toHaveBeenCalledWith("AutoWorkLogTypes", "Break")
    })
  })

  it("unchecking a checkbox removes the corresponding value row by id", async () => {
    vi.mocked(getGeneralWorkSchedule).mockResolvedValue([] as WorkSchedulePeriod[])
    vi.mocked(listSystemParameters).mockResolvedValue([
      buildSystemParameter({ id: "row-regular", param: "AutoWorkLogTypes", value: "RegularAttendance" }),
      buildSystemParameter({ id: "row-break", param: "AutoWorkLogTypes", value: "Break" }),
    ])
    vi.mocked(removeSystemParameterValue).mockResolvedValue(undefined)

    renderWithProviders(<SystemSettingsPage />)

    const breakCheckbox = await screen.findByRole("checkbox", { name: "Intervalo" })
    fireEvent.click(breakCheckbox)

    await waitFor(() => {
      expect(removeSystemParameterValue).toHaveBeenCalledWith("AutoWorkLogTypes", "row-break")
    })
  })

  it("toggling the switch calls the mutation with 'true'/'false'", async () => {
    vi.mocked(getGeneralWorkSchedule).mockResolvedValue([] as WorkSchedulePeriod[])
    vi.mocked(listSystemParameters).mockResolvedValue([
      buildSystemParameter({
        id: "row-allow",
        param: "AllowManageClosedWorkLogs",
        value: "false",
        valueType: "Bool",
      }),
    ])
    vi.mocked(updateSystemParameter).mockResolvedValue(
      buildSystemParameter({ id: "row-allow", param: "AllowManageClosedWorkLogs", value: "true", valueType: "Bool" }),
    )

    renderWithProviders(<SystemSettingsPage />)

    const allowManageSwitch = await screen.findByRole("switch", { name: "Permitir gerenciar worklogs em meses fechados" })
    fireEvent.click(allowManageSwitch)

    await waitFor(() => {
      expect(updateSystemParameter).toHaveBeenCalledWith("AllowManageClosedWorkLogs", "true")
    })
  })
})
