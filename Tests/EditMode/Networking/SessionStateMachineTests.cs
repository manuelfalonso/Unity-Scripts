using System.Collections.Generic;
using NUnit.Framework;
using SombraStudios.Shared.Networking.Sessions;

namespace SombraStudios.Shared.Tests.Networking
{
    public class SessionStateMachineTests
    {
        private SessionStateMachine _stateMachine;

        [SetUp]
        public void SetUp()
        {
            _stateMachine = new SessionStateMachine();
        }

        [Test]
        public void State_StartsOffline()
        {
            Assert.That(_stateMachine.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_stateMachine.IsBusy, Is.False);
            Assert.That(_stateMachine.IsActive, Is.False);
        }

        [Test]
        public void HostFlow_ReachesHosting()
        {
            Assert.That(_stateMachine.TryTransitionTo(SessionState.Creating), Is.True);
            Assert.That(_stateMachine.IsBusy, Is.True);

            Assert.That(_stateMachine.TryTransitionTo(SessionState.Hosting), Is.True);
            Assert.That(_stateMachine.IsActive, Is.True);
            Assert.That(_stateMachine.IsBusy, Is.False);
        }

        [Test]
        public void JoinFlow_ReachesConnected()
        {
            Assert.That(_stateMachine.TryTransitionTo(SessionState.Joining), Is.True);
            Assert.That(_stateMachine.IsBusy, Is.True);

            Assert.That(_stateMachine.TryTransitionTo(SessionState.Connected), Is.True);
            Assert.That(_stateMachine.IsActive, Is.True);
        }

        [Test]
        public void TryTransitionTo_RejectsHostingWhileAlreadyHosting()
        {
            _stateMachine.TryTransitionTo(SessionState.Creating);
            _stateMachine.TryTransitionTo(SessionState.Hosting);

            Assert.That(_stateMachine.TryTransitionTo(SessionState.Creating), Is.False);
            Assert.That(_stateMachine.State, Is.EqualTo(SessionState.Hosting));
        }

        [Test]
        public void TryTransitionTo_RejectsJoiningWhileAlreadyJoining()
        {
            _stateMachine.TryTransitionTo(SessionState.Joining);

            Assert.That(_stateMachine.TryTransitionTo(SessionState.Joining), Is.False);
            Assert.That(_stateMachine.State, Is.EqualTo(SessionState.Joining));
        }

        [Test]
        public void TryTransitionTo_RejectsHostingWithoutCreating()
        {
            Assert.That(_stateMachine.TryTransitionTo(SessionState.Hosting), Is.False);
            Assert.That(_stateMachine.State, Is.EqualTo(SessionState.Offline));
        }

        [Test]
        public void TryTransitionTo_RejectsConnectedWithoutJoining()
        {
            Assert.That(_stateMachine.TryTransitionTo(SessionState.Connected), Is.False);
            Assert.That(_stateMachine.State, Is.EqualTo(SessionState.Offline));
        }

        [Test]
        public void TryTransitionTo_RejectsCrossingFromHostingToConnected()
        {
            _stateMachine.TryTransitionTo(SessionState.Creating);
            _stateMachine.TryTransitionTo(SessionState.Hosting);

            Assert.That(_stateMachine.TryTransitionTo(SessionState.Connected), Is.False);
            Assert.That(_stateMachine.State, Is.EqualTo(SessionState.Hosting));
        }

        [Test]
        public void TryTransitionTo_RejectsTransitionToSameState()
        {
            Assert.That(_stateMachine.TryTransitionTo(SessionState.Offline), Is.False);
        }

        [Test]
        public void CreatingAndJoining_MayAbortBackToOffline()
        {
            _stateMachine.TryTransitionTo(SessionState.Creating);
            Assert.That(_stateMachine.TryTransitionTo(SessionState.Offline), Is.True);

            _stateMachine.TryTransitionTo(SessionState.Joining);
            Assert.That(_stateMachine.TryTransitionTo(SessionState.Offline), Is.True);
        }

        [Test]
        public void Reset_ReturnsToOfflineFromEveryState()
        {
            var reachable = new[]
            {
                SessionState.Creating,
                SessionState.Hosting,
                SessionState.Joining,
                SessionState.Connected,
            };

            foreach (var state in reachable)
            {
                var machine = new SessionStateMachine();
                DriveTo(machine, state);
                Assert.That(machine.State, Is.EqualTo(state), $"Failed to reach {state}.");

                machine.Reset();

                Assert.That(machine.State, Is.EqualTo(SessionState.Offline), $"Reset failed from {state}.");
            }
        }

        [Test]
        public void StateChanged_ReportsPreviousAndNextInOrder()
        {
            var changes = new List<string>();
            _stateMachine.StateChanged += (previous, next) => changes.Add($"{previous}->{next}");

            _stateMachine.TryTransitionTo(SessionState.Creating);
            _stateMachine.TryTransitionTo(SessionState.Hosting);
            _stateMachine.Reset();

            Assert.That(changes, Is.EqualTo(new[]
            {
                "Offline->Creating",
                "Creating->Hosting",
                "Hosting->Offline",
            }));
        }

        [Test]
        public void StateChanged_IsNotRaisedByARejectedTransition()
        {
            var raised = 0;
            _stateMachine.StateChanged += (previous, next) => raised++;

            _stateMachine.TryTransitionTo(SessionState.Hosting);

            Assert.That(raised, Is.Zero);
        }

        [Test]
        public void Reset_IsSilentWhenAlreadyOffline()
        {
            var raised = 0;
            _stateMachine.StateChanged += (previous, next) => raised++;

            _stateMachine.Reset();

            Assert.That(raised, Is.Zero);
        }

        private static void DriveTo(SessionStateMachine machine, SessionState target)
        {
            switch (target)
            {
                case SessionState.Creating:
                    machine.TryTransitionTo(SessionState.Creating);
                    break;
                case SessionState.Hosting:
                    machine.TryTransitionTo(SessionState.Creating);
                    machine.TryTransitionTo(SessionState.Hosting);
                    break;
                case SessionState.Joining:
                    machine.TryTransitionTo(SessionState.Joining);
                    break;
                case SessionState.Connected:
                    machine.TryTransitionTo(SessionState.Joining);
                    machine.TryTransitionTo(SessionState.Connected);
                    break;
            }
        }
    }
}
