import { Outlet } from "react-router-dom";
import { Navbar } from "./Navbar";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ParamProvider } from "./ParamProvider";

export function Layout() {
  return (
    <ParamProvider>
      <TooltipProvider>
        <Navbar />
        <main className="w-full max-w-6xl mx-auto px-4 pt-20 pb-10">
          <Outlet />
        </main>
      </TooltipProvider>
    </ParamProvider>
  );
}
