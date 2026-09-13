using System;
using System.Collections.Generic;

namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// Guards the legal transitions between <see cref="SessionState"/> values.
    /// </summary>
    /// <remarks>
    /// Steam raises lobby callbacks in an order that is not always the order they were
    /// requested in, so every state change is validated instead of assigned. An illegal
    /// transition is reported rather than thrown, because the caller is usually an
    /// asynchronous callback with nowhere to catch.
    /// </remarks>
    public sealed class SessionStateMachine
    {
        private static readonly Dictionary<SessionState, SessionState[]> AllowedTransitions =
            new Dictionary<SessionState, SessionState[]>
            {
                { SessionState.Offline, new[] { SessionState.Creating, SessionState.Joining } },
                { SessionState.Creating, new[] { SessionState.Hosting, SessionState.Offline } },
                { SessionState.Hosting, new[] { SessionState.Offline } },
                { SessionState.Joining, new[] { SessionState.Connected, SessionState.Offline } },
                { SessionState.Connected, new[] { SessionState.Offline } },
            };

        /// <summary>Raised after a successful transition, with the previous and the new state.</summary>
        public event Action<SessionState, SessionState> StateChanged;

        /// <summary>The current state. Starts at <see cref="SessionState.Offline"/>.</summary>
        public SessionState State { get; private set; } = SessionState.Offline;

        /// <summary>True while a lobby operation is in flight and a second one must be refused.</summary>
        public bool IsBusy => State == SessionState.Creating || State == SessionState.Joining;

        /// <summary>True once the session carries traffic, either as host or as client.</summary>
        public bool IsActive => State == SessionState.Hosting || State == SessionState.Connected;

        /// <summary>
        /// Whether moving to <paramref name="next"/> is legal from the current state.
        /// </summary>
        public bool CanTransitionTo(SessionState next)
        {
            if (!AllowedTransitions.TryGetValue(State, out var allowed))
                return false;

            for (var i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == next)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Moves to <paramref name="next"/> when legal, raising <see cref="StateChanged"/>.
        /// </summary>
        /// <returns>False when the transition is not allowed, leaving the state untouched.</returns>
        public bool TryTransitionTo(SessionState next)
        {
            if (!CanTransitionTo(next))
                return false;

            var previous = State;
            State = next;
            OnStateChanged(previous, next);
            return true;
        }

        /// <summary>
        /// Forces the state back to <see cref="SessionState.Offline"/>, whatever it currently is.
        /// </summary>
        /// <remarks>
        /// Used by teardown and failure paths, which must always succeed — a session that
        /// cannot be abandoned is worse than one that skipped a transition.
        /// </remarks>
        public void Reset()
        {
            if (State == SessionState.Offline)
                return;

            var previous = State;
            State = SessionState.Offline;
            OnStateChanged(previous, SessionState.Offline);
        }

        private void OnStateChanged(SessionState previous, SessionState next)
        {
            StateChanged?.Invoke(previous, next);
        }
    }
}
