# DD App 重構

目標：以 Vue 作為首頁 / 登入 / 註冊與 Game Shell，以 Flash/SWF 作為登入後的遊戲前端；Node.js 負責 Flash-compatible HTTP/TCP protocol、遊戲 domain 與 SQLite。

## 架構

- `web/`：Vue 網站與 Flash Game Shell
- `api/`：Node.js backend
- `db/`：SQLite schema / seed / migration
- `flash/`：Flash protocol mapping 與相容層文件

## 原則

1. Flash 是遊戲內前端，遊戲內操作直接與 backend 通訊。
2. Vue 不重做遊戲 UI。
3. Domain service 不依賴傳輸協定；Flash HTTP、Flash TCP 與 Vue API 共用 domain service。
4. 優先相容 `Source Flash` 與既有 `Request` / binary protocol。
5. SQLite 為第一版預設資料庫。
