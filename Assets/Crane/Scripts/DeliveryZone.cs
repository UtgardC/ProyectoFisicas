using System.Collections.Generic;
using UnityEngine;

namespace ScrapCrane
{
    public sealed class DeliveryZone : MonoBehaviour
    {
        public BoxCollider zone;
        public int requiredCount = 5;
        public int DeliveredCount { get; private set; }
        public float DeliveredMass { get; private set; }
        public bool Complete => DeliveredCount >= requiredCount;
        readonly Dictionary<ScrapLoad, float> settling = new Dictionary<ScrapLoad, float>();
        ScrapLoad[] loads;

        void Start()
        {
            if (zone == null) zone = GetComponent<BoxCollider>();
            loads = FindObjectsByType<ScrapLoad>(FindObjectsSortMode.None);
        }

        void FixedUpdate()
        {
            foreach (var load in loads)
            {
                if (load == null || load.Body == null) continue;
                // Require the entire load inside the bin and settled, with the magnet released.
                bool inside = IsContained(load);
                bool settled = inside && !load.Attached && load.Body.linearVelocity.sqrMagnitude < .16f &&
                               load.Body.angularVelocity.sqrMagnitude < .25f;
                if (!settled)
                {
                    settling.Remove(load);
                    if (load.Delivered && !inside) load.Delivered = false;
                    continue;
                }
                if (load.Delivered) continue;
                settling.TryGetValue(load, out float elapsed);
                elapsed += Time.fixedDeltaTime;
                settling[load] = elapsed;
                if (elapsed >= .8f) load.Delivered = true;
            }
            DeliveredCount = 0;
            DeliveredMass = 0f;
            foreach (var load in loads)
                if (load != null && load.Delivered) { DeliveredCount++; DeliveredMass += load.MassKg; }
        }

        public bool IsContained(ScrapLoad load)
        {
            if (zone == null) return false;
            Bounds area = zone.bounds;
            foreach (var col in load.GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger) continue;
                if (!area.Contains(col.bounds.min) || !area.Contains(col.bounds.max)) return false;
            }
            return true;
        }

        public void ResetProgress()
        {
            DeliveredCount = 0;
            DeliveredMass = 0;
            settling.Clear();
            if (loads != null) foreach (var load in loads) if (load != null) load.Delivered = false;
        }
    }
}
