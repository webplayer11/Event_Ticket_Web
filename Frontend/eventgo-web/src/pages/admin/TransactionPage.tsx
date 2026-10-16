export const TransactionPage = () => {
  const transactions = [
    {
      id: "TX-9901",
      customer: "Nguyễn Việt Huỳnh",
      event: "NEON PULSE — Live in Saigon",
      amount: "650.000đ",
      method: "VNPAY",
      status: "SUCCESS",
      date: "12.10.2026 09:15",
    },
    {
      id: "TX-9902",
      customer: "Trần Bảo Ngọc",
      event: "Vietnam Innovation Forum 2026",
      amount: "300.000đ",
      method: "MOMO",
      status: "SUCCESS",
      date: "12.10.2026 08:40",
    },
    {
      id: "TX-9903",
      customer: "Hoàng Minh Khoa",
      event: "Đường chạy thành phố — Night Run",
      amount: "390.000đ",
      method: "BANK_TRANSFER",
      status: "REFUNDED",
      date: "11.10.2026 14:20",
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-white">Giao dịch & Vé toàn sàn</h1>
        <p className="text-xs text-slate-400 mt-1">
          Lịch sử giao dịch thanh toán, trạng thái xuất vé và đối soát tài chính
        </p>
      </div>

      <div className="bg-[#0D1217] border border-slate-800 rounded-2xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400 bg-slate-900/60">
                <th className="py-3.5 px-4 font-semibold">Mã GD</th>
                <th className="py-3.5 px-4 font-semibold">Khách hàng</th>
                <th className="py-3.5 px-4 font-semibold">Sự kiện</th>
                <th className="py-3.5 px-4 font-semibold">Số tiền</th>
                <th className="py-3.5 px-4 font-semibold">Cổng TT</th>
                <th className="py-3.5 px-4 font-semibold">Thời gian</th>
                <th className="py-3.5 px-4 font-semibold text-right">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 text-slate-300">
              {transactions.map((tx) => (
                <tr key={tx.id} className="hover:bg-slate-800/30">
                  <td className="py-3.5 px-4 font-mono font-bold text-white">{tx.id}</td>
                  <td className="py-3.5 px-4">{tx.customer}</td>
                  <td className="py-3.5 px-4 text-slate-400">{tx.event}</td>
                  <td className="py-3.5 px-4 font-semibold text-emerald-400">{tx.amount}</td>
                  <td className="py-3.5 px-4">{tx.method}</td>
                  <td className="py-3.5 px-4 text-slate-400">{tx.date}</td>
                  <td className="py-3.5 px-4 text-right">
                    <span
                      className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold ${
                        tx.status === "SUCCESS"
                          ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                          : "bg-amber-500/10 text-amber-400 border border-amber-500/20"
                      }`}
                    >
                      {tx.status === "SUCCESS" ? "Thành công" : "Đã hoàn tiền"}
                    </span>
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
