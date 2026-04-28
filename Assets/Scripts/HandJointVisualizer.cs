using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace AGVRSystem
{
    /// <summary>
    /// Renders glowing joint spheres, skeleton bone lines, and live angle labels
    /// on tracked hands. Joints pulse and shift color from green (relaxed) to
    /// orange (bent) based on flexion angle.
    /// </summary>
    public class HandJointVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OVRSkeleton _leftSkeleton;
        [SerializeField] private OVRSkeleton _rightSkeleton;

        [Header("Joint Markers")]
        [SerializeField] private float _jointRadius = 0.005f;
        [SerializeField] private float _tipRadius = 0.004f;
        [SerializeField] private float _wristRadius = 0.007f;

        [Header("Bone Lines")]
        [SerializeField] private float _lineWidth = 0.002f;

        [Header("Angle Labels")]
        [SerializeField] private float _angleLabelSize = 0.008f;
        [SerializeField] private float _angleLabelOffset = 0.015f;

        [Header("Effects")]
        [SerializeField] private float _pulseSpeed = 3f;
        [SerializeField] private float _pulseIntensity = 0.4f;
        [SerializeField] private float _glowIntensity = 2.5f;

        [Header("Colors")]
        [SerializeField] private Color _relaxedColor = new Color(0.2f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color _bentColor = new Color(0.95f, 0.55f, 0.1f, 1f);
        [SerializeField] private Color _tipColor = new Color(0.4f, 0.7f, 1f, 1f);
        [SerializeField] private Color _lineColor = new Color(0.3f, 0.8f, 0.5f, 0.6f);
        [SerializeField] private Color _angleTextColor = new Color(1f, 1f, 0.85f, 1f);

        private const int MaxBones = 24;
        private const float MaxFlexionAngle = 120f;
        private const float AngleArcRadius = 0.012f;
        private const int ArcSegments = 12;

        /// <summary>
        /// Bone connection pairs (from, to) defining the skeleton wireframe.
        /// </summary>
        private static readonly int[,] BoneConnections =
        {
            { 0, 2 },   // Wrist -> Thumb0
            { 2, 3 },   // Thumb0 -> Thumb1
            { 3, 4 },   // Thumb1 -> Thumb2
            { 4, 5 },   // Thumb2 -> Thumb3
            { 0, 6 },   // Wrist -> Index1
            { 6, 7 },   // Index1 -> Index2
            { 7, 8 },   // Index2 -> Index3
            { 0, 9 },   // Wrist -> Middle1
            { 9, 10 },  // Middle1 -> Middle2
            { 10, 11 }, // Middle2 -> Middle3
            { 0, 12 },  // Wrist -> Ring1
            { 12, 13 }, // Ring1 -> Ring2
            { 13, 14 }, // Ring2 -> Ring3
            { 0, 15 },  // Wrist -> Pinky0
            { 15, 16 }, // Pinky0 -> Pinky1
            { 16, 17 }, // Pinky1 -> Pinky2
            { 17, 18 }, // Pinky2 -> Pinky3
        };

        /// <summary>
        /// Angle measurement joints: (proximal, pivot, distal) bone indices.
        /// Angles are shown at the pivot bone.
        /// </summary>
        private static readonly int[,] AngleJoints =
        {
            { 2, 3, 4 },   // Thumb IP joint
            { 3, 4, 5 },   // Thumb DIP
            { 6, 7, 8 },   // Index PIP
            { 9, 10, 11 }, // Middle PIP
            { 12, 13, 14 },// Ring PIP
            { 15, 16, 17 },// Pinky MCP
            { 16, 17, 18 },// Pinky PIP
        };

        /// <summary>
        /// Tip bone indices for special coloring.
        /// </summary>
        private static readonly HashSet<int> TipBones = new HashSet<int> { 5, 8, 11, 14, 18 };

        [Header("Smoothing")]
        [Tooltip("Position smoothing speed (higher = more responsive, lower = smoother). 12 is a good default.")]
        [SerializeField] private float _positionSmoothSpeed = 12f;
        [Tooltip("Minimum cutoff frequency for One Euro Filter — reduces low-speed jitter.")]
        [SerializeField] private float _oneEuroMinCutoff = 1.0f;
        [Tooltip("Speed coefficient for One Euro Filter — increases cutoff at high velocity.")]
        [SerializeField] private float _oneEuroBeta = 0.1f;

        private HandVisualData _leftVisual;
        private HandVisualData _rightVisual;
        private Material _jointMaterial;
        private Camera _mainCamera;

        // Per-bone smoothed world positions for each hand
        private Vector3[] _leftSmoothed;
        private Vector3[] _rightSmoothed;
        private Vector3[] _leftPrevRaw;
        private Vector3[] _rightPrevRaw;
        private float[] _leftDxFilter;
        private float[] _rightDxFilter;

        private struct HandVisualData
        {
            public GameObject Root;
            public GameObject[] JointSpheres;
            public LineRenderer[] BoneLines;
            public TextMeshPro[] AngleLabels;
            public LineRenderer[] AngleArcs;
            public bool WasInitialized;
        }

        private void Awake()
        {
            CreateJointMaterial();
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            UpdateHand(_leftSkeleton, ref _leftVisual, "LeftJoints");
            UpdateHand(_rightSkeleton, ref _rightVisual, "RightJoints");
        }

        private void OnDestroy()
        {
            CleanupHand(ref _leftVisual);
            CleanupHand(ref _rightVisual);

            if (_jointMaterial != null)
            {
                Destroy(_jointMaterial);
            }
        }

        private void CreateJointMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _jointMaterial = new Material(shader);
            _jointMaterial.SetColor("_BaseColor", _relaxedColor);
            _jointMaterial.SetFloat("_Smoothness", 0.85f);
            _jointMaterial.SetFloat("_Metallic", 0.1f);
            _jointMaterial.EnableKeyword("_EMISSION");
            _jointMaterial.SetColor("_EmissionColor", _relaxedColor * _glowIntensity);
        }

        private void UpdateHand(OVRSkeleton skeleton, ref HandVisualData visual, string rootName)
        {
            if (skeleton == null || !skeleton.IsInitialized || skeleton.Bones == null)
            {
                if (visual.Root != null)
                {
                    visual.Root.SetActive(false);
                }
                return;
            }

            int boneCount = skeleton.Bones.Count;
            if (boneCount == 0)
                return;

            if (!visual.WasInitialized)
            {
                InitializeHandVisuals(skeleton, ref visual, rootName, boneCount);
            }

            if (visual.Root != null && !visual.Root.activeSelf)
            {
                visual.Root.SetActive(true);
            }

            UpdateJointPositions(skeleton, ref visual, boneCount,
                ref GetSmoothedArray(isLeft: rootName == "LeftJoints"),
                ref GetPrevRawArray(isLeft: rootName == "LeftJoints"),
                ref GetDxFilterArray(isLeft: rootName == "LeftJoints"));
            UpdateBoneLines(ref visual, boneCount,
                ref GetSmoothedArray(isLeft: rootName == "LeftJoints"));
            UpdateAngleLabels(skeleton, ref visual, boneCount);
            UpdateAngleArcs(skeleton, ref visual, boneCount);
        }

        private ref Vector3[] GetSmoothedArray(bool isLeft)
        {
            if (isLeft) return ref _leftSmoothed;
            return ref _rightSmoothed;
        }

        private ref Vector3[] GetPrevRawArray(bool isLeft)
        {
            if (isLeft) return ref _leftPrevRaw;
            return ref _rightPrevRaw;
        }

        private ref float[] GetDxFilterArray(bool isLeft)
        {
            if (isLeft) return ref _leftDxFilter;
            return ref _rightDxFilter;
        }

        private void InitializeHandVisuals(
            OVRSkeleton skeleton,
            ref HandVisualData visual,
            string rootName,
            int boneCount)
        {
            visual.Root = new GameObject(rootName);
            visual.Root.transform.SetParent(transform, false);

            // Joint spheres
            visual.JointSpheres = new GameObject[boneCount];
            for (int i = 0; i < boneCount && i < MaxBones; i++)
            {
                float radius = GetJointRadius(i);
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Joint_{i}";
                sphere.transform.SetParent(visual.Root.transform, false);
                sphere.transform.localScale = Vector3.one * radius * 2f;

                // Remove collider to avoid physics interference
                Collider col = sphere.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                // Assign unique material instance for per-joint color
                Renderer rend = sphere.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material = new Material(_jointMaterial);
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                }

                visual.JointSpheres[i] = sphere;
            }

            // Bone lines
            int connectionCount = BoneConnections.GetLength(0);
            visual.BoneLines = new LineRenderer[connectionCount];
            for (int i = 0; i < connectionCount; i++)
            {
                GameObject lineObj = new GameObject($"BoneLine_{i}");
                lineObj.transform.SetParent(visual.Root.transform, false);
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                ConfigureLine(lr, _lineColor, _lineWidth);
                lr.positionCount = 2;
                visual.BoneLines[i] = lr;
            }

            // Angle labels
            int angleCount = AngleJoints.GetLength(0);
            visual.AngleLabels = new TextMeshPro[angleCount];
            visual.AngleArcs = new LineRenderer[angleCount];

            for (int i = 0; i < angleCount; i++)
            {
                // Label
                GameObject labelObj = new GameObject($"AngleLabel_{i}");
                labelObj.transform.SetParent(visual.Root.transform, false);
                TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
                tmp.fontSize = 1.2f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = _angleTextColor;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;

                RectTransform rt = labelObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(0.05f, 0.02f);
                }

                labelObj.transform.localScale = Vector3.one * _angleLabelSize;
                visual.AngleLabels[i] = tmp;

                // Angle arc
                GameObject arcObj = new GameObject($"AngleArc_{i}");
                arcObj.transform.SetParent(visual.Root.transform, false);
                LineRenderer arc = arcObj.AddComponent<LineRenderer>();
                ConfigureLine(arc, _relaxedColor, _lineWidth * 0.8f);
                arc.positionCount = ArcSegments + 1;
                arc.loop = false;
                visual.AngleArcs[i] = arc;
            }

            visual.WasInitialized = true;
        }

        private void UpdateJointPositions(
            OVRSkeleton skeleton,
            ref HandVisualData visual,
            int boneCount,
            ref Vector3[] smoothed,
            ref Vector3[] prevRaw,
            ref float[] dxFilter)
        {
            // Allocate or resize smoothing arrays when bone count changes
            if (smoothed == null || smoothed.Length != boneCount)
            {
                smoothed = new Vector3[boneCount];
                prevRaw = new Vector3[boneCount];
                dxFilter = new float[boneCount];

                for (int i = 0; i < boneCount; i++)
                {
                    OVRBone bone = skeleton.Bones[i];
                    Vector3 startPos = (bone?.Transform != null) ? bone.Transform.position : Vector3.zero;
                    smoothed[i] = startPos;
                    prevRaw[i] = startPos;
                    dxFilter[i] = 0f;
                }
            }

            float dt = Time.deltaTime;
            float time = Time.time;

            for (int i = 0; i < boneCount && i < visual.JointSpheres.Length; i++)
            {
                if (visual.JointSpheres[i] == null)
                    continue;

                OVRBone bone = skeleton.Bones[i];
                if (bone == null || bone.Transform == null)
                    continue;

                Vector3 rawPos = bone.Transform.position;

                // One Euro Filter: adapt cutoff based on speed to reduce jitter
                // at low velocity while staying responsive at high velocity.
                Vector3 rawDelta = (rawPos - prevRaw[i]);
                float rawSpeed = (dt > Mathf.Epsilon) ? rawDelta.magnitude / dt : 0f;

                // Smooth the derivative estimate (derivative filter)
                float cutoffDerivative = 1.0f;
                float alphaD = OneEuroAlpha(dt, cutoffDerivative);
                dxFilter[i] = alphaD * rawSpeed + (1f - alphaD) * dxFilter[i];

                // Adaptive cutoff: higher speed → higher cutoff → less smoothing
                float adaptiveCutoff = _oneEuroMinCutoff + _oneEuroBeta * dxFilter[i];
                float alpha = OneEuroAlpha(dt, adaptiveCutoff);

                smoothed[i] = Vector3.Lerp(smoothed[i], rawPos, alpha);
                prevRaw[i] = rawPos;

                visual.JointSpheres[i].transform.position = smoothed[i];

                // Compute flexion color based on angle at this joint
                float flexion = GetJointFlexion(skeleton, i, boneCount);
                float t = Mathf.Clamp01(flexion / MaxFlexionAngle);

                Color jointColor;
                if (TipBones.Contains(i))
                {
                    jointColor = Color.Lerp(_tipColor, _bentColor, t);
                }
                else
                {
                    jointColor = Color.Lerp(_relaxedColor, _bentColor, t);
                }

                // Pulse effect on bent joints
                float pulse = 1f;
                if (t > 0.2f)
                {
                    pulse = 1f + Mathf.Sin(time * _pulseSpeed + i) * _pulseIntensity * t;
                }

                // Scale pulse
                float radius = GetJointRadius(i);
                visual.JointSpheres[i].transform.localScale = Vector3.one * radius * 2f * pulse;

                // Update material emission
                Renderer rend = visual.JointSpheres[i].GetComponent<Renderer>();
                if (rend != null && rend.material != null)
                {
                    rend.material.SetColor("_BaseColor", jointColor);
                    rend.material.SetColor("_EmissionColor", jointColor * _glowIntensity * pulse);
                }
            }
        }

        // Computes the One Euro Filter alpha blending factor.
        private static float OneEuroAlpha(float dt, float cutoff)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / Mathf.Max(dt, Mathf.Epsilon));
        }

        private void UpdateBoneLines(
            ref HandVisualData visual,
            int boneCount,
            ref Vector3[] smoothed)
        {
            if (smoothed == null)
                return;

            int connectionCount = BoneConnections.GetLength(0);
            for (int i = 0; i < connectionCount; i++)
            {
                if (visual.BoneLines[i] == null)
                    continue;

                int fromIdx = BoneConnections[i, 0];
                int toIdx   = BoneConnections[i, 1];

                if (fromIdx >= boneCount || toIdx >= boneCount ||
                    fromIdx >= smoothed.Length || toIdx >= smoothed.Length)
                {
                    visual.BoneLines[i].enabled = false;
                    continue;
                }

                visual.BoneLines[i].enabled = true;
                // Use smoothed positions so lines never jitter independently of joints
                visual.BoneLines[i].SetPosition(0, smoothed[fromIdx]);
                visual.BoneLines[i].SetPosition(1, smoothed[toIdx]);
            }
        }

        private void UpdateAngleLabels(
            OVRSkeleton skeleton,
            ref HandVisualData visual,
            int boneCount)
        {
            int angleCount = AngleJoints.GetLength(0);

            for (int i = 0; i < angleCount; i++)
            {
                if (visual.AngleLabels[i] == null)
                    continue;

                int proxIdx = AngleJoints[i, 0];
                int pivotIdx = AngleJoints[i, 1];
                int distIdx = AngleJoints[i, 2];

                if (proxIdx >= boneCount || pivotIdx >= boneCount || distIdx >= boneCount)
                {
                    visual.AngleLabels[i].gameObject.SetActive(false);
                    continue;
                }

                OVRBone proxBone = skeleton.Bones[proxIdx];
                OVRBone pivotBone = skeleton.Bones[pivotIdx];
                OVRBone distBone = skeleton.Bones[distIdx];

                if (proxBone?.Transform == null ||
                    pivotBone?.Transform == null ||
                    distBone?.Transform == null)
                {
                    visual.AngleLabels[i].gameObject.SetActive(false);
                    continue;
                }

                Vector3 proxPos = proxBone.Transform.position;
                Vector3 pivotPos = pivotBone.Transform.position;
                Vector3 distPos = distBone.Transform.position;

                Vector3 v1 = proxPos - pivotPos;
                Vector3 v2 = distPos - pivotPos;

                if (v1.sqrMagnitude < Mathf.Epsilon || v2.sqrMagnitude < Mathf.Epsilon)
                {
                    visual.AngleLabels[i].gameObject.SetActive(false);
                    continue;
                }

                float angle = Vector3.Angle(v1, v2);

                visual.AngleLabels[i].gameObject.SetActive(true);

                // Position label offset from pivot joint
                Vector3 midDir = (v1.normalized + v2.normalized).normalized;
                if (midDir.sqrMagnitude < Mathf.Epsilon)
                {
                    midDir = Vector3.up;
                }

                visual.AngleLabels[i].transform.position =
                    pivotPos + midDir * _angleLabelOffset;

                // Billboard toward camera
                if (_mainCamera != null)
                {
                    visual.AngleLabels[i].transform.rotation =
                        Quaternion.LookRotation(
                            visual.AngleLabels[i].transform.position - _mainCamera.transform.position);
                }

                // Color based on flexion
                float t = Mathf.Clamp01(angle / MaxFlexionAngle);
                Color labelCol = Color.Lerp(
                    new Color(0.7f, 1f, 0.8f, 1f),
                    new Color(1f, 0.8f, 0.4f, 1f),
                    t);
                visual.AngleLabels[i].color = labelCol;
                visual.AngleLabels[i].text = $"{angle:F0}\u00B0";
            }
        }

        private void UpdateAngleArcs(
            OVRSkeleton skeleton,
            ref HandVisualData visual,
            int boneCount)
        {
            int angleCount = AngleJoints.GetLength(0);

            for (int i = 0; i < angleCount; i++)
            {
                if (visual.AngleArcs[i] == null)
                    continue;

                int proxIdx = AngleJoints[i, 0];
                int pivotIdx = AngleJoints[i, 1];
                int distIdx = AngleJoints[i, 2];

                if (proxIdx >= boneCount || pivotIdx >= boneCount || distIdx >= boneCount)
                {
                    visual.AngleArcs[i].enabled = false;
                    continue;
                }

                OVRBone proxBone = skeleton.Bones[proxIdx];
                OVRBone pivotBone = skeleton.Bones[pivotIdx];
                OVRBone distBone = skeleton.Bones[distIdx];

                if (proxBone?.Transform == null ||
                    pivotBone?.Transform == null ||
                    distBone?.Transform == null)
                {
                    visual.AngleArcs[i].enabled = false;
                    continue;
                }

                Vector3 pivotPos = pivotBone.Transform.position;
                Vector3 v1 = (proxBone.Transform.position - pivotPos).normalized;
                Vector3 v2 = (distBone.Transform.position - pivotPos).normalized;

                if (v1.sqrMagnitude < Mathf.Epsilon || v2.sqrMagnitude < Mathf.Epsilon)
                {
                    visual.AngleArcs[i].enabled = false;
                    continue;
                }

                visual.AngleArcs[i].enabled = true;

                float angle = Vector3.Angle(v1, v2);
                float t = Mathf.Clamp01(angle / MaxFlexionAngle);

                // Draw arc from v1 to v2 around pivot
                Vector3 cross = Vector3.Cross(v1, v2);
                if (cross.sqrMagnitude < Mathf.Epsilon)
                {
                    cross = Vector3.up;
                }

                for (int s = 0; s <= ArcSegments; s++)
                {
                    float frac = (float)s / ArcSegments;
                    Quaternion rot = Quaternion.AngleAxis(angle * frac, cross.normalized);
                    Vector3 arcPoint = pivotPos + rot * v1 * AngleArcRadius;
                    visual.AngleArcs[i].SetPosition(s, arcPoint);
                }

                // Arc color matches flexion
                Color arcCol = Color.Lerp(_relaxedColor, _bentColor, t);
                arcCol.a = 0.7f;
                visual.AngleArcs[i].startColor = arcCol;
                visual.AngleArcs[i].endColor = arcCol;
            }
        }

        private float GetJointFlexion(OVRSkeleton skeleton, int boneIdx, int boneCount)
        {
            // Find if this bone is a pivot in any angle measurement
            int angleCount = AngleJoints.GetLength(0);
            for (int i = 0; i < angleCount; i++)
            {
                if (AngleJoints[i, 1] == boneIdx)
                {
                    int proxIdx = AngleJoints[i, 0];
                    int distIdx = AngleJoints[i, 2];

                    if (proxIdx < boneCount && distIdx < boneCount)
                    {
                        OVRBone prox = skeleton.Bones[proxIdx];
                        OVRBone pivot = skeleton.Bones[boneIdx];
                        OVRBone dist = skeleton.Bones[distIdx];

                        if (prox?.Transform != null &&
                            pivot?.Transform != null &&
                            dist?.Transform != null)
                        {
                            Vector3 v1 = prox.Transform.position - pivot.Transform.position;
                            Vector3 v2 = dist.Transform.position - pivot.Transform.position;

                            if (v1.sqrMagnitude > Mathf.Epsilon && v2.sqrMagnitude > Mathf.Epsilon)
                            {
                                return Vector3.Angle(v1, v2);
                            }
                        }
                    }
                }
            }

            return 0f;
        }

        private float GetJointRadius(int boneIndex)
        {
            if (boneIndex == 0)
                return _wristRadius;

            if (TipBones.Contains(boneIndex))
                return _tipRadius;

            return _jointRadius;
        }

        private void ConfigureLine(LineRenderer lr, Color color, float width)
        {
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.numCornerVertices = 4;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            // Use URP unlit for lines
            Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (lineMat != null)
            {
                lineMat.SetColor("_BaseColor", color);
                lr.material = lineMat;
            }
        }

        private void CleanupHand(ref HandVisualData visual)
        {
            if (visual.Root != null)
            {
                Destroy(visual.Root);
            }
            visual = default;
        }
    }
}
