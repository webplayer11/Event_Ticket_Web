import { useState, useEffect } from "react";
import { Link, Outlet, useNavigate, useSearchParams, useLocation } from "react-router-dom";
import { Ticket, User, Sparkles, Shield, Briefcase, Search, X, ChevronDown } from "lucide-react";
import { useAuthStore } from "../../stores/useAuthStore";

const navCategories = [
  { label: "Âm nhạc", slug: "am-nhac", icon: "♫" },
  { label: "Sân khấu", slug: "san-khau", icon: "✦" },
  { label: "Thể thao", slug: "the-thao", icon: "⚽" },
  { label: "Hội thảo", slug: "hoi-thao", icon: "◫" },
  { label: "Điện ảnh", slug: "dien-anh", icon: "▶" },
  { label: "Workshop", slug: "workshop", icon: "✎" },
];

export const CustomerLayout = () => {
  const { user } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const [searchQuery, setSearchQuery] = useState(searchParams.get("q") || "");

  useEffect(() => {
    setSearchQuery(searchParams.get("q") || "");
  }, [searchParams]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = searchQuery.trim();
    if (trimmed) {
      navigate(`/?q=${encodeURIComponent(trimmed)}`);
      requestAnimationFrame(() => {
        document.getElementById("events")?.scrollIntoView({ behavior: "smooth", block: "start" });
      });
    } else {
      navigate("/");
    }
  };

  const handleClearSearch = () => {
    setSearchQuery("");
    navigate("/");
  };

  const handleNavigateCategory = (slug: string) => {
    if (location.pathname === "/") {
      const el = document.getElementById(`category-${slug}`);
      if (el) {
        const yOffset = -120;
        const y = el.getBoundingClientRect().top + window.pageYOffset + yOffset;
        window.scrollTo({ top: y, behavior: "smooth" });
      }
    } else {
      navigate(`/#category-${slug}`);
    }
  };

  return (
    <div className="min-h-screen bg-[#0d1117] text-slate-100 flex flex-col font-sans">
      {/* Main Header */}
      <header className="sticky top-0 z-30 bg-[#0d1117]/95 backdrop-blur border-b border-slate-800/80">
        <div className="max-w-7xl mx-auto px-4 h-16 flex items-center justify-between gap-4">
          <div className="flex items-center gap-6 lg:gap-8 flex-1">
            <Link to="/" className="text-xl font-bold tracking-tight text-white flex items-center gap-2 shrink-0">
              <span className="w-8 h-8 rounded-lg bg-emerald-500 flex items-center justify-center text-black font-extrabold text-sm">
                EG
              </span>
              <span>EventGO</span>
            </Link>

            <nav className="hidden md:flex items-center gap-4 lg:gap-6 text-sm font-medium text-slate-300 flex-1">
              <Link to="/" className="hover:text-emerald-400 transition-colors shrink-0">
                Khám phá
              </Link>

              {/* Thanh tìm kiếm sự kiện ở giữa Khám phá và Vé của tôi */}
              <form onSubmit={handleSearchSubmit} className="relative flex items-center w-full max-w-xs lg:max-w-sm">
                <Search className="w-4 h-4 text-slate-400 absolute left-3 pointer-events-none" />
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Tìm sự kiện, địa điểm..."
                  className="w-full bg-slate-900/90 border border-slate-700/80 hover:border-slate-600 focus:border-emerald-500 rounded-full pl-9 pr-8 py-1.5 text-xs text-white placeholder:text-slate-500 focus:outline-none transition-all shadow-inner"
                />
                {searchQuery && (
                  <button
                    type="button"
                    onClick={handleClearSearch}
                    className="absolute right-2.5 text-slate-400 hover:text-white"
                    title="Xóa tìm kiếm"
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                )}
              </form>

              <Link to="/my-tickets" className="hover:text-emerald-400 transition-colors flex items-center gap-1.5 shrink-0">
                <Ticket className="w-4 h-4 text-emerald-400" />
                <span>Vé của tôi</span>
              </Link>
              <Link to="/organizer" className="hover:text-emerald-400 transition-colors flex items-center gap-1.5 text-slate-300 shrink-0">
                <Briefcase className="w-4 h-4 text-emerald-400" />
                <span>Dành cho BTC</span>
              </Link>
              {user?.role === "Admin" && (
                <Link to="/admin" className="hover:text-red-400 transition-colors flex items-center gap-1.5 text-red-400 shrink-0">
                  <Shield className="w-4 h-4" />
                  <span>Cổng Admin</span>
                </Link>
              )}
            </nav>
          </div>

          <div className="flex items-center gap-3 shrink-0">
            <Link
              to="/organizer/create-event"
              className="hidden sm:inline-flex items-center gap-1.5 bg-emerald-500 hover:bg-emerald-400 text-slate-950 px-3.5 py-1.5 rounded-lg text-sm font-medium transition-colors"
            >
              <Sparkles className="w-4 h-4" />
              <span>Tạo sự kiện</span>
            </Link>

            {user ? (
              <Link
                to="/account"
                className="flex items-center gap-2 p-1.5 px-3 rounded-lg border border-slate-700/60 hover:bg-slate-800 text-slate-200 text-sm transition-colors"
              >
                <div className="w-6 h-6 rounded-full bg-slate-700 flex items-center justify-center text-xs">
                  <User className="w-3.5 h-3.5" />
                </div>
                <span className="hidden sm:inline">{user.fullName}</span>
              </Link>
            ) : (
              <div className="flex items-center gap-2">
                <Link
                  to="/login"
                  className="px-3 py-1.5 rounded-lg text-sm font-medium text-slate-300 hover:text-white hover:bg-slate-800/80 transition-colors"
                >
                  Đăng nhập
                </Link>
                <Link
                  to="/register"
                  className="px-3 py-1.5 rounded-lg text-sm font-semibold bg-emerald-500 hover:bg-emerald-400 text-slate-950 transition-colors"
                >
                  Đăng ký
                </Link>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* Main Page Content */}
      <main className="flex-1">
        <Outlet />
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-800 bg-[#090d12] text-slate-400 text-sm py-10 mt-16">
        <div className="max-w-7xl mx-auto px-4 grid grid-cols-1 md:grid-cols-4 gap-8">
          <div>
            <div className="font-bold text-white text-base mb-3 flex items-center gap-2">
              <span className="w-6 h-6 rounded bg-emerald-500 flex items-center justify-center text-black font-extrabold text-xs">
                EG
              </span>
              EventGO
            </div>
            <p className="text-xs text-slate-400 leading-relaxed">
              Nền tảng đặt vé, quản lý sự kiện và giải pháp check-in chuyên nghiệp cho cộng đồng.
            </p>
          </div>

          <div>
            <h2 className="font-semibold text-slate-200 mb-3 text-xs uppercase tracking-wider">Khách hàng</h2>
            <ul className="space-y-2 text-xs">
              <li><Link to="/" className="hover:text-emerald-400">Khám phá sự kiện</Link></li>
              <li><Link to="/my-tickets" className="hover:text-emerald-400">Vé của tôi</Link></li>
              <li><Link to="/account" className="hover:text-emerald-400">Tài khoản</Link></li>
            </ul>
          </div>

          <div>
            <h2 className="font-semibold text-slate-200 mb-3 text-xs uppercase tracking-wider">Ban tổ chức</h2>
            <ul className="space-y-2 text-xs">
              <li><Link to="/organizer" className="hover:text-emerald-400">Kênh ban tổ chức</Link></li>
              <li><Link to="/organizer/create-event" className="hover:text-emerald-400">Đăng ký sự kiện mới</Link></li>
              <li><Link to="/organizer/check-in" className="hover:text-emerald-400">Ứng dụng soát vé</Link></li>
            </ul>
          </div>

          <div>
            <h2 className="font-semibold text-slate-200 mb-3 text-xs uppercase tracking-wider">Hệ thống</h2>
            <ul className="space-y-2 text-xs">
              <li><Link to="/admin" className="hover:text-emerald-400">Quản trị viên (Admin)</Link></li>
              <li><span className="text-slate-500">Phiên bản: 1.0.0 (EventGO Web)</span></li>
            </ul>
          </div>
        </div>
      </footer>
    </div>
  );
};
