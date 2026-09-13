using System;
using System.Collections.Generic;
using NUnit.Framework;
using SombraStudios.Shared.Networking.Sessions;

namespace SombraStudios.Shared.Tests.Networking
{
    public class MultiplayerSessionControllerTests
    {
        private const ulong HostSteamId = 76561197960287930;
        private const ulong ClientSteamId = 76561197960287931;
        private const ulong LobbyId = 109775241234567890;

        private FakeLobbyBackend _backend;
        private FakeSessionDriver _driver;
        private MultiplayerSessionConfig _config;
        private MultiplayerSessionController _controller;
        private List<string> _failures;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeLobbyBackend { LocalUserId = HostSteamId };
            _driver = new FakeSessionDriver();
            _config = new MultiplayerSessionConfig
            {
                GameSignature = "sombra.testgame",
                ProtocolVersion = 3,
            };
            _controller = new MultiplayerSessionController(_backend, _driver, _config);

            _failures = new List<string>();
            _controller.Failed += message => _failures.Add(message);
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        [Test]
        public void Host_RequestsALobbyAndWaitsForSteam()
        {
            Assert.That(_controller.Host(), Is.True);

            Assert.That(_controller.State, Is.EqualTo(SessionState.Creating));
            Assert.That(_backend.CreateLobbyCalls, Is.EqualTo(1));
            Assert.That(_driver.StartHostCalls, Is.Zero, "The server must not start before the lobby exists.");
        }

        [Test]
        public void Host_PublishesTheHostSteamIdIntoLobbyMetadata()
        {
            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            // This is the handshake: without it a client has no address to connect to.
            Assert.That(_backend.PublishedData[LobbyMetadata.HostSteamIdKey], Is.EqualTo(HostSteamId.ToString()));
            Assert.That(_backend.PublishedData[LobbyMetadata.SignatureKey], Is.EqualTo("sombra.testgame"));
            Assert.That(_backend.PublishedData[LobbyMetadata.ProtocolVersionKey], Is.EqualTo("3"));
        }

        [Test]
        public void Host_ReachesHostingOnceTheServerStarts()
        {
            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            Assert.That(_controller.State, Is.EqualTo(SessionState.Hosting));
            Assert.That(_controller.IsActive, Is.True);
            Assert.That(_driver.StartHostCalls, Is.EqualTo(1));
            Assert.That(_failures, Is.Empty);
        }

        [Test]
        public void Host_IgnoresTheLobbyEnteredCallbackForItsOwnLobby()
        {
            // Steam raises LobbyEntered for the owner as well; acting on it would start a
            // client against ourselves.
            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            Assert.That(_driver.StartClientCalls, Is.Zero);
            Assert.That(_controller.State, Is.EqualTo(SessionState.Hosting));
        }

        [Test]
        public void Host_IsRefusedWhileAlreadyHosting()
        {
            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            Assert.That(_controller.Host(), Is.False);
            Assert.That(_backend.CreateLobbyCalls, Is.EqualTo(1));
        }

        [Test]
        public void Host_FailsWhenTheBackendIsUnavailable()
        {
            _backend.IsAvailable = false;

            Assert.That(_controller.Host(), Is.False);
            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_failures, Has.Count.EqualTo(1));
            Assert.That(_backend.CreateLobbyCalls, Is.Zero);
        }

        [Test]
        public void Host_FailsOnAnInvalidConfiguration()
        {
            _config.MaxPlayers = 1;

            Assert.That(_controller.Host(), Is.False);
            Assert.That(_failures, Has.Count.EqualTo(1));
            Assert.That(_failures[0], Does.Contain("MaxPlayers"));
        }

        [Test]
        public void Host_TearsDownWhenTheNetcodeLayerRefusesToStart()
        {
            _driver.StartHostResult = false;

            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_backend.LeaveLobbyCalls, Is.GreaterThanOrEqualTo(1), "A dead session must not keep its lobby.");
            Assert.That(_failures, Has.Count.EqualTo(1));
        }

        [Test]
        public void Join_ConnectsToTheHostIdReadFromLobbyMetadata()
        {
            _backend.LocalUserId = ClientSteamId;
            _backend.SeedHostMetadata(_config, HostSteamId);

            Assert.That(_controller.Join(LobbyId), Is.True);
            Assert.That(_controller.State, Is.EqualTo(SessionState.Joining));
            Assert.That(_backend.LastRequestedLobbyId, Is.EqualTo(LobbyId));

            _backend.CompleteJoin(LobbyId, HostSteamId);

            Assert.That(_driver.StartClientCalls, Is.EqualTo(1));
            Assert.That(_driver.LastHostId, Is.EqualTo(HostSteamId));
        }

        [Test]
        public void Join_StaysJoiningUntilTheHandshakeCompletes()
        {
            _backend.LocalUserId = ClientSteamId;
            _backend.SeedHostMetadata(_config, HostSteamId);
            _controller.Join(LobbyId);
            _backend.CompleteJoin(LobbyId, HostSteamId);

            Assert.That(_controller.State, Is.EqualTo(SessionState.Joining));
            Assert.That(_controller.IsActive, Is.False);

            _driver.RaiseConnected();

            Assert.That(_controller.State, Is.EqualTo(SessionState.Connected));
            Assert.That(_controller.IsActive, Is.True);
            Assert.That(_failures, Is.Empty);
        }

        [Test]
        public void Join_FailsOnALobbyWithNoMetadata()
        {
            // A stranger's lobby on the shared App ID 480.
            _backend.LocalUserId = ClientSteamId;
            _controller.Join(LobbyId);
            _backend.CompleteJoin(LobbyId, HostSteamId);

            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_driver.StartClientCalls, Is.Zero);
            Assert.That(_failures, Has.Count.EqualTo(1));
            Assert.That(_failures[0], Does.Contain("signature"));
        }

        [Test]
        public void Join_FailsOnAProtocolMismatch()
        {
            _backend.LocalUserId = ClientSteamId;
            var newerHost = new MultiplayerSessionConfig
            {
                GameSignature = "sombra.testgame",
                ProtocolVersion = 9,
            };
            _backend.SeedHostMetadata(newerHost, HostSteamId);

            _controller.Join(LobbyId);
            _backend.CompleteJoin(LobbyId, HostSteamId);

            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_driver.StartClientCalls, Is.Zero);
            Assert.That(_failures[0], Does.Contain("protocol"));
        }

        [Test]
        public void Join_IsRefusedForLobbyZero()
        {
            Assert.That(_controller.Join(0), Is.False);
            Assert.That(_backend.JoinLobbyCalls, Is.Zero);
        }

        [Test]
        public void Join_IsRefusedWhileAlreadyJoining()
        {
            _controller.Join(LobbyId);

            Assert.That(_controller.Join(LobbyId), Is.False);
            Assert.That(_backend.JoinLobbyCalls, Is.EqualTo(1));
        }

        [Test]
        public void BackendFailure_DuringCreateReturnsToOffline()
        {
            _controller.Host();
            _backend.RaiseOperationFailed("Steam refused to create the lobby.");

            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_failures, Is.EqualTo(new[] { "Steam refused to create the lobby." }));
        }

        [Test]
        public void Leave_ShutsDownTheDriverAndTheLobby()
        {
            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            _controller.Leave();

            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_driver.ShutdownCalls, Is.EqualTo(1));
            Assert.That(_backend.LeaveLobbyCalls, Is.EqualTo(1));
        }

        [Test]
        public void Leave_DoesNotReportAFailureForADeliberateShutdown()
        {
            // The driver raises Disconnected while shutting down, which must not be mistaken
            // for losing the host.
            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            _controller.Leave();

            Assert.That(_failures, Is.Empty);
        }

        [Test]
        public void Leave_IsSafeWhenAlreadyOffline()
        {
            Assert.DoesNotThrow(() => _controller.Leave());
            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
        }

        [Test]
        public void UnexpectedDisconnect_ReportsAFailureAndReturnsToOffline()
        {
            _backend.LocalUserId = ClientSteamId;
            _backend.SeedHostMetadata(_config, HostSteamId);
            _controller.Join(LobbyId);
            _backend.CompleteJoin(LobbyId, HostSteamId);
            _driver.RaiseConnected();

            _driver.RaiseDisconnected();

            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_failures, Has.Count.EqualTo(1));
            Assert.That(_failures[0], Does.Contain("host"));
        }

        [Test]
        public void HostClosingTheLobby_ReportsAFailureOnTheClient()
        {
            _backend.LocalUserId = ClientSteamId;
            _backend.SeedHostMetadata(_config, HostSteamId);
            _controller.Join(LobbyId);
            _backend.CompleteJoin(LobbyId, HostSteamId);
            _driver.RaiseConnected();

            _backend.RaiseLobbyClosedRemotely();

            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_failures[0], Does.Contain("closed"));
        }

        [Test]
        public void Invite_IsRefusedWhileOffline()
        {
            Assert.That(_controller.Invite(), Is.False);
            Assert.That(_backend.InviteCalls, Is.Zero);
        }

        [Test]
        public void Invite_OpensTheDialogWhileHosting()
        {
            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            Assert.That(_controller.Invite(), Is.True);
            Assert.That(_backend.InviteCalls, Is.EqualTo(1));
        }

        [Test]
        public void JoinRequested_JoinsDirectlyWhenOffline()
        {
            _backend.LocalUserId = ClientSteamId;

            _backend.RaiseJoinRequested(LobbyId);

            Assert.That(_controller.State, Is.EqualTo(SessionState.Joining));
            Assert.That(_backend.LastRequestedLobbyId, Is.EqualTo(LobbyId));
        }

        [Test]
        public void JoinRequested_ReplacesAnActiveSession()
        {
            // Accepting an invite mid-game has to abandon the current lobby first, or the
            // state machine would refuse the join.
            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            _backend.RaiseJoinRequested(999);

            Assert.That(_controller.State, Is.EqualTo(SessionState.Joining));
            Assert.That(_backend.LastRequestedLobbyId, Is.EqualTo(999UL));
            Assert.That(_driver.ShutdownCalls, Is.EqualTo(1));
        }

        [Test]
        public void MemberEvents_AreForwarded()
        {
            var joined = new List<ulong>();
            var left = new List<ulong>();
            _controller.MemberJoined += member => joined.Add(member.SteamId);
            _controller.MemberLeft += member => left.Add(member.SteamId);

            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            var guest = new LobbyMember(ClientSteamId, "Guest");
            _backend.RaiseMemberJoined(guest);
            Assert.That(joined, Is.EqualTo(new[] { ClientSteamId }));
            Assert.That(_controller.Members, Has.Count.EqualTo(2));

            _backend.RaiseMemberLeft(guest);
            Assert.That(left, Is.EqualTo(new[] { ClientSteamId }));
        }

        [Test]
        public void StateChanged_ReportsTheFullHostSequence()
        {
            var transitions = new List<string>();
            _controller.StateChanged += (previous, next) => transitions.Add($"{previous}->{next}");

            _controller.Host();
            _backend.CompleteCreate(LobbyId);
            _controller.Leave();

            Assert.That(transitions, Is.EqualTo(new[]
            {
                "Offline->Creating",
                "Creating->Hosting",
                "Hosting->Offline",
            }));
        }

        [Test]
        public void Dispose_StopsRespondingToBackendCallbacks()
        {
            _controller.Host();
            _controller.Dispose();

            _backend.CompleteCreate(LobbyId);

            Assert.That(_controller.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_driver.StartHostCalls, Is.Zero);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            _controller.Host();
            _backend.CompleteCreate(LobbyId);

            _controller.Dispose();

            Assert.DoesNotThrow(() => _controller.Dispose());
            Assert.That(_driver.ShutdownCalls, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_RejectsMissingCollaborators()
        {
            Assert.Throws<ArgumentNullException>(() => new MultiplayerSessionController(null, _driver));
            Assert.Throws<ArgumentNullException>(() => new MultiplayerSessionController(_backend, null));
        }

        [Test]
        public void Constructor_FallsBackToDefaultConfiguration()
        {
            using (var controller = new MultiplayerSessionController(new FakeLobbyBackend(), new FakeSessionDriver()))
            {
                Assert.That(controller.Config, Is.Not.Null);
                Assert.That(controller.Config.SteamAppId, Is.EqualTo(MultiplayerSessionConfig.SpacewarAppId));
            }
        }
    }
}
