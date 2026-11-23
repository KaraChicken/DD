# MSSQL 至 SQLite 遷移規格

## 1. 目標

將目前舊系統使用的 MSSQL 資料結構與資料遷移至 SQLite，讓新 API 與 Game Server 使用一致的資料存取層。

本文件尚未列出具體資料表及欄位，因為必須先盤點舊 Db 專案、SQL schema、Stored Procedure、Trigger、View 及實際資料庫備份。不得在未取得 schema 前臆造資料表。

## 2. 遷移原則

- 先盤點，再設計 schema，再轉資料，最後切換讀寫。
- 舊 MSSQL 保留為唯讀來源或備份，直到新系統驗收完成。
- 所有結構修改透過 migration 管理。
- 資料轉換必須可重複執行，並記錄筆數、錯誤與摘要。
- 遷移前後要比較筆數、主鍵、外鍵關係、重要欄位及業務規則。
- 不允許在正式環境以未測試的自動轉換腳本直接覆蓋資料。

## 3. SQLite 技術規格

### 連線與並行

- 建議啟用 WAL，以改善讀寫並行；仍須理解 SQLite 同一時間只有一個 writer 的限制。
- 設定合理的 busy_timeout。
- 以短交易完成寫入，避免長時間佔用 writer lock。
- 依資料一致性需求設定 foreign_keys。
- 正式環境須有一致的 journal、synchronous、cache 與 timeout 設定，並記錄其選擇理由。
- 使用參數化 SQL，禁止拼接使用者輸入。
- 以備份 API 或安全的 checkpoint/backup 流程備份資料庫，不應在高寫入時隨意複製單一主資料檔而忽略 WAL。

### 型別對應參考

| MSSQL | SQLite 建議 | 注意事項 |
|---|---|---|
| INT / BIGINT | INTEGER | 確認值域及主鍵策略 |
| BIT | INTEGER 0/1 | 在應用層轉為 boolean |
| NVARCHAR / VARCHAR | TEXT | 確認編碼、長度及排序需求 |
| DECIMAL / NUMERIC | NUMERIC 或整數最小單位 | 金額、精度敏感數值不可直接使用浮點 |
| DATETIME / DATETIME2 | TEXT（ISO 8601 UTC）或 INTEGER epoch | 全系統統一時區及精度 |
| UNIQUEIDENTIFIER | TEXT | 統一 UUID 格式 |
| VARBINARY / IMAGE | BLOB | 評估大型資產是否應存檔案系統 |
| IDENTITY | INTEGER PRIMARY KEY，必要時 AUTOINCREMENT | 不要預設每張表都需要 AUTOINCREMENT |

SQLite 型別親和性與 MSSQL 嚴格型別行為不同。應用程式必須自行驗證輸入範圍與資料格式。

## 4. MSSQL 特有功能盤點

逐一檢查並處理：

- Stored Procedure：轉成明確的 repository/service 業務邏輯或 SQLite SQL。
- Trigger：確認是否可用應用層交易處理，避免隱藏副作用。
- View：轉為 SQLite View 或查詢層。
- Identity：確認 ID 產生及匯入舊 ID 的策略。
- GETDATE、DATEADD、DATEDIFF、ISNULL、TOP、NEWID 等函式與語法差異。
- OUTPUT INSERTED、MERGE、臨時表、Table-Valued Parameter 等 MSSQL 專屬用法。
- Collation、大小寫、排序、NULL 比較與唯一索引語意。
- 交易隔離、鎖定及錯誤回滾行為。
- 依賴 SQL Server Agent 或外部排程的工作。

## 5. 建議資料存取架構

應用程式不直接散布 SQL：

- database package：建立連線、PRAGMA、migration、交易與備份。
- repositories：封裝各領域的查詢及寫入。
- services：處理跨 repository 的業務流程與交易邊界。
- migrations：依序執行版本化 schema 變更。

Repository 介面應由領域需求決定，不要為了抽象而建立過度通用的 SQL builder。

## 6. 遷移流程

1. 從舊 Db 專案與實際 MSSQL 匯出完整 schema。
2. 列出所有表、欄位、索引、主外鍵、預設值、約束與資料量。
3. 列出所有 Stored Procedure、Trigger、View 與呼叫端。
4. 建立 MSSQL 到 SQLite 對照表及差異處理。
5. 建立 SQLite 初始 migration。
6. 撰寫匯出與匯入工具，支援批次、交易及錯誤報告。
7. 在測試資料庫執行遷移。
8. 驗證筆數、關聯、重要數值及抽樣資料。
9. 對新 SQLite 執行完整 API / Game Server 測試。
10. 正式切換前先停寫或採用明確的資料同步窗口，避免遷移期間資料分歧。
11. 保留可回復的 MSSQL 備份與切換紀錄。

## 7. 驗證項目

- 每張表的遷移前後筆數。
- 主鍵唯一性與外鍵完整性。
- NULL、空字串、0、日期、布林及二進位資料。
- 角色、物品、貨幣、任務、郵件等重要遊戲資料的業務一致性（實際領域依舊 schema 確認）。
- 關鍵查詢的結果一致性。
- 多個讀取者與寫入者下的鎖定、timeout、回滾及重啟恢復。
- 備份、還原及 migration rollback/forward 策略。

## 8. 尚待確認

- MSSQL 版本及資料庫編碼 / Collation。
- 實際資料表、Stored Procedure、Trigger、View 數量。
- 是否存在 SQL Server Agent 工作或外部整合。
- 預期同時在線人數、寫入頻率與資料庫大小。
- SQLite 是單一主機本機檔案，還是會被多台主機共用。SQLite 不應直接放在一般網路共享磁碟上供多台伺服器同時寫入。
