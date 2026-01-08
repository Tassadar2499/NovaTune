namespace NovaTuneApp.Tests;

[CollectionDefinition("Integration Tests", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestsApiFactory> { }
