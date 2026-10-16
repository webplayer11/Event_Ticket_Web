import { useState } from "react";
import { Check, X, Eye } from "lucide-react";

export const EventApprovalPage = () => {
  const [events, setEvents] = useState([
    {
      id: 101,
      title: "Rock Storm Live 2026",
      organizer: "LiveNation Vietnam",
      date: "15.11.2026",
      venue: "SVĐ Quân Khu 7, TP.HCM",
      ticketCount: 15000,
      status: "PENDING",
    },
    {
      id: 102,
      title: "AI Summit Vietnam 2026",
      organizer: "TechHub Asia",
      date: "02.12.2026",
      venue: "Trung tâm Hội nghị Quốc gia, Hà Nội",
      ticketCount: 3000,
      status: "PENDING",
    },
  ]);

  const handleAction = (id: number, status: "APPROVED" | "REJECTED") => {
    setEvents((prev) => prev.filter((e) => e.id !== id));
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-white">Kiểm duyệt sự kiện</h1>
        <p className="text-xs text-slate-400 mt-1">
          Duyệt hoặc từ chối các sự kiện do Ban tổ chức gửi lên trước khi mở bán chính thức
        </p>
      </div>

      <div className="bg-[#0D1217] border border-slate-800 rounded-2xl overflow-hidden">
        {events.length === 0 ? (
          <div className="p-12 text-center text-slate-500 text-xs">
            Hiện không có sự kiện nào đang chờ phê duyệt.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead>
                <tr className="border-b border-slate-800 text-slate-400 bg-slate-900/60">
                  <th className="py-3.5 px-4 font-semibold">Tên sự kiện</th>
                  <th className="py-3.5 px-4 font-semibold">Nhà tổ chức</th>
                  <th className="py-3.5 px-4 font-semibold">Ngày diễn ra</th>
                  <th className="py-3.5 px-4 font-semibold">Địa điểm</th>
                  <th className="py-3.5 px-4 font-semibold">Quy mô vé</th>
                  <th className="py-3.5 px-4 font-semibold text-right">Phê duyệt</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/60 text-slate-300">
                {events.map((e) => (
                  <tr key={e.id} className="hover:bg-slate-800/30">
                    <td className="py-3.5 px-4 font-bold text-white">{e.title}</td>
                    <td className="py-3.5 px-4 text-slate-300">{e.organizer}</td>
                    <td className="py-3.5 px-4">{e.date}</td>
                    <td className="py-3.5 px-4">{e.venue}</td>
                    <td className="py-3.5 px-4 font-semibold text-white">
                      {e.ticketCount.toLocaleString("vi-VN")}
                    </td>
                    <td className="py-3.5 px-4 text-right space-x-2">
                      <button
                        onClick={() => handleAction(e.id, "APPROVED")}
                        className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 border border-emerald-500/20 text-xs font-semibold"
                      >
                        <Check className="w-3.5 h-3.5" /> Duyệt
                      </button>
                      <button
                        onClick={() => handleAction(e.id, "REJECTED")}
                        className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-red-500/10 text-red-400 hover:bg-red-500/20 border border-red-500/20 text-xs font-semibold"
                      >
                        <X className="w-3.5 h-3.5" /> Từ chối
                      </button>
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
