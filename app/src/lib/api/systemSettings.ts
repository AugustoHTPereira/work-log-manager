import { apiClient } from "./client"
import type { SystemSettings, UpdateSystemSettingsPayload } from "./types"

export function getSystemSettings(): Promise<SystemSettings> {
  return apiClient.get<SystemSettings>("/system-settings")
}

export function updateSystemSettings(payload: UpdateSystemSettingsPayload): Promise<SystemSettings> {
  return apiClient.put<SystemSettings>("/system-settings", payload)
}
