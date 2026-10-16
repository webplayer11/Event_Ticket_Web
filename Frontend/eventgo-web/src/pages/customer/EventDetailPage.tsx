import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { Calendar, MapPin, Ticket, ShieldCheck, ChevronRight } from "lucide-react";

export const EventDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const [selectedTier, setSelectedTier] = useState<number>(1);
  const [quantity, setQuantity] = useState<number>(1);

  const tiers = [
    { id: 1, name: "Early Bird General", price: 450000, desc: "Vé vào cổng tự do khu vực General" },
    { id: 2, name: "Standard GA", price: 650000, desc: "Vé chính thức + 1 đồ uống miễn phí" },
    { id: 3, name: "VIP Lounge Pass", price: 1200000, desc: "Khu vực VIP sát sân khấu + Lối đi riêng + Quà tặng độc quyền" },
  ];

  const currentTier = tiers.find((t) => t.id === selectedTier) ?? tiers[0];
  const totalPrice = currentTier.price * quantity;

  return (
    <div className="max-w-6xl mx-auto px-4 py-8">
      {/* Event Header */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        <div className="lg:col-span-2 space-y-6">
          <div className="aspect-video w-full rounded-2xl bg-gradient-to-tr from-slate-900 to-emerald-950/40 border border-slate-800 flex items-center justify-center p-8 relative overflow-hidden">
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_bottom_left,_var(--tw-gradient-stops))] from-emerald-500/20 via-transparent to-transparent"></div>
            <div className="text-center relative z-10">
              <span className="text-xs uppercase tracking-widest text-emerald-400 font-bold px-3 py-1 bg-emerald-500/10 rounded-full border border-emerald-500/20">
                Live Concert #{id || 1}
              </span>
              <h1 className="text-3xl font-extrabold text-white mt-4">
                NEON PULSE — Live in Saigon
              </h1>
              <p className="text-slate-400 text-sm mt-2">
                Đêm đại nhạc hội ánh sáng và âm thanh điện tử lớn nhất năm 2026
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
                <p className="text-sm font-semibold text-white">20:00 · Thứ 7, 29.08.2026</p>
              </div>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-slate-800 flex items-center justify-center text-emerald-400">
                <MapPin className="w-5 h-5" />
              </div>
              <div>
                <p className="text-xs text-slate-400">Địa điểm</p>
                <p className="text-sm font-semibold text-white">The Global City, TP. Hồ Chí Minh</p>
              </div>
            </div>
          </div>

          {/* Description */}
          <div className="space-y-3 bg-[#141922] p-6 rounded-xl border border-slate-800 text-sm text-slate-300 leading-relaxed">
            <h2 className="text-base font-bold text-white">Giới thiệu sự kiện</h2>
            <p>
              NEON PULSE quy tụ những nghệ sĩ indie và nhà sản xuất âm nhạc hàng đầu. Sân khấu được trang bị hệ thống laser mapping hiện đại cùng trải nghiệm âm thanh 3D sống động.
            </p>
            <p>
              Sự kiện tuân thủ các tiêu chuẩn an toàn nghiêm ngặt, vé điện tử QR code được mã hóa chống làm giả và quét check-in tốc độ cao tại cửa kiểm soát.
            </p>
          </div>
        </div>

        {/* Ticket Selector Sidebar */}
        <div className="space-y-5">
          <div className="bg-[#141922] border border-slate-800 rounded-2xl p-6 sticky top-24">
            <h2 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
              <Ticket className="w-5 h-5 text-emerald-400" />
              Chọn loại vé
            </h2>

            <div className="space-y-3 mb-6">
              {tiers.map((tier) => (
                <div
                  key={tier.id}
                  onClick={() => setSelectedTier(tier.id)}
                  className={`p-3.5 rounded-xl border cursor-pointer transition-all ${
                    selectedTier === tier.id
                      ? "border-emerald-500 bg-emerald-500/10"
                      : "border-slate-800 bg-slate-900/60 hover:border-slate-700"
                  }`}
                >
                  <div className="flex justify-between items-center mb-1">
                    <span className="font-semibold text-sm text-white">{tier.name}</span>
                    <span className="text-emerald-400 font-bold text-sm">
                      {tier.price.toLocaleString("vi-VN")}đ
                    </span>
                  </div>
                  <p className="text-xs text-slate-400">{tier.desc}</p>
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
                <span className="font-bold text-sm w-4 text-center">{quantity}</span>
                <button
                  onClick={() => setQuantity(Math.min(10, quantity + 1))}
                  className="w-8 h-8 rounded-lg bg-slate-800 hover:bg-slate-700 text-white font-bold flex items-center justify-center"
                >
                  +
                </button>
              </div>
            </div>

            {/* Total and CTA */}
            <div className="space-y-4">
              <div className="flex justify-between items-center">
                <span className="text-xs text-slate-400">Tổng thanh toán:</span>
                <span className="text-xl font-extrabold text-emerald-400">
                  {totalPrice.toLocaleString("vi-VN")}đ
                </span>
              </div>

              <button
                onClick={() => navigate("/checkout", { state: { tier: currentTier, quantity, totalPrice } })}
                className="w-full py-3 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-sm flex items-center justify-center gap-2 transition-all shadow-lg shadow-emerald-500/20"
              >
                <span>Tiến hành đặt vé</span>
                <ChevronRight className="w-4 h-4" />
              </button>

              <div className="flex items-center justify-center gap-1.5 text-[11px] text-slate-500">
                <ShieldCheck className="w-3.5 h-3.5 text-emerald-500" />
                <span>Vé chính hãng, hoàn hủy minh bạch theo quy định BTC</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
