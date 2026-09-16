import { useSystemParameters } from "@/features/settings/hooks/useSystemParameters";
import React from "react";
import { Spinner } from "./ui/spinner";

export const SystemParameter = {
  AllowManageClosedWorkLogs: "AllowManageClosedWorkLogs",
};

export type ParamProviderType = {
  allowManageClosedWorkLogs: boolean;
  isLoading: boolean;
  raw: {
    [key: string]: string | undefined;
  };
};

export const ParamContext = React.createContext<ParamProviderType | undefined>(
  undefined,
);

export function ParamProvider({ children }: React.PropsWithChildren<{}>) {
  const { data: systemParameters, isLoading } = useSystemParameters();

  const getParam = (param: string) => {
    return systemParameters?.find((x) => x.param === param)?.value;
  };

  const raw =
    systemParameters?.reduce(
      (acc, curr) => {
        acc[curr.param] = curr.value;
        return acc;
      },
      {} as { [key: string]: string | undefined },
    ) ?? {};

  const allowManageClosedWorkLogsParam =
    getParam(SystemParameter.AllowManageClosedWorkLogs) === "true";

  if (isLoading) {
    return (
      <div className="w-full h-screen flex items-center justify-center gap-2 text-muted-foreground ">
        <Spinner />
        <p className="text-sm animate-pulse">Carregando...</p>
      </div>
    );
  }

  return (
    <ParamContext.Provider
      value={{
        allowManageClosedWorkLogs: allowManageClosedWorkLogsParam,
        isLoading,
        raw,
      }}
    >
      {children}
    </ParamContext.Provider>
  );
}
