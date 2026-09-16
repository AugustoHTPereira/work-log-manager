import { toast } from "sonner";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { DAYS } from "@/components/WeekScheduleEditor";
import type { WorkLogType } from "@/lib/api/types";
import { useGeneralWorkSchedule } from "./hooks/useGeneralWorkSchedule";
import {
  useAddSystemParameterValue,
  useRemoveSystemParameterValue,
  useSystemParameters,
  useUpdateSystemParameter,
} from "./hooks/useSystemParameters";
import { GenericWorkScheduleModal } from "./GenericWorkScheduleModal";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Section, SectionHeader, SectionTitle } from "./components/Section";

const AUTO_WORK_LOG_TYPE_OPTIONS: {
  type: Extract<WorkLogType, "RegularAttendance" | "Break">;
  label: string;
}[] = [
  { type: "RegularAttendance", label: "Presença regular" },
  { type: "Break", label: "Intervalo" },
];

export function SystemSettingsPage() {
  const { data: schedule, isLoading } = useGeneralWorkSchedule();

  const { data: systemParameters } = useSystemParameters();
  const updateSystemParameter = useUpdateSystemParameter();
  const addSystemParameterValue = useAddSystemParameterValue();
  const removeSystemParameterValue = useRemoveSystemParameterValue();

  const autoWorkLogTypeRows = (systemParameters ?? []).filter(
    (p) => p.param === "AutoWorkLogTypes",
  );
  const enabledAutoWorkLogTypes =
    autoWorkLogTypeRows.length > 0
      ? autoWorkLogTypeRows.map((row) => row.value)
      : ["RegularAttendance"];

  const allowManageClosedWorkLogsParam = systemParameters?.find(
    (p) => p.param === "AllowManageClosedWorkLogs",
  );
  const allowManageClosedWorkLogs =
    allowManageClosedWorkLogsParam?.value === "true";

  const handleAutoWorkLogTypeChange = async (
    type: string,
    checked: boolean,
  ) => {
    try {
      if (checked) {
        await addSystemParameterValue.mutateAsync({
          param: "AutoWorkLogTypes",
          value: type,
        });
      } else {
        const row = autoWorkLogTypeRows.find((r) => r.value === type);
        if (!row) {
          return;
        }
        await removeSystemParameterValue.mutateAsync({
          param: "AutoWorkLogTypes",
          id: row.id,
        });
      }
      toast.success("Configurações atualizadas com sucesso.");
    } catch {
      toast.error("Não foi possível atualizar as configurações.");
    }
  };

  const handleAllowManageClosedWorkLogsChange = async (checked: boolean) => {
    try {
      await updateSystemParameter.mutateAsync({
        param: "AllowManageClosedWorkLogs",
        value: String(checked),
      });
      toast.success("Configurações atualizadas com sucesso.");
    } catch {
      toast.error("Não foi possível atualizar as configurações.");
    }
  };

  if (isLoading) {
    return <p className="text-muted-foreground">Carregando...</p>;
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Configurações</h1>

      <div className="w-full space-y-12">
        <Section>
          <SectionHeader className="border-b">
            <SectionTitle>Configurações do sistema</SectionTitle>
          </SectionHeader>

          <div className="divide-y">
            <div className="space-y-2 p-4">
              <p className="text-sm">
                Tipos gerados automaticamente no fechamento de mês
              </p>
              {AUTO_WORK_LOG_TYPE_OPTIONS.map(({ type, label }) => (
                <div key={type} className="flex items-center gap-2">
                  <Checkbox
                    id={`auto-work-log-type-${type}`}
                    checked={enabledAutoWorkLogTypes.includes(type)}
                    onCheckedChange={(checked) =>
                      handleAutoWorkLogTypeChange(type, checked === true)
                    }
                  />
                  <Label htmlFor={`auto-work-log-type-${type}`}>{label}</Label>
                </div>
              ))}
            </div>

            <div className="flex items-center gap-2 p-4">
              <Switch
                id="allow-manage-closed-work-logs"
                checked={allowManageClosedWorkLogs}
                onCheckedChange={handleAllowManageClosedWorkLogsChange}
              />
              <Label htmlFor="allow-manage-closed-work-logs">
                Permitir gerenciar worklogs em meses fechados
              </Label>
            </div>
          </div>
        </Section>

        <Section>
          <SectionHeader className="border-b flex items-center justify-between">
            <SectionTitle>Expediente</SectionTitle>
            <GenericWorkScheduleModal schedule={schedule!} />
          </SectionHeader>

          <div>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[20%]">Dia</TableHead>
                  <TableHead className="w-[80%]">Horários</TableHead>
                </TableRow>
              </TableHeader>

              <TableBody>
                {schedule &&
                  DAYS.map((day) => {
                    const dayPeriods = schedule
                      .map((period, index) => ({ period, index }))
                      .filter(({ period }) => period.dayOfWeek === day.value);

                    return (
                      <TableRow key={day.value}>
                        <TableCell>{day.label}</TableCell>
                        <TableCell>
                          {dayPeriods.length === 0 ? (
                            <span className="text-muted-foreground">
                              Sem expediente
                            </span>
                          ) : (
                            <div className="space-y-0.5">
                              {dayPeriods.map(({ period, index }) => (
                                <div
                                  key={index}
                                  className="flex items-center gap-1"
                                >
                                  <span>{period.startTime}</span>
                                  <span className="text-muted-foreground">
                                    -
                                  </span>
                                  <span>{period.endTime}</span>
                                </div>
                              ))}
                            </div>
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
              </TableBody>
            </Table>
          </div>
        </Section>
      </div>
    </div>
  );
}
