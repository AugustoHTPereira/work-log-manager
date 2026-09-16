import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { renderHook, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { describe, expect, it, vi } from "vitest"
import type { EmployeeSummary } from "@/lib/api/types"
import { useEmployees } from "./useEmployees"

vi.mock("@/lib/api/employees", () => ({
  getEmployees: vi.fn(),
}))

import { getEmployees } from "@/lib/api/employees"

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })

  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe("useEmployees", () => {
  it("returns the employees returned by the API layer", async () => {
    const employees: EmployeeSummary[] = [
      { id: "1", name: "Jane Doe", role: "Developer", hireDate: "2020-01-01" },
    ]
    vi.mocked(getEmployees).mockResolvedValue(employees)

    const { result } = renderHook(() => useEmployees(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(result.current.data).toEqual(employees)
  })
})
