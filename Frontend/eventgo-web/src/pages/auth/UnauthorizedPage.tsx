import { Link } from "react-router-dom";
import { ShieldAlert, ArrowLeft, LogIn } from "lucide-react";
import { useAuthStore } from "../../stores/useAuthStore";

export const UnauthorizedPage = () => {
  const { user } = useAuthStore();

  return (
    <div className="min-h-screen bg-[#0D1117] flex items-center justify-center p-4">
      <div className="max-w-md w-full text-center space-y-6 bg-[#161B22] p-8 rounded-2xl border border-slate-800 shadow-2xl">
        <div className="w-16 h-16 rounded-full bg-red-500/10 text-red-400 mx-auto flex items-center justify-center">
          <ShieldAlert className="w-8 h-8" />
        </div>

        <div className="space-y-2">
          <h1 className="text-2xl font-bold text-white">403 — Không có quyền truy cập</h1>
          <p className="text-xs text-slate-400 leading-relaxed">
            {user ? (
              <>
                Tài khoản của bạn (<span className="text-white font-medium">{user.email}</span>) với vai trò{" "}
                <span className="text-emerald-400 font-bold">{user.role}</span> không có thẩm quyền truy cập vào phân hệ này.
              </>
            ) : (
              "Bạn chưa đăng nhập hoặc không có thẩm quyền truy cập vào phân hệ này."
            )}
          </p>
        </div>

        <div className="flex flex-col sm:flex-row gap-3 pt-2">
          <Link
            to="/"
            className="flex-1 py-2.5 px-4 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold flex items-center justify-center gap-2 transition-colors"
          >
            <ArrowLeft className="w-3.5 h-3.5" />
            <span>Về trang chủ</span>
          </Link>
          <Link
            to="/login"
            className="flex-1 py-2.5 px-4 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold flex items-center justify-center gap-2 transition-colors"
          >
            <LogIn className="w-3.5 h-3.5" />
            <span>Đổi tài khoản</span>
          </Link>
        </div>
      </div>
    </div>
  );
};
