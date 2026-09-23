using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    /// <summary>
    /// Component lookups that are safe with Unity's overloaded null. In the Editor a missing GetComponent
    /// result is a non-null "fake null" object, so <c>GetComponent&lt;T&gt;() ?? fallback</c> never falls back.
    /// </summary>
    public static class UnityComponents
    {
        public static T GetOrAdd<T>(GameObject owner) where T : Component
            => owner.TryGetComponent(out T existing) ? existing : owner.AddComponent<T>();

        public static T OnSelfOrChildren<T>(GameObject owner) where T : Component
            => owner.TryGetComponent(out T own) ? own : owner.GetComponentInChildren<T>(true);
    }
}
