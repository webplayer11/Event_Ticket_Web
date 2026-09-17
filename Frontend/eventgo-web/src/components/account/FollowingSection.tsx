import { BadgeCheck, ChevronRight } from "lucide-react";

type FollowingUser = {
    id: string;
    name: string;
    role: string;
    verified?: boolean;
    initial: string;
};

type Props = {
    onExplore: () => void;
};

const mockFollowing: FollowingUser[] = [
    { id: "1", name: "Mỹ Anh", role: "Singer · Songwriter", verified: true, initial: "M" },
    { id: "2", name: "Lumi Studio", role: "Visual arts", verified: true, initial: "L" },
    { id: "3", name: "Bếp Nhà Mình", role: "Culinary team", initial: "B" },
    { id: "4", name: "Run Saigon", role: "Sports community", initial: "R" },
];

export default function FollowingSection({ onExplore }: Props) {
    return (
        <section className="flex flex-1 flex-col rounded-2xl border border-white/10 bg-[#242527] p-6 shadow-sm">
            <div className="mb-5 flex items-center justify-between">
                <h2 className="text-lg font-bold text-white">Đang theo dõi</h2>
                <button
                    onClick={onExplore}
                    className="flex items-center gap-1 text-sm font-semibold text-[#2DC275] transition-colors hover:text-[#24a161]"
                >
                    Khám phá thêm <ChevronRight size={14} strokeWidth={2.5} />
                </button>
            </div>

            <div className="flex flex-col gap-2">
                {mockFollowing.map((user) => (
                    <button
                        key={user.id}
                        className="flex w-full items-center gap-4 rounded-xl p-2.5 text-left transition-colors hover:bg-white/5"
                    >
                        <div className="relative shrink-0">
                            <div className="flex h-11 w-11 items-center justify-center rounded-full bg-white/10 text-lg font-bold text-white">
                                {user.initial}
                            </div>
                            {user.verified && (
                                <div className="absolute -bottom-1 -right-1 rounded-full bg-[#242527] p-[2px]">
                                    <BadgeCheck size={14} className="fill-[#2DC275] stroke-black" strokeWidth={1.5} />
                                </div>
                            )}
                        </div>
                        <div>
                            <p className="text-sm font-bold text-white">{user.name}</p>
                            <p className="mt-0.5 text-xs font-medium text-white/50">{user.role}</p>
                        </div>
                    </button>
                ))}
            </div>
        </section>
    );
}
