import { useMutation, useQueryClient } from "@tanstack/react-query"
import { employeeQueryKey } from "@/features/employees/hooks/useEmployee"
import { updateWorkLog } from "@/lib/api/workLogs"
import type { UpdateWorkLogPayload } from "@/lib/api/types"

export function useUpdateWorkLog(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ workLogId, payload }: { workLogId: string; payload: UpdateWorkLogPayload }) =>
      updateWorkLog(employeeId, workLogId, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: employeeQueryKey(employeeId) })
    },
  })
}
