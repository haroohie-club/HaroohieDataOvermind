using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using HaroohieDataOvermind.Models;
using LiteDB;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;

namespace HaroohieDataOvermind;

public class Program
{
    private const string AllowOrigins = "_allowOrigins";
    
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: AllowOrigins,
                policy =>
                {
                    policy.WithOrigins("https://*.haroohie.club", "http://localhost:3000") 
                        .WithMethods("GET", "POST");
                });
        });

        WebApplication app = builder.Build();
        app.UseCors(AllowOrigins);

        MongoClientSettings clientSettings = new()
        {
            Scheme = ConnectionStringScheme.MongoDB, 
            Server = new("localhost", 27017),
        };
        MongoClient mongo = new(clientSettings);
        IMongoDatabase chokuDb = mongo.GetDatabase("chokuretsu");
        if (chokuDb.GetCollection<ChokuSaveData>(ChokuSaveData.ChokuSaveCollectionName) is null)
        {
            chokuDb.CreateCollection(ChokuSaveData.ChokuSaveCollectionName);
        }
        if (chokuDb.GetCollection<ChokuStats>(ChokuStats.ChokuStatsCollectionName) is null)
        {
            chokuDb.CreateCollection(ChokuStats.ChokuStatsCollectionName);
        }

        Task.Run(() => ChokuStats.UpdateStats(chokuDb.GetCollection<ChokuSaveData>(ChokuSaveData.ChokuSaveCollectionName),
            chokuDb.GetCollection<ChokuStats>(ChokuStats.ChokuStatsCollectionName)));
        
        RouteGroupBuilder chokuStatsApi = app.MapGroup("/choku-wrapped");
        chokuStatsApi.MapGet("/", () =>
        {
            IMongoCollection<ChokuStats> statsCol = chokuDb.GetCollection<ChokuStats>(ChokuStats.ChokuStatsCollectionName);
            ChokuStats stats = statsCol.FindSync(Builders<ChokuStats>.Filter.Empty).FirstOrDefault() ?? new();
            return Results.Ok(stats);
        });
        chokuStatsApi.MapGet("/{sha}", async (string sha) =>
        {
            ChokuSaveData? saveData = (await chokuDb.GetCollection<ChokuSaveData>(ChokuSaveData.ChokuSaveCollectionName)
                .FindAsync(Builders<ChokuSaveData>.Filter.Eq("_id", sha))).FirstOrDefault();
            if (saveData is null)
            {
                return Results.NotFound();
            }
            
            IMongoCollection<ChokuStats> statsCol = chokuDb.GetCollection<ChokuStats>(ChokuStats.ChokuStatsCollectionName);
            ChokuStats stats = statsCol.FindSync(Builders<ChokuStats>.Filter.Empty).FirstOrDefault() ?? new();
            stats.SaveData = saveData;
            return Results.Ok(stats);
        });
        chokuStatsApi.MapPost("/", async context =>
        {
            if (context.Request.Form.Files.Count < 1)
            {
                await context.Response.WriteAsync("ERR_NO_SAVE_DATA");
            }
            MemoryStream chokuDataStream = new();
            await context.Request.Form.Files[0].CopyToAsync(chokuDataStream);
            ChokuSaveData chokuData = new(chokuDataStream.ToArray());
            if (chokuData.IsValid)
            {
                IMongoCollection<ChokuSaveData> saveDataCol = chokuDb.GetCollection<ChokuSaveData>(ChokuSaveData.ChokuSaveCollectionName);

                if ((await saveDataCol.FindAsync(
                        Builders<ChokuSaveData>.Filter.Eq("_id", chokuData.Sha256Hash)))
                    .FirstOrDefault() is not null)
                {
                    await context.Response.WriteAsync(chokuData.Sha256Hash);
                    return;
                }
                
                IMongoCollection<ChokuStats> statsCol = chokuDb.GetCollection<ChokuStats>(ChokuStats.ChokuStatsCollectionName);
                await saveDataCol.InsertOneAsync(chokuData, new InsertOneOptions());
                await ChokuStats.UpdateStats(saveDataCol, statsCol);
                
                await context.Response.WriteAsync(chokuData.Sha256Hash);
                return;
            }
            
            await context.Response.WriteAsync("ERR_INVALID_SAVE");
        });

        app.Run();
    }
}

[JsonSerializable(typeof(ChokuStats))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}