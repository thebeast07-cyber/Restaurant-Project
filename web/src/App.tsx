import { Navigate, Route, Routes } from "react-router-dom";
import { LoginPage } from "./pages/LoginPage";
import { DashboardPage } from "./pages/DashboardPage";
import { ShiftPage } from "./pages/ShiftPage";
import { TablesPage } from "./pages/TablesPage";
import { OrderPage } from "./pages/OrderPage";
import { PurchasingPage } from "./pages/PurchasingPage";
import { SuppliersPage } from "./pages/SuppliersPage";
import { IngredientsPage } from "./pages/IngredientsPage";
import { PurchaseRequestsPage } from "./pages/PurchaseRequestsPage";
import { PurchasesPage } from "./pages/PurchasesPage";
import { StockOpnamePage } from "./pages/StockOpnamePage";
import { MenuPage } from "./pages/MenuPage";
import { CategoriesPage } from "./pages/CategoriesPage";
import { ProductsAdminPage } from "./pages/ProductsAdminPage";
import { ReportsPage } from "./pages/ReportsPage";
import { FinancePage } from "./pages/FinancePage";
import { HRPage } from "./pages/HRPage";
import { EmployeesPage } from "./pages/EmployeesPage";
import { AttendancePage } from "./pages/AttendancePage";
import { KasbonPage } from "./pages/KasbonPage";
import { PayrollPage } from "./pages/PayrollPage";
import { SelfOrderPage } from "./pages/SelfOrderPage";
import { ReceiptPage } from "./pages/ReceiptPage";
import { ProtectedRoute } from "./components/ProtectedRoute";

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      {/* Public, unauthenticated — customer-facing (QR self-order + digital receipt),
          deliberately outside ProtectedRoute: a customer at a table has no login. */}
      <Route path="/s/:tableId" element={<SelfOrderPage />} />
      <Route path="/receipt/:orderId" element={<ReceiptPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/shift" element={<ShiftPage />} />
        <Route path="/tables" element={<TablesPage />} />
        <Route path="/orders/:orderId" element={<OrderPage />} />
        <Route path="/purchasing" element={<PurchasingPage />} />
        <Route path="/purchasing/suppliers" element={<SuppliersPage />} />
        <Route path="/purchasing/ingredients" element={<IngredientsPage />} />
        <Route path="/purchasing/requests" element={<PurchaseRequestsPage />} />
        <Route path="/purchasing/purchases" element={<PurchasesPage />} />
        <Route path="/purchasing/stock-opname" element={<StockOpnamePage />} />
        <Route path="/menu" element={<MenuPage />} />
        <Route path="/menu/categories" element={<CategoriesPage />} />
        <Route path="/menu/products" element={<ProductsAdminPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/finance" element={<FinancePage />} />
        <Route path="/hr" element={<HRPage />} />
        <Route path="/hr/employees" element={<EmployeesPage />} />
        <Route path="/hr/attendance" element={<AttendancePage />} />
        <Route path="/hr/kasbon" element={<KasbonPage />} />
        <Route path="/hr/payroll" element={<PayrollPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

export default App;
