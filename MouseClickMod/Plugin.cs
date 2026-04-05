using BepInEx;
using BepInEx.Logging;
using GorillaLocomotion;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouseClickMod
{
    [BepInPlugin("egas.MouseClick", "MouseClick", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        private Transform rightHandTriggerCollider;
        private bool mouseClickEnabled = false;

        // Cached original position/scale so we can restore when mod is toggled off
        private Vector3 originalHandScale;
        private bool hasOriginalScale = false;

        // Visual cursor sphere
        private GameObject cursorSphere;

        // GUI toggle button dimensions - bigger and visible
        private readonly Rect toggleRect = new Rect(10f, 10f, 120f, 30f);

        private new ManualLogSource Logger => base.Logger;

        void Awake()
        {
            Instance = this;
            Logger.LogInfo("MouseClick mod loaded.");
        }

        void Start()
        {
            GorillaTagger.OnPlayerSpawned(OnPlayerSpawn);

            // Create a persistent cursor sphere (hidden by default)
            cursorSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cursorSphere.name = "MouseClickCursor";
            cursorSphere.transform.localScale = Vector3.one * 0.07f;
            Destroy(cursorSphere.GetComponent<Collider>());

            Renderer rend = cursorSphere.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("GUI/Text Shader"));
            rend.material.color = Color.clear;
            cursorSphere.SetActive(false);
        }

        void OnDestroy()
        {
            if (cursorSphere != null)
                Destroy(cursorSphere);

            // Re-enable TransformFollow if we disabled it
            RestoreHandFollow();
        }

        void Update()
        {
            if (!mouseClickEnabled || rightHandTriggerCollider == null)
            {
                if (cursorSphere != null && cursorSphere.activeSelf)
                    cursorSphere.SetActive(false);
                return;
            }

            // Get the main camera - try multiple approaches
            Camera cam = GetGameCamera();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 512f))
            {
                cursorSphere.SetActive(true);
                cursorSphere.transform.position = hit.point;

                Renderer rend = cursorSphere.GetComponent<Renderer>();

                if (Mouse.current.leftButton.isPressed)
                {
                    // Show magenta cursor when clicking
                    rend.material.color = Color.magenta;

                    // Move the hand trigger collider to the hit point to press buttons
                    rightHandTriggerCollider.position = hit.point;
                    rightHandTriggerCollider.localScale = Vector3.one * 0.07f;
                }
                else
                {
                    // Show semi-transparent white cursor when hovering
                    rend.material.color = new Color(1f, 1f, 1f, 0.4f);
                }
            }
            else
            {
                if (cursorSphere.activeSelf)
                    cursorSphere.SetActive(false);
            }
        }

        void OnGUI()
        {
            // Draw toggle button
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = mouseClickEnabled ? Color.green : Color.red;

            if (GUI.Button(toggleRect, mouseClickEnabled ? "Mouse Click: ON" : "Mouse Click: OFF"))
            {
                mouseClickEnabled = !mouseClickEnabled;

                if (!mouseClickEnabled)
                {
                    // Hide cursor sphere when disabled
                    if (cursorSphere != null)
                        cursorSphere.SetActive(false);
                }

                Logger.LogInfo($"MouseClick toggled: {mouseClickEnabled}");
            }

            GUI.backgroundColor = prev;
        }

        void OnPlayerSpawn()
        {
            Logger.LogInfo("Player spawned, locating RightHandTriggerCollider...");

            GameObject go = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/RightHandTriggerCollider");
            if (go == null)
            {
                // Fallback search
                go = GameObject.Find("RightHandTriggerCollider");
            }

            if (go != null)
            {
                rightHandTriggerCollider = go.transform;

                if (!hasOriginalScale)
                {
                    originalHandScale = rightHandTriggerCollider.localScale;
                    hasOriginalScale = true;
                }

                // Disable TransformFollow so we can control the hand position manually
                TransformFollow tf = go.GetComponent<TransformFollow>();
                if (tf != null)
                    tf.enabled = false;

                Logger.LogInfo("RightHandTriggerCollider found and set up.");
            }
            else
            {
                Logger.LogWarning("RightHandTriggerCollider not found. Mouse clicking won't work.");
            }
        }

        private Camera GetGameCamera()
        {
            // First try the third person camera (used in the original)
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.thirdPersonCamera != null)
            {
                Camera cam = GorillaTagger.Instance.thirdPersonCamera.GetComponentInChildren<Camera>();
                if (cam != null) return cam;
            }

            // Fallback to main camera
            return Camera.main;
        }

        private void RestoreHandFollow()
        {
            if (rightHandTriggerCollider == null) return;
            TransformFollow tf = rightHandTriggerCollider.GetComponent<TransformFollow>();
            if (tf != null)
                tf.enabled = true;
        }
    }
}
