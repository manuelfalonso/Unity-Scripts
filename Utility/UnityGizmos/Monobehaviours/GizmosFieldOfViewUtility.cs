using UnityEngine;

namespace SombraStudios.Shared.Utility.UnityGizmos
{
    /// <summary>
    /// Draws a field of view cone: two boundary lines and the arc that closes them.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="Gizmos"/> line segments rather than <c>Handles.DrawSolidArc</c>, because
    /// <c>Handles</c> lives in <c>UnityEditor</c> and would drag an Editor-only dependency into a
    /// runtime component. <see cref="Gizmos"/> has no arc primitive, hence the segments.
    /// <para>
    /// Like every other gizmo utility here, coordinates are local while the base's
    /// "Is Local Position" option is enabled, since it sets <see cref="Gizmos.matrix"/> to the
    /// transform's local-to-world matrix before drawing.
    /// </para>
    /// </remarks>
    public class GizmosFieldOfViewUtility : GizmosUtility
    {
        [Header("Field Of View")]
        [Tooltip("The full field of view angle, in degrees, spread evenly either side of the facing axis.")]
        [SerializeField][Range(0f, 360f)] private float _fieldOfViewAngle = 160f;

        [Tooltip("How far the field of view reaches, in world units.")]
        [SerializeField] private float _viewDistance = 12f;

        [Tooltip("Sweep the cone in the XY plane around the local forward axis, as 2D games need, " +
                 "instead of the XZ plane around the local up axis.")]
        [SerializeField] private bool _is2D = false;

        [Tooltip("How many line segments approximate the arc. Higher is smoother.")]
        [SerializeField][Range(2, 128)] private int _arcSegments = 24;

        /// <summary>
        /// Gets or sets the full field of view angle, in degrees.
        /// </summary>
        public float FieldOfViewAngle { get => _fieldOfViewAngle; set => _fieldOfViewAngle = value; }

        /// <summary>
        /// Gets or sets how far the field of view reaches, in world units.
        /// </summary>
        public float ViewDistance { get => _viewDistance; set => _viewDistance = value; }

        /// <summary>
        /// Gets or sets whether the cone is swept for a 2D game.
        /// </summary>
        public bool Is2D { get => _is2D; set => _is2D = value; }


        /// <inheritdoc/>
        protected override void DrawGizmo()
        {
            DrawFieldOfViewCone();
        }


        /// <summary>
        /// Draws the two cone boundaries and the arc joining them.
        /// </summary>
        private void DrawFieldOfViewCone()
        {
            if (_fieldOfViewAngle <= 0f || _viewDistance <= 0f) { return; }

            // In 2D the sprite's up axis is what "forward" means, and the cone opens in the XY plane.
            var facing = _is2D ? Vector3.up : Vector3.forward;
            var sweepAxis = _is2D ? Vector3.forward : Vector3.up;
            var halfAngle = _fieldOfViewAngle * 0.5f;

            var firstEdge = EdgePoint(-halfAngle, facing, sweepAxis);
            var lastEdge = EdgePoint(halfAngle, facing, sweepAxis);

            Gizmos.DrawLine(Vector3.zero, firstEdge);
            Gizmos.DrawLine(Vector3.zero, lastEdge);

            var previous = firstEdge;

            for (var i = 1; i <= _arcSegments; i++)
            {
                var angle = Mathf.Lerp(-halfAngle, halfAngle, (float)i / _arcSegments);
                var point = EdgePoint(angle, facing, sweepAxis);

                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }

        /// <summary>
        /// Returns the point on the cone's rim at the given offset from the facing direction.
        /// </summary>
        /// <param name="angle">Degrees away from <paramref name="facing"/>.</param>
        /// <param name="facing">The direction the cone points along.</param>
        /// <param name="sweepAxis">The axis the cone opens around.</param>
        /// <returns>A point at <see cref="ViewDistance"/> from the origin.</returns>
        private Vector3 EdgePoint(float angle, Vector3 facing, Vector3 sweepAxis)
        {
            return Quaternion.AngleAxis(angle, sweepAxis) * facing * _viewDistance;
        }
    }
}
