import { useMutation, useQueryClient } from "@tanstack/react-query"
import { createEmployee } from "@/lib/api/employees"
import { employeesQueryKey } from "./useEmployees"

export function useCreateEmployee() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createEmployee,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: employeesQueryKey })
    },
  })
}
