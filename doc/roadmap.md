# 重構實作路線圖

## 原則

採取漸進式重構，不一次刪除或替換所有舊系統。每階段都要有可執行成果、測試及回復方式。階段可部分並行，但協定與資料模型盤點必須先完成。

## Phase 0：Repository 與舊系統盤點

工作：

- 建立完整目錄清單與檔案分類。
- 確認 apps/、packages/ 是否已存在，以及 workspace package 清單。
- 盤點 ClientWeb、Db、Request、Source Flash、Source Server、Web、Tools。
- 找出所有啟動入口、建置腳本、設定檔、環境變數、Port 與外部依賴。
- 建立舊系統服務拓樸及呼叫關係。
- 匯出 MSSQL schema 與資料庫物件清單。
- 確認可用的 SWF、建置來源及實際 Ruffle 測試檔。

完成條件：

- 每個重要資料夾都有用途、入口與依賴紀錄。
- 已識別目前可建置、可執行的舊元件。
- Request 與 Flash 封包流程至少有初步對照。
- 資料庫物件清單可供遷移設計使用。

## Phase 1：協定與領域模型

工作：

- 追蹤 Flash 登入、連線、心跳、遊戲大廳、戰鬥及結算封包。
- 對照 Source Flash、Source Server、Request handler。
- 定義封包 framing、編解碼、錯誤處理與版本相容策略。
- 建立共用 protocol package 的測試向量。

完成條件：

- 主要封包有來源、方向、欄位、型別及處理端對照。
- 能以測試向量驗證封包編解碼。
- 未確認的協定行為有明確標記，不以猜測補齊。

## Phase 2：SQLite 資料層

工作：

- 完成 MSSQL schema 盤點與 SQLite 對照。
- 建立 database package、migration 與 repository。
- 建立資料匯出、轉換、匯入及驗證工具。
- 以測試資料完成完整遷移演練。

完成條件：

- Schema migration 可重複執行。
- 重要資料筆數及關聯驗證通過。
- 備份與還原程序測試通過。
- 關鍵查詢及交易行為通過測試。

## Phase 3：Game Server 與 Request 相容層

工作：

- 把 Transport、Protocol、Dispatcher、Handlers、Services、Repositories 分層。
- 先保留舊封包格式，再逐步替換內部實作。
- 建立登入及主要遊戲流程端對端測試。
- 讓 Game Server 使用新的 database package。

完成條件：

- 舊客戶端可連接新伺服器。
- 登入、大廳、角色資料及核心遊戲流程可測試。
- 伺服器錯誤、斷線與重連行為可預期。

## Phase 4：Web 前後端分離

工作：

- 建立 Vue 3 / TypeScript / Vite 前端。
- 建立 Node.js API。
- 實作 Auth、Account、Player、Game 等必要 API。
- 加入 Session、安全設定、API schema 及測試。
- 建立 Game Shell。

完成條件：

- 註冊、登入、登出流程正常。
- 前端不直接存取資料庫。
- API 權限與輸入驗證完整。
- Build 與 typecheck 通過。

## Phase 5：Ruffle 整合

工作：

- 固定 Ruffle 版本。
- 載入目標 SWF。
- 測試 ActionScript API、網路連線及跨網域 policy。
- 完成登入到核心遊戲流程測試。
- 記錄不相容項目並決定最小修正方案。

完成條件：

- 實際 SWF 在目標瀏覽器可執行。
- 遊戲協定連線成功。
- 核心遊戲流程通過端對端測試。
- 失敗情境有明確錯誤與診斷資訊。

## Phase 6：Windows Launcher / Plugin

工作：

- 決定桌面技術。
- 定義管理介面與權限模型。
- 實作程序生命週期、狀態監控、設定及日誌。
- 定義 Request Plugin Contract 與版本相容策略。
- 加入安裝、更新及設定備份流程。

完成條件：

- 能安全啟動、停止、重啟及監控服務。
- 服務異常退出時狀態正確。
- 管理操作有審計紀錄。
- 插件版本不相容時能拒絕載入並提供原因。

## Phase 7：整合、部署與回復

工作：

- 建立乾淨環境部署文件。
- 設定正式環境秘密與網路規則。
- 完成資料庫備份、還原與升級演練。
- 完成壓力、故障與安全測試。
- 定義版本標記、Release 與回復流程。

完成條件：

- 新環境可依文件部署。
- 服務可監控並可復原。
- 舊系統保留至新系統正式驗收。
- 每次 Release 都有變更紀錄及回復方式。

## 優先級建議

最高優先：Phase 0、Phase 1。  
第二優先：Phase 2、Phase 3。  
第三優先：Phase 4、Phase 5。  
第四優先：Phase 6、Phase 7。

Launcher 可以早期製作 UI 原型，但真正整合應等待伺服器管理介面與生命週期契約確定。
