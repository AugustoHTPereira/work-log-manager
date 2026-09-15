import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/lib/api/client"
import type { MonthClosingResult } from "@/lib/api/types"
import { MonthCloseModal } from "./MonthCloseModal"

vi.mock("@/lib/api/monthClosings", () => ({
  closeMonth: vi.fn(),
}))

const toastError = vi.fn()
vi.mock("sonner", () => ({
  toast: { error: (...args: unknown[]) => toastError(...args) },
}))

import { closeMonth } from "@/lib/api/monthClosings"

function renderWithProviders(ui: ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>)
}

function getMonthInput() {
  return document.querySelector<HTMLInputElement>('input[type="month"]')!
}

function buildResult(overrides: Partial<MonthClosingResult> = {}): MonthClosingResult {
  return {
    id: "closing-1",
    month: 8,
    year: 2026,
    createdAtUtc: "2026-09-01T00:00:00.000Z",
    summaries: [
      { employeeId: "emp-1", employeeName: "Jane Doe", generatedCount: 21, skippedCount: 0 },
    ],
    ...overrides,
  }
}

describe("MonthCloseModal", () => {
  beforeEach(() => {
    vi.setSystemTime(new Date("2026-09-14T12:00:00.000Z"))
  })

  afterEach(() => {
    vi.useRealTimers()
    toastError.mockClear()
    vi.mocked(closeMonth).mockReset()
  })

  it("defaults the month selector to the previous month and caps it with `max`", () => {
    renderWithProviders(<MonthCloseModal open onOpenChange={() => {}} />)

    const input = getMonthInput()
    expect(input.value).toBe("2026-08")
    expect(input.max).toBe("2026-08")
  })

  it("shows the summary step with generated/skipped labels on success", async () => {
    vi.mocked(closeMonth).mockResolvedValue(buildResult())

    renderWithProviders(<MonthCloseModal open onOpenChange={() => {}} />)

    fireEvent.click(screen.getByRole("button", { name: /prosseguir/i }))

    expect(await screen.findByText("Jane Doe")).toBeInTheDocument()
    expect(screen.getByText("21")).toBeInTheDocument()
    expect(screen.getByText("Dias gerados")).toBeInTheDocument()
    expect(screen.getByText("Dias pulados")).toBeInTheDocument()
    expect(closeMonth).toHaveBeenCalledWith({ month: 8, year: 2026 })
  })

  it("shows an error toast and stays on the selection step when the API call fails", async () => {
    vi.mocked(closeMonth).mockRejectedValue(new ApiError("Month 08/2026 has already been closed.", 400))

    renderWithProviders(<MonthCloseModal open onOpenChange={() => {}} />)

    fireEvent.click(screen.getByRole("button", { name: /prosseguir/i }))

    await waitFor(() => {
      expect(toastError).toHaveBeenCalledWith("Month 08/2026 has already been closed.")
    })

    expect(screen.getByRole("button", { name: /prosseguir/i })).toBeInTheDocument()
  })
})
