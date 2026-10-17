import { api } from "./api";
import type {
  EventResponse,
  ManagedEventResponse,
  EventCategoryResponse,
  SaveEventRequest,
} from "../types";

/** Sự kiện đã được duyệt và còn diễn ra (public, no auth required) */
export function getPublishedEvents(): Promise<EventResponse[]> {
  return api<EventResponse[]>("/events");
}

/** Chi tiết sự kiện đã được duyệt theo id */
export function getPublishedEventById(id: string): Promise<EventResponse> {
  return api<EventResponse>(`/events/${id}`);
}

/** Sự kiện tôi đang quản lý (Owner/Manager) */
export function getMyEvents(): Promise<ManagedEventResponse[]> {
  return api<ManagedEventResponse[]>("/events/mine");
}

/** Tạo sự kiện mới */
export function createEvent(
  data: SaveEventRequest,
): Promise<ManagedEventResponse> {
  return api<ManagedEventResponse>("/events", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

/** Cập nhật sự kiện */
export function updateEvent(
  id: string,
  data: SaveEventRequest,
): Promise<ManagedEventResponse> {
  return api<ManagedEventResponse>(`/events/${id}`, {
    method: "PUT",
    body: JSON.stringify(data),
  });
}

/** Danh mục sự kiện */
export function getEventCategories(): Promise<EventCategoryResponse[]> {
  return api<EventCategoryResponse[]>("/event-categories");
}
