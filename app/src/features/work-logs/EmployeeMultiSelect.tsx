import { Check, ChevronsUpDown, X } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"
import { cn } from "cn"
import type { EmployeeSummary } from "@/lib/api/types"

interface EmployeeMultiSelectProps {
  employees: EmployeeSummary[]
  selectedIds: string[]
  onChange: (ids: string[]) => void
  disabled?: boolean
}

export function EmployeeMultiSelect({
  employees,
  selectedIds,
  onChange,
  disabled,
}: EmployeeMultiSelectProps) {
  const selectedEmployees = selectedIds
    .map((id) => employees.find((employee) => employee.id === id))
    .filter((employee): employee is EmployeeSummary => Boolean(employee))

  const toggleEmployee = (id: string) => {
    if (selectedIds.includes(id)) {
      onChange(selectedIds.filter((selectedId) => selectedId !== id))
    } else {
      onChange([...selectedIds, id])
    }
  }

  const removeEmployee = (id: string) => {
    onChange(selectedIds.filter((selectedId) => selectedId !== id))
  }

  const triggerLabel =
    selectedEmployees.length === 0
      ? "Selecione os funcionários"
      : `${selectedEmployees.length} funcionário(s) selecionado(s)`

  return (
    <div className="space-y-2">
      <Popover>
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            role="combobox"
            aria-label="Funcionários"
            disabled={disabled}
            className="w-full justify-between font-normal"
          >
            {triggerLabel}
            <ChevronsUpDown className="ml-2 size-4 shrink-0 opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-(--radix-popover-trigger-width) p-0">
          <Command>
            <CommandInput placeholder="Buscar funcionário..." />
            <CommandList>
              <CommandEmpty>Nenhum funcionário encontrado.</CommandEmpty>
              <CommandGroup>
                {employees.map((employee) => {
                  const isSelected = selectedIds.includes(employee.id)
                  return (
                    <CommandItem
                      key={employee.id}
                      value={employee.name}
                      onSelect={() => toggleEmployee(employee.id)}
                    >
                      <Check className={cn("size-4", isSelected ? "opacity-100" : "opacity-0")} />
                      {employee.name}
                    </CommandItem>
                  )
                })}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>

      {selectedEmployees.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {selectedEmployees.map((employee) => (
            <Badge key={employee.id} variant="secondary" className="gap-1">
              {employee.name}
              <button
                type="button"
                aria-label={`Remover ${employee.name}`}
                onClick={() => removeEmployee(employee.id)}
                disabled={disabled}
                className="ml-1 rounded-full outline-hidden focus-visible:ring-[2px] focus-visible:ring-ring/50"
              >
                <X className="size-3" />
              </button>
            </Badge>
          ))}
        </div>
      )}
    </div>
  )
}
