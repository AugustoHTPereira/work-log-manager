import { useMutation, useQueryClient } from "@tanstack/react-query"
import { updateEmployee } from "@/lib/api/employees"
import type { UpdateEmployeePayload } from "@/lib/api/types"
import { employeeQueryKey } from "./useEmployee"
import { employeesQueryKey } from "./useEmployees"

export function useUpdateEmployee() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: UpdateEmployeePayload }) => updateEmployee(id, payload),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: employeesQueryKey })
      queryClient.invalidateQueries({ queryKey: employeeQueryKey(variables.id) })
    },
  })
}
