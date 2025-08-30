using System.Security.Cryptography;
using HaruhiChokuretsuLib.Save;
using MongoDB.Bson.Serialization.Attributes;

namespace HaroohieDataOvermind.Models;

public class ChokuSaveData
{
    public const string ChokuSaveCollectionName = "choku_save";

    [BsonId]
    public string Sha256Hash { get; set; }
    public bool IsValid { get; set; } = true;
    public bool HasFriendship { get; set; } = true;

    public int HaruhiFriendshipLevel { get; set; }
    public int MikuruFriendshipLevel { get; set; }
    public int NagatoFriendshipLevel { get; set; }
    public int KoizumiFriendshipLevel { get; set; }
    public int TsuruyaFriendshipLevel { get; set; }

    public string UnlockedEnding { get; set; } = string.Empty;

    public int TopicsObtained { get; set; }

    public List<Route> RoutesTaken { get; set; } = [];
    public Dictionary<string, int> RoutesWithCharacter { get; set; } = [];
    public Dictionary<string, int> RoutesWithSideCharacter { get; set; } = [];

    public int HaruhiMeter { get; set; }

    // Ep 1
    public bool SawGameOverTutorial { get; set; }
    public string Ep1ActivityGuess { get; set; } = string.Empty;
    public int NumCompSocMembersInterviewed { get; set; }
    
    // Ep 2
    public bool FoundTheSecretNote { get; set; }

    // Ep 3
    public string WhoWalkedYouHome { get; set; } = string.Empty;
    
    // Ep 4
    
    // Ep 5
    public bool ClearedChessPuzzle { get; set; }
    public bool DefeatedHaruhiInChess { get; set; }
    public bool WhoWokeYouUp { get; set; }

    public ChokuSaveData(byte[] data)
    {
        Sha256Hash = string.Join("", SHA256.HashData(data).Select(b => $"{b:X2}"));
        try
        {
            SaveFile save = new(data);

            SaveSlotData? slot = save.CheckpointSaveSlots.OrderByDescending(s => s.SaveTime)
                .FirstOrDefault(s => s.IsFlagSet(4379) && s.IsFlagSet(4380) && s.IsFlagSet(4381) && s.IsFlagSet(4382) && s.IsFlagSet(4628));
            if (slot is null)
            {
                IsValid = false;
                return;
            }

            // The friendship levels are cleared when the game is completed, but we have a hack that saves them
            // If these values are non-zero, that hack is present
            HaruhiFriendshipLevel = slot.Footer[0];
            MikuruFriendshipLevel = slot.Footer[1];
            NagatoFriendshipLevel = slot.Footer[2];
            KoizumiFriendshipLevel = slot.Footer[3];
            TsuruyaFriendshipLevel = slot.Footer[4];
            
            // If it's not, we mark this save as such for the site to know not to display friendship levels
            if (HaruhiFriendshipLevel == 0 && MikuruFriendshipLevel == 0 && NagatoFriendshipLevel == 0 &&
                KoizumiFriendshipLevel == 0 && TsuruyaFriendshipLevel == 0)
            {
                HasFriendship = false;
            }

            // Check which ending they unlocked
            if (slot.IsFlagSet(4315))
            {
                UnlockedEnding = EndingToLabel(Ending.TsuruyaEnding);
            }
            else if (slot.IsFlagSet(4311))
            {
                UnlockedEnding = EndingToLabel(Ending.HaruhiEnding);
            }
            else if (slot.IsFlagSet(4312))
            {
                UnlockedEnding = EndingToLabel(Ending.MikuruEnding);
            }
            else if (slot.IsFlagSet(4313))
            {
                UnlockedEnding = EndingToLabel(Ending.NagatoEnding);
            }
            else if (slot.IsFlagSet(4314))
            {
                UnlockedEnding = EndingToLabel(Ending.KoizumiEnding);
            }

            for (int i = 122; i <= 819; i++)
            {
                if (slot.IsFlagSet(i))
                {
                    TopicsObtained++;
                }
            }

            foreach (string character in Enum.GetValues<Character>().Select(CharacterToLabel))
            {
                RoutesWithCharacter.Add(character, 0);
            }
            foreach (string sideCharacter in Enum.GetValues<SideCharacter>().Select(SideCharacterToLabel))
            {
                RoutesWithSideCharacter.Add(sideCharacter, 0);
            }
            foreach (Route[] selection in Routes)
            {
                foreach (Route route in selection)
                {
                    if (slot.IsFlagSet(route.Flag))
                    {
                        RoutesTaken.Add(route);
                        foreach (Character character in route.Characters)
                        {
                            RoutesWithCharacter[CharacterToLabel(character)]++;
                        }
                        foreach (SideCharacter sideCharacter in route.SideCharacters)
                        {
                            RoutesWithSideCharacter[SideCharacterToLabel(sideCharacter)]++;
                        }
                        break;
                    }
                }
            }

            HaruhiMeter = (slot.HaruhiMeter + 1) * 10;
            
            // TR05 + !EV1_002 SEL001
            SawGameOverTutorial = slot.IsFlagSet(1016) && !slot.IsFlagSet(1196);

            // EV1_001 sections SEL001, SEL002, or SEL003
            for (int i = 0; i < 4; i++)
            {
                if (i == 3 || slot.IsFlagSet(i + 1167))
                {
                    Ep1ActivityGuess = Ep1ActivityGuesses[i];
                    break;
                }
            }

            for (int i = 1181; i <= 1189; i += 2)
            {
                if (slot.IsFlagSet(i))
                {
                    NumCompSocMembersInterviewed++;
                }
            }
        }
        catch
        {
            IsValid = false;
        }
    }

    public static readonly string[] Ep1ActivityGuesses =
    [
        "chokuretsu-wrapped-ep1-go-swimming",
        "chokuretsu-wrapped-ep1-go-camping",
        "chokuretsu-wrapped-ep1-summer-camp"
    ];

    public static string EndingToLabel(Ending ending)
    {
        return ending switch
        {
            Ending.HaruhiEnding => "chokuretsu-wrapped-haruhi",
            Ending.MikuruEnding => "chokuretsu-wrapped-mikuru",
            Ending.NagatoEnding => "chokuretsu-wrapped-nagato",
            Ending.KoizumiEnding => "chokuretsu-wrapped-koizumi",
            Ending.TsuruyaEnding => "chokuretsu-wrapped-tsuruya",
            _ => "chokuretsu-wrapped-unknown",
        };
    }

    public static string CharacterToLabel(Character character)
    {
        return character switch
        {
            Character.Haruhi => "chokuretsu-wrapped-haruhi",
            Character.Mikuru => "chokuretsu-wrapped-mikuru",
            Character.Nagato => "chokuretsu-wrapped-nagato",
            Character.Koizumi => "chokuretsu-wrapped-koizumi",
            _ => "chokuretsu-wrapped-unknown",
        };
    }

    public static string SideCharacterToLabel(SideCharacter character)
    {
        return character switch
        {
            SideCharacter.Cat => "chokuretsu-wrapped-cat",
            SideCharacter.Girl => "chokuretsu-wrapped-mystery-girl",
            SideCharacter.Grocer => "chokuretsu-wrapped-grocer",
            SideCharacter.Kunikida => "chokuretsu-wrapped-kunikida",
            SideCharacter.MemberA => "chokuretsu-wrapped-member-a",
            SideCharacter.MemberB => "chokuretsu-wrapped-member-b",
            SideCharacter.MemberC => "chokuretsu-wrapped-member-c",
            SideCharacter.MemberD => "chokuretsu-wrapped-member-d",
            SideCharacter.Okabe => "chokuretsu-wrapped-okabe",
            SideCharacter.President =>  "chokuretsu-wrapped-president",
            SideCharacter.Sister => "chokuretsu-wrapped-sister",
            SideCharacter.Taniguchi => "chokuretsu-wrapped-taniguchi",
            SideCharacter.Tsuruya => "chokuretsu-wrapped-tsuruya",
            _ => "chokuretsu-wrapped-unknown",
        };
    }

    public static readonly Route[][] Routes =
    [
        [
            new(1022, "chokuretsu-wrapped-ep1-working-alone", [], Objective.A, [ SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.President ]),
            new(1023, "chokuretsu-wrapped-ep1-with-mikuru", [Character.Mikuru], Objective.A, [ SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.President ]),
            new(1024, "chokuretsu-wrapped-ep1-with-nagato", [Character.Nagato], Objective.A, [ SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.President ]),
            new(1025, "chokuretsu-wrapped-ep1-working-with-koizumi", [Character.Koizumi], Objective.A, [ SideCharacter.MemberA, SideCharacter.President, SideCharacter.Sister ]),
            new(1026, "chokuretsu-wrapped-ep1-flower-in-each-hand", [Character.Mikuru, Character.Nagato], Objective.A, [ SideCharacter.MemberA, SideCharacter.President ]),
            new(1027, "chokuretsu-wrapped-ep1-koizumis-plan", [Character.Mikuru, Character.Koizumi], Objective.A, [ SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.President ]),
            new(1028, "chokuretsu-wrapped-ep1-the-cool-two", [Character.Nagato, Character.Koizumi], Objective.A, [ SideCharacter.MemberA, SideCharacter.President ]),
            new(1029, "chokuretsu-wrapped-ep1-everyone-to-the-computer-society", [Character.Mikuru, Character.Nagato, Character.Koizumi], Objective.A, [ SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.MemberC, SideCharacter.President ]),
            new(1030, "chokuretsu-wrapped-ep1-alone-with-haruhi", [Character.Haruhi], Objective.B, []),
            new(1031, "chokuretsu-wrapped-ep1-boisterous-girls", [Character.Haruhi, Character.Mikuru], Objective.B, [ SideCharacter.Sister ]),
            new(1032, "chokuretsu-wrapped-ep1-haruhi-and-nagato", [Character.Haruhi, Character.Nagato], Objective.B, []),
            new(1033, "chokuretsu-wrapped-ep1-a-point-of-reference", [Character.Haruhi, Character.Koizumi], Objective.B, []),
            new(1034, "chokuretsu-wrapped-ep1-preliminary-investigation", [Character.Haruhi, Character.Mikuru, Character.Nagato], Objective.B, []),
            new(1035, "chokuretsu-wrapped-ep1-sos-brigade-activity-record", [Character.Haruhi, Character.Mikuru, Character.Koizumi], Objective.B, []),
            new(1036, "chokuretsu-wrapped-ep1-second-raid", [Character.Haruhi, Character.Nagato, Character.Koizumi], Objective.B, [ SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.President ]),
            new(1037, "chokuretsu-wrapped-ep1-gathering-the-troops", [Character.Haruhi, Character.Mikuru, Character.Nagato, Character.Koizumi], Objective.B, [ SideCharacter.Sister ]),
        ],
        [
            new(1038, "chokuretsu-wrapped-ep2-reconfirmation", [], Objective.A, [ SideCharacter.MemberB, SideCharacter.President ]),
            new(1039, "chokuretsu-wrapped-ep2-consultation", [Character.Mikuru], Objective.A, [ SideCharacter.Sister ]),
            new(1040, "chokuretsu-wrapped-ep2-for-persuading-haruhi", [Character.Nagato], Objective.A, [ SideCharacter.Sister ]),
            new(1041, "chokuretsu-wrapped-ep2-koizumis-proposition", [Character.Koizumi], Objective.A, []),
            new(1042, "chokuretsu-wrapped-ep2-an-aged-timbre", [Character.Mikuru, Character.Nagato], Objective.A, []),
            new(1043, "chokuretsu-wrapped-ep2-the-computer-societys-secret", [Character.Mikuru, Character.Koizumi], Objective.A, [ SideCharacter.President ]),
            new(1044, "chokuretsu-wrapped-ep2-suspicious-conduct", [Character.Nagato, Character.Koizumi], Objective.A, [ SideCharacter.MemberB ]),
            new(1046, "chokuretsu-wrapped-ep2-in-the-mountain-of-books", [Character.Haruhi], Objective.B, []),
            new(1047, "chokuretsu-wrapped-ep2-in-charge-of-odd-jobs", [Character.Haruhi, Character.Mikuru], Objective.B, []),
            new(1048, "chokuretsu-wrapped-ep2-reading-time", [Character.Haruhi, Character.Nagato], Objective.B, [ SideCharacter.Sister ]),
            new(1049, "chokuretsu-wrapped-ep2-hierarchy", [Character.Haruhi, Character.Koizumi], Objective.B, [ SideCharacter.Sister ]),
            new(1053, "chokuretsu-wrapped-ep2-kyons-strenuous-effort", [], Objective.C, []),
            new(1054, "chokuretsu-wrapped-ep2-mikurus-great-work", [Character.Mikuru], Objective.C, [ SideCharacter.Okabe ]),
            new(1055, "chokuretsu-wrapped-ep2-before-you-know-it", [Character.Nagato], Objective.C, []),
            new(1056, "chokuretsu-wrapped-ep2-in-anticipation", [Character.Koizumi], Objective.C, [SideCharacter.Okabe]),
            new(1057, "chokuretsu-wrapped-ep2-poster", [Character.Mikuru, Character.Nagato], Objective.C, [ SideCharacter.Sister, SideCharacter.Tsuruya ]),
            new(1058, "chokuretsu-wrapped-ep2-songwriting-contest", [Character.Mikuru, Character.Koizumi], Objective.C, []),
            new(1059, "chokuretsu-wrapped-ep2-north-highs-alumni", [Character.Nagato, Character.Koizumi], Objective.C, []),
        ],
        [
            new(1061, "chokuretsu-wrapped-ep3-kyon-and-the-stray-cat", [], Objective.A, [ SideCharacter.Cat, SideCharacter.Grocer, SideCharacter.Sister ]),
            new(1062, "chokuretsu-wrapped-ep3-careless-mikuru", [Character.Mikuru], Objective.A, [ SideCharacter.Grocer ]),
            new(1063, "chokuretsu-wrapped-ep3-difficult-choice", [Character.Nagato], Objective.A, []),
            new(1064, "chokuretsu-wrapped-ep3-lottery-ticket", [Character.Koizumi], Objective.A, [ SideCharacter.Grocer ]),
            new(1065, "chokuretsu-wrapped-ep3-a-flower-in-each-hand-again", [Character.Mikuru, Character.Nagato], Objective.A, [ SideCharacter.Grocer, SideCharacter.Sister ]),
            new(1066, "chokuretsu-wrapped-ep3-mikuru-and-the-stray-cat", [Character.Mikuru, Character.Koizumi], Objective.A, [ SideCharacter.Cat, SideCharacter.Grocer ]),
            new(1067, "chokuretsu-wrapped-ep3-the-shopkeepers-favor", [Character.Nagato, Character.Koizumi], Objective.A, [ SideCharacter.Grocer, SideCharacter.Sister ]),
            new(1068, "chokuretsu-wrapped-ep3-buying-too-much", [Character.Mikuru, Character.Nagato, Character.Koizumi], Objective.A, [ SideCharacter.Cat, SideCharacter.Grocer, SideCharacter.Sister ]),
            new(1069, "chokuretsu-wrapped-ep3-haphazard", [Character.Haruhi], Objective.B, [ SideCharacter.Sister ]),
            new(1070, "chokuretsu-wrapped-ep3-the-maid-is-a-slugger", [Character.Haruhi, Character.Mikuru], Objective.B, [ SideCharacter.Sister ]),
            new(1071, "chokuretsu-wrapped-ep3-wasted-effort", [Character.Haruhi, Character.Nagato], Objective.B, []),
            new(1072, "chokuretsu-wrapped-ep3-a-mountain-of-oversights", [Character.Haruhi, Character.Koizumi], Objective.B, []),
            new(1073, "chokuretsu-wrapped-ep3-computer-society-in-a-bind", [Character.Haruhi, Character.Mikuru, Character.Nagato], Objective.B, [ SideCharacter.MemberA, SideCharacter.President, SideCharacter.Sister ]),
            new(1074, "chokuretsu-wrapped-ep3-derailment", [Character.Haruhi, Character.Mikuru, Character.Koizumi], Objective.B, [ SideCharacter.Sister ]),
            new(1075, "chokuretsu-wrapped-ep3-handmade", [Character.Haruhi, Character.Nagato, Character.Koizumi], Objective.B, []),
            new(1076, "chokuretsu-wrapped-ep3-a-mountain-and-a-molehill", [Character.Haruhi, Character.Mikuru, Character.Nagato, Character.Koizumi], Objective.B, [ SideCharacter.Sister ]),
        ],
        [
            new(1077, "chokuretsu-wrapped-ep3-preparations", [Character.Haruhi], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Sister, SideCharacter.Taniguchi ]),
            new(1078, "chokuretsu-wrapped-ep3-never-before-seen", [Character.Haruhi, Character.Nagato], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Sister, SideCharacter.Taniguchi ]),
            new(1079, "chokuretsu-wrapped-ep3-lame-story", [Character.Haruhi, Character.Koizumi], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Sister, SideCharacter.Taniguchi ]),
            new(1080, "chokuretsu-wrapped-ep3-a-huge-bother", [Character.Haruhi, Character.Nagato, Character.Koizumi], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Sister, SideCharacter.Taniguchi ]),
            new(1081, "chokuretsu-wrapped-ep3-feels-like-a-date", [Character.Mikuru], Objective.D, [ SideCharacter.Tsuruya ]),
            new(1082, "chokuretsu-wrapped-ep3-nagato-and-a-little-sister", [Character.Mikuru, Character.Nagato], Objective.D, [ SideCharacter.Sister, SideCharacter.Tsuruya ]),
            new(1083, "chokuretsu-wrapped-ep3-mikurus-disaster", [Character.Mikuru, Character.Koizumi], Objective.D, [ SideCharacter.Tsuruya ]),
            new(1084, "chokuretsu-wrapped-ep3-state-of-emergency", [Character.Mikuru, Character.Nagato, Character.Koizumi], Objective.D, [ SideCharacter.Tsuruya ]),
        ],
        [
            new(1085, "chokuretsu-wrapped-ep4-poolside", [Character.Haruhi], Objective.A, []),
            new(1086, "chokuretsu-wrapped-ep4-which-ones-the-moon", [Character.Haruhi, Character.Mikuru], Objective.A, []),
            new(1087, "chokuretsu-wrapped-ep4-to-our-intergalactic-friends", [Character.Haruhi, Character.Nagato], Objective.A, []),
            new(1089, "chokuretsu-wrapped-ep4-group-work", [Character.Koizumi], Objective.B, []),
            new(1090, "chokuretsu-wrapped-ep4-that-fellow-in-the-science-lab", [Character.Mikuru, Character.Koizumi], Objective.B, []),
            new(1091, "chokuretsu-wrapped-ep4-the-science-of-fear", [Character.Nagato, Character.Koizumi], Objective.B, []),
            new(1093, "chokuretsu-wrapped-ep4-to-the-convenience-store-alone", [], Objective.C, []),
            new(1094, "chokuretsu-wrapped-ep4-mikurus-shopping", [Character.Mikuru], Objective.C, []),
            new(1095, "chokuretsu-wrapped-ep4-what-nagato-wants", [Character.Nagato], Objective.C, []),
            new(1096, "chokuretsu-wrapped-ep4-a-shopping-bag-in-each-hand", [Character.Mikuru, Character.Nagato], Objective.C, []),
        ],
        [
            new(1097, "chokuretsu-wrapped-ep4-the-last-stand", [Character.Haruhi], Objective.A, []),
            new(1098, "chokuretsu-wrapped-ep4-bandage", [Character.Haruhi, Character.Mikuru], Objective.A, []),
            new(1099, "chokuretsu-wrapped-ep4-nagatos-fear", [Character.Haruhi, Character.Nagato], Objective.A, []),
            new(1100, "chokuretsu-wrapped-ep4-the-rules-of-the-test-of-courage", [Character.Haruhi, Character.Koizumi], Objective.A, []),
            new(1101, "chokuretsu-wrapped-ep4-the-kingdom-of-shadows", [Character.Haruhi, Character.Mikuru, Character.Nagato], Objective.A, []),
            new(1102, "chokuretsu-wrapped-ep4-i-cant-accept-it", [Character.Haruhi, Character.Mikuru, Character.Koizumi], Objective.A, []),
            new(1103, "chokuretsu-wrapped-ep4-trying-again", [Character.Haruhi, Character.Nagato, Character.Koizumi], Objective.A, []),
            new(1105, "chokuretsu-wrapped-ep4-singing-your-own-praises", [], Objective.B, [ SideCharacter.Girl, SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.MemberC, SideCharacter.President ]),
            new(1106, "chokuretsu-wrapped-ep4-unreliable-partner", [Character.Mikuru], Objective.B, [ SideCharacter.MemberA, SideCharacter.President ]),
            new(1107, "chokuretsu-wrapped-ep4-reliable-partner", [Character.Nagato], Objective.B, [ SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.MemberC, SideCharacter.MemberD, SideCharacter.President ]),
            new(1108, "chokuretsu-wrapped-ep4-give-and-take", [Character.Koizumi],  Objective.B, [ SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.MemberC, SideCharacter.President ]),
            new(1109, "chokuretsu-wrapped-ep4-both-extremes", [Character.Mikuru, Character.Nagato], Objective.B, [ SideCharacter.Girl, SideCharacter.Kunikida, SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.President, SideCharacter.Taniguchi ]),
            new(1110, "chokuretsu-wrapped-ep4-big-trouble", [Character.Mikuru, Character.Koizumi], Objective.B, [ SideCharacter.Girl, SideCharacter.Kunikida, SideCharacter.MemberA, SideCharacter.President, SideCharacter.Taniguchi ]),
            new(1111, "chokuretsu-wrapped-ep4-an-unexpected-reunion", [Character.Nagato, Character.Koizumi], Objective.B, [ SideCharacter.Girl, SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.MemberC, SideCharacter.MemberD ]),
            new(1112, "chokuretsu-wrapped-ep4-traces", [Character.Mikuru, Character.Nagato, Character.Koizumi], Objective.B, [ SideCharacter.Kunikida, SideCharacter.Taniguchi ]),
            new(1113, "chokuretsu-wrapped-ep4-extra-victims", [], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Taniguchi ]),
            new(1114, "chokuretsu-wrapped-ep4-stolen-goods", [Character.Mikuru], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Taniguchi ]),
            new(1115, "chokuretsu-wrapped-ep4-a-merciless-individual", [Character.Nagato], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Taniguchi ]),
            new(1116, "chokuretsu-wrapped-ep4-follow-them", [Character.Nagato], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Taniguchi ]),
            new(1117, "chokuretsu-wrapped-ep4-unforeseen", [Character.Mikuru, Character.Nagato], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Taniguchi ]),
            new(1118, "chokuretsu-wrapped-ep4-uneasiness", [Character.Mikuru, Character.Koizumi], Objective.C, [ SideCharacter.Kunikida, SideCharacter.Taniguchi ]),
            new(1119, "chokuretsu-wrapped-ep4-theres-no-one-here", [Character.Nagato, Character.Koizumi], Objective.C, [ SideCharacter.Kunikida, SideCharacter.MemberA, SideCharacter.MemberB, SideCharacter.President, SideCharacter.Taniguchi ]),
        ],
        [
            new(1121, "chokuretsu-wrapped-ep5-game", [Character.Haruhi], Objective.A, []),
            new(1122, "chokuretsu-wrapped-ep5-miscalculation", [Character.Haruhi, Character.Mikuru], Objective.A, []),
            new(1123, "chokuretsu-wrapped-ep5-climax", [Character.Haruhi, Character.Koizumi], Objective.A, []),
            new(1124, "chokuretsu-wrapped-ep5-tournament", [Character.Haruhi, Character.Mikuru, Character.Koizumi], Objective.A, []),
            new(1125, "chokuretsu-wrapped-ep5-working-in-solitude", [], Objective.B, []),
            new(1126, "chokuretsu-wrapped-ep5-near-miss", [Character.Mikuru], Objective.B, []),
            new(1127, "chokuretsu-wrapped-ep5-a-nearby-blind-spot", [Character.Koizumi], Objective.B, []),
            new(1129, "chokuretsu-wrapped-ep5-straightforward-duty", [], Objective.C, []),
            new(1130, "chokuretsu-wrapped-ep5-state-of-emergency", [Character.Mikuru], Objective.C, []),
            new(1131, "chokuretsu-wrapped-ep5-two-options", [Character.Koizumi], Objective.C, []),
            new(1133, "chokuretsu-wrapped-ep5-trace-the-abnormality", [Character.Nagato], Objective.D, []),
            new(1134, "chokuretsu-wrapped-ep5-all-alone", [Character.Mikuru, Character.Nagato], Objective.D, []),
            new(1135, "chokuretsu-wrapped-ep5-keeping-busy", [Character.Nagato, Character.Koizumi], Objective.D, []),
        ],
    ];
}

public record Route(int Flag, string Name, Character[] Characters, Objective Objective, SideCharacter[] SideCharacters);

public enum Ending
{
    HaruhiEnding,
    MikuruEnding,
    NagatoEnding,
    KoizumiEnding,
    TsuruyaEnding,
}

public enum Character
{
    Haruhi,
    Mikuru,
    Nagato,
    Koizumi,
}

public enum SideCharacter
{
    Cat,
    Girl,
    Grocer,
    Kunikida,
    MemberA,
    MemberB,
    MemberC,
    MemberD,
    Okabe,
    President,
    Sister,
    Taniguchi,
    Tsuruya
}

public enum Objective
{
    A,
    B,
    C,
    D,
}