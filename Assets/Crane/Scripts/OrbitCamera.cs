using UnityEngine;
using UnityEngine.InputSystem;

namespace ScrapCrane
{
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        [Min(0f), InspectorName("Sensibilidad de zoom")] public float zoomSensitivity = .16f;
        [Min(.1f), InspectorName("Distancia mínima")] public float minimumDistance = 9f;
        [Min(.1f), InspectorName("Distancia máxima")] public float maximumDistance = 28f;
        float yaw, pitch, distance;
        void Start()
        {
            Vector3 offset = transform.position - target.position;
            distance = offset.magnitude;
            yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(offset.y / distance) * Mathf.Rad2Deg;
        }
        void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse != null && !CraneHUD.PointerOverControls())
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * .2f;
                    pitch = Mathf.Clamp(pitch - delta.y * .2f, 12f, 72f);
                }
                distance = Mathf.Clamp(distance - mouse.scroll.y.ReadValue() * zoomSensitivity,
                    minimumDistance, Mathf.Max(minimumDistance, maximumDistance));
            }
            transform.position = target.position + new Vector3(
                Mathf.Sin(yaw * Mathf.Deg2Rad) * Mathf.Cos(pitch * Mathf.Deg2Rad),
                Mathf.Sin(pitch * Mathf.Deg2Rad),
                Mathf.Cos(yaw * Mathf.Deg2Rad) * Mathf.Cos(pitch * Mathf.Deg2Rad)) * distance;
            transform.LookAt(target.position);
        }
    }
}
