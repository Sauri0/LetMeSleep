using LetMeSleep.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class UnityComponentsPlayModeTests
    {
        private GameObject owner;

        [TearDown]
        public void TearDown() { if (owner) Object.DestroyImmediate(owner); }

        [Test]
        public void GetOrAddCreatesALiveComponentWhenMissing()
        {
            owner = new GameObject("Voice owner");
            // The voice capture used GetComponent ?? AddComponent: in the Editor the missing lookup is a fake
            // null, nothing was added and push-to-talk threw MissingComponentException in Play Mode.
            var capture = UnityComponents.GetOrAdd<AudioSource>(owner);
            Assert.That(capture != null, Is.True);
            Assert.That(capture.isActiveAndEnabled, Is.True);
            Assert.That(UnityComponents.GetOrAdd<AudioSource>(owner), Is.SameAs(capture), "An existing component is reused.");
            Assert.That(owner.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void OnSelfOrChildrenFallsBackToChildrenWhenTheRootLacksIt()
        {
            owner = new GameObject("Character root");
            var child = new GameObject("View"); child.transform.SetParent(owner.transform, false);
            var view = child.AddComponent<AudioSource>();
            child.SetActive(false);
            Assert.That(UnityComponents.OnSelfOrChildren<AudioSource>(owner), Is.SameAs(view), "Inactive children count, like the original lookup.");
            var own = owner.AddComponent<AudioSource>();
            Assert.That(UnityComponents.OnSelfOrChildren<AudioSource>(owner), Is.SameAs(own));
        }
    }
}
