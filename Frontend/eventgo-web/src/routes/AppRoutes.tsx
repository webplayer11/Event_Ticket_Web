import { Routes, Route, Navigate } from "react-router-dom";
import { CustomerLayout } from "../layouts/CustomerLayout/CustomerLayout";
import { OrganizerLayout } from "../layouts/OrganizerLayout/OrganizerLayout";
import { AdminLayout } from "../layouts/AdminLayout/AdminLayout";
import { RoleProtectedRoute } from "./RoleProtectedRoute";

// Customer Pages
import { HomePage } from "../pages/customer/HomePage";
import { EventDetailPage } from "../pages/customer/EventDetailPage";
import { CheckoutPage } from "../pages/customer/CheckoutPage";
import { MyTicketsPage } from "../pages/customer/MyTicketsPage";
import { AccountPage } from "../pages/customer/AccountPage";

// Organizer Pages
import { OrganizerDashboardPage } from "../pages/organizer/DashboardPage";
import { EventListPage } from "../pages/organizer/EventListPage";
import { CreateEventPage } from "../pages/organizer/CreateEventPage";
import { CheckInScannerPage } from "../pages/organizer/CheckInScannerPage";

// Admin Pages
import { AdminDashboardPage } from "../pages/admin/DashboardPage";
import { EventApprovalPage } from "../pages/admin/EventApprovalPage";
import { UserManagementPage } from "../pages/admin/UserManagementPage";
import { TransactionPage } from "../pages/admin/TransactionPage";

// Auth Pages
import { LoginPage } from "../pages/auth/LoginPage";
import { RegisterPage } from "../pages/auth/RegisterPage";
import { UnauthorizedPage } from "../pages/auth/UnauthorizedPage";

export const AppRoutes = () => {
  return (
    <Routes>
      {/* 1. PUBLIC & CUSTOMER ROUTES */}
      <Route element={<CustomerLayout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/events/:id" element={<EventDetailPage />} />
        <Route path="/checkout" element={<CheckoutPage />} />
        <Route path="/my-tickets" element={<MyTicketsPage />} />
      </Route>

      {/* Account Route */}
      <Route path="/account" element={<AccountPage />} />

      {/* 2. AUTHENTICATION ROUTES */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/unauthorized" element={<UnauthorizedPage />} />

      {/* 3. ORGANIZER ROUTES (Role: Organizer & Admin) */}
      <Route element={<RoleProtectedRoute allowedRoles={["Organizer", "Admin"]} />}>
        <Route path="/organizer" element={<OrganizerLayout />}>
          <Route index element={<OrganizerDashboardPage />} />
          <Route path="events" element={<EventListPage />} />
          <Route path="create-event" element={<CreateEventPage />} />
          <Route path="check-in" element={<CheckInScannerPage />} />
        </Route>
      </Route>

      {/* 4. ADMIN ROUTES (Role: Admin only) */}
      <Route element={<RoleProtectedRoute allowedRoles={["Admin"]} />}>
        <Route path="/admin" element={<AdminLayout />}>
          <Route index element={<AdminDashboardPage />} />
          <Route path="events" element={<EventApprovalPage />} />
          <Route path="users" element={<UserManagementPage />} />
          <Route path="transactions" element={<TransactionPage />} />
        </Route>
      </Route>

      {/* Catch-all fallback */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};
