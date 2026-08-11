using DoomSurvivor.Core;

namespace DoomSurvivor.Net;

public interface ISessionService
{
    GameMode Mode { get; }
    void Create(GameMode mode);
    void Close();
}

public interface INetTransport
{
    bool IsConnected { get; }
    void Start();
    void Stop();
}

public sealed class LocalSessionService : ISessionService
{
    public GameMode Mode { get; private set; } = GameMode.SoloSurvivor;
    public void Create(GameMode mode) => Mode = mode;
    public void Close() { }
}

public sealed class NullNetTransport : INetTransport
{
    public bool IsConnected => false;
    public void Start() { }
    public void Stop() { }
}
