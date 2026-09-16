import { useMutation, useQueryClient } from "@tanstack/react-query"
import { employeeWorkLogsQueryKey } from "./useEmployeeWorkLogs"
import { ApiError } from "@/lib/api/client"
import { createWorkLog } from "@/lib/api/workLogs"
import type { CreateWorkLogPayload, EmployeeWorkLog } from "@/lib/api/types"

interface BulkCreateWorkLogsInput {
  employeeIds: string[]
  payload: CreateWorkLogPayload
}

export interface BulkCreateWorkLogResult {
  succeeded: { employeeId: string; workLog: EmployeeWorkLog }[]
  failed: { employeeId: string; message: string }[]
}

async function createWorkLogsBulk({
  employeeIds,
  payload,
}: BulkCreateWorkLogsInput): Promise<BulkCreateWorkLogResult> {
  const outcomes = await Promise.allSettled(
    employeeIds.map((employeeId) => createWorkLog(employeeId, payload)),
  )

  const result: BulkCreateWorkLogResult = { succeeded: [], failed: [] }

  outcomes.forEach((outcome, index) => {
    const employeeId = employeeIds[index]

    if (outcome.status === "fulfilled") {
      result.succeeded.push({ employeeId, workLog: outcome.value })
    } else {
      const message =
        outcome.reason instanceof ApiError
          ? outcome.reason.message
          : "Não foi possível criar o worklog."
      result.failed.push({ employeeId, message })
    }
  })

  return result
}

export function useCreateWorkLogsBulk() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createWorkLogsBulk,
    onSuccess: (result) => {
      result.succeeded.forEach(({ employeeId }) => {
        queryClient.invalidateQueries({ queryKey: employeeWorkLogsQueryKey(employeeId) })
      })
    },
  })
}
