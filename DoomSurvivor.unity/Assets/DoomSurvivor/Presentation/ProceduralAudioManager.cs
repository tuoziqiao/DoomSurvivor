using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoomSurvivor.Presentation
{
    public sealed class ProceduralAudioManager : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> lastPlayed = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> cueVolumes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> cueCooldowns = new(StringComparer.Ordinal);
        private readonly List<AudioSource> sources = new(10);
        private int nextSource;
        private AudioSource musicSource;
        private readonly System.Random pitchRandom = new();

        public static ProceduralAudioManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            CreateSources();
            CreateClips();
            CreateMusicSource();
        }

        private void Update()
        {
            if (musicSource == null)
                return;

            var settings = AppRoot.Instance?.Session.Settings;
            musicSource.volume = settings == null ? 0.12f : settings.MasterVolume * settings.MusicVolume * 0.25f;
            var shouldPlay = SceneManager.GetActiveScene().name == "Battle" && musicSource.volume > 0.001f;
            if (shouldPlay && !musicSource.isPlaying)
                musicSource.Play();
            else if (!shouldPlay && musicSource.isPlaying)
                musicSource.Stop();
        }

        public void Play(string cue)
        {
            if (string.IsNullOrWhiteSpace(cue) || !clips.TryGetValue(cue, out var clip)) return;
            var cooldown = cueCooldowns.TryGetValue(cue, out var customCooldown) ? customCooldown : 0.03f;
            if (lastPlayed.TryGetValue(cue, out var last) && Time.unscaledTime - last < cooldown) return;
            lastPlayed[cue] = Time.unscaledTime;

            var source = sources[nextSource++ % sources.Count];
            source.Stop();
            source.clip = clip;
            var settings = AppRoot.Instance?.Session.Settings;
            var master = settings == null ? 0.7f : settings.MasterVolume * settings.SfxVolume;
            var cueGain = cueVolumes.TryGetValue(cue, out var gain) ? gain : 1f;
            source.volume = Mathf.Clamp01(master * cueGain);
            source.pitch = cue switch
            {
                "lightning_chain" => 0.92f + (float)pitchRandom.NextDouble() * 0.22f,
                "fire_bottle" => 0.97f + (float)pitchRandom.NextDouble() * 0.08f,
                "sniper_shot" => 0.96f + (float)pitchRandom.NextDouble() * 0.1f,
                "upgrade_select" => 0.98f + (float)pitchRandom.NextDouble() * 0.08f,
                _ => 1f
            };
            source.Play();
        }

        private void CreateSources()
        {
            for (var i = 0; i < 10; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                sources.Add(source);
            }
        }

        private void CreateClips()
        {
            Register("fire_bottle", BuildFireBottle(), 0.78f, 0.06f);
            Register("lightning_chain", BuildLightning(), 0.8f, 0.045f);
            Register("sniper_shot", BuildSniperShot(), 0.9f, 0.05f);
            Register("level_up", BuildLevelUp(), 0.75f, 0.12f);
            Register("upgrade_select", BuildUpgradeSelect(), 0.7f, 0.04f);
            Register("crate", BuildCrateOpen(), 0.72f, 0.1f);
            Register("altar", BuildAltar(), 0.78f, 0.12f);
            Register("boss_intro", BuildBossIntro(), 0.88f, 0.2f);
            Register("boss_defeat", BuildBossDefeat(), 0.9f, 0.15f);
            Register("victory", BuildVictory(), 0.8f, 0.2f);
            Register("defeat", BuildDefeat(), 0.82f, 0.2f);
        }

        private void Register(string name, AudioClip clip, float volume, float cooldown)
        {
            clips[name] = clip;
            cueVolumes[name] = volume;
            cueCooldowns[name] = cooldown;
        }

        private static AudioClip BuildFireBottle()
        {
            // Bottle uncork / glass open — short pickup-style cue (not a throw boom).
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.18f * sampleRate);
            var data = new float[count];
            var random = new System.Random(1701);
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;

                // Cork pop: quick high transient that drops.
                var cork = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(2400f, 900f, Mathf.Clamp01(progress * 6f)) * t) *
                           Mathf.Exp(-progress * 28f) * 0.55f;

                // Glass rim clink.
                var glass = Mathf.Sin(2f * Mathf.PI * 1850f * t) * SoftPulse(progress, 0.02f, 0.22f) * 0.28f +
                            Mathf.Sin(2f * Mathf.PI * 2780f * t) * SoftPulse(progress, 0.03f, 0.18f) * 0.14f;

                // Soft hollow bottle body.
                var body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 180f, progress) * t) *
                           SoftPulse(progress, 0.04f, 0.55f) * 0.22f;

                // Brief liquid / air hiss after the pop.
                var hiss = BandNoise(random, 0.22f) * SoftPulse(progress, 0.05f, 0.45f) * 0.2f +
                           Mathf.Sin(2f * Mathf.PI * 110f * t) * SoftPulse(progress, 0.08f, 0.5f) * 0.08f;

                data[i] = Mathf.Clamp(cork + glass + body + hiss, -1f, 1f);
            }
            return CreateClip("SFX_fire_bottle", data, sampleRate);
        }

        private static AudioClip BuildLightning()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.2f * sampleRate);
            var data = new float[count];
            var random = new System.Random(2207);
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var zap = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(2100f, 780f, progress) * t) *
                          Mathf.Exp(-progress * 11f) * 0.48f;
                var spark = BandNoise(random, 0.55f) * Mathf.Exp(-progress * 8f) * 0.5f;
                var body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(360f, 140f, progress) * t) *
                           SoftPulse(progress, 0.02f, 0.55f) * 0.28f;
                var tick = Mathf.Sin(2f * Mathf.PI * 3200f * t) *
                           SoftPulse(progress, 0.0f, 0.08f) * 0.35f;
                data[i] = Mathf.Clamp(zap + spark + body + tick, -1f, 1f);
            }
            return CreateClip("SFX_lightning_chain", data, sampleRate);
        }

        private static AudioClip BuildSniperShot()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.22f * sampleRate);
            var data = new float[count];
            var random = new System.Random(991);
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var crack = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1650f, 420f, progress) * t) *
                            Mathf.Exp(-progress * 18f) * 0.62f;
                var body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 70f, progress) * t) *
                           SoftPulse(progress, 0.0f, 0.35f) * 0.4f;
                var tail = BandNoise(random, 0.2f) * Mathf.Exp(-progress * 6f) * 0.18f;
                data[i] = Mathf.Clamp(crack + body + tail, -1f, 1f);
            }
            return CreateClip("SFX_sniper_shot", data, sampleRate);
        }

        private static AudioClip BuildLevelUp()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.42f * sampleRate);
            var data = new float[count];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var noteIndex = Mathf.Clamp(Mathf.FloorToInt(progress * notes.Length), 0, notes.Length - 1);
                var local = (progress * notes.Length) % 1f;
                var tone = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * t) * SoftPulse(local, 0.0f, 0.9f) * 0.42f;
                var shimmer = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * 2f * t) * SoftPulse(local, 0.1f, 0.7f) * 0.12f;
                data[i] = Mathf.Clamp((tone + shimmer) * (1f - progress * 0.35f), -1f, 1f);
            }
            return CreateClip("SFX_level_up", data, sampleRate);
        }

        private static AudioClip BuildUpgradeSelect()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.12f * sampleRate);
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(880f, 1320f, progress) * t) *
                           Mathf.Exp(-progress * 8f) * 0.45f;
                var click = Mathf.Sin(2f * Mathf.PI * 2400f * t) * SoftPulse(progress, 0f, 0.12f) * 0.2f;
                data[i] = Mathf.Clamp(tone + click, -1f, 1f);
            }
            return CreateClip("SFX_upgrade_select", data, sampleRate);
        }

        private static AudioClip BuildCrateOpen()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.24f * sampleRate);
            var data = new float[count];
            var random = new System.Random(540);
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var wood = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(260f, 140f, progress) * t) *
                           SoftPulse(progress, 0f, 0.45f) * 0.35f;
                var rattle = BandNoise(random, 0.35f) * SoftPulse(progress, 0.05f, 0.55f) * 0.28f;
                var chime = Mathf.Sin(2f * Mathf.PI * 980f * t) * SoftPulse(progress, 0.35f, 0.9f) * 0.22f;
                data[i] = Mathf.Clamp(wood + rattle + chime, -1f, 1f);
            }
            return CreateClip("SFX_crate", data, sampleRate);
        }

        private static AudioClip BuildAltar()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.36f * sampleRate);
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var hum = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 110f, progress) * t) *
                          SoftPulse(progress, 0f, 0.95f) * 0.38f;
                var bell = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(540f, 720f, progress) * t) *
                           SoftPulse(progress, 0.1f, 0.85f) * 0.28f;
                var overtone = Mathf.Sin(2f * Mathf.PI * 1080f * t) * SoftPulse(progress, 0.2f, 0.7f) * 0.1f;
                data[i] = Mathf.Clamp(hum + bell + overtone, -1f, 1f);
            }
            return CreateClip("SFX_altar", data, sampleRate);
        }

        private static AudioClip BuildBossIntro()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.7f * sampleRate);
            var data = new float[count];
            var random = new System.Random(75);
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var rumble = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 40f, progress) * t) *
                             SoftPulse(progress, 0f, 0.95f) * 0.55f;
                var roar = BandNoise(random, 0.28f) * SoftPulse(progress, 0.15f, 0.85f) * 0.32f;
                var sting = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 90f, progress) * t) *
                            SoftPulse(progress, 0.05f, 0.5f) * 0.22f;
                data[i] = Mathf.Clamp(rumble + roar + sting, -1f, 1f);
            }
            return CreateClip("SFX_boss_intro", data, sampleRate);
        }

        private static AudioClip BuildBossDefeat()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.55f * sampleRate);
            var data = new float[count];
            var random = new System.Random(330);
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var crash = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 55f, progress) * t) *
                            Mathf.Exp(-progress * 5f) * 0.5f;
                var debris = BandNoise(random, 0.4f) * Mathf.Exp(-progress * 4f) * 0.35f;
                var resolve = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(440f, 660f, progress) * t) *
                              SoftPulse(progress, 0.35f, 0.95f) * 0.2f;
                data[i] = Mathf.Clamp(crash + debris + resolve, -1f, 1f);
            }
            return CreateClip("SFX_boss_defeat", data, sampleRate);
        }

        private static AudioClip BuildVictory()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.7f * sampleRate);
            var data = new float[count];
            float[] notes = { 392f, 523.25f, 659.25f, 783.99f };
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var noteIndex = Mathf.Clamp(Mathf.FloorToInt(progress * notes.Length), 0, notes.Length - 1);
                var local = (progress * notes.Length) % 1f;
                var tone = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * t) * SoftPulse(local, 0f, 0.95f) * 0.4f;
                var fifth = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * 1.5f * t) * SoftPulse(local, 0.1f, 0.8f) * 0.12f;
                data[i] = Mathf.Clamp(tone + fifth, -1f, 1f);
            }
            return CreateClip("SFX_victory", data, sampleRate);
        }

        private static AudioClip BuildDefeat()
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(0.7f * sampleRate);
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)count;
                var tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 90f, progress) * t) *
                           SoftPulse(progress, 0f, 0.95f) * 0.42f;
                var low = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 55f, progress) * t) *
                          SoftPulse(progress, 0.1f, 0.9f) * 0.28f;
                data[i] = Mathf.Clamp(tone + low, -1f, 1f);
            }
            return CreateClip("SFX_defeat", data, sampleRate);
        }

        private void CreateMusicSource()
        {
            const int sampleRate = 22050;
            const int seconds = 8;
            var count = sampleRate * seconds;
            var data = new float[count];
            var random = new System.Random(1977);
            var filteredNoise = 0f;
            for (var i = 0; i < count; i++)
            {
                var time = i / (float)sampleRate;
                filteredNoise = Mathf.Lerp(filteredNoise, (float)random.NextDouble() * 2f - 1f, 0.0025f);
                var drone = Mathf.Sin(2f * Mathf.PI * 55f * time) * 0.48f +
                            Mathf.Sin(2f * Mathf.PI * 82.5f * time) * 0.22f;
                var pulse = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 0.125f * time);
                data[i] = (drone * pulse + filteredNoise * 0.24f) * 0.18f;
            }

            var clip = AudioClip.Create("Music_Wasteland_Ambience", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.clip = clip;
        }

        private static float SoftPulse(float progress, float start, float end)
        {
            if (progress < start || progress > end) return 0f;
            var local = Mathf.InverseLerp(start, end, progress);
            return Mathf.Sin(Mathf.PI * local);
        }

        private static float BandNoise(System.Random random, float amount) =>
            ((float)random.NextDouble() * 2f - 1f) * amount;

        private static AudioClip CreateClip(string name, float[] data, int sampleRate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
