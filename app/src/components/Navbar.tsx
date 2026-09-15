import { NavLink } from "react-router-dom"
import { cn } from "cn"

const navLinkClassName = ({ isActive }: { isActive: boolean }) =>
  cn(
    "text-sm font-medium transition-colors hover:text-foreground",
    isActive ? "text-foreground" : "text-muted-foreground",
  )

export function Navbar() {
  return (
    <nav className="fixed top-0 left-0 right-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className="container mx-auto flex h-16 items-center justify-between px-4">
        <NavLink to="/" className="text-lg font-semibold">
          Work Log Manager
        </NavLink>
        <div className="flex items-center gap-6">
          <NavLink to="/" end className={navLinkClassName}>
            Funcionários
          </NavLink>
          <NavLink to="/settings" className={navLinkClassName}>
            Configurações
          </NavLink>
        </div>
      </div>
    </nav>
  )
}
