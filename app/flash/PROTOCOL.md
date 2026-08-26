# Flash Protocol 重構

`Source Flash` 是 Client 行為與 protocol 的主要參考；舊 `Request/` 與 Server code 是 server-side compatibility reference。

## 分層

```text
Flash/SWF
  -> HTTP/XML bootstrap 或 TCP binary protocol
  -> app/api transport
  -> domain services
  -> SQLite
```

## 第一階段

- LOGIN / session handshake
- Player bootstrap
- Inventory
- Equipment
- Shop
- Quest
- Activity

不要用 Vue API 取代 Flash game protocol。Vue 只負責首頁、登入、註冊與登入後的 Game Shell。
