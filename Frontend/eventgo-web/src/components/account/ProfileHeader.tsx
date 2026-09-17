import { Pencil } from "lucide-react";

type Props = {
    userName: string;
    userEmail: string;
};

export default function ProfileHeader({ userName, userEmail }: Props) {
    return (
        <div className="flex flex-col items-center gap-6 rounded-2xl border border-white/10 bg-[#242527] p-6 shadow-sm md:flex-row md:p-8">
            <div className="relative shrink-0">
                <div className="flex h-24 w-24 items-center justify-center rounded-full bg-[#2DC275] shadow-lg text-4xl font-extrabold text-black">
                    {userName.charAt(0).toUpperCase()}
                </div>
                <button
                    className="absolute bottom-0 right-0 flex h-8 w-8 items-center justify-center rounded-full bg-white text-black shadow-md transition-transform hover:scale-110"
                    aria-label="Chỉnh sửa hồ sơ"
                >
                    <Pencil size={14} strokeWidth={2.5} />
                </button>
            </div>

            <div className="flex flex-col items-center text-center md:items-start md:text-left">
                <h1 className="text-2xl font-extrabold text-white sm:text-3xl">{userName}</h1>
                <p className="mt-1 text-sm font-medium text-white/50">{userEmail}</p>
            </div>
        </div>
    );
}
