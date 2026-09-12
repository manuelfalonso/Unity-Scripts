using System;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// Which built in layout a <see cref="PlinkoBoard"/> should build when no layout asset is supplied.
    /// </summary>
    public enum PlinkoLayoutPreset
    {
        /// <summary>The classic horizontally symmetric staggered grid.</summary>
        StaggeredGrid = 0,

        /// <summary>A sparse test board: one peg per entry, each at a different height.</summary>
        Staircase = 1,

        /// <summary>Use the layout supplied by the config asset and build nothing.</summary>
        FromAsset = 2,
    }

    /// <summary>
    /// Scene facing adapter for a deterministic Plinko board. Bakes the drop simulation on Awake and hands
    /// out recorded drops for a requested catch point.
    /// </summary>
    /// <remarks>
    /// A thin adapter by design: the simulation lives in <see cref="PlinkoSolver"/> and the plain objects
    /// beneath it, so the whole system runs in Edit Mode tests with no scene. The board is authored in a 2D
    /// plane and projected through this transform, so the same component serves a 2D game and a 3D one.
    /// </remarks>
    public class PlinkoBoard : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Which built in layout to build. Choose From Asset to use a layout authored in the config " +
                 "asset instead.")]
        [SerializeField] private PlinkoLayoutPreset _preset = PlinkoLayoutPreset.StaggeredGrid;

        [Tooltip("Optional asset holding the layout and settings. Required when the preset is From Asset; " +
                 "otherwise its settings are still used and only its layout is ignored.")]
        [SerializeField] private PlinkoBoardConfigSO _configAsset;

        [Tooltip("Optional pre-baked paths. When assigned and its signature matches this board, the drops " +
                 "are loaded instead of simulated, which is what a shipped build should do. Bake one from a " +
                 "Plinko Board Config asset's inspector.")]
        [SerializeField] private PlinkoPathSetSO _pathSetAsset;

        [Header("Staggered Grid Preset")]
        [Tooltip("Peg rows in the generated grid.")]
        [Min(1)]
        [SerializeField] private int _rows = 12;

        [Tooltip("Pegs per row in the generated grid.")]
        [Min(2)]
        [SerializeField] private int _columns = 9;

        [Header("Both Presets")]
        [Tooltip("Number of drop points across the top.")]
        [Min(1)]
        [SerializeField] private int _entryCount = 5;

        [Tooltip("Number of catch points along the bottom.")]
        [Min(1)]
        [SerializeField] private int _catchPointCount = 9;

        [Header("Simulation")]
        [Tooltip("Ball size, gravity and the budget a single drop is allowed.")]
        [SerializeField] private PlinkoSimConfig _simConfig = new PlinkoSimConfig();

        [Tooltip("How pegs and walls throw the token back, and how much bounces scatter.")]
        [SerializeField] private PlinkoBounceConfig _bounceConfig = new PlinkoBounceConfig();

        [Tooltip("How many drops to simulate and how many to keep per entry and catch point pair.")]
        [SerializeField] private PlinkoGenerationConfig _generationConfig = new PlinkoGenerationConfig();

        [Header("Lifecycle")]
        [Tooltip("Bake during Awake. Turn off to bake manually, for example after loading a layout.")]
        [SerializeField] private bool _bakeOnAwake = true;

        [Tooltip("Write the bake report to the Console. Leave on while authoring: it is where unreachable " +
                 "catch points and underfilled pairs are reported.")]
        [SerializeField] private bool _logReport = true;

        [Header("Gizmos")]
        [Tooltip("Draw pegs, entries, catch points, walls and the play area in the Scene view.")]
        [SerializeField] private bool _drawGizmos = true;

        private DeterministicRandom _drawRandom;

        /// <summary>Raised after a bake completes, successful or not.</summary>
        public event Action<PlinkoBakeReport> Baked;

        /// <summary>The layout that was simulated, or <c>null</c> before the first bake.</summary>
        public PlinkoLayout Layout { get; private set; }

        /// <summary>The baked drops, or <c>null</c> before the first successful bake.</summary>
        public PlinkoPathSet PathSet { get; private set; }

        /// <summary>The most recent bake report, or <c>null</c> before the first bake.</summary>
        public PlinkoBakeReport Report { get; private set; }

        /// <summary>True when the board has a usable path set.</summary>
        public bool IsBaked => Layout != null && PathSet != null;

        /// <summary>Number of entries on the board.</summary>
        public int EntryCount => Layout?.EntryCount ?? 0;

        /// <summary>Number of catch points on the board.</summary>
        public int CatchPointCount => Layout?.CatchPointCount ?? 0;

        /// <summary>Ball settings in use, so a player can size the token.</summary>
        public PlinkoSimConfig SimConfig => _configAsset != null ? _configAsset.Sim : _simConfig;

        private void Awake()
        {
            if (_bakeOnAwake)
            {
                Bake();
            }
        }

        /// <summary>
        /// Builds the layout, runs the drop simulation and stores the resulting paths. Safe to call again
        /// after changing a setting.
        /// </summary>
        public void Bake()
        {
            PlinkoLayout layout = BuildLayout();
            PlinkoSimConfig sim = _configAsset != null ? _configAsset.Sim : _simConfig;
            PlinkoBounceConfig bounce = _configAsset != null ? _configAsset.Bounce : _bounceConfig;
            PlinkoGenerationConfig generation =
                _configAsset != null ? _configAsset.Generation : _generationConfig;

            if (TryLoadBakedPaths(layout, sim, bounce, generation))
            {
                return;
            }

            PlinkoBakeResult result = PlinkoSolver.Bake(layout, sim, bounce, generation);

            Layout = result.IsUsable ? result.Layout : null;
            PathSet = result.PathSet;
            Report = result.Report;

            // The draw stream is separate from the bake stream, so a session's replay order does not depend
            // on how many drops the bake happened to simulate.
            _drawRandom = new DeterministicRandom(generation.Seed ^ 0x1B873593);

            if (_logReport)
            {
                if (result.Report.Error != null || !result.Report.IsComplete)
                {
                    Debug.LogWarning(result.Report, this);
                }
                else
                {
                    Debug.Log(result.Report, this);
                }
            }

            Baked?.Invoke(result.Report);
        }

        /// <summary>
        /// Returns a recorded drop from the given entry to the given catch point, cycling that pair's pool so
        /// the same trajectory is not repeated until the others have been used.
        /// </summary>
        /// <param name="entryIndex">Entry to drop from.</param>
        /// <param name="catchPointIndex">Catch point the token must land in.</param>
        /// <returns>A path, or <c>null</c> when the board is not baked or the pair was never reached.</returns>
        public PlinkoPath GetPath(int entryIndex, int catchPointIndex)
        {
            if (!IsBaked)
            {
                Debug.LogWarning("Plinko board is not baked, so it cannot supply a path.", this);
                return null;
            }

            if (entryIndex < 0 || entryIndex >= EntryCount ||
                catchPointIndex < 0 || catchPointIndex >= CatchPointCount)
            {
                Debug.LogWarning($"Entry {entryIndex} or catch point {catchPointIndex} is out of range.", this);
                return null;
            }

            return PathSet.Draw(entryIndex, catchPointIndex, _drawRandom);
        }

        /// <summary>
        /// Whether any simulated drop from this entry reached this catch point.
        /// </summary>
        public bool IsReachable(int entryIndex, int catchPointIndex)
        {
            return IsBaked && PathSet.IsReachable(entryIndex, catchPointIndex);
        }

        /// <summary>
        /// Projects a board space position into world space through this transform.
        /// </summary>
        public Vector3 BoardToWorld(Vector2 boardPosition)
        {
            return transform.TransformPoint(new Vector3(boardPosition.x, boardPosition.y, 0f));
        }

        /// <summary>
        /// Uses the assigned baked asset when it belongs to this exact board and these exact settings.
        /// </summary>
        /// <remarks>
        /// A mismatch is reported as an error and the board falls back to simulating. Silently replaying data
        /// baked from a different board would look completely convincing and be entirely wrong: the token
        /// would glide through pegs that have moved and land where it no longer can.
        /// </remarks>
        private bool TryLoadBakedPaths(
            PlinkoLayout layout, PlinkoSimConfig sim, PlinkoBounceConfig bounce,
            PlinkoGenerationConfig generation)
        {
            if (_pathSetAsset == null)
            {
                return false;
            }

            ulong expected = PlinkoHash.Compute(layout, sim, bounce, generation);

            if (!_pathSetAsset.HasData)
            {
                Debug.LogError(
                    $"Plinko path asset '{_pathSetAsset.name}' holds no data. Bake it, or clear the field.",
                    this);
                return false;
            }

            if (_pathSetAsset.Signature != PlinkoHash.Format(expected))
            {
                Debug.LogError(
                    $"Plinko path asset '{_pathSetAsset.name}' was baked from a different board " +
                    $"(asset {_pathSetAsset.Signature}, board {PlinkoHash.Format(expected)}). Re-bake it. " +
                    "Simulating instead so this board still works.",
                    this);
                return false;
            }

            Layout = _pathSetAsset.Layout;
            PathSet = _pathSetAsset.ToPathSet();
            Report = null;

            _drawRandom = new DeterministicRandom(generation.Seed ^ 0x1B873593);

            if (_logReport)
            {
                Debug.Log(
                    $"Plinko loaded {PathSet.TotalPathCount} baked paths from '{_pathSetAsset.name}' " +
                    $"(signature {_pathSetAsset.Signature}).", this);
            }

            Baked?.Invoke(null);
            return true;
        }

        private PlinkoLayout BuildLayout()
        {
            switch (_preset)
            {
                case PlinkoLayoutPreset.Staircase:
                    return PlinkoLayoutGenerator.CreateStaircase(_entryCount);

                case PlinkoLayoutPreset.FromAsset:
                    return _configAsset != null ? _configAsset.Layout : null;

                default:
                    return PlinkoLayoutGenerator.CreateStaggeredGrid(
                        _rows, _columns, _entryCount, _catchPointCount);
            }
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos)
            {
                return;
            }

            // Built fresh rather than reusing the baked layout, so gizmos also work before a bake and update
            // live while settings are being edited.
            PlinkoLayout layout = Layout ?? BuildLayout();
            if (layout == null || !layout.Validate(out _))
            {
                return;
            }

            float ballRadius = SimConfig != null ? SimConfig.BallRadius : 0.18f;

            Gizmos.color = Color.grey;
            for (int i = 0; i < layout.Pegs.Count; i++)
            {
                Gizmos.DrawWireSphere(BoardToWorld(layout.Pegs[i]), layout.PegRadius);
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < layout.Entries.Count; i++)
            {
                Vector3 entry = BoardToWorld(layout.Entries[i]);
                Gizmos.DrawWireSphere(entry, ballRadius);
                Gizmos.DrawLine(entry, BoardToWorld(layout.Entries[i] + Vector2.down * 0.5f));
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < layout.CatchPoints.Count; i++)
            {
                PlinkoCatchPoint catchPoint = layout.CatchPoints[i];
                Vector3 left = BoardToWorld(new Vector2(catchPoint.MinX, catchPoint.Y));
                Vector3 right = BoardToWorld(new Vector2(catchPoint.MaxX, catchPoint.Y));

                Gizmos.DrawLine(left, right);
                Gizmos.DrawLine(left, BoardToWorld(new Vector2(catchPoint.MinX, catchPoint.Y - 0.6f)));
            }

            Gizmos.color = Color.white;
            for (int i = 0; i < layout.Walls.Count; i++)
            {
                Gizmos.DrawLine(BoardToWorld(layout.Walls[i].From), BoardToWorld(layout.Walls[i].To));
            }

            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Rect area = layout.PlayArea;
            Vector3 a = BoardToWorld(new Vector2(area.xMin, area.yMin));
            Vector3 b = BoardToWorld(new Vector2(area.xMax, area.yMin));
            Vector3 c = BoardToWorld(new Vector2(area.xMax, area.yMax));
            Vector3 d = BoardToWorld(new Vector2(area.xMin, area.yMax));

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
