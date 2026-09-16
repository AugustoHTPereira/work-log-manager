import { Route, Routes } from "react-router-dom";
import { Layout } from "@/components/Layout";
import { EmployeeDetailPage } from "@/features/employees/EmployeeDetailPage";
import { EmployeeListPage } from "@/features/employees/EmployeeListPage";
import { SystemSettingsPage } from "@/features/settings/SystemSettingsPage";

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<EmployeeListPage />} />
        <Route path="/employees/:id" element={<EmployeeDetailPage />} />
        <Route path="/settings" element={<SystemSettingsPage />} />
      </Route>
    </Routes>
  );
}

export default App;
