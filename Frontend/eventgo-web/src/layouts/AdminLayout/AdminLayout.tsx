import { Link, Outlet, useLocation } from "react-router-dom";
import {
  LayoutDashboard,
  CalendarCheck,
  Users,
  CreditCard,
  Shield,
  ArrowLeft,
} from "lucide-react";
import { useAuthStore } from "../../stores/useAuthStore";

export const AdminLayout = () => {
  const location = useLocation();
  const { user } = useAuthStore();

  const navItems = [
    { label: "Dashboard", path: "/admin", icon: LayoutDashboard },
    { label: "Duyệt sự kiện", path: "/admin/events", icon: CalendarCheck },
    { label: "Quản lý User", path: "/admin/users", icon: Users },
    { label: "Giao dịch & Vé", path: "/admin/transactions", icon: CreditCard },
  ];

  return (
    <div className="min-h-screen bg-[#090D10] text-slate-100 flex">
      {/* Sidebar */}
      <aside className="w-64 border-r border-slate-800 bg-[#0D1217] flex flex-col fixed inset-y-0 left-0 z-20">
        <div className="h-16 flex items-center px-6 border-b border-slate-800 gap-3">
          <div className="w-8 h-8 rounded-lg bg-red-500/20 text-red-400 flex items-center justify-center font-bold">
            <Shield className="w-5 h-5" />
          </div>
          <div>
            <h1 className="font-semibold text-sm tracking-wide text-white">EventGO Admin</h1>
            <p className="text-[11px] text-slate-400">System Management</p>
          </div>
        </div>

        <nav className="p-4 space-y-1.5 flex-1">
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = location.pathname === item.path;
            return (
              <Link
                key={item.path}
                to={item.path}
                className={`flex items-center gap-3 px-3.5 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                  isActive
                    ? "bg-red-500/10 text-red-400 border border-red-500/20"
                    : "text-slate-400 hover:text-white hover:bg-slate-800/60"
                }`}
              >
                <Icon className="w-4 h-4" />
                <span>{item.label}</span>
              </Link>
            );
          })}
        </nav>

        <div className="p-4 border-t border-slate-800 space-y-3">
          <div className="flex items-center gap-3 px-2">
            <div className="w-8 h-8 rounded-full bg-slate-800 text-xs flex items-center justify-center font-bold text-slate-300">
              AD
            </div>
            <div className="text-xs truncate">
              <p className="font-medium text-white truncate">{user?.fullName ?? "Admin"}</p>
              <p className="text-slate-400 truncate">{user?.email}</p>
            </div>
          </div>
          <Link
            to="/"
            className="flex items-center gap-2 text-xs text-slate-400 hover:text-white py-2 px-2 rounded hover:bg-slate-800/50 transition-colors"
          >
            <ArrowLeft className="w-3.5 h-3.5" />
            <span>Về trang chủ khách hàng</span>
          </Link>
        </div>
      </aside>

      {/* Main Content */}
      <div className="flex-1 ml-64 min-h-screen flex flex-col">
        <header className="h-16 border-b border-slate-800 bg-[#0D1217]/80 backdrop-blur sticky top-0 z-10 px-8 flex items-center justify-between">
          <div className="text-sm font-medium text-slate-300">
            Cổng quản trị hệ thống
          </div>
          <div className="text-xs px-2.5 py-1 rounded-full bg-red-500/10 text-red-400 border border-red-500/20 font-medium">
            Role: Admin
          </div>
        </header>

        <main className="p-8 flex-1">
          <Outlet />
        </main>
      </div>
    </div>
  );
};
