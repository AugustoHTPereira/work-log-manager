import { render, screen, within } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import { describe, expect, it, vi } from "vitest"
import type { EmployeeSummary } from "@/lib/api/types"
import { EmployeeMultiSelect } from "./EmployeeMultiSelect"

const employees: EmployeeSummary[] = [
  { id: "emp-1", name: "Ana Silva", role: "Developer", hireDate: "2020-01-01" },
  { id: "emp-2", name: "Bruno Costa", role: "Designer", hireDate: "2020-01-01" },
  { id: "emp-3", name: "Carla Souza", role: "Manager", hireDate: "2020-01-01" },
]

describe("EmployeeMultiSelect", () => {
  it("selecting an item in the Command list adds it to selectedIds", async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()

    render(<EmployeeMultiSelect employees={employees} selectedIds={["emp-1"]} onChange={onChange} />)

    await user.click(screen.getByRole("combobox", { name: "Funcionários" }))
    const listbox = await screen.findByRole("listbox")
    await user.click(within(listbox).getByText("Bruno Costa"))

    expect(onChange).toHaveBeenCalledWith(["emp-1", "emp-2"])
  })

  it("selecting an already-selected item in the Command list removes it from selectedIds", async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()

    render(
      <EmployeeMultiSelect employees={employees} selectedIds={["emp-1", "emp-2"]} onChange={onChange} />,
    )

    await user.click(screen.getByRole("combobox", { name: "Funcionários" }))
    const listbox = await screen.findByRole("listbox")
    await user.click(within(listbox).getByText("Bruno Costa"))

    expect(onChange).toHaveBeenCalledWith(["emp-1"])
  })

  it("removes the pre-selected employee via its chip's remove button", async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()

    render(<EmployeeMultiSelect employees={employees} selectedIds={["emp-1"]} onChange={onChange} />)

    await user.click(screen.getByRole("button", { name: /remover ana silva/i }))

    expect(onChange).toHaveBeenCalledWith([])
  })

  it("renders a chip for each currently selected employee", () => {
    render(
      <EmployeeMultiSelect
        employees={employees}
        selectedIds={["emp-1", "emp-3"]}
        onChange={vi.fn()}
      />,
    )

    expect(screen.getByText("Ana Silva")).toBeInTheDocument()
    expect(screen.getByText("Carla Souza")).toBeInTheDocument()
    expect(screen.queryByText("Bruno Costa")).not.toBeInTheDocument()
  })
})
