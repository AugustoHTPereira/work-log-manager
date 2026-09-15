import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { getSystemSettings, updateSystemSettings } from "@/lib/api/systemSettings"
import type { UpdateSystemSettingsPayload } from "@/lib/api/types"

export const systemSettingsQueryKey = ["system-settings"] as const

export function useSystemSettings() {
  return useQuery({
    queryKey: systemSettingsQueryKey,
    queryFn: getSystemSettings,
  })
}

export function useUpdateSystemSettings() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload: UpdateSystemSettingsPayload) => updateSystemSettings(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: systemSettingsQueryKey })
    },
  })
}
