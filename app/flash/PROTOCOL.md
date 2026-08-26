# Flash protocol map

`Source Flash/` is the primary client-flow and protocol reference. Legacy `Request/` and server code are the server-side compatibility reference.

## Architecture

```text
Vue: home / login / register / game shell
                    |
                    v
              Flash / SWF
                    |
       HTTP/XML or TCP binary protocol
                    |
                    v
              Node.js gateway
                    |
              domain services
                    |
                  SQLite
```

## Implementation order

1. Login / game-session handshake
2. Player bootstrap
3. Inventory
4. Equipment
5. Shop
6. Quest
7. Activity
8. Room
9. Battle

## Evidence rule

Every Flash packet must be mapped from the actual ActionScript writer and reader before implementation. Record:

- ActionScript class and method
- opcode/package type
- field order and width
- encoding/endian rules
- encryption/checksum rules
- response reader
- legacy server handler/service
- resulting Flash state change

Unknown fields are `UNVERIFIED`; do not guess them.

## Login

Status: `IN PROGRESS`.

The website session and the Flash game session are separate. Website login establishes the web session; the SWF then performs its own game-server handshake and receives the game session/player bootstrap required by the original client.

## HTTP/XML compatibility

Legacy `Request/*.ashx` endpoints are retained only when the Flash source confirms that the client calls them and their XML shape can be reproduced. They are not a replacement for the Flash TCP game protocol.
