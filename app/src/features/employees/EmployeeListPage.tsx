import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import {
  CalendarCheck,
  DownloadCloud,
  MoreHorizontal,
  Pen,
  Plus,
  Trash,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { EmployeeSummary, MonthClosingResult } from "@/lib/api/types";
import { MonthCloseModal } from "@/features/month-closing/MonthCloseModal";
import { DeleteConfirmDialog } from "@/components/DeleteConfirmDialog";
import { EmployeeFormModal } from "./EmployeeFormModal";
import { useEmployees } from "./hooks/useEmployees";
import { WorkLogFormModal } from "../work-logs/WorkLogFormModal";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useMonthClosingReport } from "../month-closing/hooks/useMonthClosingReport";
import { useMonthClosings } from "../month-closing/hooks/useMonthClosings";
import { useDeleteMonthClosing } from "../month-closing/hooks/useDeleteMonthClosing";
import { formatDate } from "@/lib/date";
import { cn } from "cn";

export function EmployeeListPage() {
  const { data: employees, isLoading } = useEmployees();
  const navigate = useNavigate();

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isMonthCloseModalOpen, setIsMonthCloseModalOpen] = useState(false);
  const [workLogEmployee, setWorkLogEmployee] =
    useState<EmployeeSummary | null>(null);
  const { data: monthClosings } = useMonthClosings();

  return (
    <div>
      <div
        className={cn(
          "grid gap-4 grid-cols-1",
          !!monthClosings?.length && "md:grid-cols-[1fr_minmax(200px,400px)]",
        )}
      >
        <div className="space-y-6">
          <div className="flex items-center justify-between">
            <h1 className="text-2xl font-semibold">Funcionários</h1>
            <div className="flex items-center gap-2">
              <Button
                variant="secondary"
                onClick={() => setIsMonthCloseModalOpen(true)}
              >
                <CalendarCheck className="size-4" />
                Fechar mês
              </Button>
              <Button
                onClick={() => setIsCreateModalOpen(true)}
                variant="outline"
                size="icon"
              >
                <Plus className="size-4" />
              </Button>
            </div>
          </div>

          {isLoading && <p className="text-muted-foreground">Carregando...</p>}

          {!isLoading && employees && employees.length === 0 && (
            <p className="text-muted-foreground">
              Nenhum funcionário cadastrado.
            </p>
          )}

          {!isLoading && employees && employees.length > 0 && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[50%]">Nome</TableHead>
                  <TableHead className="w-[25%]">Cargo</TableHead>
                  <TableHead className="w-[10%]">Data de admissão</TableHead>
                  <TableHead className="w-[5%]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {employees.map((employee) => (
                  <DropdownMenu key={employee.id}>
                    <TableRow
                      className="cursor-pointer"
                      onClick={() => navigate(`/employees/${employee.id}`)}
                      onContextMenu={(event) => event.preventDefault()}
                    >
                      <TableCell>{employee.name}</TableCell>
                      <TableCell>{employee.role}</TableCell>
                      <TableCell>
                        {formatDate(employee.hireDate, "dd/MM/yyyy")}
                      </TableCell>
                      <TableCell className="flex justify-end gap-2 py-0.5">
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon-sm">
                            <MoreHorizontal />
                          </Button>
                        </DropdownMenuTrigger>
                      </TableCell>
                    </TableRow>

                    <DropdownMenuContent>
                      <DropdownMenuItem
                        onSelect={() => setWorkLogEmployee(employee)}
                      >
                        <Pen />
                        Editar
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        onSelect={() => setWorkLogEmployee(employee)}
                      >
                        <Plus />
                        Adicionar worklog
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                ))}
              </TableBody>
            </Table>
          )}
        </div>

        {!!monthClosings?.length && (
          <div>
            <div className="border rounded-md">
              <MonthClosingsTable monthClosings={monthClosings} />
            </div>
          </div>
        )}
      </div>

      <EmployeeFormModal
        open={isCreateModalOpen}
        onOpenChange={setIsCreateModalOpen}
      />

      <MonthCloseModal
        open={isMonthCloseModalOpen}
        onOpenChange={setIsMonthCloseModalOpen}
      />

      {workLogEmployee && (
        <WorkLogFormModal
          open={Boolean(workLogEmployee)}
          onOpenChange={(open) => {
            if (!open) {
              setWorkLogEmployee(null);
            }
          }}
          employeeId={workLogEmployee.id}
        />
      )}
    </div>
  );
}

function MonthClosingsTable({
  monthClosings,
}: {
  monthClosings: MonthClosingResult[];
}) {
  const downloadReport = useMonthClosingReport();
  const deleteMonthClosing = useDeleteMonthClosing();
  const [closingToDelete, setClosingToDelete] =
    useState<MonthClosingResult | null>(null);

  const handleDeleteMonthClosing = async () => {
    if (!closingToDelete) return;
    try {
      const result = await deleteMonthClosing.mutateAsync(closingToDelete.id);
      toast.success(
        `Fechamento excluído. ${result.deletedAutomaticWorkLogsCount} worklogs automáticos removidos, ${result.unlinkedManualWorkLogsCount} worklogs manuais desvinculados.`,
      );
    } catch {
      toast.error("Não foi possível excluir o fechamento.");
    } finally {
      setClosingToDelete(null);
    }
  };

  return (
    <>
      {monthClosings?.length && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-[60%]">Fechamento</TableHead>
              <TableHead className="w-[20%]" />
              <TableHead className="w-[20%]" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {monthClosings.map((monthClosing) => (
              <TableRow key={monthClosing.id}>
                <TableCell className="font-medium">
                  {monthClosing.month.toString().padStart(2, "0")}/
                  {monthClosing.year}
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatDate(monthClosing.createdAtUtc, "dd/MM/yyyy HH:mm")}
                </TableCell>
                <TableCell className="flex justify-end gap-0 py-0.5">
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    onClick={() =>
                      downloadReport.mutateAsync({ ...monthClosing })
                    }
                  >
                    <DownloadCloud />
                  </Button>

                  <Button
                    variant="ghost"
                    size="icon-sm"
                    className="text-destructive hover:text-destructive hover:bg-destructive/10"
                    onClick={() => setClosingToDelete(monthClosing)}
                  >
                    <Trash />
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <DeleteConfirmDialog
        open={Boolean(closingToDelete)}
        onOpenChange={(open) => {
          if (!open) setClosingToDelete(null);
        }}
        title="Excluir fechamento"
        description={
          closingToDelete
            ? `Isso excluirá permanentemente o fechamento de ${closingToDelete.month
                .toString()
                .padStart(
                  2,
                  "0",
                )}/${closingToDelete.year} e todos os worklogs gerados automaticamente por ele. Worklogs manuais vinculados serão apenas desvinculados, não excluídos. Esta ação não pode ser desfeita.`
            : ""
        }
        onConfirm={handleDeleteMonthClosing}
      />
    </>
  );
}
