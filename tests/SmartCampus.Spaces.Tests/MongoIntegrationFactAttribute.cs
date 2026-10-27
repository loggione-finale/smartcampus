namespace SmartCampus.Spaces.Tests;

public sealed class MongoIntegrationFactAttribute : FactAttribute
{
    public const string ConnectionStringEnvVar = "ConnectionStrings__SpacesTest";

    public MongoIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvVar)))
        {
            Skip = $"Set '{ConnectionStringEnvVar}' to run real MongoDB integration tests.";
        }
    }
}
