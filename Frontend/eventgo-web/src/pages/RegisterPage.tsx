import { useState } from "react";
import type { FormEvent } from "react";
import { api, ApiError } from "../services/api";

type RegisterPageProps = {
  onNavigateHome: () => void;
  onNavigateLogin: (email?: string) => void;
};

export default function RegisterPage({
  onNavigateHome,
  onNavigateLogin,
}: RegisterPageProps) {
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [agreeTerms, setAgreeTerms] = useState(true);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const [socialMessage, setSocialMessage] = useState("");

  // Checks for password criteria
  const hasMinLength = password.length >= 8;
  const hasUpperLower = /[a-z]/.test(password) && /[A-Z]/.test(password);
  const hasNumber = /\d/.test(password);
  const hasSpecial = /[@$!%*?&#^()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password);
  const isMatched = password !== "" && password === confirmPassword;

  function handleSocialRegister(provider: "Google" | "Facebook" | "Apple") {
    setError("");
    setSocialMessage(`Đang kết nối tài khoản ${provider}...`);
    setBusy(true);

    setTimeout(() => {
      setBusy(false);
      setSocialMessage("");
      // Simulation of social login redirect or success
      setEmail(`user.${provider.toLowerCase()}@example.com`);
      setFullName(`Người dùng ${provider}`);
      setSuccess(true);
    }, 1200);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setSocialMessage("");

    if (!agreeTerms) {
      setError("Bạn cần đồng ý với Điều khoản sử dụng và Chính sách bảo mật.");
      return;
    }

    if (!hasMinLength || !hasUpperLower || !hasNumber || !hasSpecial) {
      setError(
        "Mật khẩu phải chứa ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
      );
      return;
    }

    if (!isMatched) {
      setError("Mật khẩu xác nhận không trùng khớp.");
      return;
    }

    setBusy(true);

    try {
      await api<{ message: string }>("/auth/register", {
        method: "POST",
        body: JSON.stringify({
          fullName: fullName.trim(),
          email: email.trim(),
          password,
          confirmPassword,
        }),
      });

      setSuccess(true);
    } catch (caught) {
      if (caught instanceof ApiError) {
        setError(caught.message);
      } else if (caught instanceof Error) {
        setError(caught.message);
      } else {
        setError("Đã xảy ra lỗi khi đăng ký. Vui lòng thử lại.");
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="register-page-wrapper">
      <header className="register-header">
        <div className="shell register-header-inner">
          <button
            type="button"
            className="register-brand-btn"
            onClick={onNavigateHome}
            aria-label="Về trang chủ EventGo"
          >
            <span className="brand-mark">E</span>
            <span className="brand-text">
              event<span>go</span>
            </span>
          </button>
          <button
            type="button"
            className="register-back-link"
            onClick={onNavigateHome}
          >
            ← Về trang chủ
          </button>
        </div>
      </header>

      <main className="register-main shell">
        <div className="register-card-layout">
          {/* Left Hero / Decorative Banner */}
          <aside className="register-banner">
            <div className="register-banner-overlay" />
            <div className="register-banner-content">
              <span className="register-badge">Tài khoản EventGo</span>
              <h1>Khám phá thế giới sự kiện đỉnh cao.</h1>
              <p>
                Tạo tài khoản để sở hữu vé điện tử nhanh chóng, lưu giữ sự kiện
                yêu thích và nhận nhiều ưu đãi độc quyền.
              </p>

              <div className="register-features">
                <div className="feature-item">
                  <span className="feature-icon">⚡</span>
                  <div>
                    <strong>Mua vé siêu tốc</strong>
                    <small>Thanh toán và nhận QR Code tức thì</small>
                  </div>
                </div>
                <div className="feature-item">
                  <span className="feature-icon">🔒</span>
                  <div>
                    <strong>Bảo mật 100%</strong>
                    <small>Thông tin và lịch sử giao dịch mã hóa an toàn</small>
                  </div>
                </div>
                <div className="feature-item">
                  <span className="feature-icon">✦</span>
                  <div>
                    <strong>Ưu đãi độc quyền</strong>
                    <small>Nhận mã giảm giá sớm cho thành viên</small>
                  </div>
                </div>
              </div>

              <div className="register-quote">
                <p>
                  “EventGo giúp tôi không bao giờ bỏ lỡ các đêm nhạc indie yêu thích tại Sài Gòn.”
                </p>
                <span>— Khán giả thân thiết</span>
              </div>
            </div>
          </aside>

          {/* Right Form Card */}
          <section className="register-form-container">
            {success ? (
              <div className="register-success-view">
                <div className="success-icon-wrap">
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                </div>
                <h2>Đăng ký thành công!</h2>
                <p>
                  Tài khoản <strong>{email || fullName}</strong> đã được tạo thành công. Bạn có thể đăng nhập ngay bây giờ để bắt đầu trải nghiệm.
                </p>
                <div className="success-actions">
                  <button
                    type="button"
                    className="button primary full-width"
                    onClick={() => onNavigateLogin(email)}
                  >
                    Đăng nhập ngay →
                  </button>
                  <button
                    type="button"
                    className="text-link"
                    onClick={onNavigateHome}
                  >
                    Quay về trang chủ
                  </button>
                </div>
              </div>
            ) : (
              <div className="register-form-wrapper">
                <div className="register-form-header">
                  <h2>Tạo tài khoản mới</h2>
                  <p>Chọn phương thức đăng ký nhanh hoặc dùng Email</p>
                </div>

                {/* Quick Social Registration Buttons */}
                <div className="social-register-group">
                  <button
                    type="button"
                    className="social-btn google-btn"
                    disabled={busy}
                    onClick={() => handleSocialRegister("Google")}
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
                    onClick={() => handleSocialRegister("Facebook")}
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
                    onClick={() => handleSocialRegister("Apple")}
                  >
                    <svg className="social-icon" viewBox="0 0 24 24" fill="currentColor">
                      <path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.81-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M15.97 6.35c.66-.8 1.11-1.92.99-3.05-1 .04-2.16.67-2.85 1.48-.62.72-1.16 1.87-1.01 2.99 1.12.09 2.22-.58 2.87-1.42z"/>
                    </svg>
                    <span>Apple</span>
                  </button>
                </div>

                <div className="form-divider">
                  <span>Hoặc đăng ký bằng Email</span>
                </div>

                {socialMessage && (
                  <div className="social-status-info">
                    <span className="spinner" /> {socialMessage}
                  </div>
                )}

                {error && (
                  <div className="register-error-alert" role="alert">
                    <span className="error-icon">⚠️</span>
                    <span>{error}</span>
                  </div>
                )}

                <form onSubmit={handleSubmit} noValidate>
                  <div className="form-group">
                    <label htmlFor="reg-fullname">Họ và tên</label>
                    <div className="input-with-icon">
                      <svg
                        className="input-icon"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.8"
                      >
                        <circle cx="12" cy="8" r="4" />
                        <path d="M4.5 21a7.5 7.5 0 0 1 15 0" />
                      </svg>
                      <input
                        id="reg-fullname"
                        type="text"
                        required
                        placeholder="Nguyễn Văn A"
                        value={fullName}
                        onChange={(e) => setFullName(e.target.value)}
                        disabled={busy}
                        autoComplete="name"
                      />
                    </div>
                  </div>

                  <div className="form-group">
                    <label htmlFor="reg-email">Email</label>
                    <div className="input-with-icon">
                      <svg
                        className="input-icon"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.8"
                      >
                        <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
                        <polyline points="22,6 12,13 2,6" />
                      </svg>
                      <input
                        id="reg-email"
                        type="email"
                        required
                        placeholder="name@example.com"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        disabled={busy}
                        autoComplete="email"
                      />
                    </div>
                  </div>

                  <div className="form-group">
                    <label htmlFor="reg-password">Mật khẩu</label>
                    <div className="input-with-icon">
                      <svg
                        className="input-icon"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.8"
                      >
                        <rect
                          x="3"
                          y="11"
                          width="18"
                          height="11"
                          rx="2"
                          ry="2"
                        />
                        <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                      </svg>
                      <input
                        id="reg-password"
                        type={showPassword ? "text" : "password"}
                        required
                        placeholder="Nhập mật khẩu"
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        disabled={busy}
                        autoComplete="new-password"
                      />
                      <button
                        type="button"
                        className="toggle-password-btn"
                        onClick={() => setShowPassword(!showPassword)}
                        tabIndex={-1}
                        aria-label={
                          showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"
                        }
                      >
                        {showPassword ? "🙈" : "👁️"}
                      </button>
                    </div>
                  </div>

                  <div className="form-group">
                    <label htmlFor="reg-confirm-password">
                      Xác nhận mật khẩu
                    </label>
                    <div className="input-with-icon">
                      <svg
                        className="input-icon"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.8"
                      >
                        <rect
                          x="3"
                          y="11"
                          width="18"
                          height="11"
                          rx="2"
                          ry="2"
                        />
                        <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                      </svg>
                      <input
                        id="reg-confirm-password"
                        type={showConfirmPassword ? "text" : "password"}
                        required
                        placeholder="Nhập lại mật khẩu"
                        value={confirmPassword}
                        onChange={(e) => setConfirmPassword(e.target.value)}
                        disabled={busy}
                        autoComplete="new-password"
                      />
                      <button
                        type="button"
                        className="toggle-password-btn"
                        onClick={() =>
                          setShowConfirmPassword(!showConfirmPassword)
                        }
                        tabIndex={-1}
                        aria-label={
                          showConfirmPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"
                        }
                      >
                        {showConfirmPassword ? "🙈" : "👁️"}
                      </button>
                    </div>
                  </div>

                  {/* Dynamic Password Checklists */}
                  {password.length > 0 && (
                    <div className="password-checklist">
                      <div className={hasMinLength ? "check-pass" : "check-fail"}>
                        <span>{hasMinLength ? "✓" : "○"}</span> Ít nhất 8 ký tự
                      </div>
                      <div className={hasUpperLower ? "check-pass" : "check-fail"}>
                        <span>{hasUpperLower ? "✓" : "○"}</span> Chữ hoa & chữ thường
                      </div>
                      <div className={hasNumber ? "check-pass" : "check-fail"}>
                        <span>{hasNumber ? "✓" : "○"}</span> Chứa chữ số
                      </div>
                      <div className={hasSpecial ? "check-pass" : "check-fail"}>
                        <span>{hasSpecial ? "✓" : "○"}</span> Ký tự đặc biệt (@$!%*?&)
                      </div>
                      {confirmPassword.length > 0 && (
                        <div className={isMatched ? "check-pass" : "check-fail"}>
                          <span>{isMatched ? "✓" : "○"}</span> Mật khẩu khớp
                        </div>
                      )}
                    </div>
                  )}

                  <div className="form-group checkbox-group">
                    <label className="checkbox-label">
                      <input
                        type="checkbox"
                        checked={agreeTerms}
                        onChange={(e) => setAgreeTerms(e.target.checked)}
                        disabled={busy}
                      />
                      <span>
                        Tôi đồng ý với{" "}
                        <a href="#" onClick={(e) => e.preventDefault()}>
                          Điều khoản dịch vụ
                        </a>{" "}
                        và{" "}
                        <a href="#" onClick={(e) => e.preventDefault()}>
                          Chính sách bảo mật
                        </a>{" "}
                        của EventGo
                      </span>
                    </label>
                  </div>

                  <button
                    type="submit"
                    className="button primary full-width register-submit-btn"
                    disabled={busy}
                  >
                    {busy ? (
                      <span className="spinner-text">
                        <span className="spinner" /> Đang đăng ký...
                      </span>
                    ) : (
                      "Đăng ký bằng Email"
                    )}
                  </button>
                </form>

                <div className="register-footer-switch">
                  <span>Đã có tài khoản?</span>{" "}
                  <button
                    type="button"
                    className="text-link-bold"
                    onClick={() => onNavigateLogin(email)}
                  >
                    Đăng nhập ngay
                  </button>
                </div>
              </div>
            )}
          </section>
        </div>
      </main>
    </div>
  );
}
