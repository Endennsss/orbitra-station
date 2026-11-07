// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Client.Lathe.UI;
using Content.Client.Research.UI;
using Content.Client.Xenoarchaeology.Ui;
using Robust.Client.UserInterface.CustomControls;

namespace Content.IntegrationTests.Tests._Orbitra.Research;

/// <summary>Related native science dialogs retain the common presentation when opened from the imported interfaces.</summary>
[TestFixture, NonParallelizable]
public sealed class OrbitraScienceWindowStyleTest
{
    [Test]
    public async Task RelatedScienceDialogsUseOrbitraChrome()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        BaseWindow[] windows = [];
        try
        {
            await pair.Client.WaitPost(() =>
            {
                windows = [new LatheMenu(), new AnalysisConsoleMenu(), new DiskConsoleMenu(), new ResearchClientServerSelectionMenu()];
                foreach (var window in windows)
                    window.OpenCentered();
            });
            await pair.RunTicksSync(2);
            await pair.Client.WaitAssertion(() =>
            {
                foreach (var window in windows)
                {
                    Assert.That(window.HasStyleClass("OrbitraWindowChrome"), Is.True, window.GetType().Name);
                    Assert.That(window.HasStyleClass("OrbitraEntryWindow"), Is.True, window.GetType().Name);
                }
            });
        }
        finally
        {
            await pair.Client.WaitPost(() =>
            {
                foreach (var window in windows)
                    window.Dispose();
            });
        }
        await pair.CleanReturnAsync();
    }
}
