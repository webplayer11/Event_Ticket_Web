import { useState } from "react";
import { UserCheck, ShieldAlert, Search } from "lucide-react";
import { UserRole } from "../../types";

interface SystemUser {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  status: "Active" | "Banned";
  registeredAt: string;
}

export const UserManagementPage = () => {
  const [searchTerm, setSearchTerm] = useState("");
  const [users, setUsers] = useState<SystemUser[]>([
    {
      id: "u-1",
      name: "Nguyễn Việt Huỳnh",
      email: "huynh@eventgo.vn",
      role: "Customer",
      status: "Active",
      registeredAt: "01.08.2026",
    },
    {
      id: "u-2",
      name: "Lê Minh Tuấn (LiveNation)",
      email: "organizer@livenation.vn",
      role: "Organizer",
      status: "Active",
      registeredAt: "10.07.2026",
    },
    {
      id: "u-3",
      name: "System Administrator",
      email: "admin@eventgo.vn",
      role: "Admin",
      status: "Active",
      registeredAt: "01.01.2026",
    },
  ]);

  const toggleStatus = (id: string) => {
    setUsers((prev) =>
      prev.map((u) =>
        u.id === id ? { ...u, status: u.status === "Active" ? "Banned" : "Active" } : u
      )
    );
  };

  const filtered = users.filter(
    (u) =>
      u.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      u.email.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">Quản lý người dùng</h1>
          <p className="text-xs text-slate-400 mt-1">
            Danh sách tài khoản Admin, Organizer và Customer trên toàn hệ thống
          </p>
        </div>

        <div className="relative w-full sm:w-64">
          <Search className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Tìm theo tên hoặc email..."
            className="w-full bg-slate-900 border border-slate-700 rounded-xl pl-9 pr-3.5 py-2 text-xs text-white focus:outline-none focus:border-red-500"
          />
        </div>
      </div>

      <div className="bg-[#0D1217] border border-slate-800 rounded-2xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400 bg-slate-900/60">
                <th className="py-3.5 px-4 font-semibold">Tên người dùng</th>
                <th className="py-3.5 px-4 font-semibold">Email</th>
                <th className="py-3.5 px-4 font-semibold">Vai trò (Role)</th>
                <th className="py-3.5 px-4 font-semibold">Ngày tham gia</th>
                <th className="py-3.5 px-4 font-semibold">Trạng thái</th>
                <th className="py-3.5 px-4 font-semibold text-right">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 text-slate-300">
              {filtered.map((u) => (
                <tr key={u.id} className="hover:bg-slate-800/30">
                  <td className="py-3.5 px-4 font-bold text-white">{u.name}</td>
                  <td className="py-3.5 px-4 text-slate-400">{u.email}</td>
                  <td className="py-3.5 px-4">
                    <span
                      className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold ${
                        u.role === "Admin"
                          ? "bg-red-500/10 text-red-400 border border-red-500/20"
                          : u.role === "Organizer"
                          ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                          : "bg-blue-500/10 text-blue-400 border border-blue-500/20"
                      }`}
                    >
                      {u.role}
                    </span>
                  </td>
                  <td className="py-3.5 px-4">{u.registeredAt}</td>
                  <td className="py-3.5 px-4">
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                        u.status === "Active"
                          ? "text-emerald-400 bg-emerald-500/10"
                          : "text-red-400 bg-red-500/10"
                      }`}
                    >
                      {u.status === "Active" ? "Hoạt động" : "Bị khóa"}
                    </span>
                  </td>
                  <td className="py-3.5 px-4 text-right">
                    {u.role !== "Admin" && (
                      <button
                        onClick={() => toggleStatus(u.id)}
                        className={`text-xs px-2.5 py-1 rounded-md font-medium transition-colors ${
                          u.status === "Active"
                            ? "bg-red-500/10 text-red-400 hover:bg-red-500/20"
                            : "bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20"
                        }`}
                      >
                        {u.status === "Active" ? "Khóa" : "Mở khóa"}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
