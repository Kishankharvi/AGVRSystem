using UnityEngine;

namespace AGVRSystem.Exercises
{
    /// <summary>
    /// Detect index tip touching thumb tip (distance less than 0.015m). 20 taps target, 0.3s cooldown.
    /// </summary>
    public class FingerTappingExercise : BaseExercise
    {
        private const float TapThreshold = 0.015f;
        private const float TapCooldown = 0.3f;
        private const int DefaultTargetTaps = 20;

        private float _cooldownTimer;
        private int _tapCount;

        public override void StartExercise()
        {
            ResetBase();
            TargetReps = DefaultTargetTaps;
            _cooldownTimer = 0f;
            _tapCount = 0;
        }

        public override void StopExercise()
        {
            IsActive = false;
        }

        public override float Evaluate()
        {
            return _tapCount / (float)Mathf.Max(1, TargetReps);
        }

        private void Update()
        {
            if (!IsActive)
                return;

            _cooldownTimer -= Time.deltaTime;

            var manager = HandTrackingManager.Instance;
            if (manager == null || manager.RightSkeleton == null || !manager.RightSkeleton.IsInitialized)
                return;

            var bones = manager.RightSkeleton.Bones;
            if (bones == null || bones.Count <= (int)OVRSkeleton.BoneId.Hand_ThumbTip)
                return;

            int indexTipIdx = (int)OVRSkeleton.BoneId.Hand_IndexTip;
            int thumbTipIdx = (int)OVRSkeleton.BoneId.Hand_ThumbTip;

            if (indexTipIdx >= bones.Count || thumbTipIdx >= bones.Count)
                return;

            Vector3 indexTipPos = bones[indexTipIdx].Transform.position;
            Vector3 thumbTipPos = bones[thumbTipIdx].Transform.position;

            float distance = Vector3.Distance(indexTipPos, thumbTipPos);

            if (distance < TapThreshold && _cooldownTimer <= 0f)
            {
                _tapCount++;
                _cooldownTimer = TapCooldown;
                RegisterRep(1f);
            }
        }
    }
}
