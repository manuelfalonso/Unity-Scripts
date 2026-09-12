using NUnit.Framework;
using SombraStudios.Shared.Gameplay.Plinko;
using UnityEngine;

namespace SombraStudios.Shared.Tests.Plinko
{
    /// <summary>
    /// Guards the one thing a player notices immediately: the token must never pass through a peg.
    /// </summary>
    /// <remarks>
    /// Every other test checks where a drop ends. These sample the whole recorded trajectory densely and
    /// measure the closest the token centre ever gets to a peg, which is the only way a tunnelling bug
    /// shows up — it does not change the landing catch point, so nothing else catches it.
    /// </remarks>
    public class PlinkoPenetrationTests
    {
        /// <summary>How far inside a peg the token is allowed to be, in board units.</summary>
        private const float PenetrationTolerance = 0.02f;

        /// <summary>Sampling interval along the drop, in seconds. Must be fine enough to catch a pass-through.</summary>
        private const float SampleStep = 0.0005f;

        [Test]
        public void Simulator_TokenNeverPassesThroughAPegOnTheGrid()
        {
            AssertNoPenetration(
                PlinkoLayoutGenerator.CreateStaggeredGrid(), seed: 987654, drops: 120);
        }

        [Test]
        public void Simulator_TokenNeverPassesThroughAPegOnTheStaircase()
        {
            AssertNoPenetration(
                PlinkoLayoutGenerator.CreateStaircase(), seed: 13579, drops: 200);
        }

        [Test]
        public void Simulator_TokenNeverPassesThroughAPegAtHighRestitution()
        {
            // High restitution means fast, steep, upward bounces, which is where tunnelling appears first.
            var bounce = new PlinkoBounceConfig { Restitution = 0.9f, ScatterDegrees = 30f };
            AssertNoPenetration(
                PlinkoLayoutGenerator.CreateStaggeredGrid(), seed: 246810, drops: 80, bounce: bounce);
        }

        [Test]
        public void Simulator_TokenNeverPassesThroughAPegWithCoarseTracing()
        {
            // A coarse trace must lose precision, not correctness: the tracer has to keep the token outside
            // the pegs even when it is stepping in large jumps.
            var sim = new PlinkoSimConfig { TraceSubsteps = 24 };
            AssertNoPenetration(
                PlinkoLayoutGenerator.CreateStaggeredGrid(), seed: 112233, drops: 60, sim: sim);
        }

        [Test]
        public void Simulator_EveryBounceStartsOnTheSurfaceItBouncedOff()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaggeredGrid();
            var sim = new PlinkoSimConfig();
            var simulator = new PlinkoDropSimulator(layout, sim, new PlinkoBounceConfig());
            var random = new DeterministicRandom(5150);

            float contact = layout.PegRadius + sim.BallRadius;
            int checkedContacts = 0;

            for (int i = 0; i < 60; i++)
            {
                PlinkoPath path = simulator.Simulate(i % layout.EntryCount, random);
                if (path == null)
                {
                    continue;
                }

                for (int c = 0; c < path.Contacts.Count; c++)
                {
                    PlinkoContact hit = path.Contacts[c];
                    if (hit.Kind != PlinkoTraceOutcome.Peg)
                    {
                        continue;
                    }

                    float distance = (hit.Position - layout.Pegs[hit.Index]).magnitude;
                    Assert.AreEqual(
                        contact, distance, 0.01f,
                        $"Contact {c} on peg {hit.Index} was recorded at distance {distance} instead of the " +
                        $"contact radius {contact}, so the bounce did not happen on the surface.");

                    checkedContacts++;
                }
            }

            Assert.Greater(checkedContacts, 0);
        }

        private static void AssertNoPenetration(
            PlinkoLayout layout,
            int seed,
            int drops,
            PlinkoSimConfig sim = null,
            PlinkoBounceConfig bounce = null)
        {
            sim ??= new PlinkoSimConfig();
            bounce ??= new PlinkoBounceConfig();

            var simulator = new PlinkoDropSimulator(layout, sim, bounce);
            var random = new DeterministicRandom(seed);

            float contact = layout.PegRadius + sim.BallRadius;
            float allowed = contact - PenetrationTolerance;
            float worstDepth = 0f;
            string worstDetail = null;
            int simulated = 0;

            for (int i = 0; i < drops; i++)
            {
                PlinkoPath path = simulator.Simulate(i % layout.EntryCount, random);
                if (path == null)
                {
                    continue;
                }

                simulated++;

                int samples = Mathf.Max(2, Mathf.CeilToInt(path.Duration / SampleStep));
                for (int s = 0; s <= samples; s++)
                {
                    float time = path.Duration * s / samples;
                    Vector2 position = path.Evaluate(time);

                    for (int p = 0; p < layout.Pegs.Count; p++)
                    {
                        Vector2 peg = layout.Pegs[p];

                        // Cheap reject before the square root.
                        if (Mathf.Abs(peg.y - position.y) > contact || Mathf.Abs(peg.x - position.x) > contact)
                        {
                            continue;
                        }

                        float distance = (position - peg).magnitude;
                        if (distance >= allowed)
                        {
                            continue;
                        }

                        float depth = allowed - distance;
                        if (depth > worstDepth)
                        {
                            worstDepth = depth;
                            worstDetail =
                                $"drop {i} (entry {path.EntryIndex} -> catch {path.CatchPointIndex}, " +
                                $"{path.Contacts.Count} contacts) was {depth:F4} inside peg {p} at {peg} " +
                                $"at t = {time:F4} of {path.Duration:F3}, centre at {position}";
                        }
                    }
                }
            }

            Assert.Greater(simulated, 0, "No drops were simulated.");
            Assert.AreEqual(
                0f, worstDepth, 0f,
                $"The token passed through a peg. Worst case: {worstDetail}");
        }
    }
}
