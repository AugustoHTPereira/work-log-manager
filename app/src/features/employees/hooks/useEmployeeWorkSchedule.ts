import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { getEmployeeWorkSchedule, updateEmployeeWorkSchedule } from "@/lib/api/workSchedules"
import type { WorkSchedulePeriodInput } from "@/lib/api/types"

export function employeeWorkScheduleQueryKey(employeeId: string) {
  return ["employees", employeeId, "work-schedule"] as const
}

export function useEmployeeWorkSchedule(employeeId: string | undefined) {
  return useQuery({
    queryKey: employeeWorkScheduleQueryKey(employeeId ?? ""),
    queryFn: () => getEmployeeWorkSchedule(employeeId as string),
    enabled: Boolean(employeeId),
  })
}

export function useUpdateEmployeeWorkSchedule(employeeId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (periods: WorkSchedulePeriodInput[]) => updateEmployeeWorkSchedule(employeeId, periods),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: employeeWorkScheduleQueryKey(employeeId) })
    },
  })
}
