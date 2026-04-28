using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AGVRSystem.Exercises
{
    /// <summary>
    /// User grabs an XRGrabInteractable object and holds for 3 seconds.
    /// Early release resets timer.
    /// </summary>
    public class GripHoldExercise : BaseExercise
    {
        [SerializeField] private XRGrabInteractable _grabbable;

        private const float HoldDuration = 3f;
        private const int DefaultTargetReps = 5;

        private int _successfulGrabs;
        private int _attempts;
        private float _grabTimer;
        private bool _isHolding;

        /// <summary>
        /// Current hold progress normalized to 0-1 for HUD display.
        /// </summary>
        public float HoldProgress => _isHolding ? Mathf.Clamp01(_grabTimer / (HoldDuration * DifficultyMultiplier)) : 0f;

        public override void StartExercise()
        {
            ResetBase();
            TargetReps = DefaultTargetReps;
            _successfulGrabs = 0;
            _attempts = 0;
            _grabTimer = 0f;
            _isHolding = false;

            if (_grabbable != null)
            {
                _grabbable.selectEntered.AddListener(OnGrabbed);
                _grabbable.selectExited.AddListener(OnReleased);
            }
        }

        public override void StopExercise()
        {
            IsActive = false;
            _isHolding = false;

            if (_grabbable != null)
            {
                _grabbable.selectEntered.RemoveListener(OnGrabbed);
                _grabbable.selectExited.RemoveListener(OnReleased);
            }
        }

        public override float Evaluate()
        {
            return _successfulGrabs / Mathf.Max(1f, _attempts);
        }

        private void Update()
        {
            if (!IsActive || !_isHolding)
                return;

            _grabTimer += Time.deltaTime;

            if (_grabTimer >= HoldDuration * DifficultyMultiplier)
            {
                _successfulGrabs++;
                _isHolding = false;
                float accuracy = _successfulGrabs / (float)Mathf.Max(1, _attempts);
                RegisterRep(accuracy);
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (!IsActive)
                return;

            _isHolding = true;
            _grabTimer = 0f;
            _attempts++;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!IsActive)
                return;

            _isHolding = false;
            _grabTimer = 0f;
        }

        private void OnDestroy()
        {
            if (_grabbable != null)
            {
                _grabbable.selectEntered.RemoveListener(OnGrabbed);
                _grabbable.selectExited.RemoveListener(OnReleased);
            }
        }
    }
}
