import { api } from "./api";
import type { ManagedEventResponse, AdminUserResponse } from "../types";

/** Lấy danh sách sự kiện theo trạng thái (default: PendingApproval) */
export function getEventsForReview(
  status?: string,
): Promise<ManagedEventResponse[]> {
  const query = status ? `?status=${encodeURIComponent(status)}` : "";
  return api<ManagedEventResponse[]>(`/platform-admin/events${query}`);
}

/** Số sự kiện đang chờ duyệt */
export function getPendingEventCount(): Promise<{ count: number }> {
  return api<{ count: number }>("/platform-admin/events/pending-count");
}

/** Duyệt sự kiện */
export function approveEvent(
  eventId: string,
  note?: string,
): Promise<ManagedEventResponse> {
  return api<ManagedEventResponse>(
    `/platform-admin/events/${eventId}/approve`,
    {
      method: "POST",
      body: JSON.stringify({ note: note ?? null }),
    },
  );
}

/** Từ chối sự kiện */
export function rejectEvent(
  eventId: string,
  note: string,
): Promise<ManagedEventResponse> {
  return api<ManagedEventResponse>(`/platform-admin/events/${eventId}/reject`, {
    method: "POST",
    body: JSON.stringify({ note }),
  });
}

/** Tìm user theo email */
export function findUserByEmail(email: string): Promise<AdminUserResponse> {
  return api<AdminUserResponse>(
    `/platform-admin/users?email=${encodeURIComponent(email)}`,
  );
}

/** Gán vai trò hệ thống */
export function assignSystemRole(
  userId: string,
  role: string,
): Promise<AdminUserResponse> {
  return api<AdminUserResponse>(`/platform-admin/users/${userId}/system-role`, {
    method: "PUT",
    body: JSON.stringify({ role }),
  });
}

/** Khóa / mở khóa tài khoản */
export function updateAccountStatus(
  userId: string,
  isActive: boolean,
): Promise<AdminUserResponse> {
  return api<AdminUserResponse>(`/platform-admin/users/${userId}/status`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}
