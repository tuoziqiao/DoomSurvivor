using System;

namespace DoomSurvivor.Application;

public enum PresentationScreen
{
    Bootstrap,
    MainMenu,
    Battle,
    Result
}

public sealed class PresentationFlow
{
    public PresentationScreen Current { get; private set; } = PresentationScreen.Bootstrap;
    public event Action<PresentationScreen>? Changed;

    public void GoTo(PresentationScreen next)
    {
        if (Current == next) return;
        Current = next;
        Changed?.Invoke(next);
    }
}
