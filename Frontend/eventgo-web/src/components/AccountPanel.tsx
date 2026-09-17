import { useState } from "react";
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
};

export default function AccountPanel({
    open,
    onClose,
    onUserChange,
}: Props) {
    const [mode, setMode] = useState<"login" | "register">("login");
    const [user, setUser] = useState<UserResponse | null>(null);
    const [organizations, setOrganizations] =
        useState<OrganizationResponse[]>([]);

    const [organizationsLoaded, setOrganizationsLoaded] = useState(false);
    const [busy, setBusy] = useState(false);
    const [message, setMessage] = useState("");
    const [error, setError] = useState("");

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
                : "Đã xảy ra lỗi. Vui lòng thử lại.",
        );
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
            if (mode === "register") {
                const confirmPassword =
                    String(form.get("confirmPassword") ?? "");

                if (password !== confirmPassword) {
                    throw new Error("Mật khẩu xác nhận không khớp.");
                }

                await api<{ message: string }>("/auth/register", {
                    method: "POST",
                    body: JSON.stringify({
                        fullName: String(form.get("fullName") ?? "").trim(),
                        email,
                        password,
                        confirmPassword,
                    }),
                });

                setMode("login");
                setMessage("Đăng ký thành công. Hãy đăng nhập bằng tài khoản vừa tạo.");
            } else {
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
            }
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
                "/organizations/mine",
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
                aria-label={user ? "Tài khoản" : "Đăng nhập hoặc đăng ký"}
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
                    {user
                        ? `Xin chào, ${user.fullName}`
                        : mode === "login"
                            ? "Đăng nhập EventGO"
                            : "Tạo tài khoản"}
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
                            <p>Bạn chưa tham gia tổ chức nào.</p>
                        )}

                        <ul>
                            {organizations.map((organization) => (
                                <li key={organization.id}>
                                    <strong>{organization.name}</strong>
                                    {" — "}
                                    {organization.myRole}
                                </li>
                            ))}
                        </ul>

                        <button
                            type="button"
                            className="text-link"
                            disabled={busy}
                            onClick={() => {
                                clearSession();
                                setMessage("");
                                setError("");
                                setMode("login");
                            }}
                        >
                            Đăng xuất
                        </button>
                    </div>
                ) : (
                    <>
                        <form key={mode} onSubmit={handleSubmit}>
                            {mode === "register" && (
                                <label>
                                    Họ tên
                                    <input
                                        name="fullName"
                                        required
                                        minLength={2}
                                        maxLength={100}
                                        autoComplete="name"
                                        disabled={busy}
                                    />
                                </label>
                            )}

                            <label>
                                Email
                                <input
                                    name="email"
                                    type="email"
                                    required
                                    autoComplete="email"
                                    disabled={busy}
                                />
                            </label>

                            <label>
                                Mật khẩu
                                <input
                                    name="password"
                                    type="password"
                                    required
                                    minLength={8}
                                    autoComplete={
                                        mode === "login" ? "current-password" : "new-password"
                                    }
                                    disabled={busy}
                                />
                            </label>

                            {mode === "register" && (
                                <>
                                    <label>
                                        Xác nhận mật khẩu
                                        <input
                                            name="confirmPassword"
                                            type="password"
                                            required
                                            minLength={8}
                                            autoComplete="new-password"
                                            disabled={busy}
                                        />
                                    </label>
                                    <p>
                                        Mật khẩu có ít nhất 8 ký tự, gồm chữ hoa, chữ thường,
                                        số và ký tự đặc biệt.
                                    </p>
                                </>
                            )}

                            <button className="button primary" disabled={busy}>
                                {busy
                                    ? "Đang xử lý..."
                                    : mode === "login"
                                        ? "Đăng nhập"
                                        : "Đăng ký"}
                            </button>
                        </form>

                        <button
                            type="button"
                            className="text-link"
                            disabled={busy}
                            onClick={() => {
                                setMode(mode === "login" ? "register" : "login");
                                setMessage("");
                                setError("");
                            }}
                        >
                            {mode === "login"
                                ? "Chưa có tài khoản? Đăng ký"
                                : "Đã có tài khoản? Đăng nhập"}
                        </button>
                    </>
                )}
            </section>
        </div>
    );
}