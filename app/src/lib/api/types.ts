/**
 * Shared types mirroring the back-end API DTOs (WorkLogManager.Api.Dtos.*).
 */

export type WorkLogType = "Absence" | "Overtime"

export interface EmployeeSummary {
  id: string
  name: string
  role: string
  hireDate: string
  dailyWorkHours: number | null
}

export interface EmployeeWorkLog {
  id: string
  employeeId: string
  type: WorkLogType
  startDate: string
  endDate: string
  durationSeconds: number
}

export interface EmployeeDetail {
  id: string
  name: string
  role: string
  hireDate: string
  dailyWorkHours: number | null
  effectiveDailyWorkHours: number
  workLogs: EmployeeWorkLog[]
}

export interface CreateEmployeePayload {
  name: string
  role: string
  hireDate: string
  dailyWorkHours: number | null
}

export type UpdateEmployeePayload = CreateEmployeePayload

export interface CreateWorkLogPayload {
  type: WorkLogType
  startDate: string
  endDate: string
}

export type UpdateWorkLogPayload = CreateWorkLogPayload

export interface SystemSettings {
  defaultDailyWorkHours: number
}

export interface UpdateSystemSettingsPayload {
  defaultDailyWorkHours: number
}
