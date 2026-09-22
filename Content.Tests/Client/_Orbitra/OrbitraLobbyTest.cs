using Content.Client._Orbitra.Lobby;
using NUnit.Framework;

namespace Content.Tests.Client._Orbitra;

[TestFixture]
public sealed class OrbitraLobbyTest
{
    [TestCase(1, 1, 4)]
    [TestCase(4, 1, 9)]
    [TestCase(9, 1, 1)]
    [TestCase(1, -1, 9)]
    [TestCase(9, -1, 4)]
    public void NavigationSkipsEmptySlotsAndWraps(int selected, int step, int expected)
    {
        Assert.That(OrbitraLobbyPolicy.NextSlot([9, 1, 4], selected, step), Is.EqualTo(expected));
    }

    [Test]
    public void NoNavigationWithoutAlternative()
    {
        Assert.That(OrbitraLobbyPolicy.NextSlot([], 0, 1), Is.Null);
        Assert.That(OrbitraLobbyPolicy.NextSlot([4], 4, 1), Is.Null);
        Assert.That(OrbitraLobbyPolicy.NextSlot([1, 4], 1, 0), Is.Null);
    }

    [TestCase(1280, true, true)]
    [TestCase(1199, false, true)]
    [TestCase(900, false, true)]
    [TestCase(899, false, false)]
    public void ResponsivePanelsUseLogicalWidth(float width, bool info, bool chat)
    {
        Assert.That(OrbitraLobbyPolicy.DockInformation(width), Is.EqualTo(info));
        Assert.That(OrbitraLobbyPolicy.DockChat(width), Is.EqualTo(chat));
    }
}
