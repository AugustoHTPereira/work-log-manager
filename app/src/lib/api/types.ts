/**
 * Shared types mirroring the back-end API DTOs (WorkLogManager.Api.Dtos.*).
 */

export type WorkLogType = "Absence" | "Overtime" | "RegularAttendance" | "Break"

export type WorkLogOrigin = "Automatic" | "Manual"

export type DayOfWeek =
  | "Sunday"
  | "Monday"
  | "Tuesday"
  | "Wednesday"
  | "Thursday"
  | "Friday"
  | "Saturday"

export interface WorkSchedulePeriod {
  id: string
  dayOfWeek: DayOfWeek
  startTime: string
  endTime: string
}

export interface WorkSchedulePeriodInput {
  dayOfWeek: DayOfWeek
  startTime: string
  endTime: string
}

export interface EmployeeSummary {
  id: string
  name: string
  role: string
  hireDate: string
}

export interface EmployeeWorkLog {
  id: string
  employeeId: string
  type: WorkLogType
  startDate: string
  endDate: string
  durationSeconds: number
  monthClosingId: string | null
  note: string | null
  origin: WorkLogOrigin
}

export interface EmployeeDetail {
  id: string
  name: string
  role: string
  hireDate: string
}

export interface CreateEmployeePayload {
  name: string
  role: string
  hireDate: string
}

export type UpdateEmployeePayload = CreateEmployeePayload

export interface CreateWorkLogPayload {
  type: WorkLogType
  startDate: string
  endDate: string
  note: string | null
}

export type UpdateWorkLogPayload = CreateWorkLogPayload

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

export interface DeleteMonthClosingResult {
  monthClosingId: string
  month: number
  year: number
  deletedAutomaticWorkLogsCount: number
  unlinkedManualWorkLogsCount: number
}

export type SystemParameterName = "AutoWorkLogTypes" | "AllowManageClosedWorkLogs"

export type SystemParameterValueType = "String" | "Int" | "Bool" | "Array"

export interface SystemParameter {
  id: string
  param: SystemParameterName
  value: string
  valueType: SystemParameterValueType
  createdAtUtc: string
  updatedAtUtc: string
}
