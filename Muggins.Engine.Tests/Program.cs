namespace Muggins.Engine.Tests;

internal static class Program
{
    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            (nameof(EngineTests.RulesScoreAndFindLegalPlaysAsync), EngineTests.RulesScoreAndFindLegalPlaysAsync),
            (nameof(EngineTests.BotChoosesRequiredOpeningAsync), EngineTests.BotChoosesRequiredOpeningAsync),
            (nameof(EngineTests.ApiJsonAndBoardMappingAsync), EngineTests.ApiJsonAndBoardMappingAsync),
            (nameof(ApiIntegrationTests.CoordinatorSkipsSubmissionWhenWaitingAsync), ApiIntegrationTests.CoordinatorSkipsSubmissionWhenWaitingAsync),
            (nameof(ApiIntegrationTests.CoordinatorSubmitsWithVersionAndCredentialsAsync), ApiIntegrationTests.CoordinatorSubmitsWithVersionAndCredentialsAsync),
            (nameof(ApiIntegrationTests.Updates204ReturnsNullAsync), ApiIntegrationTests.Updates204ReturnsNullAsync),
            (nameof(ApiIntegrationTests.CoordinatorReturnsAuthoritativeStateOnConflictAsync), ApiIntegrationTests.CoordinatorReturnsAuthoritativeStateOnConflictAsync)
        };

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {test.Name}: {exception}");
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
        return failures == 0 ? 0 : 1;
    }
}
