using DoomSurvivor.Application;

namespace DoomSurvivor.Tests.Application;

[TestClass]
public sealed class PresentationFlowTests
{
    [TestMethod]
    public void FlowEmitsOnlyWhenScreenChanges()
    {
        var flow = new PresentationFlow();
        var transitions = new List<PresentationScreen>();
        flow.Changed += screen => transitions.Add(screen);

        flow.GoTo(PresentationScreen.Bootstrap);
        flow.GoTo(PresentationScreen.MainMenu);
        flow.GoTo(PresentationScreen.Battle);
        flow.GoTo(PresentationScreen.Battle);
        flow.GoTo(PresentationScreen.Result);

        CollectionAssert.AreEqual(
            new[] { PresentationScreen.MainMenu, PresentationScreen.Battle, PresentationScreen.Result },
            transitions);
        Assert.AreEqual(PresentationScreen.Result, flow.Current);
    }
}
