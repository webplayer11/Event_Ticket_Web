import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { Plus, Users, Ticket, DollarSign, Calendar, Loader2 } from "lucide-react";
import { getMyEvents } from "../../services/eventService";
import type { ManagedEventResponse } from "../../types";

function formatEventDate(startsAt: string): string {
  const d = new Date(startsAt);
  const day = d.getDate().toString().padStart(2, "0");
  const month = (d.getMonth() + 1).toString().padStart(2, "0");
  const year = d.getFullYear();
  return `${day}.${month}.${year}`;
}

function getStatusLabel(status: string): string {
  const map: Record<string, string> = {
    Draft: "Bản nháp",
    PendingApproval: "Chờ duyệt",
    Published: "Đang bán",
    Rejected: "Bị từ chối",
    Cancelled: "Đã hủy",
  };
  return map[status] ?? status;
}

function getStatusClass(status: string): string {
  if (status === "Published")
    return "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20";
  if (status === "PendingApproval")
    return "bg-amber-500/10 text-amber-400 border border-amber-500/20";
  if (status === "Rejected")
    return "bg-red-500/10 text-red-400 border border-red-500/20";
  return "bg-slate-800 text-slate-400";
}

export const OrganizerDashboardPage = () => {
  const [events, setEvents] = useState<ManagedEventResponse[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    getMyEvents()
      .then((data) => {
        if (!cancelled) setEvents(data);
      })
      .catch(() => {
        // Dashboard: fail silently, show 0 stats
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const publishedEvents = events.filter((e) => e.status === "Published");
  const totalTickets = publishedEvents.reduce(
    (sum, ev) =>
      sum +
      ev.ticketTypes.reduce((s, t) => s + (t.totalQuantity - t.availableQuantity), 0),
    0,
  );

  const stats = [
    {
      label: "Sự kiện đang mở",
      value: loading ? "…" : `${publishedEvents.length} sự kiện`,
      change: "Hoạt động",
      icon: Calendar,
    },
    {
      label: "Vé đã bán",
      value: loading ? "…" : `${totalTickets.toLocaleString("vi-VN")} vé`,
      change: "Toàn thời gian",
      icon: Ticket,
    },
    {
      label: "Tổng sự kiện",
      value: loading ? "…" : `${events.length} sự kiện`,
      change: "Bao gồm bản nháp",
      icon: Users,
    },
    {
      label: "Doanh thu ước tính",
      value: "—",
      change: "Tính năng sắp ra mắt",
      icon: DollarSign,
    },
  ];

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">
            Kênh Ban Tổ Chức
          </h1>
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
            <div
              key={s.label}
              className="p-5 rounded-2xl bg-[#141A1E] border border-slate-800 space-y-3"
            >
              <div className="flex justify-between items-center text-slate-400">
                <span className="text-xs">{s.label}</span>
                <div className="w-7 h-7 rounded-lg bg-emerald-500/10 text-emerald-400 flex items-center justify-center">
                  <Icon className="w-4 h-4" />
                </div>
              </div>
              <div className="text-xl font-bold text-white">
                {loading ? (
                  <Loader2 className="w-4 h-4 animate-spin text-emerald-400" />
                ) : (
                  s.value
                )}
              </div>
              <div className="text-[11px] text-emerald-400 font-medium">
                {s.change}
              </div>
            </div>
          );
        })}
      </div>

      {/* Events quick table */}
      <div className="bg-[#141A1E] border border-slate-800 rounded-2xl p-6 space-y-4">
        <div className="flex justify-between items-center">
          <h2 className="text-base font-bold text-white">Sự kiện gần đây</h2>
          <Link
            to="/organizer/events"
            className="text-xs text-emerald-400 hover:underline"
          >
            Xem tất cả
          </Link>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-8 gap-3 text-slate-400">
            <Loader2 className="w-5 h-5 animate-spin text-emerald-400" />
            <span className="text-sm">Đang tải…</span>
          </div>
        ) : events.length === 0 ? (
          <p className="text-xs text-slate-500 text-center py-8">
            Bạn chưa tạo sự kiện nào.
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead>
                <tr className="border-b border-slate-800 text-slate-400">
                  <th className="pb-3 font-semibold">Tên sự kiện</th>
                  <th className="pb-3 font-semibold">Ngày diễn ra</th>
                  <th className="pb-3 font-semibold">Tổ chức</th>
                  <th className="pb-3 font-semibold">Trạng thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/60 text-slate-300">
                {events.slice(0, 5).map((ev) => (
                  <tr key={ev.id}>
                    <td className="py-3 font-semibold text-white">{ev.title}</td>
                    <td className="py-3">{formatEventDate(ev.startsAt)}</td>
                    <td className="py-3 text-slate-400">{ev.organizationName}</td>
                    <td className="py-3">
                      <span
                        className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${getStatusClass(ev.status)}`}
                      >
                        {getStatusLabel(ev.status)}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
