import AccountNavbar from "../components/account/AccountNavbar";
import AccountSidebar from "../components/account/AccountSidebar";
import ProfileHeader from "../components/account/ProfileHeader";
import MembershipSection from "../components/account/MembershipSection";
import FollowingSection from "../components/account/FollowingSection";

export default function AccountPage({
    userName,
    userEmail,
    currentPath,
    onNavigate,
    onLogout,
}: {
    userName: string;
    userEmail: string;
    currentPath: string;
    onNavigate: (path: string) => void;
    onLogout: () => void;
}) {
    return (
        <div className="flex min-h-screen flex-col bg-[#111] text-white">
            {/* Green Navbar */}
            <AccountNavbar
                userName={userName}
                onNavigate={onNavigate}
                onLogout={onLogout}
            />

            {/* Main Container */}
            <div className="flex flex-1 flex-col md:flex-row mx-auto w-full max-w-[1440px]">
                {/* Sidebar */}
                <AccountSidebar
                    userName={userName}
                    activePath={currentPath}
                    onNavigate={onNavigate}
                />

                {/* Content Area */}
                <main className="flex-1 overflow-y-auto px-4 py-6 md:px-8 md:py-10">
                    <div className="mx-auto max-w-4xl flex flex-col h-full space-y-6">
                        <ProfileHeader userName={userName} userEmail={userEmail} />

                        <div className="flex flex-col gap-6 lg:flex-row flex-1">
                            <MembershipSection onExplore={() => onNavigate("/membership")} />
                            <FollowingSection onExplore={() => onNavigate("/")} />
                        </div>
                    </div>
                </main>
            </div>

            <footer className="shrink-0 border-t border-white/5 bg-[#111] py-4 text-center text-xs font-medium text-white/40">
                © 2026 EventGo.
            </footer>
        </div>
    );
}
