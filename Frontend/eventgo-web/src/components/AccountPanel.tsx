import { useState, useEffect } from "react";
import type { FormEvent } from "react";
import { api, ApiError, setAccessToken } from "../services/api";

type UserResponse = {
  id: string;
  fullName: string;
  email: string;
  createdAt: string;
};

type AuthResponse = {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: UserResponse;
};

type OrganizationResponse = {
  id: string;
  name: string;
  myRole: string;
};

type Props = {
  open: boolean;
  onClose: () => void;
  onUserChange: (name: string | null) => void;
  onOpenRegister?: () => void;
  initialEmail?: string;
};

export default function AccountPanel({
  open,
  onClose,
  onUserChange,
  onOpenRegister,
  initialEmail = "",
}: Props) {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [organizations, setOrganizations] = useState<OrganizationResponse[]>([]);
  const [organizationsLoaded, setOrganizationsLoaded] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [emailInput, setEmailInput] = useState(initialEmail);

  useEffect(() => {
    if (initialEmail) {
      setEmailInput(initialEmail);
    }
  }, [initialEmail]);

  function clearSession() {
    setAccessToken(null);
    setUser(null);
    setOrganizations([]);
    setOrganizationsLoaded(false);
    onUserChange(null);
  }

  function reportError(caught: unknown) {
    if (caught instanceof ApiError && caught.status === 401) {
      clearSession();
    }

    setError(
      caught instanceof Error
        ? caught.message
        : "Đã xảy ra lỗi. Vui lòng thử lại."
    );
  }

  function handleSocialLogin(provider: string) {
    setError("");
    setMessage(`Đang đăng nhập bằng ${provider}...`);
    setBusy(true);

    setTimeout(() => {
      setBusy(false);
      setMessage("");
      const mockUser: UserResponse = {
        id: "social-123",
        fullName: `Khách hàng (${provider})`,
        email: `user.${provider.toLowerCase()}@example.com`,
        createdAt: new Date().toISOString(),
      };
      setUser(mockUser);
      onUserChange(mockUser.fullName);
      onClose();
    }, 1000);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const form = new FormData(event.currentTarget);
    const email = String(form.get("email") ?? "").trim();
    const password = String(form.get("password") ?? "");

    setBusy(true);
    setMessage("");
    setError("");

    try {
      clearSession();

      const auth = await api<AuthResponse>("/auth/login", {
        method: "POST",
        body: JSON.stringify({ email, password }),
      });

      setAccessToken(auth.accessToken);

      // Kiểm tra token thật sự dùng được trên endpoint bảo vệ.
      let currentUser: UserResponse;

      try {
        currentUser = await api<UserResponse>("/auth/me");
      } catch (caught) {
        setAccessToken(null);
        throw caught;
      }

      setUser(currentUser);
      onUserChange(currentUser.fullName);
      setMessage("");
      onClose();
      window.scrollTo({ top: 0, behavior: "smooth" });
    } catch (caught) {
      reportError(caught);
    } finally {
      setBusy(false);
    }
  }

  async function loadOrganizations() {
    setBusy(true);
    setError("");
    setMessage("");

    try {
      const result = await api<OrganizationResponse[]>(
        "/organizations/mine"
      );

      setOrganizations(result);
      setOrganizationsLoaded(true);
    } catch (caught) {
      reportError(caught);
    } finally {
      setBusy(false);
    }
  }

  // Component vẫn được giữ mounted để không mất phiên khi đóng popup.
  if (!open) return null;

  return (
    <div
      className="modal-backdrop"
      onMouseDown={() => {
        if (!busy) onClose();
      }}
    >
      <section
        className="login-modal"
        role="dialog"
        aria-modal="true"
        aria-label={user ? "Tài khoản" : "Đăng nhập EventGO"}
        onMouseDown={(event) => event.stopPropagation()}
        style={{ maxHeight: "90vh", overflowY: "auto" }}
      >
        <button
          type="button"
          className="modal-close"
          onClick={onClose}
          disabled={busy}
          aria-label="Đóng"
        >
          ×
        </button>

        <span className="brand-mark">E</span>

        <h2>
          {user ? `Xin chào, ${user.fullName}` : "Đăng nhập EventGO"}
        </h2>

        {error && (
          <p role="alert" style={{ color: "#b42318", whiteSpace: "pre-line" }}>
            {error}
          </p>
        )}

        {message && <p role="status">{message}</p>}

        {user ? (
          <div>
            <p>{user.email}</p>

            <button
              type="button"
              className="button primary"
              disabled={busy}
              onClick={loadOrganizations}
            >
              {busy ? "Đang tải..." : "Xem tổ chức của tôi"}
            </button>

            {organizationsLoaded && organizations.length === 0 && (
              <p style={{ marginTop: "12px" }}>Bạn chưa tham gia tổ chức nào.</p>
            )}

            <ul style={{ paddingLeft: "20px", marginTop: "12px" }}>
              {organizations.map((organization) => (
                <li key={organization.id}>
                  <strong>{organization.name}</strong> — {organization.myRole}
                </li>
              ))}
            </ul>

            <button
              type="button"
              className="text-link"
              style={{ marginTop: "16px", cursor: "pointer" }}
              disabled={busy}
              onClick={() => {
                clearSession();
                setMessage("");
                setError("");
              }}
            >
              Đăng xuất
            </button>
          </div>
        ) : (
          <>
            <div className="social-register-group modal-social-group">
              <button
                type="button"
                className="social-btn google-btn"
                disabled={busy}
                onClick={() => handleSocialLogin("Google")}
              >
                <svg className="social-icon" viewBox="0 0 24 24">
                  <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
                  <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
                  <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l2.85-2.22.81-.63z"/>
                  <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06l3.66 2.84c.87-2.6 3.3-4.52 6.16-4.52z"/>
                </svg>
                <span>Google</span>
              </button>

              <button
                type="button"
                className="social-btn facebook-btn"
                disabled={busy}
                onClick={() => handleSocialLogin("Facebook")}
              >
                <svg className="social-icon" viewBox="0 0 24 24" fill="#1877F2">
                  <path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/>
                </svg>
                <span>Facebook</span>
              </button>

              <button
                type="button"
                className="social-btn apple-btn"
                disabled={busy}
                onClick={() => handleSocialLogin("Apple")}
              >
                <svg className="social-icon" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.81-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M15.97 6.35c.66-.8 1.11-1.92.99-3.05-1 .04-2.16.67-2.85 1.48-.62.72-1.16 1.87-1.01 2.99 1.12.09 2.22-.58 2.87-1.42z"/>
                </svg>
                <span>Apple</span>
              </button>
            </div>

            <div className="form-divider">
              <span>hoặc dùng Email</span>
            </div>

            <form onSubmit={handleSubmit}>
              <label>
                Email
                <input
                  name="email"
                  type="email"
                  required
                  autoComplete="email"
                  disabled={busy}
                  value={emailInput}
                  onChange={(e) => setEmailInput(e.target.value)}
                />
              </label>

              <label>
                Mật khẩu
                <input
                  name="password"
                  type="password"
                  required
                  minLength={8}
                  autoComplete="current-password"
                  disabled={busy}
                />
              </label>

              <button className="button primary" disabled={busy}>
                {busy ? "Đang xử lý..." : "Đăng nhập"}
              </button>
            </form>

            <button
              type="button"
              className="text-link"
              style={{ marginTop: "16px", cursor: "pointer", display: "inline-block" }}
              disabled={busy}
              onClick={() => {
                onClose();
                if (onOpenRegister) {
                  onOpenRegister();
                } else {
                  window.history.pushState({}, "", "/register");
                  window.dispatchEvent(new PopStateEvent("popstate"));
                }
              }}
            >
              Chưa có tài khoản? Đăng ký ngay
            </button>
          </>
        )}
      </section>
    </div>
  );
}