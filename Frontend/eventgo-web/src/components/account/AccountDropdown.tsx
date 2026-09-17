import { Ticket, CreditCard, CalendarDays, UserCircle, LogOut } from "lucide-react";

type Props = {
  onNavigate: (path: string) => void;
  onLogout: () => void;
};

const items = [
  { label: "Vé của tôi", icon: Ticket, path: "/tickets" },
  { label: "Thẻ thành viên", icon: CreditCard, path: "/membership" },
  { label: "Sự kiện của tôi", icon: CalendarDays, path: "/my-events" },
  { label: "Tài khoản của tôi", icon: UserCircle, path: "/account" },
];

export default function AccountDropdown({ onNavigate, onLogout }: Props) {
  return (
    <div className="absolute right-0 top-full mt-2 w-64 rounded-xl border border-white/10 bg-[#1E2022] p-2 shadow-2xl opacity-0 invisible translate-y-2 transition-all duration-200 group-hover:opacity-100 group-hover:visible group-hover:translate-y-0 z-50 flex flex-col gap-1">
      {items.map(({ label, icon: Icon, path }) => (
        <button
          key={path}
          className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-white/90 transition-colors hover:bg-white/10"
          onClick={() => onNavigate(path)}
        >
          <Icon size={16} className="text-white/70" />
          {label}
        </button>
      ))}
      <div className="my-1 h-[1px] w-full bg-white/10" />
      <button
        className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-red-500 transition-colors hover:bg-red-500/10"
        onClick={onLogout}
      >
        <LogOut size={16} />
        Đăng xuất
      </button>
    </div>
  );
}
