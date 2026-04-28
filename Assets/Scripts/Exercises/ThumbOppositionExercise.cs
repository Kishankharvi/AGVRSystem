using UnityEngine;

namespace AGVRSystem.Exercises
{
    /// <summary>
    /// Touch thumb to each finger in sequence (Index -> Middle -> Ring -> Pinky).
    /// Pinch >= 0.75f required, max 0.5s gap between touches.
    /// </summary>
    public class ThumbOppositionExercise : BaseExercise
    {
        private const float PinchThreshold = 0.75f;
        private const float MaxGap = 0.5f;
        private const int DefaultTargetReps = 6;
        private const int SequenceLength = 4;

        private readonly OVRHand.HandFinger[] _sequence =
        {
            OVRHand.HandFinger.Index,
            OVRHand.HandFinger.Middle,
            OVRHand.HandFinger.Ring,
            OVRHand.HandFinger.Pinky
        };

        private int _currentFingerIndex;
        private float _gapTimer;
        private float _totalGapTime;
        private bool _waitingForRelease;

        public override void StartExercise()
        {
            ResetBase();
            TargetReps = DefaultTargetReps;
            ResetSequence();
        }

        public override void StopExercise()
        {
            IsActive = false;
        }

        public override float Evaluate()
        {
            return CurrentReps / (float)Mathf.Max(1, TargetReps);
        }

        private void Update()
        {
            if (!IsActive)
                return;

            var manager = HandTrackingManager.Instance;
            if (manager == null || manager.RightHand == null || !manager.IsRightTracked)
                return;

            OVRHand hand = manager.RightHand;
            OVRHand.HandFinger targetFinger = _sequence[_currentFingerIndex];
            float pinchStrength = hand.GetFingerPinchStrength(targetFinger);

            if (_waitingForRelease)
            {
                if (pinchStrength < PinchThreshold * 0.5f)
                {
                    _waitingForRelease = false;
                }
                return;
            }

            _gapTimer += Time.deltaTime;

            if (_gapTimer > MaxGap && _currentFingerIndex > 0)
            {
                ResetSequence();
                return;
            }

            if (pinchStrength >= PinchThreshold)
            {
                _totalGapTime += _gapTimer;
                _currentFingerIndex++;
                _gapTimer = 0f;
                _waitingForRelease = true;

                if (_currentFingerIndex >= SequenceLength)
                {
                    float maxPossibleGap = MaxGap * SequenceLength;
                    float accuracy = 1f - Mathf.Clamp01(_totalGapTime / maxPossibleGap);
                    RegisterRep(accuracy);
                    ResetSequence();
                }
            }
        }

        private void ResetSequence()
        {
            _currentFingerIndex = 0;
            _gapTimer = 0f;
            _totalGapTime = 0f;
            _waitingForRelease = false;
        }
    }
}
