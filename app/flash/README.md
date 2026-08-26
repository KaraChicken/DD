# Flash Game Gateway

`app/flash` is the server-side boundary for the original Flash client.

## Rules

- The Flash SWF is the game UI/client after login.
- Vue is only the website/login/register/game-shell layer.
- Game actions must be handled by the Node.js game backend, not by Vue.
- The original `Source Flash/` tree is the protocol and client-flow reference.
- Do not invent a JSON game protocol when the Flash client already has a binary socket protocol.
- Keep HTTP/XML endpoints only where the original Flash client actually uses them.

## Current implementation stage

The first target is the Flash login handshake. Until the exact ActionScript packet writer/parser is mapped, protocol bytes must not be guessed.

Target flow:

```text
Vue login
  -> web session
  -> game shell
  -> Flash SWF
  -> Flash TCP login packet
  -> Node game gateway
  -> account/player lookup
  -> game session
  -> player bootstrap
  -> Flash
```

## Protocol mapping

See `PROTOCOL.md`. Every packet added here must cite the corresponding ActionScript implementation and the legacy server handler before it is considered compatible.
