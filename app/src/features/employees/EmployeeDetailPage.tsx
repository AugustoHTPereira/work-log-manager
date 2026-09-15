import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { MoreHorizontal, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
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
import { Info } from "@/components/ui/info";
import { formatDuration } from "@/lib/worklog-duration";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { format } from "date-fns";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { Badge, type BadgeProps } from "@/components/ui/badge";

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
  const { data: employee, isLoading } = useEmployee(id);
  const deleteEmployee = useDeleteEmployee();
  const deleteWorkLog = useDeleteWorkLog(id ?? "");

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
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
    } catch {
      toast.error("Não foi possível excluir o worklog.");
    } finally {
      setWorkLogToDelete(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{employee.name}</h1>
          <p className="text-muted-foreground">{employee.role}</p>
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
                  <Pencil className="size-4" />
                  Editar
                </DropdownMenuItem>
                <DropdownMenuItem
                  variant="destructive"
                  onClick={() => setIsDeleteEmployeeDialogOpen(true)}
                >
                  <Trash2 className="size-4" />
                  Excluir
                </DropdownMenuItem>
              </DropdownMenuGroup>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4 rounded-md border p-4 sm:grid-cols-3">
        <Info
          title="Data de admissão"
          value={employee.hireDate}
          description="Data de admissão do funcionário."
        />
        <Info
          title="Horas/dia (específico)"
          value={employee.dailyWorkHours ?? "-"}
          description="Horas/dia específicas para este funcionário. Caso não esteja definido, o valor padrão da empresa será usado."
        />
        <Info
          title="Horas/dia (efetivo)"
          value={employee.effectiveDailyWorkHours ?? "-"}
          description="Horas/dia efetivas do funcionário, considerando o valor específico ou o padrão da empresa."
        />
      </div>

      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold">Worklogs</h2>
          <Button
            onClick={() => setWorkLogModal({ open: true })}
            size="icon"
            variant="outline"
          >
            <Plus className="size-4" />
          </Button>
        </div>

        {employee.workLogs.length === 0 && (
          <p className="text-muted-foreground">Nenhum worklog registrado.</p>
        )}

        {employee.workLogs.length > 0 && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-[10%]">Data</TableHead>
                <TableHead className="w-[70%]">Tipo</TableHead>
                <TableHead className="w-[10%]">Duração</TableHead>
                <TableHead className="w-[10%] text-right" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {employee.workLogs.map((workLog) => (
                <TableRow key={workLog.id}>
                  <TableCell>
                    <Tooltip>
                      <TooltipTrigger>
                        {format(new Date(workLog.startDate), "dd/MM/yyyy")}
                      </TooltipTrigger>
                      <TooltipContent>
                        <div className="flex items-center gap-1">
                          <span>
                            {format(
                              new Date(workLog.startDate),
                              "dd/MM/yyyy HH:mm",
                            )}
                          </span>
                          <span>-</span>
                          <span>
                            {format(
                              new Date(workLog.endDate),
                              "dd/MM/yyyy HH:mm",
                            )}
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
                    <Tooltip>
                      <TooltipTrigger>
                        {formatDuration(workLog.durationSeconds)}
                      </TooltipTrigger>
                      <TooltipContent>
                        <div className="flex items-center gap-1">
                          <span>
                            {format(
                              new Date(workLog.startDate),
                              "dd/MM/yyyy HH:mm",
                            )}
                          </span>
                          <span>-</span>
                          <span>
                            {format(
                              new Date(workLog.endDate),
                              "dd/MM/yyyy HH:mm",
                            )}
                          </span>
                        </div>
                      </TooltipContent>
                    </Tooltip>
                  </TableCell>
                  <TableCell className="flex justify-end gap-2 py-0.5">
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon-sm">
                          <MoreHorizontal className="size-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent>
                        <DropdownMenuGroup>
                          <DropdownMenuItem
                            onClick={() =>
                              setWorkLogModal({ open: true, workLog })
                            }
                          >
                            <Pencil className="size-4" />
                            Editar
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            variant="destructive"
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
          dailyWorkHours: employee.dailyWorkHours,
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
    </div>
  );
}
