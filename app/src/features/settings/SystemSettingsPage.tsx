import { zodResolver } from "@hookform/resolvers/zod"
import { useEffect } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import { Button } from "@/components/ui/button"
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form"
import { Input } from "@/components/ui/input"
import { useSystemSettings, useUpdateSystemSettings } from "./hooks/useSystemSettings"

const settingsFormSchema = z.object({
  defaultDailyWorkHours: z
    .string()
    .min(1, "Horas padrão por dia é obrigatório.")
    .refine((value) => Number(value) > 0, "Horas padrão por dia deve ser maior que zero."),
})

type SettingsFormValues = z.infer<typeof settingsFormSchema>

export function SystemSettingsPage() {
  const { data: settings, isLoading } = useSystemSettings()
  const updateSettings = useUpdateSystemSettings()

  const form = useForm<SettingsFormValues>({
    resolver: zodResolver(settingsFormSchema),
    defaultValues: { defaultDailyWorkHours: "" },
  })

  useEffect(() => {
    if (settings) {
      form.reset({ defaultDailyWorkHours: String(settings.defaultDailyWorkHours) })
    }
  }, [settings, form])

  const onSubmit = async (values: SettingsFormValues) => {
    try {
      await updateSettings.mutateAsync({ defaultDailyWorkHours: Number(values.defaultDailyWorkHours) })
      toast.success("Configurações atualizadas com sucesso.")
    } catch {
      toast.error("Não foi possível atualizar as configurações.")
    }
  }

  if (isLoading) {
    return <p className="text-muted-foreground">Carregando...</p>
  }

  return (
    <div className="max-w-md space-y-6">
      <h1 className="text-2xl font-semibold">Configurações</h1>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <FormField
            control={form.control}
            name="defaultDailyWorkHours"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Horas padrão por dia</FormLabel>
                <FormControl>
                  <Input type="number" step="0.01" min="0" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <Button type="submit" disabled={updateSettings.isPending}>
            Salvar
          </Button>
        </form>
      </Form>
    </div>
  )
}
