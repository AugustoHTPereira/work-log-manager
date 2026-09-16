import { getMonthClosings } from "@/lib/api/monthClosings";
import { useQuery } from "@tanstack/react-query";

export const monthClosingsQueryKey = ["monthClosings"] as const;

export function useMonthClosings() {
  return useQuery({
    queryKey: monthClosingsQueryKey,
    queryFn: getMonthClosings,
  });
}
