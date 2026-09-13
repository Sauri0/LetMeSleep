using System.Collections.Generic;
using UnityEngine;

namespace LetMeSleep.Presentation
{
    /// <summary>Identity for EVERY owned collider, including compound collider children.
    /// Integration must filter these out of gameplay queries; a layer alone is not a guarantee.</summary>
    [DisallowMultipleComponent]
    public sealed class MosquitoRagdollPart : MonoBehaviour
    {
        public MosquitoRagdollSimulation Owner { get; internal set; }
        public MosquitoBodyId Body { get; internal set; }
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();

        public static bool TryGet(Collider collider, out MosquitoRagdollPart part)
        {
            part = collider ? collider.GetComponentInParent<MosquitoRagdollPart>() : null;
            return part != null;
        }

        public bool HasEnvironmentContact
        {
            get
            {
                contacts.RemoveWhere(c => !c || !c.enabled || !c.gameObject.activeInHierarchy);
                return contacts.Count > 0;
            }
        }

        private void OnCollisionEnter(Collision collision) => Register(collision);
        private void OnCollisionStay(Collision collision) => Register(collision);
        private void OnCollisionExit(Collision collision) => contacts.Remove(collision.collider);
        private void OnDisable() => contacts.Clear();
        internal void ClearContacts() => contacts.Clear();

        private void Register(Collision collision)
        {
            if (!Owner || Owner.Mode != MosquitoRagdollMode.LocalSimulation) return;
            if (TryGet(collision.collider, out var other) && other.Owner == Owner) return;
            contacts.Add(collision.collider);
        }
    }
}
