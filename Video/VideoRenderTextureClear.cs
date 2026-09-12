using SombraStudios.Shared.Extensions;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

namespace SombraStudios.Shared.Video
{
    /// <summary>
    /// Fix to issue: RenderTexture retaining last video player frame
    /// This happens when using the same render texture to play another video after the previous one.
    /// Reference:
    /// https://forum.unity.com/threads/rendertexture-retaining-last-video-player-frame.498624/#post-3717181
    /// Know issue:
    /// This script may cause problem disabling OpenGL3 API, and enabling Vulkan.
    /// </summary>
    /// <remarks>
    /// Clearing on <c>Awake</c> only covers the frame left behind by a previous play session. Call
    /// <see cref="Clear"/> before starting each subsequent clip to cover the case the summary
    /// describes - swapping videos on one shared render texture.
    /// </remarks>
    public class VideoRenderTextureClear : MonoBehaviour
    {
        [FormerlySerializedAs("_videoplayer")]
        [Tooltip("The player whose target texture is cleared. Resolved from this GameObject when left empty.")]
        [SerializeField] private VideoPlayer _videoPlayer;

        private void Awake()
        {
            this.EnsureComponent(ref _videoPlayer);

            Clear();
        }

        /// <summary>
        /// Clears the video player's target texture to opaque black.
        /// </summary>
        /// <remarks>
        /// Does nothing when there is no player or no target texture. That second guard matters: with a
        /// null target, <see cref="RenderTexture.active"/> would be the backbuffer and the clear would
        /// wipe the screen instead.
        /// </remarks>
        public void Clear()
        {
            if (_videoPlayer == null) { return; }

            var target = _videoPlayer.targetTexture;
            if (target == null) { return; }

            // Restored rather than nulled, so a caller that is mid-render keeps its target.
            var previous = RenderTexture.active;

            RenderTexture.active = target;
            GL.Clear(true, true, Color.black);

            RenderTexture.active = previous;
        }
    }
}
