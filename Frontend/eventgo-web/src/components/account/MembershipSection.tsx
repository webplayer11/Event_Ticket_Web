import { ChevronRight } from "lucide-react";

type Props = {
    onExplore: () => void;
};

export default function MembershipSection({ onExplore }: Props) {
    return (
        <section className="flex flex-1 flex-col rounded-2xl border border-white/10 bg-[#242527] p-6 shadow-sm">
            <div className="mb-5 flex items-center justify-between">
                <h2 className="text-lg font-bold text-white">Thẻ thành viên của tôi</h2>
                <button
                    onClick={onExplore}
                    className="flex items-center gap-1 text-sm font-semibold text-[#2DC275] transition-colors hover:text-[#24a161]"
                >
                    Khám phá thêm <ChevronRight size={14} strokeWidth={2.5} />
                </button>
            </div>

            <div className="flex flex-1 items-center justify-center rounded-xl border border-dashed border-white/20 bg-white/[0.02] p-8 text-center text-sm font-medium text-white/40">
                Bạn chưa có thẻ thành viên
            </div>
        </section>
    );
}
