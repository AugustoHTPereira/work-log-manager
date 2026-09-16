import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import {
  addSystemParameterValue,
  listSystemParameters,
  removeSystemParameterValue,
  updateSystemParameter,
} from "@/lib/api/systemParameters"
import type { SystemParameterName } from "@/lib/api/types"

export const systemParametersQueryKey = ["system-parameters"] as const

export function useSystemParameters() {
  return useQuery({
    queryKey: systemParametersQueryKey,
    queryFn: () => listSystemParameters(),
  })
}

export function useUpdateSystemParameter() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ param, value }: { param: SystemParameterName; value: string }) =>
      updateSystemParameter(param, value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: systemParametersQueryKey })
    },
  })
}

export function useAddSystemParameterValue() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ param, value }: { param: SystemParameterName; value: string }) =>
      addSystemParameterValue(param, value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: systemParametersQueryKey })
    },
  })
}

export function useRemoveSystemParameterValue() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ param, id }: { param: SystemParameterName; id: string }) =>
      removeSystemParameterValue(param, id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: systemParametersQueryKey })
    },
  })
}
