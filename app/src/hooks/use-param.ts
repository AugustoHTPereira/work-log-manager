import { ParamContext } from "@/components/ParamProvider";
import { useContext } from "react";

export function useParam(
  param: string,
  defaultValue: string | boolean | number,
): {
  value: string | boolean | number | undefined;
} {
  const context = useContext(ParamContext);

  if (!context) {
    throw new Error("useParam must be used within a ParamProvider");
  }

  const value = context[param as keyof typeof context];

  return { value: value !== undefined ? value : defaultValue };
}
