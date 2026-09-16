import { NavLink } from "react-router-dom";
import { cn } from "cn";
import { Cog, Users2 } from "lucide-react";
import { Button } from "./ui/button";
import { Tooltip, TooltipContent, TooltipTrigger } from "./ui/tooltip";

const navLinkClassName = ({ isActive }: { isActive: boolean }) =>
  cn(
    "text-sm font-medium transition-colors hover:text-foreground",
    isActive ? "text-foreground" : "text-muted-foreground",
  );

export function Navbar() {
  return (
    <nav className="fixed top-0 left-0 right-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className="mx-auto flex h-16 items-center justify-between px-4">
        <NavLink to="/" className="text-lg font-semibold">
          Work Log Manager
        </NavLink>
        <div className="flex items-center gap-0.5">
          <Tooltip>
            <TooltipTrigger>
              <Button asChild variant="outline" size="icon">
                <NavLink to="/" end className={navLinkClassName}>
                  <Users2 />
                </NavLink>
              </Button>
            </TooltipTrigger>
            <TooltipContent>Funcionários</TooltipContent>
          </Tooltip>
          <Tooltip>
            <TooltipTrigger>
              <Button asChild variant="outline" size="icon">
                <NavLink to="/settings" className={navLinkClassName}>
                  <Cog />
                </NavLink>
              </Button>
            </TooltipTrigger>
            <TooltipContent>Configurações</TooltipContent>
          </Tooltip>
        </div>
      </div>
    </nav>
  );
}
