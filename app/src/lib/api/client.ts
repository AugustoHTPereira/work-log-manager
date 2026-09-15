/**
 * Pure HTTP access layer: axios wrapper with a base URL and standardized error handling.
 * No React, no caching - only typed functions per endpoint live on top of this (see
 * employees.ts, workLogs.ts, systemSettings.ts).
 */

import axios, { type AxiosRequestConfig } from "axios";

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5244";

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

const httpClient = axios.create({
  baseURL: API_BASE_URL,
  headers: { "Content-Type": "application/json" },
  // Treat every status as "valid" so the response never throws an AxiosError before we
  // get a chance to inspect it below - mirrors the previous `!response.ok` check on fetch.
  validateStatus: () => true,
});

async function request<TResponse>(
  path: string,
  config: AxiosRequestConfig,
): Promise<TResponse> {
  const response = await httpClient.request<TResponse>({ url: path, ...config });

  if (response.status < 200 || response.status >= 300) {
    const body = response.data as { message?: string } | null;
    const message =
      body?.message ??
      `Request to ${path} failed with status ${response.status}.`;
    throw new ApiError(message, response.status);
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  return response.data;
}

export const apiClient = {
  get: <TResponse>(path: string) => request<TResponse>(path, { method: "GET" }),
  post: <TResponse>(path: string, body: unknown) =>
    request<TResponse>(path, { method: "POST", data: body }),
  put: <TResponse>(path: string, body: unknown) =>
    request<TResponse>(path, { method: "PUT", data: body }),
  delete: <TResponse>(path: string) =>
    request<TResponse>(path, { method: "DELETE" }),
};
