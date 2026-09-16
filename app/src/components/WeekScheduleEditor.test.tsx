import { fireEvent, render, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"
import type { WorkSchedulePeriodInput } from "@/lib/api/types"
import { WeekScheduleEditor } from "./WeekScheduleEditor"

describe("WeekScheduleEditor", () => {
  it("renders the 7 days of the week", () => {
    render(<WeekScheduleEditor periods={[]} onChange={vi.fn()} />)

    expect(screen.getByText("Segunda")).toBeInTheDocument()
    expect(screen.getByText("Terça")).toBeInTheDocument()
    expect(screen.getByText("Quarta")).toBeInTheDocument()
    expect(screen.getByText("Quinta")).toBeInTheDocument()
    expect(screen.getByText("Sexta")).toBeInTheDocument()
    expect(screen.getByText("Sábado")).toBeInTheDocument()
    expect(screen.getByText("Domingo")).toBeInTheDocument()
  })

  it("calls onChange with a new period for the correct day when 'Adicionar período' is clicked", () => {
    const onChange = vi.fn()
    render(<WeekScheduleEditor periods={[]} onChange={onChange} />)

    fireEvent.click(screen.getByRole("button", { name: "Adicionar período em Segunda" }))

    expect(onChange).toHaveBeenCalledTimes(1)
    const newPeriods = onChange.mock.calls[0][0] as WorkSchedulePeriodInput[]
    expect(newPeriods).toHaveLength(1)
    expect(newPeriods[0].dayOfWeek).toBe("Monday")
  })

  it("removes the correct period and calls onChange without it", () => {
    const onChange = vi.fn()
    const periods: WorkSchedulePeriodInput[] = [
      { dayOfWeek: "Monday", startTime: "08:00", endTime: "12:00" },
      { dayOfWeek: "Tuesday", startTime: "09:00", endTime: "10:00" },
    ]

    render(<WeekScheduleEditor periods={periods} onChange={onChange} />)

    fireEvent.click(screen.getByRole("button", { name: "Remover período em Segunda" }))

    expect(onChange).toHaveBeenCalledWith([periods[1]])
  })

  it("updates the start time of the correct period without affecting others", () => {
    const onChange = vi.fn()
    const periods: WorkSchedulePeriodInput[] = [
      { dayOfWeek: "Monday", startTime: "08:00", endTime: "12:00" },
    ]

    render(<WeekScheduleEditor periods={periods} onChange={onChange} />)

    const startTimeInput = screen.getByDisplayValue("08:00")
    fireEvent.change(startTimeInput, { target: { value: "09:00" } })

    expect(onChange).toHaveBeenCalledWith([
      { dayOfWeek: "Monday", startTime: "09:00", endTime: "12:00" },
    ])
  })
})
