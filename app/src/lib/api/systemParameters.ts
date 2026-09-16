import { apiClient } from "./client"
import type { SystemParameter, SystemParameterName } from "./types"

export function listSystemParameters(paramFilter?: SystemParameterName[]): Promise<SystemParameter[]> {
  if (!paramFilter || paramFilter.length === 0) {
    return apiClient.get<SystemParameter[]>("/settings")
  }

  // Built manually instead of relying on axios' default array serialization (which would
  // produce `param[]=`, incompatible with ASP.NET Core's native array model binding).
  const params = new URLSearchParams()
  paramFilter.forEach((value) => params.append("param", value))

  return apiClient.get<SystemParameter[]>("/settings", params)
}

export function getSystemParameter(param: SystemParameterName): Promise<SystemParameter[]> {
  return apiClient.get<SystemParameter[]>(`/settings/${param}`)
}

export function updateSystemParameter(param: SystemParameterName, value: string): Promise<SystemParameter> {
  return apiClient.put<SystemParameter>(`/settings/${param}`, { value })
}

export function addSystemParameterValue(param: SystemParameterName, value: string): Promise<SystemParameter> {
  return apiClient.post<SystemParameter>(`/settings/${param}`, { value })
}

export function removeSystemParameterValue(param: SystemParameterName, id: string): Promise<void> {
  return apiClient.delete<void>(`/settings/${param}/${id}`)
}
