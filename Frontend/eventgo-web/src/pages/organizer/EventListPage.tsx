import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { Plus, Edit, QrCode, Loader2, AlertCircle } from "lucide-react";
import { getMyEvents } from "../../services/eventService";
import type { ManagedEventResponse } from "../../types";

function formatEventDate(startsAt: string): string {
  const d = new Date(startsAt);
  const hours = d.getHours().toString().padStart(2, "0");
  const minutes = d.getMinutes().toString().padStart(2, "0");
  const day = d.getDate().toString().padStart(2, "0");
  const month = (d.getMonth() + 1).toString().padStart(2, "0");
  const year = d.getFullYear();
  return `${hours}:${minutes} · ${day}.${month}.${year}`;
}

function getStatusLabel(status: string): string {
  const map: Record<string, string> = {
    Draft: "Bản nháp",
    PendingApproval: "Chờ duyệt",
    Published: "Đang mở bán",
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

export const EventListPage = () => {
  const [events, setEvents] = useState<ManagedEventResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getMyEvents()
      .then((data) => {
        if (cancelled) return;
        setEvents(data);
        setError(null);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err?.message ?? "Không thể tải danh sách sự kiện.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-2xl font-bold text-white">Danh sách sự kiện</h1>
          <p className="text-xs text-slate-400 mt-1">
            Quản lý các sự kiện bạn đã tạo và cấu hình
          </p>
        </div>
        <Link
          to="/organizer/create-event"
          className="inline-flex items-center gap-2 px-3.5 py-2 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold transition-colors"
        >
          <Plus className="w-4 h-4" />
          <span>Tạo sự kiện mới</span>
        </Link>
      </div>

      {loading && (
        <div className="flex items-center justify-center py-12 gap-3 text-slate-400">
          <Loader2 className="w-5 h-5 animate-spin text-emerald-400" />
          <span className="text-sm">Đang tải…</span>
        </div>
      )}

      {error && !loading && (
        <div className="flex items-center justify-center py-12 gap-3 text-red-400">
          <AlertCircle className="w-5 h-5" />
          <span className="text-sm">{error}</span>
        </div>
      )}

      {!loading && !error && (
        <div className="bg-[#141A1E] border border-slate-800 rounded-2xl overflow-hidden">
          {events.length === 0 ? (
            <div className="p-12 text-center text-slate-500 text-xs">
              Bạn chưa tạo sự kiện nào.
              <br />
              <Link
                to="/organizer/create-event"
                className="text-emerald-400 underline mt-2 inline-block"
              >
                Tạo sự kiện đầu tiên →
              </Link>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-800 text-slate-400 bg-slate-900/40">
                    <th className="py-3.5 px-4 font-semibold">Tên sự kiện</th>
                    <th className="py-3.5 px-4 font-semibold">Thời gian</th>
                    <th className="py-3.5 px-4 font-semibold">Địa điểm</th>
                    <th className="py-3.5 px-4 font-semibold">Tổ chức</th>
                    <th className="py-3.5 px-4 font-semibold">Trạng thái</th>
                    <th className="py-3.5 px-4 font-semibold text-right">
                      Thao tác
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 text-slate-300">
                  {events.map((ev) => (
                    <tr key={ev.id} className="hover:bg-slate-800/20">
                      <td className="py-3.5 px-4 font-bold text-white">
                        {ev.title}
                      </td>
                      <td className="py-3.5 px-4">
                        {formatEventDate(ev.startsAt)}
                      </td>
                      <td className="py-3.5 px-4">
                        {ev.venueName}, {ev.city}
                      </td>
                      <td className="py-3.5 px-4 text-slate-400">
                        {ev.organizationName}
                      </td>
                      <td className="py-3.5 px-4">
                        <span
                          className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${getStatusClass(ev.status)}`}
                        >
                          {getStatusLabel(ev.status)}
                        </span>
                        {ev.lastReviewNote && ev.status === "Rejected" && (
                          <p className="text-[10px] text-red-400 mt-1 max-w-[200px] truncate" title={ev.lastReviewNote}>
                            {ev.lastReviewNote}
                          </p>
                        )}
                      </td>
                      <td className="py-3.5 px-4 text-right space-x-2">
                        {ev.status === "Published" && (
                          <Link
                            to="/organizer/check-in"
                            className="p-1.5 inline-block text-slate-400 hover:text-emerald-400 rounded hover:bg-slate-800"
                            title="Soát vé QR"
                          >
                            <QrCode className="w-4 h-4" />
                          </Link>
                        )}
                        {(ev.status === "Draft" ||
                          ev.status === "PendingApproval" ||
                          ev.status === "Rejected") && (
                          <Link
                            to={`/organizer/create-event?editId=${ev.id}`}
                            className="p-1.5 inline-block text-slate-400 hover:text-white rounded hover:bg-slate-800"
                            title="Chỉnh sửa"
                          >
                            <Edit className="w-4 h-4" />
                          </Link>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
