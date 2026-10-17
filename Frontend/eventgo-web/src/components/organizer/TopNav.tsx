import { Link } from 'react-router-dom';
import { Ticket, User, Sparkles } from 'lucide-react';
import { useAuthStore } from '../../stores/useAuthStore';

export const TopNav = () => {
    const { user } = useAuthStore();

    return (
        <header className="fixed top-0 z-50 flex h-16 w-full items-center justify-between bg-[#0a0e12] px-6 text-white border-b border-slate-800/80">
            <Link to="/organizer" className="flex items-center gap-2 hover:opacity-90 transition-opacity">
                <span className="w-8 h-8 rounded-lg bg-emerald-500 flex items-center justify-center text-black font-extrabold text-sm">
                    EG
                </span>
                <div className="flex items-center gap-1.5">
                    <span className="text-base font-bold text-white">EventGO</span>
                    <span className="text-xs px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 font-semibold border border-emerald-500/20">
                        Kênh Ban Tổ Chức
                    </span>
                </div>
            </Link>

            <div className="flex items-center gap-3">
                <Link
                    to="/organizer/create-event"
                    className="flex items-center gap-1.5 rounded-lg bg-emerald-500 px-3.5 py-1.5 text-xs font-bold text-slate-950 transition-colors hover:bg-emerald-400"
                >
                    <Sparkles className="w-3.5 h-3.5" />
                    <span>+ Tạo sự kiện</span>
                </Link>

                <Link
                    to="/account"
                    className="flex items-center gap-2 p-1.5 px-3 rounded-lg border border-slate-700/60 hover:bg-slate-800/80 text-slate-200 text-xs transition-colors"
                >
                    <div className="flex h-6 w-6 items-center justify-center rounded-full bg-slate-700 text-[10px] font-bold">
                        <User className="h-3.5 w-3.5" />
                    </div>
                    <span>{user?.fullName || "Tài khoản"}</span>
                </Link>
            </div>
        </header>
    );
};
