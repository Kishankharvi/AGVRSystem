using UnityEngine;

namespace AGVRSystem.Exercises
{
    /// <summary>
    /// Hold index pinch >= 0.85f for 1.5 seconds. Early release counts as a failed attempt.
    /// </summary>
    public class PrecisionPinchingExercise : BaseExercise
    {
        private const float PinchThreshold = 0.85f;
        private const float HoldDuration = 1.5f;
        private const int DefaultTargetReps = 10;

        private float _pinchTimer;
        private bool _isPinching;
        private int _failedAttempts;
        private float _pinchStrengthAccumulator;
        private int _pinchSampleCount;

        /// <summary>
        /// Current pinch hold progress normalized to 0-1 for HUD display.
        /// </summary>
        public float HoldProgress => _isPinching ? Mathf.Clamp01(_pinchTimer / HoldDuration) : 0f;

        public override void StartExercise()
        {
            ResetBase();
            TargetReps = DefaultTargetReps;
            _pinchTimer = 0f;
            _isPinching = false;
            _failedAttempts = 0;
            _pinchStrengthAccumulator = 0f;
            _pinchSampleCount = 0;
        }

        public override void StopExercise()
        {
            IsActive = false;
        }

        public override float Evaluate()
        {
            return CurrentReps / Mathf.Max(1f, CurrentReps + _failedAttempts);
        }

        private void Update()
        {
            if (!IsActive)
                return;

            var manager = HandTrackingManager.Instance;
            if (manager == null)
                return;

            float pinchStrength = GetBestPinchStrength(manager);
            float adjustedThreshold = PinchThreshold * Mathf.Clamp(DifficultyMultiplier, 0.5f, 1f);

            if (pinchStrength >= adjustedThreshold)
            {
                if (!_isPinching)
                {
                    _isPinching = true;
                    _pinchTimer = 0f;
                    _pinchStrengthAccumulator = 0f;
                    _pinchSampleCount = 0;
                }

                _pinchTimer += Time.deltaTime;
                _pinchStrengthAccumulator += pinchStrength;
                _pinchSampleCount++;

                if (_pinchTimer >= HoldDuration)
                {
                    float avgStrength = _pinchStrengthAccumulator / Mathf.Max(1, _pinchSampleCount);
                    RegisterRep(avgStrength);
                    _isPinching = false;
                    _pinchTimer = 0f;
                }
            }
            else if (_isPinching)
            {
                _failedAttempts++;
                _isPinching = false;
                _pinchTimer = 0f;
            }
        }

        private float GetBestPinchStrength(HandTrackingManager manager)
        {
            float left = 0f;
            float right = 0f;

            if (manager.LeftHand != null && manager.IsLeftTracked)
            {
                left = manager.LeftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
            }

            if (manager.RightHand != null && manager.IsRightTracked)
            {
                right = manager.RightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
            }

            return Mathf.Max(left, right);
        }
    }
}
