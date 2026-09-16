import { useMutation, useQueryClient } from "@tanstack/react-query"
import { deleteMonthClosing } from "@/lib/api/monthClosings"
import { monthClosingsQueryKey } from "./useMonthClosings"

export function useDeleteMonthClosing() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => deleteMonthClosing(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: monthClosingsQueryKey })
    },
  })
}
