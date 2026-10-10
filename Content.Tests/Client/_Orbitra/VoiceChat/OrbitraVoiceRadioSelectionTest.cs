using Content.Client._Orbitra.UserInterface;
using NUnit.Framework;

namespace Content.Tests.Client._Orbitra.VoiceChat;

[TestFixture]
public sealed class OrbitraVoiceRadioSelectionTest
{
    [Test]
    public void ExplicitDepartmentWinsWhenRadioIsSelected()
    {
        Assert.That(OrbitraRadioChannelSelection.Resolve("Security", "Common", true), Is.EqualTo("Security"));
    }

    [Test]
    public void DefaultChannelIsUsedWhenRadioSelectionHasNoExplicitPrefix()
    {
        Assert.That(OrbitraRadioChannelSelection.Resolve(null, "Engineering", true), Is.EqualTo("Engineering"));
    }

    [Test]
    public void NonRadioSelectionHasNoVoiceChannel()
    {
        Assert.That(OrbitraRadioChannelSelection.Resolve("Security", "Common", false), Is.Null);
    }

    [Test]
    public void MissingDefaultAndExplicitChannelClearsSelection()
    {
        Assert.That(OrbitraRadioChannelSelection.Resolve(null, null, true), Is.Null);
    }
}
