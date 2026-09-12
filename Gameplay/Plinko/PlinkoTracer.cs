using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// What ended a traced arc.
    /// </summary>
    public enum PlinkoTraceOutcome
    {
        /// <summary>The arc reached a peg.</summary>
        Peg = 0,

        /// <summary>The arc reached a wall or side shape.</summary>
        Wall = 1,

        /// <summary>The arc fell into a catch point.</summary>
        CatchPoint = 2,

        /// <summary>The arc left the play area, which voids the drop.</summary>
        LeftPlayArea = 3,

        /// <summary>The arc ran for the whole time budget without reaching anything.</summary>
        Timeout = 4,
    }

    /// <summary>
    /// The first thing a traced arc reached.
    /// </summary>
    public readonly struct PlinkoTraceResult
    {
        /// <summary>What ended the arc.</summary>
        public readonly PlinkoTraceOutcome Outcome;

        /// <summary>Index of the peg, wall or catch point reached, or -1.</summary>
        public readonly int Index;

        /// <summary>Seconds from launch to the contact.</summary>
        public readonly float Time;

        /// <summary>Ball centre at the contact.</summary>
        public readonly Vector2 Position;

        /// <summary>Unit surface normal at the contact, pointing towards the ball. Zero when not a contact.</summary>
        public readonly Vector2 Normal;

        internal PlinkoTraceResult(
            PlinkoTraceOutcome outcome, int index, float time, Vector2 position, Vector2 normal)
        {
            Outcome = outcome;
            Index = index;
            Time = time;
            Position = position;
            Normal = normal;
        }
    }

    /// <summary>
    /// Marches a ballistic arc forward through a layout and reports the first peg, wall or catch point it
    /// reaches.
    /// </summary>
    /// <remarks>
    /// The arc itself is exact, <c>p(t) = p0 + v0*t + 0.5*g*t^2</c>; only the search for the first contact is
    /// stepped. Two rules keep that search from either missing a collider or sticking to one:
    /// <list type="number">
    /// <item><description><b>Step size follows the motion, not the clock.</b> Each step is sized from the
    /// ball's current speed so it advances at most a fraction of a contact radius, however fast it is going.
    /// A fixed number of steps across a fixed time budget cannot do this: sized for a slow arc it lets a fast
    /// token near the bottom of a tall board leap clean over a peg, and sized for the fast case it spends
    /// that effort on every slow arc too.</description></item>
    /// <item><description><b>A contact only counts while approaching.</b> A candidate is rejected when the
    /// ball is already moving away from that surface, which is exactly the case for the peg it has just
    /// bounced off. Skipping that peg for the whole arc instead — the previous approach — let the ball arc
    /// back down and pass straight through it.</description></item>
    /// </list>
    /// Everything is derived from the arc and from config, never from framerate, so a trace reproduces
    /// exactly. The inner loop deliberately avoids delegates and square roots: bracketing compares squared
    /// distances, and only the final bisection works in real distance.
    /// </remarks>
    public static class PlinkoTracer
    {
        /// <summary>Fraction of a contact radius the ball may advance in one search step.</summary>
        private const float StepFraction = 0.4f;

        /// <summary>Upper bound on search steps for one arc, so a pathological arc cannot stall a bake.</summary>
        private const int MaxSteps = 8192;

        /// <summary>Arc samples taken inside one step when bracketing a contact.</summary>
        private const int BracketSamples = 2;

        /// <summary>
        /// Traces one arc and returns what it reached first.
        /// </summary>
        /// <param name="layout">Board to travel through, as a flat snapshot.</param>
        /// <param name="sim">Ball radius, gravity and trace resolution.</param>
        /// <param name="origin">Board space launch position of the ball centre.</param>
        /// <param name="velocity">Board space launch velocity.</param>
        public static PlinkoTraceResult Trace(
            PlinkoLayoutSnapshot layout, PlinkoSimConfig sim, Vector2 origin, Vector2 velocity)
        {
            Vector2 gravity = sim.GravityVector;
            float ballRadius = sim.BallRadius;
            float pegContact = layout.PegRadius + ballRadius;
            float gravityMagnitude = gravity.magnitude;
            int refineIterations = sim.RefineIterations;
            float traceMaxTime = sim.TraceMaxTime;

            Vector2[] pegs = layout.Pegs;
            PlinkoWall[] walls = layout.Walls;
            PlinkoCatchPoint[] catchPoints = layout.CatchPoints;
            Rect playArea = layout.PlayArea;

            float allowedTravel = Mathf.Max(Mathf.Min(pegContact, ballRadius) * StepFraction, 1e-4f);
            float maxStepTime = traceMaxTime / Mathf.Max(sim.TraceSubsteps, 1);
            float minStepTime = traceMaxTime / MaxSteps;

            Vector2 previous = origin;
            float previousTime = 0f;

            while (previousTime < traceMaxTime)
            {
                float stepTime = ResolveStepTime(
                    velocity, gravity, gravityMagnitude, previousTime, allowedTravel,
                    minStepTime, maxStepTime);

                float time = Mathf.Min(previousTime + stepTime, traceMaxTime);
                Vector2 current = Evaluate(origin, velocity, gravity, time);

                float bestTime = float.MaxValue;
                int bestIndex = -1;
                var bestOutcome = PlinkoTraceOutcome.Timeout;
                Vector2 bestNormal = Vector2.zero;

                float boxMinX = Mathf.Min(previous.x, current.x) - pegContact;
                float boxMaxX = Mathf.Max(previous.x, current.x) + pegContact;
                float boxMinY = Mathf.Min(previous.y, current.y) - pegContact;
                float boxMaxY = Mathf.Max(previous.y, current.y) + pegContact;

                float pegContactSquared = pegContact * pegContact;
                float ballRadiusSquared = ballRadius * ballRadius;

                for (int i = 0; i < pegs.Length; i++)
                {
                    Vector2 peg = pegs[i];
                    if (peg.x < boxMinX || peg.x > boxMaxX || peg.y < boxMinY || peg.y > boxMaxY)
                    {
                        continue;
                    }

                    if (!TryBracketCircle(
                            origin, velocity, gravity, peg, pegContactSquared, previousTime, time,
                            out float outsideTime, out float insideTime))
                    {
                        continue;
                    }

                    float hitTime = BisectCircle(
                        origin, velocity, gravity, peg, pegContactSquared,
                        outsideTime, insideTime, refineIterations);

                    if (hitTime >= bestTime)
                    {
                        continue;
                    }

                    Vector2 at = Evaluate(origin, velocity, gravity, hitTime);
                    Vector2 offset = at - peg;
                    Vector2 normal = offset.sqrMagnitude > 1e-12f ? offset.normalized : Vector2.up;

                    if (!IsApproaching(velocity, gravity, hitTime, normal))
                    {
                        continue;
                    }

                    bestTime = hitTime;
                    bestIndex = i;
                    bestOutcome = PlinkoTraceOutcome.Peg;
                    bestNormal = normal;
                }

                for (int i = 0; i < walls.Length; i++)
                {
                    PlinkoWall wall = walls[i];
                    if (Mathf.Max(wall.From.x, wall.To.x) < boxMinX ||
                        Mathf.Min(wall.From.x, wall.To.x) > boxMaxX ||
                        Mathf.Max(wall.From.y, wall.To.y) < boxMinY ||
                        Mathf.Min(wall.From.y, wall.To.y) > boxMaxY)
                    {
                        continue;
                    }

                    if (!TryBracketWall(
                            origin, velocity, gravity, wall, ballRadiusSquared, previousTime, time,
                            out float outsideTime, out float insideTime))
                    {
                        continue;
                    }

                    float hitTime = BisectWall(
                        origin, velocity, gravity, wall, ballRadiusSquared,
                        outsideTime, insideTime, refineIterations);

                    if (hitTime >= bestTime)
                    {
                        continue;
                    }

                    Vector2 normal = WallNormalTowards(
                        wall, Evaluate(origin, velocity, gravity, hitTime));

                    if (!IsApproaching(velocity, gravity, hitTime, normal))
                    {
                        continue;
                    }

                    bestTime = hitTime;
                    bestIndex = i;
                    bestOutcome = PlinkoTraceOutcome.Wall;
                    bestNormal = normal;
                }

                for (int i = 0; i < catchPoints.Length; i++)
                {
                    PlinkoCatchPoint catchPoint = catchPoints[i];
                    if (previous.y <= catchPoint.Y || current.y > catchPoint.Y)
                    {
                        continue;
                    }

                    float crossTime = RefineHeight(
                        origin, velocity, gravity, catchPoint.Y, previousTime, time, refineIterations);

                    if (crossTime >= bestTime)
                    {
                        continue;
                    }

                    if (catchPoint.ContainsX(Evaluate(origin, velocity, gravity, crossTime).x))
                    {
                        bestTime = crossTime;
                        bestIndex = i;
                        bestOutcome = PlinkoTraceOutcome.CatchPoint;
                        bestNormal = Vector2.zero;
                    }
                }

                if (bestIndex >= 0)
                {
                    return new PlinkoTraceResult(
                        bestOutcome, bestIndex, bestTime,
                        Evaluate(origin, velocity, gravity, bestTime), bestNormal);
                }

                if (!playArea.Contains(current))
                {
                    return new PlinkoTraceResult(
                        PlinkoTraceOutcome.LeftPlayArea, -1, time, current, Vector2.zero);
                }

                previous = current;
                previousTime = time;
            }

            return new PlinkoTraceResult(
                PlinkoTraceOutcome.Timeout, -1, traceMaxTime, previous, Vector2.zero);
        }

        /// <summary>
        /// Position of a ballistic arc at <paramref name="time"/>.
        /// </summary>
        public static Vector2 Evaluate(Vector2 origin, Vector2 velocity, Vector2 gravity, float time)
        {
            return origin + velocity * time + 0.5f * gravity * (time * time);
        }

        /// <summary>
        /// Velocity of a ballistic arc at <paramref name="time"/>.
        /// </summary>
        public static Vector2 EvaluateVelocity(Vector2 velocity, Vector2 gravity, float time)
        {
            return velocity + gravity * time;
        }

        /// <summary>
        /// Chooses how long the next search step may last so the ball advances at most
        /// <see cref="StepFraction"/> of a contact radius.
        /// </summary>
        /// <remarks>
        /// Sized from the speed at the start of the step plus what gravity will add during it, so the bound
        /// holds across the whole step.
        /// </remarks>
        private static float ResolveStepTime(
            Vector2 velocity, Vector2 gravity, float gravityMagnitude, float time,
            float allowedTravel, float minStepTime, float maxStepTime)
        {
            float speed = EvaluateVelocity(velocity, gravity, time).magnitude;
            float guess = allowedTravel / Mathf.Max(speed, 1e-3f);
            float speedBound = speed + gravityMagnitude * Mathf.Min(guess, maxStepTime);

            return Mathf.Clamp(allowedTravel / Mathf.Max(speedBound, 1e-3f), minStepTime, maxStepTime);
        }

        /// <summary>
        /// Whether the ball is still moving towards a surface with the given normal at
        /// <paramref name="time"/>.
        /// </summary>
        /// <remarks>
        /// This is what lets a bounce start exactly on the surface it just left without immediately
        /// re-colliding, while keeping that same surface solid if the arc curves back onto it later.
        /// </remarks>
        private static bool IsApproaching(Vector2 velocity, Vector2 gravity, float time, Vector2 normal)
        {
            return Vector2.Dot(EvaluateVelocity(velocity, gravity, time), normal) < 0f;
        }

        private static bool TryBracketCircle(
            Vector2 origin, Vector2 velocity, Vector2 gravity, Vector2 center, float radiusSquared,
            float startTime, float endTime, out float outsideTime, out float insideTime)
        {
            outsideTime = startTime;
            insideTime = endTime;

            float previousDistance = CircleGap(origin, velocity, gravity, center, radiusSquared, startTime);
            float previousTime = startTime;

            for (int i = 1; i <= BracketSamples; i++)
            {
                float time = Mathf.Lerp(startTime, endTime, i / (float)BracketSamples);
                float current = CircleGap(origin, velocity, gravity, center, radiusSquared, time);

                if (current <= 0f)
                {
                    // Only an entry counts. Starting inside and heading out is handled by the approach test.
                    outsideTime = previousDistance > 0f ? previousTime : startTime;
                    insideTime = time;
                    return true;
                }

                previousDistance = current;
                previousTime = time;
            }

            return false;
        }

        private static bool TryBracketWall(
            Vector2 origin, Vector2 velocity, Vector2 gravity, PlinkoWall wall, float radiusSquared,
            float startTime, float endTime, out float outsideTime, out float insideTime)
        {
            outsideTime = startTime;
            insideTime = endTime;

            float previousDistance = WallGap(origin, velocity, gravity, wall, radiusSquared, startTime);
            float previousTime = startTime;

            for (int i = 1; i <= BracketSamples; i++)
            {
                float time = Mathf.Lerp(startTime, endTime, i / (float)BracketSamples);
                float current = WallGap(origin, velocity, gravity, wall, radiusSquared, time);

                if (current <= 0f)
                {
                    outsideTime = previousDistance > 0f ? previousTime : startTime;
                    insideTime = time;
                    return true;
                }

                previousDistance = current;
                previousTime = time;
            }

            return false;
        }

        private static float BisectCircle(
            Vector2 origin, Vector2 velocity, Vector2 gravity, Vector2 center, float radiusSquared,
            float outsideTime, float insideTime, int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                float middle = (outsideTime + insideTime) * 0.5f;
                if (CircleGap(origin, velocity, gravity, center, radiusSquared, middle) > 0f)
                {
                    outsideTime = middle;
                }
                else
                {
                    insideTime = middle;
                }
            }

            return insideTime;
        }

        private static float BisectWall(
            Vector2 origin, Vector2 velocity, Vector2 gravity, PlinkoWall wall, float radiusSquared,
            float outsideTime, float insideTime, int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                float middle = (outsideTime + insideTime) * 0.5f;
                if (WallGap(origin, velocity, gravity, wall, radiusSquared, middle) > 0f)
                {
                    outsideTime = middle;
                }
                else
                {
                    insideTime = middle;
                }
            }

            return insideTime;
        }

        /// <summary>
        /// Squared distance to a circle's surface, signed. Zero crossings match the real distance's, so this
        /// can drive both bracketing and bisection without a square root.
        /// </summary>
        private static float CircleGap(
            Vector2 origin, Vector2 velocity, Vector2 gravity, Vector2 center, float radiusSquared, float time)
        {
            return (Evaluate(origin, velocity, gravity, time) - center).sqrMagnitude - radiusSquared;
        }

        private static float WallGap(
            Vector2 origin, Vector2 velocity, Vector2 gravity, PlinkoWall wall, float radiusSquared, float time)
        {
            Vector2 point = Evaluate(origin, velocity, gravity, time);
            return (point - ClosestPointOnSegment(wall, point)).sqrMagnitude - radiusSquared;
        }

        private static Vector2 ClosestPointOnSegment(PlinkoWall wall, Vector2 point)
        {
            Vector2 delta = wall.Delta;
            float lengthSquared = delta.sqrMagnitude;

            if (lengthSquared <= Mathf.Epsilon)
            {
                return wall.From;
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - wall.From, delta) / lengthSquared);
            return wall.From + delta * t;
        }

        private static Vector2 WallNormalTowards(PlinkoWall wall, Vector2 point)
        {
            Vector2 offset = point - ClosestPointOnSegment(wall, point);
            if (offset.sqrMagnitude > 1e-10f)
            {
                return offset.normalized;
            }

            // Dead centre on the segment: fall back to its perpendicular.
            Vector2 delta = wall.Delta;
            return new Vector2(-delta.y, delta.x).normalized;
        }

        private static float RefineHeight(
            Vector2 origin, Vector2 velocity, Vector2 gravity, float height,
            float aboveTime, float belowTime, int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                float middle = (aboveTime + belowTime) * 0.5f;
                if (Evaluate(origin, velocity, gravity, middle).y > height)
                {
                    aboveTime = middle;
                }
                else
                {
                    belowTime = middle;
                }
            }

            return belowTime;
        }
    }
}
