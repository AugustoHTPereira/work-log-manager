import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { WeekScheduleEditor } from "@/components/WeekScheduleEditor";
import type { EmployeeSummary, WorkSchedulePeriodInput } from "@/lib/api/types";
import {
  useEmployeeWorkSchedule,
  useUpdateEmployeeWorkSchedule,
} from "./hooks/useEmployeeWorkSchedule";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
interface EmployeeWorkScheduleModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  employee?: EmployeeSummary;
}

export function EmployeeWorkScheduleModal({
  open,
  onOpenChange,
  employee,
}: EmployeeWorkScheduleModalProps) {
  const updateWorkSchedule = useUpdateEmployeeWorkSchedule(employee?.id ?? "");
  const { data: workSchedule } = useEmployeeWorkSchedule(employee?.id ?? "");

  const [workSchedulePeriods, setWorkSchedulePeriods] = useState<
    WorkSchedulePeriodInput[]
  >([]);

  useEffect(() => {
    if (workSchedule) {
      setWorkSchedulePeriods(
        workSchedule.map(({ dayOfWeek, startTime, endTime }) => ({
          dayOfWeek,
          startTime,
          endTime,
        })),
      );
    }
  }, [workSchedule]);

  const handleSaveWorkSchedule = async () => {
    try {
      await updateWorkSchedule.mutateAsync(workSchedulePeriods);
      toast.success("Horário de expediente atualizado com sucesso.");
    } catch {
      toast.error("Não foi possível atualizar o horário de expediente.");
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>Horário de trabalho</DialogTitle>
        </DialogHeader>

        <WeekScheduleEditor
          periods={workSchedulePeriods}
          onChange={setWorkSchedulePeriods}
        />

        <DialogFooter>
          <Button onClick={handleSaveWorkSchedule}>Salvar</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
