using DoomSurvivor.Core;
using DoomSurvivor.Gameplay.Effects;
using NUnit.Framework;
using UnityEngine;

namespace DoomSurvivor.Tests.EditMode
{
    public sealed class SkillFxTests
    {
        [Test]
        public void LightningPath_KeepsEndpointsAndRespectsSegmentFloor()
        {
            var points = LightningPath.Build(0f, 0f, 10f, 0f, 1, 4f, () => 0.5f);
            Assert.That(points.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(points[0], Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(points[points.Count - 1], Is.EqualTo(new Vector2(10f, 0f)));
        }

        [Test]
        public void LightningPath_EnvelopeKeepsMidpointNearLineWhenRandomCentered()
        {
            var points = LightningPath.Build(0f, 0f, 100f, 0f, 4, 20f, () => 0.5f);
            Assert.That(points.Count, Is.EqualTo(5));
            for (var i = 1; i < points.Count - 1; i++)
            {
                Assert.That(points[i].y, Is.EqualTo(0f).Within(0.001f));
            }
        }

        [Test]
        public void SkillFx_SpendRespectsBudgetAndLowQualityCostCap()
        {
            var root = new GameObject("SkillFxTestRoot");
            try
            {
                var fx = new SkillFxService();
                fx.Initialize(root.transform);
                fx.SetQuality(ParticleQuality.Medium);
                Assert.That(fx.TrySpend(1f), Is.False);

                fx.Tick(1f);
                Assert.That(fx.Budget, Is.EqualTo(24f).Within(0.001f));
                Assert.That(fx.TrySpend(10f), Is.True);
                Assert.That(fx.Budget, Is.EqualTo(14f).Within(0.001f));

                fx.SetQuality(ParticleQuality.Low);
                fx.Tick(1f);
                var before = fx.Budget;
                Assert.That(fx.TrySpend(5f), Is.True);
                Assert.That(fx.Budget, Is.EqualTo(before - 1f).Within(0.001f));

                while (fx.TrySpend(1f))
                {
                }

                Assert.That(fx.TrySpend(1f), Is.False);
                fx.Lightning(Vector2.zero, new Vector2(1f, 0f));
                fx.SniperShot(Vector2.zero, new Vector2(2f, 0.5f));
                fx.Destroy();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
