using UnityEngine;
using TMPro;

namespace AGVRSystem.UI
{
    /// <summary>
    /// Panel showing overall grip % and per-finger progress bars (Index, Middle, Ring, Pinky).
    /// Matches the reference design with rounded dark panel background.
    /// </summary>
    public class GripPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _percentageText;
        [SerializeField] private ProgressBar _indexBar;
        [SerializeField] private ProgressBar _middleBar;
        [SerializeField] private ProgressBar _ringBar;
        [SerializeField] private ProgressBar _pinkyBar;

        private const float StrengthScale = 100f;

        /// <summary>
        /// Updates the panel with grip data from an OVRHand.
        /// </summary>
        public void UpdateGrip(OVRHand hand, float overallGrip)
        {
            if (_percentageText != null)
            {
                _percentageText.text = $"{overallGrip:F0}%";
            }

            if (hand == null || !hand.IsTracked)
            {
                SetAllBars(0f);
                return;
            }

            float index = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
            float middle = hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
            float ring = hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
            float pinky = hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);

            if (_indexBar != null) _indexBar.SetValue(index);
            if (_middleBar != null) _middleBar.SetValue(middle);
            if (_ringBar != null) _ringBar.SetValue(ring);
            if (_pinkyBar != null) _pinkyBar.SetValue(pinky);
        }

        private void SetAllBars(float value)
        {
            if (_indexBar != null) _indexBar.SetValueImmediate(value);
            if (_middleBar != null) _middleBar.SetValueImmediate(value);
            if (_ringBar != null) _ringBar.SetValueImmediate(value);
            if (_pinkyBar != null) _pinkyBar.SetValueImmediate(value);
        }
    }
}
