import { useMutation, useQueryClient } from "@tanstack/react-query"
import { closeMonth } from "@/lib/api/monthClosings"
import type { CloseMonthPayload } from "@/lib/api/types"

export function useCloseMonth() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload: CloseMonthPayload) => closeMonth(payload),
    onSuccess: () => {
      // The closing may have generated work logs for several employees at once, so
      // invalidate every cached employee list/detail query, not just a single key.
      queryClient.invalidateQueries({ predicate: (query) => query.queryKey[0] === "employees" })
    },
  })
}
