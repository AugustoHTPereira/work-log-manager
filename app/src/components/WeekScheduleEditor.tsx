import { Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { DayOfWeek, WorkSchedulePeriodInput } from "@/lib/api/types";

export const DAYS: { value: DayOfWeek; label: string }[] = [
  { value: "Monday", label: "Segunda" },
  { value: "Tuesday", label: "Terça" },
  { value: "Wednesday", label: "Quarta" },
  { value: "Thursday", label: "Quinta" },
  { value: "Friday", label: "Sexta" },
  { value: "Saturday", label: "Sábado" },
  { value: "Sunday", label: "Domingo" },
];

const DEFAULT_START_TIME = "07:00";
const DEFAULT_END_TIME = "17:00";

/**
 * Trims a "HH:mm:ss" time string (as returned by the API) down to "HH:mm" for display in an
 * `<input type="time">`. Already-short values (e.g. produced by the input itself) pass through
 * unchanged.
 */
function toTimeInputValue(value: string): string {
  return value.slice(0, 5);
}

interface WeekScheduleEditorProps {
  periods: WorkSchedulePeriodInput[];
  onChange: (periods: WorkSchedulePeriodInput[]) => void;
}

/**
 * Controlled, shared editor for a week's worth of work schedule periods (7 fixed days, each
 * with a dynamic list of start/end time periods). Used identically by the general settings
 * page and by each employee's own schedule section - no direct `lib/api` calls, all state is
 * owned by the parent via `periods`/`onChange`.
 */
export function WeekScheduleEditor({
  periods,
  onChange,
}: WeekScheduleEditorProps) {
  const handleAddPeriod = (dayOfWeek: DayOfWeek) => {
    onChange([
      ...periods,
      { dayOfWeek, startTime: DEFAULT_START_TIME, endTime: DEFAULT_END_TIME },
    ]);
  };

  const handleRemovePeriod = (indexToRemove: number) => {
    onChange(periods.filter((_, index) => index !== indexToRemove));
  };

  const handlePeriodChange = (
    indexToUpdate: number,
    field: "startTime" | "endTime",
    value: string,
  ) => {
    onChange(
      periods.map((period, index) =>
        index === indexToUpdate ? { ...period, [field]: value } : period,
      ),
    );
  };

  return (
    <div className="divide-y">
      {DAYS.map((day) => {
        const dayPeriods = periods
          .map((period, index) => ({ period, index }))
          .filter(({ period }) => period.dayOfWeek === day.value);

        return (
          <div key={day.value} className="pb-2 mb-2">
            <div className="flex items-center justify-between">
              <h3 className="font-medium">{day.label}</h3>

              <Button
                type="button"
                variant="outline"
                size="icon-sm"
                aria-label={`Adicionar período em ${day.label}`}
                onClick={() => handleAddPeriod(day.value)}
              >
                <Plus className="size-4" />
              </Button>
            </div>

            {dayPeriods.length === 0 && (
              <p className="text-sm text-muted-foreground">
                Folga (sem expediente).
              </p>
            )}

            <div className="space-y-0.5">
              {dayPeriods.map(({ period, index }) => (
                <div key={index} className="flex items-center gap-2">
                  <Input
                    type="time"
                    value={toTimeInputValue(period.startTime)}
                    onChange={(event) =>
                      handlePeriodChange(index, "startTime", event.target.value)
                    }
                    className="w-auto"
                  />
                  <span className="text-muted-foreground">até</span>
                  <Input
                    type="time"
                    value={toTimeInputValue(period.endTime)}
                    onChange={(event) =>
                      handlePeriodChange(index, "endTime", event.target.value)
                    }
                    className="w-auto"
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    aria-label={`Remover período em ${day.label}`}
                    onClick={() => handleRemovePeriod(index)}
                  >
                    <Trash2 className="size-4 text-destructive" />
                  </Button>
                </div>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}
