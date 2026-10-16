import { Shield, Users, CalendarCheck, TrendingUp, DollarSign } from "lucide-react";

export const AdminDashboardPage = () => {
  const systemStats = [
    { label: "Tổng giao dịch toàn sàn", value: "1.890.000.000đ", change: "+24.5%", icon: DollarSign },
    { label: "Tổng số User", value: "24.510", change: "+1.200 tháng này", icon: Users },
    { label: "Sự kiện chờ phê duyệt", value: "5 sự kiện", change: "Cần xử lý", icon: CalendarCheck },
    { label: "Tỷ lệ check-in thành công", value: "99.4%", change: "+0.2%", icon: TrendingUp },
  ];

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-bold text-white flex items-center gap-2">
          <Shield className="w-6 h-6 text-red-400" />
          Bảng điều khiển Quản trị viên (Admin)
        </h1>
        <p className="text-xs text-slate-400 mt-1">
          Giám sát hoạt động toàn sàn, kiểm duyệt sự kiện và bảo đảm an toàn hệ thống
        </p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {systemStats.map((item) => {
          const Icon = item.icon;
          return (
            <div key={item.label} className="p-5 rounded-2xl bg-[#0D1217] border border-slate-800 space-y-3">
              <div className="flex justify-between items-center text-slate-400">
                <span className="text-xs">{item.label}</span>
                <div className="w-7 h-7 rounded-lg bg-red-500/10 text-red-400 flex items-center justify-center">
                  <Icon className="w-4 h-4" />
                </div>
              </div>
              <div className="text-xl font-bold text-white">{item.value}</div>
              <div className="text-[11px] text-slate-400">{item.change}</div>
            </div>
          );
        })}
      </div>

      {/* Quick logs & health */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="bg-[#0D1217] border border-slate-800 rounded-2xl p-6 space-y-4">
          <h2 className="text-sm font-bold text-white">Yêu cầu kiểm duyệt mới nhất</h2>
          <div className="space-y-3 text-xs">
            <div className="p-3 rounded-xl bg-slate-900 border border-slate-800 flex justify-between items-center">
              <div>
                <p className="font-semibold text-white">Rock Storm Live 2026</p>
                <p className="text-slate-400 text-[11px]">BTC: LiveNation Vietnam</p>
              </div>
              <span className="px-2.5 py-1 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20 font-medium">
                Chờ duyệt
              </span>
            </div>
            <div className="p-3 rounded-xl bg-slate-900 border border-slate-800 flex justify-between items-center">
              <div>
                <p className="font-semibold text-white">AI Summit Vietnam 2026</p>
                <p className="text-slate-400 text-[11px]">BTC: TechHub Asia</p>
              </div>
              <span className="px-2.5 py-1 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20 font-medium">
                Chờ duyệt
              </span>
            </div>
          </div>
        </div>

        <div className="bg-[#0D1217] border border-slate-800 rounded-2xl p-6 space-y-4">
          <h2 className="text-sm font-bold text-white">Trạng thái hệ thống</h2>
          <div className="space-y-3 text-xs text-slate-300">
            <div className="flex justify-between items-center py-2 border-b border-slate-800/80">
              <span className="text-slate-400">Database (SQL Server):</span>
              <span className="text-emerald-400 font-semibold">Kết nối tốt (1433)</span>
            </div>
            <div className="flex justify-between items-center py-2 border-b border-slate-800/80">
              <span className="text-slate-400">Backend API:</span>
              <span className="text-emerald-400 font-semibold">Đang hoạt động (5248)</span>
            </div>
            <div className="flex justify-between items-center py-2">
              <span className="text-slate-400">Cổng thanh toán:</span>
              <span className="text-emerald-400 font-semibold">VNPAY, MoMo sẵn sàng</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
