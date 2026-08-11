using System;
using System.Collections.Generic;
using DoomSurvivor.Core;
using UnityEngine;

namespace DoomSurvivor.Gameplay.Effects
{
    /// <summary>
    /// 技能 / 战斗视觉特效（轻量、可限流、对象池化）。
    /// </summary>
    internal sealed class SkillFxService
    {
        private const float MaxBudget = 24f;

        private ParticleQuality quality = ParticleQuality.Medium;
        private float budget;
        private Transform root;
        private RuntimePool pulsePool;
        private RuntimePool sparkPool;
        private RuntimePool trailPool;
        private RuntimePool flamePool;
        private RuntimePool muzzlePool;
        private RuntimePool boltPool;
        private RuntimePool slashPool;
        private RuntimePool footballImpactPool;
        private readonly List<FxTween> tweens = new(64);
        private System.Random random = new();

        public void Initialize(Transform parent)
        {
            root = new GameObject("SkillFxRoot").transform;
            root.SetParent(parent, false);
            pulsePool = new RuntimePool("FxPulsePool", root,
                () => FxSpriteFactory.CreateSpriteView("Pulse", FxSpriteFactory.Circle, Color.white, 40), 24);
            sparkPool = new RuntimePool("FxSparkPool", root,
                () => FxSpriteFactory.CreateSpriteView("Spark", FxSpriteFactory.Rect, Color.white, 45), 48);
            trailPool = new RuntimePool("FxTrailPool", root,
                () => FxSpriteFactory.CreateSpriteView("Trail", FxSpriteFactory.Circle, Color.white, 15), 48);
            flamePool = new RuntimePool("FxFlamePool", root,
                () => FxSpriteFactory.CreateSpriteView("Flame",
                    WeaponArtCatalog.LoadBattle("fire_flame") ?? FxSpriteFactory.Flame,
                    Color.white, 25, true), 32);
            muzzlePool = new RuntimePool("FxMuzzlePool", root,
                () => FxSpriteFactory.CreateSpriteView("Muzzle", FxSpriteFactory.Rect, Color.white, 40), 12);
            boltPool = new RuntimePool("FxBoltPool", root, () => FxSpriteFactory.CreateBoltView("Bolt"), 16);
            slashPool = new RuntimePool("FxSlashPool", root, () => FxSpriteFactory.CreateSlashView("Slash"), 16);
            footballImpactPool = new RuntimePool("FxFootballImpactPool", root,
                () => FxSpriteFactory.CreateSpriteView("FootballImpact",
                    WeaponArtCatalog.LoadBattle("capsule_football_impact") ?? FxSpriteFactory.Rect,
                    Color.white, 46, true), 8);
        }

        public void Destroy()
        {
            ClearTweens(true);
            pulsePool?.DestroyAll();
            sparkPool?.DestroyAll();
            trailPool?.DestroyAll();
            flamePool?.DestroyAll();
            muzzlePool?.DestroyAll();
            boltPool?.DestroyAll();
            slashPool?.DestroyAll();
            footballImpactPool?.DestroyAll();
            if (root != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(root.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                }
                root = null;
            }
        }

        public void SetQuality(ParticleQuality value) => quality = value;

        public void Tick(float dt)
        {
            var rate = quality == ParticleQuality.High ? 48f : quality == ParticleQuality.Low ? 12f : 28f;
            budget = Mathf.Min(MaxBudget, budget + rate * dt);
            for (var i = tweens.Count - 1; i >= 0; i--)
            {
                var tween = tweens[i];
                tween.Elapsed += dt;
                var t = Mathf.Clamp01(tween.Elapsed / Mathf.Max(0.001f, tween.Duration));
                if (tween.EaseIn) t = t * t;
                ApplyTween(tween, t);
                if (t >= 1f)
                {
                    ReleaseTweenTarget(tween);
                    tweens.RemoveAt(i);
                }
            }
        }

        public float Budget => budget;

        public bool TrySpend(float cost = 1f)
        {
            if (quality == ParticleQuality.Low && cost > 1f) cost = 1f;
            if (budget < cost) return false;
            budget -= cost;
            return true;
        }

        public void CastPulse(Vector2 position, Color color)
        {
            if (!TrySpend(1f)) return;
            var view = pulsePool.Acquire();
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.color = new Color(color.r, color.g, color.b, 0.55f);
            view.transform.position = position;
            view.transform.localScale = Vector3.one;
            FxSpriteFactory.SetWorldSize(view, FxSpriteFactory.Px(12f), FxSpriteFactory.Px(12f));
            FxSpriteFactory.UpdateDepth(view, position.y, 30);
            StartTween(view, FxKind.Pulse, 0.16f, position, position, 1f, 2.8f, 0.55f, 0f, false, pulsePool);
        }

        public void HitSpark(Vector2 position, bool isCrit)
        {
            if (!TrySpend(isCrit ? 2f : 1f)) return;
            var color = isCrit ? FxSpriteFactory.FromRgb(0xffe066) : FxSpriteFactory.FromRgb(0xfff2c8);
            var count = quality == ParticleQuality.Low ? 3 : isCrit ? 8 : 5;
            var origin = position + new Vector2(0f, FxSpriteFactory.Px(10f));
            for (var i = 0; i < count; i++)
            {
                var ang = Mathf.PI * 2f * i / count + (float)random.NextDouble() * 0.4f;
                var dist = FxSpriteFactory.Px((isCrit ? 28f : 16f) + (float)random.NextDouble() * 18f);
                var target = origin + new Vector2(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist * 0.7f);
                var view = sparkPool.Acquire();
                var renderer = view.GetComponent<SpriteRenderer>();
                renderer.color = new Color(color.r, color.g, color.b, 0.95f);
                view.transform.position = origin;
                view.transform.rotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg);
                FxSpriteFactory.SetWorldSize(view,
                    FxSpriteFactory.Px(isCrit ? 5f : 3f),
                    FxSpriteFactory.Px(isCrit ? 2f : 1.5f));
                FxSpriteFactory.UpdateDepth(view, position.y, 25);
                StartTween(view, FxKind.Spark, 0.14f + (float)random.NextDouble() * 0.08f,
                    origin, target, 1f, 0.2f, 0.95f, 0f, false, sparkPool);
            }

            if (isCrit)
            {
                var ring = pulsePool.Acquire();
                var renderer = ring.GetComponent<SpriteRenderer>();
                renderer.color = FxSpriteFactory.FromRgb(0xffcc66, 0.35f);
                var ringPos = position + new Vector2(0f, FxSpriteFactory.Px(8f));
                ring.transform.position = ringPos;
                FxSpriteFactory.SetWorldSize(ring, FxSpriteFactory.Px(16f), FxSpriteFactory.Px(16f));
                FxSpriteFactory.UpdateDepth(ring, position.y, 24);
                StartTween(ring, FxKind.Pulse, 0.2f, ringPos, ringPos, 1f, 2.4f, 0.35f, 0f, false, pulsePool);
            }
        }

        public void FootballImpact(Vector2 position, Vector2 incomingDirection)
        {
            if (!TrySpend(1f)) return;
            var view = footballImpactPool.Acquire();
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.sprite = WeaponArtCatalog.LoadBattle("capsule_football_impact") ?? FxSpriteFactory.Rect;
            renderer.color = Color.white;
            view.transform.position = position;
            view.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(incomingDirection.y, incomingDirection.x) * Mathf.Rad2Deg - 45f);
            view.transform.localScale = Vector3.one;
            FxSpriteFactory.SetWorldSize(view, FxSpriteFactory.Px(54f), FxSpriteFactory.Px(54f));
            FxSpriteFactory.UpdateDepth(view, position.y, 46);
            StartTween(view, FxKind.Pulse, 0.16f, position, position, 0.82f, 1.28f, 1f, 0f, false,
                footballImpactPool);
        }

        public void SlashArc(Vector2 position, float angleRadians) =>
            SlashArc(position, angleRadians, FxSpriteFactory.FromRgb(0xd0e4ff, 0.85f));

        public void SlashArc(Vector2 position, float angleRadians, Color outerColor)
        {
            if (!TrySpend(1f)) return;
            var view = slashPool.Acquire();
            var lines = view.GetComponentsInChildren<LineRenderer>(true);
            FillArc(lines[0], position, FxSpriteFactory.Px(16f), angleRadians - 0.7f, angleRadians + 0.7f,
                outerColor, FxSpriteFactory.Px(3f));
            FillArc(lines[1], position, FxSpriteFactory.Px(12f), angleRadians - 0.5f, angleRadians + 0.5f,
                new Color(1f, 1f, 1f, 0.5f), FxSpriteFactory.Px(1.5f));
            FxSpriteFactory.UpdateDepth(view, position.y, 18);
            var tween = StartTween(view, FxKind.LineFade, 0.11f, position, position, 1f, 1f, 1f, 0f, false, slashPool);
            CaptureLineColors(tween, view);
        }

        public void Lightning(Vector2 from, Vector2 to)
        {
            var depthY = Mathf.Max(from.y, to.y);
            if (!TrySpend(2.5f))
            {
                SimpleBolt(from, to, false);
                SpawnLightningImpact(to, depthY, false);
                return;
            }

            SimpleBolt(from, to, true);
            if (quality != ParticleQuality.Low && TrySpend(1f))
            {
                // Ghost bolt: slight offset for thicker electric body.
                var side = random.NextDouble() < 0.5 ? -1f : 1f;
                var dx = to.x - from.x;
                var dy = to.y - from.y;
                var len = Mathf.Max(0.001f, Mathf.Sqrt(dx * dx + dy * dy));
                var offset = FxSpriteFactory.Px(2.5f) * side;
                var ghostFrom = from + new Vector2(-dy / len * offset, dx / len * offset);
                var ghostTo = to + new Vector2(-dy / len * offset * 0.35f, dx / len * offset * 0.35f);
                SimpleBolt(ghostFrom, ghostTo, false);
            }

            var sourceFlash = pulsePool.Acquire();
            var sourceRenderer = sourceFlash.GetComponent<SpriteRenderer>();
            sourceRenderer.color = FxSpriteFactory.FromRgb(0xb8f0ff, 0.78f);
            sourceFlash.transform.position = from;
            FxSpriteFactory.SetWorldSize(sourceFlash, FxSpriteFactory.Px(18f), FxSpriteFactory.Px(18f));
            FxSpriteFactory.UpdateDepth(sourceFlash, depthY, 26);
            StartTween(sourceFlash, FxKind.Pulse, 0.12f, from, from, 1f, 2.4f, 0.78f, 0f, false, pulsePool);

            SpawnLightningImpact(to, depthY, true);
        }

        public void SniperShot(Vector2 from, Vector2 to)
        {
            var depthY = Mathf.Max(from.y, to.y);
            var direction = to - from;
            var angle = Mathf.Atan2(direction.y, direction.x);

            if (!TrySpend(2f))
            {
                SpawnSniperTracer(from, to, depthY, false);
                return;
            }

            SpawnSniperTracer(from, to, depthY, true);

            var muzzle = muzzlePool.Acquire();
            var muzzleRenderer = muzzle.GetComponent<SpriteRenderer>();
            muzzleRenderer.color = FxSpriteFactory.FromRgb(0xfff4c0, 0.95f);
            muzzle.transform.position = from;
            muzzle.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            FxSpriteFactory.SetWorldSize(muzzle, FxSpriteFactory.Px(18f), FxSpriteFactory.Px(7f));
            FxSpriteFactory.UpdateDepth(muzzle, depthY, 28);
            StartTween(muzzle, FxKind.Muzzle, 0.08f, from, from, 1f, 2.2f, 0.95f, 0f, false, muzzlePool);

            var muzzleBloom = pulsePool.Acquire();
            var bloomRenderer = muzzleBloom.GetComponent<SpriteRenderer>();
            bloomRenderer.color = FxSpriteFactory.FromRgb(0xffe08a, 0.55f);
            muzzleBloom.transform.position = from;
            FxSpriteFactory.SetWorldSize(muzzleBloom, FxSpriteFactory.Px(14f), FxSpriteFactory.Px(14f));
            FxSpriteFactory.UpdateDepth(muzzleBloom, depthY, 27);
            StartTween(muzzleBloom, FxKind.Pulse, 0.1f, from, from, 1f, 2.6f, 0.55f, 0f, false, pulsePool);

            var impact = pulsePool.Acquire();
            var impactRenderer = impact.GetComponent<SpriteRenderer>();
            impactRenderer.color = FxSpriteFactory.FromRgb(0xfff2a8, 0.7f);
            impact.transform.position = to;
            FxSpriteFactory.SetWorldSize(impact, FxSpriteFactory.Px(20f), FxSpriteFactory.Px(20f));
            FxSpriteFactory.UpdateDepth(impact, depthY, 26);
            StartTween(impact, FxKind.Pulse, 0.14f, to, to, 1f, 2.8f, 0.7f, 0f, false, pulsePool);

            var sparkCount = quality == ParticleQuality.Low ? 4 : quality == ParticleQuality.High ? 9 : 6;
            for (var i = 0; i < sparkCount; i++)
            {
                var spread = angle + ((float)random.NextDouble() - 0.5f) * 1.4f;
                var dist = FxSpriteFactory.Px(12f + (float)random.NextDouble() * 22f);
                var origin = to;
                var target = to + new Vector2(Mathf.Cos(spread) * dist, Mathf.Sin(spread) * dist * 0.75f);
                var spark = sparkPool.Acquire();
                var renderer = spark.GetComponent<SpriteRenderer>();
                renderer.color = FxSpriteFactory.FromRgb(i % 2 == 0 ? 0xfff6d0 : 0xffc978, 0.95f);
                spark.transform.position = origin;
                spark.transform.rotation = Quaternion.Euler(0f, 0f, spread * Mathf.Rad2Deg);
                FxSpriteFactory.SetWorldSize(spark, FxSpriteFactory.Px(5f), FxSpriteFactory.Px(1.8f));
                FxSpriteFactory.UpdateDepth(spark, depthY, 27);
                StartTween(spark, FxKind.Spark, 0.12f + (float)random.NextDouble() * 0.08f,
                    origin, target, 1f, 0.25f, 0.95f, 0f, false, sparkPool);
            }
        }

        private void SpawnLightningImpact(Vector2 to, float depthY, bool fancy)
        {
            var flash = pulsePool.Acquire();
            var flashRenderer = flash.GetComponent<SpriteRenderer>();
            flashRenderer.color = FxSpriteFactory.FromRgb(0xa8e8ff, fancy ? 0.82f : 0.55f);
            flash.transform.position = to;
            FxSpriteFactory.SetWorldSize(flash, FxSpriteFactory.Px(fancy ? 30f : 20f), FxSpriteFactory.Px(fancy ? 30f : 20f));
            FxSpriteFactory.UpdateDepth(flash, depthY, 26);
            StartTween(flash, FxKind.Pulse, fancy ? 0.18f : 0.12f, to, to, 1f, fancy ? 2.6f : 2.0f,
                fancy ? 0.82f : 0.55f, 0f, false, pulsePool);

            if (!fancy) return;
            var ring = pulsePool.Acquire();
            var ringRenderer = ring.GetComponent<SpriteRenderer>();
            ringRenderer.color = FxSpriteFactory.FromRgb(0x6ad4ff, 0.4f);
            ring.transform.position = to;
            FxSpriteFactory.SetWorldSize(ring, FxSpriteFactory.Px(16f), FxSpriteFactory.Px(16f));
            FxSpriteFactory.UpdateDepth(ring, depthY, 25);
            StartTween(ring, FxKind.Pulse, 0.22f, to, to, 1f, 3.2f, 0.4f, 0f, false, pulsePool);

            var sparkCount = quality == ParticleQuality.High ? 9 : 6;
            for (var i = 0; i < sparkCount; i++)
            {
                var angle = Mathf.PI * 2f * i / sparkCount + (float)random.NextDouble() * 0.35f;
                var inner = FxSpriteFactory.Px(4f + (float)random.NextDouble() * 3f);
                var outer = FxSpriteFactory.Px(16f + (float)random.NextDouble() * 14f);
                var origin = to + new Vector2(Mathf.Cos(angle) * inner, Mathf.Sin(angle) * inner * 0.7f);
                var target = to + new Vector2(Mathf.Cos(angle) * outer, Mathf.Sin(angle) * outer * 0.7f);
                var spark = sparkPool.Acquire();
                var renderer = spark.GetComponent<SpriteRenderer>();
                renderer.color = FxSpriteFactory.FromRgb(i % 2 == 0 ? 0xe8fbff : 0x6ad8ff, 0.95f);
                spark.transform.position = origin;
                spark.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                FxSpriteFactory.SetWorldSize(spark, FxSpriteFactory.Px(5f), FxSpriteFactory.Px(1.6f));
                FxSpriteFactory.UpdateDepth(spark, depthY, 27);
                StartTween(spark, FxKind.Spark, 0.16f + (float)random.NextDouble() * 0.08f,
                    origin, target, 1f, 0.28f, 0.95f, 0f, false, sparkPool);
            }
        }

        private void SpawnSniperTracer(Vector2 from, Vector2 to, float depthY, bool fancy)
        {
            var view = boltPool.Acquire();
            var lines = view.GetComponentsInChildren<LineRenderer>(true);
            var points = new List<Vector2>(2) { from, to };
            SetLine(lines[0], points, FxSpriteFactory.FromRgb(0xffb84a, fancy ? 0.28f : 0.16f),
                fancy ? FxSpriteFactory.Px(7f) : FxSpriteFactory.Px(4f));
            SetLine(lines[1], points, FxSpriteFactory.FromRgb(0xffe9a0, fancy ? 0.75f : 0.55f),
                fancy ? FxSpriteFactory.Px(3.2f) : FxSpriteFactory.Px(2.2f));
            SetLine(lines[2], points, FxSpriteFactory.FromRgb(0xfffdf2, 1f),
                fancy ? FxSpriteFactory.Px(1.4f) : FxSpriteFactory.Px(1f));
            FxSpriteFactory.UpdateDepth(view, depthY, 24);
            var tween = StartTween(view, FxKind.LineFade, fancy ? 0.09f : 0.06f, from, from, 1f, 1f, 1f, 0f, true,
                boltPool);
            CaptureLineColors(tween, view);
        }

        public void FireImpact(Vector2 position, float radius)
        {
            if (!TrySpend(2f)) return;
            var count = quality == ParticleQuality.Low ? 3 : quality == ParticleQuality.High ? 7 : 5;
            for (var i = 0; i < count; i++)
            {
                var angle = Mathf.PI * 2f * i / count + (float)random.NextDouble() * 0.25f;
                var distance = radius * (0.08f + (float)random.NextDouble() * 0.28f);
                var origin = position + new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * 0.45f);
                var target = origin + new Vector2(0f, FxSpriteFactory.Px(10f + (float)random.NextDouble() * 8f));
                var flame = flamePool.Acquire();
                var renderer = flame.GetComponent<SpriteRenderer>();
                renderer.color = new Color(1f, 1f, 1f, 0.95f);
                flame.transform.position = origin;
                var scale = 0.5f + (float)random.NextDouble() * 0.35f;
                flame.transform.localScale = Vector3.one;
                FxSpriteFactory.SetWorldSize(flame, FxSpriteFactory.Px(36f) * scale, FxSpriteFactory.Px(35f) * scale);
                FxSpriteFactory.UpdateDepth(flame, position.y, 4);
                StartTween(flame, FxKind.Flame, 0.22f + (float)random.NextDouble() * 0.1f,
                    origin, target, 1f, 1.15f, 0.95f, 0f, false, flamePool);
            }
        }

        public void ProjectileTrail(Vector2 position, Color color, float sizePixels = 3f)
        {
            if (quality == ParticleQuality.Low) return;
            if (!TrySpend(0.35f)) return;
            var view = trailPool.Acquire();
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.color = new Color(color.r, color.g, color.b, 0.55f);
            view.transform.position = position;
            FxSpriteFactory.SetWorldSize(view, FxSpriteFactory.Px(sizePixels * 2f), FxSpriteFactory.Px(sizePixels * 2f));
            FxSpriteFactory.UpdateDepth(view, position.y);
            StartTween(view, FxKind.Pulse, 0.12f, position, position, 1f, 0.2f, 0.55f, 0f, false, trailPool);
        }

        public void DroneMuzzle(Vector2 position, float angleRadians)
        {
            if (!TrySpend(1f)) return;
            var view = muzzlePool.Acquire();
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.color = FxSpriteFactory.FromRgb(0xffee88, 0.9f);
            view.transform.position = position;
            view.transform.rotation = Quaternion.Euler(0f, 0f, angleRadians * Mathf.Rad2Deg);
            FxSpriteFactory.SetWorldSize(view, FxSpriteFactory.Px(10f), FxSpriteFactory.Px(4f));
            FxSpriteFactory.UpdateDepth(view, position.y, 20);
            StartTween(view, FxKind.Muzzle, 0.07f, position, position, 1f, 1.8f, 0.9f, 0f, false, muzzlePool);
        }

        public void DeathBurst(Vector2 position, Color color)
        {
            if (!TrySpend(1.5f)) return;
            var ringPos = position + new Vector2(0f, FxSpriteFactory.Px(6f));
            var ring = pulsePool.Acquire();
            var ringRenderer = ring.GetComponent<SpriteRenderer>();
            ringRenderer.color = new Color(color.r, color.g, color.b, 0.45f);
            ring.transform.position = ringPos;
            FxSpriteFactory.SetWorldSize(ring, FxSpriteFactory.Px(16f), FxSpriteFactory.Px(16f));
            FxSpriteFactory.UpdateDepth(ring, position.y, 12);
            StartTween(ring, FxKind.Pulse, 0.22f, ringPos, ringPos, 1f, 2.6f, 0.45f, 0f, false, pulsePool);

            for (var i = 0; i < 8; i++)
            {
                var angle = Mathf.PI * 2f * i / 8f + (float)random.NextDouble() * 0.3f;
                var speed = FxSpriteFactory.Px(90f + (float)random.NextDouble() * 70f);
                var origin = position + new Vector2(0f, FxSpriteFactory.Px(8f));
                var target = origin + new Vector2(Mathf.Cos(angle) * speed * 0.38f,
                    Mathf.Sin(angle) * speed * 0.32f + FxSpriteFactory.Px(28f));
                var particle = sparkPool.Acquire();
                var renderer = particle.GetComponent<SpriteRenderer>();
                renderer.color = new Color(color.r, color.g, color.b, 0.95f);
                particle.transform.position = origin;
                var size = FxSpriteFactory.Px(2.5f + (float)random.NextDouble() * 2.5f);
                FxSpriteFactory.SetWorldSize(particle, size * 2f, size * 2f);
                FxSpriteFactory.UpdateDepth(particle, position.y, 14);
                StartTween(particle, FxKind.Spark, 0.3f + (float)random.NextDouble() * 0.14f,
                    origin, target, 1f, 0.15f, 0.95f, 0f, false, sparkPool);
            }
        }

        public GameObject AcquireFlame() => flamePool.Acquire();

        public void ReleaseFlame(GameObject view)
        {
            if (view != null) flamePool.Release(view);
        }

        private void SimpleBolt(Vector2 from, Vector2 to, bool fancy)
        {
            var view = boltPool.Acquire();
            var lines = view.GetComponentsInChildren<LineRenderer>(true);
            var length = Vector2.Distance(from, to);
            var maxSegments = quality == ParticleQuality.High ? 12 : 9;
            var segs = fancy ? Mathf.Clamp(Mathf.CeilToInt(length / FxSpriteFactory.Px(22f)), 5, maxSegments) : 2;
            var jitter = fancy
                ? Mathf.Min(FxSpriteFactory.Px(32f), FxSpriteFactory.Px(10f) + length * 0.09f)
                : FxSpriteFactory.Px(7f);
            var points = LightningPath.Build(from.x, from.y, to.x, to.y, segs, jitter, () => (float)random.NextDouble());

            SetLine(lines[0], points, FxSpriteFactory.FromRgb(0x2a7bff, fancy ? 0.28f : 0.14f),
                fancy ? FxSpriteFactory.Px(14f) : FxSpriteFactory.Px(6f));
            SetLine(lines[1], points, FxSpriteFactory.FromRgb(0x7ad8ff, fancy ? 0.78f : 0.55f),
                fancy ? FxSpriteFactory.Px(6.5f) : FxSpriteFactory.Px(3.5f));
            SetLine(lines[2], points, FxSpriteFactory.FromRgb(0xf7ffff, 1f),
                fancy ? FxSpriteFactory.Px(2.4f) : FxSpriteFactory.Px(1.5f));

            if (fancy && quality != ParticleQuality.Low)
            {
                var branchStep = quality == ParticleQuality.High ? 1 : 2;
                for (var i = 1; i < points.Count - 1; i += branchStep)
                {
                    var previous = points[i - 1];
                    var current = points[i];
                    var next = points[i + 1];
                    var dx = next.x - previous.x;
                    var dy = next.y - previous.y;
                    var lineLength = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
                    var side = random.NextDouble() < 0.5 ? -1f : 1f;
                    var branchLength = FxSpriteFactory.Px(16f + (float)random.NextDouble() * 18f);
                    var bx = current.x + (-dy / lineLength) * branchLength * side +
                             dx / lineLength * FxSpriteFactory.Px(6f);
                    var by = current.y + (dx / lineLength) * branchLength * side +
                             dy / lineLength * FxSpriteFactory.Px(6f);
                    SpawnBranchBolt(current, new Vector2(bx, by));
                }
            }

            FxSpriteFactory.UpdateDepth(view, Mathf.Max(from.y, to.y), 22);
            var tween = StartTween(view, FxKind.LineFade, fancy ? 0.2f : 0.1f, from, from, 1f, 1f, 1f, 0f, true,
                boltPool);
            CaptureLineColors(tween, view);
        }

        private void SpawnBranchBolt(Vector2 from, Vector2 to)
        {
            var view = boltPool.Acquire();
            var lines = view.GetComponentsInChildren<LineRenderer>(true);
            var branch = LightningPath.Build(from.x, from.y, to.x, to.y, 3, FxSpriteFactory.Px(5f),
                () => (float)random.NextDouble());
            SetLine(lines[0], branch, FxSpriteFactory.FromRgb(0x2a7bff, 0.22f), FxSpriteFactory.Px(5f));
            SetLine(lines[1], branch, FxSpriteFactory.FromRgb(0xb6f0ff, 0.55f), FxSpriteFactory.Px(2.2f));
            SetLine(lines[2], branch, FxSpriteFactory.FromRgb(0xffffff, 0.9f), FxSpriteFactory.Px(1.2f));
            FxSpriteFactory.UpdateDepth(view, Mathf.Max(from.y, to.y), 22);
            var tween = StartTween(view, FxKind.LineFade, 0.16f, from, from, 1f, 1f, 1f, 0f, true, boltPool);
            CaptureLineColors(tween, view);
        }

        private void FillArc(LineRenderer line, Vector2 center, float radius, float start, float end, Color color,
            float width)
        {
            const int segments = 10;
            line.positionCount = segments + 1;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = Mathf.Lerp(start, end, t);
                line.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius,
                    0f));
            }
        }

        private void SetLine(LineRenderer line, List<Vector2> points, Color color, float width)
        {
            line.positionCount = points.Count;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            for (var i = 0; i < points.Count; i++)
            {
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));
            }
        }

        private FxTween StartTween(GameObject view, FxKind kind, float duration, Vector2 from, Vector2 to,
            float startScale, float endScale, float startAlpha, float endAlpha, bool easeIn, RuntimePool pool)
        {
            var tween = new FxTween
            {
                View = view,
                Kind = kind,
                Duration = duration,
                Elapsed = 0f,
                From = from,
                To = to,
                StartScale = startScale,
                EndScale = endScale,
                StartAlpha = startAlpha,
                EndAlpha = endAlpha,
                EaseIn = easeIn,
                Pool = pool,
                BaseScale = view.transform.localScale
            };
            tweens.Add(tween);
            return tween;
        }

        private static void CaptureLineColors(FxTween tween, GameObject view)
        {
            var lines = view.GetComponentsInChildren<LineRenderer>(true);
            tween.LineStartColors = new Color[lines.Length];
            tween.LineEndColors = new Color[lines.Length];
            for (var i = 0; i < lines.Length; i++)
            {
                tween.LineStartColors[i] = lines[i].startColor;
                tween.LineEndColors[i] = lines[i].endColor;
            }
        }

        private void ApplyTween(FxTween tween, float t)
        {
            if (tween.View == null) return;
            var pos = Vector2.Lerp(tween.From, tween.To, t);
            tween.View.transform.position = pos;
            var scale = Mathf.Lerp(tween.StartScale, tween.EndScale, t);
            if (tween.Kind == FxKind.Muzzle)
            {
                var baseScale = tween.BaseScale;
                tween.View.transform.localScale = new Vector3(baseScale.x * scale, baseScale.y, baseScale.z);
            }
            else if (tween.Kind == FxKind.Pulse || tween.Kind == FxKind.Spark || tween.Kind == FxKind.Flame)
            {
                tween.View.transform.localScale = tween.BaseScale * scale;
            }

            var alpha = Mathf.Lerp(tween.StartAlpha, tween.EndAlpha, t);
            if (tween.View.TryGetComponent<SpriteRenderer>(out var sprite))
            {
                var c = sprite.color;
                c.a = alpha;
                sprite.color = c;
            }

            if (tween.Kind == FxKind.LineFade && tween.LineStartColors != null)
            {
                var lines = tween.View.GetComponentsInChildren<LineRenderer>(true);
                var fade = 1f - t;
                for (var i = 0; i < lines.Length && i < tween.LineStartColors.Length; i++)
                {
                    var start = tween.LineStartColors[i];
                    var end = tween.LineEndColors[i];
                    lines[i].startColor = new Color(start.r, start.g, start.b, start.a * fade);
                    lines[i].endColor = new Color(end.r, end.g, end.b, end.a * fade);
                }
            }
        }

        private void ReleaseTweenTarget(FxTween tween)
        {
            if (tween.View == null || tween.Pool == null) return;
            tween.Pool.Release(tween.View);
        }

        private void ClearTweens(bool release)
        {
            if (release)
            {
                foreach (var tween in tweens)
                {
                    ReleaseTweenTarget(tween);
                }
            }

            tweens.Clear();
        }

        private enum FxKind
        {
            Pulse,
            Spark,
            Flame,
            Muzzle,
            LineFade
        }

        private sealed class FxTween
        {
            public GameObject View;
            public FxKind Kind;
            public float Duration;
            public float Elapsed;
            public Vector2 From;
            public Vector2 To;
            public float StartScale;
            public float EndScale;
            public float StartAlpha;
            public float EndAlpha;
            public bool EaseIn;
            public RuntimePool Pool;
            public Vector3 BaseScale;
            public Color[] LineStartColors;
            public Color[] LineEndColors;
        }
    }
}
