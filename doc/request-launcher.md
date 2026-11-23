# Request 對接與 Windows Launcher EXE 規格

## 1. 目標

將舊 Request 功能整理成可維護的模組，並提供獨立 Windows 圖形介面 EXE，讓管理者能設定、啟動、停止、監控及檢查伺服器。

「Request」與「Launcher」是兩種不同責任：Request 處理遊戲請求與既有協定；Launcher 管理服務程序。可以由同一個安裝包提供，但內部模組必須分離。

## 2. Request 模組責任

- 接收或承接遊戲客戶端請求。
- 解析協定封包並驗證格式。
- 根據封包類型分派 handler。
- 驗證登入狀態、玩家狀態與操作權限。
- 呼叫領域服務及 repository。
- 統一處理錯誤、回應封包及日誌。
- 保留舊客戶端所需的封包格式與行為。

在完成 Source Flash、Source Server 與 Request 原始碼盤點前，不應定義具體封包 ID、handler 名稱、欄位順序、加密方式或連線埠。

## 3. Request 內部分層

建議拆分：

1. Transport：Socket / 連線生命週期。
2. Protocol Codec：封包 framing、編碼、解碼、版本及必要的加解密。
3. Dispatcher：依封包類型分派。
4. Handlers：單一請求的處理邏輯。
5. Services：遊戲領域規則。
6. Repositories：資料庫存取。
7. Compatibility：舊版客戶端特殊行為與相容處理。

Handler 不應直接建立資料庫連線，也不應混入 GUI 或程序管理程式碼。

## 4. Launcher EXE 建議技術

桌面技術選型需考量 Windows 原生整合與現有團隊能力：

| 選項 | 適用情境 |
|---|---|
| C# / .NET 8+ / WPF | Windows 專用、需要成熟的程序管理及原生整合 |
| Tauri | 希望使用 Web UI 技術，但仍需要較輕量的桌面包裝 |
| Electron | 團隊熟悉 Node.js，且可接受較大的執行檔與記憶體占用 |

建議先確認目標 Windows 版本、是否需要系統服務、是否需管理舊 .NET Server，再選定技術。不要在同一個 Launcher 混用多套桌面框架。

## 5. Launcher 功能需求

### Dashboard

- 顯示各服務狀態：Stopped、Starting、Running、Stopping、Failed、Unknown。
- 顯示啟動時間、程序 ID、最後健康檢查時間。
- 顯示連線數、錯誤數等指標；只有服務實際提供資料時才顯示。
- 提供明確的服務名稱與版本。

### 服務控制

- Start：啟動服務。
- Stop：先要求正常關閉，逾時後才允許管理者選擇強制終止。
- Restart：執行 Stop，再確認已停止後 Start。
- 不允許重複啟動同一服務造成多個實例，除非該服務明確支援多實例。

### 設定管理

- 執行檔路徑與工作目錄。
- 啟動參數。
- 服務 Port、Bind Address。
- 資料庫檔案路徑。
- 日誌目錄與保留政策。
- 環境變數及秘密引用。

設定需有 schema、預設值、驗證與版本欄位。秘密應存 Windows Credential Manager 或受保護的系統儲存區，不以明文放在一般 JSON 設定檔。

### 日誌與診斷

- 即時顯示 stdout / stderr 或服務日誌來源。
- 支援等級、時間、服務篩選。
- 可開啟日誌資料夾。
- 顯示最近錯誤與診斷資訊。
- 避免在 GUI 顯示或匯出密碼、Token 等敏感資訊。

## 6. Launcher 與 Server 通訊

優先考慮以下模式：

### 本機 IPC

適合 Launcher 與伺服器在同一台 Windows 主機。可使用 Named Pipes 或本機 loopback 管理 API。必須限制存取權限，驗證呼叫者，並避免把管理端口暴露到公網。

### 管理 API

適合未來需要遠端管理。必須使用 TLS、強驗證、權限分級、審計日誌及速率限制。預設不應綁定 0.0.0.0。

Launcher 不應直接操作 Game Server 的內部記憶體，也不應直接改寫 SQLite 資料庫來達成管理功能。

## 7. 服務生命週期狀態

建議狀態轉移：

    Stopped -> Starting -> Running -> Stopping -> Stopped
                         -> Failed
    Running -> Failed
    Failed -> Starting

每次啟動需記錄啟動時間、程序 ID、工作目錄、設定版本及結果。程序異常退出後要更新狀態，不能只依賴 UI 上一次記錄的狀態。

健康檢查應與程序存活檢查分開：程序存在不代表服務可正常接受連線。

## 8. 插件介面

若 Request 要支援可擴充插件，應先定義穩定的 Plugin Contract：

- Plugin ID、名稱、版本及相容的 Host API 版本。
- 初始化、啟動、停止、Dispose 生命週期。
- 設定 schema 與驗證。
- 日誌介面。
- 可呼叫的受限服務介面。
- 權限宣告與相依性。
- 錯誤隔離及逾時策略。

插件載入方式必須依實際執行環境選擇。若插件是原生 DLL / .NET assembly，需明確處理版本相依、卸載限制及程序崩潰風險；若插件是獨立程序，則透過 IPC 傳遞有版本的訊息。不要直接載入不可信任的第三方程式碼。

## 9. 安全與驗收

- Launcher 管理權限與一般玩家權限完全分離。
- 預設僅允許本機管理。
- 設定檔錯誤時顯示可理解的驗證訊息，不得默默覆蓋。
- Start / Stop / Restart 具備逾時與錯誤處理。
- 伺服器崩潰後 UI 能正確顯示 Failed。
- Launcher 關閉時不得意外終止正在運行的伺服器，除非使用者明確選擇。
- 插件版本不相容時拒絕載入並記錄原因。
- 管理操作有審計紀錄。
