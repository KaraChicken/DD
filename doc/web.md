# Web 前後端分離規格

## 1. 目標與範圍

建立現代化網站，將畫面呈現與伺服器業務邏輯分離。Web 前端處理頁面、狀態及 Ruffle 容器；Node.js API 負責帳號、Session、網站資料與管理操作。

專案 README 已描述 Vue 與 Node.js 方向；根目錄 package.json 已存在 dev:web、dev:api 等 workspace scripts。實際 apps/web 與 apps/api 是否已建立，仍須以目前分支檔案盤點確認。

## 2. 建議技術選型

| 項目 | 建議 |
|---|---|
| 前端框架 | Vue 3 |
| 語言 | TypeScript |
| 建置工具 | Vite |
| 路由 | Vue Router |
| 狀態管理 | Pinia（僅在確有跨頁狀態需求時） |
| UI | 選擇一套元件系統或自行建立設計系統，避免混用多套 |
| 後端 | Node.js + TypeScript |
| HTTP 框架 | Fastify 或 Express，需在實作前選定一套 |
| API 文件 | OpenAPI |
| 驗證 | 安全的伺服器端 Session 優先；若採 Token，需定義更新與撤銷機制 |
| 測試 | Vitest；API 可使用整合測試工具 |

上述為建議技術，不代表目前 repository 已安裝或完成設定。

## 3. 前端功能分區

### 公開頁面

- 首頁及遊戲介紹。
- 註冊、登入、登出。
- 系統公告與維護狀態。
- 錯誤及服務不可用提示。

### 登入後頁面

- 玩家基本資訊。
- 遊戲啟動入口。
- Game Shell：負責 Ruffle Player 的版面、載入、錯誤與全螢幕操作。
- 帳號相關設定。

Flash 遊戲內部操作仍由 SWF 負責；Vue 不應重做遊戲內角色、背包、戰鬥等介面，除非未來決定替換 Flash 客戶端。

## 4. API 責任

API 應提供版本化路徑，例如 /api/v1。實際 endpoint 必須依現有功能及資料模型盤點後定案。

建議功能群組：

| 功能群組 | 責任 |
|---|---|
| Auth | 註冊、登入、登出、目前登入者 |
| Account | 帳號設定與帳號狀態 |
| Player | 玩家基本資料及網站需要顯示的資訊 |
| Game | 遊戲入口、啟動憑證或連線資訊 |
| News | 公告及維護資訊 |
| Admin | 受授權的伺服器管理操作 |

每個 endpoint 必須定義 HTTP method、輸入 schema、輸出 schema、錯誤碼、權限、速率限制及測試。

## 5. 身分驗證與安全

- 密碼使用成熟的密碼雜湊演算法及安全參數儲存，不可明文或可逆加密保存。
- 登入與註冊需有速率限制及輸入驗證。
- Session Cookie 設定 HttpOnly、Secure（HTTPS 環境）及適當 SameSite。
- 若採 Cookie Session，需防範 CSRF；若採 Token，需妥善處理 Token 儲存、更新、撤銷及 XSS 風險。
- 權限必須在 API 後端驗證，不可只依賴前端隱藏按鈕。
- 管理 API 不得公開給一般玩家。
- 密碼、Session ID、Access Token 及資料庫憑證不得寫入一般日誌。

## 6. Game Shell 與 Ruffle 整合責任

Web 只負責：

1. 確認使用者已登入。
2. 從 API 取得遊戲連線所需的短效資訊（如果舊協定允許此模式）。
3. 載入指定版本的 Ruffle runtime 與 SWF。
4. 顯示載入、錯誤、斷線及重新連線狀態。
5. 依實際 Ruffle API 提供全螢幕、尺寸及必要設定。

不能把長期有效的伺服器管理憑證、資料庫密碼或其他秘密放進前端程式。SWF 內嵌的既有連線設定也必須檢查，避免暴露管理端資訊。

## 7. 開發與建置

根目錄 scripts 應維持清楚且一致：

- dev:web：啟動前端開發伺服器。
- dev:api：啟動 API 開發伺服器。
- build：建置所有 workspace package。
- typecheck：檢查所有 workspace 的型別。

各 app/package 應自行宣告 scripts；workspace 執行失敗時要明確指出是哪個 package 缺少命令，不應以空腳本掩蓋錯誤。

## 8. 驗收條件

- 未登入使用者無法存取需要登入的頁面及 API。
- 註冊、登入、登出及 Session 過期流程可重複測試。
- API 輸入驗證、錯誤格式與權限檢查一致。
- 前端不直接連接 SQLite。
- Game Shell 可載入指定 SWF，並能顯示載入失敗原因。
- 前端 production build 與 typecheck 通過。
