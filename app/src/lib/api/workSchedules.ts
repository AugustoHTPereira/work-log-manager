import { apiClient } from "./client"
import type { WorkSchedulePeriod, WorkSchedulePeriodInput } from "./types"

export function getGeneralWorkSchedule(): Promise<WorkSchedulePeriod[]> {
  return apiClient.get<WorkSchedulePeriod[]>("/work-schedule")
}

export function updateGeneralWorkSchedule(periods: WorkSchedulePeriodInput[]): Promise<WorkSchedulePeriod[]> {
  return apiClient.put<WorkSchedulePeriod[]>("/work-schedule", { periods })
}

export function getEmployeeWorkSchedule(employeeId: string): Promise<WorkSchedulePeriod[]> {
  return apiClient.get<WorkSchedulePeriod[]>(`/employees/${employeeId}/work-schedule`)
}

export function updateEmployeeWorkSchedule(
  employeeId: string,
  periods: WorkSchedulePeriodInput[],
): Promise<WorkSchedulePeriod[]> {
  return apiClient.put<WorkSchedulePeriod[]>(`/employees/${employeeId}/work-schedule`, { periods })
}
