# Steam Multiplayer Setup

How to get two players connected over Steam in a fresh project, using
`Networking/Sessions/` and `Networking/Steam/`.

Everything here targets **Facepunch.Steamworks** plus **Netcode for GameObjects**
(NGO). Both are optional packages: without them the Steam assembly compiles
itself out and the rest of the library is unaffected.

---

## 1. How Steam multiplayer actually works

Worth reading once, because the structure explains every step below.

Steamworks is a C++ SDK your game links against; it talks to the Steam client
running on the same machine. Three of its interfaces matter here:

| Layer | Steam interface | What it gives you |
|---|---|---|
| Identity | `ISteamUser` / `ISteamFriends` | Steam ID, persona name, the invite overlay |
| Matchmaking | `ISteamMatchmaking` | **Lobbies** — create, join, metadata, invites |
| Transport | `ISteamNetworkingSockets` | The packets, relayed via Steam Datagram Relay |

**A lobby is not a connection.** It is a shared metadata board hosted on Valve's
servers, holding up to 250 members and a set of key-value pairs. It carries no
game traffic at all. Connecting is therefore two steps:

1. The host creates a lobby and writes **its own Steam ID** into lobby metadata.
2. A client enters the lobby, reads that Steam ID back, and *only then* opens a
   relay connection to it.

`MultiplayerSessionController` implements exactly that handshake, and
`LobbyMetadata` is the payload it writes and reads.

**Steam Datagram Relay (SDR)** carries those packets over Valve's backbone. You
get NAT traversal, no port forwarding, no player IP addresses exposed, and often
better routing than a direct connection — which is the main reason to prefer
Steam over raw UDP for a peer-hosted game.

### Reference documentation

- [Steam Networking overview](https://partner.steamgames.com/doc/features/multiplayer/networking)
- [Steam Datagram Relay](https://partner.steamgames.com/doc/features/multiplayer/steamdatagramrelay)
- [ISteamMatchmaking — lobbies](https://partner.steamgames.com/doc/api/ISteamMatchmaking)
- [ISteamNetworkingSockets](https://partner.steamgames.com/doc/api/ISteamnetworkingSockets)
- [ISteamFriends](https://partner.steamgames.com/doc/api/ISteamFriends)
- [Facepunch.Steamworks wiki](https://wiki.facepunch.com/steamworks/)
- [Facepunch `SteamMatchmaking`](https://wiki.facepunch.com/steamworks/SteamMatchmaking)
- [Facepunch Transport for NGO](https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/main/Transports/com.community.netcode.transport.facepunch)
- [Netcode for GameObjects manual](https://docs-multiplayer.unity3d.com/netcode/current/about/)
- [Community guide: NGO + Facepunch](https://github.com/MrRobinOfficial/Guide-UnitySteamNetcodeGameObjects)

---

## 2. Install the packages

**Netcode for GameObjects** — Package Manager → Unity Registry → *Netcode for
GameObjects*, or add to `Packages/manifest.json`:

```json
"com.unity.netcode.gameobjects": "2.0.0"
```

**Facepunch transport** — Package Manager → **+** → *Add package from git URL*:

```
https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.facepunch
```

This one package ships the `Facepunch.Steamworks` DLLs for Win32/Win64/macOS/
Linux **and** the `steam_api` native libraries. There is no Steamworks SDK
download and no manual DLL drop.

Once both are installed, Unity defines `NETCODE_GAMEOBJECTS` and
`FACEPUNCH_TRANSPORT` automatically through asmdef `versionDefines`. **Do not
add them to Scripting Define Symbols by hand** — they are meant to disappear
when a package is removed.

> **Platform limits.** The transport targets Editor, Windows, macOS and Linux
> standalone only. `SombraStudios.Shared.Networking.Steam` mirrors that list, so
> Android, iOS and WebGL builds simply exclude it rather than failing.

---

## 3. Create `steam_appid.txt`

Put a file named `steam_appid.txt` in the **project root** — next to `Assets/`,
not inside it — containing one line:

```
480
```

Without it the Editor cannot attach to the running Steam client and every call
fails with `Steamworks is not initialized`. Copy the same file next to the
executable in a build, or ship your own App ID instead.

### Is 480 the right App ID? Yes — keep it.

**480 is Spacewar**, Valve's public test app. Any Steam account can run it, no
partner account or payment required. It is the correct and intended App ID for
development, and you should not change it until you are ready to ship. Step 8
covers the switch to your own.

**What you must change instead is `GameSignature`** (step 4). Because 480 is
public, every other developer testing against Spacewar is creating lobbies under
the same App ID — including lobbies your Quick Match or lobby browser could walk
straight into. `GameSignature` is a string stamped into your lobby metadata, and
a client refuses any lobby that does not carry a matching one. Set it to
something unique like `mystudio.mygame` and the collision problem disappears.

---

## 4. Scene setup

1. Add a **NetworkManager** to the scene.
2. Add a **FacepunchTransport** component and assign it as the NetworkManager's
   *Network Transport*.
3. Add **`SteamMultiplayerBootstrap`** to the same GameObject.
4. Add **`SteamLobbyDemoGUI`** alongside it for a zero-setup test panel.
5. Set **Game Signature** to something unique to your project, for example
   `mystudio.mygame`. Leaving the default on App ID 480 means you can end up
   joining a stranger's lobby.

Inspector fields on `SteamMultiplayerBootstrap`:

| Field | Meaning |
|---|---|
| Network Manager | Falls back to this GameObject, then to the singleton |
| Use Local Backend | Swap Steam for the in-memory lobby (see step 6) |
| Steam App Id | `480` while developing |
| Max Players | Lobby capacity including the host |
| Visibility | `FriendsOnly` by default; `Public` to be searchable |
| Game Signature | **Change this.** Keeps your lobbies apart from everyone else's on App ID 480 |
| Protocol Version | Version stamp for your netcode. See below |
| Join From Command Line | Handles `+connect_lobby` on launch |

### About Protocol Version

An integer written into the lobby metadata. A client compares the lobby's number
against its own and refuses to join when they differ, reporting
`Lobby runs protocol X, this build runs Y`.

Its job is to stop an **old build from joining a new one** after you change
anything about how the two sides talk: an RPC signature, a `NetworkVariable`
type, the order of a serialized payload. Those mismatches otherwise show up as
corrupted state or a silent desync, which is far harder to diagnose than a
refused join.

Leave it at `1` while developing. Increment it when you ship an update whose
netcode is incompatible with the previous release. If you only change gameplay
logic that both sides agree on, leave it alone.

---

## 5. Use it from code

```csharp
using SombraStudios.Shared.Networking.Sessions;
using SombraStudios.Shared.Networking.Steam;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private SteamMultiplayerBootstrap _multiplayer;

    private void OnEnable()
    {
        _multiplayer.StateChanged += HandleStateChanged;
        _multiplayer.Failed += message => Debug.LogWarning(message);
    }

    private void OnDisable()
    {
        _multiplayer.StateChanged -= HandleStateChanged;
    }

    public void OnHostPressed() => _multiplayer.Host();

    public void OnInvitePressed() => _multiplayer.Invite();

    private void HandleStateChanged(SessionState previous, SessionState next)
    {
        if (next == SessionState.Hosting || next == SessionState.Connected)
            Debug.Log($"In game as {next}. Lobby {_multiplayer.Controller.CurrentLobbyId}");
    }
}
```

The state sequence is `Offline → Creating → Hosting` for a host and
`Offline → Joining → Connected` for a client. `Failed` fires with a
human-readable reason whenever a session drops back to `Offline` unexpectedly.

Nothing needs to poll: joining through an invite arrives on its own, either as a
Steam callback (game already running) or as `+connect_lobby` on the command line
(game launched by the invite). `SteamMultiplayerBootstrap` handles both.

---

## 6. Testing with one machine

**Steam allows one signed-in client per machine**, so two real Steam peers need
two machines. Two Editor instances on one PC share a single Steam client and
therefore a single Steam ID — they cannot be two distinct peers, no matter how
many windows you open.

So for day-to-day iteration you swap out *both* Steam pieces for local ones.

### The three pieces

| Piece | What it is | How to switch to it |
|---|---|---|
| **Unity Transport** | NGO's built-in transport. Moves bytes over plain UDP to an IP address instead of through Steam's relay. `FacepunchTransport` is the Steam alternative; only one can be assigned at a time. | NetworkManager → **Add Component** → *Unity Transport*, then set it as the **Network Transport**. Leave `127.0.0.1` / `7777`. |
| **`LocalLobbyBackend`** | Stands in for Steam matchmaking, answering Host and Join out of memory, so the session code runs the same path it will against Steam. | Tick **Use Local Backend** on `SteamMultiplayerBootstrap`. |
| **Multiplayer Play Mode** | Unity package (`com.unity.multiplayer.playmode`) that runs up to four *virtual players* from one Editor, so a second player needs no build. | **Window → Multiplayer → Multiplayer Play Mode**, tick the players you want. |

### Putting it together

1. Assign **Unity Transport** to the NetworkManager (not FacepunchTransport).
2. Tick **Use Local Backend** on `SteamMultiplayerBootstrap`.
3. Enable one or two virtual players in Multiplayer Play Mode.
4. Press Play. Host in one window; in another, press Join. **Any** Lobby ID
   works locally — see below.

### What local mode does and does not prove

**Real:** the connection. NGO genuinely starts a server and connects clients over
UDP, so RPCs, `NetworkVariable`s, spawning and disconnects behave as they will in
production.

**Fake:** the lobby. Each instance has its own private in-memory one, shared with
nobody. Specifically:

- **Lobby members** is a fabricated fixed pair. It never updates when someone
  joins, and no "player left" event fires. The panel labels it *(simulated)*.
  For who is really connected, read the **Netcode:** line
  (`NetworkManager.ConnectedClientsIds`).
- **The Lobby ID is cosmetic.** Any number joins the same session: there is no
  lobby directory to look one up in, and Unity Transport connects to its own
  configured `127.0.0.1:7777` regardless. `1000` is just the placeholder
  `LocalLobbyBackend` reports. Against Steam the ID is real and a wrong one fails.
- **Invite a friend** always fails — there is no Steam overlay to open.
- The metadata handshake is untested: the joiner fabricates the metadata it then
  validates.

Lobby membership, invites and SDR routing are real only against Steam, on two
machines.

### The two-machine check

1. Assign **FacepunchTransport** and untick **Use Local Backend**.
2. Build a standalone player and run it on machine B, with Steam running and
   signed into a second account.
3. Run the Editor on machine A.
4. Host on A, press **Invite a friend**, and accept on B — or copy A's lobby ID
   from the demo panel and paste it into B's Join field.
5. Expect A at `Hosting`, B at `Connected`, both appearing in each other's
   member list, and the member list updating when either one leaves.

---

## 7. Troubleshooting

| Symptom | Cause |
|---|---|
| `Steamworks is not initialized` | No `steam_appid.txt` in the project root, or the Steam client is not running |
| Host button does nothing, no error | Steam callbacks are not being pumped. `SteamMultiplayerBootstrap.Update` does this while no session is running — make sure the component is enabled |
| Client hangs at `Joining` | The host's metadata never arrived, or `FacepunchTransport.targetSteamId` was not set. Check the `sombra.host` key on the lobby |
| Joined a lobby full of strangers | Default `GameSignature` on App ID 480. Change it |
| `Lobby belongs to 'X', not 'Y'` in local mode | The backend was built without your config. Fixed — make sure `SteamMultiplayerBootstrap` creates `new LocalLobbyBackend(config)` |
| Member list never updates in local mode | Expected. `LocalLobbyBackend` fabricates it; read the `Netcode:` line or `NetworkManager.ConnectedClientsIds` instead (step 6) |
| `Invites need a platform backend` | Expected in local mode. Invites need Steam |
| `Lobby runs protocol X, this build runs Y` | Mismatched builds. Rebuild both sides |
| Works in Editor, fails in build | `steam_appid.txt` must sit next to the executable too |
| Android/iOS/WebGL build errors | Expected — the Steam assembly excludes those platforms. Guard your own call sites with `#if FACEPUNCH_TRANSPORT` |
| Ping UI always reads 0 | `FacepunchTransport.GetCurrentRtt` returns 0; it is not implemented upstream |

### Two mistakes worth calling out

**Do not call `SteamClient.Init` yourself.** `FacepunchTransport` calls it in
`Initialize()` and `SteamClient.Shutdown()` in `Shutdown()`. `SteamRuntime`
tracks ownership so only whoever initialised Steam shuts it down; a second
`Init` or an early `Shutdown` silently kills callbacks.

**Steam callbacks stop between sessions.** The transport pumps
`SteamClient.RunCallbacks()` only while NGO is running, and lobby creation
happens *before* that. `SteamMultiplayerBootstrap.Update` covers the gap. If you
write your own bootstrap, you must do the same or `Host()` will never get an
answer.

---

## 8. Shipping with your own App ID

Only do this when you are ready to publish. `480` stays correct for the whole
development period.

1. **Get an App ID.** Register the game on the
   [Steamworks partner site](https://partner.steamgames.com/). This needs a
   Steamworks partner account and a one-time fee per app, paid to Valve. Once the
   app exists, Steam assigns it a numeric App ID — a number like `1234560`,
   shown in your app's admin page and in its store URL.
2. **Replace `480` with that number** in two places, both of which must match:
   - `steam_appid.txt` in the project root — change the single line `480` to
     your number.
   - The **Steam App Id** field on `SteamMultiplayerBootstrap` in the Inspector.

   If the two disagree, Steam initialises against one ID while your lobbies are
   created under the other, and joins fail with no useful error.
3. **Set a real `GameSignature`** — for example `mystudio.mygame` — and leave
   `ProtocolVersion` at `1` for your first release.
4. **Delete `steam_appid.txt` from the shipped build.** A game launched through
   Steam gets its App ID from the client automatically, and a stray
   `steam_appid.txt` overrides it — which lets people run the game outside Steam
   and breaks ownership checks. Keep the file for local testing only, and exclude
   it from your release build.
