import { useMutation, useQueryClient } from "@tanstack/react-query"
import { employeeWorkLogsQueryKey } from "./useEmployeeWorkLogs"
import { deleteWorkLog } from "@/lib/api/workLogs"

export function useDeleteWorkLog(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (workLogId: string) => deleteWorkLog(employeeId, workLogId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: employeeWorkLogsQueryKey(employeeId) })
    },
  })
}
