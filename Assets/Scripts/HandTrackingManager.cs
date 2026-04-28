using System;
using UnityEngine;

namespace AGVRSystem
{
    /// <summary>
    /// Singleton manager wrapping OVRHand + OVRSkeleton for both hands.
    /// Provides tracking state events and grip strength calculation.
    /// </summary>
    public class HandTrackingManager : MonoBehaviour
    {
        public static HandTrackingManager Instance { get; private set; }

        [SerializeField] private OVRHand _leftHand;
        [SerializeField] private OVRHand _rightHand;
        [SerializeField] private OVRSkeleton _leftSkeleton;
        [SerializeField] private OVRSkeleton _rightSkeleton;

        public OVRHand LeftHand => _leftHand;
        public OVRHand RightHand => _rightHand;
        public OVRSkeleton LeftSkeleton => _leftSkeleton;
        public OVRSkeleton RightSkeleton => _rightSkeleton;

        public bool IsLeftTracked { get; private set; }
        public bool IsRightTracked { get; private set; }

        /// <summary>
        /// Fired when either hand loses tracking.
        /// </summary>
        public event Action OnTrackingLost;

        /// <summary>
        /// Fired when tracking is restored after being lost.
        /// </summary>
        public event Action OnTrackingRestored;

        private bool _wasLeftTracked;
        private bool _wasRightTracked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            UpdateTrackingState();
        }

        private void UpdateTrackingState()
        {
            bool leftTracked = _leftHand != null
                && _leftHand.IsTracked
                && _leftHand.HandConfidence != OVRHand.TrackingConfidence.Low;

            bool rightTracked = _rightHand != null
                && _rightHand.IsTracked
                && _rightHand.HandConfidence != OVRHand.TrackingConfidence.Low;

            IsLeftTracked = leftTracked;
            IsRightTracked = rightTracked;

            bool wasAnyTracked = _wasLeftTracked || _wasRightTracked;
            bool isAnyTracked = leftTracked || rightTracked;

            if (wasAnyTracked && !isAnyTracked)
            {
                OnTrackingLost?.Invoke();
            }
            else if (!wasAnyTracked && isAnyTracked)
            {
                OnTrackingRestored?.Invoke();
            }

            _wasLeftTracked = leftTracked;
            _wasRightTracked = rightTracked;
        }

        /// <summary>
        /// Calculates grip strength as the average pinch strength of Index, Middle, Ring, Pinky fingers (0-100).
        /// </summary>
        public float GetHandGripStrength(OVRHand hand)
        {
            if (hand == null || !hand.IsTracked)
                return 0f;

            float sum = 0f;
            sum += hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
            sum += hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
            sum += hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
            sum += hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);

            const int fingerCount = 4;
            const float strengthScale = 100f;
            return (sum / fingerCount) * strengthScale;
        }
    }
}
