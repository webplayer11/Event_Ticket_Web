import { useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  User,
  Mail,
  Phone,
  Shield,
  Ticket,
  Briefcase,
  Sparkles,
  LogOut,
  CheckCircle2,
  ArrowRight,
  ShieldCheck,
  Calendar,
} from "lucide-react";
import { useAuthStore } from "../../stores/useAuthStore";

export const AccountPage = () => {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();

  useEffect(() => {
    if (!user) {
      navigate("/login");
    }
  }, [user, navigate]);

  if (!user) {
    return null;
  }

  const handleLogout = () => {
    logout();
    navigate("/");
  };

  const roleLabel =
    user.role === "Admin"
      ? "Quản trị viên (Platform Admin)"
      : user.role === "Organizer"
      ? "Ban tổ chức (Organizer)"
      : "Khách hàng (Customer)";

  return (
    <div className="max-w-5xl mx-auto px-4 py-8">
      {/* Page Header */}
      <div className="mb-8">
        <h1 className="text-2xl sm:text-3xl font-bold text-white flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400">
            <User className="w-5 h-5" />
          </div>
          <span>Tài khoản của tôi</span>
        </h1>
        <p className="text-sm text-slate-400 mt-1.5 ml-13">
          Quản lý thông tin cá nhân, quyền hạn và hoạt động sự kiện của bạn trên EventGO.
        </p>
      </div>

      {/* Profile Overview Banner */}
      <div className="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 sm:p-8 backdrop-blur shadow-xl flex flex-col sm:flex-row items-start sm:items-center justify-between gap-6">
        <div className="flex items-center gap-5">
          <div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-emerald-500/20 to-teal-500/10 border-2 border-emerald-500/30 flex items-center justify-center text-3xl font-extrabold text-emerald-400 shadow-inner shrink-0">
            {user.fullName ? user.fullName.charAt(0).toUpperCase() : "U"}
          </div>

          <div className="space-y-1.5">
            <div className="flex flex-wrap items-center gap-2.5">
              <h2 className="text-xl sm:text-2xl font-bold text-white">{user.fullName}</h2>
              <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                <CheckCircle2 className="w-3 h-3" />
                Đang hoạt động
              </span>
            </div>
            <p className="text-sm text-slate-400 flex items-center gap-1.5">
              <Mail className="w-3.5 h-3.5 text-slate-500" />
              <span>{user.email}</span>
            </p>
            <div className="pt-1">
              <span className="inline-block text-[11px] font-bold tracking-wider uppercase px-2 py-0.5 rounded bg-slate-800 text-slate-300 border border-slate-700/60">
                {user.role}
              </span>
            </div>
          </div>
        </div>

        <button
          onClick={handleLogout}
          className="inline-flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-medium text-red-400 border border-red-500/30 bg-red-500/5 hover:bg-red-500/10 hover:border-red-500/50 transition-colors self-end sm:self-center"
        >
          <LogOut className="w-4 h-4" />
          <span>Đăng xuất</span>
        </button>
      </div>

      {/* Main Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mt-6">
        {/* Left Column (2 cols) */}
        <div className="lg:col-span-2 space-y-6">
          {/* Personal Information */}
          <section className="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 backdrop-blur">
            <h3 className="text-base font-bold text-white flex items-center gap-2 mb-4">
              <User className="w-4 h-4 text-emerald-400" />
              <span>Thông tin cá nhân</span>
            </h3>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="bg-slate-950/40 border border-slate-800/80 rounded-xl p-3.5">
                <p className="text-xs text-slate-400 font-medium">Họ và tên</p>
                <p className="text-sm font-semibold text-white mt-1">{user.fullName}</p>
              </div>

              <div className="bg-slate-950/40 border border-slate-800/80 rounded-xl p-3.5">
                <p className="text-xs text-slate-400 font-medium">Email đăng nhập</p>
                <p className="text-sm font-semibold text-white mt-1 truncate">{user.email}</p>
              </div>

              <div className="bg-slate-950/40 border border-slate-800/80 rounded-xl p-3.5">
                <p className="text-xs text-slate-400 font-medium">Số điện thoại</p>
                <p className="text-sm font-semibold text-slate-300 mt-1">
                  {user.phoneNumber || "Chưa cập nhật"}
                </p>
              </div>

              <div className="bg-slate-950/40 border border-slate-800/80 rounded-xl p-3.5">
                <p className="text-xs text-slate-400 font-medium">Vai trò hệ thống</p>
                <p className="text-sm font-semibold text-emerald-400 mt-1">{roleLabel}</p>
              </div>
            </div>
          </section>

          {/* Quick Actions / Shortcuts */}
          <section className="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 backdrop-blur">
            <h3 className="text-base font-bold text-white flex items-center gap-2 mb-4">
              <Sparkles className="w-4 h-4 text-emerald-400" />
              <span>Lối tắt & Dịch vụ của bạn</span>
            </h3>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
              <Link
                to="/my-tickets"
                className="group p-4 rounded-xl bg-slate-950/40 border border-slate-800/80 hover:border-emerald-500/40 hover:bg-slate-800/40 transition-all flex flex-col justify-between"
              >
                <div>
                  <div className="w-9 h-9 rounded-lg bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400 mb-3 group-hover:scale-105 transition-transform">
                    <Ticket className="w-4 h-4" />
                  </div>
                  <h4 className="font-semibold text-white text-sm group-hover:text-emerald-400 transition-colors">
                    Vé của tôi
                  </h4>
                  <p className="text-xs text-slate-400 mt-1 leading-relaxed">
                    Kiểm tra danh sách vé đã mua, xuất trình mã QR check-in sự kiện.
                  </p>
                </div>
                <div className="flex items-center gap-1 text-xs font-medium text-emerald-400 mt-4">
                  <span>Xem vé ngay</span>
                  <ArrowRight className="w-3.5 h-3.5 group-hover:translate-x-1 transition-transform" />
                </div>
              </Link>

              <Link
                to="/organizer"
                className="group p-4 rounded-xl bg-slate-950/40 border border-slate-800/80 hover:border-emerald-500/40 hover:bg-slate-800/40 transition-all flex flex-col justify-between"
              >
                <div>
                  <div className="w-9 h-9 rounded-lg bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400 mb-3 group-hover:scale-105 transition-transform">
                    <Briefcase className="w-4 h-4" />
                  </div>
                  <h4 className="font-semibold text-white text-sm group-hover:text-emerald-400 transition-colors">
                    Kênh Ban tổ chức
                  </h4>
                  <p className="text-xs text-slate-400 mt-1 leading-relaxed">
                    Tạo sự kiện mới, thiết lập loại vé và quản lý lượt quét check-in.
                  </p>
                </div>
                <div className="flex items-center gap-1 text-xs font-medium text-emerald-400 mt-4">
                  <span>Truy cập kênh BTC</span>
                  <ArrowRight className="w-3.5 h-3.5 group-hover:translate-x-1 transition-transform" />
                </div>
              </Link>

              <Link
                to="/"
                className="group p-4 rounded-xl bg-slate-950/40 border border-slate-800/80 hover:border-emerald-500/40 hover:bg-slate-800/40 transition-all flex flex-col justify-between"
              >
                <div>
                  <div className="w-9 h-9 rounded-lg bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400 mb-3 group-hover:scale-105 transition-transform">
                    <Calendar className="w-4 h-4" />
                  </div>
                  <h4 className="font-semibold text-white text-sm group-hover:text-emerald-400 transition-colors">
                    Khám phá sự kiện
                  </h4>
                  <p className="text-xs text-slate-400 mt-1 leading-relaxed">
                    Khám phá các đêm nhạc, hội thảo, thể thao hot nhất trên EventGO.
                  </p>
                </div>
                <div className="flex items-center gap-1 text-xs font-medium text-emerald-400 mt-4">
                  <span>Tìm sự kiện</span>
                  <ArrowRight className="w-3.5 h-3.5 group-hover:translate-x-1 transition-transform" />
                </div>
              </Link>

              {user.role === "Admin" && (
                <Link
                  to="/admin"
                  className="group p-4 rounded-xl bg-slate-950/40 border border-slate-800/80 hover:border-red-500/40 hover:bg-slate-800/40 transition-all flex flex-col justify-between"
                >
                  <div>
                    <div className="w-9 h-9 rounded-lg bg-red-500/10 border border-red-500/20 flex items-center justify-center text-red-400 mb-3 group-hover:scale-105 transition-transform">
                      <Shield className="w-4 h-4" />
                    </div>
                    <h4 className="font-semibold text-white text-sm group-hover:text-red-400 transition-colors">
                      Cổng quản trị Admin
                    </h4>
                    <p className="text-xs text-slate-400 mt-1 leading-relaxed">
                      Phê duyệt sự kiện, quản lý người dùng và giám sát giao dịch.
                    </p>
                  </div>
                  <div className="flex items-center gap-1 text-xs font-medium text-red-400 mt-4">
                    <span>Vào cổng Admin</span>
                    <ArrowRight className="w-3.5 h-3.5 group-hover:translate-x-1 transition-transform" />
                  </div>
                </Link>
              )}
            </div>
          </section>
        </div>

        {/* Right Column (1 col) */}
        <div className="space-y-6">
          {/* Membership & Status Card */}
          <section className="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 backdrop-blur">
            <div className="flex items-center gap-2.5 mb-3">
              <ShieldCheck className="w-5 h-5 text-emerald-400" />
              <h3 className="text-base font-bold text-white">Thành viên EventGO</h3>
            </div>
            <p className="text-xs text-slate-400 leading-relaxed">
              Tài khoản của bạn đã được xác thực an toàn. Bạn có thể đặt vé trực tuyến, tải vé điện tử về điện thoại và nhận vé ngay tức thì qua email.
            </p>

            <div className="mt-5 pt-5 border-t border-slate-800/80 space-y-3">
              <div className="flex items-center justify-between text-xs">
                <span className="text-slate-400">Trạng thái bảo mật</span>
                <span className="text-emerald-400 font-semibold flex items-center gap-1">
                  <CheckCircle2 className="w-3.5 h-3.5" />
                  JWT Secured
                </span>
              </div>
              <div className="flex items-center justify-between text-xs">
                <span className="text-slate-400">Thời hạn phiên</span>
                <span className="text-slate-300 font-medium">Tự động làm mới</span>
              </div>
            </div>
          </section>

          {/* Support / Quick Help Card */}
          <section className="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 backdrop-blur">
            <h3 className="text-base font-bold text-white mb-2">Hỗ trợ khách hàng</h3>
            <p className="text-xs text-slate-400 leading-relaxed mb-4">
              Cần trợ giúp về đơn hàng, đổi vé hoặc có thắc mắc kỹ thuật? Đội ngũ CSKH EventGO luôn sẵn sàng hỗ trợ bạn.
            </p>
            <div className="bg-slate-950/40 border border-slate-800/80 rounded-xl p-3 text-xs space-y-1">
              <p className="text-slate-400">Email hỗ trợ:</p>
              <p className="font-semibold text-emerald-400">support@eventgo.vn</p>
            </div>
          </section>
        </div>
      </div>
    </div>
  );
};
