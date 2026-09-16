import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { getGeneralWorkSchedule, updateGeneralWorkSchedule } from "@/lib/api/workSchedules"
import type { WorkSchedulePeriodInput } from "@/lib/api/types"

export const generalWorkScheduleQueryKey = ["general-work-schedule"] as const

export function useGeneralWorkSchedule() {
  return useQuery({
    queryKey: generalWorkScheduleQueryKey,
    queryFn: getGeneralWorkSchedule,
  })
}

export function useUpdateGeneralWorkSchedule() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (periods: WorkSchedulePeriodInput[]) => updateGeneralWorkSchedule(periods),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: generalWorkScheduleQueryKey })
    },
  })
}
