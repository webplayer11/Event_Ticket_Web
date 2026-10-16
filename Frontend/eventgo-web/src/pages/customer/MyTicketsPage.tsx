import { useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { Ticket, Calendar, MapPin, CheckCircle, Clock } from "lucide-react";

interface TicketData {
  id: string;
  code: string;
  eventName: string;
  tier: string;
  price: string;
  date: string;
  location: string;
  status: "Valid" | "Used";
  qrToken: string;
}

const mockTickets: TicketData[] = [
  {
    id: "tkt-1",
    code: "EG-NEON-88291",
    eventName: "NEON PULSE — Live in Saigon",
    tier: "VIP Area A - Early Bird",
    price: "650.000đ",
    date: "20:00 · 29.08.2026",
    location: "The Global City, TP. Hồ Chí Minh",
    status: "Valid",
    qrToken: "EG_TOKEN_NEON_88291_VERIFIED",
  },
  {
    id: "tkt-2",
    code: "EG-FORUM-44120",
    eventName: "Vietnam Innovation Forum 2026",
    tier: "Standard Pass",
    price: "300.000đ",
    date: "08:30 · 12.09.2026",
    location: "Thiskyhall Sala, TP. Hồ Chí Minh",
    status: "Valid",
    qrToken: "EG_TOKEN_FORUM_44120_VERIFIED",
  },
  {
    id: "tkt-3",
    code: "EG-RUN-11928",
    eventName: "Đường chạy thành phố — Night Run",
    tier: "Runner 10KM",
    price: "390.000đ",
    date: "18:00 · 19.09.2026",
    location: "Công viên Bờ sông Sài Gòn",
    status: "Used",
    qrToken: "EG_TOKEN_RUN_11928_USED",
  },
];

export const MyTicketsPage = () => {
  const [filter, setFilter] = useState<"ALL" | "VALID" | "USED">("ALL");
  const [selectedTicket, setSelectedTicket] = useState<TicketData | null>(null);

  const filteredTickets = mockTickets.filter((t) => {
    if (filter === "VALID") return t.status === "Valid";
    if (filter === "USED") return t.status === "Used";
    return true;
  });

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
              filter === "ALL" ? "bg-emerald-500 text-slate-950 font-bold" : "text-slate-300 hover:text-white"
            }`}
          >
            Tất cả ({mockTickets.length})
          </button>
          <button
            onClick={() => setFilter("VALID")}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors ${
              filter === "VALID" ? "bg-emerald-500 text-slate-950 font-bold" : "text-slate-300 hover:text-white"
            }`}
          >
            Chưa sử dụng ({mockTickets.filter((t) => t.status === "Valid").length})
          </button>
          <button
            onClick={() => setFilter("USED")}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors ${
              filter === "USED" ? "bg-emerald-500 text-slate-950 font-bold" : "text-slate-300 hover:text-white"
            }`}
          >
            Đã check-in ({mockTickets.filter((t) => t.status === "Used").length})
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {filteredTickets.map((ticket) => (
          <div
            key={ticket.id}
            className={`rounded-2xl border p-5 transition-all flex flex-col justify-between ${
              ticket.status === "Valid"
                ? "bg-[#131922] border-slate-700/80 hover:border-emerald-500/50 shadow-lg shadow-black/40"
                : "bg-[#0f141a]/60 border-slate-800/50 opacity-70"
            }`}
          >
            <div>
              <div className="flex justify-between items-start mb-3">
                <span className="text-xs font-mono font-semibold px-2.5 py-1 rounded-md bg-slate-800 text-emerald-400 border border-slate-700">
                  {ticket.code}
                </span>
                <span
                  className={`text-xs px-2.5 py-0.5 rounded-full font-medium flex items-center gap-1 ${
                    ticket.status === "Valid"
                      ? "bg-emerald-500/15 text-emerald-400 border border-emerald-500/30"
                      : "bg-slate-700 text-slate-400"
                  }`}
                >
                  {ticket.status === "Valid" ? (
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

              <h2 className="text-lg font-bold text-white mb-2 leading-snug">{ticket.eventName}</h2>
              <div className="text-xs text-emerald-400 font-semibold mb-4">{ticket.tier}</div>

              <div className="space-y-2 text-xs text-slate-300 mb-6">
                <div className="flex items-center gap-2">
                  <Calendar className="w-4 h-4 text-slate-400 shrink-0" />
                  <span>{ticket.date}</span>
                </div>
                <div className="flex items-center gap-2">
                  <MapPin className="w-4 h-4 text-slate-400 shrink-0" />
                  <span className="truncate">{ticket.location}</span>
                </div>
              </div>
            </div>

            <div className="pt-4 border-t border-slate-800 flex items-center justify-between">
              <div>
                <span className="text-[11px] text-slate-400">Giá vé</span>
                <p className="text-sm font-bold text-white">{ticket.price}</p>
              </div>

              <button
                onClick={() => setSelectedTicket(ticket)}
                className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold transition-colors"
              >
                <span>Xem mã QR</span>
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* QR Code Modal */}
      {selectedTicket && (
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
                {selectedTicket.code}
              </span>
              <h2 className="text-base font-bold text-white">{selectedTicket.eventName}</h2>
              <p className="text-xs text-slate-400">{selectedTicket.tier}</p>
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
