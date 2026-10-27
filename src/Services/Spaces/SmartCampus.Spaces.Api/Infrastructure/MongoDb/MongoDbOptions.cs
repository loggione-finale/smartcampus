namespace SmartCampus.Spaces.Api.Infrastructure.MongoDb;

public class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "spaces";
    public string SpacesCollectionName { get; set; } = "spaces";
}
