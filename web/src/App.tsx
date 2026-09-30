import { Navigate, Route, Routes } from "react-router-dom";
import { LoginPage } from "./pages/LoginPage";
import { DashboardPage } from "./pages/DashboardPage";
import { ShiftPage } from "./pages/ShiftPage";
import { TablesPage } from "./pages/TablesPage";
import { OrderPage } from "./pages/OrderPage";
import { ProtectedRoute } from "./components/ProtectedRoute";

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/shift" element={<ShiftPage />} />
        <Route path="/tables" element={<TablesPage />} />
        <Route path="/orders/:orderId" element={<OrderPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

export default App;
