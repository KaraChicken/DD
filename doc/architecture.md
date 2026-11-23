# 整體系統架構

## 1. 目標

將 DD 重構為可獨立開發、測試、部署與維護的系統，同時保留原有 DDTank 4.1 Flash 客戶端及必要的遊戲協定相容性。

本次重構分為四個主要方向：

1. Web：前後端分離，使用現代前端與 Node.js API。
2. Database：將 MSSQL 資料模型與資料遷移至 SQLite。
3. Request / Launcher：將 Request 對接能力模組化，並提供 Windows 圖形介面 EXE 管理伺服器。
4. Flash：以 Ruffle 在瀏覽器中執行既有 SWF，盡量不重寫遊戲 UI。

## 2. 目標模組

建議的目標目錄如下。此為目標規劃，不代表目前所有目錄或程式已存在。

    DD/
    ├── apps/
    │   ├── web/                 # Vue 網站與 Game Shell
    │   ├── api/                 # HTTP API、帳號與網站業務
    │   ├── game-server/         # 遊戲協定與遊戲服務
    │   └── launcher/            # Windows 桌面管理程式（若採獨立專案）
    ├── packages/
    │   ├── database/            # SQLite schema、migration、repository
    │   ├── protocol/            # 封包定義、編解碼與協定型別
    │   ├── request/             # Request handler 與相容層
    │   ├── shared/              # 共用型別、錯誤碼與設定模型
    │   └── ruffle/              # Ruffle 整合資源與載入程式
    ├── doc/
    ├── legacy/
    │   ├── ClientWeb/
    │   ├── Db/
    │   ├── Request/
    │   ├── Source Flash/
    │   ├── Source Server/
    │   ├── Web/
    │   └── Tools/
    ├── package.json
    └── pnpm-workspace.yaml

既有資料夾是否全部移入 legacy/，必須等完成引用、建置腳本、相對路徑與部署方式盤點後再執行。不可在尚未確認依賴時直接搬移。

## 3. 系統關係

    Browser
      ├── Vue Web ── HTTPS/JSON ──> Node.js API ──┐
      │                                          │
      └── Ruffle ── SWF game protocol ──> Game Server
                                                 │
    Windows Launcher ── Local IPC / Admin API ───┤
                                                 │
                                         Database Layer
                                                 │
                                              SQLite

Web API 與 Game Server 可以是同一個部署單元，也可以獨立程序；但程式模組與責任必須分離。最終部署型態應根據既有伺服器的啟動方式、Socket 模型與負載測試決定。

## 4. 模組責任與邊界

### Web

- 顯示首頁、登入、註冊、玩家資訊與遊戲入口。
- 管理瀏覽器端 Session 與遊戲啟動狀態。
- 提供 Ruffle Player 的容器及載入設定。
- 不重新實作 Flash SWF 已經負責的遊戲內 UI，除非未來另行決定替換客戶端。

### API

- 處理網站使用的 HTTP API。
- 負責帳號驗證、註冊、Session、玩家網站資料及管理功能。
- 驗證輸入、授權與請求速率。
- 不直接承擔所有遊戲封包處理；遊戲即時通訊應由 Game Server / Protocol 層負責。

### Game Server / Protocol / Request

- 實作或承接舊客戶端所需的遊戲通訊協定。
- 解析封包、驗證連線狀態、分派 Request handler。
- 將遊戲規則與資料存取分離。
- 維持舊 SWF 所要求的封包格式、回應順序及錯誤行為。
- 具體封包 ID、加密、序列化、握手、心跳與斷線流程，必須由 Source Flash、Source Server、Request 等原始碼確認。

### Database

- 統一提供資料存取介面。
- 管理 schema 與版本 migration。
- 管理交易、連線生命週期、備份與還原。
- 不允許各模組各自拼接 SQL 或自行建立不同的資料庫 schema。

### Launcher

- 提供 Windows 圖形化伺服器管理介面。
- 負責啟動、停止、重啟、狀態、日誌與設定。
- 透過明確的管理 API 或 IPC 與伺服器溝通。
- 不應將遊戲核心直接塞入 GUI 程序，也不應繞過服務層任意修改資料庫。

### Ruffle

- 在瀏覽器中載入既有 SWF。
- 提供遊戲畫面及 ActionScript 執行環境。
- 不負責替代遊戲伺服器，也不會自動修復舊 SWF 的所有相容性問題。
- 與伺服器之間仍須符合原客戶端的協定及網路安全要求。

## 5. 通訊介面原則

每條跨模組通訊都必須定義：

- 傳輸方式與連線生命週期。
- 訊息 schema、欄位型別與版本。
- 身分驗證與授權方式。
- Timeout、重試、斷線與錯誤處理。
- 日誌欄位及敏感資料遮罩。
- 相容性策略與測試案例。

建議的介面分類：

| 呼叫端 | 目標 | 介面 |
|---|---|---|
| Web | API | HTTPS + JSON |
| SWF / Ruffle | Game Server | 舊客戶端實際使用的遊戲協定 |
| Launcher | Server 管理端 | 本機 IPC 或受驗證的管理 API |
| API / Game Server | Database | 共用 Database package |
| API | Game Server | 內部服務介面；依部署需求決定 HTTP、IPC 或訊息通道 |

不能假設 Ruffle 中的 SWF 一定能使用所有瀏覽器網路功能。實際 Socket 能力、跨網域政策與安全限制必須以目標 SWF 和 Ruffle 版本測試。

## 6. 共用技術原則

- 優先使用 TypeScript 管理新程式碼的型別與介面；舊版程式碼可依實際技術棧逐步遷移。
- 以 pnpm workspace 管理 apps/* 與 packages/*。
- 新模組必須有明確入口、設定 schema、建置與測試命令。
- 設定與秘密不得硬編碼在原始碼或提交到 Git。
- 開發、測試與正式環境使用不同設定檔及資料庫。
- 日誌應包含時間、等級、模組、事件及關聯 ID，不記錄明文密碼或 Token。
- 所有資料庫結構變更都必須有可追蹤的 migration。
- 先以相容層承接舊協定，再逐步替換內部實作。

## 7. 非目標

目前不把下列項目視為既定工作：

- 重新製作整套 Flash 遊戲 UI。
- 未盤點前直接刪除舊版伺服器或資料庫程式。
- 未經測試就更改舊 SWF 的封包格式。
- 將所有服務強制拆成微服務。
- 讓 Launcher 直接取代 Game Server。
