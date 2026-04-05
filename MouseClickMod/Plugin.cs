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
        private Renderer cursorRenderer;

        // GUI toggle button dimensions - bigger and visible
        private readonly Rect toggleRect = new Rect(10f, 10f, 120f, 30f);

        // Component type names we consider "pressable" — checked by name so we don't
        // need hard assembly references to every button type.
        private static readonly string[] ButtonTypeNames = new[]
        {
            "GorillaPressableButton",
            "GorillaKeyboardButton",
            "GorillaKeyboardButtonNew",
            "GorillaTagButton",
            "GorillaColorizableButton",
            "GorillaColorButton",
            "UnityEngine.UI.Button",
            "Button",
        };

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

            cursorRenderer = cursorSphere.GetComponent<Renderer>();
            cursorRenderer.material = new Material(Shader.Find("GUI/Text Shader"));
            cursorRenderer.material.color = Color.clear;
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

            Camera cam = GetGameCamera();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            // Cast through ALL colliders (including triggers) so we can skip barriers
            RaycastHit[] allHits = Physics.RaycastAll(ray, 512f, Physics.AllLayers, QueryTriggerInteraction.Collide);

            // Sort by distance ascending
            System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit? bestHit = FindBestHit(allHits);

            if (bestHit.HasValue)
            {
                cursorSphere.SetActive(true);
                cursorSphere.transform.position = bestHit.Value.point;

                if (Mouse.current.leftButton.isPressed)
                {
                    cursorRenderer.material.color = Color.magenta;
                    rightHandTriggerCollider.position = bestHit.Value.point;
                    rightHandTriggerCollider.localScale = Vector3.one * 0.07f;
                }
                else
                {
                    cursorRenderer.material.color = new Color(1f, 1f, 1f, 0.4f);
                }
            }
            else
            {
                if (cursorSphere.activeSelf)
                    cursorSphere.SetActive(false);
            }
        }

        /// <summary>
        /// Walk through hits (nearest first) and return the first one that:
        ///   1. Has a known button component on it or any parent, OR
        ///   2. Is the closest non-invisible solid hit if no button is found at all.
        /// Invisible barriers are identified as non-Renderer, non-button colliders.
        /// </summary>
        private RaycastHit? FindBestHit(RaycastHit[] hits)
        {
            if (hits.Length == 0) return null;

            RaycastHit? fallback = null;

            foreach (RaycastHit hit in hits)
            {
                GameObject go = hit.collider.gameObject;

                // Skip the cursor sphere itself
                if (go == cursorSphere) continue;

                // Skip the player's own body colliders
                if (IsPlayerCollider(go)) continue;

                if (IsButtonObject(go))
                    return hit; // Found a button — use it immediately

                // Store first non-player, non-button hit as fallback
                if (fallback == null)
                    fallback = hit;
            }

            // No button hit found — return closest non-player object as fallback
            return fallback;
        }

        private bool IsButtonObject(GameObject go)
        {
            // Walk up the hierarchy up to 3 levels looking for a button component
            Transform t = go.transform;
            for (int i = 0; i < 4 && t != null; i++)
            {
                foreach (Component comp in t.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().Name;
                    foreach (string btn in ButtonTypeNames)
                    {
                        if (typeName == btn) return true;
                    }
                }
                t = t.parent;
            }
            return false;
        }

        private bool IsPlayerCollider(GameObject go)
        {
            // Skip anything that is part of the local player hierarchy
            Transform t = go.transform;
            while (t != null)
            {
                if (t.name == "GorillaPlayer" || t.name == "Player VR Controller"
                    || t.name == "Player Objects")
                    return true;
                t = t.parent;
            }
            return false;
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
