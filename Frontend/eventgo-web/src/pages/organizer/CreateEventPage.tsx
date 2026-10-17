import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import {
  Calendar,
  MapPin,
  Ticket,
  Plus,
  Trash2,
  Clock,
  Building2,
  Sparkles,
  ArrowLeft,
  CheckCircle2,
  AlertCircle,
  Loader2,
  Globe,
  FileText,
} from "lucide-react";
import { getEventCategories, createEvent } from "../../services/eventService";
import type { EventCategoryResponse, TicketTypeRequest } from "../../types";

interface TicketTierForm {
  name: string;
  price: number;
  totalQuantity: number;
  maxQuantityPerOrder: number;
  saleStartsAt: string;
  saleEndsAt: string;
}

export const CreateEventPage = () => {
  const navigate = useNavigate();

  // Categories state
  const [categories, setCategories] = useState<EventCategoryResponse[]>([]);
  const [loadingCategories, setLoadingCategories] = useState(true);

  // Form states
  const [title, setTitle] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [organizerName, setOrganizerName] = useState("");
  const [organizerInfo, setOrganizerInfo] = useState("");
  const [addressType, setAddressType] = useState<"Offline" | "Online">("Offline");
  const [venueName, setVenueName] = useState("");
  const [city, setCity] = useState("TP. Hồ Chí Minh");
  const [address, setAddress] = useState("");
  const [onlineLink, setOnlineLink] = useState("");
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");
  const [description, setDescription] = useState("");

  // Ticket tiers state
  const [ticketTypes, setTicketTypes] = useState<TicketTierForm[]>([
    {
      name: "Vé tiêu chuẩn (Standard)",
      price: 0,
      totalQuantity: 100,
      maxQuantityPerOrder: 5,
      saleStartsAt: "",
      saleEndsAt: "",
    },
  ]);

  // Submission states
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState(false);

  useEffect(() => {
    getEventCategories()
      .then((data) => {
        setCategories(data);
        if (data.length > 0) {
          setCategoryId(data[0].id);
        }
      })
      .catch(() => {
        // Fallback default categories if server offline
        setCategories([
          { id: "am-nhac", name: "Âm nhạc", slug: "am-nhac" },
          { id: "the-thao", name: "Thể thao", slug: "the-thao" },
          { id: "hoi-thao", name: "Hội thảo", slug: "hoi-thao" },
          { id: "nghe-thuat", name: "Sân khấu & Nghệ thuật", slug: "san-khau" },
        ]);
      })
      .finally(() => setLoadingCategories(false));
  }, []);

  // Set default sale dates whenever event dates change
  useEffect(() => {
    if (startsAt && endsAt) {
      setTicketTypes((prev) =>
        prev.map((t) => ({
          ...t,
          saleStartsAt: t.saleStartsAt || new Date().toISOString().slice(0, 16),
          saleEndsAt: t.saleEndsAt || endsAt,
        }))
      );
    }
  }, [startsAt, endsAt]);

  const handleAddTicketTier = () => {
    setTicketTypes((prev) => [
      ...prev,
      {
        name: `Hạng vé ${prev.length + 1}`,
        price: 150000,
        totalQuantity: 50,
        maxQuantityPerOrder: 4,
        saleStartsAt: startsAt ? new Date().toISOString().slice(0, 16) : "",
        saleEndsAt: endsAt || "",
      },
    ]);
  };

  const handleRemoveTicketTier = (index: number) => {
    if (ticketTypes.length <= 1) {
      alert("Sự kiện cần tối thiểu 1 hạng vé mở bán.");
      return;
    }
    setTicketTypes((prev) => prev.filter((_, i) => i !== index));
  };

  const handleUpdateTicketTier = (index: number, field: keyof TicketTierForm, value: unknown) => {
    setTicketTypes((prev) =>
      prev.map((tier, i) => (i === index ? { ...tier, [field]: value } : tier))
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    // Basic Validations
    if (!title.trim()) {
      setErrorMsg("Vui lòng nhập tên sự kiện.");
      return;
    }
    if (!categoryId) {
      setErrorMsg("Vui lòng chọn thể loại sự kiện.");
      return;
    }
    if (!startsAt || !endsAt) {
      setErrorMsg("Vui lòng chọn đầy đủ thời gian bắt đầu và kết thúc sự kiện.");
      return;
    }
    if (new Date(startsAt) >= new Date(endsAt)) {
      setErrorMsg("Thời gian kết thúc phải diễn ra sau thời gian bắt đầu.");
      return;
    }
    if (addressType === "Offline" && (!venueName.trim() || !address.trim())) {
      setErrorMsg("Vui lòng nhập tên địa điểm và địa chỉ sự kiện.");
      return;
    }
    if (addressType === "Online" && !onlineLink.trim()) {
      setErrorMsg("Vui lòng nhập đường link tham gia sự kiện trực tuyến.");
      return;
    }
    if (!organizerName.trim()) {
      setErrorMsg("Vui lòng nhập tên Ban tổ chức.");
      return;
    }
    if (!description.trim()) {
      setErrorMsg("Vui lòng nhập mô tả chi tiết sự kiện.");
      return;
    }

    // Validate ticket tiers
    for (const [idx, tier] of ticketTypes.entries()) {
      if (!tier.name.trim()) {
        setErrorMsg(`Hạng vé thứ #${idx + 1} chưa có tên.`);
        return;
      }
      if (tier.price < 0) {
        setErrorMsg(`Giá vé của hạng "${tier.name}" không hợp lệ.`);
        return;
      }
      if (tier.totalQuantity <= 0) {
        setErrorMsg(`Số lượng vé của hạng "${tier.name}" phải lớn hơn 0.`);
        return;
      }
    }

    setSubmitting(true);

    try {
      const payloadTicketTypes: TicketTypeRequest[] = ticketTypes.map((tier) => ({
        name: tier.name.trim(),
        price: Number(tier.price),
        totalQuantity: Number(tier.totalQuantity),
        maxQuantityPerOrder: Number(tier.maxQuantityPerOrder) || 5,
        saleStartsAt: tier.saleStartsAt
          ? new Date(tier.saleStartsAt).toISOString()
          : new Date().toISOString(),
        saleEndsAt: tier.saleEndsAt
          ? new Date(tier.saleEndsAt).toISOString()
          : new Date(endsAt).toISOString(),
      }));

      await createEvent({
        title: title.trim(),
        description: description.trim(),
        categoryId,
        venueName: addressType === "Offline" ? venueName.trim() : "Trực tuyến (Online)",
        address: addressType === "Offline" ? address.trim() : onlineLink.trim(),
        city: addressType === "Offline" ? city.trim() : "Online",
        startsAt: new Date(startsAt).toISOString(),
        endsAt: new Date(endsAt).toISOString(),
        organizerName: organizerName.trim(),
        organizerInfo: organizerInfo.trim() || organizerName.trim(),
        ticketTypes: payloadTicketTypes,
      });

      setIsSuccess(true);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string; error?: string } }; message?: string })
          ?.response?.data?.message ||
        (err as { message?: string })?.message ||
        "Tạo sự kiện thất bại. Vui lòng kiểm tra lại thông tin.";
      setErrorMsg(msg);
    } finally {
      setSubmitting(false);
    }
  };

  // SUCCESS SCREEN
  if (isSuccess) {
    return (
      <div className="mx-auto max-w-2xl py-16 px-4 text-center space-y-6">
        <div className="w-20 h-20 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center mx-auto ring-8 ring-emerald-500/10">
          <CheckCircle2 className="w-10 h-10" />
        </div>
        <div className="space-y-2">
          <h1 className="text-2xl font-bold text-white">Sự kiện đã được gửi duyệt!</h1>
          <p className="text-sm text-slate-400 max-w-md mx-auto">
            Sự kiện <span className="text-emerald-400 font-semibold">"{title}"</span> đã được
            tạo thành công và đang chuyển sang hàng đợi kiểm duyệt của Admin. Khi được duyệt, vé sẽ tự
            động mở bán trên trang chủ.
          </p>
        </div>

        <div className="flex flex-col sm:flex-row items-center justify-center gap-3 pt-4">
          <button
            onClick={() => navigate("/organizer/events")}
            className="w-full sm:w-auto px-6 py-2.5 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-sm transition-colors shadow-lg shadow-emerald-500/20"
          >
            Xem danh sách sự kiện của tôi
          </button>
          <button
            onClick={() => {
              setIsSuccess(false);
              setTitle("");
              setDescription("");
              setStartsAt("");
              setEndsAt("");
              setVenueName("");
              setAddress("");
            }}
            className="w-full sm:w-auto px-6 py-2.5 rounded-xl bg-slate-900 border border-slate-700 hover:bg-slate-800 text-white font-semibold text-sm transition-colors"
          >
            Tạo thêm sự kiện khác
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto px-4 py-8 space-y-8">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-slate-800/80 pb-5">
        <div>
          <button
            type="button"
            onClick={() => navigate(-1)}
            className="inline-flex items-center gap-1.5 text-xs text-slate-400 hover:text-white transition-colors mb-2"
          >
            <ArrowLeft className="w-3.5 h-3.5" />
            <span>Quay lại</span>
          </button>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <Sparkles className="w-6 h-6 text-emerald-400" />
            Tạo sự kiện & Mở bán vé
          </h1>
          <p className="text-xs text-slate-400 mt-1">
            Thiết lập thông tin sự kiện, thời gian, địa điểm và cấu hình các hạng vé mở bán
          </p>
        </div>
      </div>

      {/* Error alert */}
      {errorMsg && (
        <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/30 text-red-400 text-xs flex items-center gap-3">
          <AlertCircle className="w-5 h-5 shrink-0" />
          <span>{errorMsg}</span>
        </div>
      )}

      {/* Main Form */}
      <form onSubmit={handleSubmit} className="space-y-8">
        {/* 1. THÔNG TIN CHÍNH */}
        <div className="bg-[#12181A] border border-[#2d2d2d] rounded-2xl p-6 space-y-5">
          <div className="flex items-center gap-2 text-sm font-bold text-emerald-400 uppercase tracking-wider">
            <FileText className="w-4 h-4" />
            <span>1. Thông tin tổng quan sự kiện</span>
          </div>

          <div className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-slate-300 mb-1.5">
                Tên sự kiện <span className="text-emerald-400">*</span>
              </label>
              <input
                type="text"
                placeholder="VD: Neon Pulse — Đại nhạc hội Live in Saigon 2026"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                maxLength={200}
                required
                className="w-full bg-slate-900/90 border border-slate-700 rounded-xl px-4 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500"
              />
              <div className="text-right text-[11px] text-slate-500 mt-1">
                {title.length}/200 ký tự
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1.5">
                  Thể loại sự kiện <span className="text-emerald-400">*</span>
                </label>
                {loadingCategories ? (
                  <div className="flex items-center gap-2 text-xs text-slate-400 py-2.5">
                    <Loader2 className="w-4 h-4 animate-spin text-emerald-400" />
                    Đang tải danh mục...
                  </div>
                ) : (
                  <select
                    value={categoryId}
                    onChange={(e) => setCategoryId(e.target.value)}
                    className="w-full bg-slate-900/90 border border-slate-700 rounded-xl px-3 py-2.5 text-sm text-white focus:outline-none focus:border-emerald-500"
                  >
                    {categories.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                )}
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1.5">
                  Tên Ban tổ chức <span className="text-emerald-400">*</span>
                </label>
                <div className="relative">
                  <Building2 className="w-4 h-4 text-slate-500 absolute left-3.5 top-3" />
                  <input
                    type="text"
                    placeholder="VD: Cty Cổ phần Giải trí EventGO"
                    value={organizerName}
                    onChange={(e) => setOrganizerName(e.target.value)}
                    required
                    className="w-full bg-slate-900/90 border border-slate-700 rounded-xl pl-10 pr-4 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500"
                  />
                </div>
              </div>
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-300 mb-1.5">
                Giới thiệu Ban tổ chức (Tùy chọn)
              </label>
              <textarea
                rows={2}
                placeholder="Thông tin ngắn về đơn vị tổ chức, kinh nghiệm tổ chức các sự kiện..."
                value={organizerInfo}
                onChange={(e) => setOrganizerInfo(e.target.value)}
                className="w-full bg-slate-900/90 border border-slate-700 rounded-xl p-3 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500"
              />
            </div>
          </div>
        </div>

        {/* 2. THỜI GIAN VÀ ĐỊA ĐIỂM */}
        <div className="bg-[#12181A] border border-[#2d2d2d] rounded-2xl p-6 space-y-5">
          <div className="flex items-center gap-2 text-sm font-bold text-emerald-400 uppercase tracking-wider">
            <Clock className="w-4 h-4" />
            <span>2. Thời gian & Địa điểm tổ chức</span>
          </div>

          {/* Time Picker */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-slate-300 mb-1.5">
                Thời gian bắt đầu sự kiện <span className="text-emerald-400">*</span>
              </label>
              <div className="relative">
                <input
                  type="datetime-local"
                  value={startsAt}
                  onChange={(e) => setStartsAt(e.target.value)}
                  required
                  className="w-full bg-slate-900/90 border border-slate-700 rounded-xl px-3 py-2.5 text-sm text-white focus:outline-none focus:border-emerald-500"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-300 mb-1.5">
                Thời gian kết thúc sự kiện <span className="text-emerald-400">*</span>
              </label>
              <div className="relative">
                <input
                  type="datetime-local"
                  value={endsAt}
                  onChange={(e) => setEndsAt(e.target.value)}
                  required
                  className="w-full bg-slate-900/90 border border-slate-700 rounded-xl px-3 py-2.5 text-sm text-white focus:outline-none focus:border-emerald-500"
                />
              </div>
            </div>
          </div>

          {/* Address Type Switch */}
          <div className="pt-2">
            <label className="block text-xs font-medium text-slate-300 mb-2">
              Hình thức tổ chức
            </label>
            <div className="grid grid-cols-2 gap-3 max-w-sm">
              <button
                type="button"
                onClick={() => setAddressType("Offline")}
                className={`py-2 px-4 rounded-xl border text-xs font-semibold flex items-center justify-center gap-2 transition-all ${addressType === "Offline"
                    ? "bg-emerald-500/15 border-emerald-500 text-emerald-400"
                    : "bg-slate-900 border-slate-700 text-slate-400 hover:border-slate-600"
                  }`}
              >
                <MapPin className="w-4 h-4" />
                Trực tiếp (Offline)
              </button>
              <button
                type="button"
                onClick={() => setAddressType("Online")}
                className={`py-2 px-4 rounded-xl border text-xs font-semibold flex items-center justify-center gap-2 transition-all ${addressType === "Online"
                    ? "bg-emerald-500/15 border-emerald-500 text-emerald-400"
                    : "bg-slate-900 border-slate-700 text-slate-400 hover:border-slate-600"
                  }`}
              >
                <Globe className="w-4 h-4" />
                Trực tuyến (Online)
              </button>
            </div>
          </div>

          {/* Offline details */}
          {addressType === "Offline" ? (
            <div className="space-y-4 pt-2">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1.5">
                    Tên địa điểm / Tòa nhà <span className="text-emerald-400">*</span>
                  </label>
                  <input
                    type="text"
                    placeholder="VD: Nhà thi đấu Phú Thọ, Trung tâm Hội nghị SECC..."
                    value={venueName}
                    onChange={(e) => setVenueName(e.target.value)}
                    required={addressType === "Offline"}
                    className="w-full bg-slate-900/90 border border-slate-700 rounded-xl px-4 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1.5">
                    Tỉnh / Thành phố <span className="text-emerald-400">*</span>
                  </label>
                  <input
                    type="text"
                    placeholder="VD: TP. Hồ Chí Minh, Hà Nội, Đà Nẵng..."
                    value={city}
                    onChange={(e) => setCity(e.target.value)}
                    required={addressType === "Offline"}
                    className="w-full bg-slate-900/90 border border-slate-700 rounded-xl px-4 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1.5">
                  Địa chỉ chi tiết <span className="text-emerald-400">*</span>
                </label>
                <input
                  type="text"
                  placeholder="VD: Số 1 Lữ Gia, Phường 15, Quận 11"
                  value={address}
                  onChange={(e) => setAddress(e.target.value)}
                  required={addressType === "Offline"}
                  className="w-full bg-slate-900/90 border border-slate-700 rounded-xl px-4 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500"
                />
              </div>
            </div>
          ) : (
            <div className="pt-2">
              <label className="block text-xs font-medium text-slate-300 mb-1.5">
                Link tham gia hoặc Nền tảng phát sóng <span className="text-emerald-400">*</span>
              </label>
              <input
                type="text"
                placeholder="VD: https://meet.google.com/abc-xyz hoặc Zoom Link"
                value={onlineLink}
                onChange={(e) => setOnlineLink(e.target.value)}
                required={addressType === "Online"}
                className="w-full bg-slate-900/90 border border-slate-700 rounded-xl px-4 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500"
              />
            </div>
          )}
        </div>

        {/* 3. MÔ TẢ CHI TIẾT */}
        <div className="bg-[#12181A] border border-[#2d2d2d] rounded-2xl p-6 space-y-4">
          <div className="flex items-center gap-2 text-sm font-bold text-emerald-400 uppercase tracking-wider">
            <Calendar className="w-4 h-4" />
            <span>3. Nội dung & Giới thiệu sự kiện</span>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-300 mb-1.5">
              Mô tả chi tiết sự kiện <span className="text-emerald-400">*</span>
            </label>
            <textarea
              rows={6}
              placeholder="Giới thiệu chương trình biểu diễn, danh sách nghệ sĩ/diễn giả tham dự, lịch trình chi tiết và các quy định tham gia sự kiện..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              required
              className="w-full bg-slate-900/90 border border-slate-700 rounded-xl p-4 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 leading-relaxed"
            />
          </div>
        </div>

        {/* 4. CẤU HÌNH CÁC HẠNG VÉ */}
        <div className="bg-[#12181A] border border-[#2d2d2d] rounded-2xl p-6 space-y-5">
          <div className="flex items-center justify-between border-b border-slate-800 pb-4">
            <div className="flex items-center gap-2 text-sm font-bold text-emerald-400 uppercase tracking-wider">
              <Ticket className="w-4 h-4" />
              <span>4. Danh sách hạng vé mở bán</span>
            </div>
            <button
              type="button"
              onClick={handleAddTicketTier}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 hover:bg-emerald-500/20 text-xs font-bold transition-all"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>Thêm hạng vé</span>
            </button>
          </div>

          <div className="space-y-4">
            {ticketTypes.map((tier, index) => (
              <div
                key={index}
                className="p-4 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3 relative group"
              >
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-slate-300">
                    Hạng vé #{index + 1}
                  </span>
                  {ticketTypes.length > 1 && (
                    <button
                      type="button"
                      onClick={() => handleRemoveTicketTier(index)}
                      className="p-1 rounded-md text-red-400/70 hover:text-red-400 hover:bg-red-500/10 transition-colors"
                      title="Xóa hạng vé này"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  )}
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <div>
                    <label className="block text-[11px] text-slate-400 mb-1">
                      Tên hạng vé <span className="text-emerald-400">*</span>
                    </label>
                    <input
                      type="text"
                      placeholder="VD: Hạng VIP, Phổ thông GA..."
                      value={tier.name}
                      onChange={(e) => handleUpdateTicketTier(index, "name", e.target.value)}
                      required
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                    />
                  </div>

                  <div>
                    <div className="flex items-center justify-between mb-1">
                      <label className="block text-[11px] text-slate-400">
                        Giá vé (VNĐ) <span className="text-emerald-400">*</span>
                      </label>
                      <button
                        type="button"
                        onClick={() => handleUpdateTicketTier(index, "price", 0)}
                        className="text-[10px] text-emerald-400 hover:underline"
                      >
                        Miễn phí (0đ)
                      </button>
                    </div>
                    <input
                      type="number"
                      min={0}
                      step={1000}
                      placeholder="0"
                      value={tier.price}
                      onChange={(e) =>
                        handleUpdateTicketTier(index, "price", Math.max(0, Number(e.target.value)))
                      }
                      required
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                    />
                  </div>

                  <div>
                    <label className="block text-[11px] text-slate-400 mb-1">
                      Số lượng vé phát hành <span className="text-emerald-400">*</span>
                    </label>
                    <input
                      type="number"
                      min={1}
                      placeholder="100"
                      value={tier.totalQuantity}
                      onChange={(e) =>
                        handleUpdateTicketTier(
                          index,
                          "totalQuantity",
                          Math.max(1, Number(e.target.value))
                        )
                      }
                      required
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 pt-1 border-t border-slate-800/60">
                  <div>
                    <label className="block text-[11px] text-slate-400 mb-1">
                      Tối đa vé / Đơn hàng
                    </label>
                    <input
                      type="number"
                      min={1}
                      max={20}
                      value={tier.maxQuantityPerOrder}
                      onChange={(e) =>
                        handleUpdateTicketTier(
                          index,
                          "maxQuantityPerOrder",
                          Math.max(1, Number(e.target.value))
                        )
                      }
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                    />
                  </div>

                  <div>
                    <label className="block text-[11px] text-slate-400 mb-1">
                      Thời gian mở bán vé
                    </label>
                    <input
                      type="datetime-local"
                      value={tier.saleStartsAt}
                      onChange={(e) =>
                        handleUpdateTicketTier(index, "saleStartsAt", e.target.value)
                      }
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-emerald-500"
                    />
                  </div>

                  <div>
                    <label className="block text-[11px] text-slate-400 mb-1">
                      Thời gian đóng bán vé
                    </label>
                    <input
                      type="datetime-local"
                      value={tier.saleEndsAt}
                      onChange={(e) => handleUpdateTicketTier(index, "saleEndsAt", e.target.value)}
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-emerald-500"
                    />
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* SUBMIT BUTTON */}
        <div className="flex items-center justify-end gap-4 pt-4 border-t border-slate-800">
          <button
            type="button"
            onClick={() => navigate("/organizer/events")}
            disabled={submitting}
            className="px-5 py-2.5 rounded-xl border border-slate-700 hover:bg-slate-800 text-slate-300 text-xs font-semibold transition-colors disabled:opacity-50"
          >
            Hủy bỏ
          </button>

          <button
            type="submit"
            disabled={submitting}
            className="inline-flex items-center gap-2 px-6 py-2.5 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-xs transition-all shadow-lg shadow-emerald-500/20 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {submitting ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>Đang gửi sự kiện lên hệ thống...</span>
              </>
            ) : (
              <>
                <Sparkles className="w-4 h-4" />
                <span>Tạo sự kiện & Gửi duyệt</span>
              </>
            )}
          </button>
        </div>
      </form>
    </div>
  );
};
