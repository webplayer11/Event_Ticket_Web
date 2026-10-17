export type UserRole = "Admin" | "Organizer" | "Customer";

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  avatarUrl?: string;
  phoneNumber?: string;
}

// ---------- Backend API types ----------

export interface TicketTypeResponse {
  id: string;
  name: string;
  description: string;
  price: number;
  currency: string;
  totalQuantity: number;
  availableQuantity: number;
  maxQuantityPerOrder: number;
  saleStartsAt: string;
  saleEndsAt: string;
}

export interface EventResponse {
  id: string;
  title: string;
  slug: string;
  description: string;
  categoryId: string;
  categoryName: string;
  organizationId: string;
  organizationName: string;
  venueName: string;
  address: string;
  city: string;
  startsAt: string;
  endsAt: string;
  status: string;
  createdAt: string;
  publishedAt?: string;
  ticketTypes: TicketTypeResponse[];
}

export interface ManagedEventResponse extends EventResponse {
  createdByUserId: string;
  createdByName: string;
  createdByEmail: string;
  lastReviewNote?: string;
  lastReviewedAt?: string;
}

export interface EventCategoryResponse {
  id: string;
  name: string;
  slug: string;
}

export interface TicketResponse {
  id: string;
  ticketCode: string;
  eventTitle: string;
  eventStartsAt: string;
  venueName: string;
  ticketTypeName: string;
  status: string;
  issuedAt: string;
  qrToken?: string;
}

export interface TicketValidationResponse {
  ticketId: string;
  ticketCode: string;
  eventId: string;
  eventTitle: string;
  ticketTypeName: string;
  status: string;
  isCheckedIn: boolean;
  customerName: string;
  customerEmail: string;
}

export interface SaveEventRequest {
  organizationId?: string;
  organizerName?: string;
  organizerInfo?: string;
  title: string;
  description: string;
  categoryId: string;
  venueName: string;
  address: string;
  city: string;
  startsAt: string;
  endsAt: string;
  ticketTypes: TicketTypeRequest[];
}

export interface TicketTypeRequest {
  name: string;
  description?: string;
  price: number;
  totalQuantity: number;
  maxQuantityPerOrder: number;
  saleStartsAt: string;
  saleEndsAt: string;
}

// Admin user
export interface AdminUserResponse {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

// ---------- Legacy frontend-only types (kept for backward compat) ----------
export interface EventItem {
  id: number;
  title: string;
  date: string;
  place: string;
  price: string;
  category: string;
  image?: string;
  featured?: boolean;
  status?: "Draft" | "Pending" | "Approved" | "Rejected";
  organizerName?: string;
}

export interface TicketItem {
  id: string;
  ticketCode: string;
  eventName: string;
  eventDate: string;
  venue: string;
  tierName: string;
  price: number;
  qrCodeUrl: string;
  status: "Valid" | "Used" | "Cancelled";
  holderName: string;
}
