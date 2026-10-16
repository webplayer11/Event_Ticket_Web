import { useState } from "react";
import { QrCode, CheckCircle2, AlertTriangle, ScanLine } from "lucide-react";

export const CheckInScannerPage = () => {
  const [ticketInput, setTicketInput] = useState("");
  const [result, setResult] = useState<{
    status: "SUCCESS" | "DUPLICATE" | "INVALID" | null;
    message?: string;
    ticketInfo?: { holder: string; tier: string; event: string; code: string; time: string };
  }>({ status: null });

  const handleSimulateScan = (codeToScan?: string) => {
    const code = codeToScan || ticketInput.trim();
    if (!code) return;

    if (code.includes("USED") || code === "EG-RUN-11928") {
      setResult({
        status: "DUPLICATE",
        message: "CẢNH BÁO: Vé này đã được check-in trước đó vào 19:30!",
        ticketInfo: {
          holder: "Lê Minh Tuấn",
          tier: "Runner 10KM",
          event: "Đường chạy thành phố — Night Run",
          code,
          time: "19:30 · 19.09.2026",
        },
      });
    } else if (code.startsWith("EG-") || code.includes("VERIFIED")) {
      setResult({
        status: "SUCCESS",
        message: "CHECK-IN THÀNH CÔNG! Vé hợp lệ.",
        ticketInfo: {
          holder: "Nguyễn Việt Huỳnh",
          tier: "VIP Area A - Early Bird",
          event: "NEON PULSE — Live in Saigon",
          code,
          time: new Date().toLocaleTimeString("vi-VN"),
        },
      });
    } else {
      setResult({
        status: "INVALID",
        message: "Mã vé không tồn tại hoặc không hợp lệ trên hệ thống!",
      });
    }
  };

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-white flex items-center gap-2">
          <ScanLine className="w-6 h-6 text-emerald-400" />
          Soát vé sự kiện (Check-in Scanner)
        </h1>
        <p className="text-xs text-slate-400 mt-1">
          Hệ thống quét mã QR tự động xác thực và chặn vé lặp (Double Check-in Prevention)
        </p>
      </div>

      {/* Scanner Box */}
      <div className="bg-[#141A1E] border border-slate-800 rounded-2xl p-6 text-center space-y-4">
        <div className="w-48 h-48 border-2 border-dashed border-emerald-500/50 rounded-2xl mx-auto flex flex-col items-center justify-center bg-emerald-500/5 relative overflow-hidden">
          <QrCode className="w-16 h-16 text-emerald-400 animate-pulse" />
          <span className="text-xs text-slate-400 mt-2 font-medium">Khu vực quét Camera</span>
          <div className="absolute top-0 left-0 right-0 h-0.5 bg-emerald-400 shadow-[0_0_8px_#10b981] animate-bounce"></div>
        </div>

        <div className="max-w-md mx-auto space-y-3">
          <label htmlFor="ticket-code-input" className="text-xs text-slate-400 block text-left">Hoặc nhập mã vé thủ công để kiểm tra:</label>
          <div className="flex gap-2">
            <input
              id="ticket-code-input"
              type="text"
              value={ticketInput}
              onChange={(e) => setTicketInput(e.target.value)}
              placeholder="VD: EG-NEON-88291..."
              className="flex-1 bg-slate-900 border border-slate-700 rounded-xl px-3.5 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
            />
            <button
              onClick={() => handleSimulateScan()}
              className="px-4 py-2 bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-xs rounded-xl transition-colors"
            >
              Kiểm tra
            </button>
          </div>
        </div>

        {/* Quick Test Chips */}
        <div className="flex items-center justify-center gap-2 pt-2">
          <span className="text-[11px] text-slate-500">Mã mẫu:</span>
          <button
            onClick={() => {
              setTicketInput("EG-NEON-88291");
              handleSimulateScan("EG-NEON-88291");
            }}
            className="text-[11px] px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-emerald-400 font-mono"
          >
            EG-NEON-88291 (Hợp lệ)
          </button>
          <button
            onClick={() => {
              setTicketInput("EG-RUN-11928");
              handleSimulateScan("EG-RUN-11928");
            }}
            className="text-[11px] px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-red-400 font-mono"
          >
            EG-RUN-11928 (Đã dùng)
          </button>
        </div>
      </div>

      {/* Result Card */}
      {result.status && (
        <div
          className={`p-5 rounded-2xl border text-sm space-y-3 transition-all ${
            result.status === "SUCCESS"
              ? "bg-emerald-500/10 border-emerald-500/30 text-emerald-300"
              : result.status === "DUPLICATE"
              ? "bg-amber-500/10 border-amber-500/30 text-amber-300"
              : "bg-red-500/10 border-red-500/30 text-red-300"
          }`}
        >
          <div className="flex items-center gap-2 font-bold text-base">
            {result.status === "SUCCESS" && <CheckCircle2 className="w-5 h-5 text-emerald-400" />}
            {result.status === "DUPLICATE" && <AlertTriangle className="w-5 h-5 text-amber-400" />}
            {result.status === "INVALID" && <AlertTriangle className="w-5 h-5 text-red-400" />}
            <span>{result.message}</span>
          </div>

          {result.ticketInfo && (
            <div className="bg-black/30 p-3.5 rounded-xl space-y-1.5 text-xs text-slate-300">
              <div className="flex justify-between">
                <span className="text-slate-400">Chủ vé:</span>
                <span className="font-semibold text-white">{result.ticketInfo.holder}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Hạng vé:</span>
                <span className="font-semibold text-white">{result.ticketInfo.tier}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Sự kiện:</span>
                <span className="font-semibold text-white">{result.ticketInfo.event}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Thời gian soát:</span>
                <span>{result.ticketInfo.time}</span>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
