import { cn } from "cn";
import type { ComponentProps } from "react";

export function Section({ className, ...props }: ComponentProps<"div">) {
  return <div className={cn("border rounded-md", className)} {...props} />;
}

export function SectionHeader({ className, ...props }: ComponentProps<"div">) {
  return <div className={cn("p-4", className)} {...props} />;
}

export function SectionTitle({ className, ...props }: ComponentProps<"h2">) {
  return <h2 className={cn("font-medium text-lg", className)} {...props} />;
}
