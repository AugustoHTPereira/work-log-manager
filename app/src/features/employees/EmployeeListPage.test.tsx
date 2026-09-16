import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { render, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter } from "react-router-dom"
import { describe, expect, it, vi } from "vitest"
import type { EmployeeSummary } from "@/lib/api/types"
import { EmployeeListPage } from "./EmployeeListPage"

vi.mock("@/lib/api/employees", () => ({
  getEmployees: vi.fn(),
  createEmployee: vi.fn(),
  updateEmployee: vi.fn(),
  deleteEmployee: vi.fn(),
}))

import { getEmployees } from "@/lib/api/employees"

function renderWithProviders(ui: ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>,
  )
}

describe("EmployeeListPage", () => {
  it("renders employee rows and the create button", async () => {
    const employees: EmployeeSummary[] = [
      { id: "1", name: "Jane Doe", role: "Developer", hireDate: "2020-01-01" },
    ]
    vi.mocked(getEmployees).mockResolvedValue(employees)

    renderWithProviders(<EmployeeListPage />)

    expect(await screen.findByText("Jane Doe")).toBeInTheDocument()
    expect(screen.getByRole("button", { name: /novo funcionário/i })).toBeInTheDocument()
  })
})
