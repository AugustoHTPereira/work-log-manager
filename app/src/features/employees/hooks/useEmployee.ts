import { useQuery } from "@tanstack/react-query"
import { getEmployee } from "@/lib/api/employees"

export function employeeQueryKey(employeeId: string) {
  return ["employees", employeeId] as const
}

export function useEmployee(employeeId: string | undefined) {
  return useQuery({
    queryKey: employeeQueryKey(employeeId ?? ""),
    queryFn: () => getEmployee(employeeId as string),
    enabled: Boolean(employeeId),
  })
}
