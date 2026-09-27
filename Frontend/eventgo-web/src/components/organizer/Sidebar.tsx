import React from 'react';
import { Ticket, FileText, FolderOpen } from 'lucide-react';

const navigate = (path: string) => {
    window.history.pushState({}, '', path);
    window.dispatchEvent(new PopStateEvent('popstate'));
    window.scrollTo({ top: 0 });
};

export const Sidebar = () => {
    const path = window.location.pathname;
    const isActive = (segment: string) => path.includes(segment);

    const menuItemClass = (active: boolean) =>
        `flex items-center gap-3 rounded-md px-3 py-2.5 text-sm font-medium transition-colors cursor-pointer ${active
            ? 'bg-green-600/10 text-green-500 border border-green-500/20'
            : 'text-gray-300 hover:bg-gray-800 hover:text-white'
        }`;

    return (
        <aside className="fixed left-0 top-16 z-40 h-[calc(100vh-4rem)] w-64 bg-black border-r border-[#2d2d2d] py-4">
            <nav className="flex flex-col gap-2 px-4">
                <button
                    onClick={() => navigate('/organizer/events')}
                    className={menuItemClass(isActive('/organizer/event') || isActive('/organizer/create-event'))}
                >
                    <Ticket className="h-5 w-5" />
                    Sự kiện của tôi
                </button>
                <button
                    onClick={() => navigate('/organizer/reports')}
                    className={menuItemClass(isActive('/organizer/reports'))}
                >
                    <FolderOpen className="h-5 w-5" />
                    Quản lý báo cáo
                </button>
                <button
                    onClick={() => navigate('/organizer/terms')}
                    className={menuItemClass(isActive('/organizer/terms'))}
                >
                    <FileText className="h-5 w-5" />
                    Điều khoản cho Ban tổ chức
                </button>
            </nav>
        </aside>
    );
};
