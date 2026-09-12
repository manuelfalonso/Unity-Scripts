using System.Collections.Generic;
using System.Text;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// What a bake produced, and everything it could not produce.
    /// </summary>
    /// <remarks>
    /// A layout that cannot throw a token from one entry into one catch point is an authoring fact, not a
    /// runtime failure. Reporting it here turns it into a line a designer can act on rather than a token that
    /// hangs or lands somewhere it should not.
    /// </remarks>
    public sealed class PlinkoBakeReport
    {
        /// <summary>Signature of the layout and settings this bake ran on.</summary>
        public ulong Signature { get; set; }

        /// <summary>How many drops were simulated in total.</summary>
        public int Simulated { get; set; }

        /// <summary>How many of those reached a catch point.</summary>
        public int Landed { get; set; }

        /// <summary>How many simulated drops were discarded, by reason.</summary>
        public Dictionary<PlinkoDropFailure, int> Failures { get; } =
            new Dictionary<PlinkoDropFailure, int>();

        /// <summary>Entry and catch point pairs no simulated drop ever connected.</summary>
        public List<string> UnreachablePairs { get; } = new List<string>();

        /// <summary>Pairs that kept fewer paths than requested.</summary>
        public List<string> UnderfilledPairs { get; } = new List<string>();

        /// <summary>Pairs that could only be filled by dropping the shared duration band.</summary>
        public List<string> DurationRelaxedPairs { get; } = new List<string>();

        /// <summary>Total paths kept across every pair.</summary>
        public int TotalPaths { get; set; }

        /// <summary>Median duration across every drop that landed, in seconds.</summary>
        public float MedianDuration { get; set; }

        /// <summary>Shortest kept drop, in seconds.</summary>
        public float MinDuration { get; set; }

        /// <summary>Longest kept drop, in seconds.</summary>
        public float MaxDuration { get; set; }

        /// <summary>Most contacts in any kept drop.</summary>
        public int MaxContacts { get; set; }

        /// <summary>Mean contacts across kept drops.</summary>
        public float MeanContacts { get; set; }

        /// <summary>Time the bake took, in milliseconds.</summary>
        public double ElapsedMilliseconds { get; set; }

        /// <summary>A fatal configuration problem, or <c>null</c> when the bake ran.</summary>
        public string Error { get; set; }

        /// <summary>Fraction of simulated drops that reached a catch point.</summary>
        public float LandingRate => Simulated == 0 ? 0f : Landed / (float)Simulated;

        /// <summary>
        /// True when the bake ran, every pair was reached, and every pair kept the paths it asked for.
        /// </summary>
        public bool IsComplete =>
            Error == null && UnreachablePairs.Count == 0 && UnderfilledPairs.Count == 0;

        /// <summary>
        /// Records one discarded drop.
        /// </summary>
        public void RecordFailure(PlinkoDropFailure failure)
        {
            Failures.TryGetValue(failure, out int count);
            Failures[failure] = count + 1;
        }

        /// <summary>
        /// A multi line summary suitable for the Console.
        /// </summary>
        public override string ToString()
        {
            if (Error != null)
            {
                return $"Plinko bake failed: {Error}";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Plinko bake: {TotalPaths} paths kept from {Landed}/{Simulated} landed drops " +
                               $"({LandingRate:P1}), {ElapsedMilliseconds:F0} ms.");
            builder.AppendLine($"Signature: {PlinkoHash.Format(Signature)}");
            builder.AppendLine($"Duration: median {MedianDuration:F2}s, kept range {MinDuration:F2}s to " +
                               $"{MaxDuration:F2}s.");
            builder.AppendLine($"Contacts per drop: mean {MeanContacts:F1}, max {MaxContacts}.");

            foreach (KeyValuePair<PlinkoDropFailure, int> failure in Failures)
            {
                builder.AppendLine($"Discarded [{failure.Key}]: {failure.Value}.");
            }

            if (UnreachablePairs.Count > 0)
            {
                builder.AppendLine($"UNREACHABLE pairs ({UnreachablePairs.Count}): " +
                                   string.Join(", ", UnreachablePairs));
            }

            if (UnderfilledPairs.Count > 0)
            {
                builder.AppendLine($"Underfilled pairs ({UnderfilledPairs.Count}): " +
                                   string.Join(", ", UnderfilledPairs));
            }

            if (DurationRelaxedPairs.Count > 0)
            {
                builder.AppendLine($"Duration band relaxed for ({DurationRelaxedPairs.Count}): " +
                                   string.Join(", ", DurationRelaxedPairs));
            }

            builder.Append(IsComplete ? "Result: complete." : "Result: incomplete, see above.");
            return builder.ToString();
        }
    }
}
