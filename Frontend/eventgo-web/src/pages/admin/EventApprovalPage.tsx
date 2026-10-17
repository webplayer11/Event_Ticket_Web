import { useState, useEffect, useCallback } from "react";
import { Check, X, Loader2, AlertCircle, RefreshCw } from "lucide-react";
import {
  getEventsForReview,
  approveEvent,
  rejectEvent,
} from "../../services/adminService";
import type { ManagedEventResponse } from "../../types";

function formatDate(dateStr: string): string {
  const d = new Date(dateStr);
  return `${d.getDate().toString().padStart(2, "0")}.${(d.getMonth() + 1).toString().padStart(2, "0")}.${d.getFullYear()}`;
}

function RejectModal({
  event,
  onConfirm,
  onCancel,
}: {
  event: ManagedEventResponse;
  onConfirm: (note: string) => void;
  onCancel: () => void;
}) {
  const [note, setNote] = useState("");
  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="bg-[#151c24] border border-red-900/50 rounded-2xl max-w-md w-full p-6 space-y-4 shadow-2xl">
        <h2 className="text-base font-bold text-white">Từ chối sự kiện</h2>
        <p className="text-xs text-slate-400">
          <span className="text-white font-semibold">{event.title}</span>
          <br />
          Hãy nhập lý do để ban tổ chức có thể chỉnh sửa và gửi lại.
        </p>
        <textarea
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder="Lý do từ chối (bắt buộc)…"
          rows={4}
          className="w-full bg-slate-900 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white resize-none focus:outline-none focus:border-red-500"
        />
        <div className="flex gap-2 justify-end">
          <button
            onClick={onCancel}
            className="px-4 py-2 text-xs rounded-lg bg-slate-800 hover:bg-slate-700 text-white"
          >
            Hủy
          </button>
          <button
            disabled={!note.trim()}
            onClick={() => onConfirm(note.trim())}
            className="px-4 py-2 text-xs rounded-lg bg-red-500/20 hover:bg-red-500/30 text-red-400 border border-red-500/30 font-semibold disabled:opacity-50"
          >
            Xác nhận từ chối
          </button>
        </div>
      </div>
    </div>
  );
}

export const EventApprovalPage = () => {
  const [events, setEvents] = useState<ManagedEventResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [processingId, setProcessingId] = useState<string | null>(null);
  const [rejectTarget, setRejectTarget] = useState<ManagedEventResponse | null>(
    null,
  );
  const [statusFilter, setStatusFilter] = useState("PendingApproval");

  const loadEvents = useCallback(() => {
    setLoading(true);
    setError(null);
    getEventsForReview(statusFilter)
      .then(setEvents)
      .catch((err) => setError(err?.message ?? "Không thể tải dữ liệu."))
      .finally(() => setLoading(false));
  }, [statusFilter]);

  useEffect(() => {
    loadEvents();
  }, [loadEvents]);

  const handleApprove = async (ev: ManagedEventResponse) => {
    setProcessingId(ev.id);
    try {
      await approveEvent(ev.id);
      setEvents((prev) => prev.filter((e) => e.id !== ev.id));
    } catch (err: unknown) {
      alert((err as { message?: string })?.message ?? "Không thể duyệt sự kiện.");
    } finally {
      setProcessingId(null);
    }
  };

  const handleRejectConfirm = async (note: string) => {
    if (!rejectTarget) return;
    const targetId = rejectTarget.id;
    setRejectTarget(null);
    setProcessingId(targetId);
    try {
      await rejectEvent(targetId, note);
      setEvents((prev) => prev.filter((e) => e.id !== targetId));
    } catch (err: unknown) {
      alert((err as { message?: string })?.message ?? "Không thể từ chối sự kiện.");
    } finally {
      setProcessingId(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">Kiểm duyệt sự kiện</h1>
          <p className="text-xs text-slate-400 mt-1">
            Duyệt hoặc từ chối các sự kiện do Ban tổ chức gửi lên
          </p>
        </div>
        <div className="flex items-center gap-2">
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-emerald-500"
          >
            <option value="PendingApproval">Chờ duyệt</option>
            <option value="Published">Đã duyệt</option>
            <option value="Rejected">Bị từ chối</option>
            <option value="Draft">Bản nháp</option>
          </select>
          <button
            onClick={loadEvents}
            className="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white transition-colors"
            title="Làm mới"
          >
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>
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
        <div className="bg-[#0D1217] border border-slate-800 rounded-2xl overflow-hidden">
          {events.length === 0 ? (
            <div className="p-12 text-center text-slate-500 text-xs">
              Không có sự kiện nào trong trạng thái này.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-800 text-slate-400 bg-slate-900/60">
                    <th className="py-3.5 px-4 font-semibold">Tên sự kiện</th>
                    <th className="py-3.5 px-4 font-semibold">Nhà tổ chức</th>
                    <th className="py-3.5 px-4 font-semibold">Người tạo</th>
                    <th className="py-3.5 px-4 font-semibold">Ngày diễn ra</th>
                    <th className="py-3.5 px-4 font-semibold">Địa điểm</th>
                    <th className="py-3.5 px-4 font-semibold">Ghi chú</th>
                    <th className="py-3.5 px-4 font-semibold text-right">
                      Phê duyệt
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 text-slate-300">
                  {events.map((e) => (
                    <tr key={e.id} className="hover:bg-slate-800/30">
                      <td className="py-3.5 px-4 font-bold text-white">
                        {e.title}
                      </td>
                      <td className="py-3.5 px-4">{e.organizationName}</td>
                      <td className="py-3.5 px-4">
                        <p className="font-medium text-white">{e.createdByName}</p>
                        <p className="text-slate-500 text-[10px]">
                          {e.createdByEmail}
                        </p>
                      </td>
                      <td className="py-3.5 px-4">{formatDate(e.startsAt)}</td>
                      <td className="py-3.5 px-4">
                        {e.venueName}, {e.city}
                      </td>
                      <td className="py-3.5 px-4">
                        {e.lastReviewNote ? (
                          <span
                            className="text-amber-400 text-[10px] max-w-[120px] block truncate"
                            title={e.lastReviewNote}
                          >
                            {e.lastReviewNote}
                          </span>
                        ) : (
                          <span className="text-slate-600">—</span>
                        )}
                      </td>
                      <td className="py-3.5 px-4 text-right space-x-2">
                        {statusFilter === "PendingApproval" && (
                          <>
                            <button
                              disabled={processingId === e.id}
                              onClick={() => handleApprove(e)}
                              className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 border border-emerald-500/20 text-xs font-semibold disabled:opacity-50"
                            >
                              {processingId === e.id ? (
                                <Loader2 className="w-3.5 h-3.5 animate-spin" />
                              ) : (
                                <Check className="w-3.5 h-3.5" />
                              )}
                              Duyệt
                            </button>
                            <button
                              disabled={processingId === e.id}
                              onClick={() => setRejectTarget(e)}
                              className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-red-500/10 text-red-400 hover:bg-red-500/20 border border-red-500/20 text-xs font-semibold disabled:opacity-50"
                            >
                              <X className="w-3.5 h-3.5" /> Từ chối
                            </button>
                          </>
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

      {rejectTarget && (
        <RejectModal
          event={rejectTarget}
          onConfirm={handleRejectConfirm}
          onCancel={() => setRejectTarget(null)}
        />
      )}
    </div>
  );
};
