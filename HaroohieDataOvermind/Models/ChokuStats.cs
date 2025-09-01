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
    public List<TopicAggregate> TopicsObtained { get; set; } = [];

    public List<List<RouteAggregate>> RoutesTaken { get; set; } = [];
    public int RoutesCountMax { get; set; }
    public Dictionary<string, double> AverageRoutesWithCharacter { get; set; } = [];
    public Dictionary<string, double> AverageRoutesWithSideCharacter { get; set; } = [];

    public double AverageHaruhiMeter { get; set; }

    // Episode 1
    public Dictionary<string, int> SawGameOverTutorialChart { get; set; } = [];
    public Dictionary<string, int> Ep1ActivityGuessChart { get; set; } = [];
    public Dictionary<string, int> NumCompSocMembersInterviewedChart { get; set; } = [];
    public Dictionary<string, int> Ep1MemoryCardChart { get; set; } = [];
    public Dictionary<string, int> Ep1ResolutionChart { get; set; } = [];
    
    // Episode 2
    public Dictionary<string, int> Ep2FoundTheSecretNoteChart { get; set; } = [];
    public Dictionary<string, int> Ep2ResolutionChart { get; set; } = [];
    
    // Episode 3
    public Dictionary<string, int> Ep3WalkedHomeChart { get; set; } = [];
    public Dictionary<string, int> Ep3ResolutionChart { get; set; } = [];
    
    // Episode 4
    public Dictionary<string, int> Ep4AResolutionChart { get; set; } = [];
    public Dictionary<string, int> Ep4BResolutionChart { get; set; } = [];
    
    // Episode 5
    public Dictionary<string, int> Ep5ChessPuzzleResultChart { get; set; } = [];
    public Dictionary<string, int> Ep5ChessTourneyWinnerChart { get; set; } = [];
    public Dictionary<string, int> Ep5WhoWokeYouUpChart { get; set; } = [];

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
        foreach (Ending ending in Enum.GetValues<Ending>())
        {
            stats.EndingChart.TryAdd(ChokuSaveData.EndingToLabel(ending), 0);
        }

        stats.AverageTopicsObtained = saves.Average(s => s.NumTopicsObtained);
        stats.TopicsObtainedChart = saves.GroupBy(s => s.NumTopicsObtained)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());
        List<TopicAggregate> topics = saves.SelectMany(s => s.TopicsObtained).GroupBy(t => t)
            .Select(t => new TopicAggregate(t.Key, t.Count())).ToList();
        stats.TopicsObtained = ChokuSaveData.Topics.Select(t =>
            new TopicAggregate(t, topics.FirstOrDefault(ta => ta.Topic.Flag == t.Flag)?.Count ?? 0)).ToList();

        List<RouteAggregate> routes = saves.SelectMany(s => s.RoutesTaken).GroupBy(r => r.Flag)
            .Select(r => new RouteAggregate(r.First(), r.Count())).ToList();
        stats.RoutesTaken = ChokuSaveData.Routes.Select(rs => rs.Select(r => new RouteAggregate(r, routes.FirstOrDefault(ra => ra.Route.Name.Equals(r.Name))?.Count ?? 0)).ToList()).ToList();
        stats.RoutesCountMax = stats.RoutesTaken.Max(rs => rs.Max(r => r.Count));
        stats.AverageRoutesWithCharacter = Enum.GetValues<Character>().Select(ChokuSaveData.CharacterToLabel).ToDictionary(c => c, c => saves
            .SelectMany(s => s.RoutesWithCharacter.Where(k => k.Key == c)
                .Select(k => k.Value)).Average());
        stats.AverageRoutesWithSideCharacter = Enum.GetValues<SideCharacter>().Select(ChokuSaveData.SideCharacterToLabel).ToDictionary(c => c, c => saves
            .SelectMany(s => s.RoutesWithSideCharacter.Where(k => k.Key == c)
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
        foreach (string ep1Activity in ChokuSaveData.Ep1ActivityGuesses)
        {
            stats.Ep1ActivityGuessChart.TryAdd(ep1Activity, 0);
        }
        stats.NumCompSocMembersInterviewedChart = saves.GroupBy(s => s.NumCompSocMembersInterviewed)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());
        stats.Ep1MemoryCardChart = saves.GroupBy(s => s.Ep1DidWhatWithMemoryCard)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (string ep1MemoryCardAction in ChokuSaveData.Ep1MemoryCardActions)
        {
            stats.Ep1MemoryCardChart.TryAdd(ep1MemoryCardAction, 0);
        }
        stats.Ep1ResolutionChart = saves.GroupBy(s => s.Ep1Resolution)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (string ep1Resolution in ChokuSaveData.Ep1Resolutions)
        {
            stats.Ep1ResolutionChart.TryAdd(ep1Resolution, 0);
        }

        int foundTheSecretNote = saves.Count(s => s.Ep2FoundTheSecretNote);
        stats.Ep2FoundTheSecretNoteChart = new()
        {
            { "chokuretsu-wrapped-ep2-found-the-note", foundTheSecretNote },
            { "chokuretsu-wrapped-ep2-didnt-find-the-note", saves.Count - foundTheSecretNote },
        };
        stats.Ep2ResolutionChart = saves.GroupBy(s => s.Ep2Resolution)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (string ep2Resolution in ChokuSaveData.Ep2Resolutions)
        {
            stats.Ep2ResolutionChart.TryAdd(ep2Resolution, 0);
        }
        
        stats.Ep3WalkedHomeChart = saves.GroupBy(s => s.Ep3WhoWalkedYouHome)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (string ep2Resolution in ChokuSaveData.Ep3WalksHome)
        {
            stats.Ep3WalkedHomeChart.TryAdd(ep2Resolution, 0);
        }
        stats.Ep3ResolutionChart = saves.GroupBy(s => s.Ep3Resolution)
            .ToDictionary(g => g.Key, g => g.Count());
        stats.Ep3ResolutionChart.TryAdd("chokuretsu-wrapped-ep3-aogeba-toutoshi-dj", 0);
        stats.Ep3ResolutionChart.TryAdd("chokuretsu-wrapped-ep3-mock-graduation-ceremony", 0);
        
        stats.Ep4AResolutionChart = saves.GroupBy(s => s.Ep4AResolution)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (string ep4AResolution in ChokuSaveData.Ep4AResolutions)
        {
            stats.Ep4AResolutionChart.TryAdd(ep4AResolution, 0);
        }
        stats.Ep4BResolutionChart = saves.GroupBy(s => s.Ep4BResolution)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (string ep4BResolution in ChokuSaveData.Ep4BResolutions)
        {
            stats.Ep4BResolutionChart.TryAdd(ep4BResolution, 0);
        }
        
        stats.Ep5ChessPuzzleResultChart = saves.GroupBy(s => s.Ep5ChessPuzzleResult)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (string ep5ChessPuzzleResult in ChokuSaveData.Ep5ChessPuzzleResults)
        {
            stats.Ep5ChessPuzzleResultChart.TryAdd(ep5ChessPuzzleResult, 0);
        }
        stats.Ep5ChessTourneyWinnerChart = saves.GroupBy(s => s.Ep5ChessTourneyWinner)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (string ep5ChessTourneyWinners in ChokuSaveData.Ep5ChessTourneyWinners)
        {
            stats.Ep5ChessTourneyWinnerChart.TryAdd(ep5ChessTourneyWinners, 0);
        }
        stats.Ep5WhoWokeYouUpChart = saves.GroupBy(s => s.Ep5WhoWokeYouUp)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (string ep5ChessTourneyWinners in ChokuSaveData.Ep5WakeUps)
        {
            stats.Ep5WhoWokeYouUpChart.TryAdd(ep5ChessTourneyWinners, 0);
        }
        
        await statsCol.InsertOneAsync(stats, new InsertOneOptions());
    }
}

public record RouteAggregate(Route Route, int Count);
public record TopicAggregate(Topic Topic, int Count);