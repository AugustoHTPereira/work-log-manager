import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { renderHook, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { describe, expect, it, vi } from "vitest"
import { employeeWorkLogsQueryKey } from "./useEmployeeWorkLogs"
import { ApiError } from "@/lib/api/client"
import type { CreateWorkLogPayload, EmployeeWorkLog } from "@/lib/api/types"
import { useCreateWorkLogsBulk } from "./useCreateWorkLogsBulk"

vi.mock("@/lib/api/workLogs", () => ({
  createWorkLog: vi.fn(),
}))

import { createWorkLog } from "@/lib/api/workLogs"

function buildWorkLog(employeeId: string): EmployeeWorkLog {
  return {
    id: `work-log-${employeeId}`,
    employeeId,
    type: "Overtime",
    startDate: "2026-01-01T08:00:00.000Z",
    endDate: "2026-01-01T09:30:00.000Z",
    durationSeconds: 5400,
    monthClosingId: null,
    note: null,
    origin: "Manual",
  }
}

const payload: CreateWorkLogPayload = {
  type: "Overtime",
  startDate: "2026-01-01T08:00:00.000Z",
  endDate: "2026-01-01T09:30:00.000Z",
  note: null,
}

function createWrapper(queryClient: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe("useCreateWorkLogsBulk", () => {
  it("aggregates succeeded and failed results without rejecting the mutation, even on partial failure", async () => {
    vi.mocked(createWorkLog).mockImplementation((employeeId) => {
      if (employeeId === "emp-3") {
        return Promise.reject(new ApiError("Mês fechado.", 409))
      }
      return Promise.resolve(buildWorkLog(employeeId))
    })

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const { result } = renderHook(() => useCreateWorkLogsBulk(), {
      wrapper: createWrapper(queryClient),
    })

    const mutationResult = await result.current.mutateAsync({
      employeeIds: ["emp-1", "emp-2", "emp-3"],
      payload,
    })

    expect(mutationResult.succeeded).toHaveLength(2)
    expect(mutationResult.succeeded.map((item) => item.employeeId).sort()).toEqual(["emp-1", "emp-2"])
    expect(mutationResult.failed).toEqual([{ employeeId: "emp-3", message: "Mês fechado." }])
    expect(result.current.isError).toBe(false)
  })

  it("invalidates the employee query for each employee that succeeded", async () => {
    vi.mocked(createWorkLog).mockImplementation((employeeId) => {
      if (employeeId === "emp-2") {
        return Promise.reject(new ApiError("Erro.", 400))
      }
      return Promise.resolve(buildWorkLog(employeeId))
    })

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries")

    const { result } = renderHook(() => useCreateWorkLogsBulk(), {
      wrapper: createWrapper(queryClient),
    })

    await result.current.mutateAsync({ employeeIds: ["emp-1", "emp-2"], payload })

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: employeeWorkLogsQueryKey("emp-1") })
    })
    expect(invalidateSpy).not.toHaveBeenCalledWith({ queryKey: employeeWorkLogsQueryKey("emp-2") })
  })
})
