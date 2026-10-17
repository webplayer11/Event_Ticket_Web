import { api } from "./api";
import type { TicketResponse, TicketValidationResponse } from "../types";

/** Lấy danh sách vé của tôi */
export function getMyTickets(): Promise<TicketResponse[]> {
  return api<TicketResponse[]>("/tickets/mine");
}

/** Validate QR token (dành cho staff check-in) */
export function validateQrToken(
  eventId: string,
  qrToken: string,
): Promise<TicketValidationResponse> {
  return api<TicketValidationResponse>(`/events/${eventId}/check-in/validate`, {
    method: "POST",
    body: JSON.stringify({ qrToken }),
  });
}

/** Check-in vé */
export function checkInTicket(
  eventId: string,
  qrToken: string,
): Promise<TicketValidationResponse> {
  return api<TicketValidationResponse>(`/events/${eventId}/check-in`, {
    method: "POST",
    body: JSON.stringify({ qrToken }),
  });
}
