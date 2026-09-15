import { useEffect, useRef, useState } from "react"
import { toast } from "sonner"
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import type { EmployeeWorkLog, WorkLogType } from "@/lib/api/types"
import { DurationParseError, formatDuration, parseDuration } from "@/lib/worklog-duration"
import { useCreateWorkLog } from "./hooks/useCreateWorkLog"
import { useUpdateWorkLog } from "./hooks/useUpdateWorkLog"
import {
  advancedToSimple,
  isMultiDay,
  simpleToAdvanced,
  toDatetimeLocalValue,
  toIsoString,
} from "./worklog-date-mode"

interface WorkLogFormModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  employeeId: string
  workLog?: EmployeeWorkLog
}

export function WorkLogFormModal({ open, onOpenChange, employeeId, workLog }: WorkLogFormModalProps) {
  const isEditing = Boolean(workLog)
  const createWorkLog = useCreateWorkLog(employeeId)
  const updateWorkLog = useUpdateWorkLog(employeeId)

  const [type, setType] = useState<WorkLogType>("Overtime")

  // Advanced mode (`datetime-local` pair) is the single source of truth for the payload.
  const [startDate, setStartDate] = useState("")
  const [endDate, setEndDate] = useState("")

  // Simple mode (Data/Início/Fim) is derived/synchronized from the advanced mode fields.
  const [isAdvancedMode, setIsAdvancedMode] = useState(false)
  const [simpleDate, setSimpleDate] = useState("")
  const [simpleStartTime, setSimpleStartTime] = useState("")
  const [simpleEndTime, setSimpleEndTime] = useState("")

  const [durationText, setDurationText] = useState("")
  const [durationError, setDurationError] = useState<string | null>(null)
  const [dateError, setDateError] = useState<string | null>(null)

  const lastEditedField = useRef<"dates" | "duration" | null>(null)
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    if (!open) {
      return
    }

    let nextStartDate: string
    let nextEndDate: string

    if (workLog) {
      setType(workLog.type)
      nextStartDate = toDatetimeLocalValue(workLog.startDate)
      nextEndDate = toDatetimeLocalValue(workLog.endDate)
      setStartDate(nextStartDate)
      setEndDate(nextEndDate)
      setDurationText(formatDuration(workLog.durationSeconds))
    } else {
      const now = toDatetimeLocalValue(new Date().toISOString())
      nextStartDate = now
      nextEndDate = now
      setType("Overtime")
      setStartDate(now)
      setEndDate(now)
      setDurationText("0s")
    }

    const nextIsAdvancedMode = workLog ? isMultiDay(nextStartDate, nextEndDate) : false
    setIsAdvancedMode(nextIsAdvancedMode)

    const simple = advancedToSimple(nextStartDate, nextEndDate)
    setSimpleDate(simple.date)
    setSimpleStartTime(simple.startTime)
    setSimpleEndTime(simple.endTime)

    setDurationError(null)
    setDateError(null)
    lastEditedField.current = null
  }, [open, workLog])

  const applyDates = (nextStart: string, nextEnd: string) => {
    lastEditedField.current = "dates"

    setStartDate(nextStart)
    setEndDate(nextEnd)

    if (nextStart && nextEnd) {
      const durationSeconds = Math.max(
        0,
        Math.round((new Date(nextEnd).getTime() - new Date(nextStart).getTime()) / 1000),
      )
      setDurationText(formatDuration(durationSeconds))
    }

    setDateError(null)
  }

  const handleDateChange = (field: "start" | "end", value: string) => {
    const nextStart = field === "start" ? value : startDate
    const nextEnd = field === "end" ? value : endDate
    applyDates(nextStart, nextEnd)
  }

  const handleSimpleDateChange = (value: string) => {
    setSimpleDate(value)
    const { startDate: nextStart, endDate: nextEnd } = simpleToAdvanced({
      date: value,
      startTime: simpleStartTime,
      endTime: simpleEndTime,
    })
    applyDates(nextStart, nextEnd)
  }

  const handleSimpleTimeChange = (field: "start" | "end", value: string) => {
    const nextStartTime = field === "start" ? value : simpleStartTime
    const nextEndTime = field === "end" ? value : simpleEndTime

    if (field === "start") setSimpleStartTime(value)
    else setSimpleEndTime(value)

    const { startDate: nextStart, endDate: nextEnd } = simpleToAdvanced({
      date: simpleDate,
      startTime: nextStartTime,
      endTime: nextEndTime,
    })
    applyDates(nextStart, nextEnd)
  }

  const handleAdvancedModeChange = (checked: boolean) => {
    setIsAdvancedMode(checked)

    if (!checked) {
      const simple = advancedToSimple(startDate, endDate)
      setSimpleDate(simple.date)
      setSimpleStartTime(simple.startTime)
      setSimpleEndTime(simple.endTime)
    }
  }

  const handleDurationChange = (value: string) => {
    lastEditedField.current = "duration"
    setDurationText(value)

    if (debounceTimer.current) {
      clearTimeout(debounceTimer.current)
    }

    debounceTimer.current = setTimeout(() => {
      try {
        const durationSeconds = parseDuration(value)
        setDurationError(null)

        if (startDate) {
          const newEndIso = new Date(new Date(startDate).getTime() + durationSeconds * 1000).toISOString()
          const newEnd = toDatetimeLocalValue(newEndIso)
          setEndDate(newEnd)
          setDateError(null)

          if (!isAdvancedMode) {
            const simple = advancedToSimple(startDate, newEnd)
            setSimpleDate(simple.date)
            setSimpleEndTime(simple.endTime)
            // simpleStartTime is left untouched: startDate is not changed by this handler.
          }
        }
      } catch (error) {
        if (error instanceof DurationParseError) {
          setDurationError(error.message)
        }
      }
    }, 400)
  }

  const handleSubmit = async () => {
    if (!startDate || !endDate) {
      setDateError("Datas inicial e final são obrigatórias.")
      return
    }

    if (new Date(endDate).getTime() < new Date(startDate).getTime()) {
      setDateError("A data final não pode ser anterior à data inicial.")
      return
    }

    const payload = {
      type,
      startDate: toIsoString(startDate),
      endDate: toIsoString(endDate),
    }

    try {
      if (isEditing && workLog) {
        await updateWorkLog.mutateAsync({ workLogId: workLog.id, payload })
        toast.success("Worklog atualizado com sucesso.")
      } else {
        await createWorkLog.mutateAsync(payload)
        toast.success("Worklog criado com sucesso.")
      }
      onOpenChange(false)
    } catch {
      toast.error("Não foi possível salvar o worklog.")
    }
  }

  const isSubmitting = createWorkLog.isPending || updateWorkLog.isPending

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEditing ? "Editar worklog" : "Novo worklog"}</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label>Tipo</Label>
            <Select value={type} onValueChange={(value) => setType(value as WorkLogType)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Overtime">Hora extra</SelectItem>
                <SelectItem value="Absence">Falta</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="flex items-center gap-2">
            <Checkbox
              id="advanced-mode"
              checked={isAdvancedMode}
              onCheckedChange={(checked) => handleAdvancedModeChange(checked === true)}
            />
            <Label htmlFor="advanced-mode">Editar data inicial e final separadamente</Label>
          </div>

          {isAdvancedMode ? (
            <>
              <div className="space-y-2">
                <Label>Data inicial</Label>
                <Input
                  type="datetime-local"
                  value={startDate}
                  onChange={(event) => handleDateChange("start", event.target.value)}
                />
              </div>

              <div className="space-y-2">
                <Label>Data final</Label>
                <Input
                  type="datetime-local"
                  value={endDate}
                  onChange={(event) => handleDateChange("end", event.target.value)}
                />
                {dateError && <p className="text-sm text-destructive">{dateError}</p>}
              </div>
            </>
          ) : (
            <>
              <div className="space-y-2">
                <Label>Data</Label>
                <Input
                  type="date"
                  value={simpleDate}
                  onChange={(event) => handleSimpleDateChange(event.target.value)}
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Início</Label>
                  <Input
                    type="time"
                    value={simpleStartTime}
                    onChange={(event) => handleSimpleTimeChange("start", event.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Fim</Label>
                  <Input
                    type="time"
                    value={simpleEndTime}
                    onChange={(event) => handleSimpleTimeChange("end", event.target.value)}
                  />
                </div>
              </div>
              {dateError && <p className="text-sm text-destructive">{dateError}</p>}
            </>
          )}

          <div className="space-y-2">
            <Label>Worklog (duração)</Label>
            <Input
              placeholder="Ex.: 1h 30m"
              value={durationText}
              onChange={(event) => handleDurationChange(event.target.value)}
            />
            {durationError && <p className="text-sm text-destructive">{durationError}</p>}
          </div>
        </div>

        <DialogFooter>
          <Button onClick={handleSubmit} disabled={isSubmitting}>
            Salvar
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
