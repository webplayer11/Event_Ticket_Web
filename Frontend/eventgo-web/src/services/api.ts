const API_BASE_URL = (
    import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5248/api"
).replace(/\/$/, "");

let accessToken: string | null = null;
let refreshToken: string | null = null;

type TokenRefreshListener = (accessToken: string, refreshToken: string) => void;
type AuthFailureListener = () => void;

let onTokenRefreshListener: TokenRefreshListener | null = null;
let onAuthFailureListener: AuthFailureListener | null = null;

export function setAccessToken(token: string | null) {
    accessToken = token;
}

export function setRefreshToken(token: string | null) {
    refreshToken = token;
}

export function getAccessToken(): string | null {
    return accessToken;
}

export function getRefreshToken(): string | null {
    return refreshToken;
}

export function setAuthListeners(listeners: {
    onTokenRefresh?: TokenRefreshListener;
    onAuthFailure?: AuthFailureListener;
}) {
    if (listeners.onTokenRefresh) onTokenRefreshListener = listeners.onTokenRefresh;
    if (listeners.onAuthFailure) onAuthFailureListener = listeners.onAuthFailure;
}

export class ApiError extends Error {
    status: number;

    constructor(message: string, status: number) {
        super(message);
        this.name = "ApiError";
        this.status = status;
    }
}

export interface CustomRequestInit extends RequestInit {
    _isRetry?: boolean;
}

// Singleton promise mutex to ensure only ONE refresh request is in flight at any given time.
let refreshTokenPromise: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
    if (refreshTokenPromise) {
        return refreshTokenPromise;
    }

    refreshTokenPromise = (async () => {
        try {
            if (!refreshToken) {
                return null;
            }

            const response = await fetch(`${API_BASE_URL}/auth/refresh-token`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({ refreshToken }),
            });

            if (!response.ok) {
                return null;
            }

            const data = (await response.json()) as {
                accessToken?: string;
                refreshToken?: string;
            };

            if (data?.accessToken && data?.refreshToken) {
                accessToken = data.accessToken;
                refreshToken = data.refreshToken;
                onTokenRefreshListener?.(data.accessToken, data.refreshToken);
                return data.accessToken;
            }

            return null;
        } catch {
            return null;
        } finally {
            refreshTokenPromise = null;
        }
    })();

    return refreshTokenPromise;
}

export async function api<T>(
    path: string,
    options: CustomRequestInit = {},
): Promise<T> {
    const headers = new Headers(options.headers);

    if (options.body !== undefined && !(options.body instanceof FormData)) {
        headers.set("Content-Type", "application/json");
    }

    if (accessToken) {
        headers.set("Authorization", `Bearer ${accessToken}`);
    }

    let response: Response;

    try {
        response = await fetch(`${API_BASE_URL}${path}`, {
            ...options,
            headers,
        });
    } catch {
        throw new ApiError(
            "Không kết nối được API. Kiểm tra BE, địa chỉ API, HTTPS và CORS.",
            0,
        );
    }

    // Handle 401 Unauthorized with automatic refresh token rotation
    if (response.status === 401) {
        const isAuthEndpoint =
            path.includes("/auth/login") ||
            path.includes("/auth/refresh-token") ||
            path.includes("/auth/register");

        if (!isAuthEndpoint && !options._isRetry && refreshToken) {
            const newAccessToken = await refreshAccessToken();
            if (newAccessToken) {
                const retryHeaders = new Headers(options.headers);
                if (options.body !== undefined && !(options.body instanceof FormData)) {
                    retryHeaders.set("Content-Type", "application/json");
                }
                retryHeaders.set("Authorization", `Bearer ${newAccessToken}`);

                return api<T>(path, {
                    ...options,
                    headers: retryHeaders,
                    _isRetry: true,
                });
            } else {
                // Refresh token expired or revoked - clear tokens and inform listeners
                accessToken = null;
                refreshToken = null;
                onAuthFailureListener?.();
            }
        }
    }

    const text = await response.text();
    let data: unknown = null;

    if (text) {
        try {
            data = JSON.parse(text);
        } catch {
            data = null;
        }
    }

    if (!response.ok) {
        const body = data as {
            message?: string;
            title?: string;
            errors?: string[] | Record<string, string[]>;
        } | null;

        const errors = Array.isArray(body?.errors)
            ? body.errors
            : body?.errors
                ? Object.values(body.errors).flat()
                : [];

        const fallback =
            response.status === 401
                ? "Phiên đăng nhập không hợp lệ hoặc đã hết hạn. Hãy đăng nhập lại."
                : `Yêu cầu thất bại (${response.status}).`;

        const message = [
            body?.message ?? body?.title,
            ...errors,
        ].filter(Boolean).join("\n");

        throw new ApiError(message || fallback, response.status);
    }

    return data as T;
}
