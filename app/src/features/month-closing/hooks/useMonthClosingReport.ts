import { useMutation } from "@tanstack/react-query"
import { getMonthClosingReport } from "@/lib/api/monthClosings"

interface DownloadReportParams {
  id: string
  month: number
  year: number
}

function pad2(value: number): string {
  return value.toString().padStart(2, "0")
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement("a")
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}

export function useMonthClosingReport() {
  return useMutation({
    mutationFn: (params: DownloadReportParams) => getMonthClosingReport(params.id),
    onSuccess: (blob, params) => {
      downloadBlob(blob, `fechamento-${pad2(params.month)}-${params.year}.pdf`)
    },
  })
}
