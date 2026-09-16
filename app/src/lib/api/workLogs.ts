import { apiClient } from "./client"
import type { CreateWorkLogPayload, EmployeeWorkLog, UpdateWorkLogPayload, WorkLogOrigin, WorkLogType } from "./types"

export interface ListWorkLogsFilters {
  startDate?: string
  endDate?: string
  type?: WorkLogType
  origin?: WorkLogOrigin
}

export function listWorkLogs(employeeId: string, filters: ListWorkLogsFilters = {}): Promise<EmployeeWorkLog[]> {
  return apiClient.get<EmployeeWorkLog[]>(`/employees/${employeeId}/work-logs`, {
    startDate: filters.startDate,
    endDate: filters.endDate,
    type: filters.type,
    origin: filters.origin,
  })
}

export function createWorkLog(employeeId: string, payload: CreateWorkLogPayload): Promise<EmployeeWorkLog> {
  return apiClient.post<EmployeeWorkLog>(`/employees/${employeeId}/work-logs`, payload)
}

export function updateWorkLog(
  employeeId: string,
  workLogId: string,
  payload: UpdateWorkLogPayload,
): Promise<EmployeeWorkLog> {
  return apiClient.put<EmployeeWorkLog>(`/employees/${employeeId}/work-logs/${workLogId}`, payload)
}

export function deleteWorkLog(employeeId: string, workLogId: string): Promise<void> {
  return apiClient.delete<void>(`/employees/${employeeId}/work-logs/${workLogId}`)
}
