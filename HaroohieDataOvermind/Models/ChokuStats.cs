using System.Globalization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace HaroohieDataOvermind.Models;


public class ChokuStats
{
    public const string ChokuStatsCollectionName = "choku_wrapped";

    [BsonId] public int Id { get; set; } = 0;
    
    public int NumSubmissions { get; set; }

    public double HaruhiFriendshipLevel { get; set; }
    public double MikuruFriendshipLevel { get; set; }
    public double NagatoFriendshipLevel { get; set; }
    public double KoizumiFriendshipLevel { get; set; }
    public double TsuruyaFriendshipLevel { get; set; }

    public Dictionary<string, int> EndingChart { get; set; } = [];

    public double AverageTopicsObtained { get; set; }
    public Dictionary<string, int> TopicsObtainedChart { get; set; } = [];

    public List<List<RouteAggregate>> RoutesTaken { get; set; } = [];
    public int RoutesCountMax { get; set; }
    public Dictionary<string, double> AverageRoutesWithCharacter { get; set; } = [];

    public double AverageHaruhiMeter { get; set; }

    // Episode 1
    public Dictionary<string, int> SawGameOverTutorialChart { get; set; } = [];
    public Dictionary<string, int> Ep1ActivityGuessChart { get; set; } = [];
    public Dictionary<string, int> NumCompSocMembersInterviewedChart { get; set; } = [];

    public ChokuSaveData? SaveData { get; set; }

    public static async Task UpdateStats(IMongoCollection<ChokuSaveData> saveCol, IMongoCollection<ChokuStats> statsCol)
    {
        List<ChokuSaveData> saves = await (await saveCol.FindAsync(Builders<ChokuSaveData>.Filter.Empty)).ToListAsync();
        ChokuStats stats = await statsCol.FindOneAndDeleteAsync(Builders<ChokuStats>.Filter.Empty) ?? new();

        stats.NumSubmissions = saves.Count;

        ChokuSaveData[] friendSaves = saves.Where(s => s.HasFriendship).ToArray();
        double friendSavesLength = friendSaves.Length == 0 ? 1 : friendSaves.Length;
        stats.HaruhiFriendshipLevel = friendSaves.Sum(s => s.HaruhiFriendshipLevel) / friendSavesLength;
        stats.MikuruFriendshipLevel = friendSaves.Sum(s => s.MikuruFriendshipLevel) / friendSavesLength;
        stats.NagatoFriendshipLevel = friendSaves.Sum(s => s.NagatoFriendshipLevel) / friendSavesLength;
        stats.KoizumiFriendshipLevel = friendSaves.Sum(s => s.KoizumiFriendshipLevel) / friendSavesLength;
        stats.TsuruyaFriendshipLevel = friendSaves.Sum(s => s.TsuruyaFriendshipLevel) / friendSavesLength;

        stats.EndingChart = saves.GroupBy(s => s.UnlockedEnding)
            .ToDictionary(g => g.Key, g => g.Count());

        stats.AverageTopicsObtained = saves.Average(s => s.TopicsObtained);
        stats.TopicsObtainedChart = saves.GroupBy(s => s.TopicsObtained)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        List<RouteAggregate> routes = saves.SelectMany(s => s.RoutesTaken).GroupBy(r => r)
            .Select(r => new RouteAggregate(r.Key, r.Count())).ToList();
        stats.RoutesTaken = ChokuSaveData.Routes.Select(rs => rs.Select(r => new RouteAggregate(r, routes.FirstOrDefault(ra => ra.Route.Name.Equals(r.Name))?.Count ?? 0)).ToList()).ToList();
        stats.RoutesCountMax = stats.RoutesTaken.Max(rs => rs.Max(r => r.Count));
        stats.AverageRoutesWithCharacter = saves[0].RoutesWithCharacter.ToDictionary(kv => kv.Key, kv => saves
            .SelectMany(s => s.RoutesWithCharacter.Where(k => k.Key == kv.Key)
                .Select(k => k.Value)).Average());
        
        stats.AverageHaruhiMeter = saves.Average(s => s.HaruhiMeter);

        int sawGameOverTutorial = saves.Count(s => s.SawGameOverTutorial);
        stats.SawGameOverTutorialChart = new()
        {
            { "chokuretsu-wrapped-game-over-saw", sawGameOverTutorial },
            { "chokuretsu-wrapped-game-over-didnt-see", saves.Count - sawGameOverTutorial },
        };
        stats.Ep1ActivityGuessChart = saves.GroupBy(s => s.Ep1ActivityGuess)
            .ToDictionary(g => g.Key, g => g.Count());
        stats.NumCompSocMembersInterviewedChart = saves.GroupBy(s => s.NumCompSocMembersInterviewed)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());
        
        await statsCol.InsertOneAsync(stats, new InsertOneOptions());
    }
}

public record RouteAggregate(Route Route, int Count);