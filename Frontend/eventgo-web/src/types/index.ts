export type UserRole = "Admin" | "Organizer" | "Customer";

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  avatarUrl?: string;
  phoneNumber?: string;
}

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
