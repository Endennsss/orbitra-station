namespace Content.IntegrationTests;

[SetUpFixture]
public sealed class PoolManagerTestEventHandler
{
    [OneTimeSetUp]
    public void Setup()
    {
        PoolManager.Startup();
        // Orbitra added start - даём медленным CI-шардам завершиться в пределах таймаута workflow.
        var configuredTimeout = Environment.GetEnvironmentVariable("ORBITRA_INTEGRATION_TIMEOUT_MINUTES");
        var maximumTotalTestingTime = TimeSpan.FromMinutes(
            int.TryParse(configuredTimeout, out var minutes) && minutes is > 0 and <= 120 ? minutes : 20);
        // Orbitra added end
        // If the tests seem to be stuck, we try to end it semi-nicely
        _ = Task.Delay(maximumTotalTestingTime).ContinueWith(_ => // Orbitra-Edit
        {
            // This can and probably will cause server/client pairs to shut down MID test, and will lead to really confusing test failures.
            TestContext.Error.WriteLine($"\n\n{nameof(PoolManagerTestEventHandler)}: ERROR: Tests are taking too long. Shutting down all tests. This may lead to weird failures/exceptions.\n\n");
            PoolManager.Shutdown();
        });

        // If ending it nicely doesn't work within a minute, we do something a bit meaner.
        _ = Task.Delay(maximumTotalTestingTime.Add(TimeSpan.FromMinutes(1))).ContinueWith(_ => // Orbitra-Edit
        {
            var deathReport = PoolManager.DeathReport();
            Environment.FailFast($"Tests took way too ;\n Death Report:\n{deathReport}");
        });
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        PoolManager.Shutdown();
    }
}
