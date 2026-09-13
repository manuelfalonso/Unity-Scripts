# Networking

`SombraStudios.Shared.Networking` — session plumbing for peer-hosted multiplayer,
plus a handful of Netcode for GameObjects samples.

Three assemblies, deliberately split by what each one depends on:

| Assembly | Folder | Gate | Depends on |
|---|---|---|---|
| `SombraStudios.Shared.Networking` | `Sessions/` | none | nothing |
| `SombraStudios.Shared.Networking.Netcode` | `Netcode/` | `NETCODE_GAMEOBJECTS` | NGO |
| `SombraStudios.Shared.Networking.Steam` | `Steam/` | `FACEPUNCH_TRANSPORT` | NGO + Facepunch |

`Sessions/` compiles in a bare Unity project with no packages at all, which is
what makes the whole connection flow unit-testable. The two gated assemblies are
skipped entirely by `defineConstraints` when their package is missing.

## The idea

A Steam **lobby** is a metadata board on Valve's servers; it carries no game
traffic. So connecting is two steps, and this module keeps them apart:

- `ILobbyBackend` — finding other players and agreeing who hosts.
- `INetworkSessionDriver` — actually starting the server or the connection.

`MultiplayerSessionController` is the POCO that ties them together: the host
writes its own Steam ID into lobby metadata, the client reads it back, and only
then does the netcode layer open a connection. That handshake is the part most
tutorials skip.

## Sessions/ — the dependency-free core

| Type | Kind | What it does |
|---|---|---|
| `MultiplayerSessionController` | class | Runs the whole flow; the only thing with logic worth testing |
| `SessionState` / `SessionStateMachine` | enum / class | `Offline → Creating → Hosting`, `Offline → Joining → Connected`, with illegal transitions refused |
| `MultiplayerSessionConfig` | class | Tunables with working defaults; `Validate` catches bad values before Steam does |
| `LobbyMetadata` | struct | The key-value payload, its parser, and the compatibility check |
| `ConnectLobbyArguments` | static class | Parses `+connect_lobby <id>` off the command line |
| `LobbyVisibility` | enum | Private / FriendsOnly / Public / InviteOnly, without a Steamworks reference |
| `LobbyInfo` / `LobbyMember` | structs | Lobby and member snapshots as plain data |
| `ILobbyBackend` / `INetworkSessionDriver` | interfaces | The two seams |
| `LocalLobbyBackend` | class | In-memory lobby for iterating on one machine |

## Steam/ — the Facepunch implementation

| Type | Kind | What it does |
|---|---|---|
| `SteamMultiplayerBootstrap` | MonoBehaviour | Drop-in entry point; owns the controller and pumps Steam |
| `FacepunchLobbyBackend` | class | `ILobbyBackend` over `SteamMatchmaking` |
| `NetcodeSessionDriver` | class | `INetworkSessionDriver` over `NetworkManager`; fills in `targetSteamId` when the transport is `FacepunchTransport` |
| `SteamRuntime` | static class | Initialises Steam once and shuts it down only if it owns it |
| `SteamLobbyDemoGUI` | MonoBehaviour | IMGUI panel: Host / Join / Invite / Leave and a member list |

## Drop-in

Add `NetworkManager` + `FacepunchTransport` + `SteamMultiplayerBootstrap` to one
GameObject, set **Game Signature** to something unique, and:

```csharp
_multiplayer.Host();          // creates a lobby and starts the server
_multiplayer.Invite();        // opens the Steam friend picker
_multiplayer.Join(lobbyId);   // enters a lobby and connects to its host
_multiplayer.Leave();
```

Full setup, including `steam_appid.txt` and the packages to install, is in
[`Docs/manual/SteamworksSetup.md`](../manual/SteamworksSetup.md).

## Netcode/ — samples

`ConnectionApproval` (password-gated approval), `GUILayoutNetwork` (host/client
buttons), `NetworkCommandLine` (launch as host or client from the command line),
`NetworkVariableExample` and `RpcTest`. These are learning samples rather than
systems; `NetworkVariableExample` additionally needs `DOTWEEN`.

## Gotchas

- **Do not call `SteamClient.Init` yourself.** `FacepunchTransport` already does,
  in `Initialize()`. `SteamRuntime` tracks ownership so the second caller does
  not shut Steam down under the first.
- **Steam callbacks stop between sessions.** The transport pumps them only while
  NGO is running, but lobby creation happens before that —
  `SteamMultiplayerBootstrap.Update` covers the gap. A custom bootstrap must too.
- **App ID 480 is shared with every developer testing against Spacewar.** Always
  set `GameSignature`; `LobbyMetadata` rejects lobbies that do not carry yours.
- **One Steam client per machine.** Two Editor instances are one Steam identity,
  so a real two-peer test needs two machines. Use `LocalLobbyBackend` plus a
  local transport to iterate in between.
- **`LocalLobbyBackend` must be constructed with the session's config**
  (`new LocalLobbyBackend(config)`). It fabricates the host metadata a joining
  client then validates, so on defaults it publishes the default signature and
  the client rejects its own lobby.
- **`LocalLobbyBackend.Members` is fabricated** and never reflects real peers —
  no `MemberJoined` / `MemberLeft` fires for them. The connection itself is real;
  read `NetworkManager.ConnectedClientsIds` for who is actually there.
- **The transport is Editor + desktop standalone only,** so the Steam assembly is
  too. Guard your own call sites with `#if FACEPUNCH_TRANSPORT` if they might
  compile for mobile or WebGL.

## Tests

`Tests/EditMode/Networking/` covers the state machine, the metadata round-trip
and its rejection cases, the command-line parser, and the full controller flow
against `FakeLobbyBackend` / `FakeSessionDriver`. None of them need Steam, a
scene, or Play mode.
