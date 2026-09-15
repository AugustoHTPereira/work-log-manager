import { zodResolver } from "@hookform/resolvers/zod"
import { useEffect } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form"
import { Input } from "@/components/ui/input"
import type { EmployeeSummary } from "@/lib/api/types"
import { useCreateEmployee } from "./hooks/useCreateEmployee"
import { useUpdateEmployee } from "./hooks/useUpdateEmployee"

const employeeFormSchema = z.object({
  name: z.string().min(1, "Nome é obrigatório."),
  role: z.string().min(1, "Cargo é obrigatório."),
  hireDate: z.string().min(1, "Data de admissão é obrigatória."),
  dailyWorkHours: z.string().optional(),
})

type EmployeeFormValues = z.infer<typeof employeeFormSchema>

interface EmployeeFormModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  employee?: EmployeeSummary
}

export function EmployeeFormModal({ open, onOpenChange, employee }: EmployeeFormModalProps) {
  const isEditing = Boolean(employee)
  const createEmployee = useCreateEmployee()
  const updateEmployee = useUpdateEmployee()

  const form = useForm<EmployeeFormValues>({
    resolver: zodResolver(employeeFormSchema),
    defaultValues: {
      name: "",
      role: "",
      hireDate: "",
      dailyWorkHours: "",
    },
  })

  useEffect(() => {
    if (!open) {
      return
    }

    form.reset({
      name: employee?.name ?? "",
      role: employee?.role ?? "",
      hireDate: employee?.hireDate ?? "",
      dailyWorkHours: employee?.dailyWorkHours != null ? String(employee.dailyWorkHours) : "",
    })
  }, [open, employee, form])

  const onSubmit = async (values: EmployeeFormValues) => {
    const payload = {
      name: values.name,
      role: values.role,
      hireDate: values.hireDate,
      dailyWorkHours: values.dailyWorkHours ? Number(values.dailyWorkHours) : null,
    }

    try {
      if (isEditing && employee) {
        await updateEmployee.mutateAsync({ id: employee.id, payload })
        toast.success("Funcionário atualizado com sucesso.")
      } else {
        await createEmployee.mutateAsync(payload)
        toast.success("Funcionário criado com sucesso.")
      }
      onOpenChange(false)
    } catch {
      toast.error("Não foi possível salvar o funcionário.")
    }
  }

  const isSubmitting = createEmployee.isPending || updateEmployee.isPending

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEditing ? "Editar funcionário" : "Novo funcionário"}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nome</FormLabel>
                  <FormControl>
                    <Input placeholder="Nome do funcionário" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="role"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Cargo</FormLabel>
                  <FormControl>
                    <Input placeholder="Cargo" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="hireDate"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Data de admissão</FormLabel>
                  <FormControl>
                    <Input type="date" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="dailyWorkHours"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Horas/dia (opcional)</FormLabel>
                  <FormControl>
                    <Input type="number" step="0.01" min="0" placeholder="Ex.: 8" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <DialogFooter>
              <Button type="submit" disabled={isSubmitting}>
                Salvar
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}
