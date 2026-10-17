using System.Collections;
using NUnit.Framework;
using Polyus.Monetization;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;

namespace Polyus.Tests
{
    public sealed class DemoPlayModeTests
    {
        private GameObject root;
        [UnityTest]
        public IEnumerator HeldMovementButtonMovesAstronautWithoutRepeatedPhysicsToggles()
        {
            root = new GameObject("Movement under test");
            var demo = root.AddComponent<PolyusPrototype>();
            yield return null;
            demo.BeginRun();
            var body = GameObject.Find("Astronaut").GetComponent<Rigidbody2D>();
            var right = root.transform.Find("POLYUS Interface/Device safe area/Gameplay HUD/>").GetComponent<PolyusControl>();
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 42 };
            right.OnPointerDown(pointer);
            float start = body.position.x;
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            right.OnPointerUp(pointer);
            Assert.That(body.position.x - start, Is.InRange(.7f, 1.3f),
                "Holding movement for 0.1 seconds should travel about one world unit.");
            Assert.That(body.simulated, Is.True);
            Assert.That(body.gravityScale, Is.GreaterThan(0));
        }
        [UnityTest]
        public IEnumerator DemoLoadsArtAndSwitchesBetweenMenuCombatAndPause()
        {
            root = new GameObject("Demo under test");
            var demo = root.AddComponent<PolyusPrototype>();
            yield return null;
            Assert.That(demo.Phase, Is.EqualTo(PolyusPhase.Menu));
            Assert.That(demo.Art.Hero, Is.Not.EqualTo(demo.Art.Disc), "Hero artwork must import as a sprite.");
            Assert.That(demo.Art.Drone, Is.Not.EqualTo(demo.Art.Disc), "Drone artwork must import as a sprite.");
            Assert.That(demo.Art.Background, Is.Not.Null);
            Assert.That(demo.Art.Boss, Is.Not.EqualTo(demo.Art.Drone), "Boss must have its own artwork.");
            var hud = root.transform.Find("POLYUS Interface/Device safe area/Gameplay HUD");
            Assert.That(hud.gameObject.activeSelf, Is.False, "Menu must hide combat controls.");
            demo.BeginRun();
            Assert.That(demo.Phase, Is.EqualTo(PolyusPhase.Playing));
            Assert.That(hud.gameObject.activeSelf, Is.True);
            var astronaut = GameObject.Find("Astronaut").GetComponent<Rigidbody2D>();
            Assert.That(astronaut.simulated, Is.True);
            demo.Pause();
            Assert.That(astronaut.simulated, Is.False);
            Assert.That(hud.gameObject.activeSelf, Is.False, "Pause page must not overlap HUD.");
            demo.Resume();
            demo.RequestFlip();
            yield return new WaitForFixedUpdate();
            Assert.That(astronaut.gravityScale, Is.LessThan(0), "Gravity button must affect physics.");
            demo.SetExternalBlocked(true);
            Assert.That(astronaut.simulated, Is.False, "Ad/store modal must block simulation.");
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            if (MonetizationRoot.Instance != null) Object.Destroy(MonetizationRoot.Instance.gameObject);
            yield return null;
        }
    }
}
