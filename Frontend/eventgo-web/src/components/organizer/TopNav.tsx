import React from 'react';
import { Ticket, User, ChevronDown } from 'lucide-react';

export const TopNav = () => {
    const goToCreateEvent = () => {
        window.history.pushState({}, '', '/organizer/create-event');
        window.dispatchEvent(new PopStateEvent('popstate'));
        window.scrollTo({ top: 0 });
    };

    return (
        <header className="fixed top-0 z-50 flex h-16 w-full items-center justify-between bg-black px-6 text-white border-b border-[#2d2d2d]">
            <div className="flex items-center gap-2">
                <Ticket className="h-6 w-6 text-green-500" />
                <span className="text-xl font-bold">Organizer Center</span>
                <ChevronDown className="h-4 w-4 text-gray-400" />
            </div>
            <div className="flex items-center gap-4">
                <button
                    onClick={goToCreateEvent}
                    className="flex items-center justify-center rounded-md bg-green-600 px-4 py-2 font-medium text-white transition-colors hover:bg-green-700 cursor-pointer"
                >
                    + Tạo sự kiện
                </button>
                <div className="flex cursor-pointer items-center gap-2">
                    <div className="flex h-8 w-8 items-center justify-center rounded-full bg-gray-600">
                        <User className="h-5 w-5" />
                    </div>
                    <span className="text-sm">Tài khoản</span>
                    <ChevronDown className="h-4 w-4 text-gray-400" />
                </div>
            </div>
        </header>
    );
};
