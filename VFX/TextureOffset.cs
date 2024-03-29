using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SombraStudios.Shared.VFX
{
    /// <summary>
    /// Offseting the texture of a Sprite or UI Image to make a moving background.
    /// Required: 
    ///     Texture Import Settings: Wrap Mode -> Repeat
    ///     A Material with shader:
    ///         Universal Render Pipeline/2D/Sprite-Lit-Defaul
    ///         UI/Default
    /// </summary>
    public class TextureOffset : MonoBehaviour
    {
        [Header("Properties")]
        [Header("X Axis")]
        [Tooltip("Disable it to use Animation Curve instead")]
        [SerializeField] private bool _xLinearSpeed = true;
        [Range(-1f, 1f)]
        [SerializeField] private float _xLinearSpeedValue = 0.5f;
        [Space]
        [SerializeField] private AnimationCurve _xCurveSpeedValue;
        [SerializeField] private WrapMode _xPreWrapMode = WrapMode.PingPong;
        [SerializeField] private WrapMode _xPostWrapMode = WrapMode.PingPong;
        [Header("Y Axis")]
        [Tooltip("Disable it to use Animation Curve instead")]
        [SerializeField] private bool _yLinearSpeed = true;
        [Range(-1f, 1f)]
        [SerializeField] private float _yLinearSpeedValue = 0.5f;
        [Space]
        [SerializeField] private AnimationCurve _yCurveSpeedValue;
        [SerializeField] private WrapMode _yPreWrapMode = WrapMode.PingPong;
        [SerializeField] private WrapMode _yPostWrapMode = WrapMode.PingPong;

        [Space]
        [Tooltip("_MainTex is the name used by Unity's builtin shaders")]
        [SerializeField] private string _texturePropertyName = "_MainTex";

        [Header("Debug")]
        [SerializeField] private bool _debugMode = false;

        private float _xOffSet = 0f;
        private float _yOffSet = 0f;
        private float _timeOffSet = 0f;
        private List<string> _shaderName = new List<string>() 
            { "Universal Render Pipeline/2D/Sprite-Lit-Default" , "UI/Default" };

        private Material _material;

        void Start()
        {
            if (GetComponent<Renderer>())
                _material = GetComponent<Renderer>().material;
            else
                _material = GetComponent<Image>().material;

            InitializeCurves();

            if (!_debugMode) return;
            // Debug Mode
            if (_shaderName.Contains(_material.shader.name))
                Debug.Log("Shader is OK");
            else
            { 
                Debug.LogError("The Sahder must be ");
                _shaderName.ForEach(delegate(string name) { Debug.LogError(name); });
            }
        }

        private void InitializeCurves()
        {
            if (_xCurveSpeedValue.length == 0)
                _xCurveSpeedValue = 
                    new AnimationCurve(new Keyframe(0, 0.1f), new Keyframe(1, 0.1f));

            if (_yCurveSpeedValue.length == 0)
                _yCurveSpeedValue = 
                    new AnimationCurve(new Keyframe(0, 0.1f), new Keyframe(1, 0.1f));

            _xCurveSpeedValue.preWrapMode = _xPreWrapMode;
            _xCurveSpeedValue.postWrapMode = _xPostWrapMode;
            _yCurveSpeedValue.preWrapMode = _yPreWrapMode;
            _yCurveSpeedValue.postWrapMode = _yPostWrapMode;
        }

        void Update()
        {
            OffsetTexture();
        }

        private void OffsetTexture()
        {
            if (!_material) return;
            _timeOffSet += Time.deltaTime;

            if (_xLinearSpeed)
                _xOffSet += Time.deltaTime * _xLinearSpeedValue * -1f;
            else
                _xOffSet += Time.deltaTime * _xCurveSpeedValue.Evaluate(_timeOffSet)  * - 1f;

            if (_yLinearSpeed)
                _yOffSet += Time.deltaTime * _yLinearSpeedValue * -1f;
            else
                _yOffSet += Time.deltaTime * _yCurveSpeedValue.Evaluate(_timeOffSet) * -1f;

            _material.SetTextureOffset(_texturePropertyName, new Vector2(_xOffSet, _yOffSet));
        }
    }
}
