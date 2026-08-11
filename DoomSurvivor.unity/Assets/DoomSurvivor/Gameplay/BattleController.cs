using System;
using System.Collections.Generic;
using System.Linq;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay.Effects;
using UnityEngine;

namespace DoomSurvivor.Gameplay
{
    public sealed class BattleController : MonoBehaviour
    {
        private const float FixedStep = 1f / 60f;
        private const int MaxStepsPerFrame = 5;
        private const float ActorVisualScaleMultiplier = 1.5f;
        private const float FireBottleSpeedPixels = 380f;
        private const float FireBottleAngularVelocity = 540f;
        private const float GrassTileSize = 8f;
        private const float CrateRefreshInterval = 40f;
        private const float CapsuleFootballFireInterval = 2.5f;
        private const float CapsuleFootballRetryInterval = 0.2f;
        private const float CapsuleFootballTargetRangePixels = 1200f;
        private const float CapsuleFootballBounceRangePixels = 300f;
        private const float CapsuleFootballSpeedPixels = 600f;
        private const float CapsuleFootballDamage = 15f;
        private const int CapsuleFootballMaxHits = 3;
        private const float CapsuleFootballLifetime = 3f;
        private const float CapsuleFootballKnockbackPixels = 120f;
        private const float CapsuleFootballKnockbackDuration = 0.22f;
        private const float CapsuleFootballFlashDuration = 0.12f;
        private const float CapsuleFootballAngularVelocity = 900f;

        private readonly struct EnvironmentPlacement
        {
            public EnvironmentPlacement(Vector2 normalizedPosition, string spriteKey, Vector2 visualSize, Vector2 collisionSize)
            {
                NormalizedPosition = normalizedPosition;
                SpriteKey = spriteKey;
                VisualSize = visualSize;
                CollisionSize = collisionSize;
            }

            public Vector2 NormalizedPosition { get; }
            public string SpriteKey { get; }
            public Vector2 VisualSize { get; }
            public Vector2 CollisionSize { get; }
        }

        // Positions are authored in the original 64 x 48 world and scaled with the stage bounds.
        // The first sixteen retain the legacy obstacle layout; the remaining twelve complete a symmetric scatter.
        private static readonly EnvironmentPlacement[] InteriorEnvironmentPlacements =
        {
            new(new Vector2(-25f, 18f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),
            new(new Vector2(-10f, 20f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),
            new(new Vector2(26f, 15f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),
            new(new Vector2(26f, -15f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),
            new(new Vector2(10f, -21f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),
            new(new Vector2(-27f, -16f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),
            new(new Vector2(-16f, 7f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),
            new(new Vector2(21f, -7f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),
            new(new Vector2(-7f, 13f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),
            new(new Vector2(7f, 15f), "tree_cluster", new Vector2(2.1f, 2.1f), new Vector2(1.3f, 1.0f)),

            new(new Vector2(-18f, 15f), "bush", new Vector2(1.35f, 1.35f), new Vector2(0.95f, 0.85f)),
            new(new Vector2(18f, 19f), "bush", new Vector2(1.35f, 1.35f), new Vector2(0.95f, 0.85f)),
            new(new Vector2(29f, 7f), "bush", new Vector2(1.35f, 1.35f), new Vector2(0.95f, 0.85f)),
            new(new Vector2(20f, -20f), "bush", new Vector2(1.35f, 1.35f), new Vector2(0.95f, 0.85f)),
            new(new Vector2(-16f, -19f), "bush", new Vector2(1.35f, 1.35f), new Vector2(0.95f, 0.85f)),
            new(new Vector2(-20f, -6f), "bush", new Vector2(1.35f, 1.35f), new Vector2(0.95f, 0.85f)),
            new(new Vector2(-4f, -15f), "bush", new Vector2(1.35f, 1.35f), new Vector2(0.95f, 0.85f)),
            new(new Vector2(4f, -12f), "bush", new Vector2(1.35f, 1.35f), new Vector2(0.95f, 0.85f)),

            new(new Vector2(-29f, -5f), "rock", new Vector2(1.2f, 1.2f), new Vector2(0.9f, 0.75f)),
            new(new Vector2(18f, 8f), "rock", new Vector2(1.2f, 1.2f), new Vector2(0.9f, 0.75f)),
            new(new Vector2(-8f, 2f), "rock", new Vector2(1.2f, 1.2f), new Vector2(0.9f, 0.75f)),
            new(new Vector2(8f, 1f), "rock", new Vector2(1.2f, 1.2f), new Vector2(0.9f, 0.75f)),
            new(new Vector2(29f, -10f), "rock", new Vector2(1.2f, 1.2f), new Vector2(0.9f, 0.75f)),
            new(new Vector2(-29f, 10f), "rock", new Vector2(1.2f, 1.2f), new Vector2(0.9f, 0.75f)),

            new(new Vector2(0f, 20f), "tree_stump", new Vector2(1.1f, 1.1f), new Vector2(0.8f, 0.75f)),
            new(new Vector2(0f, -20f), "tree_stump", new Vector2(1.1f, 1.1f), new Vector2(0.8f, 0.75f)),
            new(new Vector2(-24f, 0f), "tree_stump", new Vector2(1.1f, 1.1f), new Vector2(0.8f, 0.75f)),
            new(new Vector2(24f, 0f), "tree_stump", new Vector2(1.1f, 1.1f), new Vector2(0.8f, 0.75f))
        };

        private static readonly EnvironmentPlacement[] DryHighlandEnvironmentPlacements =
        {
            new(new Vector2(-25f, 17f), "rock", new Vector2(1.35f, 1.15f), new Vector2(0.9f, 0.75f)),
            new(new Vector2(-16f, -15f), "rock", new Vector2(1.35f, 1.15f), new Vector2(0.9f, 0.75f)),
            new(new Vector2(12f, 18f), "rock", new Vector2(1.35f, 1.15f), new Vector2(0.9f, 0.75f)),
            new(new Vector2(20f, -13f), "rock", new Vector2(1.35f, 1.15f), new Vector2(0.9f, 0.75f)),
            new(new Vector2(-7f, 13f), "tree_stump", new Vector2(1.1f, 1.1f), new Vector2(0.8f, 0.75f)),
            new(new Vector2(7f, -12f), "tree_stump", new Vector2(1.1f, 1.1f), new Vector2(0.8f, 0.75f)),
            new(new Vector2(-23f, 1f), "tree_stump", new Vector2(1.1f, 1.1f), new Vector2(0.8f, 0.75f)),
            new(new Vector2(15f, 4f), "tree_stump", new Vector2(1.1f, 1.1f), new Vector2(0.8f, 0.75f))
        };

        private readonly List<EnemyRuntime> enemies = new(512);
        private readonly List<ProjectileRuntime> projectiles = new(512);
        private readonly List<CrystalRuntime> crystals = new(256);
        private readonly List<GroundZoneRuntime> zones = new(32);
        private readonly List<MapEventRuntime> mapEvents = new(64);
        private readonly List<OrbitVisualRuntime> knifeOrbits = new(8);
        private readonly List<OrbitVisualRuntime> droneOrbits = new(8);
        private FuboQinAuraRuntime fuboQinAura;
        private readonly List<Rect> obstacles = new(32);
        private readonly List<Rect> waterObstacles = new(16);
        private readonly List<EnemyRuntime> queryResults = new(64);
        private readonly List<PickupNoticeRuntime> pickupNotices = new(16);
        private readonly Queue<WaveSpawnPlanItem> waveSpawnQueue = new();
        private readonly Dictionary<string, OwnedUpgrade> upgrades = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> weaponCooldowns = new(StringComparer.Ordinal);
        private readonly int[] footballSectorCounts = new int[CapsuleFootballTargeting.SectorCount];
        private readonly float[] footballSectorNearestDistances = new float[CapsuleFootballTargeting.SectorCount];
        private const float InstantNoticeDuration = 5f;
        private int noticeSerial;

        private GameSession session;
        private GameStateMachine stateMachine;
        private IInputSource input;
        private IRandomSource random;
        private StageConfig stage;
        private CharacterConfig character;
        private SkinConfig skin;
        private PlayerRuntime player;
        private ExperienceProgress experience;
        private SpatialHashGrid<EnemyRuntime> enemyGrid;
        private SkillFxService skillFx;
        private RuntimePool enemyPool;
        private RuntimePool projectilePool;
        private RuntimePool crystalPool;
        private RuntimePool zonePool;
        private RuntimePool eventPool;
        private RuntimePool knifePool;
        private RuntimePool dronePool;
        private Camera battleCamera;
        private Vector2 mapHalfSize;
        private float accumulator;
        private float elapsed;
        private float waveSpawnAccumulator;
        private float waveIntermissionRemaining;
        private float hudTimer;
        private float fpsSmoothing = 60f;
        private float simulationSpeed = 1f;
        private float knifeTick;
        private float poisonTick;
        private float mapEventRefreshTimer;
        private float cameraShakeRemaining;
        private float cameraShakeStrength;
        private float bossAttackCooldown = 5f;
        private float bossIntroRemaining;
        private float telegraphRemaining;
        private MapEventRuntime activeTelegraph;
        private bool initialized;
        private bool ended;
        private bool allWavesCleared;
        private bool finalWaveBossStarted;
        private bool bossKilled;
        private bool invincible;
        private bool levelRefreshAvailable = true;
        private int tickIndex;
        private int killCount;
        private float totalDamage;
        private float maxSingleDamage;
        private int projectileActive;
        private int crystalActive;
        private int zoneActive;
        private int currentWave;
        private int waveCount;

        public event Action<BattleSnapshot> SnapshotChanged;
        public event Action<IReadOnlyList<UpgradeOffer>, bool> LevelUpRequested;
        public event Action<GameState> StateChanged;
        public event Action<GameResultStats> BattleEnded;
        public event Action<string> ToastRequested;
        public event Action<string> AudioRequested;
        public event Action<Vector2, float, bool> DamageNumberRequested;
        public event Action UpgradesChanged;

        public GameState State => stateMachine?.Current ?? GameState.Boot;
        public bool IsInitialized => initialized;
        public int CurrentWave => currentWave;
        public int TotalWaveCount => waveCount;
        public IReadOnlyDictionary<string, OwnedUpgrade> OwnedUpgrades => upgrades;
        public bool IsCrateGuideActive => player != null && player.CrateGuideRemaining > 0f;
        public IReadOnlyList<Vector2> ActiveSupplyCratePositions => supplyCratePositions;
        public float ScooterRemaining => player?.ScooterRemaining ?? 0f;
        public float SniperRemaining => player?.SniperRemaining ?? 0f;
        public float CrateGuideRemaining => player?.CrateGuideRemaining ?? 0f;
        public float CapsuleFootballRemaining => player?.CapsuleFootballRemaining ?? 0f;

        private readonly List<Vector2> supplyCratePositions = new(30);

        public Vector3 WorldToViewport(Vector2 position) => battleCamera != null
            ? battleCamera.WorldToViewportPoint(position) : new Vector3(-1f, -1f, -1f);

        public void Initialize(GameSession gameSession, GameStateMachine machine, IInputSource inputSource, Camera camera)
        {
            if (initialized)
            {
                return;
            }

            session = gameSession ?? throw new ArgumentNullException(nameof(gameSession));
            stateMachine = machine ?? throw new ArgumentNullException(nameof(machine));
            input = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            battleCamera = camera != null ? camera : Camera.main;
            random = new UnityRandomSource(Environment.TickCount);
            stage = session.Config.Stages.Stages.FirstOrDefault(value => value.Id == session.Launch.StageId)
                    ?? session.Config.Stages.Stages[0];
            EnsureCapsuleFootballHiddenCrateEffect(stage);
            character = session.Config.Characters.Characters.FirstOrDefault(value => value.Id == session.Launch.CharacterId)
                        ?? session.Config.Characters.Characters[0];
            skin = session.Config.Skins.Skins.FirstOrDefault(value => value.Id == session.Launch.SkinId)
                   ?? session.Config.Skins.Skins.First(value => value.CharacterId == character.Id);
            waveCount = WaveRules.ResolveWaveCount(session.Launch.Mode, session.Settings.WaveCount);
            experience = new ExperienceProgress(session.Config.Balance.Experience.LevelThresholds);
            enemyGrid = new SpatialHashGrid<EnemyRuntime>(
                WorldScale.ToUnits(session.Config.Balance.Performance.SpatialHashCellSize));

            CreatePools();
            skillFx = new SkillFxService();
            skillFx.Initialize(transform);
            skillFx.SetQuality(session.Settings.ParticleQuality);
            CreateMap();
            CreatePlayer();
            SpawnMapEvents();
            AddUpgrade(character.StartingWeaponId, UpgradeKind.Weapon, 1);
            input.Enable();
            stateMachine.Changed += OnStateChanged;
            initialized = true;
            stateMachine.Set(GameState.Playing);
            BeginWave(1);
            PublishSnapshot();
        }

        private void Update()
        {
            if (!initialized || ended)
            {
                return;
            }

            fpsSmoothing = Mathf.Lerp(fpsSmoothing, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.08f);
            if (input.PausePressed)
            {
                TogglePause();
            }

            if (stateMachine.Current == GameState.BossIntro)
            {
                bossIntroRemaining -= Time.unscaledDeltaTime;
                if (bossIntroRemaining <= 0f)
                {
                    stateMachine.Set(GameState.Playing);
                }
                return;
            }

            if (stateMachine.Current != GameState.Playing)
            {
                return;
            }

            accumulator += Time.unscaledDeltaTime * simulationSpeed;
            var steps = 0;
            while (accumulator >= FixedStep && steps < MaxStepsPerFrame)
            {
                TickSimulation(FixedStep);
                accumulator -= FixedStep;
                steps++;
            }
            if (steps == MaxStepsPerFrame)
            {
                accumulator = 0f;
            }
        }

        private void LateUpdate()
        {
            if (!initialized || player?.View == null || battleCamera == null)
            {
                return;
            }
            var target = new Vector3(player.Position.x, player.Position.y, -10f);
            if (session.Settings.ScreenShake && cameraShakeRemaining > 0f)
            {
                cameraShakeRemaining = Mathf.Max(0f, cameraShakeRemaining - Time.unscaledDeltaTime);
                var strength = cameraShakeStrength * Mathf.Clamp01(cameraShakeRemaining / 0.18f);
                var offset = UnityEngine.Random.insideUnitCircle * strength;
                target += new Vector3(offset.x, offset.y, 0f);
            }
            battleCamera.transform.position = Vector3.Lerp(
                battleCamera.transform.position, target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        }

        public void TogglePause()
        {
            if (!initialized || ended || stateMachine.Current == GameState.LevelUp ||
                stateMachine.Current == GameState.BossIntro)
            {
                return;
            }
            stateMachine.Set(stateMachine.Current == GameState.Paused ? GameState.Playing : GameState.Paused);
        }

        public void ApplyUpgrade(UpgradeOffer offer)
        {
            if (offer == null || stateMachine.Current != GameState.LevelUp)
            {
                return;
            }
            AddUpgrade(offer.Id, offer.Kind, 1);
            AudioRequested?.Invoke("upgrade_select");
            FinishLevelUp();
        }

        public void SkipUpgrade()
        {
            if (stateMachine.Current == GameState.LevelUp)
            {
                FinishLevelUp();
            }
        }

        public void RefreshUpgradeOffers()
        {
            if (stateMachine.Current != GameState.LevelUp || !levelRefreshAvailable)
            {
                return;
            }
            levelRefreshAvailable = false;
            LevelUpRequested?.Invoke(RollUpgradeOffers(), false);
        }

        public void DebugToggleInvincible()
        {
            invincible = !invincible;
            ToastRequested?.Invoke(invincible ? "无敌：开" : "无敌：关");
        }

        internal static void EnsureCapsuleFootballHiddenCrateEffect(StageConfig targetStage)
        {
            if (targetStage == null) return;
            targetStage.MapEvents ??= new MapEventsConfig();
            targetStage.MapEvents.HiddenCrateEffects ??= new List<CrateEffectConfig>();
            if (targetStage.MapEvents.HiddenCrateEffects.Any(effect => effect?.Id == "capsule_football")) return;
            targetStage.MapEvents.HiddenCrateEffects.Add(new CrateEffectConfig
            {
                Id = "capsule_football",
                Weight = 1f,
                Duration = 30f
            });
        }

        public void DebugAddExperience()
        {
            experience.Add(100);
            TryOpenLevelUp();
        }

        public void DebugSpawnElite() => SpawnEnemy("zombie_elite", 1f, false);
        public void DebugSpawnBoss() => SpawnBoss();

        internal Vector2 DebugPlayerPosition => player?.Position ?? Vector2.zero;

        internal EnemyRuntime DebugSpawnEnemyAt(string enemyId, Vector2 position, bool forceBoss = false)
        {
            if (!initialized) return null;
            SpawnEnemy(enemyId, 1f, forceBoss);
            if (enemies.Count == 0) return null;
            var enemy = enemies[^1];
            enemy.Position = ResolveMapCollision(position, enemy.Radius);
            enemy.View.transform.position = enemy.Position;
            RuntimeSpriteFactory.UpdateDepth(enemy.View, enemy.Position.y);
            RebuildEnemyGrid();
            return enemy;
        }

        internal void DebugDisableWeaponsForTests()
        {
            upgrades.Clear();
            weaponCooldowns.Clear();
            EnsureOrbitCount(knifeOrbits, knifePool, 0, true);
            EnsureOrbitCount(droneOrbits, dronePool, 0, false);
        }

        public void DebugClearEnemies()
        {
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                ReleaseEnemy(enemies[i]);
            }
            enemies.Clear();
        }

        public void DebugCycleSpeed()
        {
            simulationSpeed = simulationSpeed >= 4f ? 1f : simulationSpeed * 2f;
            ToastRequested?.Invoke($"时倍 x{simulationSpeed:0}");
        }

        public void DebugAddRandomWeapon()
        {
            var candidates = session.Config.Weapons.Weapons
                .Where(config => !upgrades.TryGetValue(config.Id, out var owned) || owned.Level < config.MaxLevel)
                .ToList();
            if (candidates.Count == 0) return;
            AddUpgrade(candidates[random.Range(0, candidates.Count)].Id, UpgradeKind.Weapon, 1);
        }

        public void DebugGrantMaxLevelWeapon(string weaponId)
        {
            if (!initialized || string.IsNullOrWhiteSpace(weaponId)) return;
            var config = session.Config.Weapons.Weapons.FirstOrDefault(value => value.Id == weaponId);
            if (config == null) return;
            if (!upgrades.TryGetValue(weaponId, out _))
                AddUpgrade(weaponId, UpgradeKind.Weapon, 1);
            var owned = upgrades[weaponId];
            var levelsToAdd = config.MaxLevel - owned.Level;
            if (levelsToAdd > 0)
                AddUpgrade(weaponId, UpgradeKind.Weapon, levelsToAdd);
            var displayName = WeaponArtCatalog.ResolveDisplayName(
                config.Name, config.Promotion, config.MaxLevel, config.MaxLevel);
            ToastRequested?.Invoke($"{displayName} 已满级");
            PublishSnapshot();
        }

        public void DebugSpawnCrystal()
        {
            if (initialized) SpawnCrystal(player.Position + Vector2.right * 0.8f, 1);
        }

        public void DebugGrantTemporaryItem(string itemId)
        {
            if (!initialized || string.IsNullOrWhiteSpace(itemId)) return;
            ApplyCrateEffect(new CrateEffectConfig { Id = itemId, MoveSpeedBonus = 0.5f });
            PublishSnapshot();
        }

        public void DebugHealFull()
        {
            if (!initialized || player == null) return;
            player.Hp = player.MaxHp;
            player.HitFlashRemaining = 0f;
            if (player.View != null && player.View.TryGetComponent<SpriteRenderer>(out var renderer))
                renderer.color = Color.white;
            ToastRequested?.Invoke("生命已回满");
            PublishSnapshot();
        }

        private void TickSimulation(float dt)
        {
            tickIndex++;
            elapsed += dt;
            skillFx?.Tick(dt);
            TickTemporaryBonuses(dt);
            TickPickupNotices(dt);
            TickPlayer(dt);
            TickWaveSpawning(dt);
            if (stateMachine.Current != GameState.Playing) return;
            TickEnemies(dt);
            RebuildEnemyGrid();
            TickCapsuleFootball(dt);
            TickWeapons(dt);
            TickOrbitVisuals();
            TickProjectiles(dt);
            TickZones(dt);
            TickCrystals(dt);
            TickMapEvents(dt);
            TickBoss(dt);
            if (player.Hp <= 0f)
            {
                FinishBattle(false);
                return;
            }
            EvaluateWaveCompletion();
            if (ended) return;
            TryOpenLevelUp();

            hudTimer -= dt;
            if (hudTimer <= 0f)
            {
                hudTimer = 0.1f;
                PublishSnapshot();
            }

        }

        private void TickPlayer(float dt)
        {
            player.Invulnerability = Mathf.Max(0f, player.Invulnerability - dt);
            player.HitFlashRemaining = Mathf.Max(0f, player.HitFlashRemaining - dt);
            var axis = Vector2.ClampMagnitude(input.Move, 1f);
            var scooterMultiplier = player.ScooterRemaining > 0f ? 1.5f : 1f;
            var targetVelocity = axis * player.MoveSpeed * player.TemporaryMoveMultiplier * scooterMultiplier;
            var acceleration = WorldScale.ToUnits(session.Config.Balance.Player.Acceleration);
            var deceleration = WorldScale.ToUnits(session.Config.Balance.Player.Deceleration);
            player.Velocity = Vector2.MoveTowards(player.Velocity, targetVelocity,
                (axis.sqrMagnitude > 0.001f ? acceleration : deceleration) * dt);
            var next = ResolveMapCollision(player.Position + player.Velocity * dt, player.CollisionRadius);
            player.Position = next;
            player.View.transform.position = next;
            RuntimeSpriteFactory.UpdateDepth(player.View, next.y, 10);
            if (player.View.TryGetComponent<SpriteRenderer>(out var renderer))
            {
                if (axis.x != 0f) renderer.flipX = axis.x < 0f;
                UpdatePlayerHitFlash(renderer);
            }
            UpdateTemporaryItemVisuals(axis.x);
        }

        private void UpdatePlayerHitFlash(SpriteRenderer renderer)
        {
            if (player.HitFlashRemaining > 0f || player.Invulnerability > 0f)
            {
                var timer = player.HitFlashRemaining > 0f ? player.HitFlashRemaining : player.Invulnerability;
                var pulse = Mathf.PingPong(timer * 14f, 1f);
                renderer.color = Color.Lerp(Color.white, new Color(1f, 0.2f, 0.2f, 1f), 0.4f + pulse * 0.6f);
            }
            else
            {
                renderer.color = Color.white;
            }
        }

        private void TickWaveSpawning(float dt)
        {
            if (allWavesCleared) return;
            if (waveIntermissionRemaining > 0f)
            {
                waveIntermissionRemaining -= dt;
                if (waveIntermissionRemaining <= 0f)
                {
                    BeginWave(currentWave + 1);
                }
                return;
            }

            var cap = Mathf.Min(stage.MaxEnemies, session.Settings.MaxEnemyDisplay);
            if (waveSpawnQueue.Count == 0 || enemies.Count >= cap) return;

            waveSpawnAccumulator += WaveRules.SpawnRateForWave(currentWave) * dt;
            while (waveSpawnAccumulator >= 1f && waveSpawnQueue.Count > 0 && enemies.Count < cap)
            {
                var item = waveSpawnQueue.Dequeue();
                SpawnEnemy(item.EnemyId, item.DifficultyMultiplier, false);
                waveSpawnAccumulator -= 1f;
            }
        }

        private void BeginWave(int wave)
        {
            currentWave = Mathf.Clamp(wave, 1, waveCount);
            waveSpawnQueue.Clear();
            var plan = WaveRules.PlanWave(currentWave, session.Settings.FirstWaveMobCount,
                session.Settings.WaveMobCountMultiplier, WaveRules.Growth,
                Mathf.Min(stage.MaxEnemies, session.Settings.MaxEnemyDisplay), random,
                WaveSpecialSpawnRules.FromSettings(session.Settings));
            foreach (var item in plan) waveSpawnQueue.Enqueue(item);
            waveSpawnAccumulator = 0f;
            waveIntermissionRemaining = 0f;
            ToastRequested?.Invoke($"第 {currentWave} / {waveCount} 波");

            if (currentWave >= waveCount && session.Settings.BossCount > 0)
            {
                SpawnFinalWaveBosses();
            }
        }

        private void EvaluateWaveCompletion()
        {
            if (allWavesCleared || waveIntermissionRemaining > 0f || waveSpawnQueue.Count > 0 ||
                enemies.Any(enemy => enemy.Active && !enemy.IsBoss))
                return;

            if (currentWave < waveCount)
            {
                waveIntermissionRemaining = WaveRules.IntermissionSeconds;
                ToastRequested?.Invoke($"第 {currentWave} 波清除");
                return;
            }

            allWavesCleared = true;
            ToastRequested?.Invoke("全部波次已清除！");
            if (session.Settings.BossCount <= 0)
            {
                FinishBattle(true);
                return;
            }

            if (!finalWaveBossStarted)
            {
                SpawnFinalWaveBosses();
                return;
            }

            if (!enemies.Any(enemy => enemy.Active && enemy.IsBoss))
            {
                bossKilled = true;
                FinishBattle(true);
            }
        }

        private void TickEnemies(float dt)
        {
            var offscreenInterval = Mathf.Max(1, session.Config.Balance.Performance.OffscreenUpdateInterval);
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (!enemy.Active) continue;
                enemy.ContactCooldown = Mathf.Max(0f, enemy.ContactCooldown - dt);
                enemy.DashCooldown -= dt;
                enemy.StunRemaining = Mathf.Max(0f, enemy.StunRemaining - dt);

                if (TickEnemyKnockback(enemy, dt))
                {
                    enemy.View.transform.position = enemy.Position;
                    RuntimeSpriteFactory.UpdateDepth(enemy.View, enemy.Position.y);
                    UpdateEnemyHpBar(enemy);
                    continue;
                }

                var viewport = battleCamera != null
                    ? battleCamera.WorldToViewportPoint(enemy.Position)
                    : new Vector3(0.5f, 0.5f, 0f);
                var offscreen = viewport.x < -0.2f || viewport.x > 1.2f || viewport.y < -0.2f || viewport.y > 1.2f;
                if (offscreen && (tickIndex + i) % offscreenInterval != 0)
                {
                    continue;
                }

                if (enemy.StunRemaining > 0f)
                {
                    enemy.DashRemaining = 0f;
                    enemy.View.transform.position = enemy.Position;
                    RuntimeSpriteFactory.UpdateDepth(enemy.View, enemy.Position.y);
                    if (enemy.View.TryGetComponent<SpriteRenderer>(out var stunRenderer))
                    {
                        var pulse = 0.55f + 0.2f * Mathf.Sin(elapsed * 8f);
                        stunRenderer.color = new Color(0.55f, 0.72f, 1f, 1f) * pulse +
                                             Color.white * (1f - pulse);
                    }
                    UpdateEnemyHpBar(enemy);
                    continue;
                }

                if (enemy.View.TryGetComponent<SpriteRenderer>(out var idleRenderer) &&
                    idleRenderer.color != Color.white)
                {
                    idleRenderer.color = Color.white;
                }

                var toPlayer = player.Position - enemy.Position;
                var direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
                if (enemy.Config.Type == "elite" && enemy.DashCooldown <= 0f && toPlayer.magnitude < 4.5f)
                {
                    enemy.DashCooldown = Mathf.Max(1f, enemy.Config.DashCooldown);
                    enemy.DashRemaining = enemy.Config.DashDuration;
                    enemy.DashDirection = direction;
                }

                float speed;
                if (enemy.DashRemaining > 0f)
                {
                    enemy.DashRemaining -= dt;
                    speed = WorldScale.ToUnits(enemy.Config.DashSpeed);
                    direction = enemy.DashDirection;
                }
                else
                {
                    speed = WorldScale.ToUnits(enemy.Config.MoveSpeed);
                }
                if (player.EnemySlowRemaining > 0f)
                {
                    var slowPercent = Mathf.Clamp(session.Settings.AnestheticCapsuleSlowPercent, 5f, 80f);
                    speed *= 1f - slowPercent * 0.01f;
                }
                enemy.Position = ResolveMapCollision(enemy.Position + direction * speed * dt, enemy.Radius);
                enemy.View.transform.position = enemy.Position;
                RuntimeSpriteFactory.UpdateDepth(enemy.View, enemy.Position.y);
                if (direction.x != 0f && enemy.View.TryGetComponent<SpriteRenderer>(out var renderer))
                {
                    renderer.flipX = direction.x < 0f;
                }
                UpdateEnemyHpBar(enemy);

                if (toPlayer.sqrMagnitude <= Mathf.Pow(enemy.Radius + player.CollisionRadius, 2f) &&
                    enemy.ContactCooldown <= 0f)
                {
                    enemy.ContactCooldown = Mathf.Max(0.2f, enemy.Config.AttackCooldown);
                    DamagePlayer(enemy.Config.ContactDamage * enemy.WeightMultiplier, false);
                }
            }
        }

        private bool TickEnemyKnockback(EnemyRuntime enemy, float dt)
        {
            if (enemy.KnockbackDurationRemaining <= 0f || enemy.KnockbackRemaining.sqrMagnitude <= 0.000001f)
            {
                enemy.KnockbackDurationRemaining = 0f;
                enemy.KnockbackRemaining = Vector2.zero;
                return false;
            }

            var ratio = Mathf.Clamp01(dt / enemy.KnockbackDurationRemaining);
            var step = enemy.KnockbackRemaining * ratio;
            enemy.Position = ResolveMapCollision(enemy.Position + step, enemy.Radius);
            enemy.KnockbackRemaining -= step;
            enemy.KnockbackDurationRemaining = Mathf.Max(0f, enemy.KnockbackDurationRemaining - dt);
            if (enemy.KnockbackDurationRemaining <= 0f)
                enemy.KnockbackRemaining = Vector2.zero;
            return true;
        }

        private void TickCapsuleFootball(float dt)
        {
            if (player.CapsuleFootballRemaining <= 0f) return;
            player.CapsuleFootballCooldown -= dt;
            if (player.CapsuleFootballCooldown > 0f) return;

            var target = CapsuleFootballTargeting.FindDensestTarget(enemies, player.Position,
                WorldScale.ToUnits(CapsuleFootballTargetRangePixels), footballSectorCounts,
                footballSectorNearestDistances);
            if (target == null)
            {
                player.CapsuleFootballCooldown = CapsuleFootballRetryInterval;
                return;
            }

            var origin = CapsuleFootballOrigin();
            player.CapsuleFootballFlashRemaining = CapsuleFootballFlashDuration;
            if (player.CapsuleFootballView != null)
            {
                player.CapsuleFootballView.transform.localScale = player.CapsuleFootballBaseScale * 1.14f;
                player.CapsuleFootballView.GetComponent<SpriteRenderer>().color = Color.white;
            }
            skillFx.CastPulse(origin, FxSpriteFactory.FromRgb(0xb8f4ff));
            SpawnCapsuleFootball(origin, target);
            player.CapsuleFootballCooldown = CapsuleFootballFireInterval;
        }

        private Vector2 CapsuleFootballOrigin()
        {
            SpriteRenderer renderer = null;
            if (player.View != null) player.View.TryGetComponent(out renderer);
            var facingLeft = renderer != null && renderer.flipX;
            var playerHeight = renderer != null
                ? renderer.bounds.size.y
                : 0.62f * character.Scale * ActorVisualScaleMultiplier;
            return player.Position + new Vector2(facingLeft ? -0.22f : 0.22f, -playerHeight * 0.05f);
        }

        private void SpawnCapsuleFootball(Vector2 origin, EnemyRuntime target)
        {
            var direction = (target.Position - origin).normalized;
            var view = projectilePool.Acquire();
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.sprite = WeaponArtCatalog.LoadBattle("capsule_football_ball") ?? RuntimeSpriteFactory.Circle;
            renderer.color = Color.white;
            RuntimeSpriteFactory.SetWorldSize(view, 0.34f, 0.34f);
            view.transform.position = origin;
            view.transform.rotation = Quaternion.identity;

            projectiles.Add(new ProjectileRuntime
            {
                View = view,
                Position = origin,
                Velocity = direction * WorldScale.ToUnits(CapsuleFootballSpeedPixels),
                Radius = WorldScale.ToUnits(15f),
                RemainingRange = WorldScale.ToUnits(CapsuleFootballTargetRangePixels),
                Damage = CapsuleFootballDamage,
                Penetration = CapsuleFootballMaxHits,
                Active = true,
                TrailColor = FxSpriteFactory.FromRgb(0x77e8ff),
                TrailSize = 4f,
                TrailTimer = 0f,
                Kind = ProjectileKind.CapsuleFootball,
                AngularVelocity = CapsuleFootballAngularVelocity,
                FootballTarget = target,
                FootballLifetime = CapsuleFootballLifetime
            });
            projectileActive++;
        }

        private void TickWeapons(float dt)
        {
            foreach (var owned in upgrades.Values)
            {
                if (owned.Kind != UpgradeKind.Weapon) continue;
                weaponCooldowns.TryGetValue(owned.Id, out var cooldown);
                cooldown -= dt;
                switch (owned.Id)
                {
                    case "wind_blade" when cooldown <= 0f:
                        cooldown = FireWindBlade(owned.Level);
                        break;
                    case "fire_bottle" when cooldown <= 0f:
                        cooldown = FireBottle(owned.Level);
                        break;
                    case "lightning_chain" when cooldown <= 0f:
                        cooldown = FireLightning(owned.Level);
                        break;
                    case "drone" when cooldown <= 0f:
                        cooldown = FireDrone(owned.Level);
                        break;
                }
                weaponCooldowns[owned.Id] = cooldown;
            }

            if (upgrades.TryGetValue("rotating_knife", out var knife))
            {
                knifeTick -= dt;
                if (knifeTick <= 0f)
                {
                    knifeTick = 0.24f / Mathf.Max(0.5f, player.AttackSpeedMultiplier);
                    TickRotatingKnife(knife.Level, knife.MaxLevel);
                }
            }

            if (upgrades.TryGetValue("fubo_qin", out var fubo))
                TickFuboQinAura(fubo.Level, fubo.MaxLevel, dt);
            else
                HideFuboQinAura();
        }

        private float FireWindBlade(int level)
        {
            var effect = WeaponEffect("wind_blade", level);
            var target = FindNearestEnemy(player.Position, 30f);
            if (target == null) return 0.15f;
            skillFx.CastPulse(player.Position + new Vector2(0f, FxSpriteFactory.Px(12f)),
                FxSpriteFactory.FromRgb(0xa8d8ff));
            var count = Mathf.Max(1, Mathf.RoundToInt(Get(effect, "projectileCount", 1f)));
            var direction = (target.Position - player.Position).normalized;
            var trail = FxSpriteFactory.FromRgb(0xa8d8ff);
            for (var i = 0; i < count; i++)
            {
                var angle = (i - (count - 1) * 0.5f) * 8f;
                SpawnProjectile(player.Position, Quaternion.Euler(0, 0, angle) * direction,
                    Get(effect, "projectileSpeed", 500f), Get(effect, "range", 500f),
                    Get(effect, "damage", 10f), Mathf.RoundToInt(Get(effect, "penetration", 1f)),
                    trail, 3f,                     WeaponArtCatalog.LoadBattle("wind_blade") ?? RuntimeSpriteFactory.White,
                    new Vector2(0.49f, 0.49f));
            }
            return Get(effect, "cooldown", 1f) / Mathf.Max(0.25f, player.AttackSpeedMultiplier);
        }

        private void TickRotatingKnife(int level, int maxLevel)
        {
            var knifeConfig = GetWeaponConfig("rotating_knife");
            var isPromoted = WeaponArtCatalog.IsPromoted(knifeConfig?.Promotion, level, maxLevel);
            var effect = WeaponEffect("rotating_knife", level);
            var radius = WorldScale.ToUnits(ResolveRotatingKnifeOrbitRadiusPixels(level, maxLevel));
            var size = WorldScale.ToUnits(Get(effect, "knifeSize", 12f));
            var canvasSize = size * 3f * (isPromoted ? 1.05f : 1f);
            var hitRadius = canvasSize * 0.5f + 0.35f;
            var damage = Get(effect, "damage", 8f);
            var count = Mathf.Max(1, Mathf.RoundToInt(Get(effect, "knifeCount", 1f)));
            var rotationSpeed = ResolveRotatingKnifeRotationSpeed(level);
            for (var i = 0; i < count; i++)
            {
                var angle = elapsed * rotationSpeed + i * Mathf.PI * 2f / count;
                var point = player.Position + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.72f);
                var enemy = FindNearestEnemy(point, hitRadius);
                if (enemy != null)
                {
                    ApplyEnemyDamage(enemy, damage);
                }
            }
        }

        private float ResolveRotatingKnifeRotationSpeed(int level)
        {
            var effect = WeaponEffect("rotating_knife", level);
            var baseSpeed = Get(effect, "rotationSpeed", 2f);
            return baseSpeed * Mathf.Max(0.25f, session.Settings.RotatingKnifeRotationSpeedMul);
        }

        private float ResolveRotatingKnifeOrbitRadiusPixels(int level, int maxLevel) =>
            WeaponRules.ResolveRotatingKnifeOrbitRadiusPixels(
                session.Settings.RotatingKnifeBaseOrbitRadius,
                session.Settings.RotatingKnifeMaxOrbitRadius,
                level,
                maxLevel);

        private void TickFuboQinAura(int level, int maxLevel, float dt)
        {
            if (player == null) return;
            var config = GetWeaponConfig("fubo_qin");
            var promotion = config?.Promotion;
            var effect = WeaponEffect("fubo_qin", level);
            var radiusPx = WeaponRules.ResolveFuboQinAuraRadiusPixels(
                session.Settings.FuboQinBaseAuraRadius,
                session.Settings.FuboQinMaxAuraRadius,
                level,
                maxLevel);
            var radius = WorldScale.ToUnits(radiusPx);
            var tickInterval = Mathf.Max(0.05f, Get(effect, "tickInterval", 0.35f));
            var pulseSpeed = Get(effect, "ripplePulseSpeed", 2.5f);
            var isPromoted = WeaponArtCatalog.IsPromoted(promotion, level, maxLevel);
            EnsureFuboQinAura();
            fuboQinAura.Active = true;
            fuboQinAura.Root.SetActive(true);
            fuboQinAura.TickTimer += dt;
            var center = player.Position;
            fuboQinAura.Root.transform.position = center;
            UpdateFuboQinAuraVisual(radius, pulseSpeed, isPromoted);
            if (fuboQinAura.TickTimer < tickInterval) return;
            fuboQinAura.TickTimer -= tickInterval;
            var damage = Get(effect, "damage", 5f);
            DamageEnemiesInRadius(center, radius, damage);
        }

        private void EnsureFuboQinAura()
        {
            if (fuboQinAura != null) return;
            var root = new GameObject("FuboQinAura");
            root.transform.SetParent(transform, false);
            var auraSprite = WeaponArtCatalog.LoadFuboQinAura(false) ?? FxSpriteFactory.RippleRing;
            var usePlaceholder = auraSprite == FxSpriteFactory.RippleRing;
            var innerColor = usePlaceholder
                ? new Color(0.45f, 0.92f, 0.58f, 0.32f)
                : new Color(1f, 1f, 1f, 0.32f);
            var outerColor = usePlaceholder
                ? new Color(0.45f, 0.92f, 0.58f, 0.18f)
                : new Color(1f, 1f, 1f, 0.18f);
            var inner = FxSpriteFactory.CreateSpriteView("FuboQinInner", auraSprite, innerColor, 4, true);
            inner.transform.SetParent(root.transform, false);
            var outer = FxSpriteFactory.CreateSpriteView("FuboQinOuter", auraSprite, outerColor, 3, true);
            outer.transform.SetParent(root.transform, false);
            fuboQinAura = new FuboQinAuraRuntime
            {
                Root = root,
                InnerRing = inner,
                OuterRing = outer,
                VisualSeed = random.Range(0f, Mathf.PI * 2f),
                Active = true,
                UsingGoldAura = false
            };
        }

        private void UpdateFuboQinAuraVisual(float radius, float pulseSpeed, bool isPromoted)
        {
            if (fuboQinAura == null) return;
            if (fuboQinAura.UsingGoldAura != isPromoted)
            {
                fuboQinAura.UsingGoldAura = isPromoted;
                var auraSprite = WeaponArtCatalog.LoadFuboQinAura(isPromoted) ?? FxSpriteFactory.RippleRing;
                fuboQinAura.InnerRing.GetComponent<SpriteRenderer>().sprite = auraSprite;
                fuboQinAura.OuterRing.GetComponent<SpriteRenderer>().sprite = auraSprite;
            }

            var sprite = fuboQinAura.InnerRing.GetComponent<SpriteRenderer>().sprite;
            var usePlaceholder = sprite == FxSpriteFactory.RippleRing;
            var innerColor = usePlaceholder
                ? isPromoted
                    ? new Color(0.95f, 0.86f, 0.42f, 0.38f)
                    : new Color(0.45f, 0.92f, 0.58f, 0.32f)
                : isPromoted
                    ? new Color(1f, 1f, 1f, 0.38f)
                    : new Color(1f, 1f, 1f, 0.32f);
            var outerColor = usePlaceholder
                ? isPromoted
                    ? new Color(0.95f, 0.86f, 0.42f, 0.22f)
                    : new Color(0.45f, 0.92f, 0.58f, 0.18f)
                : isPromoted
                    ? new Color(1f, 1f, 1f, 0.22f)
                    : new Color(1f, 1f, 1f, 0.18f);
            var pulse = 1f + Mathf.Sin(elapsed * pulseSpeed + fuboQinAura.VisualSeed) * 0.08f;
            var outerPulse = 1f + Mathf.Sin(elapsed * pulseSpeed * 0.85f + fuboQinAura.VisualSeed + 0.6f) * 0.12f;
            ConfigureAuraRing(fuboQinAura.InnerRing, radius * 1.55f * pulse, innerColor);
            ConfigureAuraRing(fuboQinAura.OuterRing, radius * 2.05f * outerPulse, outerColor);
            RuntimeSpriteFactory.UpdateDepth(fuboQinAura.Root, player.Position.y, 3);
        }

        private static void ConfigureAuraRing(GameObject ring, float diameter, Color color)
        {
            if (ring == null) return;
            var renderer = ring.GetComponent<SpriteRenderer>();
            renderer.color = color;
            RuntimeSpriteFactory.SetWorldSize(ring, diameter, diameter);
        }

        private void HideFuboQinAura()
        {
            if (fuboQinAura == null) return;
            fuboQinAura.Active = false;
            fuboQinAura.Root.SetActive(false);
            fuboQinAura.TickTimer = 0f;
        }

        private void DamageEnemiesInRadius(Vector2 center, float radius, float damage)
        {
            var radiusSq = radius * radius;
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (!enemy.Active) continue;
                if (Vector2.SqrMagnitude(enemy.Position - center) > radiusSq) continue;
                ApplyEnemyDamage(enemy, damage);
            }
        }

        private WeaponConfig GetWeaponConfig(string id) =>
            session.Config.Weapons.Weapons.FirstOrDefault(value => value.Id == id);

        private float FireBottle(int level)
        {
            var effect = WeaponEffect("fire_bottle", level);
            var target = FindNearestEnemy(player.Position, 20f);
            if (target == null) return 0.2f;
            skillFx.CastPulse(player.Position + new Vector2(0f, FxSpriteFactory.Px(12f)),
                FxSpriteFactory.FromRgb(0xff8844));
            var radiusPixels = Get(effect, "zoneRadius", 50f);
            SpawnFireBottle(player.Position + new Vector2(0f, FxSpriteFactory.Px(12f)), target.Position,
                radiusPixels, Get(effect, "damage", 6f), Get(effect, "zoneDuration", 3f),
                Get(effect, "tickInterval", 0.5f));
            AudioRequested?.Invoke("fire_bottle");
            return Get(effect, "cooldown", 3f) / Mathf.Max(0.25f, player.AttackSpeedMultiplier);
        }

        private float FireLightning(int level)
        {
            var effect = WeaponEffect("lightning_chain", level);
            var current = FindNearestEnemy(player.Position, 20f);
            if (current == null) return 0.2f;
            skillFx.CastPulse(player.Position + new Vector2(0f, FxSpriteFactory.Px(12f)),
                FxSpriteFactory.FromRgb(0x88ccff));
            var hit = new HashSet<EnemyRuntime>();
            var chainCount = Mathf.Max(1, Mathf.RoundToInt(Get(effect, "chainCount", 2f)));
            var range = WorldScale.ToUnits(Get(effect, "chainRange", 120f));
            var from = player.Position + new Vector2(0f, FxSpriteFactory.Px(8f));
            for (var i = 0; i < chainCount && current != null; i++)
            {
                hit.Add(current);
                skillFx.Lightning(from, current.Position + new Vector2(0f, FxSpriteFactory.Px(10f)));
                ApplyEnemyDamage(current, Get(effect, "damage", 15f));
                from = current.Position + new Vector2(0f, FxSpriteFactory.Px(10f));
                current = FindNearestEnemy(current.Position, range, hit);
            }
            AudioRequested?.Invoke("lightning_chain");
            return Get(effect, "cooldown", 2f) / Mathf.Max(0.25f, player.AttackSpeedMultiplier);
        }

        private float FireDrone(int level)
        {
            var effect = WeaponEffect("drone", level);
            var target = FindNearestEnemy(player.Position, 24f);
            if (target == null) return 0.2f;
            var count = Mathf.Max(1, Mathf.RoundToInt(Get(effect, "droneCount", 1f)));
            var radius = WorldScale.ToUnits(Get(effect, "orbitRadius", 199f));
            EnsureOrbitCount(droneOrbits, dronePool, count, false);
            var trail = FxSpriteFactory.FromRgb(0xffee88);
            for (var i = 0; i < count; i++)
            {
                var angle = elapsed + i * Mathf.PI * 2f / count;
                var origin = player.Position + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.7f);
                var bob = Mathf.Sin(elapsed * 6f + i) * FxSpriteFactory.Px(3f);
                origin.y += bob;
                var orbit = droneOrbits[i];
                orbit.Position = origin;
                orbit.Angle = angle;
                orbit.View.transform.position = origin;
                RuntimeSpriteFactory.UpdateDepth(orbit.View, origin.y, 16);
                var direction = (target.Position - origin).normalized;
                skillFx.DroneMuzzle(origin, Mathf.Atan2(direction.y, direction.x));
                SpawnProjectile(origin, direction,
                    Get(effect, "projectileSpeed", 400f), 500f,
                    Get(effect, "damage", 8f), 1, trail, 3f,
                    WeaponArtCatalog.LoadBattle("drone_bolt") ?? RuntimeSpriteFactory.White,
                    new Vector2(0.265f, 0.265f));
            }
            return 1f / Mathf.Max(0.1f, Get(effect, "fireRate", 1.5f) * player.AttackSpeedMultiplier);
        }

        private void TickOrbitVisuals()
        {
            if (upgrades.TryGetValue("rotating_knife", out var knife))
            {
                var knifeConfig = GetWeaponConfig(knife.Id);
                var promotion = knifeConfig?.Promotion;
                var effect = WeaponEffect("rotating_knife", knife.Level);
                var radius = WorldScale.ToUnits(ResolveRotatingKnifeOrbitRadiusPixels(knife.Level, knife.MaxLevel));
                var size = WorldScale.ToUnits(Get(effect, "knifeSize", 12f));
                var count = Mathf.Max(1, Mathf.RoundToInt(Get(effect, "knifeCount", 1f)));
                var rotationSpeed = ResolveRotatingKnifeRotationSpeed(knife.Level);
                var wheelKey = WeaponArtCatalog.ResolveBattleKey("rotating_knife", promotion, knife.Level, knife.MaxLevel);
                var wheelSprite = WeaponArtCatalog.LoadBattle(wheelKey) ?? FxSpriteFactory.Knife;
                var isPromoted = WeaponArtCatalog.IsPromoted(promotion, knife.Level, knife.MaxLevel);
                EnsureOrbitCount(knifeOrbits, knifePool, count, true);
                for (var i = 0; i < count; i++)
                {
                    var angle = elapsed * rotationSpeed + i * Mathf.PI * 2f / count;
                    var point = player.Position +
                                new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.72f);
                    var orbit = knifeOrbits[i];
                    orbit.Position = point;
                    orbit.Angle = angle;
                    orbit.View.transform.position = point;
                    if (isPromoted)
                    {
                        orbit.View.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
                    }
                    else
                    {
                        var spinAngle = elapsed * rotationSpeed * Mathf.Rad2Deg * 2.5f + i * (360f / count);
                        orbit.View.transform.rotation = Quaternion.Euler(0f, 0f, spinAngle);
                    }
                    orbit.View.GetComponent<SpriteRenderer>().sprite = wheelSprite;
                    var canvasSize = size * 3f * (isPromoted ? 1.05f : 1f);
                    RuntimeSpriteFactory.SetWorldSize(orbit.View, canvasSize, canvasSize);
                    UpdateRotatingKnifeGoldAura(orbit, isPromoted, canvasSize, i);
                    RuntimeSpriteFactory.UpdateDepth(orbit.View, point.y, 15);
                }
            }
            else
            {
                EnsureOrbitCount(knifeOrbits, knifePool, 0, true);
            }

            if (upgrades.TryGetValue("drone", out var drone))
            {
                var effect = WeaponEffect("drone", drone.Level);
                var radius = WorldScale.ToUnits(Get(effect, "orbitRadius", 199f));
                var count = Mathf.Max(1, Mathf.RoundToInt(Get(effect, "droneCount", 1f)));
                EnsureOrbitCount(droneOrbits, dronePool, count, false);
                for (var i = 0; i < count; i++)
                {
                    var angle = elapsed + i * Mathf.PI * 2f / count;
                    var origin = player.Position +
                                 new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.7f);
                    origin.y += Mathf.Sin(elapsed * 6f + i) * FxSpriteFactory.Px(3f);
                    var orbit = droneOrbits[i];
                    orbit.Position = origin;
                    orbit.Angle = angle;
                    orbit.View.transform.position = origin;
                    RuntimeSpriteFactory.SetWorldSize(orbit.View, FxSpriteFactory.Px(60f),
                        FxSpriteFactory.Px(60f));
                    RuntimeSpriteFactory.UpdateDepth(orbit.View, origin.y, 16);
                }
            }
            else
            {
                EnsureOrbitCount(droneOrbits, dronePool, 0, false);
            }
        }

        private void UpdateRotatingKnifeGoldAura(OrbitVisualRuntime orbit, bool isPromoted, float wheelSize, int index)
        {
            if (orbit.GoldAuraView == null || orbit.GoldAuraRenderer == null) return;

            orbit.GoldAuraView.SetActive(isPromoted);
            if (!isPromoted)
            {
                orbit.GoldAuraView.transform.localRotation = Quaternion.identity;
                return;
            }

            var pulse = 1.12f + Mathf.Sin(elapsed * 7.5f + index * 1.91f) * 0.05f;
            RuntimeSpriteFactory.SetWorldSize(orbit.GoldAuraView, wheelSize * pulse, wheelSize * pulse);
            orbit.GoldAuraRenderer.color = new Color(1f, 1f, 1f,
                0.88f + Mathf.Sin(elapsed * 6f + index * 1.37f) * 0.05f);
            orbit.GoldAuraView.transform.localRotation =
                Quaternion.Euler(0f, 0f, elapsed * 45f + index * 120f);
            RuntimeSpriteFactory.UpdateDepth(orbit.GoldAuraView, orbit.Position.y, 14);
        }

        private void EnsureOrbitCount(List<OrbitVisualRuntime> orbits, RuntimePool pool, int count, bool knife)
        {
            while (orbits.Count < count)
            {
                var view = pool.Acquire();
                if (knife)
                {
                    view.GetComponent<SpriteRenderer>().sprite =
                        WeaponArtCatalog.LoadBattle("rotating_knife") ?? FxSpriteFactory.Knife;
                    view.GetComponent<SpriteRenderer>().color = Color.white;
                    var aura = view.transform.Find("KnifeGoldAura")?.gameObject;
                    if (aura == null)
                    {
                        aura = FxSpriteFactory.CreateSpriteView("KnifeGoldAura",
                            WeaponArtCatalog.LoadRotatingKnifeGoldAura() ?? RuntimeSpriteFactory.Circle,
                            Color.white, 14, true);
                        aura.transform.SetParent(view.transform, false);
                    }
                    var auraRenderer = aura.GetComponent<SpriteRenderer>();
                    auraRenderer.sprite = WeaponArtCatalog.LoadRotatingKnifeGoldAura() ?? RuntimeSpriteFactory.Circle;
                    auraRenderer.color = Color.white;
                    aura.SetActive(false);
                    orbits.Add(new OrbitVisualRuntime
                    {
                        View = view,
                        GoldAuraView = aura,
                        GoldAuraRenderer = auraRenderer,
                        Active = true
                    });
                }
                else
                {
                    view.GetComponent<SpriteRenderer>().sprite =
                        WeaponArtCatalog.LoadBattle("drone") ?? FxSpriteFactory.Drone;
                    view.GetComponent<SpriteRenderer>().color = Color.white;
                    orbits.Add(new OrbitVisualRuntime { View = view, Active = true });
                }
            }

            while (orbits.Count > count)
            {
                var last = orbits[orbits.Count - 1];
                last.Active = false;
                pool.Release(last.View);
                orbits.RemoveAt(orbits.Count - 1);
            }
        }

        private void TickProjectiles(float dt)
        {
            for (var i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];
                if (!projectile.Active) continue;
                if (projectile.Kind == ProjectileKind.CapsuleFootball)
                {
                    if (TickCapsuleFootballProjectile(projectile, dt))
                    {
                        ReleaseProjectile(projectile);
                        projectiles.RemoveAt(i);
                    }
                    continue;
                }
                var distance = projectile.Velocity.magnitude * dt;
                if (projectile.Kind == ProjectileKind.FireBottle)
                    distance = Mathf.Min(distance, projectile.RemainingRange);
                projectile.Position += projectile.Velocity.normalized * distance;
                projectile.RemainingRange -= distance;
                projectile.View.transform.position = projectile.Position;
                if (Mathf.Abs(projectile.AngularVelocity) > 0.01f)
                    projectile.View.transform.Rotate(0f, 0f, projectile.AngularVelocity * dt);
                RuntimeSpriteFactory.UpdateDepth(projectile.View, projectile.Position.y, 20);
                projectile.TrailTimer -= dt;
                if (projectile.TrailTimer <= 0f)
                {
                    projectile.TrailTimer = 0.04f;
                    skillFx.ProjectileTrail(projectile.Position, projectile.TrailColor, projectile.TrailSize);
                }
                if (projectile.Kind == ProjectileKind.FireBottle)
                {
                    if (projectile.RemainingRange <= 0f)
                    {
                        skillFx.FireImpact(projectile.Position, WorldScale.ToUnits(projectile.ZoneRadiusPixels));
                        SpawnZone(projectile.Position, projectile.ZoneRadiusPixels, projectile.Damage,
                            projectile.ZoneDuration, projectile.ZoneTickInterval);
                        ReleaseProjectile(projectile);
                        projectiles.RemoveAt(i);
                    }
                    continue;
                }
                enemyGrid.Query(projectile.Position.x, projectile.Position.y, projectile.Radius + 0.7f, queryResults);
                foreach (var enemy in queryResults)
                {
                    if (!enemy.Active || Vector2.SqrMagnitude(enemy.Position - projectile.Position) >
                        Mathf.Pow(enemy.Radius + projectile.Radius, 2f)) continue;
                    ApplyEnemyDamage(enemy, projectile.Damage);
                    projectile.Penetration--;
                    if (projectile.Penetration <= 0) break;
                }
                if (projectile.RemainingRange <= 0f || projectile.Penetration <= 0)
                {
                    ReleaseProjectile(projectile);
                    projectiles.RemoveAt(i);
                }
            }
        }

        private bool TickCapsuleFootballProjectile(ProjectileRuntime projectile, float dt)
        {
            projectile.FootballLifetime -= dt;
            if (projectile.FootballLifetime <= 0f) return true;

            if (projectile.FootballTarget == null || !projectile.FootballTarget.Active ||
                projectile.HasHitWithFootball(projectile.FootballTarget))
            {
                projectile.FootballTarget = FindNearestFootballTarget(projectile.Position,
                    WorldScale.ToUnits(CapsuleFootballBounceRangePixels), projectile);
                if (projectile.FootballTarget == null) return true;
            }

            var target = projectile.FootballTarget;
            var delta = target.Position - projectile.Position;
            var direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : projectile.Velocity.normalized;
            var travel = WorldScale.ToUnits(CapsuleFootballSpeedPixels) * dt;
            var hitDistance = projectile.Radius + target.Radius;
            var hitsThisTick = delta.magnitude <= hitDistance + travel;
            projectile.Velocity = direction * WorldScale.ToUnits(CapsuleFootballSpeedPixels);
            projectile.Position = hitsThisTick
                ? target.Position
                : projectile.Position + direction * travel;
            projectile.View.transform.position = projectile.Position;
            projectile.View.transform.Rotate(0f, 0f, projectile.AngularVelocity * dt);
            RuntimeSpriteFactory.UpdateDepth(projectile.View, projectile.Position.y, 20);
            projectile.TrailTimer -= dt;
            if (projectile.TrailTimer <= 0f)
            {
                projectile.TrailTimer = 0.04f;
                skillFx.ProjectileTrail(projectile.Position, projectile.TrailColor, projectile.TrailSize);
            }

            if (!hitsThisTick) return false;

            var impactPosition = target.Position;
            projectile.RegisterFootballHit(target);
            ApplyEnemyDamage(target, projectile.Damage, false);
            skillFx.FootballImpact(impactPosition, direction);
            if (target.Active) ApplyCapsuleFootballKnockback(target, direction);
            if (projectile.FootballHitCount >= CapsuleFootballMaxHits) return true;

            projectile.FootballTarget = FindNearestFootballTarget(impactPosition,
                WorldScale.ToUnits(CapsuleFootballBounceRangePixels), projectile);
            if (projectile.FootballTarget == null) return true;
            projectile.Velocity = (projectile.FootballTarget.Position - impactPosition).normalized *
                                  WorldScale.ToUnits(CapsuleFootballSpeedPixels);
            return false;
        }

        private EnemyRuntime FindNearestFootballTarget(Vector2 position, float radius, ProjectileRuntime projectile)
        {
            EnemyRuntime best = null;
            var bestDistance = radius * radius;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!enemy.Active || projectile.HasHitWithFootball(enemy)) continue;
                var distance = Vector2.SqrMagnitude(enemy.Position - position);
                if (distance >= bestDistance) continue;
                best = enemy;
                bestDistance = distance;
            }
            return best;
        }

        internal static void ApplyCapsuleFootballKnockback(EnemyRuntime enemy, Vector2 direction)
        {
            if (enemy == null || !enemy.Active || enemy.IsBoss) return;
            enemy.DashRemaining = 0f;
            enemy.KnockbackRemaining = direction.normalized * WorldScale.ToUnits(CapsuleFootballKnockbackPixels);
            enemy.KnockbackDurationRemaining = CapsuleFootballKnockbackDuration;
        }

        private void TickZones(float dt)
        {
            for (var i = zones.Count - 1; i >= 0; i--)
            {
                var zone = zones[i];
                zone.Remaining -= dt;
                zone.Age += dt;
                zone.TickTimer -= dt;
                UpdateZoneVisual(zone);
                if (zone.TickTimer <= 0f)
                {
                    zone.TickTimer += zone.TickInterval;
                    enemyGrid.Query(zone.Position.x, zone.Position.y, zone.Radius, queryResults);
                    foreach (var enemy in queryResults)
                    {
                        if (enemy.Active && Vector2.SqrMagnitude(enemy.Position - zone.Position) <=
                            Mathf.Pow(zone.Radius + enemy.Radius, 2f))
                        {
                            ApplyEnemyDamage(enemy, zone.Damage);
                        }
                    }
                }
                if (zone.Remaining <= 0f)
                {
                    ReleaseZone(zone);
                    zones.RemoveAt(i);
                }
            }
        }

        private void TickCrystals(float dt)
        {
            var fullMapMagnet = player.MagnetBurstRemaining > 0f && player.MagnetBurstFullMap;
            var pickupRadius = fullMapMagnet
                ? mapHalfSize.magnitude * 2.5f
                : player.PickupRadius * Mathf.Max(1f, player.TemporaryPickupRadiusMul);
            for (var i = crystals.Count - 1; i >= 0; i--)
            {
                var crystal = crystals[i];
                var delta = player.Position - crystal.Position;
                var distance = delta.magnitude;
                if (fullMapMagnet || distance <= pickupRadius || crystal.Attracting)
                {
                    crystal.Attracting = true;
                    var speed = Mathf.Lerp(6f, 22f, 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, pickupRadius)));
                    if (player.MagnetBurstRemaining > 0f) speed *= 0.9f;
                    if (distance > 0.0001f)
                        crystal.Position += delta.normalized * speed * dt;
                    else
                        crystal.Position = player.Position;
                    crystal.View.transform.position = crystal.Position;
                    distance = Vector2.Distance(player.Position, crystal.Position);
                }
                if (distance <= player.CollisionRadius + 0.12f)
                {
                    experience.Add(Mathf.Max(1, Mathf.RoundToInt(crystal.Value * player.ExperienceMultiplier)));
                    crystal.Active = false;
                    crystal.Attracting = false;
                    crystalPool.Release(crystal.View);
                    crystalActive--;
                    crystals.RemoveAt(i);
                }
            }
        }

        private void TickMapEvents(float dt)
        {
            TickCrateRefresh(dt);
            poisonTick -= dt;
            foreach (var mapEvent in mapEvents)
            {
                if (!mapEvent.Active || mapEvent.Kind == MapEventKind.Telegraph) continue;
                UpdateMapEventVisual(mapEvent);
                var distance = Vector2.Distance(player.Position, mapEvent.Position);
                if ((mapEvent.Kind == MapEventKind.Crate || mapEvent.Kind == MapEventKind.HiddenCrate) &&
                    !mapEvent.Triggered && distance <= mapEvent.Radius)
                {
                    mapEvent.Triggered = true;
                    TriggerCrate(mapEvent);
                    mapEvent.Active = false;
                    if (mapEvent.Kind == MapEventKind.Crate) supplyCratePositions.Remove(mapEvent.Position);
                    eventPool.Release(mapEvent.View);
                }
                else if (mapEvent.Kind == MapEventKind.Altar && !mapEvent.Triggered && distance <= mapEvent.Radius)
                {
                    mapEvent.Triggered = true;
                    TriggerAltar(mapEvent);
                    mapEvent.Active = false;
                    eventPool.Release(mapEvent.View);
                }
                else if (mapEvent.Kind == MapEventKind.HealingChicken && !mapEvent.Triggered && distance <= mapEvent.Radius)
                {
                    mapEvent.Triggered = true;
                    CollectHealingChicken();
                    mapEvent.Active = false;
                    eventPool.Release(mapEvent.View);
                }
                else if (mapEvent.Kind == MapEventKind.PoisonFog && distance <= mapEvent.Radius && poisonTick <= 0f)
                {
                    DamagePlayer(player.MaxHp * session.Settings.PoisonFogDps * 0.01f * 0.4f, true);
                }
            }
            if (poisonTick <= 0f) poisonTick = 0.4f;
        }

        private void TickCrateRefresh(float dt)
        {
            if (session.Settings.CrateRefreshChance <= 0 &&
                session.Settings.HiddenCrateRefreshChance <= 0 &&
                session.Settings.AltarRefreshChance <= 0 &&
                session.Settings.HealingChickenRefreshChance <= 0)
                return;

            mapEventRefreshTimer += dt;
            if (mapEventRefreshTimer < CrateRefreshInterval)
                return;

            mapEventRefreshTimer -= CrateRefreshInterval;
            var config = stage.MapEvents;
            if (CountActiveMapEvents(MapEventKind.HiddenCrate) < session.Settings.HiddenCrateCount &&
                random.Value * 100f < session.Settings.HiddenCrateRefreshChance)
            {
                SpawnMapEvent(MapEventKind.HiddenCrate, RandomMapPosition(),
                    WorldScale.ToUnits(config.CrateInteractRadius), new Color(0.9f, 0.65f, 0.18f, 0.08f));
            }

            if (CountActiveMapEvents(MapEventKind.Crate) < session.Settings.CrateCount &&
                random.Value * 100f < session.Settings.CrateRefreshChance)
            {
                SpawnMapEvent(MapEventKind.Crate, RandomMapPosition(),
                    WorldScale.ToUnits(config.CrateInteractRadius), new Color(0.9f, 0.65f, 0.18f));
            }

            if (CountActiveMapEvents(MapEventKind.Altar) < session.Settings.AltarCount &&
                random.Value * 100f < session.Settings.AltarRefreshChance)
            {
                SpawnMapEvent(MapEventKind.Altar, RandomMapPosition(),
                    WorldScale.ToUnits(config.AltarInteractRadius), new Color(0.58f, 0.25f, 0.8f));
            }

            if (CountActiveMapEvents(MapEventKind.HealingChicken) < session.Settings.HealingChickenCount &&
                random.Value * 100f < session.Settings.HealingChickenRefreshChance)
            {
                SpawnMapEvent(MapEventKind.HealingChicken, RandomMapPosition(),
                    WorldScale.ToUnits(Mathf.Max(16f, config.HealingChickenInteractRadius)), Color.white);
            }
        }

        private int CountActiveMapEvents(MapEventKind kind)
        {
            var count = 0;
            foreach (var mapEvent in mapEvents)
                if (mapEvent.Active && mapEvent.Kind == kind)
                    count++;
            return count;
        }

        private void TickBoss(float dt)
        {
            var boss = enemies.FirstOrDefault(enemy => enemy.Active && enemy.IsBoss);
            if (boss == null) return;
            bossAttackCooldown -= dt;
            if (bossAttackCooldown <= 0f && activeTelegraph == null)
            {
                bossAttackCooldown = 5.5f;
                activeTelegraph = SpawnMapEvent(MapEventKind.Telegraph, player.Position, 1.5f,
                    new Color(1f, 0.1f, 0.05f, 0.32f));
                telegraphRemaining = 1.15f;
            }
            if (activeTelegraph != null)
            {
                telegraphRemaining -= dt;
                if (telegraphRemaining <= 0f)
                {
                    if (Vector2.Distance(player.Position, activeTelegraph.Position) <= activeTelegraph.Radius)
                    {
                        DamagePlayer(boss.Config.ContactDamage * 1.5f, false);
                    }
                    activeTelegraph.Active = false;
                    eventPool.Release(activeTelegraph.View);
                    mapEvents.Remove(activeTelegraph);
                    activeTelegraph = null;
                }
            }
        }

        private void TickTemporaryBonuses(float dt)
        {
            // Timed bonuses are represented as smooth expiry back to their baseline.
            player.TemporaryMoveMultiplier = Mathf.MoveTowards(player.TemporaryMoveMultiplier, 1f, dt * 0.08f);
            player.BloodPactRemaining = Mathf.Max(0f, player.BloodPactRemaining - dt);
            if (player.BloodPactRemaining <= 0f)
                player.TemporaryDamageBonus = Mathf.MoveTowards(player.TemporaryDamageBonus, 0f, dt * 0.01f);
            player.ScooterRemaining = Mathf.Max(0f, player.ScooterRemaining - dt);
            player.SniperRemaining = Mathf.Max(0f, player.SniperRemaining - dt);
            player.CrateGuideRemaining = Mathf.Max(0f, player.CrateGuideRemaining - dt);
            player.CapsuleFootballRemaining = Mathf.Max(0f, player.CapsuleFootballRemaining - dt);
            player.CapsuleFootballFlashRemaining = Mathf.Max(0f, player.CapsuleFootballFlashRemaining - dt);
            player.EnemySlowRemaining = Mathf.Max(0f, player.EnemySlowRemaining - dt);
            player.MagnetBurstRemaining = Mathf.Max(0f, player.MagnetBurstRemaining - dt);
            if (player.MagnetBurstRemaining <= 0f)
            {
                player.TemporaryPickupRadiusMul = 1f;
                player.MagnetBurstFullMap = false;
            }
            if (player.ScooterRemaining <= 0f && player.ScooterView != null) player.ScooterView.SetActive(false);
            if (player.SniperRemaining <= 0f && player.SniperView != null) player.SniperView.SetActive(false);
            if (player.CapsuleFootballRemaining <= 0f && player.CapsuleFootballView != null)
            {
                player.CapsuleFootballCooldown = 0f;
                player.CapsuleFootballView.SetActive(false);
            }
        }

        private void RebuildEnemyGrid()
        {
            enemyGrid.Clear();
            foreach (var enemy in enemies)
            {
                if (enemy.Active) enemyGrid.Insert(enemy.Position.x, enemy.Position.y, enemy);
            }
        }

        private void SpawnEnemy(string enemyId, float multiplier, bool forceBoss)
        {
            var config = session.Config.Enemies.Enemies.FirstOrDefault(value => value.Id == enemyId);
            if (config == null) return;
            var angle = random.Range(0f, Mathf.PI * 2f);
            var distance = random.Range(7f, 10f);
            var position = ResolveMapCollision(player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                WorldScale.ToUnits(config.CollisionRadius));
            var view = enemyPool.Acquire();
            ConfigureEnemyView(view, config);
            view.transform.position = position;
            var enemy = new EnemyRuntime
            {
                View = view,
                Config = config,
                Position = position,
                MaxHp = config.MaxHp * multiplier,
                Hp = config.MaxHp * multiplier,
                Radius = WorldScale.ToUnits(config.CollisionRadius),
                Active = true,
                IsBoss = forceBoss || config.Type == "boss",
                WeightMultiplier = multiplier,
                DashCooldown = config.DashCooldown
            };
            BindEnemyHpBar(enemy);
            enemies.Add(enemy);
        }

        private void SpawnBoss()
        {
            SpawnBossGroup(1);
        }

        private void SpawnFinalWaveBosses()
        {
            if (finalWaveBossStarted) return;
            finalWaveBossStarted = true;
            SpawnBossGroup(Mathf.Max(0, session.Settings.BossCount));
        }

        private void SpawnBossGroup(int count)
        {
            if (count <= 0) return;
            for (var i = 0; i < count; i++) SpawnEnemy("boss_mutant_giant", 1f, true);
            bossKilled = false;
            bossIntroRemaining = 1.5f;
            stateMachine.Set(GameState.BossIntro);
            ToastRequested?.Invoke(count > 1 ? $"{count} 名 BOSS 变异巨尸来袭" : "BOSS 变异巨尸来袭");
            AudioRequested?.Invoke("boss_intro");
        }

        private void SpawnProjectile(Vector2 origin, Vector2 direction, float speedPixels, float rangePixels,
            float damage, int penetration, Color trailColor, float trailSize, Sprite sprite, Vector2 worldSize)
        {
            var view = projectilePool.Acquire();
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : RuntimeSpriteFactory.White;
            renderer.color = Color.white;
            RuntimeSpriteFactory.SetWorldSize(view, worldSize.x, worldSize.y);
            var projectile = new ProjectileRuntime
            {
                View = view,
                Position = origin,
                Velocity = direction.normalized * WorldScale.ToUnits(speedPixels),
                RemainingRange = WorldScale.ToUnits(rangePixels),
                Radius = 0.08f,
                Damage = damage,
                Penetration = Mathf.Max(1, penetration),
                Active = true,
                TrailColor = trailColor,
                TrailSize = trailSize,
                TrailTimer = 0f,
                Kind = ProjectileKind.Bullet,
                AngularVelocity = 0f
            };
            view.transform.position = origin;
            view.transform.right = direction;
            projectiles.Add(projectile);
            projectileActive++;
        }

        private void SpawnFireBottle(Vector2 origin, Vector2 destination, float zoneRadiusPixels, float damage,
            float zoneDuration, float zoneTickInterval)
        {
            var offset = destination - origin;
            var distance = offset.magnitude;
            if (distance <= 0.001f)
            {
                skillFx.FireImpact(destination, WorldScale.ToUnits(zoneRadiusPixels));
                SpawnZone(destination, zoneRadiusPixels, damage, zoneDuration, zoneTickInterval);
                return;
            }

            var direction = offset / distance;
            var view = projectilePool.Acquire();
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.sprite = WeaponArtCatalog.LoadBattle("fire_bottle") ?? RuntimeSpriteFactory.White;
            renderer.color = Color.white;
            RuntimeSpriteFactory.SetWorldSize(view, 0.34f, 0.34f);
            view.transform.position = origin;
            view.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 55f);

            projectiles.Add(new ProjectileRuntime
            {
                View = view,
                Position = origin,
                Velocity = direction * WorldScale.ToUnits(FireBottleSpeedPixels),
                RemainingRange = distance,
                Radius = 0f,
                Damage = damage,
                Penetration = 0,
                Active = true,
                TrailColor = FxSpriteFactory.FromRgb(0xff8844),
                TrailSize = 4f,
                TrailTimer = 0f,
                Kind = ProjectileKind.FireBottle,
                ZoneRadiusPixels = zoneRadiusPixels,
                ZoneDuration = zoneDuration,
                ZoneTickInterval = zoneTickInterval,
                AngularVelocity = FireBottleAngularVelocity
            });
            projectileActive++;
        }

        private void SpawnZone(Vector2 position, float radiusPixels, float damage, float durationSeconds, float tickInterval)
        {
            var view = zonePool.Acquire();
            var radius = WorldScale.ToUnits(radiusPixels);
            var zoneRenderer = view.GetComponent<SpriteRenderer>();
            zoneRenderer.sprite = WeaponArtCatalog.LoadBattle("fire_zone") ?? RuntimeSpriteFactory.Circle;
            zoneRenderer.color = new Color(1f, 1f, 1f, 0.28f);
            RuntimeSpriteFactory.SetWorldSize(view, radius * 2f, radius * 2f);
            view.transform.position = position;
            var zone = new GroundZoneRuntime
            {
                View = view,
                Position = position,
                Radius = radius,
                Damage = damage,
                Remaining = durationSeconds,
                Duration = durationSeconds,
                TickInterval = Mathf.Max(0.05f, tickInterval),
                TickTimer = 0f,
                Age = 0f,
                VisualSeed = (Mathf.Abs(position.x * 13f + position.y * 7f) % 997f) / 997f * Mathf.PI * 2f,
                Active = true
            };
            for (var i = 0; i < zone.Flames.Length; i++)
            {
                zone.Flames[i] = skillFx.AcquireFlame();
                zone.FlameAngles[i] = Mathf.PI * 2f * i / zone.Flames.Length;
                zone.FlameOrbits[i] = i == 0 ? 0f : 0.2f + i % 3 * 0.18f;
                zone.FlamePhases[i] = i * 1.73f;
                zone.FlameSizes[i] = i == 0 ? 1.2f : 0.8f + i % 2 * 0.22f;
                var flameRenderer = zone.Flames[i].GetComponent<SpriteRenderer>();
                flameRenderer.sprite = WeaponArtCatalog.LoadBattle("fire_flame") ?? FxSpriteFactory.Flame;
                flameRenderer.color = new Color(1f, 1f, 1f, 0.85f);
            }
            UpdateZoneVisual(zone);
            zones.Add(zone);
            zoneActive++;
        }

        private void UpdateZoneVisual(GroundZoneRuntime zone)
        {
            var fadeIn = Mathf.Min(1f, zone.Age * 6f);
            var fadeOut = Mathf.Min(1f, Mathf.Max(0f, zone.Remaining) * 1.8f);
            var lifeAlpha = fadeIn * fadeOut;
            var pulse = Mathf.Sin(zone.Age * 4.8f + zone.VisualSeed);
            if (zone.View.TryGetComponent<SpriteRenderer>(out var zoneRenderer))
            {
                var c = zoneRenderer.color;
                c.a = 0.3f * lifeAlpha;
                zoneRenderer.color = c;
                zone.View.transform.localScale = new Vector3(
                    (zone.Radius * 2f / Mathf.Max(0.001f, zoneRenderer.sprite.bounds.size.x)) * (0.98f + pulse * 0.025f),
                    (zone.Radius * 2f / Mathf.Max(0.001f, zoneRenderer.sprite.bounds.size.y)) * (0.68f + pulse * 0.018f),
                    1f);
            }

            RuntimeSpriteFactory.UpdateDepth(zone.View, zone.Position.y, -20);
            var flameScale = Mathf.Max(0.82f, zone.Radius / 0.58f);
            for (var i = 0; i < zone.Flames.Length; i++)
            {
                var flame = zone.Flames[i];
                if (flame == null) continue;
                var sway = Mathf.Sin(zone.Age * (5.6f + i * 0.37f) + zone.FlamePhases[i] + zone.VisualSeed);
                var angle = zone.FlameAngles[i] + zone.VisualSeed * 0.35f;
                var orbit = zone.Radius * zone.FlameOrbits[i];
                var fx = zone.Position.x + Mathf.Cos(angle) * orbit + sway * FxSpriteFactory.Px(3f);
                var fy = zone.Position.y + Mathf.Sin(angle) * orbit * 0.5f +
                         Mathf.Cos(zone.Age * 4f + i) * FxSpriteFactory.Px(1.5f);
                flame.transform.position = new Vector3(fx, fy, 0f);
                flame.transform.rotation = Quaternion.Euler(0f, 0f, sway * 0.12f * Mathf.Rad2Deg);
                var heightPulse = 0.86f + Mathf.Abs(sway) * 0.3f;
                var size = flameScale * zone.FlameSizes[i];
                FxSpriteFactory.SetWorldSize(flame,
                    FxSpriteFactory.Px(36f) * size * (0.88f - sway * 0.1f),
                    FxSpriteFactory.Px(35f) * size * heightPulse);
                if (flame.TryGetComponent<SpriteRenderer>(out var flameRenderer))
                {
                    flameRenderer.color = new Color(1f, 1f, 1f,
                        (0.72f + Mathf.Abs(sway) * 0.24f) * lifeAlpha);
                }
                FxSpriteFactory.UpdateDepth(flame, fy, 3);
            }
        }

        private void ReleaseZone(GroundZoneRuntime zone)
        {
            zone.Active = false;
            for (var i = 0; i < zone.Flames.Length; i++)
            {
                if (zone.Flames[i] == null) continue;
                skillFx.ReleaseFlame(zone.Flames[i]);
                zone.Flames[i] = null;
            }
            zonePool.Release(zone.View);
            zoneActive--;
        }

        private void SpawnCrystal(Vector2 position, int value)
        {
            var view = crystalPool.Acquire();
            view.transform.position = position;
            RuntimeSpriteFactory.SetWorldSize(view, 0.46f, 0.29f);
            crystals.Add(new CrystalRuntime { View = view, Position = position, Value = value, Active = true });
            crystalActive++;
        }

        private MapEventRuntime SpawnMapEvent(MapEventKind kind, Vector2 position, float radius, Color color)
        {
            var view = eventPool.Acquire();
            view.transform.position = position;
            var result = new MapEventRuntime
            {
                View = view,
                Kind = kind,
                Position = position,
                Radius = radius,
                Active = true,
                VisualSeed = random.Range(0f, Mathf.PI * 2f)
            };
            BindMapEventVisual(result, color);
            if (kind == MapEventKind.Crate) supplyCratePositions.Add(position);
            mapEvents.Add(result);
            return result;
        }

        private void BindMapEventVisual(MapEventRuntime mapEvent, Color color)
        {
            var primary = mapEvent.View.GetComponent<SpriteRenderer>();
            var aura = GetOrCreateMapChild(mapEvent.View, "Aura", 1);
            var inner = GetOrCreateMapChild(mapEvent.View, "FogInner", 2);
            mapEvent.AuraView = aura;
            mapEvent.FogInnerView = inner;
            for (var i = 0; i < mapEvent.SmokePuffs.Length; i++)
                mapEvent.SmokePuffs[i] = GetOrCreateMapChild(mapEvent.View, $"SmokePuff{i}", 3 + i);

            aura.SetActive(false);
            inner.SetActive(false);
            foreach (var puff in mapEvent.SmokePuffs) puff.SetActive(false);
            primary.color = color;
            primary.sprite = RuntimeSpriteFactory.Circle;
            switch (mapEvent.Kind)
            {
                case MapEventKind.Crate:
                    primary.sprite = MapArtCatalog.LoadProp("map_crate") ?? RuntimeSpriteFactory.Circle;
                    primary.color = Color.white;
                    RuntimeSpriteFactory.SetWorldSize(mapEvent.View, 0.85f, 0.85f);
                    ConfigureMapChild(aura, MapArtCatalog.LoadEffect("map_event_aura") ?? RuntimeSpriteFactory.Circle,
                        new Color(1f, 0.72f, 0.28f, 0.24f), 1.10f, 0.75f, Vector2.zero);
                    aura.SetActive(true);
                    break;
                case MapEventKind.HiddenCrate:
                    primary.sprite = MapArtCatalog.LoadProp("map_hidden_crate") ?? RuntimeSpriteFactory.Circle;
                    primary.color = Color.clear; // Bound to the formal asset, intentionally invisible by design.
                    RuntimeSpriteFactory.SetWorldSize(mapEvent.View, 0.85f, 0.85f);
                    break;
                case MapEventKind.Altar:
                    primary.sprite = MapArtCatalog.LoadProp("map_altar") ?? RuntimeSpriteFactory.Circle;
                    primary.color = Color.white;
                    RuntimeSpriteFactory.SetWorldSize(mapEvent.View, 1.10f, 1.10f);
                    ConfigureMapChild(aura, MapArtCatalog.LoadEffect("map_event_aura") ?? RuntimeSpriteFactory.Circle,
                        new Color(0.68f, 0.28f, 1f, 0.34f), 1.35f, 0.90f, Vector2.zero);
                    aura.SetActive(true);
                    break;
                case MapEventKind.HealingChicken:
                    primary.sprite = MapArtCatalog.LoadPickup("chicken_leg") ?? RuntimeSpriteFactory.Circle;
                    primary.color = Color.white;
                    RuntimeSpriteFactory.SetWorldSize(mapEvent.View, 0.576f, 0.576f);
                    ConfigureMapChild(aura, MapArtCatalog.LoadEffect("map_event_aura") ?? RuntimeSpriteFactory.Circle,
                        new Color(0.38f, 1f, 0.48f, 0.22f), 0.736f, 0.496f, Vector2.zero);
                    aura.SetActive(true);
                    break;
                case MapEventKind.PoisonFog:
                    primary.sprite = MapArtCatalog.LoadEffect("poison_fog") ?? RuntimeSpriteFactory.Circle;
                    primary.color = new Color(0.42f, 0.45f, 0.40f, 0.48f);
                    RuntimeSpriteFactory.SetWorldSize(mapEvent.View, mapEvent.Radius * 2.2f, mapEvent.Radius * 1.65f);
                    ConfigureMapChild(inner, MapArtCatalog.LoadEffect("poison_fog") ?? RuntimeSpriteFactory.Circle,
                        new Color(0.32f, 0.35f, 0.30f, 0.40f), mapEvent.Radius * 1.55f, mapEvent.Radius * 1.05f, Vector2.zero);
                    inner.SetActive(true);
                    for (var i = 0; i < mapEvent.SmokePuffs.Length; i++)
                    {
                        ConfigureMapChild(mapEvent.SmokePuffs[i], MapArtCatalog.LoadEffect("poison_smoke_puff") ?? RuntimeSpriteFactory.Circle,
                            new Color(0.38f, 0.40f, 0.36f, 0.42f), mapEvent.Radius * 0.38f, mapEvent.Radius * 0.38f, Vector2.zero);
                        mapEvent.SmokePuffs[i].SetActive(true);
                    }
                    break;
                default:
                    RuntimeSpriteFactory.SetWorldSize(mapEvent.View, mapEvent.Radius * 2f, mapEvent.Radius * 2f);
                    break;
            }
            RuntimeSpriteFactory.UpdateDepth(mapEvent.View, mapEvent.Position.y, mapEvent.Kind == MapEventKind.PoisonFog ? -50 : 5);
            UpdateMapChildDepths(mapEvent);
        }

        private static GameObject GetOrCreateMapChild(GameObject root, string name, int orderOffset)
        {
            var existing = root.transform.Find(name);
            if (existing != null) return existing.gameObject;
            var child = RuntimeSpriteFactory.CreateSpriteView(name, RuntimeSpriteFactory.Circle, Color.clear, orderOffset);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private static void ConfigureMapChild(GameObject child, Sprite sprite, Color color, float width, float height, Vector2 localPosition)
        {
            var renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            var parentScale = child.transform.parent.lossyScale;
            RuntimeSpriteFactory.SetWorldSize(child, width / Mathf.Max(0.0001f, parentScale.x), height / Mathf.Max(0.0001f, parentScale.y));
            child.transform.localPosition = new Vector3(localPosition.x / Mathf.Max(0.0001f, parentScale.x),
                localPosition.y / Mathf.Max(0.0001f, parentScale.y), 0f);
        }

        private void UpdateMapEventVisual(MapEventRuntime mapEvent)
        {
            var time = elapsed + mapEvent.VisualSeed;
            var primary = mapEvent.View.GetComponent<SpriteRenderer>();
            switch (mapEvent.Kind)
            {
                case MapEventKind.Crate:
                case MapEventKind.Altar:
                case MapEventKind.HealingChicken:
                    mapEvent.View.transform.position = mapEvent.Position + Vector2.up * (Mathf.Sin(time * 1.4f) * 0.018f);
                    if (mapEvent.AuraView != null)
                    {
                        mapEvent.AuraView.transform.localRotation = Quaternion.Euler(0f, 0f, time * 22f);
                        var auraRenderer = mapEvent.AuraView.GetComponent<SpriteRenderer>();
                        auraRenderer.sortingOrder = primary.sortingOrder - 1;
                    }
                    break;
                case MapEventKind.PoisonFog:
                    var pulse = 1f + Mathf.Sin(time * 1.7f) * 0.07f;
                    mapEvent.View.transform.position = mapEvent.Position + new Vector2(Mathf.Sin(time * 0.42f) * 0.04f,
                        Mathf.Cos(time * 0.35f) * 0.025f);
                    primary.color = new Color(0.42f, 0.45f, 0.40f, 0.44f + Mathf.Sin(time * 1.3f) * 0.05f);
                    if (mapEvent.FogInnerView != null)
                    {
                        var innerSprite = mapEvent.FogInnerView.GetComponent<SpriteRenderer>().sprite;
                        var scale = mapEvent.View.transform.lossyScale;
                        var bounds = innerSprite.bounds.size;
                        mapEvent.FogInnerView.transform.localScale = new Vector3(
                            mapEvent.Radius * 1.55f * pulse / Mathf.Max(0.0001f, scale.x * bounds.x),
                            mapEvent.Radius * 1.05f * pulse / Mathf.Max(0.0001f, scale.y * bounds.y), 1f);
                        mapEvent.FogInnerView.GetComponent<SpriteRenderer>().color =
                            new Color(0.32f, 0.35f, 0.30f, 0.36f + Mathf.Sin(time * 1.1f) * 0.05f);
                    }
                    for (var i = 0; i < mapEvent.SmokePuffs.Length; i++)
                    {
                        var puff = mapEvent.SmokePuffs[i];
                        if (puff == null) continue;
                        var angle = time * (0.30f + i * 0.025f) + i * Mathf.PI * 2f / mapEvent.SmokePuffs.Length;
                        var orbit = mapEvent.Radius * (0.28f + 0.06f * (i % 3));
                        puff.transform.localPosition = new Vector3(Mathf.Cos(angle) * orbit / Mathf.Max(0.0001f, mapEvent.View.transform.lossyScale.x),
                            Mathf.Sin(angle) * orbit * 0.55f / Mathf.Max(0.0001f, mapEvent.View.transform.lossyScale.y), 0f);
                        puff.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
                        puff.GetComponent<SpriteRenderer>().color = new Color(0.38f, 0.40f, 0.36f,
                            0.34f + 0.12f * (0.5f + 0.5f * Mathf.Sin(time * 1.2f + i)));
                    }
                    break;
            }
            UpdateMapChildDepths(mapEvent);
        }

        internal static float RestoreFullHealth(PlayerRuntime target)
        {
            if (target == null) return 0f;
            var restored = Mathf.Max(0f, target.MaxHp - target.Hp);
            target.Hp = target.MaxHp;
            target.HitFlashRemaining = 0f;
            return restored;
        }

        private void CollectHealingChicken()
        {
            var restored = RestoreFullHealth(player);
            if (player.View != null && player.View.TryGetComponent<SpriteRenderer>(out var renderer))
                renderer.color = Color.white;
            RegisterPickupNotice("chicken_leg", "烤鸡腿",
                restored > 0f ? $"生命值回满 +{restored:0.#}" : "生命值已满", InstantNoticeDuration);
            AudioRequested?.Invoke("crate");
            PublishSnapshot();
        }

        private static void UpdateMapChildDepths(MapEventRuntime mapEvent)
        {
            var order = mapEvent.View.GetComponent<SpriteRenderer>().sortingOrder;
            if (mapEvent.AuraView != null) mapEvent.AuraView.GetComponent<SpriteRenderer>().sortingOrder = order - 1;
            if (mapEvent.FogInnerView != null) mapEvent.FogInnerView.GetComponent<SpriteRenderer>().sortingOrder = order + 1;
            for (var i = 0; i < mapEvent.SmokePuffs.Length; i++)
                if (mapEvent.SmokePuffs[i] != null) mapEvent.SmokePuffs[i].GetComponent<SpriteRenderer>().sortingOrder = order + 2 + i;
        }

        private void ApplyEnemyDamage(EnemyRuntime enemy, float baseDamage, bool allowSniperExecute = true)
        {
            if (enemy == null || !enemy.Active) return;
            if (allowSniperExecute && player.SniperRemaining > 0f &&
                (enemy.Config.Type == "normal" || enemy.Config.Type == "elite"))
            {
                totalDamage += Mathf.Max(0f, enemy.Hp);
                maxSingleDamage = Mathf.Max(maxSingleDamage, enemy.Hp);
                var sniperFrom = player.SniperView != null && player.SniperView.activeSelf
                    ? (Vector2)player.SniperView.transform.position
                    : player.Position + new Vector2(0f, FxSpriteFactory.Px(8f));
                skillFx.SniperShot(sniperFrom, enemy.Position + new Vector2(0f, FxSpriteFactory.Px(10f)));
                AudioRequested?.Invoke("sniper_shot");
                KillEnemy(enemy);
                return;
            }
            var critical = random.Value < player.CritRate;
            var variance = random.Range(session.Config.Balance.Combat.DamageVarianceMin,
                session.Config.Balance.Combat.DamageVarianceMax);
            var damage = DamageFormula.Calculate(baseDamage,
                player.DamageMultiplier + player.TemporaryDamageBonus, 1f,
                critical ? player.CritDamage : 1f, variance, 0f);
            enemy.Hp -= damage;
            totalDamage += damage;
            maxSingleDamage = Mathf.Max(maxSingleDamage, damage);
            if (session.Settings.DamageNumbers)
                DamageNumberRequested?.Invoke(enemy.Position, damage, critical);
            skillFx.HitSpark(enemy.Position, critical);
            UpdateEnemyHpBar(enemy);
            if (enemy.Hp <= 0f)
            {
                KillEnemy(enemy);
            }
        }

        private void DamagePlayer(float rawDamage, bool bypassInvulnerability)
        {
            if (invincible || (!bypassInvulnerability && player.Invulnerability > 0f)) return;
            player.Hp -= Mathf.Max(1f, rawDamage - player.Armor);
            if (session.Settings.ScreenShake)
            {
                cameraShakeRemaining = 0.18f;
                cameraShakeStrength = bypassInvulnerability ? 0.07f : 0.14f;
            }
            player.HitFlashRemaining = Mathf.Max(player.HitFlashRemaining, 0.22f);
            if (!bypassInvulnerability)
            {
                player.Invulnerability = session.Config.Balance.Combat.InvincibilityDuration;
            }
        }

        private void KillEnemy(EnemyRuntime enemy)
        {
            enemy.Active = false;
            killCount++;
            if (enemy.IsBoss)
                AudioRequested?.Invoke("boss_defeat");
            var burstColor = enemy.IsBoss
                ? FxSpriteFactory.FromRgb(0xff5555)
                : enemy.Config.Type == "elite"
                    ? FxSpriteFactory.FromRgb(0xaa66ff)
                    : enemy.Config.Type == "leader"
                        ? FxSpriteFactory.FromRgb(0xff8844)
                        : FxSpriteFactory.FromRgb(0x88cc66);
            skillFx.DeathBurst(enemy.Position, burstColor);
            SpawnCrystal(enemy.Position, enemy.Config.ExperienceReward);
            if (enemy.IsBoss)
            {
                bossKilled = !enemies.Any(value => value != enemy && value.Active && value.IsBoss);
            }
            enemyPool.Release(enemy.View);
            enemies.Remove(enemy);
            if (enemy.IsBoss && allWavesCleared && bossKilled)
            {
                FinishBattle(true);
            }
        }

        private void ReleaseEnemy(EnemyRuntime enemy)
        {
            enemy.Active = false;
            enemyPool.Release(enemy.View);
        }

        private void ReleaseProjectile(ProjectileRuntime projectile)
        {
            projectile.Active = false;
            projectilePool.Release(projectile.View);
            projectileActive--;
        }

        private void TryOpenLevelUp()
        {
            if (stateMachine.Current != GameState.Playing || experience.PendingLevelUps <= 0) return;
            if (!HasAvailableUpgrades())
            {
                DrainPendingLevelUpsSilently();
                return;
            }
            experience.ConsumeLevelUp();
            RecalculatePlayerStats();
            levelRefreshAvailable = true;
            stateMachine.Set(GameState.LevelUp);
            AudioRequested?.Invoke("level_up");
            LevelUpRequested?.Invoke(RollUpgradeOffers(), true);
        }

        private bool HasAvailableUpgrades() =>
            HasAvailableUpgrades(session.Config.Weapons.Weapons, session.Config.Skills.Skills, upgrades);

        internal static bool HasAvailableUpgrades(
            IReadOnlyList<WeaponConfig> weapons,
            IReadOnlyList<SkillConfig> skills,
            IReadOnlyDictionary<string, OwnedUpgrade> ownedUpgrades)
        {
            if (weapons != null)
            {
                foreach (var weapon in weapons)
                {
                    if (weapon == null) continue;
                    var level = ownedUpgrades != null && ownedUpgrades.TryGetValue(weapon.Id, out var owned)
                        ? owned.Level : 0;
                    if (level < weapon.MaxLevel) return true;
                }
            }
            if (skills != null)
            {
                foreach (var skill in skills)
                {
                    if (skill == null) continue;
                    var level = ownedUpgrades != null && ownedUpgrades.TryGetValue(skill.Id, out var owned)
                        ? owned.Level : 0;
                    if (level < skill.MaxLevel) return true;
                }
            }
            return false;
        }

        private void DrainPendingLevelUpsSilently()
        {
            while (experience.ConsumeLevelUp()) { }
            RecalculatePlayerStats();
            PublishSnapshot();
        }

        private List<UpgradeOffer> RollUpgradeOffers()
        {
            var pool = new List<UpgradeOffer>();
            foreach (var weapon in session.Config.Weapons.Weapons)
            {
                var level = upgrades.TryGetValue(weapon.Id, out var owned) ? owned.Level : 0;
                if (level < weapon.MaxLevel)
                {
                    var nextLevel = level + 1;
                    var name = weapon.Name;
                    var description = weapon.Description;
                    var icon = weapon.Icon;
                    if (nextLevel >= weapon.MaxLevel && weapon.Promotion.IsConfigured)
                    {
                        name = weapon.Promotion.Name;
                        if (!string.IsNullOrWhiteSpace(weapon.Promotion.Description))
                            description = weapon.Promotion.Description;
                        if (!string.IsNullOrWhiteSpace(weapon.Promotion.Icon))
                            icon = weapon.Promotion.Icon;
                    }

                    pool.Add(new UpgradeOffer
                    {
                        Id = weapon.Id,
                        Name = name,
                        Description = description,
                        Icon = icon,
                        Kind = UpgradeKind.Weapon,
                        NextLevel = nextLevel
                    });
                }
            }
            foreach (var passive in session.Config.Skills.Skills)
            {
                var level = upgrades.TryGetValue(passive.Id, out var owned) ? owned.Level : 0;
                if (level < passive.MaxLevel)
                {
                    pool.Add(new UpgradeOffer
                    {
                        Id = passive.Id,
                        Name = passive.Name,
                        Description = passive.Description,
                        Icon = passive.Icon,
                        Kind = UpgradeKind.Passive,
                        NextLevel = level + 1
                    });
                }
            }
            var results = new List<UpgradeOffer>(3);
            while (pool.Count > 0 && results.Count < 3)
            {
                var index = random.Range(0, pool.Count);
                results.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return results;
        }

        private void AddUpgrade(string id, UpgradeKind kind, int amount)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var weaponConfig = kind == UpgradeKind.Weapon
                ? session.Config.Weapons.Weapons.FirstOrDefault(value => value.Id == id)
                : null;
            var max = kind == UpgradeKind.Weapon
                ? (weaponConfig ?? throw new InvalidOperationException($"未知武器: {id}")).MaxLevel
                : session.Config.Skills.Skills.First(value => value.Id == id).MaxLevel;
            if (!upgrades.TryGetValue(id, out var owned))
            {
                var skillConfig = kind == UpgradeKind.Passive
                    ? session.Config.Skills.Skills.FirstOrDefault(value => value.Id == id)
                    : null;
                var name = kind == UpgradeKind.Weapon
                    ? weaponConfig?.Name
                    : skillConfig?.Name;
                owned = new OwnedUpgrade
                {
                    Id = id,
                    Name = name ?? id,
                    Icon = kind == UpgradeKind.Weapon
                        ? weaponConfig?.Icon ?? string.Empty
                        : skillConfig?.Icon ?? string.Empty,
                    Kind = kind,
                    MaxLevel = max
                };
                upgrades.Add(id, owned);
            }
            owned.MaxLevel = max;
            owned.Level = Mathf.Clamp(owned.Level + amount, 1, max);
            RecalculatePlayerStats();
            UpgradesChanged?.Invoke();
        }

        private void RecalculatePlayerStats()
        {
            var oldMax = player?.MaxHp ?? character.MaxHp;
            var oldHp = player?.Hp ?? character.MaxHp;
            var maxHpBonus = PassiveValue("passive_toughness", "maxHpBonus");
            if (player == null) return;
            var growthPercent = session.Config.Balance.Player.LevelMaxHpGrowthPercent;
            growthPercent = Mathf.Clamp(growthPercent, 0f, 1f);
            var scaledBaseMaxHp = PlayerLevelMaxHp.ScaledBaseMaxHp(character.MaxHp, experience.Level, growthPercent);
            player.MaxHp = scaledBaseMaxHp + maxHpBonus;
            player.Hp = PlayerLevelMaxHp.ApplyHpAfterMaxIncrease(oldHp, oldMax, player.MaxHp);
            player.Armor = character.Armor + PassiveValue("passive_toughness", "armorBonus");
            player.MoveSpeed = WorldScale.ToUnits(character.MoveSpeed) *
                               (1f + PassiveValue("passive_swift", "moveSpeedBonus"));
            player.PickupRadius = WorldScale.ToUnits(character.PickupRadius) *
                                  (1f + PassiveValue("passive_magnet", "pickupRadiusBonus"));
            player.DamageMultiplier = character.DamageMultiplier +
                                      PassiveValue("passive_strength", "damageMultiplierBonus");
            player.AttackSpeedMultiplier = character.AttackSpeedMultiplier +
                                           PassiveValue("passive_haste", "attackSpeedBonus");
        }

        private float PassiveValue(string id, string key)
        {
            if (!upgrades.TryGetValue(id, out var owned)) return 0f;
            var config = session.Config.Skills.Skills.First(value => value.Id == id);
            return config.LevelEffects.TryGetValue(owned.Level.ToString(), out var effect) &&
                   effect.TryGetValue(key, out var value) ? value : 0f;
        }

        private Dictionary<string, float> WeaponEffect(string id, int level)
        {
            var config = session.Config.Weapons.Weapons.First(value => value.Id == id);
            return config.LevelEffects.TryGetValue(level.ToString(), out var effect)
                ? effect
                : new Dictionary<string, float>();
        }

        private static float Get(IReadOnlyDictionary<string, float> values, string key, float fallback) =>
            values.TryGetValue(key, out var value) ? value : fallback;

        private void FinishLevelUp()
        {
            if (experience.PendingLevelUps > 0)
            {
                if (!HasAvailableUpgrades())
                {
                    DrainPendingLevelUpsSilently();
                    stateMachine.Set(GameState.Playing);
                    return;
                }
                experience.ConsumeLevelUp();
                RecalculatePlayerStats();
                levelRefreshAvailable = true;
                LevelUpRequested?.Invoke(RollUpgradeOffers(), true);
                return;
            }
            stateMachine.Set(GameState.Playing);
        }

        private void TriggerCrate(MapEventRuntime crate)
        {
            var isHidden = crate.Kind == MapEventKind.HiddenCrate && stage.MapEvents.HiddenCrateEffects.Count > 0;
            var effects = isHidden ? stage.MapEvents.HiddenCrateEffects : stage.MapEvents.CrateEffects;
            var weights = isHidden ? session.Settings.HiddenCrateEffectWeights : session.Settings.CrateEffectWeights;
            ApplyCrateEffect(WeightedCrateEffect(effects, weights) ?? new CrateEffectConfig { Id = "xp_burst" });
            AudioRequested?.Invoke("crate");
            PublishSnapshot();
        }

        private void ApplyCrateEffect(CrateEffectConfig effect)
        {
            var id = effect?.Id ?? "xp_burst";
            switch (id)
            {
                case "spawn_boss":
                    SpawnBoss();
                    RegisterPickupNotice(id, "危险信号", "召唤 1 名 Boss", InstantNoticeDuration);
                    return;
                case "spawn_poison_fog":
                    var fogCount = Mathf.Max(1, effect.FogCount);
                    for (var i = 0; i < fogCount; i++) SpawnPoisonFog(RandomMapPosition());
                    RegisterPickupNotice(id, "毒雾扩散", $"地图中生成 {fogCount} 片毒雾", InstantNoticeDuration);
                    return;
                case "double_level":
                    var levelUps = Mathf.Max(1, effect.LevelUps);
                    var levelExperience = experience.RequiredForLevel(experience.Level) * levelUps;
                    experience.Add(levelExperience);
                    RegisterPickupNotice(id, "等级跃升",
                        $"获得 {levelExperience} 点经验（约 {levelUps} 级）", InstantNoticeDuration);
                    return;
                case "max_hp_bonus":
                    player.MaxHp += effect.MaxHpBonus;
                    player.Hp += effect.MaxHpBonus;
                    RegisterPickupNotice(id, "生命强化",
                        $"生命上限 +{effect.MaxHpBonus:0.#}，并恢复 {effect.MaxHpBonus:0.#} 点生命", InstantNoticeDuration);
                    return;
                case "move_speed_bonus":
                    var speedBonus = Mathf.Max(0.1f, effect.MoveSpeedBonus);
                    player.TemporaryMoveMultiplier += speedBonus;
                    RegisterPickupNotice(id, "疾行增幅",
                        $"移动速度临时 +{Mathf.RoundToInt(speedBonus * 100f)}%",
                        (player.TemporaryMoveMultiplier - 1f) / 0.08f, PickupNoticeSync.MoveSpeed);
                    return;
                case "magnet_burst":
                    ApplyMagnetBurst(Mathf.Max(1f, effect.PickupRadiusMul > 0f ? effect.PickupRadiusMul : 5f),
                        Mathf.Max(1f, effect.Duration > 0f ? effect.Duration : 60f),
                        "经验磁吸",
                        false, 0f);
                    return;
                case "anesthetic_capsule":
                    var slowDuration = session.Settings.AnestheticCapsuleDuration > 0f
                        ? session.Settings.AnestheticCapsuleDuration
                        : Mathf.Max(5f, effect.Duration > 0f ? effect.Duration : 20f);
                    var slowPercent = session.Settings.AnestheticCapsuleSlowPercent > 0f
                        ? session.Settings.AnestheticCapsuleSlowPercent
                        : Mathf.Clamp(
                            (effect.MoveSpeedBonus > 0f ? effect.MoveSpeedBonus : 0.2f) * 100f, 5f, 80f);
                    player.EnemySlowRemaining = Mathf.Max(player.EnemySlowRemaining, slowDuration);
                    RegisterPickupNotice(id, "麻醉胶囊",
                        $"怪物移动速度 -{Mathf.RoundToInt(slowPercent)}%",
                        player.EnemySlowRemaining, PickupNoticeSync.EnemySlow);
                    return;
                case "scooter_boost":
                    player.ScooterRemaining = session.Settings.ScooterBoostDuration;
                    SetTemporaryItemVisual(ref player.ScooterView, "Scooter", MapArtCatalog.LoadItem("player_scooter"),
                        RuntimeSpriteFactory.White, new Vector2(1.08f, 0.40f));
                    RegisterPickupNotice(id, "滑板加速", "移动速度 +50%",
                        player.ScooterRemaining, PickupNoticeSync.Scooter);
                    return;
                case "sniper_rifle":
                    player.SniperRemaining = session.Settings.SniperRifleDuration;
                    SetTemporaryItemVisual(ref player.SniperView, "Sniper", MapArtCatalog.LoadItem("player_sniper"),
                        RuntimeSpriteFactory.White, new Vector2(0.95f, 0.52f));
                    RegisterPickupNotice(id, "狙击步枪", "普通与精英敌人一击必杀",
                        player.SniperRemaining, PickupNoticeSync.Sniper);
                    return;
                case "crate_guide":
                    player.CrateGuideRemaining = session.Settings.CrateGuideDuration;
                    RegisterPickupNotice(id, "追踪眼镜", "显示补给箱方位",
                        player.CrateGuideRemaining, PickupNoticeSync.CrateGuide);
                    return;
                case "capsule_football":
                    player.CapsuleFootballRemaining = Mathf.Max(5f, session.Settings.CapsuleFootballDuration);
                    player.CapsuleFootballCooldown = 0f;
                    player.CapsuleFootballFlashRemaining = 0f;
                    SetTemporaryItemVisual(ref player.CapsuleFootballView, "CapsuleFootballBelt",
                        WeaponArtCatalog.LoadBattle("capsule_football_belt"), RuntimeSpriteFactory.White,
                        new Vector2(0.56f, 0.40f));
                    player.CapsuleFootballBaseScale = player.CapsuleFootballView.transform.localScale;
                    RegisterPickupNotice(id, "胶囊足球", "每 2.5 秒自动弹射，最多命中 3 人",
                        player.CapsuleFootballRemaining, PickupNoticeSync.CapsuleFootball);
                    return;
                case "purge":
                    for (var i = enemies.Count - 1; i >= 0; i--)
                    {
                        if (enemies[i].IsBoss) continue;
                        KillEnemy(enemies[i]);
                    }
                    RegisterPickupNotice(id, "清屏献祭",
                        "清除场上普通敌人并保留经验掉落", InstantNoticeDuration);
                    return;
                default:
                    var experienceAmount = random.Range(stage.MapEvents.CrateXpMin, stage.MapEvents.CrateXpMax + 1);
                    experience.Add(experienceAmount);
                    RegisterPickupNotice("xp_burst", "经验补给",
                        $"立即获得 {experienceAmount} 点经验", InstantNoticeDuration);
                    return;
            }
        }

        private void SetTemporaryItemVisual(ref GameObject view, string name, Sprite sprite, Sprite fallback, Vector2 size)
        {
            if (view == null)
            {
                view = RuntimeSpriteFactory.CreateSpriteView(name, sprite ?? fallback, Color.white, 12);
                view.transform.SetParent(transform, false);
            }
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite ?? fallback;
            renderer.color = Color.white;
            RuntimeSpriteFactory.SetWorldSize(view, size.x, size.y);
            view.SetActive(true);
        }

        private void UpdateTemporaryItemVisuals(float horizontalInput)
        {
            if (player.ScooterView != null && player.ScooterView.activeSelf)
            {
                var playerRenderer = player.View.GetComponent<SpriteRenderer>();
                var playerHeight = playerRenderer != null
                    ? playerRenderer.bounds.size.y
                    : 0.62f * character.Scale * ActorVisualScaleMultiplier;
                // Player pivot is center → feet are at -50% height; scooter pivot sits on the feet.
                var feetY = -playerHeight * 0.5f;
                var facingLeft = horizontalInput < 0f ||
                    (horizontalInput == 0f && playerRenderer != null && playerRenderer.flipX);
                player.ScooterView.transform.position = player.Position + new Vector2(0f, feetY);
                // Scooter art faces opposite to the player sprite, so invert flip.
                player.ScooterView.GetComponent<SpriteRenderer>().flipX = !facingLeft;
                RuntimeSpriteFactory.UpdateDepth(player.ScooterView, player.Position.y, 6);
            }
            if (player.SniperView != null && player.SniperView.activeSelf)
            {
                var facingLeft = horizontalInput < 0f || (horizontalInput == 0f && player.View.GetComponent<SpriteRenderer>().flipX);
                // Player pivot is center; mid-body is y=0, then shift down ~5% of character height.
                var playerHeight = player.View.TryGetComponent<SpriteRenderer>(out var playerRenderer)
                    ? playerRenderer.bounds.size.y
                    : 0.62f * character.Scale * ActorVisualScaleMultiplier;
                var yOffset = -playerHeight * 0.05f;
                player.SniperView.transform.position = player.Position + new Vector2(facingLeft ? -0.17f : 0.17f, yOffset);
                player.SniperView.GetComponent<SpriteRenderer>().flipX = facingLeft;
                RuntimeSpriteFactory.UpdateDepth(player.SniperView, player.Position.y, 12);
            }
            if (player.CapsuleFootballView != null && player.CapsuleFootballView.activeSelf)
            {
                var playerRenderer = player.View.GetComponent<SpriteRenderer>();
                var facingLeft = horizontalInput < 0f ||
                                 (horizontalInput == 0f && playerRenderer != null && playerRenderer.flipX);
                var playerHeight = playerRenderer != null
                    ? playerRenderer.bounds.size.y
                    : 0.62f * character.Scale * ActorVisualScaleMultiplier;
                var yOffset = -playerHeight * 0.05f;
                player.CapsuleFootballView.transform.position = player.Position +
                    new Vector2(facingLeft ? -0.14f : 0.14f, yOffset);
                var beltRenderer = player.CapsuleFootballView.GetComponent<SpriteRenderer>();
                beltRenderer.flipX = facingLeft;
                var flash = Mathf.Clamp01(player.CapsuleFootballFlashRemaining / CapsuleFootballFlashDuration);
                beltRenderer.color = flash > 0f
                    ? Color.Lerp(Color.white, new Color(0.72f, 0.94f, 1f, 1f), flash)
                    : Color.white;
                player.CapsuleFootballView.transform.localScale = player.CapsuleFootballBaseScale *
                    (1f + flash * 0.14f);
                RuntimeSpriteFactory.UpdateDepth(player.CapsuleFootballView, player.Position.y, 11);
            }
        }

        private void TriggerAltar(MapEventRuntime altar)
        {
            var effects = stage.MapEvents.AltarEffects;
            if (effects == null || effects.Count == 0) return;
            var effect = WeightedAltarEffect(effects);
            if (effect == null) return;
            ApplyAltarEffect(effect);
            AudioRequested?.Invoke("altar");
            PublishSnapshot();
        }

        private void ApplyAltarEffect(AltarEffectConfig effect)
        {
            var hpCost = effect.Id switch
            {
                "blood_pact" => session.Settings.AltarBloodPactHpCost,
                "magnet_burst" => session.Settings.AltarMagnetBurstHpCost,
                "random_teleport" => session.Settings.AltarTeleportHpCost,
                "stun_watch" => session.Settings.AltarStunWatchHpCost,
                _ => effect.HpCost
            };
            player.Hp = Mathf.Max(1f, player.Hp - player.MaxHp * hpCost * 0.01f);
            switch (effect.Id)
            {
                case "blood_pact":
                    var damageBonus = session.Settings.AltarBloodPactDamageBonus > 0f
                        ? session.Settings.AltarBloodPactDamageBonus
                        : Mathf.Max(0.05f, effect.DamageBonus);
                    var bloodDuration = session.Settings.AltarBloodPactDuration > 0f
                        ? session.Settings.AltarBloodPactDuration
                        : Mathf.Max(1f, effect.Duration > 0f ? effect.Duration : 25f);
                    player.TemporaryDamageBonus = Mathf.Max(player.TemporaryDamageBonus, damageBonus);
                    player.BloodPactRemaining = Mathf.Max(player.BloodPactRemaining, bloodDuration);
                    RegisterPickupNotice(effect.Id, "献血加攻",
                        $"生命 -{hpCost:0.#}%，伤害临时 +{Mathf.RoundToInt(damageBonus * 100f)}%",
                        player.BloodPactRemaining, PickupNoticeSync.DamageBonus);
                    return;
                case "magnet_burst":
                    var magnetDuration = session.Settings.AltarMagnetDuration > 0f
                        ? session.Settings.AltarMagnetDuration
                        : Mathf.Max(1f, effect.Duration > 0f ? effect.Duration : 4f);
                    ApplyMagnetBurst(1f, magnetDuration, "磁力爆发", true, hpCost);
                    return;
                case "random_teleport":
                    ApplyRandomTeleport(hpCost);
                    return;
                case "stun_watch":
                    ApplyBossStunWatch(hpCost);
                    return;
                default:
                    RegisterPickupNotice(effect.Id, "未知祝福",
                        $"生命 -{hpCost:0.#}%", InstantNoticeDuration);
                    return;
            }
        }

        private void ApplyBossStunWatch(float hpCost)
        {
            var duration = session.Settings.AltarStunWatchDuration > 0f
                ? session.Settings.AltarStunWatchDuration
                : Mathf.Max(1f, 10f);
            var stunned = 0;
            foreach (var enemy in enemies)
            {
                if (!enemy.Active || !enemy.IsBoss) continue;
                enemy.StunRemaining = Mathf.Max(enemy.StunRemaining, duration);
                enemy.DashRemaining = 0f;
                stunned++;
                skillFx?.CastPulse(enemy.Position + new Vector2(0f, FxSpriteFactory.Px(12f)),
                    FxSpriteFactory.FromRgb(0x88c8ff));
            }

            if (stunned > 0)
            {
                RegisterPickupNotice("stun_watch", "麻醉型手表",
                    $"生命 -{hpCost:0.#}%，眩晕 Boss {duration:0.#} 秒",
                    duration, PickupNoticeSync.BossStun);
            }
            else
            {
                RegisterPickupNotice("stun_watch", "麻醉型手表",
                    $"生命 -{hpCost:0.#}%，场上暂无 Boss", InstantNoticeDuration);
            }
        }

        private float MaxBossStunRemaining()
        {
            var max = 0f;
            foreach (var enemy in enemies)
            {
                if (enemy.Active && enemy.IsBoss)
                    max = Mathf.Max(max, enemy.StunRemaining);
            }
            return max;
        }

        private void ApplyRandomTeleport(float hpCost)
        {
            var origin = player.Position;
            var destination = RandomMapPosition();
            for (var i = 0; i < 10; i++)
            {
                var candidate = RandomMapPosition();
                if (Vector2.Distance(candidate, origin) > Vector2.Distance(destination, origin))
                    destination = candidate;
            }
            destination = ResolveMapCollision(destination, player.CollisionRadius);
            skillFx?.CastPulse(origin + new Vector2(0f, FxSpriteFactory.Px(8f)),
                FxSpriteFactory.FromRgb(0xb48cff));
            player.Position = destination;
            player.Velocity = Vector2.zero;
            if (player.View != null)
                player.View.transform.position = destination;
            skillFx?.CastPulse(destination + new Vector2(0f, FxSpriteFactory.Px(8f)),
                FxSpriteFactory.FromRgb(0xd8b8ff));
            RegisterPickupNotice("random_teleport", "全图传送",
                $"生命 -{hpCost:0.#}%，已传送至地图随机位置", InstantNoticeDuration);
        }

        private AltarEffectConfig WeightedAltarEffect(List<AltarEffectConfig> effects)
        {
            if (effects == null || effects.Count == 0) return null;
            var total = 0f;
            foreach (var effect in effects)
            {
                var weight = session.Settings.AltarEffectWeights.TryGetValue(effect.Id, out var overrideWeight)
                    ? overrideWeight : effect.Weight;
                total += Mathf.Max(0f, weight);
            }
            if (total <= 0f) return effects[0];
            var roll = random.Range(0f, total);
            foreach (var effect in effects)
            {
                var weight = session.Settings.AltarEffectWeights.TryGetValue(effect.Id, out var overrideWeight)
                    ? overrideWeight : effect.Weight;
                roll -= Mathf.Max(0f, weight);
                if (roll <= 0f) return effect;
            }
            return effects[^1];
        }

        private void ApplyMagnetBurst(float pickupRadiusMul, float duration, string title, bool fromAltar,
            float hpCost)
        {
            if (fromAltar)
            {
                player.MagnetBurstFullMap = true;
                player.TemporaryPickupRadiusMul = 1f;
            }
            else
            {
                player.TemporaryPickupRadiusMul = Mathf.Max(player.TemporaryPickupRadiusMul, pickupRadiusMul);
            }
            player.MagnetBurstRemaining = Mathf.Max(player.MagnetBurstRemaining, duration);
            ForceAttractAllCrystals();
            var detail = fromAltar
                ? $"生命 -{hpCost:0.#}%，全图吸附经验晶体"
                : $"拾取范围 x{pickupRadiusMul:0.#}，立即吸附场上晶体";
            RegisterPickupNotice("magnet_burst", title, detail, player.MagnetBurstRemaining,
                PickupNoticeSync.MagnetBurst);
        }

        private void ForceAttractAllCrystals()
        {
            for (var i = crystals.Count - 1; i >= 0; i--)
            {
                var crystal = crystals[i];
                crystal.Attracting = true;
                var distance = Vector2.Distance(player.Position, crystal.Position);
                if (distance <= player.CollisionRadius + 0.35f)
                {
                    experience.Add(Mathf.Max(1, Mathf.RoundToInt(crystal.Value * player.ExperienceMultiplier)));
                    crystal.Active = false;
                    crystal.Attracting = false;
                    crystalPool.Release(crystal.View);
                    crystalActive--;
                    crystals.RemoveAt(i);
                    continue;
                }

                crystal.Position = Vector2.MoveTowards(crystal.Position, player.Position,
                    Mathf.Max(2.5f, distance * 0.55f) * 0.9f);
                crystal.View.transform.position = crystal.Position;
            }
        }

        private void RegisterPickupNotice(string effectKey, string title, string detail, float duration,
            PickupNoticeSync sync = PickupNoticeSync.None)
        {
            if (sync != PickupNoticeSync.None)
            {
                foreach (var existing in pickupNotices)
                {
                    if (existing.Sync != sync || !string.Equals(existing.EffectKey, effectKey, StringComparison.Ordinal))
                        continue;
                    existing.Title = title;
                    existing.Detail = detail;
                    existing.Remaining = Mathf.Max(0.1f, duration);
                    return;
                }
            }

            pickupNotices.Add(new PickupNoticeRuntime
            {
                InstanceId = sync != PickupNoticeSync.None ? effectKey : $"{effectKey}_{++noticeSerial}",
                EffectKey = effectKey,
                Title = title,
                Detail = detail,
                Remaining = Mathf.Max(0.1f, duration),
                Sync = sync
            });
        }

        private void TickPickupNotices(float dt)
        {
            for (var i = pickupNotices.Count - 1; i >= 0; i--)
            {
                var notice = pickupNotices[i];
                notice.Remaining = notice.Sync switch
                {
                    PickupNoticeSync.Scooter => player.ScooterRemaining,
                    PickupNoticeSync.Sniper => player.SniperRemaining,
                    PickupNoticeSync.CrateGuide => player.CrateGuideRemaining,
                    PickupNoticeSync.CapsuleFootball => player.CapsuleFootballRemaining,
                    PickupNoticeSync.DamageBonus => player.BloodPactRemaining > 0f
                        ? player.BloodPactRemaining
                        : player.TemporaryDamageBonus > 0.01f ? player.TemporaryDamageBonus / 0.01f : 0f,
                    PickupNoticeSync.MoveSpeed => player.TemporaryMoveMultiplier > 1.01f
                        ? (player.TemporaryMoveMultiplier - 1f) / 0.08f : 0f,
                    PickupNoticeSync.MagnetBurst => player.MagnetBurstRemaining,
                    PickupNoticeSync.BossStun => MaxBossStunRemaining(),
                    PickupNoticeSync.EnemySlow => player.EnemySlowRemaining,
                    _ => notice.Remaining - dt
                };
                if (notice.Remaining <= 0.05f)
                    pickupNotices.RemoveAt(i);
            }
        }

        private CrateEffectConfig WeightedCrateEffect(List<CrateEffectConfig> effects,
            Dictionary<string, float> weightOverrides = null)
        {
            if (effects == null || effects.Count == 0) return null;
            weightOverrides ??= session.Settings.CrateEffectWeights;
            var total = 0f;
            foreach (var effect in effects)
            {
                var weight = weightOverrides != null && weightOverrides.TryGetValue(effect.Id, out var overrideWeight)
                    ? overrideWeight : effect.Weight;
                total += Mathf.Max(0f, weight);
            }
            if (total <= 0f) return effects[0];
            var roll = random.Range(0f, total);
            foreach (var effect in effects)
            {
                var weight = weightOverrides != null && weightOverrides.TryGetValue(effect.Id, out var overrideWeight)
                    ? overrideWeight : effect.Weight;
                roll -= Mathf.Max(0f, weight);
                if (roll <= 0f) return effect;
            }
            return effects[^1];
        }

        private EnemyRuntime FindNearestEnemy(Vector2 position, float radius, HashSet<EnemyRuntime> excluded = null)
        {
            EnemyRuntime best = null;
            var bestDistance = radius * radius;
            foreach (var enemy in enemies)
            {
                if (!enemy.Active || excluded?.Contains(enemy) == true) continue;
                var distance = Vector2.SqrMagnitude(enemy.Position - position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best;
        }

        private void CreatePools()
        {
            enemyPool = new RuntimePool("EnemyPool", transform,
                () => RuntimeSpriteFactory.CreateSpriteView("Enemy", RuntimeSpriteFactory.White, Color.white, 0), 80);
            projectilePool = new RuntimePool("ProjectilePool", transform,
                () => RuntimeSpriteFactory.CreateSpriteView("Projectile",
                    WeaponArtCatalog.LoadBattle("wind_blade") ?? RuntimeSpriteFactory.White,
                    Color.white, 20), 120);
            var crystalSprite = MapArtCatalog.LoadPickup("experience_crystal");
            var crystalColor = crystalSprite != null ? Color.white : new Color(0.4f, 1f, 0.7f);
            crystalPool = new RuntimePool("CrystalPool", transform,
                () => RuntimeSpriteFactory.CreateSpriteView("Crystal", crystalSprite ?? RuntimeSpriteFactory.White, crystalColor, 5), 100);
            zonePool = new RuntimePool("GroundZonePool", transform,
                () => RuntimeSpriteFactory.CreateSpriteView("GroundZone",
                    WeaponArtCatalog.LoadBattle("fire_zone") ?? RuntimeSpriteFactory.Circle,
                    new Color(1f, 1f, 1f, 0.28f), -20), 12);
            eventPool = new RuntimePool("MapEventPool", transform,
                CreateMapEventView, 32);
            knifePool = new RuntimePool("KnifeOrbitPool", transform,
                () => FxSpriteFactory.CreateSpriteView("Knife",
                    WeaponArtCatalog.LoadBattle("rotating_knife") ?? FxSpriteFactory.Knife,
                    Color.white, 15), 8);
            dronePool = new RuntimePool("DroneOrbitPool", transform,
                () => FxSpriteFactory.CreateSpriteView("Drone",
                    WeaponArtCatalog.LoadBattle("drone") ?? FxSpriteFactory.Drone,
                    Color.white, 16), 8);
        }

        private static GameObject CreateMapEventView()
        {
            var root = RuntimeSpriteFactory.CreateSpriteView("MapEvent", RuntimeSpriteFactory.Circle, Color.white, -10);
            GetOrCreateMapChild(root, "Aura", 1);
            GetOrCreateMapChild(root, "FogInner", 2);
            for (var i = 0; i < 5; i++) GetOrCreateMapChild(root, $"SmokePuff{i}", 3 + i);
            return root;
        }

        private void CreateMap()
        {
            mapHalfSize = new Vector2(WorldScale.ToUnits(stage.MapWidth), WorldScale.ToUnits(stage.MapHeight)) * 0.5f;
            obstacles.Clear();
            waterObstacles.Clear();
            CreateGrassTiles();
            CreateInteriorEnvironment();
        }

        private void CreateGrassTiles()
        {
            var skinId = GameSettings.NormalizeMapSkinId(session.Settings.MapSkinId);
            var worldWidth = mapHalfSize.x * 2f;
            var worldHeight = mapHalfSize.y * 2f;
            var columns = Mathf.CeilToInt(worldWidth / GrassTileSize);
            var rows = Mathf.CeilToInt(worldHeight / GrassTileSize);

            if (MapLayoutCatalog.UsesAuthoredLayout(skinId) &&
                columns == MapLayoutCatalog.DryHighlandCoastColumns &&
                rows == MapLayoutCatalog.DryHighlandCoastRows)
            {
                CreateAuthoredMapTiles(skinId, columns, rows);
                return;
            }

            var sprite = MapArtCatalog.LoadTile(skinId) ?? MapArtCatalog.LoadTile("grass_tile_01");
            var fallbackColor = new Color(0.27f, 0.52f, 0.21f);

            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var width = Mathf.Min(GrassTileSize, worldWidth - x * GrassTileSize);
                var height = Mathf.Min(GrassTileSize, worldHeight - y * GrassTileSize);
                var tile = RuntimeSpriteFactory.CreateSpriteView("GrassTile", sprite ?? RuntimeSpriteFactory.White,
                    sprite != null ? Color.white : fallbackColor, -2000);
                tile.transform.SetParent(transform, false);
                tile.transform.position = new Vector2(
                    -mapHalfSize.x + x * GrassTileSize + width * 0.5f,
                    -mapHalfSize.y + y * GrassTileSize + height * 0.5f);
                RuntimeSpriteFactory.SetWorldSize(tile, width, height);
            }
        }

        private void CreateAuthoredMapTiles(string skinId, int columns, int rows)
        {
            var worldWidth = mapHalfSize.x * 2f;
            var worldHeight = mapHalfSize.y * 2f;
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var terrain = MapLayoutCatalog.GetTerrain(skinId, x, y);
                var width = Mathf.Min(GrassTileSize, worldWidth - x * GrassTileSize);
                var height = Mathf.Min(GrassTileSize, worldHeight - y * GrassTileSize);
                var tile = RuntimeSpriteFactory.CreateSpriteView("MapTile",
                    MapArtCatalog.LoadTile(MapLayoutCatalog.GetTileKey(terrain)) ?? RuntimeSpriteFactory.White,
                    TerrainFallbackColor(terrain), -2000);
                tile.transform.SetParent(transform, false);
                tile.transform.position = new Vector2(
                    -mapHalfSize.x + x * GrassTileSize + width * 0.5f,
                    -mapHalfSize.y + y * GrassTileSize + height * 0.5f);
                RuntimeSpriteFactory.SetWorldSize(tile, width, height);

                if (!MapLayoutCatalog.IsWalkable(terrain))
                {
                    var waterRect = new Rect(tile.transform.position.x - width * 0.5f,
                        tile.transform.position.y - height * 0.5f, width, height);
                    waterObstacles.Add(waterRect);
                    obstacles.Add(waterRect);
                }
            }
        }

        private static Color TerrainFallbackColor(MapTerrainKind terrain) => terrain switch
        {
            MapTerrainKind.Sandstone => new Color(0.63f, 0.39f, 0.22f),
            MapTerrainKind.Gravel => new Color(0.55f, 0.55f, 0.51f),
            MapTerrainKind.Path => new Color(0.68f, 0.51f, 0.31f),
            MapTerrainKind.Shore => new Color(0.76f, 0.69f, 0.52f),
            MapTerrainKind.Water => new Color(0.18f, 0.34f, 0.42f),
            _ => new Color(0.55f, 0.47f, 0.27f)
        };

        private void CreateInteriorEnvironment()
        {
            var placements = MapLayoutCatalog.UsesAuthoredLayout(session.Settings.MapSkinId)
                ? DryHighlandEnvironmentPlacements
                : InteriorEnvironmentPlacements;
            foreach (var placement in placements)
            {
                var position = new Vector2(
                    placement.NormalizedPosition.x * mapHalfSize.x / 32f,
                    placement.NormalizedPosition.y * mapHalfSize.y / 24f);
                CreateEnvironmentView("Environment", MapArtCatalog.LoadEnvironment(placement.SpriteKey), position,
                    placement.VisualSize, placement.CollisionSize, false);
            }
        }

        private void CreateEnvironmentView(string name, Sprite sprite, Vector2 position, Vector2 visualSize,
            Vector2 collisionSize, bool rotateVertical)
        {
            obstacles.Add(new Rect(position.x - collisionSize.x * 0.5f, position.y - collisionSize.y * 0.5f,
                collisionSize.x, collisionSize.y));
            var view = RuntimeSpriteFactory.CreateSpriteView(name, sprite ?? RuntimeSpriteFactory.Circle,
                sprite != null ? Color.white : new Color(0.13f, 0.28f, 0.12f), 0);
            view.transform.SetParent(transform, false);
            view.transform.position = position;
            if (rotateVertical) view.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            RuntimeSpriteFactory.SetWorldSize(view, visualSize.x, visualSize.y);
            RuntimeSpriteFactory.UpdateDepth(view, position.y);
        }

        private void CreatePlayer()
        {
            var sprite = Resources.Load<Sprite>($"Models/Characters/{System.IO.Path.GetFileNameWithoutExtension(skin.ModelAsset)}");
            var view = RuntimeSpriteFactory.CreateSpriteView("Player", sprite, Color.white, 10);
            view.transform.SetParent(transform, false);
            RuntimeSpriteFactory.SetWorldSize(view,
                0.48f * character.Scale * ActorVisualScaleMultiplier,
                0.62f * character.Scale * ActorVisualScaleMultiplier);
            RuntimeSpriteFactory.AddShadow(view, 0.42f);
            player = new PlayerRuntime
            {
                View = view,
                Position = Vector2.zero,
                Hp = character.MaxHp,
                MaxHp = character.MaxHp,
                Armor = character.Armor,
                MoveSpeed = WorldScale.ToUnits(character.MoveSpeed),
                PickupRadius = WorldScale.ToUnits(character.PickupRadius),
                CollisionRadius = WorldScale.ToUnits(character.CollisionRadius),
                DamageMultiplier = character.DamageMultiplier,
                AttackSpeedMultiplier = character.AttackSpeedMultiplier,
                CritRate = character.CritRate,
                CritDamage = character.CritDamage,
                ExperienceMultiplier = character.ExperienceMultiplier
            };
        }

        private void ConfigureEnemyView(GameObject view, EnemyConfig config)
        {
            // 普通怪 b2 / 精英怪 b5·b6 / 首领怪 b1·b3 / BOSS b4
            // 素材：Presentation/Resources/Models/Enemies
            string asset;
            if (config.Type == "boss") asset = "b4";
            else if (config.Type == "elite") asset = random.Value < 0.5f ? "b5" : "b6";
            else if (config.Type == "leader") asset = random.Value < 0.5f ? "b1" : "b3";
            else asset = "b2";
            var sprite = Resources.Load<Sprite>($"Models/Enemies/{asset}");
            var renderer = view.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : RuntimeSpriteFactory.White;
            renderer.color = Color.white;
            var height = WorldScale.ToUnits(config.CollisionRadius * 3f) * config.Scale * ActorVisualScaleMultiplier;
            RuntimeSpriteFactory.SetWorldSize(view, height * 0.72f, height);
        }

        private void BindEnemyHpBar(EnemyRuntime enemy)
        {
            if (enemy?.View == null) return;
            if (enemy.IsBoss)
            {
                if (enemy.HpBarRoot != null) enemy.HpBarRoot.SetActive(false);
                var existing = enemy.View.transform.Find("HpBar");
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            var rootTransform = enemy.View.transform.Find("HpBar");
            GameObject root;
            GameObject fill;
            if (rootTransform == null)
            {
                root = new GameObject("HpBar");
                root.transform.SetParent(enemy.View.transform, false);
                var bg = RuntimeSpriteFactory.CreateSpriteView("HpBarBg", RuntimeSpriteFactory.White,
                    new Color(0.08f, 0.08f, 0.1f, 0.85f), 40);
                bg.transform.SetParent(root.transform, false);
                fill = RuntimeSpriteFactory.CreateSpriteView("HpBarFill", RuntimeSpriteFactory.White,
                    new Color(0.86f, 0.22f, 0.28f, 0.95f), 41);
                fill.transform.SetParent(root.transform, false);
            }
            else
            {
                root = rootTransform.gameObject;
                fill = rootTransform.Find("HpBarFill")?.gameObject;
                if (fill == null)
                {
                    fill = RuntimeSpriteFactory.CreateSpriteView("HpBarFill", RuntimeSpriteFactory.White,
                        new Color(0.86f, 0.22f, 0.28f, 0.95f), 41);
                    fill.transform.SetParent(root.transform, false);
                }
            }

            enemy.HpBarRoot = root;
            enemy.HpBarFill = fill;
            root.SetActive(true);
            UpdateEnemyHpBar(enemy);
        }

        private void UpdateEnemyHpBar(EnemyRuntime enemy)
        {
            if (enemy == null || enemy.View == null) return;
            if (enemy.IsBoss)
            {
                if (enemy.HpBarRoot != null) enemy.HpBarRoot.SetActive(false);
                return;
            }

            if (enemy.HpBarRoot == null || enemy.HpBarFill == null)
            {
                BindEnemyHpBar(enemy);
                if (enemy.HpBarRoot == null || enemy.HpBarFill == null) return;
            }

            enemy.HpBarRoot.SetActive(true);
            var visualHeight = enemy.View.TryGetComponent<SpriteRenderer>(out var body)
                ? body.bounds.size.y
                : enemy.Radius * 2f;
            // Parented to enemy view — counteract body scale so bar stays readable.
            var parentScale = enemy.View.transform.lossyScale;
            var localY = (visualHeight * 0.5f + 0.07f) / Mathf.Max(0.0001f, parentScale.y);
            enemy.HpBarRoot.transform.localPosition = new Vector3(0f, localY, 0f);
            enemy.HpBarRoot.transform.localRotation = Quaternion.identity;
            enemy.HpBarRoot.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, parentScale.x),
                1f / Mathf.Max(0.0001f, parentScale.y),
                1f);

            const float barWidth = 0.48f;
            const float barHeight = 0.055f;
            var ratio = enemy.MaxHp > 0.001f ? Mathf.Clamp01(enemy.Hp / enemy.MaxHp) : 0f;
            var bg = enemy.HpBarRoot.transform.Find("HpBarBg")?.gameObject;
            if (bg != null)
            {
                RuntimeSpriteFactory.SetWorldSize(bg, barWidth, barHeight);
                bg.transform.localPosition = Vector3.zero;
                RuntimeSpriteFactory.UpdateDepth(bg, enemy.Position.y, 35);
            }

            var fillWidth = Mathf.Max(0.001f, barWidth * ratio);
            RuntimeSpriteFactory.SetWorldSize(enemy.HpBarFill, fillWidth, barHeight);
            enemy.HpBarFill.transform.localPosition = new Vector3(-barWidth * 0.5f + fillWidth * 0.5f, 0f, 0f);
            if (enemy.HpBarFill.TryGetComponent<SpriteRenderer>(out var fillRenderer))
            {
                fillRenderer.color = enemy.Config.Type == "elite" || enemy.Config.Type == "leader"
                    ? new Color(1f, 0.55f, 0.18f, 0.95f)
                    : new Color(0.86f, 0.22f, 0.28f, 0.95f);
            }
            RuntimeSpriteFactory.UpdateDepth(enemy.HpBarFill, enemy.Position.y, 36);
        }

        private void SpawnMapEvents()
        {
            var config = stage.MapEvents;
            for (var i = 0; i < session.Settings.CrateCount; i++)
                SpawnMapEvent(MapEventKind.Crate, RandomMapPosition(), WorldScale.ToUnits(config.CrateInteractRadius),
                    new Color(0.9f, 0.65f, 0.18f));
            for (var i = 0; i < session.Settings.HiddenCrateCount; i++)
                SpawnMapEvent(MapEventKind.HiddenCrate, RandomMapPosition(), WorldScale.ToUnits(config.CrateInteractRadius),
                    new Color(0.9f, 0.65f, 0.18f, 0.08f));
            for (var i = 0; i < session.Settings.AltarCount; i++)
                SpawnMapEvent(MapEventKind.Altar, RandomMapPosition(), WorldScale.ToUnits(config.AltarInteractRadius),
                    new Color(0.58f, 0.25f, 0.8f));
            for (var i = 0; i < session.Settings.HealingChickenCount; i++)
                SpawnMapEvent(MapEventKind.HealingChicken, RandomMapPosition(),
                    WorldScale.ToUnits(Mathf.Max(16f, config.HealingChickenInteractRadius)), Color.white);
            for (var i = 0; i < session.Settings.PoisonFogCount; i++) SpawnPoisonFog(RandomMapPosition());
        }

        private void SpawnPoisonFog(Vector2 position)
        {
            SpawnMapEvent(MapEventKind.PoisonFog, position,
                WorldScale.ToUnits(random.Range(session.Settings.PoisonFogRadiusMin, session.Settings.PoisonFogRadiusMax)),
                new Color(0.38f, 0.72f, 0.22f, 0.2f));
        }

        private Vector2 RandomMapPosition()
        {
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var point = new Vector2(random.Range(-mapHalfSize.x + 2f, mapHalfSize.x - 2f),
                    random.Range(-mapHalfSize.y + 2f, mapHalfSize.y - 2f));
                if (Vector2.Distance(point, player?.Position ?? Vector2.zero) > 2.5f &&
                    IsWalkablePosition(point, 0.55f))
                    return point;
            }

            return IsWalkablePosition(Vector2.zero, 0.55f) ? Vector2.zero :
                new Vector2(-mapHalfSize.x + 2f, -mapHalfSize.y + 2f);
        }

        private bool IsWalkablePosition(Vector2 position, float radius)
        {
            if (position.x < -mapHalfSize.x + radius || position.x > mapHalfSize.x - radius ||
                position.y < -mapHalfSize.y + radius || position.y > mapHalfSize.y - radius)
                return false;

            foreach (var obstacle in obstacles)
            {
                if (position.x >= obstacle.xMin - radius && position.x <= obstacle.xMax + radius &&
                    position.y >= obstacle.yMin - radius && position.y <= obstacle.yMax + radius)
                    return false;
            }

            return true;
        }

        private Vector2 ResolveMapCollision(Vector2 position, float radius)
        {
            position.x = Mathf.Clamp(position.x, -mapHalfSize.x + radius, mapHalfSize.x - radius);
            position.y = Mathf.Clamp(position.y, -mapHalfSize.y + radius, mapHalfSize.y - radius);
            foreach (var obstacle in obstacles)
            {
                var closest = new Vector2(Mathf.Clamp(position.x, obstacle.xMin, obstacle.xMax),
                    Mathf.Clamp(position.y, obstacle.yMin, obstacle.yMax));
                var delta = position - closest;
                if (delta.sqrMagnitude >= radius * radius) continue;
                if (delta.sqrMagnitude < 0.000001f)
                {
                    var left = Mathf.Abs(position.x - obstacle.xMin);
                    var right = Mathf.Abs(obstacle.xMax - position.x);
                    var bottom = Mathf.Abs(position.y - obstacle.yMin);
                    var top = Mathf.Abs(obstacle.yMax - position.y);
                    var minimum = Mathf.Min(left, right, bottom, top);
                    if (minimum == left) position.x = obstacle.xMin - radius;
                    else if (minimum == right) position.x = obstacle.xMax + radius;
                    else if (minimum == bottom) position.y = obstacle.yMin - radius;
                    else position.y = obstacle.yMax + radius;
                }
                else
                {
                    position = closest + delta.normalized * radius;
                }
            }
            return position;
        }

        private void PublishSnapshot()
        {
            var bosses = CollectBossHudEntries();
            var effects = CollectEffectHudEntries();
            var primaryBoss = bosses.Length > 0 ? bosses[0] : default;
            var attackMultiplier = player.DamageMultiplier + player.TemporaryDamageBonus;
            var moveSpeedUnits = player.MoveSpeed * player.TemporaryMoveMultiplier *
                                 (player.ScooterRemaining > 0f ? 1.5f : 1f);
            SnapshotChanged?.Invoke(new BattleSnapshot(player.Hp, player.MaxHp, experience.Level,
                experience.Current, experience.RequiredForLevel(experience.Level), currentWave, waveCount,
                killCount, enemies.Count, primaryBoss.Hp, primaryBoss.MaxHp, bosses, effects,
                enemyPool.ActiveCount + projectileActive + crystalActive + zoneActive, fpsSmoothing,
                attackMultiplier, WorldScale.ToPixels(moveSpeedUnits)));
        }

        private BossHudEntry[] CollectBossHudEntries()
        {
            var count = 0;
            foreach (var enemy in enemies)
                if (enemy.Active && enemy.IsBoss) count++;
            if (count == 0) return System.Array.Empty<BossHudEntry>();

            var bosses = new BossHudEntry[count];
            var index = 0;
            foreach (var enemy in enemies)
            {
                if (!enemy.Active || !enemy.IsBoss) continue;
                var name = count > 1 ? $"变异巨尸 {index + 1}" : "变异巨尸";
                bosses[index++] = new BossHudEntry(name, enemy.Hp, enemy.MaxHp);
            }
            return bosses;
        }

        private EffectHudEntry[] CollectEffectHudEntries()
        {
            if (pickupNotices.Count == 0) return System.Array.Empty<EffectHudEntry>();
            var effects = new EffectHudEntry[pickupNotices.Count];
            for (var i = 0; i < pickupNotices.Count; i++)
            {
                var notice = pickupNotices[i];
                effects[i] = new EffectHudEntry(notice.InstanceId, notice.Title, notice.Detail, notice.Remaining);
            }
            return effects;
        }

        private void FinishBattle(bool victory)
        {
            if (ended) return;
            ended = true;
            stateMachine.Set(victory ? GameState.Victory : GameState.Defeat);
            AudioRequested?.Invoke(victory ? "victory" : "defeat");
            var result = new GameResultStats
            {
                Victory = victory,
                CharacterId = character.Id,
                CharacterName = character.Name,
                SkinId = skin.Id,
                SkinName = skin.Name,
                SurvivalTime = elapsed,
                KillCount = killCount,
                MaxLevel = experience.Level,
                TotalDamage = totalDamage,
                MaxSingleDamage = maxSingleDamage,
                BossKilled = bossKilled,
                TotalExperience = experience.Total,
                Skills = upgrades.Values.Select(value => new SkillResult
                {
                    Id = value.Id, Name = value.Name, Level = value.Level
                }).ToList()
            };
            BattleEnded?.Invoke(result);
        }

        private void OnStateChanged(GameState next)
        {
            StateChanged?.Invoke(next);
        }

        private void OnDestroy()
        {
            if (stateMachine != null) stateMachine.Changed -= OnStateChanged;
            input?.Dispose();
            if (knifePool != null) EnsureOrbitCount(knifeOrbits, knifePool, 0, true);
            if (dronePool != null) EnsureOrbitCount(droneOrbits, dronePool, 0, false);
            if (fuboQinAura?.Root != null) Destroy(fuboQinAura.Root);
            fuboQinAura = null;
            if (skillFx != null)
            {
                for (var i = zones.Count - 1; i >= 0; i--) ReleaseZone(zones[i]);
                zones.Clear();
                skillFx.Destroy();
                skillFx = null;
            }
            if (player?.ScooterView != null) Destroy(player.ScooterView);
            if (player?.SniperView != null) Destroy(player.SniperView);
            if (player?.CapsuleFootballView != null) Destroy(player.CapsuleFootballView);
            supplyCratePositions.Clear();
            enemyPool?.DestroyAll();
            projectilePool?.DestroyAll();
            crystalPool?.DestroyAll();
            zonePool?.DestroyAll();
            eventPool?.DestroyAll();
            knifePool?.DestroyAll();
            dronePool?.DestroyAll();
        }
    }
}
