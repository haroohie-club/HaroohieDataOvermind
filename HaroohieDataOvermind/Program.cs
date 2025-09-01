using System.Text;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using HaroohieDataOvermind.Models;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;

namespace HaroohieDataOvermind;

public class Program
{
    private const string AllowOrigins = "_allowOrigins";
    private const string SpacesUrlEnv = "SPACES_URL";
    private const string SpacesKeyEnv = "SPACES_KEY";
    private const string SpacesSecretEnv = "SPACES_SECRET";
    private const string SpacesNameEnv = "SPACES_NAME";
    
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
                    policy.WithOrigins("https://*.haroohie.club") 
                        .WithMethods("GET", "POST");
                });
        });

        WebApplication app = builder.Build();
        app.UseCors(AllowOrigins);

        MongoClientSettings clientSettings = new()
        {
            Scheme = ConnectionStringScheme.MongoDB, 
            Server = new(Environment.GetEnvironmentVariable("MONGO_HOST"), 27017),
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
            if (context.Request.Form.Files.Count < 1 || context.Request.Form.Files[0].Length != 8192)
            {
                await context.Response.WriteAsync("ERR_NOT_A_SAVE_FILE");
            }
            using MemoryStream chokuDataStream = new();
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

                chokuDataStream.Seek(0, SeekOrigin.Begin);
                AmazonS3Config config = new() { ServiceURL = Environment.GetEnvironmentVariable(SpacesUrlEnv) };
                AmazonS3Client client = new(Environment.GetEnvironmentVariable(SpacesKeyEnv),
                    Environment.GetEnvironmentVariable(SpacesSecretEnv), config);
                PutObjectRequest saveRequest = new()
                {
                    BucketName = Environment.GetEnvironmentVariable(SpacesNameEnv),
                    Key = $"saves/chokuretsu/{chokuData.Sha256Hash}.sav",
                    InputStream = chokuDataStream,
                };
                await client.PutObjectAsync(saveRequest);
                
                return;
            }
            
            await context.Response.WriteAsync("ERR_INVALID_SAVE");
        });
        chokuStatsApi.MapPost("/refresh", async context =>
        {
            string? pass = Environment.GetEnvironmentVariable("REFRESH_PASS");
            if (string.IsNullOrEmpty(pass))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Server has no password!");
                return;
            }

            var providedPass = new byte[pass.Length];
            await context.Request.Body.ReadExactlyAsync(providedPass);
            if (!Encoding.ASCII.GetString(providedPass).Equals(pass))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Incorrect password!");
                return;
            }
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync("OK");
            
            IMongoCollection<ChokuStats> statsCol = chokuDb.GetCollection<ChokuStats>(ChokuStats.ChokuStatsCollectionName);
            IMongoCollection<ChokuSaveData> saveDataCol = chokuDb.GetCollection<ChokuSaveData>(ChokuSaveData.ChokuSaveCollectionName);
            await saveDataCol.DeleteManyAsync(Builders<ChokuSaveData>.Filter.Empty);
            
            AmazonS3Config config = new() { ServiceURL = Environment.GetEnvironmentVariable(SpacesUrlEnv) };
            AmazonS3Client client = new(Environment.GetEnvironmentVariable(SpacesKeyEnv),
                Environment.GetEnvironmentVariable(SpacesSecretEnv), config);
            ListObjectsRequest listRequest = new()
            {
                BucketName = Environment.GetEnvironmentVariable(SpacesNameEnv),
                Prefix = "saves/chokuretsu/",
            };
            ListObjectsResponse listResponse = await client.ListObjectsAsync(listRequest);
            foreach (S3Object file in listResponse.S3Objects)
            {
                GetObjectRequest getRequest = new()
                {
                    BucketName = Environment.GetEnvironmentVariable(SpacesNameEnv),
                    Key = file.Key,
                };
                GetObjectResponse getResponse = await client.GetObjectAsync(getRequest);
                using MemoryStream saveDataStream = new();
                await getResponse.ResponseStream.CopyToAsync(saveDataStream);
                
                ChokuSaveData chokuData = new(saveDataStream.ToArray());
                await saveDataCol.InsertOneAsync(chokuData, new InsertOneOptions());
            }
            
            await ChokuStats.UpdateStats(saveDataCol, statsCol);
        });

        app.Run();
    }
}

[JsonSerializable(typeof(ChokuStats))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}