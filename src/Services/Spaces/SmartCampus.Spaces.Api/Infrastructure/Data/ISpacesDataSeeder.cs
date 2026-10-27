namespace SmartCampus.Spaces.Api.Infrastructure.Data;

public interface ISpacesDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
