import { apiClient } from "./client";
import type {
  CloseMonthPayload,
  DeleteMonthClosingResult,
  MonthClosingResult,
} from "./types";

export function closeMonth(
  payload: CloseMonthPayload,
): Promise<MonthClosingResult> {
  return apiClient.post<MonthClosingResult>("/month-closings", payload);
}

export function getMonthClosingReport(id: string): Promise<Blob> {
  return apiClient.getBlob(`/month-closings/${id}/report`);
}

export function getMonthClosings(): Promise<MonthClosingResult[]> {
  return apiClient.get<MonthClosingResult[]>("/month-closings");
}

export function deleteMonthClosing(
  id: string,
): Promise<DeleteMonthClosingResult> {
  return apiClient.delete<DeleteMonthClosingResult>(`/month-closings/${id}`);
}
