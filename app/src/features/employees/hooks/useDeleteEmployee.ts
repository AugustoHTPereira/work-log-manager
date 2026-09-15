import { useMutation, useQueryClient } from "@tanstack/react-query"
import { deleteEmployee } from "@/lib/api/employees"
import { employeesQueryKey } from "./useEmployees"

export function useDeleteEmployee() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => deleteEmployee(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: employeesQueryKey })
    },
  })
}
