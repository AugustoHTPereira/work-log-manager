import { Outlet } from "react-router-dom";
import { Navbar } from "./Navbar";
import { TooltipProvider } from "@/components/ui/tooltip";

export function Layout() {
  return (
    <>
      <TooltipProvider>
        <Navbar />
        <main className="container mx-auto px-4 pt-20 pb-10">
          <Outlet />
        </main>
      </TooltipProvider>
    </>
  );
}
