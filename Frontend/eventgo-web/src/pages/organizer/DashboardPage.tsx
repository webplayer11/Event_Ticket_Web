import { Link } from "react-router-dom";
import { Plus, Users, Ticket, DollarSign, Calendar } from "lucide-react";

export const OrganizerDashboardPage = () => {
  const stats = [
    { label: "Tổng doanh thu", value: "248.500.000đ", change: "+18.2%", icon: DollarSign },
    { label: "Vé đã bán", value: "1.420 vé", change: "+12.4%", icon: Ticket },
    { label: "Lượt người tham dự", value: "1.380", change: "+8.1%", icon: Users },
    { label: "Sự kiện đang mở", value: "3 sự kiện", change: "Hoạt động", icon: Calendar },
  ];

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">Kênh Ban Tổ Chức</h1>
          <p className="text-sm text-slate-400 mt-1">
            Tổng quan hiệu suất bán vé và quản lý các sự kiện của bạn
          </p>
        </div>
        <div className="flex gap-3">
          <Link
            to="/organizer/check-in"
            className="inline-flex items-center gap-2 px-4 py-2 rounded-xl border border-emerald-500/30 bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 text-xs font-semibold transition-colors"
          >
            <span>Soát vé (Check-in)</span>
          </Link>
          <Link
            to="/organizer/create-event"
            className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold transition-colors"
          >
            <Plus className="w-4 h-4" />
            <span>Tạo sự kiện mới</span>
          </Link>
        </div>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {stats.map((s) => {
          const Icon = s.icon;
          return (
            <div key={s.label} className="p-5 rounded-2xl bg-[#141A1E] border border-slate-800 space-y-3">
              <div className="flex justify-between items-center text-slate-400">
                <span className="text-xs">{s.label}</span>
                <div className="w-7 h-7 rounded-lg bg-emerald-500/10 text-emerald-400 flex items-center justify-center">
                  <Icon className="w-4 h-4" />
                </div>
              </div>
              <div className="text-xl font-bold text-white">{s.value}</div>
              <div className="text-[11px] text-emerald-400 font-medium">{s.change} so với tháng trước</div>
            </div>
          );
        })}
      </div>

      {/* Events quick table */}
      <div className="bg-[#141A1E] border border-slate-800 rounded-2xl p-6 space-y-4">
        <div className="flex justify-between items-center">
          <h2 className="text-base font-bold text-white">Sự kiện gần đây</h2>
          <Link to="/organizer/events" className="text-xs text-emerald-400 hover:underline">
            Xem tất cả
          </Link>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400">
                <th className="pb-3 font-semibold">Tên sự kiện</th>
                <th className="pb-3 font-semibold">Ngày diễn ra</th>
                <th className="pb-3 font-semibold">Vé đã bán</th>
                <th className="pb-3 font-semibold">Doanh thu</th>
                <th className="pb-3 font-semibold">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 text-slate-300">
              <tr>
                <td className="py-3 font-semibold text-white">NEON PULSE — Live in Saigon</td>
                <td className="py-3">29.08.2026</td>
                <td className="py-3">850 / 1000</td>
                <td className="py-3 text-emerald-400 font-semibold">168.000.000đ</td>
                <td className="py-3">
                  <span className="px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-[10px] font-bold">
                    Đang bán
                  </span>
                </td>
              </tr>
              <tr>
                <td className="py-3 font-semibold text-white">Afterglow — Indie Music Festival</td>
                <td className="py-3">10.10.2026</td>
                <td className="py-3">420 / 600</td>
                <td className="py-3 text-emerald-400 font-semibold">80.500.000đ</td>
                <td className="py-3">
                  <span className="px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-[10px] font-bold">
                    Đang bán
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
