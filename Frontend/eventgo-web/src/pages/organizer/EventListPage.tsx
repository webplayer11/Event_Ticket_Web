import { Link } from "react-router-dom";
import { Plus, Edit, QrCode } from "lucide-react";

export const EventListPage = () => {
  const events = [
    {
      id: 1,
      title: "NEON PULSE — Live in Saigon",
      date: "20:00 · 29.08.2026",
      venue: "The Global City, TP. Hồ Chí Minh",
      sold: 850,
      total: 1000,
      status: "Active",
    },
    {
      id: 2,
      title: "Afterglow — Indie Music Festival",
      date: "17:30 · 10.10.2026",
      venue: "Vinhomes Grand Park, TP. Hồ Chí Minh",
      sold: 420,
      total: 600,
      status: "Active",
    },
    {
      id: 3,
      title: "Future Makers — Creative Conference",
      date: "08:00 · 17.10.2026",
      venue: "GEM Center, TP. Hồ Chí Minh",
      sold: 0,
      total: 500,
      status: "Draft",
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-2xl font-bold text-white">Danh sách sự kiện</h1>
          <p className="text-xs text-slate-400 mt-1">Quản lý các sự kiện bạn đã tạo và cấu hình</p>
        </div>
        <Link
          to="/organizer/create-event"
          className="inline-flex items-center gap-2 px-3.5 py-2 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold transition-colors"
        >
          <Plus className="w-4 h-4" />
          <span>Tạo sự kiện mới</span>
        </Link>
      </div>

      <div className="bg-[#141A1E] border border-slate-800 rounded-2xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400 bg-slate-900/40">
                <th className="py-3.5 px-4 font-semibold">Tên sự kiện</th>
                <th className="py-3.5 px-4 font-semibold">Thời gian</th>
                <th className="py-3.5 px-4 font-semibold">Địa điểm</th>
                <th className="py-3.5 px-4 font-semibold">Vé đã bán</th>
                <th className="py-3.5 px-4 font-semibold">Trạng thái</th>
                <th className="py-3.5 px-4 font-semibold text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 text-slate-300">
              {events.map((ev) => (
                <tr key={ev.id} className="hover:bg-slate-800/20">
                  <td className="py-3.5 px-4 font-bold text-white">{ev.title}</td>
                  <td className="py-3.5 px-4">{ev.date}</td>
                  <td className="py-3.5 px-4">{ev.venue}</td>
                  <td className="py-3.5 px-4">
                    <span className="font-semibold text-white">{ev.sold}</span> / {ev.total}
                  </td>
                  <td className="py-3.5 px-4">
                    <span
                      className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                        ev.status === "Active"
                          ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                          : "bg-slate-800 text-slate-400"
                      }`}
                    >
                      {ev.status === "Active" ? "Đang mở bán" : "Bản nháp"}
                    </span>
                  </td>
                  <td className="py-3.5 px-4 text-right space-x-2">
                    <Link
                      to="/organizer/check-in"
                      className="p-1.5 inline-block text-slate-400 hover:text-emerald-400 rounded hover:bg-slate-800"
                      title="Soát vé QR"
                    >
                      <QrCode className="w-4 h-4" />
                    </Link>
                    <button
                      className="p-1.5 inline-block text-slate-400 hover:text-white rounded hover:bg-slate-800"
                      title="Chỉnh sửa"
                    >
                      <Edit className="w-4 h-4" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
