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
import { MenuPage } from "./pages/MenuPage";
import { CategoriesPage } from "./pages/CategoriesPage";
import { ProductsAdminPage } from "./pages/ProductsAdminPage";
import { ReportsPage } from "./pages/ReportsPage";
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
        <Route path="/purchasing" element={<PurchasingPage />} />
        <Route path="/purchasing/suppliers" element={<SuppliersPage />} />
        <Route path="/purchasing/ingredients" element={<IngredientsPage />} />
        <Route path="/purchasing/requests" element={<PurchaseRequestsPage />} />
        <Route path="/purchasing/purchases" element={<PurchasesPage />} />
        <Route path="/menu" element={<MenuPage />} />
        <Route path="/menu/categories" element={<CategoriesPage />} />
        <Route path="/menu/products" element={<ProductsAdminPage />} />
        <Route path="/reports" element={<ReportsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

export default App;
