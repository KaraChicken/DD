# Flash / SWF 與 Ruffle 整合規格

## 1. 目標

保留既有 Flash SWF 作為主要遊戲客戶端，使用 Ruffle 在瀏覽器中執行，避免在第一階段重製遊戲內 UI。

Ruffle 是 Flash Player 相容執行環境，不是遊戲伺服器，也不會自動把舊協定轉換成 HTTP API。SWF 仍須使用遊戲伺服器理解的協定。

## 2. 整合位置

- Web 前端提供 Game Shell。
- Game Shell 載入固定版本的 Ruffle runtime。
- Ruffle 載入受信任的 SWF。
- SWF 依原有協定連接 Game Server。
- API 負責網站登入及必要的啟動資訊；不得把管理秘密傳給瀏覽器。

Ruffle 版本應固定並記錄，不要在正式環境直接引用未固定版本的浮動 CDN URL。SWF、Ruffle runtime 及相關設定應有版本與校驗資訊。

## 3. SWF 盤點

從 Source Flash 與實際 SWF 檔案確認：

- SWF 版本及 ActionScript 版本。
- 入口 Document Class / 主時間軸流程。
- 遊戲伺服器位址及 Port 的取得方式。
- Socket、XMLSocket、HTTP、URLLoader 等網路 API 使用情形。
- 跨網域 policy file 行為。
- 外部 SWF、圖片、音訊、字型與設定檔載入方式。
- SharedObject、LocalConnection、ExternalInterface 等 API 使用情形。
- 是否依賴 Flash Player 特有行為。
- 是否存在硬編碼網域、IP、Port 或舊伺服器路徑。

不得僅憑 Source Flash 原始碼推定發布用 SWF 與原始碼完全一致；應確認 SWF 的建置版本及來源。

## 4. Ruffle 相容性驗證

建立獨立的相容性測試頁，按流程逐項驗證：

1. Ruffle runtime 成功載入。
2. SWF 可解析並顯示初始畫面。
3. 字型、圖片、音訊及動畫正常。
4. 登入流程可完成。
5. Socket / XMLSocket 等實際網路功能可用。
6. 跨網域政策與瀏覽器安全限制符合預期。
7. 大廳、角色、背包及戰鬥流程可操作。
8. 封包內容、回應順序及伺服器狀態符合舊客戶端預期。
9. 斷線、逾時、重新登入及重新載入可恢復。
10. 不同瀏覽器與作業系統下結果一致。

任何一項不支援，都應記錄最小重現 SWF、Ruffle 版本、瀏覽器版本、錯誤訊息及受影響的 ActionScript API。

## 5. 網路與安全

- 以 HTTPS 提供 Web 與 SWF 資源。
- 遊戲 Socket 的目標網域、Port 與跨網域 policy 必須經過實測。
- 不應假設瀏覽器頁面的 CORS 設定可以取代 Flash Socket policy。
- policy server 只能授權必要來源與必要 Port，避免使用過度寬鬆的萬用規則。
- 不將資料庫密碼、伺服器管理 Token 或其他秘密編入 SWF。
- 若登入後要向 SWF 傳遞短效憑證，必須先確認舊協定是否支援及其安全性；不可把長期帳密直接放在 URL。
- 對 SWF、外部資源及 Ruffle runtime 使用可信任來源與版本控管。

## 6. Web Game Shell 行為

Game Shell 應提供：

- 載入中狀態。
- SWF 載入失敗與錯誤診斷。
- 遊戲區域自適應尺寸。
- 全螢幕控制。
- 手動重新載入。
- 伺服器維護與連線失敗提示。
- 避免瀏覽器頁面離開或刷新造成不必要的狀態遺失提示（若可行）。

不要在前端自行解析或修改遊戲封包，除非有明確且經安全評估的需求。

## 7. 已知風險

- Ruffle 對 Flash API 的支援程度可能與原 Adobe Flash Player 不同。
- 特定 ActionScript API 或網路行為可能尚未完整支援。
- 舊 SWF 可能依賴 Flash Player 的安全沙箱、policy file 或特定瀏覽器插件行為。
- SWF 可能硬編碼舊伺服器位址，導致無法切換環境。
- 遊戲畫面能載入不代表登入、Socket、戰鬥及資料保存都可用。

若關鍵網路 API 無法由目標 Ruffle 版本支援，應先評估 SWF 的最小相容性修正或協定橋接方案，再決定是否需要修改客戶端。不可未經驗證就承諾完全相容。

## 8. 驗收條件

以實際目標 SWF 完成從登入到遊戲內核心流程的端對端測試，並保存測試環境、Ruffle 版本、瀏覽器版本及結果。至少涵蓋登入、大廳、角色資料、戰鬥、結算、斷線與重新連線。
