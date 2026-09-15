import { useEffect, useState } from "react"
import { toast } from "sonner"
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { ApiError } from "@/lib/api/client"
import type { MonthClosingResult } from "@/lib/api/types"
import { useCloseMonth } from "./hooks/useCloseMonth"

interface MonthCloseModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

function pad2(value: number): string {
  return value.toString().padStart(2, "0")
}

/** The month civil right before the current one, formatted as "YYYY-MM". */
function previousMonthValue(): string {
  const now = new Date()
  const previousMonthDate = new Date(now.getFullYear(), now.getMonth() - 1, 1)
  return `${previousMonthDate.getFullYear()}-${pad2(previousMonthDate.getMonth() + 1)}`
}

function parseMonthValue(value: string): { month: number; year: number } | null {
  const match = /^(\d{4})-(\d{2})$/.exec(value)
  if (!match) {
    return null
  }

  return { year: Number(match[1]), month: Number(match[2]) }
}

export function MonthCloseModal({ open, onOpenChange }: MonthCloseModalProps) {
  const closeMonth = useCloseMonth()

  const [step, setStep] = useState<"select" | "summary">("select")
  const [monthValue, setMonthValue] = useState("")
  const [maxMonthValue, setMaxMonthValue] = useState("")
  const [result, setResult] = useState<MonthClosingResult | null>(null)

  useEffect(() => {
    if (!open) {
      return
    }

    const previous = previousMonthValue()
    setMonthValue(previous)
    setMaxMonthValue(previous)
    setStep("select")
    setResult(null)
  }, [open])

  const handleProceed = async () => {
    const parsed = parseMonthValue(monthValue)
    if (!parsed) {
      return
    }

    try {
      const closingResult = await closeMonth.mutateAsync(parsed)
      setResult(closingResult)
      setStep("summary")
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Não foi possível fechar o mês."
      toast.error(message)
    }
  }

  const handleClose = () => {
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        {step === "select" && (
          <>
            <DialogHeader>
              <DialogTitle>Fechar mês</DialogTitle>
            </DialogHeader>

            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Mês</Label>
                <Input
                  type="month"
                  value={monthValue}
                  max={maxMonthValue}
                  onChange={(event) => setMonthValue(event.target.value)}
                />
              </div>
            </div>

            <DialogFooter>
              <Button
                onClick={handleProceed}
                disabled={closeMonth.isPending || !parseMonthValue(monthValue)}
              >
                Prosseguir
              </Button>
            </DialogFooter>
          </>
        )}

        {step === "summary" && result && (
          <>
            <DialogHeader>
              <DialogTitle>
                Fechamento de {pad2(result.month)}/{result.year} concluído
              </DialogTitle>
            </DialogHeader>

            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Funcionário</TableHead>
                  <TableHead>Dias gerados</TableHead>
                  <TableHead>Dias pulados</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {result.summaries.map((summary) => (
                  <TableRow key={summary.employeeId}>
                    <TableCell>{summary.employeeName}</TableCell>
                    <TableCell>{summary.generatedCount}</TableCell>
                    <TableCell>{summary.skippedCount}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            <DialogFooter>
              <Button onClick={handleClose}>Fechar</Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
