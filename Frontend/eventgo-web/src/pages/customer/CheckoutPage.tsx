import { useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { ShieldCheck, CreditCard, ArrowLeft, CheckCircle2 } from "lucide-react";

export const CheckoutPage = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const state = location.state as { tier?: { name: string; price: number }; quantity?: number; totalPrice?: number } | null;

  const [fullName, setFullName] = useState("Nguyễn Việt Huỳnh");
  const [email, setEmail] = useState("huynh@eventgo.vn");
  const [phone, setPhone] = useState("0912345678");
  const [paymentMethod, setPaymentMethod] = useState<"VNPAY" | "MOMO" | "BANK">("VNPAY");
  const [isSuccess, setIsSuccess] = useState(false);

  const tierName = state?.tier?.name ?? "Standard GA";
  const quantity = state?.quantity ?? 1;
  const totalPrice = state?.totalPrice ?? 650000;

  const handlePay = (e: React.FormEvent) => {
    e.preventDefault();
    setIsSuccess(true);
  };

  if (isSuccess) {
    return (
      <div className="max-w-md mx-auto px-4 py-16 text-center space-y-6">
        <div className="w-16 h-16 rounded-full bg-emerald-500/20 text-emerald-400 mx-auto flex items-center justify-center">
          <CheckCircle2 className="w-10 h-10" />
        </div>
        <div className="space-y-2">
          <h1 className="text-2xl font-bold text-white">Đặt vé thành công!</h1>
          <p className="text-sm text-slate-400">
            Mã vé và thông tin xác nhận đã được gửi về email <span className="text-emerald-400 font-medium">{email}</span>
          </p>
        </div>

        <div className="bg-[#141922] p-4 rounded-xl border border-slate-800 text-left text-xs space-y-2 text-slate-300">
          <div className="flex justify-between">
            <span className="text-slate-400">Hạng vé:</span>
            <span className="font-semibold text-white">{tierName} (x{quantity})</span>
          </div>
          <div className="flex justify-between">
            <span className="text-slate-400">Tổng thanh toán:</span>
            <span className="font-semibold text-emerald-400">{totalPrice.toLocaleString("vi-VN")}đ</span>
          </div>
          <div className="flex justify-between">
            <span className="text-slate-400">Phương thức:</span>
            <span>{paymentMethod}</span>
          </div>
        </div>

        <button
          onClick={() => navigate("/my-tickets")}
          className="w-full py-3 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-sm transition-colors"
        >
          Xem vé của tôi ngay
        </button>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto px-4 py-8">
      <button
        onClick={() => navigate(-1)}
        className="flex items-center gap-2 text-xs text-slate-400 hover:text-white mb-6"
      >
        <ArrowLeft className="w-4 h-4" />
        <span>Quay lại sự kiện</span>
      </button>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Customer & Payment Form */}
        <div className="lg:col-span-2 space-y-6">
          <form onSubmit={handlePay} className="space-y-6">
            {/* Info */}
            <div className="bg-[#141922] p-6 rounded-2xl border border-slate-800 space-y-4">
              <h2 className="text-base font-bold text-white">1. Thông tin người nhận vé</h2>
              <div className="space-y-3">
                <div>
                  <label htmlFor="checkout-fullname" className="block text-xs text-slate-400 mb-1">Họ và tên</label>
                  <input
                    id="checkout-fullname"
                    type="text"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    required
                    className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-emerald-500"
                  />
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label htmlFor="checkout-email" className="block text-xs text-slate-400 mb-1">Email nhận vé</label>
                    <input
                      id="checkout-email"
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      required
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-emerald-500"
                    />
                  </div>
                  <div>
                    <label htmlFor="checkout-phone" className="block text-xs text-slate-400 mb-1">Số điện thoại</label>
                    <input
                      id="checkout-phone"
                      type="tel"
                      value={phone}
                      onChange={(e) => setPhone(e.target.value)}
                      required
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-emerald-500"
                    />
                  </div>
                </div>
              </div>
            </div>

            {/* Payment Method */}
            <div className="bg-[#141922] p-6 rounded-2xl border border-slate-800 space-y-4">
              <h2 className="text-base font-bold text-white">2. Phương thức thanh toán</h2>
              <div className="grid grid-cols-3 gap-3">
                {(["VNPAY", "MOMO", "BANK"] as const).map((method) => (
                  <div
                    key={method}
                    onClick={() => setPaymentMethod(method)}
                    className={`p-3.5 rounded-xl border text-center cursor-pointer transition-all ${
                      paymentMethod === method
                        ? "border-emerald-500 bg-emerald-500/10 text-white font-bold"
                        : "border-slate-800 bg-slate-900/60 text-slate-400 hover:border-slate-700"
                    }`}
                  >
                    <CreditCard className="w-5 h-5 mx-auto mb-1.5 opacity-80" />
                    <span className="text-xs">{method}</span>
                  </div>
                ))}
              </div>
            </div>

            <button
              type="submit"
              className="w-full py-3.5 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-sm transition-all shadow-lg shadow-emerald-500/20"
            >
              Xác nhận & Thanh toán {totalPrice.toLocaleString("vi-VN")}đ
            </button>
          </form>
        </div>

        {/* Order Summary */}
        <div className="space-y-4">
          <div className="bg-[#141922] p-6 rounded-2xl border border-slate-800 space-y-4">
            <h2 className="text-sm font-bold text-white uppercase tracking-wider">Tóm tắt đơn hàng</h2>
            <div className="space-y-2 text-xs text-slate-300">
              <div className="flex justify-between">
                <span>Hạng vé:</span>
                <span className="font-semibold text-white">{tierName}</span>
              </div>
              <div className="flex justify-between">
                <span>Số lượng:</span>
                <span className="font-semibold text-white">{quantity} vé</span>
              </div>
              <div className="border-t border-slate-800 pt-3 flex justify-between items-center text-sm font-bold">
                <span className="text-white">Tổng cộng:</span>
                <span className="text-emerald-400">{totalPrice.toLocaleString("vi-VN")}đ</span>
              </div>
            </div>
            <div className="flex items-center gap-1.5 text-[11px] text-slate-500 pt-2 border-t border-slate-800">
              <ShieldCheck className="w-3.5 h-3.5 text-emerald-500" />
              <span>Giao dịch an toàn và bảo mật 100%</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
