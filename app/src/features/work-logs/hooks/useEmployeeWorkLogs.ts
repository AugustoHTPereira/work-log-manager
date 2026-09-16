import { useQuery } from "@tanstack/react-query"
import { listWorkLogs, type ListWorkLogsFilters } from "@/lib/api/workLogs"

export function employeeWorkLogsQueryKey(employeeId: string, filters?: ListWorkLogsFilters) {
  return filters
    ? (["employees", employeeId, "work-logs", filters] as const)
    : (["employees", employeeId, "work-logs"] as const)
}

export function useEmployeeWorkLogs(employeeId: string | undefined, filters: ListWorkLogsFilters) {
  return useQuery({
    queryKey: employeeWorkLogsQueryKey(employeeId ?? "", filters),
    queryFn: () => listWorkLogs(employeeId as string, filters),
    enabled: Boolean(employeeId),
  })
}
