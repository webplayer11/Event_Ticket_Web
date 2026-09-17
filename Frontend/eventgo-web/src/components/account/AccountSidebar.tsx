import { Settings, ShoppingBag, CalendarDays, ChevronRight } from "lucide-react";

type Props = {
    userName: string;
    activePath: string;
    onNavigate: (path: string) => void;
};

const menuItems = [
    { label: "Cài đặt tài khoản", icon: Settings, path: "/account" },
    { label: "Đơn hàng của tôi", icon: ShoppingBag, path: "/orders" },
    { label: "Sự kiện của tôi", icon: CalendarDays, path: "/my-events" },
];

export default function AccountSidebar({ userName, activePath, onNavigate }: Props) {
    return (
        <aside className="w-full shrink-0 border-b border-white/10 bg-[#1E2022] py-6 md:w-72 md:border-b-0 md:border-r md:py-8 overflow-y-auto">
            <div className="mb-8 flex items-center gap-4 px-6">
                <div className="flex h-14 w-14 items-center justify-center rounded-full bg-[#2DC275] text-2xl font-bold text-black">
                    {userName.charAt(0).toUpperCase()}
                </div>
                <div>
                    <p className="text-xs text-white/50">Tài khoản của</p>
                    <p className="text-lg font-bold text-white">{userName}</p>
                </div>
            </div>

            <nav className="flex flex-col">
                {menuItems.map(({ label, icon: Icon, path }) => {
                    const isActive = activePath === path;
                    return (
                        <button
                            key={path}
                            onClick={() => onNavigate(path)}
                            className={`group flex items-center px-6 py-3.5 text-sm transition-colors ${isActive
                                    ? "border-l-4 border-l-[#2DC275] bg-white/5 text-[#2DC275] md:border-l-0 md:border-r-4 md:border-r-[#2DC275]"
                                    : "text-white/60 hover:bg-white/5 hover:text-white"
                                }`}
                        >
                            <Icon size={18} strokeWidth={2} className="mr-3 shrink-0" />
                            <span className="font-medium">{label}</span>
                            <ChevronRight
                                size={16}
                                strokeWidth={2}
                                className={`ml-auto transition-transform ${isActive ? "text-[#2DC275]" : "text-white/20 group-hover:text-white/60"
                                    }`}
                            />
                        </button>
                    );
                })}
            </nav>
        </aside>
    );
}
