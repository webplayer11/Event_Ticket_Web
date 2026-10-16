import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { Lock, Mail, ArrowRight, Loader2, AlertCircle } from "lucide-react";
import { useAuthStore } from "../../stores/useAuthStore";
import { api, ApiError, setAccessToken } from "../../services/api";
import { UserRole } from "../../types";

type BackendAuthResponse = {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: {
    id: string;
    fullName: string;
    email: string;
    phoneNumber?: string;
    systemRoles?: string[];
  };
};

type OrganizationResponse = {
  id: string;
  name: string;
  myRole: string;
};

export const LoginPage = () => {
  const navigate = useNavigate();
  const { login } = useAuthStore();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const auth = await api<BackendAuthResponse>("/auth/login", {
        method: "POST",
        body: JSON.stringify({ email: email.trim(), password }),
      });

      setAccessToken(auth.accessToken);

      // Xác định quyền thực tế của người dùng từ hệ thống
      let determinedRole: UserRole = "Customer";

      if (auth.user.systemRoles?.includes("PlatformAdmin")) {
        determinedRole = "Admin";
      } else {
        // Kiểm tra xem người dùng có thuộc tổ chức ban tổ chức nào không
        try {
          const orgs = await api<OrganizationResponse[]>("/organizations/mine");
          if (orgs && orgs.length > 0) {
            determinedRole = "Organizer";
          }
        } catch {
          // Mặc định là Customer nếu không lấy được thông tin tổ chức
          determinedRole = "Customer";
        }
      }

      login(
        {
          id: auth.user.id,
          email: auth.user.email,
          fullName: auth.user.fullName || auth.user.email.split("@")[0],
          role: determinedRole,
          phoneNumber: auth.user.phoneNumber,
        },
        auth.accessToken
      );

      // Điều hướng tương ứng với quyền của tài khoản
      if (determinedRole === "Admin") {
        navigate("/admin");
      } else if (determinedRole === "Organizer") {
        navigate("/organizer");
      } else {
        navigate("/");
      }
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Đăng nhập thất bại. Vui lòng kiểm tra lại email và mật khẩu.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#0d1117] flex items-center justify-center p-4 font-sans">
      <div className="max-w-md w-full bg-[#161b22] border border-slate-800 rounded-2xl p-8 space-y-6 shadow-2xl">
        <div className="text-center space-y-2">
          <div className="w-10 h-10 rounded-xl bg-emerald-500 text-slate-950 font-black text-lg flex items-center justify-center mx-auto">
            EG
          </div>
          <h1 className="text-2xl font-bold text-white tracking-tight">Đăng nhập EventGO</h1>
          <p className="text-xs text-slate-400">Nhập email và mật khẩu tài khoản của bạn</p>
        </div>

        {error && (
          <div className="p-3 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-xs flex items-start gap-2.5">
            <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
            <span className="leading-relaxed">{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="login-email" className="block text-xs text-slate-400 mb-1 font-medium">
              Email
            </label>
            <div className="relative">
              <Mail className="w-4 h-4 text-slate-500 absolute left-3 top-2.5" />
              <input
                id="login-email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="name@example.com"
                required
                className="w-full bg-slate-900 border border-slate-700 rounded-xl pl-9 pr-3.5 py-2 text-xs text-white focus:outline-none focus:border-emerald-500 transition-colors"
              />
            </div>
          </div>

          <div>
            <label htmlFor="login-password" className="block text-xs text-slate-400 mb-1 font-medium">
              Mật khẩu
            </label>
            <div className="relative">
              <Lock className="w-4 h-4 text-slate-500 absolute left-3 top-2.5" />
              <input
                id="login-password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Nhập mật khẩu..."
                required
                className="w-full bg-slate-900 border border-slate-700 rounded-xl pl-9 pr-3.5 py-2 text-xs text-white focus:outline-none focus:border-emerald-500 transition-colors"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 bg-emerald-500 hover:bg-emerald-400 disabled:opacity-60 text-slate-950 font-bold text-xs rounded-xl flex items-center justify-center gap-2 transition-all shadow-lg shadow-emerald-500/20"
          >
            {loading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>Đang xác thực...</span>
              </>
            ) : (
              <>
                <span>Đăng nhập</span>
                <ArrowRight className="w-4 h-4" />
              </>
            )}
          </button>
        </form>

        <div className="text-center text-xs text-slate-500 space-y-2 pt-2 border-t border-slate-800">
          <p>
            Chưa có tài khoản?{" "}
            <Link to="/register" className="text-emerald-400 hover:underline">
              Đăng ký ngay
            </Link>
          </p>
          <p>
            <Link to="/" className="text-slate-400 hover:text-white">
              Quay lại trang chủ
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
};
