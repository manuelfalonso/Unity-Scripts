using NUnit.Framework;
using SombraStudios.Shared.Networking.Sessions;

namespace SombraStudios.Shared.Tests.Networking
{
    public class LocalLobbyBackendTests
    {
        private MultiplayerSessionConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new MultiplayerSessionConfig
            {
                GameSignature = "sombrastudios.steamtest",
                ProtocolVersion = 7,
            };
        }

        [Test]
        public void JoinLobby_FabricatesMetadataMatchingTheSuppliedConfig()
        {
            // Regression: the backend used to fall back to a default config when the instance had
            // never hosted, so a join-only client published the default signature and then
            // rejected its own lobby as belonging to another game.
            var backend = new LocalLobbyBackend(_config);

            backend.JoinLobby(1000);

            Assert.That(LobbyMetadata.TryParse(backend.GetLobbyData, out var metadata, out var error), Is.True, error);
            Assert.That(metadata.Signature, Is.EqualTo("sombrastudios.steamtest"));
            Assert.That(metadata.ProtocolVersion, Is.EqualTo(7));
            Assert.That(metadata.IsCompatibleWith(_config, out var reason), Is.True, reason);
        }

        [Test]
        public void JoinLobby_WithoutHostingFirst_IsAcceptedByAControllerUsingTheSameConfig()
        {
            // The end-to-end shape of the bug: Join on a fresh instance must reach Connected.
            var backend = new LocalLobbyBackend(_config);
            var driver = new FakeSessionDriver();

            using (var controller = new MultiplayerSessionController(backend, driver, _config))
            {
                string failure = null;
                controller.Failed += message => failure = message;

                controller.Join(1000);
                driver.RaiseConnected();

                Assert.That(failure, Is.Null, failure);
                Assert.That(controller.State, Is.EqualTo(SessionState.Connected));
                Assert.That(driver.StartClientCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public void CreateLobby_ThenJoinOnAnotherInstance_AgreeOnTheSameSignature()
        {
            var host = new LocalLobbyBackend(_config);
            var guest = new LocalLobbyBackend(_config);

            host.CreateLobby(_config);
            guest.JoinLobby(host.CurrentLobbyId);

            Assert.That(guest.GetLobbyData(LobbyMetadata.SignatureKey),
                Is.EqualTo(_config.GameSignature));
        }

        [Test]
        public void JoinLobby_WithNoConfigSupplied_FallsBackToDefaults()
        {
            // Still supported, but only agrees with a session that also runs on defaults.
            var backend = new LocalLobbyBackend();

            backend.JoinLobby(1000);

            Assert.That(backend.GetLobbyData(LobbyMetadata.SignatureKey),
                Is.EqualTo(new MultiplayerSessionConfig().GameSignature));
        }

        [Test]
        public void InviteFriend_FailsAndExplainsWhy()
        {
            var backend = new LocalLobbyBackend(_config);
            string failure = null;
            backend.OperationFailed += message => failure = message;

            backend.CreateLobby(_config);

            Assert.That(backend.InviteFriend(), Is.False);
            Assert.That(failure, Does.Contain("platform backend"));
        }

        [Test]
        public void LeaveLobby_ClearsMetadataAndMembers()
        {
            var backend = new LocalLobbyBackend(_config);
            backend.JoinLobby(1000);

            backend.LeaveLobby();

            Assert.That(backend.IsInLobby, Is.False);
            Assert.That(backend.CurrentLobbyId, Is.Zero);
            Assert.That(backend.Members, Is.Empty);
            Assert.That(backend.GetLobbyData(LobbyMetadata.SignatureKey), Is.Empty);
        }
    }
}
