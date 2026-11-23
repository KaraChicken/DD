# DD 重構設計文件

本目錄集中記錄 DD 專案的現代化重構目標、模組邊界、介面契約、資料遷移策略、測試要求與實作順序。

## 文件索引

| 文件 | 範圍 |
|---|---|
| [architecture.md](architecture.md) | 整體系統架構、模組責任、資料流與設計原則 |
| [web.md](web.md) | Web 前後端分離、前端、API、驗證及遊戲入口 |
| [database.md](database.md) | MSSQL 至 SQLite 的資料模型與遷移策略 |
| [request-launcher.md](request-launcher.md) | Request 對接、Windows EXE 管理器及插件介面 |
| [ruffle-flash.md](ruffle-flash.md) | Ruffle、SWF 載入、Flash 協定及相容性驗證 |
| [roadmap.md](roadmap.md) | 分階段實作順序、驗收條件及風險清單 |
| [decisions.md](decisions.md) | 已確認方向、暫定決策與待驗證事項 |

## 文件使用規則

- 本文件描述的是目標架構與實作規格，不代表相關功能已經完成。
- 「已確認」表示來自專案目前可讀取的 README、package.json、pnpm-workspace.yaml 或使用者明確指定的方向。
- 「待盤點」表示必須檢查既有原始碼、設定檔、資料庫結構或實際執行結果後才能定案。
- 任何協定欄位、資料表、封包 ID、Port、啟動參數及 API 路徑，未經原始碼或執行測試證實前，不得當作既有事實。
- 修改本文件時，應同步更新 decisions.md 與 roadmap.md，避免規格和實作進度脫節。

## 目前可確認的專案狀態

- 專案 README 將現代化重構描述為 Vue 前端、Node.js 後端、SQLite，以及保留 Flash/SWF 客戶端。
- 根目錄 package.json 使用 pnpm，並已有 dev:web、dev:api、build、typecheck 等工作命令。
- pnpm-workspace.yaml 的 workspace glob 是 apps/* 與 packages/*。
- README 目前提到的規劃目錄為 app/，與 workspace glob 的 apps/、packages/ 命名並不一致；本文件建議以實際 workspace 設定為基礎，統一採用 apps/ 與 packages/。
- 舊版程式碼仍是理解既有功能、封包、資料結構與相容性的主要依據，遷移前應保留並盤點。

## 建議閱讀順序

先讀 architecture.md，再依工作項目閱讀 web.md、database.md、request-launcher.md、ruffle-flash.md，最後依 roadmap.md 安排實作。
