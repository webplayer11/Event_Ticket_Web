import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Calendar,
  MapPin,
  Ticket,
  ShieldCheck,
  ChevronRight,
  Loader2,
  AlertCircle,
} from "lucide-react";
import { getPublishedEventById } from "../../services/eventService";
import type { EventResponse, TicketTypeResponse } from "../../types";

function formatEventDate(startsAt: string): string {
  const d = new Date(startsAt);
  const weekdays = ["Chủ nhật", "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7"];
  const hours = d.getHours().toString().padStart(2, "0");
  const minutes = d.getMinutes().toString().padStart(2, "0");
  const day = d.getDate().toString().padStart(2, "0");
  const month = (d.getMonth() + 1).toString().padStart(2, "0");
  const year = d.getFullYear();
  return `${hours}:${minutes} · ${weekdays[d.getDay()]}, ${day}.${month}.${year}`;
}

export const EventDetailPage = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [event, setEvent] = useState<EventResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedTier, setSelectedTier] = useState<string>("");
  const [quantity, setQuantity] = useState<number>(1);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    getPublishedEventById(id)
      .then((data) => {
        if (cancelled) return;
        setEvent(data);
        if (data.ticketTypes.length > 0) {
          setSelectedTier(data.ticketTypes[0].id);
        }
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err?.message ?? "Không thể tải thông tin sự kiện.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  if (loading) {
    return (
      <div className="max-w-6xl mx-auto px-4 py-16 flex flex-col items-center justify-center gap-4">
        <Loader2 className="w-8 h-8 text-emerald-400 animate-spin" />
        <p className="text-slate-400 text-sm">Đang tải thông tin sự kiện…</p>
      </div>
    );
  }

  if (error || !event) {
    return (
      <div className="max-w-6xl mx-auto px-4 py-16 flex flex-col items-center justify-center gap-4">
        <AlertCircle className="w-8 h-8 text-red-400" />
        <p className="text-red-400 text-sm">{error ?? "Không tìm thấy sự kiện."}</p>
        <button
          onClick={() => navigate(-1)}
          className="text-xs text-emerald-400 underline"
        >
          ← Quay lại
        </button>
      </div>
    );
  }

  const currentTier: TicketTypeResponse | undefined = event.ticketTypes.find(
    (t) => t.id === selectedTier,
  );
  const totalPrice = currentTier ? currentTier.price * quantity : 0;
  const maxQty = currentTier
    ? Math.min(currentTier.maxQuantityPerOrder, currentTier.availableQuantity)
    : 10;

  return (
    <div className="max-w-6xl mx-auto px-4 py-8">
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Left: Event info */}
        <div className="lg:col-span-2 space-y-6">
          {/* Banner */}
          <div className="aspect-video w-full rounded-2xl bg-gradient-to-tr from-slate-900 to-emerald-950/40 border border-slate-800 flex items-center justify-center p-8 relative overflow-hidden">
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_bottom_left,_var(--tw-gradient-stops))] from-emerald-500/20 via-transparent to-transparent" />
            <div className="text-center relative z-10">
              <span className="text-xs uppercase tracking-widest text-emerald-400 font-bold px-3 py-1 bg-emerald-500/10 rounded-full border border-emerald-500/20">
                {event.categoryName}
              </span>
              <h1 className="text-3xl font-extrabold text-white mt-4">
                {event.title}
              </h1>
              <p className="text-slate-400 text-sm mt-2">
                {event.organizationName}
              </p>
            </div>
          </div>

          {/* Event Info */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 bg-[#141922] p-5 rounded-xl border border-slate-800">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-slate-800 flex items-center justify-center text-emerald-400">
                <Calendar className="w-5 h-5" />
              </div>
              <div>
                <p className="text-xs text-slate-400">Thời gian</p>
                <p className="text-sm font-semibold text-white">
                  {formatEventDate(event.startsAt)}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-slate-800 flex items-center justify-center text-emerald-400">
                <MapPin className="w-5 h-5" />
              </div>
              <div>
                <p className="text-xs text-slate-400">Địa điểm</p>
                <p className="text-sm font-semibold text-white">
                  {event.venueName}, {event.city}
                </p>
                <p className="text-xs text-slate-500">{event.address}</p>
              </div>
            </div>
          </div>

          {/* Description */}
          <div className="space-y-3 bg-[#141922] p-6 rounded-xl border border-slate-800 text-sm text-slate-300 leading-relaxed">
            <h2 className="text-base font-bold text-white">Giới thiệu sự kiện</h2>
            <p style={{ whiteSpace: "pre-line" }}>{event.description}</p>
          </div>
        </div>

        {/* Right: Ticket Selector */}
        <div className="space-y-5">
          <div className="bg-[#141922] border border-slate-800 rounded-2xl p-6 sticky top-24">
            <h2 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
              <Ticket className="w-5 h-5 text-emerald-400" />
              Chọn loại vé
            </h2>

            {event.ticketTypes.length === 0 ? (
              <p className="text-sm text-slate-400">Chưa có loại vé nào.</p>
            ) : (
              <>
                <div className="space-y-3 mb-6">
                  {event.ticketTypes.map((tier) => (
                    <div
                      key={tier.id}
                      onClick={() =>
                        tier.availableQuantity > 0 && setSelectedTier(tier.id)
                      }
                      className={`p-3.5 rounded-xl border transition-all ${
                        tier.availableQuantity === 0
                          ? "opacity-50 cursor-not-allowed border-slate-800 bg-slate-900/40"
                          : selectedTier === tier.id
                            ? "border-emerald-500 bg-emerald-500/10 cursor-pointer"
                            : "border-slate-800 bg-slate-900/60 hover:border-slate-700 cursor-pointer"
                      }`}
                    >
                      <div className="flex justify-between items-center mb-1">
                        <span className="font-semibold text-sm text-white">
                          {tier.name}
                        </span>
                        <span className="text-emerald-400 font-bold text-sm">
                          {tier.price === 0
                            ? "Miễn phí"
                            : `${tier.price.toLocaleString("vi-VN")}đ`}
                        </span>
                      </div>
                      {tier.description && (
                        <p className="text-xs text-slate-400">{tier.description}</p>
                      )}
                      <p className="text-xs text-slate-500 mt-1">
                        {tier.availableQuantity > 0
                          ? `Còn ${tier.availableQuantity} vé`
                          : "Hết vé"}
                      </p>
                    </div>
                  ))}
                </div>

                {/* Quantity */}
                <div className="flex justify-between items-center py-3 border-y border-slate-800 mb-6">
                  <span className="text-sm text-slate-300 font-medium">Số lượng vé</span>
                  <div className="flex items-center gap-3">
                    <button
                      onClick={() => setQuantity(Math.max(1, quantity - 1))}
                      className="w-8 h-8 rounded-lg bg-slate-800 hover:bg-slate-700 text-white font-bold flex items-center justify-center"
                    >
                      -
                    </button>
                    <span className="font-bold text-sm w-4 text-center">
                      {quantity}
                    </span>
                    <button
                      onClick={() =>
                        setQuantity(Math.min(maxQty || 10, quantity + 1))
                      }
                      className="w-8 h-8 rounded-lg bg-slate-800 hover:bg-slate-700 text-white font-bold flex items-center justify-center"
                    >
                      +
                    </button>
                  </div>
                </div>

                {/* Total & CTA */}
                <div className="space-y-4">
                  <div className="flex justify-between items-center">
                    <span className="text-xs text-slate-400">Tổng thanh toán:</span>
                    <span className="text-xl font-extrabold text-emerald-400">
                      {totalPrice === 0
                        ? "Miễn phí"
                        : `${totalPrice.toLocaleString("vi-VN")}đ`}
                    </span>
                  </div>

                  <button
                    disabled={!currentTier || currentTier.availableQuantity === 0}
                    onClick={() =>
                      navigate("/checkout", {
                        state: { event, tier: currentTier, quantity, totalPrice },
                      })
                    }
                    className="w-full py-3 rounded-xl bg-emerald-500 hover:bg-emerald-400 disabled:opacity-50 disabled:cursor-not-allowed text-slate-950 font-bold text-sm flex items-center justify-center gap-2 transition-all shadow-lg shadow-emerald-500/20"
                  >
                    <span>Tiến hành đặt vé</span>
                    <ChevronRight className="w-4 h-4" />
                  </button>

                  <div className="flex items-center justify-center gap-1.5 text-[11px] text-slate-500">
                    <ShieldCheck className="w-3.5 h-3.5 text-emerald-500" />
                    <span>Vé chính hãng, hoàn hủy minh bạch theo quy định BTC</span>
                  </div>
                </div>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
