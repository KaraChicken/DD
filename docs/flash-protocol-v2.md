# Flash protocol V2

## Confirmed from `Source Flash`

The game client uses a raw Flash `Socket`, wrapped by `road7th.comm.ByteSocket`, for the in-game connection. This is separate from the legacy HTTP/XML `Request/*.ashx` endpoints.

`PackageOut` defines a 20-byte header:

| Offset | Size | Field |
|---:|---:|---|
| 0 | 2 | magic/header `29099` |
| 2 | 2 | packet length, including header |
| 4 | 2 | checksum |
| 6 | 2 | opcode |
| 8 | 4 | clientId |
| 12 | 4 | extend1 |
| 16 | 4 | extend2 |
| 20 | N | payload |

All fields use ActionScript `ByteArray`'s default big-endian byte order.

Checksum is:

```text
value = 119
for byte at offsets 6..end:
    value += byte
checksum = value & 32639
```

## Wire encryption

`ByteSocket` encrypts the complete packet before writing it to the Flash `Socket`.

The initial key is:

```text
AE BF 56 78 AB CD EF F1
```

The client changes to a per-login 8-byte key after sending the login packet. The exact RSA handshake that lets the server recover that key must be mapped before enabling authenticated gameplay.

Encryption/decryption is implemented in `apps/api/src/flash/protocol.ts` to match `ByteSocket` / `PackageIn`.

## Confirmed opcodes

The following values come directly from `Source Flash/scripts/ddt/data/socket/ePackageType.as`:

```text
LOGIN              1
SYS_MESSAGE        3
PING               4
RSAKEY             7
DAILY_AWARD       13
SCENE_LOGIN       16
SCENE_ADD_USER    18
SCENE_CHAT        19
SCENE_FACE        20
SCENE_REMOVE_USER 21
DELETE_GOODS      42
BUY_GOODS         44
UPDATE_COUPONS    46
UNCHAIN_EQUIP     47
SELL_GOODS        48
CHANGE_PLACE_GOODS 49
UPDATE_GOODS      51
CHAIN_EQUIP       52
ITEM_COMPOSE      58
ITEM_STRENGTHEN   59
ITEM_TRANSFER     61
ITEM_CONTINUE     62
ITEM_OPENUP       63
ITEM_EQUIP        74
ITEM_STORE        79
GAME_ROOM         94
GAME_CMD          91
QUEST_ADD        176
QUEST_REMOVE     177
QUEST_UPDATE     178
QUEST_FINISH     179
QUEST_OBTAIN     180
QUEST_CHECK      181
```

## Current implementation

`apps/api/src/flash/server.ts` is a raw TCP listener. It is intentionally disabled by default and can be enabled with:

```env
FLASH_SOCKET_ENABLED=true
FLASH_HOST=127.0.0.1
FLASH_PORT=3250
FLASH_SOCKET_ENCRYPTED=true
```

At this stage only `PING` is answered. `LOGIN` is recognized but rejected with a system message because the RSA key exchange and post-login key transition have not yet been mapped completely.

Do not enable this as a production game server yet.

## Next mapping target

1. `RSAKEY` server response and public-key acquisition
2. `LOGIN` payload decryption and account/SID validation
3. Per-connection 8-byte key transition
4. `LOGIN` response payload parsed by `SocketManager`
5. Player initialization
6. Inventory/equipment packets
7. Shop packets
8. Quest packets
9. Activity packets
10. Room/battle packets
