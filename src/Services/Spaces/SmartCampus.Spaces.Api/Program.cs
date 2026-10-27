using System.Text.Json.Serialization;
using SmartCampus.Spaces.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddSpacesMongoDb(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Spaces API v1"));
}

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Rende Program visibile ai progetti di test (test di integrazione)
public partial class Program { }