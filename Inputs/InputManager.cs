using SombraStudios.Shared.Patterns.Creational.Singleton;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SombraStudios.Shared.Inputs.Manager
{
    // Credits to github.com/Juli520
    /// <summary>
    /// Manages input actions and maps, enabling and disabling them as needed.
    /// </summary>
    public class InputManager : Singleton<InputManager>
    {
        #region Private Variables
        [Header("Debug")]
        [SerializeField] private bool _showLogs = true;

        private HashSet<InputAction> _previousEnableActions = new();
        #endregion

        #region MonoBehaviour Callbacks
        /// <summary>
        /// Called when the script instance is being loaded.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            EnableAllInputs();
        }

        /// <summary>
        /// Called when the object becomes enabled and active.
        /// </summary>
        private void OnEnable()
        {
            EnableAllInputs();
        }

        /// <summary>
        /// Called when the behaviour becomes disabled.
        /// </summary>
        private void OnDisable()
        {
            DisableAllInputs();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Disables all enabled input actions.
        /// </summary>
        public void DisableAllInputs()
        {
            StopInputs();
        }

        /// <summary>
        /// Enables all disabled input actions.
        /// </summary>
        public void EnableAllInputs()
        {
            ResumeInputs();
        }

        /// <summary>
        /// Enables the action map if it is currently disabled.
        /// </summary>
        /// <param name="inputActionReference">The input action reference.</param>
        public void EnableActionMapIfDisabled(InputActionReference inputActionReference)
        {
            if (inputActionReference == null) { return; }

            if (inputActionReference.action == null) { return; }

            if (inputActionReference.action.actionMap.enabled) { return; }

            inputActionReference.action.actionMap.Enable();
        }

        /// <summary>
        /// Enables the action if it is currently disabled.
        /// </summary>
        /// <param name="inputActionReference">The input action reference.</param>
        public void EnableActionIfDisabled(InputActionReference inputActionReference)
        {
            if (inputActionReference == null) { return; }

            if (inputActionReference.action == null) { return; }

            if (inputActionReference.action.enabled) { return; }

            inputActionReference.action.Enable();
        }

        /// <summary>
        /// Disables the action map if it is currently enabled.
        /// </summary>
        /// <param name="inputActionReference">The input action reference.</param>
        public void DisableActionMapIfEnabled(InputActionReference inputActionReference)
        {
            if (inputActionReference == null) { return; }

            if (inputActionReference.action == null) { return; }

            if (!inputActionReference.action.actionMap.enabled) { return; }

            inputActionReference.action.actionMap.Disable();
        }

        /// <summary>
        /// Disables the action if it is currently enabled.
        /// </summary>
        /// <param name="inputActionReference">The input action reference.</param>
        public void DisableActionIfEnabled(InputActionReference inputActionReference)
        {
            if (inputActionReference == null) { return; }

            if (inputActionReference.action == null) { return; }

            if (!inputActionReference.action.enabled) { return; }

            inputActionReference.action.Disable();
        }

        /// <summary>
        /// Stops all input actions, with optional exceptions.
        /// </summary>
        /// <param name="exceptions">Array of input action references to exclude from stopping.</param>
        public void StopInputs(InputActionReference[] exceptions = null)
        {
            _previousEnableActions.Clear();

            foreach (var action in InputSystem.ListEnabledActions())
            {
                _previousEnableActions.Add(action);
                action.Disable();
            }

            if (exceptions != null)
            {
                foreach (var inputActionReference in exceptions)
                {
                    inputActionReference.action.Enable();
                }
            }
        }

        /// <summary>
        /// Stops all input action maps, with optional exceptions.
        /// </summary>
        /// <param name="exceptions">Array of input action references to exclude from stopping.</param>
        public void StopMapInputs(InputActionReference[] exceptions = null)
        {
            _previousEnableActions.Clear();

            foreach (var action in InputSystem.ListEnabledActions())
            {
                _previousEnableActions.Add(action);
                action.actionMap.Disable();
            }

            if (exceptions != null)
            {
                foreach (var inputActionReference in exceptions)
                {
                    inputActionReference.action.actionMap.Enable();
                }
            }
        }

        /// <summary>
        /// Resumes all previously enabled input actions.
        /// </summary>
        public void ResumeInputs()
        {
            foreach (var action in _previousEnableActions)
            {
                action.Enable();
            }
        }

        /// <summary>
        /// Resumes all previously enabled input action maps.
        /// </summary>
        public void ResumeMapInputs()
        {
            foreach (var action in _previousEnableActions)
            {
                action.actionMap.Enable();
            }
        }
        #endregion
    }
}