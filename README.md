# Linh Makeup Beauty Website

Dự án gồm:

- `frontend`: Next.js + TypeScript + Tailwind CSS.
- `backend`: C# ASP.NET Core + PostgreSQL.
- `frontend/public/images`: ảnh gốc copy từ bộ giao diện để giữ đúng người mẫu/sản phẩm.

## Kiểm thử responsive cần chạy khi có đủ runtime

- Mobile: 375px, 390px, 430px.
- Tablet dọc: 768px, 820px.
- Tablet ngang: 1024px, 1180px.
- Desktop: 1366px, 1440px, 1920px.

## Build

```powershell
cd frontend
npm install
npm run build

cd ../backend/Beauty.Api
dotnet restore
dotnet build
```

Máy hiện tại cần cài Node.js/npm và .NET SDK để chạy các lệnh build trên.
