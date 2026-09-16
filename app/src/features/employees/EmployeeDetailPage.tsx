import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  Bot,
  Cog,
  Lock,
  MoreHorizontal,
  Pencil,
  Plus,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";
import { ApiError } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { DeleteConfirmDialog } from "@/components/DeleteConfirmDialog";
import type { EmployeeWorkLog } from "@/lib/api/types";
import { EmployeeFormModal } from "./EmployeeFormModal";
import { useDeleteEmployee } from "./hooks/useDeleteEmployee";
import { useEmployee } from "./hooks/useEmployee";
import { WorkLogFormModal } from "../work-logs/WorkLogFormModal";
import { useDeleteWorkLog } from "../work-logs/hooks/useDeleteWorkLog";
import { useEmployeeWorkLogs } from "../work-logs/hooks/useEmployeeWorkLogs";
import { formatDuration } from "@/lib/worklog-duration";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { Badge, type BadgeProps } from "@/components/ui/badge";
import { WorkLogFiltersSheet } from "./WorkLogFiltersSheet";
import {
  parseWorkLogFiltersFromSearchParams,
  workLogFiltersToApiFilters,
  workLogFiltersToSearchParams,
  type WorkLogFilters,
} from "./workLogFilters";
import { EmployeeWorkScheduleModal } from "./EmployeeWorkScheduleModal";
import { useParam } from "@/hooks/use-param";
import { formatDate } from "@/lib/date";
import { intervalToDuration } from "date-fns";

const workLogTypeLabels: Record<string, string> = {
  Overtime: "Hora extra",
  Absence: "Falta",
  RegularAttendance: "Presença",
  Break: "Intervalo",
};

const workLogTypeVariants: Record<string, Pick<BadgeProps, "variant">> = {
  Overtime: { variant: "default" },
  Absence: { variant: "destructive" },
  RegularAttendance: { variant: "outline" },
  Break: { variant: "outline" },
};

export function EmployeeDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { data: employee, isLoading } = useEmployee(id);
  const deleteEmployee = useDeleteEmployee();
  const deleteWorkLog = useDeleteWorkLog(id ?? "");
  const allowManageClosedWorkLogs = useParam(
    "allowManageClosedWorkLogs",
    false,
  );

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isEditWorkScheduleModalOpen, setIsEditWorkScheduleModalOpen] =
    useState(false);
  const [isDeleteEmployeeDialogOpen, setIsDeleteEmployeeDialogOpen] =
    useState(false);
  const [workLogModal, setWorkLogModal] = useState<{
    open: boolean;
    workLog?: EmployeeWorkLog;
  }>({
    open: false,
  });
  const [workLogToDelete, setWorkLogToDelete] =
    useState<EmployeeWorkLog | null>(null);

  const filters = useMemo(
    () => parseWorkLogFiltersFromSearchParams(searchParams),
    [searchParams],
  );

  useEffect(() => {
    if (!searchParams.get("startDate") || !searchParams.get("endDate")) {
      setSearchParams(workLogFiltersToSearchParams(filters), { replace: true });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleApplyFilters = (nextFilters: WorkLogFilters) => {
    setSearchParams(workLogFiltersToSearchParams(nextFilters));
  };

  const apiFilters = useMemo(
    () => workLogFiltersToApiFilters(filters),
    [filters],
  );
  const { data: workLogs } = useEmployeeWorkLogs(id, apiFilters);
  const filteredWorkLogs = workLogs ?? [];

  if (isLoading) {
    return <p className="text-muted-foreground">Carregando...</p>;
  }

  if (!employee || !id) {
    return <p className="text-muted-foreground">Funcionário não encontrado.</p>;
  }

  const handleDeleteEmployee = async () => {
    try {
      await deleteEmployee.mutateAsync(employee.id);
      toast.success("Funcionário excluído com sucesso.");
      navigate("/");
    } catch {
      toast.error("Não foi possível excluir o funcionário.");
    }
  };

  const handleDeleteWorkLog = async () => {
    if (!workLogToDelete) return;
    try {
      await deleteWorkLog.mutateAsync(workLogToDelete.id);
      toast.success("Worklog excluído com sucesso.");
    } catch (error) {
      const message =
        error instanceof ApiError
          ? error.message
          : "Não foi possível excluir o worklog.";
      toast.error(message);
    } finally {
      setWorkLogToDelete(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{employee.name}</h1>
          <p className="text-muted-foreground text-sm">
            {employee.role} admitido(a) em{" "}
            {formatDate(employee.hireDate, "dd/MM/yyyy")}
          </p>
        </div>
        <div className="flex gap-2">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="icon">
                <MoreHorizontal />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent>
              <DropdownMenuGroup>
                <DropdownMenuItem onClick={() => setIsEditModalOpen(true)}>
                  <Pencil />
                  Editar
                </DropdownMenuItem>
                <DropdownMenuItem
                  onClick={() => setIsEditWorkScheduleModalOpen(true)}
                >
                  <Cog />
                  Configurações
                </DropdownMenuItem>
                <DropdownMenuItem
                  variant="destructive"
                  onClick={() => setIsDeleteEmployeeDialogOpen(true)}
                >
                  <Trash2 />
                  Excluir
                </DropdownMenuItem>
              </DropdownMenuGroup>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold">Worklogs</h2>
          <div className="flex gap-2">
            <WorkLogFiltersSheet
              filters={filters}
              onApply={handleApplyFilters}
            />
            <Button
              onClick={() => setWorkLogModal({ open: true })}
              size="icon"
              variant="outline"
            >
              <Plus className="size-4" />
            </Button>
          </div>
        </div>

        {filteredWorkLogs.length === 0 && (
          <p className="text-muted-foreground">
            Nenhum worklog encontrado para o filtro selecionado.
          </p>
        )}

        {filteredWorkLogs.length > 0 && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-[10%]">Data</TableHead>
                <TableHead className="w-[10%]">Duração</TableHead>
                <TableHead className="w-[10%]">Tipo</TableHead>
                <TableHead className="w-[50%]">Notas</TableHead>
                <TableHead className="w-[10%] text-right" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredWorkLogs.map((workLog) => (
                <TableRow key={workLog.id}>
                  <TableCell>
                    <Tooltip>
                      <TooltipTrigger>
                        {formatDate(
                          new Date(workLog.startDate),
                          "EEE dd/MM/yyyy",
                        )}
                      </TooltipTrigger>
                      <TooltipContent>
                        <div className="flex items-center gap-1">
                          <span>
                            {formatDate(
                              new Date(workLog.startDate),
                              "dd/MM/yyyy HH:mm",
                            )}
                          </span>
                          <span>-</span>
                          <span>
                            {formatDate(
                              new Date(workLog.endDate),
                              "dd/MM/yyyy HH:mm",
                            )}
                          </span>
                        </div>
                      </TooltipContent>
                    </Tooltip>
                  </TableCell>
                  <TableCell>
                    <Tooltip>
                      <TooltipTrigger>
                        {formatDuration(workLog.durationSeconds)}
                      </TooltipTrigger>
                      <TooltipContent>
                        <div className="flex items-center gap-1">
                          <span>
                            {formatDate(new Date(workLog.startDate), "HH:mm")}
                          </span>
                          <span>-</span>
                          <span>
                            {formatDate(new Date(workLog.endDate), "HH:mm")}
                          </span>
                        </div>
                      </TooltipContent>
                    </Tooltip>
                  </TableCell>
                  <TableCell className="py-0.5">
                    <Badge variant={workLogTypeVariants[workLog.type]?.variant}>
                      {workLogTypeLabels[workLog.type] ?? workLog.type}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    {workLog.note ? (
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <span className="block max-w-[220px] truncate text-muted-foreground">
                            {workLog.note}
                          </span>
                        </TooltipTrigger>
                        <TooltipContent>{workLog.note}</TooltipContent>
                      </Tooltip>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="flex justify-end gap-2 py-0.5">
                    {workLog.origin === "Automatic" && (
                      <Tooltip>
                        <TooltipTrigger>
                          <Bot className="size-3.5 text-blue-400" />
                        </TooltipTrigger>
                        <TooltipContent>
                          Gerado automaticamente no fechamento de ponto.
                        </TooltipContent>
                      </Tooltip>
                    )}

                    {workLog.monthClosingId &&
                      !allowManageClosedWorkLogs.value && (
                        <Tooltip>
                          <TooltipTrigger>
                            <Lock className="size-3.5 text-muted-foreground" />
                          </TooltipTrigger>
                          <TooltipContent>
                            Vinculado ao fechamento do mês — não pode ser
                            editado ou excluído.
                          </TooltipContent>
                        </Tooltip>
                      )}

                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          data-testid="work-log-row-menu-trigger"
                        >
                          <MoreHorizontal className="size-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent>
                        <DropdownMenuGroup>
                          <DropdownMenuItem
                            disabled={
                              Boolean(workLog.monthClosingId) &&
                              !allowManageClosedWorkLogs.value
                            }
                            onClick={() =>
                              setWorkLogModal({ open: true, workLog })
                            }
                          >
                            <Pencil className="size-4" />
                            Editar
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            variant="destructive"
                            disabled={
                              Boolean(workLog.monthClosingId) &&
                              !allowManageClosedWorkLogs.value
                            }
                            onClick={() => setWorkLogToDelete(workLog)}
                          >
                            <Trash2 className="size-4" />
                            Excluir
                          </DropdownMenuItem>
                        </DropdownMenuGroup>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      <EmployeeFormModal
        open={isEditModalOpen}
        onOpenChange={setIsEditModalOpen}
        employee={{
          id: employee.id,
          name: employee.name,
          role: employee.role,
          hireDate: employee.hireDate,
        }}
      />

      <DeleteConfirmDialog
        open={isDeleteEmployeeDialogOpen}
        onOpenChange={setIsDeleteEmployeeDialogOpen}
        title="Excluir funcionário"
        description="Esta ação removerá o funcionário e todos os seus worklogs. Deseja continuar?"
        onConfirm={handleDeleteEmployee}
      />

      <DeleteConfirmDialog
        open={Boolean(workLogToDelete)}
        onOpenChange={(open) => {
          if (!open) setWorkLogToDelete(null);
        }}
        title="Excluir worklog"
        description="Esta ação não pode ser desfeita. Deseja continuar?"
        onConfirm={handleDeleteWorkLog}
      />

      <WorkLogFormModal
        open={workLogModal.open}
        onOpenChange={(open) => setWorkLogModal({ open })}
        employeeId={id}
        workLog={workLogModal.workLog}
      />

      <EmployeeWorkScheduleModal
        open={isEditWorkScheduleModalOpen}
        onOpenChange={setIsEditWorkScheduleModalOpen}
        employee={employee}
      />
    </div>
  );
}
