import { useMutation, useQueryClient } from "@tanstack/react-query"
import { employeeWorkLogsQueryKey } from "./useEmployeeWorkLogs"
import { createWorkLog } from "@/lib/api/workLogs"
import type { CreateWorkLogPayload } from "@/lib/api/types"

export function useCreateWorkLog(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload: CreateWorkLogPayload) => createWorkLog(employeeId, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: employeeWorkLogsQueryKey(employeeId) })
    },
  })
}
