import { format } from "date-fns";
import { ptBR } from "date-fns/locale";

export function formatDate(date: Date | string, formatString: string): string {
  return format(date, formatString, { locale: ptBR });
}
