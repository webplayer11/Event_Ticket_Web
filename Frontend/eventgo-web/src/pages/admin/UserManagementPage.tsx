import { useState } from "react";
import {
  UserCheck,
  ShieldAlert,
  Search,
  Loader2,
  AlertCircle,
} from "lucide-react";
import {
  findUserByEmail,
  updateAccountStatus,
  assignSystemRole,
} from "../../services/adminService";
import type { AdminUserResponse } from "../../types";

function formatDate(dateStr: string): string {
  const d = new Date(dateStr);
  return `${d.getDate().toString().padStart(2, "0")}.${(d.getMonth() + 1).toString().padStart(2, "0")}.${d.getFullYear()}`;
}

export const UserManagementPage = () => {
  const [searchEmail, setSearchEmail] = useState("");
  const [searchResult, setSearchResult] = useState<AdminUserResponse | null>(
    null,
  );
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [processingId, setProcessingId] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  const handleSearch = async () => {
    if (!searchEmail.trim()) return;
    setSearching(true);
    setSearchError(null);
    setSearchResult(null);
    setActionMessage(null);
    try {
      const user = await findUserByEmail(searchEmail.trim());
      setSearchResult(user);
    } catch (err: unknown) {
      const e = err as { message?: string; status?: number };
      setSearchError(
        e.status === 404
          ? "Không tìm thấy tài khoản với email này."
          : (e.message ?? "Đã có lỗi xảy ra."),
      );
    } finally {
      setSearching(false);
    }
  };

  const handleToggleStatus = async (user: AdminUserResponse) => {
    setProcessingId(user.id);
    setActionMessage(null);
    try {
      const updated = await updateAccountStatus(user.id, !user.isActive);
      setSearchResult(updated);
      setActionMessage(
        updated.isActive ? "✓ Đã mở khóa tài khoản." : "✓ Đã khóa tài khoản.",
      );
    } catch (err: unknown) {
      setActionMessage((err as { message?: string })?.message ?? "Thao tác thất bại.");
    } finally {
      setProcessingId(null);
    }
  };

  const handleAssignRole = async (user: AdminUserResponse, role: string) => {
    setProcessingId(user.id);
    setActionMessage(null);
    try {
      const updated = await assignSystemRole(user.id, role);
      setSearchResult(updated);
      setActionMessage(`✓ Đã gán vai trò ${role} thành công.`);
    } catch (err: unknown) {
      setActionMessage((err as { message?: string })?.message ?? "Thao tác thất bại.");
    } finally {
      setProcessingId(null);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-white">Quản lý người dùng</h1>
        <p className="text-xs text-slate-400 mt-1">
          Tìm tài khoản theo email, thay đổi vai trò và trạng thái
        </p>
      </div>

      {/* Search */}
      <div className="bg-[#141A1E] border border-slate-800 rounded-2xl p-6 space-y-4">
        <h2 className="text-sm font-bold text-white">Tìm tài khoản theo email</h2>
        <div className="flex gap-2">
          <div className="relative flex-1">
            <Search className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
            <input
              type="email"
              value={searchEmail}
              onChange={(e) => setSearchEmail(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleSearch()}
              placeholder="user@example.com"
              className="w-full bg-slate-900 border border-slate-700 rounded-xl pl-9 pr-3.5 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
            />
          </div>
          <button
            onClick={handleSearch}
            disabled={searching || !searchEmail.trim()}
            className="px-4 py-2 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold transition-colors disabled:opacity-50 flex items-center gap-2"
          >
            {searching && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
            Tìm kiếm
          </button>
        </div>

        {searchError && (
          <div className="flex items-center gap-2 text-red-400 text-xs">
            <AlertCircle className="w-4 h-4" />
            <span>{searchError}</span>
          </div>
        )}

        {actionMessage && (
          <p
            className={`text-xs ${actionMessage.startsWith("✓") ? "text-emerald-400" : "text-red-400"}`}
          >
            {actionMessage}
          </p>
        )}

        {/* Result */}
        {searchResult && (
          <div className="bg-slate-900/60 border border-slate-700 rounded-xl p-5 space-y-4">
            <div className="flex flex-col sm:flex-row justify-between gap-4">
              <div className="space-y-1">
                <p className="text-base font-bold text-white">
                  {searchResult.fullName}
                </p>
                <p className="text-xs text-slate-400">{searchResult.email}</p>
                <p className="text-xs text-slate-500">
                  Tham gia:{" "}
                  {searchResult.createdAt
                    ? formatDate(searchResult.createdAt)
                    : "—"}
                </p>
              </div>
              <div className="flex flex-col items-start sm:items-end gap-2">
                <span
                  className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold ${
                    searchResult.role === "Admin"
                      ? "bg-red-500/10 text-red-400 border border-red-500/20"
                      : searchResult.role === "Organizer"
                        ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                        : "bg-blue-500/10 text-blue-400 border border-blue-500/20"
                  }`}
                >
                  {searchResult.role}
                </span>
                <span
                  className={`text-[10px] font-semibold ${
                    searchResult.isActive ? "text-emerald-400" : "text-red-400"
                  }`}
                >
                  {searchResult.isActive ? "Hoạt động" : "Bị khóa"}
                </span>
              </div>
            </div>

            {/* Actions */}
            <div className="flex flex-wrap gap-2 pt-2 border-t border-slate-800">
              {/* Toggle status */}
              {searchResult.role !== "Admin" && (
                <button
                  disabled={processingId === searchResult.id}
                  onClick={() => handleToggleStatus(searchResult)}
                  className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors disabled:opacity-50 ${
                    searchResult.isActive
                      ? "bg-red-500/10 text-red-400 hover:bg-red-500/20 border border-red-500/20"
                      : "bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 border border-emerald-500/20"
                  }`}
                >
                  {processingId === searchResult.id ? (
                    <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  ) : searchResult.isActive ? (
                    <ShieldAlert className="w-3.5 h-3.5" />
                  ) : (
                    <UserCheck className="w-3.5 h-3.5" />
                  )}
                  {searchResult.isActive ? "Khóa tài khoản" : "Mở khóa"}
                </button>
              )}

              {/* Assign role */}
              {searchResult.role !== "Admin" && (
                <button
                  disabled={processingId === searchResult.id}
                  onClick={() => handleAssignRole(searchResult, "Admin")}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold bg-red-500/10 text-red-400 hover:bg-red-500/20 border border-red-500/20 transition-colors disabled:opacity-50"
                >
                  Gán quyền Admin
                </button>
              )}
              {searchResult.role === "Admin" && (
                <button
                  disabled={processingId === searchResult.id}
                  onClick={() => handleAssignRole(searchResult, "Customer")}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold bg-blue-500/10 text-blue-400 hover:bg-blue-500/20 border border-blue-500/20 transition-colors disabled:opacity-50"
                >
                  Hạ về Customer
                </button>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Info note */}
      <div className="bg-slate-800/40 border border-slate-700/50 rounded-xl p-4 text-xs text-slate-400 space-y-1">
        <p className="font-semibold text-slate-300">Lưu ý về phân quyền:</p>
        <ul className="list-disc list-inside space-y-0.5 ml-1">
          <li>
            <span className="text-blue-400 font-semibold">Customer</span>: Người
            dùng mặc định — có thể mua vé VÀ tạo sự kiện (kênh BTC).
          </li>
          <li>
            <span className="text-red-400 font-semibold">Admin</span>: Quản trị
            viên hệ thống — duyệt sự kiện và quản lý tài khoản.
          </li>
          <li>
            Không thể khóa hoặc hạ quyền Admin cuối cùng trong hệ thống.
          </li>
        </ul>
      </div>
    </div>
  );
};
