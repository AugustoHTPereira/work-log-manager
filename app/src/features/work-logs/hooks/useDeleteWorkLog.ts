import { useMutation, useQueryClient } from "@tanstack/react-query"
import { employeeQueryKey } from "@/features/employees/hooks/useEmployee"
import { deleteWorkLog } from "@/lib/api/workLogs"

export function useDeleteWorkLog(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (workLogId: string) => deleteWorkLog(employeeId, workLogId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: employeeQueryKey(employeeId) })
    },
  })
}
