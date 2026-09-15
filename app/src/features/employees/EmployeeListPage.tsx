import { useState } from "react"
import { useNavigate } from "react-router-dom"
import { Plus } from "lucide-react"
import { Button } from "@/components/ui/button"
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuTrigger,
} from "@/components/ui/context-menu"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import type { EmployeeSummary } from "@/lib/api/types"
import { EmployeeFormModal } from "./EmployeeFormModal"
import { useEmployees } from "./hooks/useEmployees"
import { WorkLogFormModal } from "../work-logs/WorkLogFormModal"

export function EmployeeListPage() {
  const { data: employees, isLoading } = useEmployees()
  const navigate = useNavigate()

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false)
  const [workLogEmployee, setWorkLogEmployee] = useState<EmployeeSummary | null>(null)

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Funcionários</h1>
        <Button onClick={() => setIsCreateModalOpen(true)}>
          <Plus className="size-4" />
          Novo funcionário
        </Button>
      </div>

      {isLoading && <p className="text-muted-foreground">Carregando...</p>}

      {!isLoading && employees && employees.length === 0 && (
        <p className="text-muted-foreground">Nenhum funcionário cadastrado.</p>
      )}

      {!isLoading && employees && employees.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Nome</TableHead>
              <TableHead>Cargo</TableHead>
              <TableHead>Data de admissão</TableHead>
              <TableHead>Horas/dia</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {employees.map((employee) => (
              <ContextMenu key={employee.id}>
                <ContextMenuTrigger asChild>
                  <TableRow
                    className="cursor-pointer"
                    onClick={() => navigate(`/employees/${employee.id}`)}
                    onContextMenu={(event) => event.preventDefault()}
                  >
                    <TableCell>{employee.name}</TableCell>
                    <TableCell>{employee.role}</TableCell>
                    <TableCell>{employee.hireDate}</TableCell>
                    <TableCell>{employee.dailyWorkHours ?? "-"}</TableCell>
                  </TableRow>
                </ContextMenuTrigger>
                <ContextMenuContent>
                  <ContextMenuItem onSelect={() => setWorkLogEmployee(employee)}>
                    Novo worklog
                  </ContextMenuItem>
                </ContextMenuContent>
              </ContextMenu>
            ))}
          </TableBody>
        </Table>
      )}

      <EmployeeFormModal open={isCreateModalOpen} onOpenChange={setIsCreateModalOpen} />

      {workLogEmployee && (
        <WorkLogFormModal
          open={Boolean(workLogEmployee)}
          onOpenChange={(open) => {
            if (!open) {
              setWorkLogEmployee(null)
            }
          }}
          employeeId={workLogEmployee.id}
        />
      )}
    </div>
  )
}
