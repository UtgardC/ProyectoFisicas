using UnityEngine;

namespace ScrapCrane
{
    public sealed class ScrapMagnet : MonoBehaviour
    {
        public ArticulationBody magnetBody;
        [Min(.1f)] public float attractionRange = 1.1f;
        [Min(1f)] public float holdingForce = 18000f;
        public bool IsOn { get; private set; }
        public ScrapLoad AttachedLoad { get; private set; }
        public Vector3 Pole => magnetBody.transform.TransformPoint(0f, -.29f, 0f);
        public float CarriedMass => AttachedLoad != null ? AttachedLoad.MassKg : 0f;
        readonly Collider[] nearby = new Collider[32];
        FixedJoint attachment;
        LineRenderer field;

        void Start()
        {
            var ring = new GameObject("Campo del electroimán");
            ring.transform.SetParent(magnetBody.transform, false);
            field = ring.AddComponent<LineRenderer>();
            field.useWorldSpace = false;
            field.loop = true;
            field.positionCount = 64;
            field.widthMultiplier = .025f;
            field.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            field.material.color = new Color(1f, .63f, .18f);
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64;
                field.SetPosition(i, new Vector3(Mathf.Cos(angle) * .8f, -.31f, Mathf.Sin(angle) * .8f));
            }
            field.enabled = IsOn;
        }

        public void Toggle()
        {
            IsOn = !IsOn;
            if (!IsOn) Release();
            if (field != null) field.enabled = IsOn;
        }

        public void ResetMagnet()
        {
            IsOn = false;
            Release();
            if (field != null) field.enabled = false;
        }

        void Release()
        {
            if (attachment != null) Destroy(attachment);
            if (AttachedLoad != null) AttachedLoad.Attached = false;
            AttachedLoad = null;
            attachment = null;
        }

        void FixedUpdate()
        {
            if (AttachedLoad != null && attachment == null) Release();
            if (!IsOn || AttachedLoad != null) return;

            int count = Physics.OverlapSphereNonAlloc(Pole, attractionRange, nearby, ~0, QueryTriggerInteraction.Ignore);
            ScrapLoad closest = null;
            Vector3 point = default;
            float shortest = attractionRange;
            for (int i = 0; i < count; i++)
            {
                var load = nearby[i].GetComponentInParent<ScrapLoad>();
                if (load == null || load.Attached || load.Delivered || load.Body == null) continue;
                Vector3 surface = nearby[i].ClosestPoint(Pole);
                float distance = Vector3.Distance(Pole, surface);
                // Only steel below the pole is attracted; no grabbing through the crane.
                if (load.Body.worldCenterOfMass.y > Pole.y + .15f || distance >= shortest) continue;
                closest = load;
                point = surface;
                shortest = distance;
            }
            if (closest == null) return;

            if (shortest < .19f)
            {
                // Preserve the contact pose. The payload remains a normal, dynamic Rigidbody.
                attachment = closest.gameObject.AddComponent<FixedJoint>();
                attachment.autoConfigureConnectedAnchor = false;
                attachment.anchor = closest.transform.InverseTransformPoint(point);
                attachment.connectedArticulationBody = magnetBody;
                attachment.connectedAnchor = magnetBody.transform.InverseTransformPoint(point);
                attachment.breakForce = holdingForce;
                attachment.breakTorque = holdingForce * .5f;
                attachment.enableCollision = false;
                AttachedLoad = closest;
                closest.Attached = true;
                closest.Body.WakeUp();
                return;
            }

            Vector3 direction = (Pole - point).normalized;
            float force = Mathf.Min(holdingForce * .45f, closest.MassKg * 35f) * (1f - shortest / attractionRange);
            // Equal and opposite forces keep the passive cable responsive to the payload.
            closest.Body.AddForceAtPosition(direction * force, point, ForceMode.Force);
            magnetBody.AddForceAtPosition(-direction * force, Pole, ForceMode.Force);
        }
    }
}
