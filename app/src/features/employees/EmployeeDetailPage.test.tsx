import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { render, screen, within } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { TooltipProvider } from "@/components/ui/tooltip"
import type { EmployeeDetail, EmployeeWorkLog } from "@/lib/api/types"
import type { ListWorkLogsFilters } from "@/lib/api/workLogs"
import { EmployeeDetailPage } from "./EmployeeDetailPage"

vi.mock("@/lib/api/employees", () => ({
  getEmployee: vi.fn(),
  getEmployees: vi.fn().mockResolvedValue([]),
  createEmployee: vi.fn(),
  deleteEmployee: vi.fn(),
  updateEmployee: vi.fn(),
}))

vi.mock("@/lib/api/workSchedules", () => ({
  getEmployeeWorkSchedule: vi.fn().mockResolvedValue([]),
  updateEmployeeWorkSchedule: vi.fn(),
}))

vi.mock("@/lib/api/workLogs", () => ({
  listWorkLogs: vi.fn(),
  createWorkLog: vi.fn(),
  updateWorkLog: vi.fn(),
  deleteWorkLog: vi.fn(),
}))

import { getEmployee } from "@/lib/api/employees"
import { listWorkLogs } from "@/lib/api/workLogs"

function buildEmployeeDetail(overrides: Partial<EmployeeDetail> = {}): EmployeeDetail {
  return {
    id: "emp-1",
    name: "Jane Doe",
    role: "Developer",
    hireDate: "2020-01-01",
    ...overrides,
  }
}

/**
 * Mimics the server-side filtering the real `GET /employees/{id}/work-logs` endpoint performs,
 * so tests can assert on the page's behavior driven purely by the `listWorkLogs` mock, the same
 * way the real back-end filters by date range/type/origin.
 */
function filterWorkLogsForTest(workLogs: EmployeeWorkLog[], filters: ListWorkLogsFilters): EmployeeWorkLog[] {
  return workLogs.filter((workLog) => {
    if (filters.startDate && new Date(workLog.startDate) < new Date(filters.startDate)) return false
    if (filters.endDate && new Date(workLog.startDate) > new Date(filters.endDate)) return false
    if (filters.type && workLog.type !== filters.type) return false
    if (filters.origin && workLog.origin !== filters.origin) return false
    return true
  })
}

function mockWorkLogs(workLogs: EmployeeWorkLog[]) {
  vi.mocked(listWorkLogs).mockImplementation(async (_employeeId, filters = {}) =>
    filterWorkLogsForTest(workLogs, filters),
  )
}

function LocationSearchDisplay() {
  const location = useLocation()
  return <span data-testid="location-search">{location.search}</span>
}

function renderPage(initialEntry = "/employees/emp-1") {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <MemoryRouter initialEntries={[initialEntry]}>
          <Routes>
            <Route
              path="/employees/:id"
              element={
                <>
                  <LocationSearchDisplay />
                  <EmployeeDetailPage />
                </>
              }
            />
          </Routes>
        </MemoryRouter>
      </TooltipProvider>
    </QueryClientProvider>,
  )
}

describe("EmployeeDetailPage", () => {
  beforeEach(() => {
    // Work logs in this file use January 2026 dates; pin "today" inside that month so the
    // default "current month" filter does not hide them.
    vi.useFakeTimers({ toFake: ["Date"] })
    vi.setSystemTime(new Date(2026, 0, 15))

    vi.mocked(getEmployee).mockResolvedValue(buildEmployeeDetail())
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it("disables Editar/Excluir and shows a lock indicator for a work log linked to a month closing", async () => {
    mockWorkLogs([
      {
        id: "wl-closed",
        employeeId: "emp-1",
        type: "Overtime",
        startDate: "2026-01-05T08:00:00.000Z",
        endDate: "2026-01-05T09:00:00.000Z",
        durationSeconds: 3600,
        monthClosingId: "closing-1",
        note: null,
        origin: "Manual",
      },
    ])

    renderPage()

    const row = await screen.findByText("05/01/2026")
    const tableRow = row.closest("tr")!

    const user = userEvent.setup()
    await user.click(within(tableRow).getByTestId("work-log-row-menu-trigger"))

    const editItem = await screen.findByText("Editar");
    const deleteItem = screen.getByText("Excluir");

    expect(editItem.closest('[role="menuitem"]')).toHaveAttribute("data-disabled")
    expect(deleteItem.closest('[role="menuitem"]')).toHaveAttribute("data-disabled")
  })

  it("keeps Editar/Excluir enabled for a work log not linked to any month closing", async () => {
    mockWorkLogs([
      {
        id: "wl-open",
        employeeId: "emp-1",
        type: "Overtime",
        startDate: "2026-01-05T08:00:00.000Z",
        endDate: "2026-01-05T09:00:00.000Z",
        durationSeconds: 3600,
        monthClosingId: null,
        note: null,
        origin: "Manual",
      },
    ])

    renderPage()

    const row = await screen.findByText("05/01/2026")
    const tableRow = row.closest("tr")!

    const user = userEvent.setup()
    await user.click(within(tableRow).getByTestId("work-log-row-menu-trigger"))

    const editItem = await screen.findByText("Editar");
    const deleteItem = screen.getByText("Excluir");

    expect(editItem.closest('[role="menuitem"]')).not.toHaveAttribute("data-disabled")
    expect(deleteItem.closest('[role="menuitem"]')).not.toHaveAttribute("data-disabled")
  })

  it("shows the work log note when present and a placeholder when absent", async () => {
    mockWorkLogs([
      {
        id: "wl-with-note",
        employeeId: "emp-1",
        type: "Absence",
        startDate: "2026-01-05T08:00:00.000Z",
        endDate: "2026-01-05T09:00:00.000Z",
        durationSeconds: 3600,
        monthClosingId: null,
        note: "Consulta médica",
        origin: "Manual",
      },
      {
        id: "wl-without-note",
        employeeId: "emp-1",
        type: "Overtime",
        startDate: "2026-01-06T08:00:00.000Z",
        endDate: "2026-01-06T09:00:00.000Z",
        durationSeconds: 3600,
        monthClosingId: null,
        note: null,
        origin: "Automatic",
      },
    ])

    renderPage()

    expect(await screen.findByText("Consulta médica")).toBeInTheDocument()
    expect(screen.getByText("—")).toBeInTheDocument()
  })
})

describe("EmployeeDetailPage work log filters", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] })
    vi.setSystemTime(new Date(2026, 0, 15))

    vi.mocked(getEmployee).mockResolvedValue(buildEmployeeDetail())

    mockWorkLogs([
      {
        id: "wl-in-month",
        employeeId: "emp-1",
        type: "Overtime",
        startDate: "2026-01-10T08:00:00.000-03:00",
        endDate: "2026-01-10T09:00:00.000-03:00",
        durationSeconds: 3600,
        monthClosingId: null,
        note: null,
        origin: "Manual",
      },
      {
        id: "wl-out-of-month",
        employeeId: "emp-1",
        type: "Overtime",
        startDate: "2026-02-10T08:00:00.000-03:00",
        endDate: "2026-02-10T09:00:00.000-03:00",
        durationSeconds: 3600,
        monthClosingId: null,
        note: null,
        origin: "Manual",
      },
      {
        id: "wl-absence-in-month",
        employeeId: "emp-1",
        type: "Absence",
        startDate: "2026-01-20T08:00:00.000-03:00",
        endDate: "2026-01-20T09:00:00.000-03:00",
        durationSeconds: 3600,
        monthClosingId: null,
        note: null,
        origin: "Automatic",
      },
    ])
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it("defaults to the current month and seeds startDate/endDate into the query-string", async () => {
    renderPage()

    expect(await screen.findByText("10/01/2026")).toBeInTheDocument()
    expect(screen.queryByText("10/02/2026")).not.toBeInTheDocument()

    expect(screen.getByTestId("location-search").textContent).toBe(
      "?startDate=2026-01-01&endDate=2026-01-31",
    )
  })

  it("keeps an explicit filter from the query-string across a refresh and filters the table", async () => {
    renderPage("/employees/emp-1?startDate=2026-02-01&endDate=2026-02-28")

    expect(await screen.findByText("10/02/2026")).toBeInTheDocument()
    expect(screen.queryByText("10/01/2026")).not.toBeInTheDocument()
    expect(screen.getByTestId("location-search").textContent).toBe(
      "?startDate=2026-02-01&endDate=2026-02-28",
    )
  })

  it("applies a type filter from the filters sheet and updates the query-string", async () => {
    renderPage()
    await screen.findByText("10/01/2026")

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    await user.click(screen.getByTestId("work-log-filters-trigger"))

    const typeSelects = await screen.findAllByRole("combobox")
    await user.click(typeSelects[0])
    await user.click(await screen.findByRole("option", { name: "Falta" }))

    await user.click(screen.getByRole("button", { name: "Aplicar" }))

    expect(screen.queryByText("10/01/2026")).not.toBeInTheDocument()
    expect(await screen.findByText("20/01/2026")).toBeInTheDocument()
    expect(screen.getByTestId("location-search").textContent).toBe(
      "?startDate=2026-01-01&endDate=2026-01-31&type=Absence",
    )
  })

  it("shows a specific empty-state message when the filter matches no work log", async () => {
    renderPage("/employees/emp-1?startDate=2026-06-01&endDate=2026-06-30")

    expect(
      await screen.findByText("Nenhum worklog encontrado para o filtro selecionado."),
    ).toBeInTheDocument()
  })
})
