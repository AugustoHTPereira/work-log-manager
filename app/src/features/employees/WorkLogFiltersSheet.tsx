import { useEffect, useState } from "react"
import { ListFilter } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet"
import type { WorkLogOrigin, WorkLogType } from "@/lib/api/types"
import { getDefaultWorkLogFilters, type WorkLogFilters } from "./workLogFilters"

const TYPE_OPTIONS: { value: WorkLogType; label: string }[] = [
  { value: "RegularAttendance", label: "Presença" },
  { value: "Break", label: "Intervalo" },
  { value: "Overtime", label: "Hora extra" },
  { value: "Absence", label: "Falta" },
]

const ORIGIN_OPTIONS: { value: WorkLogOrigin; label: string }[] = [
  { value: "Automatic", label: "Automático" },
  { value: "Manual", label: "Manual" },
]

const ALL_VALUE = "all"

interface WorkLogFiltersSheetProps {
  filters: WorkLogFilters
  onApply: (filters: WorkLogFilters) => void
}

export function WorkLogFiltersSheet({ filters, onApply }: WorkLogFiltersSheetProps) {
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<WorkLogFilters>(filters)

  useEffect(() => {
    if (open) {
      setDraft(filters)
    }
  }, [open, filters]);

  const handleApply = () => {
    onApply(draft)
    setOpen(false)
  }

  const handleClear = () => {
    const defaults = getDefaultWorkLogFilters()
    setDraft(defaults)
    onApply(defaults)
    setOpen(false)
  }

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button variant="outline" size="icon" data-testid="work-log-filters-trigger">
          <ListFilter className="size-4" />
        </Button>
      </SheetTrigger>
      <SheetContent>
        <SheetHeader>
          <SheetTitle>Filtrar worklogs</SheetTitle>
          <SheetDescription>
            Filtre os worklogs por período, tipo e origem.
          </SheetDescription>
        </SheetHeader>

        <div className="space-y-4 px-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="filter-start-date">Data inicial</Label>
              <Input
                id="filter-start-date"
                type="date"
                value={draft.startDate}
                onChange={(event) =>
                  setDraft((current) => ({ ...current, startDate: event.target.value }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="filter-end-date">Data final</Label>
              <Input
                id="filter-end-date"
                type="date"
                value={draft.endDate}
                onChange={(event) =>
                  setDraft((current) => ({ ...current, endDate: event.target.value }))
                }
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label>Tipo</Label>
            <Select
              value={draft.type ?? ALL_VALUE}
              onValueChange={(value) =>
                setDraft((current) => ({
                  ...current,
                  type: value === ALL_VALUE ? null : (value as WorkLogType),
                }))
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL_VALUE}>Todos</SelectItem>
                {TYPE_OPTIONS.map(({ value, label }) => (
                  <SelectItem key={value} value={value}>
                    {label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Origem</Label>
            <Select
              value={draft.origin ?? ALL_VALUE}
              onValueChange={(value) =>
                setDraft((current) => ({
                  ...current,
                  origin: value === ALL_VALUE ? null : (value as WorkLogOrigin),
                }))
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL_VALUE}>Todas</SelectItem>
                {ORIGIN_OPTIONS.map(({ value, label }) => (
                  <SelectItem key={value} value={value}>
                    {label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        <SheetFooter>
          <Button onClick={handleApply}>Aplicar</Button>
          <SheetClose asChild>
            <Button variant="outline" onClick={handleClear}>
              Limpar filtros
            </Button>
          </SheetClose>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  )
}
