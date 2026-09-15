import type { ComponentProps } from "react";

export type InfoProps = {
  title: string;
  description?: string;
  children?: React.ReactNode;
  value?: string | number;
};

export function Info({
  title,
  description,
  children,
  value,
  ...props
}: InfoProps & ComponentProps<"div">) {
  return (
    <div {...props}>
      <p className="text-sm text-muted-foreground">{title}</p>
      {value && <p>{value}</p>}
      {children}
      {description && (
        <p className="text-xs text-muted-foreground">{description}</p>
      )}
    </div>
  );
}
