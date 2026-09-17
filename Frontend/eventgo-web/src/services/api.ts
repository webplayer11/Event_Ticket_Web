const API_BASE_URL = (
    import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7166/api"
).replace(/\/$/, "");

let accessToken: string | null = null;

export function setAccessToken(token: string | null) {
    accessToken = token;
}

export class ApiError extends Error {
    status: number;

    constructor(message: string, status: number) {
        super(message);
        this.name = "ApiError";
        this.status = status;
    }
}

export async function api<T>(
    path: string,
    options: RequestInit = {},
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