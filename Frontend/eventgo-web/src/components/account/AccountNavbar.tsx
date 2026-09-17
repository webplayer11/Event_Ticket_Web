import { Search, Plus, Ticket, ChevronDown, User } from "lucide-react";
import AccountDropdown from "./AccountDropdown";
import { useState } from "react";

type Props = {
    userName: string;
    onNavigate: (path: string) => void;
    onLogout: () => void;
    onSearch?: (query: string) => void;
};

export default function AccountNavbar({ userName, onNavigate, onLogout, onSearch }: Props) {
    const [query, setQuery] = useState("");

    return (
        <header className="flex h-16 shrink-0 items-center justify-between bg-[#2DC275] px-6 text-white w-full sticky top-0 z-40">
            {/* Left: Brand */}
            <button
                onClick={() => onNavigate("/")}
                className="flex items-center gap-2.5 transition-opacity hover:opacity-90"
            >
                <span className="flex h-8 w-8 items-center justify-center rounded bg-[#111] text-lg font-black text-[#2DC275]">
                    E
                </span>
                <span className="text-xl font-extrabold tracking-wide">
                    event<span className="font-normal">go</span>
                </span>
            </button>

            {/* Center: Search */}
            <div className="relative mx-6 hidden flex-1 max-w-lg items-center md:flex">
                <input
                    type="search"
                    placeholder="Tìm sự kiện, nghệ sĩ, địa điểm..."
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && onSearch?.(query)}
                    className="h-10 w-full rounded-full border border-white/30 bg-black/10 pl-4 pr-10 text-sm text-white placeholder-white/70 outline-none transition-all focus:border-white/60 focus:bg-black/20"
                />
                <button
                    onClick={() => onSearch?.(query)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-white/80 hover:text-white"
                >
                    <Search size={18} strokeWidth={2.5} />
                </button>
            </div>

            {/* Right: Actions */}
            <nav className="flex items-center gap-5">
                <button
                    onClick={() => onNavigate("/create-event")}
                    className="hidden items-center gap-2 rounded-full bg-black/20 px-3.5 py-1.5 text-sm font-semibold transition-colors hover:bg-black/30 md:flex"
                >
                    <Plus size={16} strokeWidth={2.5} />
                    Tạo sự kiện
                </button>

                <button
                    onClick={() => onNavigate("/tickets")}
                    className="hidden items-center gap-2 text-sm font-semibold transition-opacity hover:opacity-80 md:flex"
                >
                    <Ticket size={18} strokeWidth={2} />
                    Vé của tôi
                </button>

                {/* User Dropdown wrapper with group trigger */}
                <div className="group relative flex items-center h-16">
                    <button className="flex items-center gap-2 text-sm font-medium transition-opacity hover:opacity-80">
                        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-black/20">
                            <User size={16} strokeWidth={2.5} />
                        </div>
                        <span className="max-w-[100px] truncate">{userName || "Tài khoản"}</span>
                        <ChevronDown size={16} strokeWidth={2} />
                    </button>

                    <div className="absolute top-full -mt-2 invisible group-hover:visible translate-y-2 group-hover:translate-y-0 opacity-0 group-hover:opacity-100 transition-all z-50">
                        <div className="pt-2"></div>
                        <AccountDropdown onNavigate={onNavigate} onLogout={onLogout} />
                    </div>
                </div>
            </nav>
        </header>
    );
}
