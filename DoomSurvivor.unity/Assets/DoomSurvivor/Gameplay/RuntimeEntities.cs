using System;
using System.Collections.Generic;
using DoomSurvivor.Core;
using UnityEngine;

namespace DoomSurvivor.Gameplay
{
    internal sealed class PlayerRuntime
    {
        public GameObject View;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Hp;
        public float MaxHp;
        public float Armor;
        public float MoveSpeed;
        public float PickupRadius;
        public float CollisionRadius;
        public float DamageMultiplier;
        public float AttackSpeedMultiplier;
        public float CritRate;
        public float CritDamage;
        public float ExperienceMultiplier;
        public float Invulnerability;
        public float HitFlashRemaining;
        public float TemporaryMoveMultiplier = 1f;
        public float TemporaryDamageBonus;
        public float BloodPactRemaining;
        public float TemporaryPickupRadiusMul = 1f;
        public float MagnetBurstRemaining;
        public bool MagnetBurstFullMap;
        public float ScooterRemaining;
        public float SniperRemaining;
        public float CrateGuideRemaining;
        public float CapsuleFootballRemaining;
        public float CapsuleFootballCooldown;
        public float CapsuleFootballFlashRemaining;
        public float EnemySlowRemaining;
        public GameObject ScooterView;
        public GameObject SniperView;
        public GameObject CapsuleFootballView;
        public Vector3 CapsuleFootballBaseScale = Vector3.one;
    }

    internal sealed class EnemyRuntime
    {
        public GameObject View;
        public EnemyConfig Config;
        public Vector2 Position;
        public float Hp;
        public float MaxHp;
        public float Radius;
        public float ContactCooldown;
        public float DashCooldown;
        public float DashRemaining;
        public Vector2 DashDirection;
        public bool Active;
        public bool IsBoss;
        public float WeightMultiplier;
        public float StunRemaining;
        public Vector2 KnockbackRemaining;
        public float KnockbackDurationRemaining;
        public GameObject HpBarRoot;
        public GameObject HpBarFill;
    }

    internal sealed class ProjectileRuntime
    {
        public GameObject View;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float RemainingRange;
        public float Damage;
        public int Penetration;
        public bool Active;
        public Color TrailColor = Color.cyan;
        public float TrailSize = 3f;
        public float TrailTimer;
        public ProjectileKind Kind;
        public float ZoneRadiusPixels;
        public float ZoneDuration;
        public float ZoneTickInterval;
        public float AngularVelocity;
        public EnemyRuntime FootballTarget;
        public EnemyRuntime FootballHitA;
        public EnemyRuntime FootballHitB;
        public EnemyRuntime FootballHitC;
        public int FootballHitCount;
        public float FootballLifetime;

        public bool HasHitWithFootball(EnemyRuntime enemy) =>
            enemy != null && (enemy == FootballHitA || enemy == FootballHitB || enemy == FootballHitC);

        public void RegisterFootballHit(EnemyRuntime enemy)
        {
            if (enemy == null || HasHitWithFootball(enemy) || FootballHitCount >= 3) return;
            switch (FootballHitCount)
            {
                case 0: FootballHitA = enemy; break;
                case 1: FootballHitB = enemy; break;
                case 2: FootballHitC = enemy; break;
            }
            FootballHitCount++;
        }
    }

    internal enum ProjectileKind
    {
        Bullet,
        FireBottle,
        CapsuleFootball
    }

    internal sealed class CrystalRuntime
    {
        public GameObject View;
        public Vector2 Position;
        public int Value;
        public bool Active;
        public bool Attracting;
    }

    internal sealed class GroundZoneRuntime
    {
        public GameObject View;
        public Vector2 Position;
        public float Radius;
        public float Damage;
        public float Remaining;
        public float Duration;
        public float TickInterval;
        public float TickTimer;
        public float Age;
        public float VisualSeed;
        public bool Active;
        public readonly GameObject[] Flames = new GameObject[8];
        public readonly float[] FlameAngles = new float[8];
        public readonly float[] FlameOrbits = new float[8];
        public readonly float[] FlamePhases = new float[8];
        public readonly float[] FlameSizes = new float[8];
    }

    internal sealed class OrbitVisualRuntime
    {
        public GameObject View;
        public GameObject GoldAuraView;
        public SpriteRenderer GoldAuraRenderer;
        public Vector2 Position;
        public float Angle;
        public bool Active;
    }

    internal sealed class FuboQinAuraRuntime
    {
        public GameObject Root;
        public GameObject InnerRing;
        public GameObject OuterRing;
        public float VisualSeed;
        public float TickTimer;
        public bool Active;
        public bool UsingGoldAura;
    }

    internal enum MapEventKind
    {
        Crate,
        HiddenCrate,
        Altar,
        HealingChicken,
        PoisonFog,
        Telegraph
    }

    internal sealed class MapEventRuntime
    {
        public GameObject View;
        public MapEventKind Kind;
        public Vector2 Position;
        public float Radius;
        public float TickTimer;
        public bool Active;
        public bool Triggered;
        public string EffectId = string.Empty;
        public float VisualSeed;
        public GameObject AuraView;
        public GameObject FogInnerView;
        public readonly GameObject[] SmokePuffs = new GameObject[5];
    }

public sealed class OwnedUpgrade
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Icon = string.Empty;
        public UpgradeKind Kind;
        public int Level;
        public int MaxLevel;
    }

    internal sealed class RuntimePool
    {
        private readonly Transform root;
        private readonly Stack<GameObject> inactive = new();
        private readonly List<GameObject> all = new();
        private readonly Func<GameObject> factory;

        public int ActiveCount => all.Count - inactive.Count;

        public RuntimePool(string name, Transform parent, Func<GameObject> factory, int warmCount)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            this.factory = factory;
            for (var i = 0; i < warmCount; i++)
            {
                var item = Create();
                Release(item);
            }
        }

        public GameObject Acquire()
        {
            var item = inactive.Count > 0 ? inactive.Pop() : Create();
            item.SetActive(true);
            return item;
        }

        public void Release(GameObject item)
        {
            if (item == null || !item.activeSelf)
            {
                return;
            }
            item.SetActive(false);
            item.transform.SetParent(root, false);
            inactive.Push(item);
        }

        public void DestroyAll()
        {
            foreach (var item in all)
            {
                if (item != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(item);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(item);
                    }
                }
            }
            all.Clear();
            inactive.Clear();
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
            }
        }

        private GameObject Create()
        {
            var item = factory();
            item.transform.SetParent(root, false);
            all.Add(item);
            return item;
        }
    }

    internal enum PickupNoticeSync
    {
        None,
        Scooter,
        Sniper,
        CrateGuide,
        CapsuleFootball,
        DamageBonus,
        MoveSpeed,
        MagnetBurst,
        BossStun,
        EnemySlow
    }

    internal sealed class PickupNoticeRuntime
    {
        public string InstanceId;
        public string EffectKey;
        public string Title;
        public string Detail;
        public float Remaining;
        public PickupNoticeSync Sync;
    }
}
