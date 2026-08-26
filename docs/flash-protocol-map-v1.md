# Flash Protocol Map v1

## Scope

`Source Flash` is the primary client-side protocol reference. The Flash client is the game frontend; Vue is only the website shell/login/register/lobby container.

## Confirmed transport split

There are two different server communication layers:

1. **HTTP Request/XML** for bootstrap/configuration style endpoints such as the legacy `Request/*.ashx` handlers.
2. **Persistent binary socket** for in-game state and actions. `SocketManager` owns a `ByteSocket`, and `GameSocketOut` sends `PackageOut` packets.

Do not model the game as a collection of JSON REST calls. The socket protocol is required for Flash gameplay compatibility.

## Confirmed Flash socket packet types

From `Source Flash/scripts/ddt/data/socket/ePackageType.as`:

| Packet | Code | Purpose |
|---|---:|---|
| LOGIN | 1 | game-server login handshake |
| PING | 4 | keepalive |
| RSAKEY | 7 | RSA key exchange |
| SCENE_LOGIN | 16 | scene login |
| DELETE_GOODS | 42 | delete item |
| BUY_GOODS | 44 | buy goods |
| UPDATE_COUPONS | 46 | currency update |
| UNCHAIN_EQUIP | 47 | unequip |
| SEll_GOODS | 48 | sell goods |
| CHANGE_PLACE_GOODS | 49 | move item |
| UPDATE_GOODS | 51 | item update |
| CHAIN_EQUIP | 52 | equip |
| REPAIR_GOODS | 53 | repair |
| PROP_BUY | 54 | prop purchase |
| PROP_SELL | 55 | prop sale |
| ITEM_COMPOSE | 58 | compose |
| ITEM_STRENGTHEN | 59 | strengthen |
| ITEM_HIDE | 60 | hide item |
| ITEM_TRANSFER | 61 | transfer |
| ITEM_CONTINUE | 62 | continue/use item |
| ITEM_OPENUP | 63 | open item/box |
| ITEM_STORE | 79 | item storage |
| GAME_ROOM | 94 | room/game control |
| GAME_CMD | 91 | battle/game command |
| QUEST_ADD | 176 | quest added |
| QUEST_REMOVE | 177 | quest removed |
| QUEST_UPDATE | 178 | quest updated |
| QUEST_FINISH | 179 | quest finished |
| QUSET_OBTAIN | 180 | quest obtained |
| QUEST_CHECK | 181 | quest check |
| ACTIVE_PULLDOWN | 11 | activity/event pull-down |
| DAILY_AWARD | 13 | daily award |
| GET_SIGNAWARD | 90 | sign-in reward |

## Confirmed login handshake

`SocketManager.connect(host, port)` creates the `ByteSocket`. On connect it calls `GameSocketOut.sendLogin(AccountInfo)`.

`sendLogin()`:

1. resets the socket key;
2. writes UTC timestamp fields;
3. generates an 8-byte random socket key;
4. writes `Account,Password`;
5. RSA-encrypts the payload with `AccountInfo.Key`;
6. creates `PackageOut(ePackageType.LOGIN)`;
7. writes `Version.Build` and desktop type;
8. writes the encrypted bytes;
9. sends the package;
10. installs the generated 8-byte key on the socket.

This is materially different from the website `/api/auth/login` flow. Website authentication and Flash game-server authentication must remain separate compatibility layers.

## Confirmed inventory/equipment flow

`BagAndInfoManager` receives socket packets through `CrazyTankSocketEvent`. It reads binary fields from `PackageIn`, including:

- TemplateID
- Count
- IsBinds
- ValidDate
- StrengthenLevel
- AttackCompose
- DefendCompose
- AgilityCompose
- LuckCompose

The legacy HTTP `LoadUserItems.ashx` and `LoadUserEquip.ashx` endpoints are therefore not the complete game protocol. They are bootstrap/HTTP compatibility endpoints; item/equipment mutations use socket packet types.

## Legacy HTTP compatibility endpoints

The old server exposes XML endpoints including:

- `Request/Login.ashx`
- `Request/GetSID.ashx`
- `Request/LoadUserItems.ashx`
- `Request/LoadUserEquip.ashx`
- `Request/ShopItemList.ashx`
- `Request/QuestList.ashx`
- `Request/ActiveList.ashx`

`LoadUserItems` returns `<Result>` containing `FlashUtils.CreateGoodsInfo(info)` for each player item.

`LoadUserEquip` returns player attributes plus `CreateGoodsInfo()` for equipped items.

`ShopItemList`, `QuestList`, and `ActiveList` use `csFunction.CreateCompressXml()`. The exact compression/encoding must be reproduced from the Flash decoder before claiming wire compatibility.

## Architecture decision

Node backend should therefore expose:

```text
Flash HTTP/XML compatibility
        |
Flash binary socket protocol
        |
        v
Game domain services
  Player
  Inventory
  Equipment
  Shop
  Quest
  Activity
        |
        v
SQLite persistence
```

Vue APIs are separate and must not become the source of truth for in-game actions.
