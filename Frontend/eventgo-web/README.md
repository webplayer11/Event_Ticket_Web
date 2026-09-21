# EventGo — React + Vite

Source code giao diện website bán vé sự kiện, viết bằng React, Vite và TypeScript. Dự án chỉ gồm phần frontend, không có backend hoặc cơ sở dữ liệu.

## Chạy trên máy

Yêu cầu Node.js 20.19+ hoặc 22.12+.

```bash
npm install
npm run dev
```

Mở địa chỉ được Vite hiển thị trong terminal, thường là `http://localhost:5173`.

## Build production

```bash
npm run build
npm run preview
```

## Cấu trúc chính

- `src/App.tsx`: nội dung và dữ liệu giao diện.
- `src/index.css`: toàn bộ style, responsive và hiệu ứng hover.
- `src/main.tsx`: điểm khởi tạo React.
- `public/`: favicon và ảnh chia sẻ.

Ảnh sự kiện hiện dùng URL Unsplash, vì vậy máy cần có Internet để hiển thị đầy đủ hình ảnh và font Be Vietnam Pro.
