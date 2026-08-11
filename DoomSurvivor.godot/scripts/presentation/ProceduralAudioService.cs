using System;
using Godot;

namespace DoomSurvivor.Presentation;

public partial class ProceduralAudioService : Node
{
    private AudioStreamPlayer? player;
    private AudioStreamGeneratorPlayback? playback;

    public float Volume { get; set; } = 0.7f;

    public void SetVolume(float value)
    {
        Volume = Math.Clamp(value, 0f, 1f);
        if (player is not null) player.VolumeDb = ToDb(Volume);
    }

    public override void _Ready()
    {
        try
        {
            player = new AudioStreamPlayer
            {
                Stream = new AudioStreamGenerator
                {
                    MixRate = 22050,
                    BufferLength = 0.25f
                },
                VolumeDb = ToDb(Volume)
            };
            AddChild(player);
            player.Play();
            playback = player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[Audio] Procedural audio unavailable: {exception.Message}");
        }
    }

    public void PlayCue(string cue)
    {
        if (playback is null) return;
        var (frequency, duration, amplitude) = cue switch
        {
            "level_up" => (880f, 0.18f, 0.16f),
            "pause" => (330f, 0.1f, 0.12f),
            "result" => (520f, 0.28f, 0.14f),
            "boss" => (110f, 0.36f, 0.18f),
            _ => (440f, 0.08f, 0.1f)
        };
        var frames = Math.Max(1, (int)(22050f * duration));
        for (var index = 0; index < frames; index++)
        {
            var envelope = 1f - index / (float)frames;
            var sample = MathF.Sin(MathF.Tau * frequency * index / 22050f) * amplitude * envelope;
            playback.PushFrame(new Vector2(sample, sample));
        }
    }

    private static float ToDb(float linear)
    {
        return linear <= 0.0001f ? -80f : 20f * MathF.Log10(linear);
    }
}
