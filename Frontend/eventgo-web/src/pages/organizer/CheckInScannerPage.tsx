import { useState, useEffect } from "react";
import {
  QrCode,
  CheckCircle2,
  AlertTriangle,
  ScanLine,
  ChevronDown,
  Loader2,
} from "lucide-react";
import { getMyEvents } from "../../services/eventService";
import { validateQrToken, checkInTicket } from "../../services/ticketService";
import type { ManagedEventResponse, TicketValidationResponse } from "../../types";

type ScanStatus = "SUCCESS" | "DUPLICATE" | "INVALID" | "NOT_ASSIGNED" | null;

interface ScanResult {
  status: ScanStatus;
  message: string;
  ticketInfo?: TicketValidationResponse;
}

export const CheckInScannerPage = () => {
  const [events, setEvents] = useState<ManagedEventResponse[]>([]);
  const [loadingEvents, setLoadingEvents] = useState(true);
  const [selectedEventId, setSelectedEventId] = useState<string>("");
  const [ticketInput, setTicketInput] = useState("");
  const [processing, setProcessing] = useState(false);
  const [result, setResult] = useState<ScanResult>({ status: null, message: "" });
  const [mode, setMode] = useState<"validate" | "checkin">("checkin");

  useEffect(() => {
    getMyEvents()
      .then((data) => {
        // Show only Published events for check-in
        const publishedOrAll = data.filter(
          (e) => e.status === "Published",
        );
        setEvents(publishedOrAll);
        if (publishedOrAll.length > 0) {
          setSelectedEventId(publishedOrAll[0].id);
        }
      })
      .catch(() => {
        /* ignore — user can still enter manually */
      })
      .finally(() => setLoadingEvents(false));
  }, []);

  const handleScan = async () => {
    const qrToken = ticketInput.trim();
    if (!qrToken || !selectedEventId) return;

    setProcessing(true);
    setResult({ status: null, message: "" });

    try {
      if (mode === "validate") {
        const info = await validateQrToken(selectedEventId, qrToken);
        if (info.isCheckedIn) {
          setResult({
            status: "DUPLICATE",
            message: "CẢNH BÁO: Vé này đã được check-in trước đó!",
            ticketInfo: info,
          });
        } else {
          setResult({
            status: "SUCCESS",
            message: "VÉ HỢP LỆ — Bấm Check-in để xác nhận.",
            ticketInfo: info,
          });
        }
      } else {
        // checkin mode
        const info = await checkInTicket(selectedEventId, qrToken);
        setResult({
          status: "SUCCESS",
          message: "CHECK-IN THÀNH CÔNG! Vé hợp lệ.",
          ticketInfo: info,
        });
        setTicketInput("");
      }
    } catch (err: unknown) {
      const e = err as { message?: string; status?: number };
      if (e.status === 409) {
        // Already checked in - backend may return ticket info in error
        setResult({
          status: "DUPLICATE",
          message: "CẢNH BÁO: Vé này đã được check-in trước đó!",
        });
      } else if (e.status === 404) {
        setResult({
          status: "INVALID",
          message: "Mã vé không tồn tại hoặc không hợp lệ!",
        });
      } else if (e.status === 403) {
        setResult({
          status: "NOT_ASSIGNED",
          message: "Bạn không có quyền soát vé sự kiện này.",
        });
      } else if (e.status === 422) {
        setResult({
          status: "INVALID",
          message: "Vé không hợp lệ hoặc đã hết hạn.",
        });
      } else {
        setResult({
          status: "INVALID",
          message: e.message ?? "Đã có lỗi xảy ra. Vui lòng thử lại.",
        });
      }
    } finally {
      setProcessing(false);
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
          Quét mã QR để xác thực và check-in vé — hệ thống chống vé lặp tự động
        </p>
      </div>

      {/* Event Selector */}
      <div className="bg-[#141A1E] border border-slate-800 rounded-2xl p-5 space-y-3">
        <label className="text-xs text-slate-400 block font-semibold">
          Chọn sự kiện đang soát vé:
        </label>
        {loadingEvents ? (
          <div className="flex items-center gap-2 text-slate-400 text-xs">
            <Loader2 className="w-4 h-4 animate-spin text-emerald-400" />
            Đang tải sự kiện…
          </div>
        ) : events.length === 0 ? (
          <div className="text-xs text-amber-400 bg-amber-500/10 border border-amber-500/20 rounded-lg px-3 py-2">
            Bạn chưa có sự kiện nào đang mở bán. Chỉ sự kiện đã được duyệt mới có thể soát vé.
          </div>
        ) : (
          <div className="relative">
            <select
              value={selectedEventId}
              onChange={(e) => {
                setSelectedEventId(e.target.value);
                setResult({ status: null, message: "" });
              }}
              className="w-full bg-slate-900 border border-slate-700 rounded-xl px-3.5 py-2.5 text-xs text-white appearance-none focus:outline-none focus:border-emerald-500 pr-8"
            >
              {events.map((ev) => (
                <option key={ev.id} value={ev.id}>
                  {ev.title} — {ev.venueName}
                </option>
              ))}
            </select>
            <ChevronDown className="w-4 h-4 text-slate-400 absolute right-3 top-2.5 pointer-events-none" />
          </div>
        )}

        {/* Mode toggle */}
        <div className="flex gap-2">
          <button
            onClick={() => setMode("checkin")}
            className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors ${
              mode === "checkin"
                ? "bg-emerald-500 text-slate-950"
                : "bg-slate-800 text-slate-300 hover:bg-slate-700"
            }`}
          >
            Chế độ Check-in
          </button>
          <button
            onClick={() => setMode("validate")}
            className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors ${
              mode === "validate"
                ? "bg-amber-500 text-slate-950"
                : "bg-slate-800 text-slate-300 hover:bg-slate-700"
            }`}
          >
            Chế độ Xem trước (Validate)
          </button>
        </div>
      </div>

      {/* Scanner Box */}
      <div className="bg-[#141A1E] border border-slate-800 rounded-2xl p-6 text-center space-y-4">
        <div className="w-48 h-48 border-2 border-dashed border-emerald-500/50 rounded-2xl mx-auto flex flex-col items-center justify-center bg-emerald-500/5 relative overflow-hidden">
          <QrCode className="w-16 h-16 text-emerald-400 animate-pulse" />
          <span className="text-xs text-slate-400 mt-2 font-medium">
            Khu vực quét Camera
          </span>
          <div className="absolute top-0 left-0 right-0 h-0.5 bg-emerald-400 shadow-[0_0_8px_#10b981] animate-bounce" />
        </div>

        <div className="max-w-md mx-auto space-y-3">
          <label htmlFor="ticket-code-input" className="text-xs text-slate-400 block text-left">
            Hoặc nhập QR token / mã vé thủ công:
          </label>
          <div className="flex gap-2">
            <input
              id="ticket-code-input"
              type="text"
              value={ticketInput}
              onChange={(e) => setTicketInput(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && !processing && handleScan()}
              placeholder="Nhập QR token hoặc mã vé…"
              disabled={!selectedEventId || processing}
              className="flex-1 bg-slate-900 border border-slate-700 rounded-xl px-3.5 py-2 text-xs text-white focus:outline-none focus:border-emerald-500 disabled:opacity-50"
            />
            <button
              onClick={handleScan}
              disabled={!ticketInput.trim() || !selectedEventId || processing}
              className="px-4 py-2 bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-xs rounded-xl transition-colors disabled:opacity-50 flex items-center gap-1.5"
            >
              {processing ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                "Kiểm tra"
              )}
            </button>
          </div>
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
            {result.status === "SUCCESS" && (
              <CheckCircle2 className="w-5 h-5 text-emerald-400" />
            )}
            {(result.status === "DUPLICATE" ||
              result.status === "INVALID" ||
              result.status === "NOT_ASSIGNED") && (
              <AlertTriangle
                className={`w-5 h-5 ${
                  result.status === "DUPLICATE"
                    ? "text-amber-400"
                    : "text-red-400"
                }`}
              />
            )}
            <span>{result.message}</span>
          </div>

          {result.ticketInfo && (
            <div className="bg-black/30 p-3.5 rounded-xl space-y-1.5 text-xs text-slate-300">
              <div className="flex justify-between">
                <span className="text-slate-400">Chủ vé:</span>
                <span className="font-semibold text-white">
                  {result.ticketInfo.customerName}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Email:</span>
                <span className="text-white">{result.ticketInfo.customerEmail}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Hạng vé:</span>
                <span className="font-semibold text-white">
                  {result.ticketInfo.ticketTypeName}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Mã vé:</span>
                <span className="font-mono text-emerald-400">
                  {result.ticketInfo.ticketCode}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Sự kiện:</span>
                <span className="font-semibold text-white">
                  {result.ticketInfo.eventTitle}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Trạng thái:</span>
                <span
                  className={
                    result.ticketInfo.isCheckedIn
                      ? "text-amber-400"
                      : "text-emerald-400"
                  }
                >
                  {result.ticketInfo.isCheckedIn ? "Đã check-in" : "Hợp lệ"}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Thời gian soát:</span>
                <span>{new Date().toLocaleTimeString("vi-VN")}</span>
              </div>

              {/* Confirm check-in button (validate mode only) */}
              {mode === "validate" && !result.ticketInfo.isCheckedIn && (
                <button
                  onClick={async () => {
                    if (!result.ticketInfo || !selectedEventId) return;
                    setProcessing(true);
                    try {
                      await checkInTicket(selectedEventId, ticketInput.trim());
                      setResult({
                        status: "SUCCESS",
                        message: "CHECK-IN THÀNH CÔNG! Vé hợp lệ.",
                        ticketInfo: { ...result.ticketInfo, isCheckedIn: true },
                      });
                      setTicketInput("");
                    } catch (err: unknown) {
                      setResult({
                        status: "DUPLICATE",
                        message:
                          (err as { message?: string })?.message ??
                          "Không thể check-in.",
                      });
                    } finally {
                      setProcessing(false);
                    }
                  }}
                  className="w-full mt-2 py-2 rounded-lg bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold transition-colors"
                >
                  ✓ Xác nhận Check-in
                </button>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
