import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useEmployees } from "@/features/employees/hooks/useEmployees";
import { ApiError } from "@/lib/api/client";
import type { EmployeeWorkLog, WorkLogType } from "@/lib/api/types";
import {
  DurationParseError,
  formatDuration,
  parseDuration,
} from "@/lib/worklog-duration";
import { EmployeeMultiSelect } from "./EmployeeMultiSelect";
import { useCreateWorkLog } from "./hooks/useCreateWorkLog";
import {
  useCreateWorkLogsBulk,
  type BulkCreateWorkLogResult,
} from "./hooks/useCreateWorkLogsBulk";
import { useUpdateWorkLog } from "./hooks/useUpdateWorkLog";
import {
  advancedToSimple,
  isMultiDay,
  simpleToAdvanced,
  toDatetimeLocalValue,
  toIsoString,
} from "./worklog-date-mode";

const NOTE_MAX_LENGTH = 255;

interface WorkLogFormModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  employeeId: string;
  workLog?: EmployeeWorkLog;
}

export function WorkLogFormModal({
  open,
  onOpenChange,
  employeeId,
  workLog,
}: WorkLogFormModalProps) {
  const isEditing = Boolean(workLog);
  const createWorkLog = useCreateWorkLog(employeeId);
  const updateWorkLog = useUpdateWorkLog(employeeId);
  const createWorkLogsBulk = useCreateWorkLogsBulk();
  const { data: employees = [] } = useEmployees();

  const [type, setType] = useState<WorkLogType>("Overtime");

  const [isBulkMode, setIsBulkMode] = useState(false);
  const [selectedEmployeeIds, setSelectedEmployeeIds] = useState<string[]>([]);
  const [selectionError, setSelectionError] = useState<string | null>(null);
  const [bulkResult, setBulkResult] = useState<BulkCreateWorkLogResult | null>(
    null,
  );
  const [keepOpenAfterSave, setKeepOpenAfterSave] = useState(false);

  // Advanced mode (`datetime-local` pair) is the single source of truth for the payload.
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");

  // Simple mode (Data/Início/Fim) is derived/synchronized from the advanced mode fields.
  const [isAdvancedMode, setIsAdvancedMode] = useState(false);
  const [simpleDate, setSimpleDate] = useState("");
  const [simpleStartTime, setSimpleStartTime] = useState("");
  const [simpleEndTime, setSimpleEndTime] = useState("");

  const [durationText, setDurationText] = useState("");
  const [durationError, setDurationError] = useState<string | null>(null);
  const [dateError, setDateError] = useState<string | null>(null);

  const [note, setNote] = useState("");
  const [noteError, setNoteError] = useState<string | null>(null);

  const lastEditedField = useRef<"dates" | "duration" | null>(null);
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (!open) {
      return;
    }

    let nextStartDate: string;
    let nextEndDate: string;

    if (workLog) {
      setType(workLog.type);
      nextStartDate = toDatetimeLocalValue(workLog.startDate);
      nextEndDate = toDatetimeLocalValue(workLog.endDate);
      setStartDate(nextStartDate);
      setEndDate(nextEndDate);
      setDurationText(formatDuration(workLog.durationSeconds));
      setNote(workLog.note ?? "");
    } else {
      const now = toDatetimeLocalValue(new Date().toISOString());
      nextStartDate = now;
      nextEndDate = now;
      setType("Overtime");
      setStartDate(now);
      setEndDate(now);
      setDurationText("0s");
      setNote("");
    }

    setIsBulkMode(false);
    setSelectedEmployeeIds(workLog ? [] : [employeeId]);
    setSelectionError(null);
    setBulkResult(null);
    setKeepOpenAfterSave(false);

    const nextIsAdvancedMode = workLog
      ? isMultiDay(nextStartDate, nextEndDate)
      : false;
    setIsAdvancedMode(nextIsAdvancedMode);

    const simple = advancedToSimple(nextStartDate, nextEndDate);
    setSimpleDate(simple.date);
    setSimpleStartTime(simple.startTime);
    setSimpleEndTime(simple.endTime);

    setDurationError(null);
    setDateError(null);
    setNoteError(null);
    lastEditedField.current = null;
  }, [open, workLog, employeeId]);

  const applyDates = (nextStart: string, nextEnd: string) => {
    lastEditedField.current = "dates";

    setStartDate(nextStart);
    setEndDate(nextEnd);

    if (nextStart && nextEnd) {
      const durationSeconds = Math.max(
        0,
        Math.round(
          (new Date(nextEnd).getTime() - new Date(nextStart).getTime()) / 1000,
        ),
      );
      setDurationText(formatDuration(durationSeconds));
    }

    setDateError(null);
  };

  const handleDateChange = (field: "start" | "end", value: string) => {
    const nextStart = field === "start" ? value : startDate;
    const nextEnd = field === "end" ? value : endDate;
    applyDates(nextStart, nextEnd);
  };

  const handleSimpleDateChange = (value: string) => {
    setSimpleDate(value);
    const { startDate: nextStart, endDate: nextEnd } = simpleToAdvanced({
      date: value,
      startTime: simpleStartTime,
      endTime: simpleEndTime,
    });
    applyDates(nextStart, nextEnd);
  };

  const handleSimpleTimeChange = (field: "start" | "end", value: string) => {
    const nextStartTime = field === "start" ? value : simpleStartTime;
    const nextEndTime = field === "end" ? value : simpleEndTime;

    if (field === "start") setSimpleStartTime(value);
    else setSimpleEndTime(value);

    const { startDate: nextStart, endDate: nextEnd } = simpleToAdvanced({
      date: simpleDate,
      startTime: nextStartTime,
      endTime: nextEndTime,
    });
    applyDates(nextStart, nextEnd);
  };

  const handleAdvancedModeChange = (checked: boolean) => {
    setIsAdvancedMode(checked);

    if (!checked) {
      const simple = advancedToSimple(startDate, endDate);
      setSimpleDate(simple.date);
      setSimpleStartTime(simple.startTime);
      setSimpleEndTime(simple.endTime);
    }
  };

  const handleDurationChange = (value: string) => {
    lastEditedField.current = "duration";
    setDurationText(value);

    if (debounceTimer.current) {
      clearTimeout(debounceTimer.current);
    }

    debounceTimer.current = setTimeout(() => {
      try {
        const durationSeconds = parseDuration(value);
        setDurationError(null);

        if (startDate) {
          const newEndIso = new Date(
            new Date(startDate).getTime() + durationSeconds * 1000,
          ).toISOString();
          const newEnd = toDatetimeLocalValue(newEndIso);
          setEndDate(newEnd);
          setDateError(null);

          if (!isAdvancedMode) {
            const simple = advancedToSimple(startDate, newEnd);
            setSimpleDate(simple.date);
            setSimpleEndTime(simple.endTime);
            // simpleStartTime is left untouched: startDate is not changed by this handler.
          }
        }
      } catch (error) {
        if (error instanceof DurationParseError) {
          setDurationError(error.message);
        }
      }
    }, 400);
  };

  const handleBulkModeChange = (checked: boolean) => {
    setIsBulkMode(checked);
    setSelectionError(null);
    setBulkResult(null);

    if (!checked) {
      setSelectedEmployeeIds([employeeId]);
    }
  };

  const handleNoteChange = (value: string) => {
    setNote(value);
    setNoteError(
      value.length > NOTE_MAX_LENGTH
        ? `A observação não pode ter mais de ${NOTE_MAX_LENGTH} caracteres.`
        : null,
    );
  };

  const employeeName = (id: string) =>
    employees.find((employee) => employee.id === id)?.name ?? id;

  // Used after a successful save when "keep open" is checked: resets the type/date/duration
  // fields as if the modal had just been opened for a new entry, but deliberately leaves the
  // employee selection (`isBulkMode`/`selectedEmployeeIds`) untouched so the user can keep
  // logging entries for the same employee(s) without reselecting them.
  const resetFieldsForNextEntry = () => {
    const now = toDatetimeLocalValue(new Date().toISOString());
    setType("Overtime");
    setStartDate(now);
    setEndDate(now);
    setDurationText("0s");
    setIsAdvancedMode(false);
    const simple = advancedToSimple(now, now);
    setSimpleDate(simple.date);
    setSimpleStartTime(simple.startTime);
    setSimpleEndTime(simple.endTime);
    setDurationError(null);
    setDateError(null);
    setNote("");
    setNoteError(null);
    setBulkResult(null);
    lastEditedField.current = null;
  };

  const handleSubmit = async () => {
    if (!startDate || !endDate) {
      setDateError("Datas inicial e final são obrigatórias.");
      return;
    }

    if (new Date(endDate).getTime() < new Date(startDate).getTime()) {
      setDateError("A data final não pode ser anterior à data inicial.");
      return;
    }

    if (isBulkMode && selectedEmployeeIds.length === 0) {
      setSelectionError("Selecione ao menos um funcionário.");
      return;
    }

    if (note.length > NOTE_MAX_LENGTH) {
      setNoteError(
        `A observação não pode ter mais de ${NOTE_MAX_LENGTH} caracteres.`,
      );
      return;
    }

    setSelectionError(null);

    const payload = {
      type,
      startDate: toIsoString(startDate),
      endDate: toIsoString(endDate),
      note: note.trim() === "" ? null : note,
    };

    try {
      if (isEditing && workLog) {
        await updateWorkLog.mutateAsync({ workLogId: workLog.id, payload });
        toast.success("Worklog atualizado com sucesso.");
        onOpenChange(false);
        return;
      }

      if (isBulkMode) {
        const result = await createWorkLogsBulk.mutateAsync({
          employeeIds: selectedEmployeeIds,
          payload,
        });

        if (result.failed.length === 0) {
          toast.success(
            `${result.succeeded.length} worklog(s) criado(s) com sucesso.`,
          );
          if (keepOpenAfterSave) {
            resetFieldsForNextEntry();
          } else {
            onOpenChange(false);
          }
        } else {
          // A partial failure always keeps the modal open with the per-employee results
          // panel, regardless of "keep open after save".
          setBulkResult(result);
          toast.error(
            `${result.succeeded.length} de ${selectedEmployeeIds.length} worklogs criados. ${result.failed.length} falharam.`,
          );
        }
        return;
      }

      await createWorkLog.mutateAsync(payload);
      toast.success("Worklog criado com sucesso.");
      if (keepOpenAfterSave) {
        resetFieldsForNextEntry();
      } else {
        onOpenChange(false);
      }
    } catch (error) {
      const message =
        error instanceof ApiError
          ? error.message
          : "Não foi possível salvar o worklog.";
      toast.error(message);
    }
  };

  const isSubmitting =
    createWorkLog.isPending ||
    updateWorkLog.isPending ||
    createWorkLogsBulk.isPending;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {isEditing ? "Editar worklog" : "Novo worklog"}
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label>Tipo</Label>
            <Select
              value={type}
              onValueChange={(value) => setType(value as WorkLogType)}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Overtime">Hora extra</SelectItem>
                <SelectItem value="Absence">Falta</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {!isEditing && (
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <Checkbox
                  id="bulk-mode"
                  checked={isBulkMode}
                  onCheckedChange={(checked) =>
                    handleBulkModeChange(checked === true)
                  }
                />
                <Label htmlFor="bulk-mode">
                  Aplicar a múltiplos funcionários
                </Label>
              </div>

              {isBulkMode && (
                <>
                  <EmployeeMultiSelect
                    employees={employees}
                    selectedIds={selectedEmployeeIds}
                    onChange={setSelectedEmployeeIds}
                  />
                  {selectionError && (
                    <p className="text-sm text-destructive">{selectionError}</p>
                  )}
                </>
              )}
            </div>
          )}

          <div className="flex items-center gap-2">
            <Checkbox
              id="advanced-mode"
              checked={isAdvancedMode}
              onCheckedChange={(checked) =>
                handleAdvancedModeChange(checked === true)
              }
            />
            <Label htmlFor="advanced-mode">
              Editar data inicial e final separadamente
            </Label>
          </div>

          {isAdvancedMode ? (
            <>
              <div className="space-y-2">
                <Label>Data inicial</Label>
                <Input
                  type="datetime-local"
                  value={startDate}
                  onChange={(event) =>
                    handleDateChange("start", event.target.value)
                  }
                />
              </div>

              <div className="space-y-2">
                <Label>Data final</Label>
                <Input
                  type="datetime-local"
                  value={endDate}
                  onChange={(event) =>
                    handleDateChange("end", event.target.value)
                  }
                />
                {dateError && (
                  <p className="text-sm text-destructive">{dateError}</p>
                )}
              </div>
            </>
          ) : (
            <>
              <div className="space-y-2">
                <Label>Data</Label>
                <Input
                  type="date"
                  value={simpleDate}
                  onChange={(event) =>
                    handleSimpleDateChange(event.target.value)
                  }
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Início</Label>
                  <Input
                    type="time"
                    value={simpleStartTime}
                    onChange={(event) =>
                      handleSimpleTimeChange("start", event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>Fim</Label>
                  <Input
                    type="time"
                    value={simpleEndTime}
                    onChange={(event) =>
                      handleSimpleTimeChange("end", event.target.value)
                    }
                  />
                </div>
              </div>
              {dateError && (
                <p className="text-sm text-destructive">{dateError}</p>
              )}
            </>
          )}

          <div className="space-y-2">
            <Label>Worklog (duração)</Label>
            <Input
              placeholder="Ex.: 1h 30m"
              value={durationText}
              onChange={(event) => handleDurationChange(event.target.value)}
            />
            {durationError && (
              <p className="text-sm text-destructive">{durationError}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label>Observação</Label>
            <Input
              placeholder="Observação (opcional)"
              value={note}
              onChange={(event) => handleNoteChange(event.target.value)}
            />
            {noteError && (
              <p className="text-sm text-destructive">{noteError}</p>
            )}
          </div>

          {bulkResult && bulkResult.failed.length > 0 && (
            <div className="space-y-2 rounded-md border p-3">
              <p className="text-sm font-medium">Resultado por funcionário</p>
              <ul className="space-y-1 text-sm">
                {bulkResult.succeeded.map(({ employeeId: id }) => (
                  <li key={id} className="text-muted-foreground">
                    {employeeName(id)} — criado com sucesso
                  </li>
                ))}
                {bulkResult.failed.map(({ employeeId: id, message }) => (
                  <li key={id} className="text-destructive">
                    {employeeName(id)} — falhou: {message}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>

        <DialogFooter className={isEditing ? undefined : "sm:justify-between"}>
          {!isEditing && (
            <div className="flex items-center gap-2">
              <Checkbox
                id="keep-open-after-save"
                checked={keepOpenAfterSave}
                onCheckedChange={(checked) =>
                  setKeepOpenAfterSave(checked === true)
                }
              />
              <Label htmlFor="keep-open-after-save">Novo</Label>
            </div>
          )}
          <Button onClick={handleSubmit} disabled={isSubmitting}>
            Adicionar
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
