import { Outlet } from "react-router-dom"
import { Navbar } from "./Navbar"

export function Layout() {
  return (
    <>
      <Navbar />
      <main className="container mx-auto px-4 pt-20 pb-10">
        <Outlet />
      </main>
    </>
  )
}
