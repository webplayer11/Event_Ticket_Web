import { useState, useEffect } from "react";
import { QRCodeSVG } from "qrcode.react";
import {
  Ticket,
  Calendar,
  MapPin,
  CheckCircle,
  Clock,
  Loader2,
  AlertCircle,
} from "lucide-react";
import { getMyTickets } from "../../services/ticketService";
import type { TicketResponse } from "../../types";

function formatEventDate(startsAt: string): string {
  const d = new Date(startsAt);
  const hours = d.getHours().toString().padStart(2, "0");
  const minutes = d.getMinutes().toString().padStart(2, "0");
  const day = d.getDate().toString().padStart(2, "0");
  const month = (d.getMonth() + 1).toString().padStart(2, "0");
  const year = d.getFullYear();
  return `${hours}:${minutes} · ${day}.${month}.${year}`;
}

function mapStatus(status: string): "Valid" | "Used" {
  // Backend status values: "Valid", "CheckedIn", "Cancelled"
  if (status === "CheckedIn") return "Used";
  if (status === "Valid") return "Valid";
  return "Used"; // Cancelled → show as used/inactive
}

export const MyTicketsPage = () => {
  const [tickets, setTickets] = useState<TicketResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<"ALL" | "VALID" | "USED">("ALL");
  const [selectedTicket, setSelectedTicket] = useState<TicketResponse | null>(
    null,
  );

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getMyTickets()
      .then((data) => {
        if (cancelled) return;
        setTickets(data);
        setError(null);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err?.message ?? "Không thể tải danh sách vé.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const filteredTickets = tickets.filter((t) => {
    const mapped = mapStatus(t.status);
    if (filter === "VALID") return mapped === "Valid";
    if (filter === "USED") return mapped === "Used";
    return true;
  });

  const validCount = tickets.filter((t) => mapStatus(t.status) === "Valid").length;
  const usedCount = tickets.filter((t) => mapStatus(t.status) === "Used").length;

  if (loading) {
    return (
      <div className="max-w-5xl mx-auto px-4 py-16 flex flex-col items-center justify-center gap-4">
        <Loader2 className="w-8 h-8 text-emerald-400 animate-spin" />
        <p className="text-slate-400 text-sm">Đang tải vé của bạn…</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-5xl mx-auto px-4 py-16 flex flex-col items-center justify-center gap-4">
        <AlertCircle className="w-8 h-8 text-red-400" />
        <p className="text-red-400 text-sm">{error}</p>
      </div>
    );
  }

  return (
    <div className="max-w-5xl mx-auto px-4 py-8">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 mb-8">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2.5">
            <Ticket className="w-7 h-7 text-emerald-400" />
            Vé của tôi (My Tickets)
          </h1>
          <p className="text-sm text-slate-400 mt-1">
            Quản lý vé đã mua và xuất trình mã QR khi đến sự kiện
          </p>
        </div>

        <div className="flex rounded-lg bg-slate-800/80 p-1 text-xs">
          <button
            onClick={() => setFilter("ALL")}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors ${
              filter === "ALL"
                ? "bg-emerald-500 text-slate-950 font-bold"
                : "text-slate-300 hover:text-white"
            }`}
          >
            Tất cả ({tickets.length})
          </button>
          <button
            onClick={() => setFilter("VALID")}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors ${
              filter === "VALID"
                ? "bg-emerald-500 text-slate-950 font-bold"
                : "text-slate-300 hover:text-white"
            }`}
          >
            Chưa sử dụng ({validCount})
          </button>
          <button
            onClick={() => setFilter("USED")}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors ${
              filter === "USED"
                ? "bg-emerald-500 text-slate-950 font-bold"
                : "text-slate-300 hover:text-white"
            }`}
          >
            Đã check-in ({usedCount})
          </button>
        </div>
      </div>

      {filteredTickets.length === 0 ? (
        <div className="text-center py-16 text-slate-400 space-y-2">
          <Ticket className="w-10 h-10 mx-auto text-slate-600" />
          <p className="text-sm font-medium">Không có vé nào.</p>
          <p className="text-xs">Hãy đặt vé cho sự kiện yêu thích của bạn!</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {filteredTickets.map((ticket) => {
            const isValid = mapStatus(ticket.status) === "Valid";
            return (
              <div
                key={ticket.id}
                className={`rounded-2xl border p-5 transition-all flex flex-col justify-between ${
                  isValid
                    ? "bg-[#131922] border-slate-700/80 hover:border-emerald-500/50 shadow-lg shadow-black/40"
                    : "bg-[#0f141a]/60 border-slate-800/50 opacity-70"
                }`}
              >
                <div>
                  <div className="flex justify-between items-start mb-3">
                    <span className="text-xs font-mono font-semibold px-2.5 py-1 rounded-md bg-slate-800 text-emerald-400 border border-slate-700">
                      {ticket.ticketCode}
                    </span>
                    <span
                      className={`text-xs px-2.5 py-0.5 rounded-full font-medium flex items-center gap-1 ${
                        isValid
                          ? "bg-emerald-500/15 text-emerald-400 border border-emerald-500/30"
                          : "bg-slate-700 text-slate-400"
                      }`}
                    >
                      {isValid ? (
                        <>
                          <Clock className="w-3 h-3" /> Hợp lệ
                        </>
                      ) : (
                        <>
                          <CheckCircle className="w-3 h-3" /> Đã check-in
                        </>
                      )}
                    </span>
                  </div>

                  <h2 className="text-lg font-bold text-white mb-2 leading-snug">
                    {ticket.eventTitle}
                  </h2>
                  <div className="text-xs text-emerald-400 font-semibold mb-4">
                    {ticket.ticketTypeName}
                  </div>

                  <div className="space-y-2 text-xs text-slate-300 mb-6">
                    <div className="flex items-center gap-2">
                      <Calendar className="w-4 h-4 text-slate-400 shrink-0" />
                      <span>{formatEventDate(ticket.eventStartsAt)}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <MapPin className="w-4 h-4 text-slate-400 shrink-0" />
                      <span className="truncate">{ticket.venueName}</span>
                    </div>
                  </div>
                </div>

                <div className="pt-4 border-t border-slate-800 flex items-center justify-between">
                  <div>
                    <span className="text-[11px] text-slate-400">Phát hành</span>
                    <p className="text-xs font-semibold text-slate-300">
                      {new Date(ticket.issuedAt).toLocaleDateString("vi-VN")}
                    </p>
                  </div>

                  {ticket.qrToken && isValid ? (
                    <button
                      onClick={() => setSelectedTicket(ticket)}
                      className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold transition-colors"
                    >
                      <span>Xem mã QR</span>
                    </button>
                  ) : (
                    <span className="text-xs text-slate-500">Không có QR</span>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* QR Code Modal */}
      {selectedTicket && selectedTicket.qrToken && (
        <div
          className="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4"
          onClick={() => setSelectedTicket(null)}
        >
          <div
            className="bg-[#151c24] border border-slate-700 rounded-2xl max-w-sm w-full p-6 text-center space-y-4 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="space-y-1">
              <span className="text-xs text-emerald-400 font-mono font-bold tracking-wider">
                {selectedTicket.ticketCode}
              </span>
              <h2 className="text-base font-bold text-white">
                {selectedTicket.eventTitle}
              </h2>
              <p className="text-xs text-slate-400">{selectedTicket.ticketTypeName}</p>
            </div>

            <div className="bg-white p-4 rounded-xl inline-block shadow-inner mx-auto">
              <QRCodeSVG value={selectedTicket.qrToken} size={200} level="H" />
            </div>

            <p className="text-xs text-slate-400">
              Xuất trình mã này cho nhân viên soát vé tại cổng check-in
            </p>

            <button
              onClick={() => setSelectedTicket(null)}
              className="w-full py-2.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-white text-xs font-semibold transition-colors"
            >
              Đóng
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
