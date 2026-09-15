import { apiClient } from "./client"
import type { CloseMonthPayload, MonthClosingResult } from "./types"

export function closeMonth(payload: CloseMonthPayload): Promise<MonthClosingResult> {
  return apiClient.post<MonthClosingResult>("/month-closings", payload)
}
