import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { WeekScheduleEditor } from "@/components/WeekScheduleEditor";
import { Pen } from "lucide-react";
import { toast } from "sonner";
import { useUpdateGeneralWorkSchedule } from "./hooks/useGeneralWorkSchedule";
import type { WorkSchedulePeriodInput } from "@/lib/api/types";
import { useEffect, useState } from "react";

type GenericWorkScheduleModalProps = {
  schedule: WorkSchedulePeriodInput[];
};

export function GenericWorkScheduleModal({
  schedule,
}: GenericWorkScheduleModalProps) {
  const [periods, setPeriods] = useState<WorkSchedulePeriodInput[]>([]);
  const updateSchedule = useUpdateGeneralWorkSchedule();

  useEffect(() => {
    if (schedule) {
      setPeriods(
        schedule.map(({ dayOfWeek, startTime, endTime }) => ({
          dayOfWeek,
          startTime,
          endTime,
        })),
      );
    }
  }, [schedule]);

  const handleSave = async () => {
    try {
      await updateSchedule.mutateAsync(periods);
      toast.success("Configurações atualizadas com sucesso.");
    } catch {
      toast.error("Não foi possível atualizar as configurações.");
    }
  };

  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button variant="outline" size="icon-sm">
          <Pen />
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Configurações de jornada</DialogTitle>
        </DialogHeader>
        <WeekScheduleEditor periods={periods} onChange={setPeriods} />
        <DialogFooter>
          <Button onClick={handleSave} disabled={updateSchedule.isPending}>
            Salvar
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
