# 架構決策紀錄

本文件記錄重構方向與尚未定案的選項。決策狀態分為 Confirmed（已確認方向）、Proposed（建議方案）、Pending（待驗證或選擇）。

## D-001：重構採模組化漸進遷移

狀態：Confirmed

決策：保留舊程式碼作為功能與相容性參考，逐步將新系統放入現代化 workspace。未盤點依賴前不直接刪除或大規模搬移舊資料夾。

## D-002：Web 前後端分離

狀態：Confirmed

決策：Web 前端與 Node.js 後端分離。README 已描述 Vue 與 Node.js 的方向。新程式建議採 TypeScript，API 使用明確 schema。

待定：HTTP 框架、UI 元件系統、Session 實作與部署拓樸。

## D-003：MSSQL 遷移至 SQLite

狀態：Confirmed

決策：新系統資料庫採 SQLite，並建立統一 database package、版本化 migration、備份與資料驗證流程。

待定：實際 schema、SQLite driver、ORM 或 query builder 選擇、資料庫大小及寫入負載是否適合單機 SQLite。

## D-004：Request 與 Launcher 分離責任

狀態：Confirmed

決策：Request 負責遊戲請求及協定處理；Windows EXE 負責伺服器啟停、設定、狀態與日誌。兩者可同一安裝包，但不應將 GUI 和遊戲核心耦合。

待定：Launcher 使用 C# WPF、Tauri 或 Electron；插件採同程序載入或獨立程序 IPC；管理介面採 Named Pipes 或本機 API。

## D-005：使用 Ruffle 執行既有 SWF

狀態：Confirmed

決策：以 Ruffle 作為瀏覽器端 Flash 執行環境，盡量保留原 SWF 的遊戲 UI 與既有協定。

待定：目標 SWF 對 Ruffle 的實際相容程度、Socket API、policy file、外部資源及必要的 SWF 修正。

## D-006：workspace 目錄命名

狀態：Proposed

決策：以根目錄 pnpm-workspace.yaml 的 apps/*、packages/* 為準，目標新程式採 apps/ 與 packages/。README 中原先規劃的 app/ 命名應在後續同步修正，避免兩種結構並存。

## D-007：文件目錄

狀態：Confirmed

決策：本次設計文件統一放在根目錄 doc/，使用 Markdown（.md）。

## 尚待盤點的關鍵資訊

- 舊版 Server 的實際啟動入口、依賴、Port、程序數量及服務順序。
- Request 的封包處理入口、handler 清單與外部依賴。
- Flash 客戶端協定的完整流程及實際 SWF 版本。
- MSSQL schema、Stored Procedure、Trigger、View、資料量與資料關聯。
- Web 舊版登入、Session、Cookie 及頁面流程。
- Ruffle 對目標 SWF 的網路與 ActionScript 相容性。
- Launcher 是否需要支援遠端管理、多伺服器及 Windows Service。

## 決策更新規則

新增決策時使用 D-008、D-009 等連續編號。若既有決策改變，更新原條目並記錄原因，不要保留互相矛盾的規格。
