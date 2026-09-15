/**
 * Shared types mirroring the back-end API DTOs (WorkLogManager.Api.Dtos.*).
 */

export type WorkLogType = "Absence" | "Overtime" | "RegularAttendance" | "Break"

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

export interface CloseMonthPayload {
  month: number
  year: number
}

/**
 * Counts business days processed (generated vs. skipped), not raw work log rows - same
 * semantics as the back-end `EmployeeWorkLogGenerationSummary`.
 */
export interface EmployeeWorkLogGenerationSummary {
  employeeId: string
  employeeName: string
  generatedCount: number
  skippedCount: number
}

export interface MonthClosingResult {
  id: string
  month: number
  year: number
  createdAtUtc: string
  summaries: EmployeeWorkLogGenerationSummary[]
}
