using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// Optional asset holding a hand placed layout and its simulation settings, for authoring board variants.
    /// </summary>
    /// <remarks>
    /// Never the only way to configure a board. <see cref="PlinkoBoard"/> works from its inline settings and
    /// a built in preset, and <see cref="PlinkoSolver.Bake(PlinkoLayout)"/> works from a layout alone. This
    /// asset exists so a designer can place pegs by hand, keep several levels side by side, and swap between
    /// them, which is what the spec's modular per level arrangements need.
    /// </remarks>
    [CreateAssetMenu(fileName = "PlinkoBoardConfig", menuName = "Sombra Studios/Gameplay/Plinko Board Config")]
    public class PlinkoBoardConfigSO : ScriptableObject
    {
        [Tooltip("Hand placed pegs, entries, catch points and walls. Edit the lists directly to author a level.")]
        [SerializeField] private PlinkoLayout _layout = new PlinkoLayout();

        [Tooltip("Ball size, gravity and the budget a single drop is allowed.")]
        [SerializeField] private PlinkoSimConfig _sim = new PlinkoSimConfig();

        [Tooltip("How pegs and walls throw the token back, and how much bounces scatter.")]
        [SerializeField] private PlinkoBounceConfig _bounce = new PlinkoBounceConfig();

        [Tooltip("How many drops to simulate and how many to keep per entry and catch point pair.")]
        [SerializeField] private PlinkoGenerationConfig _generation = new PlinkoGenerationConfig();

        /// <summary>Layout held by this asset.</summary>
        public PlinkoLayout Layout => _layout;

        /// <summary>Ball and budget settings held by this asset.</summary>
        public PlinkoSimConfig Sim => _sim;

        /// <summary>Bounce settings held by this asset.</summary>
        public PlinkoBounceConfig Bounce => _bounce;

        /// <summary>Generation settings held by this asset.</summary>
        public PlinkoGenerationConfig Generation => _generation;

        /// <summary>
        /// Replaces the stored layout with a freshly generated staggered grid, as a starting point for hand
        /// editing.
        /// </summary>
        [ContextMenu("Fill With Staggered Grid")]
        public void FillWithStaggeredGrid()
        {
            _layout = PlinkoLayoutGenerator.CreateStaggeredGrid();
        }

        /// <summary>
        /// Replaces the stored layout with the sparse staircase test board.
        /// </summary>
        [ContextMenu("Fill With Staircase")]
        public void FillWithStaircase()
        {
            _layout = PlinkoLayoutGenerator.CreateStaircase();
        }
    }
}
