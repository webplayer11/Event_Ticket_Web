import React from 'react';
import { TopNav } from './TopNav';
import { Sidebar } from './Sidebar';

export const OrganizerLayout = ({ children }: { children: React.ReactNode }) => {
    return (
        <div className="min-h-screen bg-black text-white font-sans selection:bg-green-500/30">
            <TopNav />
            <Sidebar />
            <main className="ml-64 pt-16 min-h-screen bg-gradient-to-br from-[#12181A] to-[#0B0F0D]">
                <div className="mx-auto max-w-5xl p-6">
                    {children}
                </div>
            </main>
        </div>
    );
};
