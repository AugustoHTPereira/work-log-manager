import { ParamContext, type ParamProviderType } from "@/components/ParamProvider";
import { useContext } from "react";

type ParamKey = Exclude<keyof ParamProviderType, "raw" | "isLoading">;

export function useParam(
  param: ParamKey,
  defaultValue: string | boolean | number,
): {
  value: string | boolean | number | undefined;
} {
  const context = useContext(ParamContext);

  if (!context) {
    throw new Error("useParam must be used within a ParamProvider");
  }

  const value = context[param];

  return { value: value !== undefined ? value : defaultValue };
}
