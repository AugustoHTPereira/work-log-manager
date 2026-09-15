import { apiClient } from "./client"
import type { CreateEmployeePayload, EmployeeDetail, EmployeeSummary, UpdateEmployeePayload } from "./types"

export function getEmployees(): Promise<EmployeeSummary[]> {
  return apiClient.get<EmployeeSummary[]>("/employees")
}

export function getEmployee(id: string): Promise<EmployeeDetail> {
  return apiClient.get<EmployeeDetail>(`/employees/${id}`)
}

export function createEmployee(payload: CreateEmployeePayload): Promise<EmployeeSummary> {
  return apiClient.post<EmployeeSummary>("/employees", payload)
}

export function updateEmployee(id: string, payload: UpdateEmployeePayload): Promise<EmployeeSummary> {
  return apiClient.put<EmployeeSummary>(`/employees/${id}`, payload)
}

export function deleteEmployee(id: string): Promise<void> {
  return apiClient.delete<void>(`/employees/${id}`)
}
