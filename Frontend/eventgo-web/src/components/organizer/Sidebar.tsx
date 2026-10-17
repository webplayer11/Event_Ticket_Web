import { Link, useLocation } from 'react-router-dom';
import {
    LayoutDashboard,
    Ticket,
    CalendarPlus,
    ScanLine,
    ArrowLeft,
} from 'lucide-react';

export const Sidebar = () => {
    const location = useLocation();
    const path = location.pathname;

    const navItems = [
        { label: 'Tổng quan Dashboard', path: '/organizer', icon: LayoutDashboard, exact: true },
        { label: 'Sự kiện của tôi', path: '/organizer/events', icon: Ticket },
        { label: 'Tạo sự kiện mới', path: '/organizer/create-event', icon: CalendarPlus },
        { label: 'Soát vé (Check-in)', path: '/organizer/check-in', icon: ScanLine },
    ];

    return (
        <aside className="fixed left-0 top-16 z-40 h-[calc(100vh-4rem)] w-64 bg-[#0a0e12] border-r border-slate-800/80 py-4 flex flex-col justify-between">
            <nav className="flex flex-col gap-1.5 px-4">
                <div className="px-2 py-1 text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
                    Quản lý sự kiện
                </div>
                {navItems.map((item) => {
                    const Icon = item.icon;
                    const active = item.exact
                        ? path === item.path
                        : path.startsWith(item.path);
                    return (
                        <Link
                            key={item.path}
                            to={item.path}
                            className={`flex items-center gap-3 rounded-lg px-3 py-2.5 text-xs font-medium transition-colors ${
                                active
                                    ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30'
                                    : 'text-slate-300 hover:bg-slate-800/60 hover:text-white'
                            }`}
                        >
                            <Icon className="h-4 w-4" />
                            <span>{item.label}</span>
                        </Link>
                    );
                })}
            </nav>

            <div className="px-4 pt-4 border-t border-slate-800/80">
                <Link
                    to="/"
                    className="flex items-center gap-2.5 px-3 py-2 text-xs text-slate-400 hover:text-white hover:bg-slate-800/50 rounded-lg transition-colors"
                >
                    <ArrowLeft className="h-4 w-4" />
                    <span>Về trang khám phá</span>
                </Link>
            </div>
        </aside>
    );
};
