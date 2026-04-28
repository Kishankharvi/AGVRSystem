using System.Collections.Generic;
using UnityEngine;

namespace AGVRSystem.Exercises
{
    /// <summary>
    /// Spread fingers >= 35 degrees between adjacent pairs, hold 2 seconds.
    /// Includes bilateral symmetry score.
    /// </summary>
    public class FingerSpreadingExercise : BaseExercise
    {
        [SerializeField] private FingerLandmarkTracker _landmarkTracker;

        private const float SpreadThreshold = 35f;
        private const float HoldDuration = 2f;
        private const int DefaultTargetReps = 8;
        private const float SymmetryDenominator = 90f;
        private const int SpreadPairCount = 4;

        private float _holdTimer;
        private readonly List<float> _symmetryScores = new List<float>();

        public override void StartExercise()
        {
            ResetBase();
            TargetReps = DefaultTargetReps;
            _holdTimer = 0f;
            _symmetryScores.Clear();
        }

        public override void StopExercise()
        {
            IsActive = false;
        }

        public override float Evaluate()
        {
            if (_symmetryScores.Count == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < _symmetryScores.Count; i++)
            {
                sum += _symmetryScores[i];
            }

            return sum / _symmetryScores.Count;
        }

        private void Update()
        {
            if (!IsActive || _landmarkTracker == null)
                return;

            var manager = HandTrackingManager.Instance;
            if (manager == null)
                return;

            float[] leftSpreads = _landmarkTracker.ComputeSpread(manager.LeftSkeleton);
            float[] rightSpreads = _landmarkTracker.ComputeSpread(manager.RightSkeleton);

            bool allPass = true;
            for (int i = 0; i < SpreadPairCount; i++)
            {
                if (leftSpreads[i] < SpreadThreshold || rightSpreads[i] < SpreadThreshold)
                {
                    allPass = false;
                    break;
                }
            }

            if (allPass)
            {
                _holdTimer += Time.deltaTime;

                if (_holdTimer >= HoldDuration)
                {
                    float leftAvg = AverageSpread(leftSpreads);
                    float rightAvg = AverageSpread(rightSpreads);
                    float symmetry = 1f - Mathf.Abs(leftAvg - rightAvg) / SymmetryDenominator;
                    symmetry = Mathf.Clamp01(symmetry);

                    _symmetryScores.Add(symmetry);
                    RegisterRep(symmetry);
                    _holdTimer = 0f;
                }
            }
            else
            {
                _holdTimer = 0f;
            }
        }

        private float AverageSpread(float[] spreads)
        {
            float sum = 0f;
            for (int i = 0; i < spreads.Length; i++)
            {
                sum += spreads[i];
            }

            return sum / Mathf.Max(1f, spreads.Length);
        }
    }
}
