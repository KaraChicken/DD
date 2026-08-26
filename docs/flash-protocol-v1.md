# Flash Protocol V1

The Flash SWF is the game client after login. Vue is only the website shell. Game actions must go through the Node.js backend.

## First compatibility endpoints

All endpoints are intentionally exposed under the original `Request/*.ashx` paths so the existing Flash client can be pointed at the new server without changing the client URL convention.

| Legacy endpoint | Node endpoint | Status |
|---|---|---|
| `LoadUserItems.ashx` | `/Request/LoadUserItems.ashx?ID=<playerId>` | V1 XML |
| `LoadUserEquip.ashx` | `/Request/LoadUserEquip.ashx?ID=<playerId>` | V1 XML |
| `ShopItemList.ashx` | `/Request/ShopItemList.ashx` | V1 XML |
| `QuestList.ashx` | `/Request/QuestList.ashx` | V1 XML |
| `ActiveList.ashx` | `/Request/ActiveList.ashx` | V1 XML |

## Authentication

The current V1 requires the `dd_session` JWT cookie. The `ID` query parameter is accepted for legacy shape, but it must match the authenticated player ID. This prevents the old `ID=123` pattern from becoming an arbitrary-player data endpoint.

## XML compatibility

The old C# Request layer returns XML and commonly uses `Result` attributes such as `value="True"` and `message="Success!"`. The Node V1 keeps this outer shape.

The old implementation also compresses some responses with `csFunction.CreateCompressXml`. The exact compression/encoding expected by the SWF has not yet been verified from the SWF bytecode, so V1 deliberately returns plain XML first. Do not call this protocol fully Flash-compatible until the SWF parser/decompression path is confirmed.

## Next protocol work

1. Inspect SWF request URLs and XML parsing/decompression.
2. Confirm exact `FlashUtils.CreateGoodsInfo/CreateShopInfo/CreateQuestInfo/CreateActiveInfo` attribute names.
3. Implement the exact legacy response encoding.
4. Add login/SID/server-list compatibility.
5. Add write operations (equip, buy, quest reward) using the same Flash protocol conventions.
