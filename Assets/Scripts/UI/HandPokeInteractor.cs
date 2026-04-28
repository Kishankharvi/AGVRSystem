using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace AGVRSystem.UI
{
    /// <summary>
    /// Detects index fingertip poke interactions with world-space canvases.
    /// Sends PointerDown/Up/Click events to Unity UI elements (Buttons, InputFields, etc.).
    /// Attach to a GameObject with an OVRHand and OVRSkeleton.
    /// </summary>
    public class HandPokeInteractor : MonoBehaviour
    {
        [Header("Hand References")]
        [SerializeField] private OVRHand _hand;
        [SerializeField] private OVRSkeleton _skeleton;

        [Header("Poke Settings")]
        [SerializeField] private float _pokeRadius = 0.015f;
        [SerializeField] private float _pokeDepthThreshold = 0.005f;
        [SerializeField] private LayerMask _canvasLayerMask = ~0;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject _pokeDotPrefab;
        [SerializeField] private Color _hoverColor = new Color(0.2f, 0.78f, 0.38f, 0.8f);
        [SerializeField] private Color _pressColor = new Color(1f, 1f, 1f, 0.95f);

        private const int IndexFingerTipBoneId = 20; // OVRSkeleton.BoneId.Hand_IndexTip
        private Transform _fingerTip;
        private GameObject _pokeDot;
        private Renderer _pokeDotRenderer;

        private GameObject _hoveredObject;
        private GameObject _pressedObject;
        private bool _wasPoking;
        private float _lastPokeTime;

        private static readonly List<RaycastResult> s_raycastResults = new List<RaycastResult>();

        private const float PokeDebounceTime = 0.15f;
        private const float PokeDotScale = 0.008f;

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_pokeDot != null)
            {
                Destroy(_pokeDot);
            }
        }

        // World-space canvases require an event camera to project 3D positions
        // correctly into screen space for GraphicRaycaster to work.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AssignEventCameraToWorldSpaceCanvases();
            SanitizeGraphicsWithoutCanvasRenderer();
        }

        private void AssignEventCameraToWorldSpaceCanvases()
        {
            Camera vrCamera = Camera.main;
            if (vrCamera == null)
                return;

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
                {
                    canvas.worldCamera = vrCamera;
                    Debug.Log($"[HandPokeInteractor] Assigned event camera to world-space canvas: {canvas.name}");
                }
            }
        }

        // Any MaskableGraphic that is missing its CanvasRenderer will crash GraphicRaycaster.
        // Disable raycastTarget on those until the scene has proper CanvasRenderers.
        private void SanitizeGraphicsWithoutCanvasRenderer()
        {
            MaskableGraphic[] graphics = FindObjectsByType<MaskableGraphic>(FindObjectsSortMode.None);
            foreach (MaskableGraphic graphic in graphics)
            {
                if (graphic.raycastTarget && graphic.GetComponent<CanvasRenderer>() == null)
                {
                    graphic.raycastTarget = false;
                    Debug.LogWarning($"[HandPokeInteractor] Disabled raycastTarget on '{graphic.name}' " +
                                     $"({graphic.GetType().Name}) — missing CanvasRenderer. " +
                                     $"Add a CanvasRenderer component to fix this permanently.");
                }
            }
        }

        private void Start()
        {
            CreatePokeDot();
            AssignEventCameraToWorldSpaceCanvases();
            SanitizeGraphicsWithoutCanvasRenderer();
        }

        private void Update()
        {
            if (_hand == null || _skeleton == null || !_hand.IsTracked)
            {
                ClearState();
                SetPokeDotVisible(false);
                return;
            }

            UpdateFingerTipReference();

            if (_fingerTip == null)
            {
                ClearState();
                SetPokeDotVisible(false);
                return;
            }

            Vector3 tipPos = _fingerTip.position;
            UpdatePokeDot(tipPos);

            // Raycast from fingertip forward (into canvas)
            PointerEventData pointerData = CreatePointerData(tipPos);
            GameObject hitObject = RaycastUI(pointerData, tipPos);

            HandleHover(hitObject, pointerData);
            HandlePoke(hitObject, pointerData, tipPos);
        }

        /// <summary>
        /// Finds the index finger tip bone from OVRSkeleton.
        /// </summary>
        private void UpdateFingerTipReference()
        {
            if (_fingerTip != null)
                return;

            var bones = _skeleton.Bones;
            if (bones == null || bones.Count == 0)
                return;

            foreach (var bone in bones)
            {
                if (bone != null && (int)bone.Id == IndexFingerTipBoneId)
                {
                    _fingerTip = bone.Transform;
                    break;
                }
            }
        }

        private PointerEventData CreatePointerData(Vector3 worldPos)
        {
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = GetScreenPosition(worldPos),
                pressPosition = GetScreenPosition(worldPos)
            };
            return eventData;
        }

        // Prefer the world camera from a nearby canvas so the projection is correct
        // for world-space canvases. Falls back to Camera.main only if unavailable.
        private Vector2 GetScreenPosition(Vector3 worldPos)
        {
            Camera cam = GetEventCamera();
            if (cam == null)
                return Vector2.zero;

            return cam.WorldToScreenPoint(worldPos);
        }

        private Camera GetEventCamera()
        {
            // Try to find a world-space canvas near the fingertip that has a camera assigned
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera != null)
                    return canvas.worldCamera;
            }

            return Camera.main;
        }

        private GameObject RaycastUI(PointerEventData pointerData, Vector3 tipPos)
        {
            s_raycastResults.Clear();

            if (EventSystem.current == null)
                return null;

            EventSystem.current.RaycastAll(pointerData, s_raycastResults);

            // Find the closest result that is within poke radius
            float closestDist = float.MaxValue;
            GameObject closest = null;

            foreach (var result in s_raycastResults)
            {
                if (result.gameObject == null)
                    continue;

                float dist = Vector3.Distance(tipPos, result.worldPosition);
                if (dist < _pokeRadius && dist < closestDist)
                {
                    closestDist = dist;
                    closest = result.gameObject;
                    pointerData.pointerCurrentRaycast = result;
                }
            }

            return closest;
        }

        private void HandleHover(GameObject hitObject, PointerEventData pointerData)
        {
            if (hitObject == _hoveredObject)
                return;

            // Exit previous hover
            if (_hoveredObject != null)
            {
                ExecuteEvents.Execute(_hoveredObject, pointerData, ExecuteEvents.pointerExitHandler);
            }

            // Enter new hover
            if (hitObject != null)
            {
                ExecuteEvents.Execute(hitObject, pointerData, ExecuteEvents.pointerEnterHandler);

                // Play hover audio
                if (Audio.UIAudioFeedback.Instance != null)
                {
                    Audio.UIAudioFeedback.Instance.PlayHover();
                }
            }

            _hoveredObject = hitObject;
            UpdatePokeDotColor(hitObject != null);
        }

        private void HandlePoke(GameObject hitObject, PointerEventData pointerData, Vector3 tipPos)
        {
            if (hitObject == null)
            {
                if (_pressedObject != null)
                {
                    ReleasePress(pointerData);
                }
                _wasPoking = false;
                return;
            }

            // Check if fingertip is close enough to be a "poke" (pressed through the canvas plane)
            bool isPoking = IsFingerPoking(tipPos, pointerData);

            if (isPoking && !_wasPoking && Time.time - _lastPokeTime > PokeDebounceTime)
            {
                // Pointer down
                _pressedObject = hitObject;
                pointerData.pointerPress = hitObject;
                pointerData.rawPointerPress = hitObject;

                ExecuteEvents.Execute(hitObject, pointerData, ExecuteEvents.pointerDownHandler);

                // Also try to select InputFields for keyboard focus
                TrySelectInputField(hitObject);

                _lastPokeTime = Time.time;
            }
            else if (!isPoking && _wasPoking && _pressedObject != null)
            {
                // Pointer up + click
                pointerData.pointerPress = _pressedObject;
                ExecuteEvents.Execute(_pressedObject, pointerData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(_pressedObject, pointerData, ExecuteEvents.pointerClickHandler);

                _pressedObject = null;
            }

            _wasPoking = isPoking;
        }

        private bool IsFingerPoking(Vector3 tipPos, PointerEventData pointerData)
        {
            if (pointerData.pointerCurrentRaycast.isValid)
            {
                float dist = Vector3.Distance(tipPos, pointerData.pointerCurrentRaycast.worldPosition);
                return dist < _pokeDepthThreshold;
            }
            return false;
        }

        /// <summary>
        /// If the poked object is a TMP_InputField or part of one, activate it for keyboard input.
        /// </summary>
        private void TrySelectInputField(GameObject target)
        {
            if (target == null)
                return;

            var inputField = target.GetComponentInParent<TMP_InputField>();
            if (inputField != null)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
        }

        private void ReleasePress(PointerEventData pointerData)
        {
            if (_pressedObject != null)
            {
                ExecuteEvents.Execute(_pressedObject, pointerData, ExecuteEvents.pointerUpHandler);
                _pressedObject = null;
            }
        }

        private void ClearState()
        {
            if (_hoveredObject != null || _pressedObject != null)
            {
                var pointerData = new PointerEventData(EventSystem.current);

                if (_hoveredObject != null)
                {
                    ExecuteEvents.Execute(_hoveredObject, pointerData, ExecuteEvents.pointerExitHandler);
                    _hoveredObject = null;
                }

                ReleasePress(pointerData);
            }

            _wasPoking = false;
            _fingerTip = null;
        }

        private void CreatePokeDot()
        {
            _pokeDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _pokeDot.name = "PokeDot";
            _pokeDot.transform.localScale = Vector3.one * PokeDotScale;

            // Remove collider so it doesn't interfere with raycasts
            var collider = _pokeDot.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            _pokeDotRenderer = _pokeDot.GetComponent<Renderer>();
            if (_pokeDotRenderer != null)
            {
                _pokeDotRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _pokeDotRenderer.material.color = _hoverColor;
            }

            _pokeDot.SetActive(false);
        }

        private void UpdatePokeDot(Vector3 position)
        {
            if (_pokeDot == null)
                return;

            _pokeDot.transform.position = position;
            SetPokeDotVisible(true);
        }

        private void UpdatePokeDotColor(bool isHovering)
        {
            if (_pokeDotRenderer == null)
                return;

            _pokeDotRenderer.material.color = isHovering ? _pressColor : _hoverColor;
            float scale = isHovering ? PokeDotScale * 1.5f : PokeDotScale;
            _pokeDot.transform.localScale = Vector3.one * scale;
        }

        private void SetPokeDotVisible(bool visible)
        {
            if (_pokeDot != null && _pokeDot.activeSelf != visible)
            {
                _pokeDot.SetActive(visible);
            }
        }
    }
}
