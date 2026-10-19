using UnityEngine;

namespace ScrapCrane
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ScrapLoad : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public bool Attached { get; set; }
        public bool Delivered { get; set; }
        public float MassKg => Body != null ? Body.mass : GetComponent<Rigidbody>().mass;
        void Awake() => Body = GetComponent<Rigidbody>();
    }
}
