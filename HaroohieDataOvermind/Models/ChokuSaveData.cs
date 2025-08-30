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

    public int NumTopicsObtained { get; set; }
    public List<Topic> TopicsObtained { get; set; } = [];

    public List<Route> RoutesTaken { get; set; } = [];
    public Dictionary<string, int> RoutesWithCharacter { get; set; } = [];
    public Dictionary<string, int> RoutesWithSideCharacter { get; set; } = [];

    public int HaruhiMeter { get; set; }

    // Ep 1
    public bool SawGameOverTutorial { get; set; }
    public string Ep1ActivityGuess { get; set; } = string.Empty;
    public int NumCompSocMembersInterviewed { get; set; }
    public string Ep1DidWhatWithMemoryCard { get; set; } = string.Empty;
    public string Ep1Resolution { get; set; } = string.Empty;

    // Ep 2
    public bool Ep2FoundTheSecretNote { get; set; }
    public string Ep2Resolution { get; set; } = string.Empty;

    // Ep 3
    public string Ep3WhoWalkedYouHome { get; set; } = string.Empty;
    public string Ep3Resolution { get; set; } = string.Empty;

    // Ep 4
    public string Ep4AResolution { get; set; } = string.Empty;
    public string Ep4BResolution { get; set; } = string.Empty;

    // Ep 5
    public bool Ep5ClearedChessPuzzle { get; set; }
    public bool Ep5DefeatedHaruhiInChess { get; set; }
    public bool Ep5WhoWokeYouUp { get; set; }

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

            for (int i = 122; i <= 820; i++)
            {
                if (slot.IsFlagSet(i))
                {
                    TopicsObtained.Add(Topics.First(t => t.Flag == i));
                    NumTopicsObtained++;
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
            
            // EV1_003 SEL003, EV1_006 SEL003, EV1_008 SEL005, EV1_009 SEL006, EV1_010 SEL006
            if (slot.IsFlagSet(1204) || slot.IsFlagSet(1237) || slot.IsFlagSet(1264) || slot.IsFlagSet(1277) ||
                slot.IsFlagSet(1289))
            {
                Ep1DidWhatWithMemoryCard = Ep1MemoryCardActions[0];
            }
            // EV1_003 SEL004, EV1_006 SEL004, EV1_008 SEL006, EV1_009 SEL005, EV1_010 SEL007
            else if (slot.IsFlagSet(1205) || slot.IsFlagSet(1238) || slot.IsFlagSet(1265) || slot.IsFlagSet(1276) ||
                     slot.IsFlagSet(1290))
            {
                Ep1DidWhatWithMemoryCard = Ep1MemoryCardActions[1];
            }
            else
            {
                Ep1DidWhatWithMemoryCard = Ep1MemoryCardActions[2];
            }
            
            // EV1_026, EV1_027, EV1_028, or EV1_029
            for (int i = 0; i < 4; i++)
            {
                if (slot.IsFlagSet(1393 + i * 3))
                {
                    Ep1Resolution = Ep1Resolutions[i];
                    break;
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
        "chokuretsu-wrapped-ep1-summer-camp",
    ];

    public static readonly string[] Ep1MemoryCardActions =
    [
        "chokuretsu-wrapped-ep1-took-memory-card",
        "chokuretsu-wrapped-ep1-returned-memory-card",
        "chokuretsu-wrapped-ep1-didnt-find-memory-card",
    ];

    public static readonly string[] Ep1Resolutions =
    [
        "chokuretsu-wrapped-ep1-h2o",
        "chokuretsu-wrapped-ep1-swapped-plates",
        "chokuretsu-wrapped-ep1-off-by-h2o-plates",
        "chokuretsu-wrapped-ep1-a-misunderstanding",
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

    public static readonly Topic[] Topics =
    [
        new(122, "chokuretsu-wrapped-topic-memory-card", 1, TopicType.Main),
        new(123, "chokuretsu-wrapped-topic-poster-misprint", 1, TopicType.Main),
        new(124, "chokuretsu-wrapped-topic-class-6-plate", 1, TopicType.Main),
        new(125, "chokuretsu-wrapped-topic-broken-music-box", 2, TopicType.Main),
        new(126, "chokuretsu-wrapped-topic-mysterious-sheet-music", 2, TopicType.Main),
        new(127, "chokuretsu-wrapped-topic-alumni-register", 2, TopicType.Main),
        new(128, "chokuretsu-wrapped-topic-contest-poster", 2, TopicType.Main),
        new(129, "chokuretsu-wrapped-topic-music-disc", 3, TopicType.Main),
        new(130, "chokuretsu-wrapped-topic-stray-cat", 3, TopicType.Main),
        new(131, "chokuretsu-wrapped-topic-cd-player", 3, TopicType.Main),
        new(132, "chokuretsu-wrapped-topic-science-textbook", 3, TopicType.Main),
        new(133, "chokuretsu-wrapped-topic-choir-club", 3, TopicType.Main),
        new(134, "chokuretsu-wrapped-topic-full-moon", 4, TopicType.Main),
        new(135, "chokuretsu-wrapped-topic-flashlight", 4, TopicType.Main),
        new(136, "chokuretsu-wrapped-topic-anatomical-model", 4, TopicType.Main),
        new(137, "chokuretsu-wrapped-topic-fireworks", 4, TopicType.Main),
        new(138, "chokuretsu-wrapped-topic-beach-ball", 4, TopicType.Main),
        new(139, "chokuretsu-wrapped-topic-shadow-puppetry", 4, TopicType.Main),
        new(140, "chokuretsu-wrapped-topic-spirit-photograph", 4, TopicType.Main),
        new(141, "chokuretsu-wrapped-topic-stain-on-the-wall", 4, TopicType.Main),
        new(142, "chokuretsu-wrapped-topic-photosynthesis", 4, TopicType.Main),
        new(143, "chokuretsu-wrapped-topic-dragging-marks", 4, TopicType.Main),
        new(144, "chokuretsu-wrapped-topic-petals", 4, TopicType.Main),
        new(145, "chokuretsu-wrapped-topic-twins", 5, TopicType.Main),
        new(146, "chokuretsu-wrapped-topic-hastily-scribbled-notes", 5, TopicType.Main),
        new(147, "chokuretsu-wrapped-topic-cell-phone", 5, TopicType.Main),
        new(148, "chokuretsu-wrapped-topic-new-ability", 5, TopicType.Main),
        new(149, "chokuretsu-wrapped-topic-curtain", 5, TopicType.Main),
        new(150, "chokuretsu-wrapped-topic-spot", 5, TopicType.Main),
        new(154, "chokuretsu-wrapped-topic-moths-and-flames", 5, TopicType.Main),
        new(155, "chokuretsu-wrapped-topic-reflection", 5, TopicType.Main),
        new(202, "chokuretsu-wrapped-topic-haruhi-and-kyon", 1, TopicType.Haruhi),
        new(203, "chokuretsu-wrapped-topic-sos-brigade-chief", 1, TopicType.Haruhi),
        new(204, "chokuretsu-wrapped-topic-alpha-wolf", 1, TopicType.Haruhi),
        new(205, "chokuretsu-wrapped-topic-brigade-chief-tyranny?!", 1, TopicType.Haruhi),
        new(206, "chokuretsu-wrapped-topic-a-scientific-approach", 2, TopicType.Haruhi),
        new(207, "chokuretsu-wrapped-topic-the-best-brigade-chief", 2, TopicType.Haruhi),
        new(208, "chokuretsu-wrapped-topic-gentle-miss-brigade-chief", 3, TopicType.Haruhi),
        new(209, "chokuretsu-wrapped-topic-the-sos-brigade's-summer", 3, TopicType.Haruhi),
        new(210, "chokuretsu-wrapped-topic-haruhi's-pace", 4, TopicType.Haruhi),
        new(211, "chokuretsu-wrapped-topic-optimistic-haruhi", 4, TopicType.Haruhi),
        new(212, "chokuretsu-wrapped-topic-kyon's-position", 5, TopicType.Haruhi),
        new(213, "chokuretsu-wrapped-topic-tranquilizer", 5, TopicType.Haruhi),
        new(214, "chokuretsu-wrapped-topic-kyon's-treat", 5, TopicType.Haruhi),
        new(302, "chokuretsu-wrapped-topic-mikuru's-tea", 1, TopicType.Mikuru),
        new(303, "chokuretsu-wrapped-topic-mikuru-and-the-club-president", 1, TopicType.Mikuru),
        new(304, "chokuretsu-wrapped-topic-the-computer-club's-goddess", 1, TopicType.Mikuru),
        new(305, "chokuretsu-wrapped-topic-mikuru-the-popular-favorite", 1, TopicType.Mikuru),
        new(306, "chokuretsu-wrapped-topic-mikuru's-weak-point", 1, TopicType.Mikuru),
        new(307, "chokuretsu-wrapped-topic-mikuru-and-the-computer-club", 1, TopicType.Mikuru),
        new(308, "chokuretsu-wrapped-topic-mikuru's-drawing", 1, TopicType.Mikuru),
        new(309, "chokuretsu-wrapped-topic-mikuru's-scary-story", 1, TopicType.Mikuru),
        new(310, "chokuretsu-wrapped-topic-mikuru-and-the-occult", 1, TopicType.Mikuru),
        new(311, "chokuretsu-wrapped-topic-mikuru-and-nagato", 1, TopicType.Mikuru),
        new(312, "chokuretsu-wrapped-topic-stained-book", 1, TopicType.Mikuru),
        new(313, "chokuretsu-wrapped-topic-plain-handkerchief", 1, TopicType.Mikuru),
        new(314, "chokuretsu-wrapped-topic-mikuru's-tears", 2, TopicType.Mikuru),
        new(315, "chokuretsu-wrapped-topic-mikuru's-cowardice", 2, TopicType.Mikuru),
        new(316, "chokuretsu-wrapped-topic-help-from-mikuru", 2, TopicType.Mikuru),
        new(317, "chokuretsu-wrapped-topic-mikuru's-snacks", 2, TopicType.Mikuru),
        new(318, "chokuretsu-wrapped-topic-mikuru's-trust-i", 2, TopicType.Mikuru),
        new(319, "chokuretsu-wrapped-topic-forgetful-mikuru", 2, TopicType.Mikuru),
        new(320, "chokuretsu-wrapped-topic-mikuru-the-klutz", 2, TopicType.Mikuru),
        new(325, "chokuretsu-wrapped-topic-passion-for-serving-tea", 2, TopicType.Mikuru),
        new(326, "chokuretsu-wrapped-topic-mikuru-and-tea-candies", 2, TopicType.Mikuru),
        new(327, "chokuretsu-wrapped-topic-mikuru's-flowers", 2, TopicType.Mikuru),
        new(328, "chokuretsu-wrapped-topic-careless-mikuru", 2, TopicType.Mikuru),
        new(330, "chokuretsu-wrapped-topic-mikuru's-recipe", 3, TopicType.Mikuru),
        new(331, "chokuretsu-wrapped-topic-mikuru's-calculator", 3, TopicType.Mikuru),
        new(332, "chokuretsu-wrapped-topic-mikuru's-efforts", 3, TopicType.Mikuru),
        new(333, "chokuretsu-wrapped-topic-a-flower-in-each-hand", 3, TopicType.Mikuru),
        new(334, "chokuretsu-wrapped-topic-mikuru-and-the-banana", 3, TopicType.Mikuru),
        new(335, "chokuretsu-wrapped-topic-dieting", 3, TopicType.Mikuru),
        new(336, "chokuretsu-wrapped-topic-mikuru-feels-regretful", 3, TopicType.Mikuru),
        new(337, "chokuretsu-wrapped-topic-kindness-towards-the-elderly", 3, TopicType.Mikuru),
        new(338, "chokuretsu-wrapped-topic-pom-poms", 3, TopicType.Mikuru),
        new(339, "chokuretsu-wrapped-topic-mikuru's-hands", 3, TopicType.Mikuru),
        new(340, "chokuretsu-wrapped-topic-mikuru's-towel", 3, TopicType.Mikuru),
        new(341, "chokuretsu-wrapped-topic-staff-room-happenings", 3, TopicType.Mikuru),
        new(342, "chokuretsu-wrapped-topic-president's-intense-stare", 3, TopicType.Mikuru),
        new(343, "chokuretsu-wrapped-topic-mikuru's-job", 3, TopicType.Mikuru),
        new(344, "chokuretsu-wrapped-topic-permission-to-borrow", 3, TopicType.Mikuru),
        new(345, "chokuretsu-wrapped-topic-fight-with-your-body", 3, TopicType.Mikuru),
        new(346, "chokuretsu-wrapped-topic-mikuru's-shield", 3, TopicType.Mikuru),
        new(347, "chokuretsu-wrapped-topic-seeing-things", 3, TopicType.Mikuru),
        new(348, "chokuretsu-wrapped-topic-feels-like-a-date", 3, TopicType.Mikuru),
        new(349, "chokuretsu-wrapped-topic-ice-cream-date", 3, TopicType.Mikuru),
        new(350, "chokuretsu-wrapped-topic-tripping-on-a-stone", 3, TopicType.Mikuru),
        new(351, "chokuretsu-wrapped-topic-mikuru's-trust-ii", 3, TopicType.Mikuru),
        new(352, "chokuretsu-wrapped-topic-plastic-bottle", 3, TopicType.Mikuru),
        new(353, "chokuretsu-wrapped-topic-maids-are-culture", 4, TopicType.Mikuru),
        new(354, "chokuretsu-wrapped-topic-mikuru's-gratitude", 4, TopicType.Mikuru),
        new(355, "chokuretsu-wrapped-topic-mikuru's-panic", 4, TopicType.Mikuru),
        new(356, "chokuretsu-wrapped-topic-mikuru's-warmth", 4, TopicType.Mikuru),
        new(357, "chokuretsu-wrapped-topic-mikuru's-drink", 4, TopicType.Mikuru),
        new(358, "chokuretsu-wrapped-topic-toy-phone", 4, TopicType.Mikuru),
        new(359, "chokuretsu-wrapped-topic-mikuru's-story", 4, TopicType.Mikuru),
        new(360, "chokuretsu-wrapped-topic-orange-juice", 4, TopicType.Mikuru),
        new(361, "chokuretsu-wrapped-topic-mikuru's-value", 4, TopicType.Mikuru),
        new(362, "chokuretsu-wrapped-topic-mikuru-invincibility-scheme", 4, TopicType.Mikuru),
        new(363, "chokuretsu-wrapped-topic-gentle-consideration", 4, TopicType.Mikuru),
        new(364, "chokuretsu-wrapped-topic-classified-information", 4, TopicType.Mikuru),
        new(365, "chokuretsu-wrapped-topic-mikuru's-respect", 4, TopicType.Mikuru),
        new(366, "chokuretsu-wrapped-topic-mikuru's-trembling", 4, TopicType.Mikuru),
        new(367, "chokuretsu-wrapped-topic-mikuru's-pen", 4, TopicType.Mikuru),
        new(368, "chokuretsu-wrapped-topic-mikuru's-motivation", 4, TopicType.Mikuru),
        new(369, "chokuretsu-wrapped-topic-mikuru's-support", 4, TopicType.Mikuru),
        new(370, "chokuretsu-wrapped-topic-mikuru's-delight", 4, TopicType.Mikuru),
        new(371, "chokuretsu-wrapped-topic-teary-eyed-mikuru", 4, TopicType.Mikuru),
        new(372, "chokuretsu-wrapped-topic-airheaded-mikuru", 4, TopicType.Mikuru),
        new(373, "chokuretsu-wrapped-topic-soft-hands", 4, TopicType.Mikuru),
        new(374, "chokuretsu-wrapped-topic-haruhi's-sympathizer", 4, TopicType.Mikuru),
        new(375, "chokuretsu-wrapped-topic-mikuru's-kindness", 4, TopicType.Mikuru),
        new(376, "chokuretsu-wrapped-topic-old-brooch", 4, TopicType.Mikuru),
        new(377, "chokuretsu-wrapped-topic-nice-assist", 4, TopicType.Mikuru),
        new(378, "chokuretsu-wrapped-topic-mikuru's-bodyguard", 4, TopicType.Mikuru),
        new(379, "chokuretsu-wrapped-topic-mikuru-the-worrywart", 4, TopicType.Mikuru),
        new(380, "chokuretsu-wrapped-topic-mikuru's-feelings", 5, TopicType.Mikuru),
        new(381, "chokuretsu-wrapped-topic-serious-mikuru", 5, TopicType.Mikuru),
        new(382, "chokuretsu-wrapped-topic-the-benefits-of-tea", 5, TopicType.Mikuru),
        new(383, "chokuretsu-wrapped-topic-request", 5, TopicType.Mikuru),
        new(384, "chokuretsu-wrapped-topic-loss-of-self-confidence", 5, TopicType.Mikuru),
        new(385, "chokuretsu-wrapped-topic-working-together-with-mikuru", 5, TopicType.Mikuru),
        new(386, "chokuretsu-wrapped-topic-wink", 5, TopicType.Mikuru),
        new(387, "chokuretsu-wrapped-topic-lucky-girl", 5, TopicType.Mikuru),
        new(388, "chokuretsu-wrapped-topic-positive", 5, TopicType.Mikuru),
        new(389, "chokuretsu-wrapped-topic-nice-mikuru", 5, TopicType.Mikuru),
        new(390, "chokuretsu-wrapped-topic-mikuru-the-phantom-thief?", 5, TopicType.Mikuru),
        new(391, "chokuretsu-wrapped-topic-mikuru's-competitive-spirit", 5, TopicType.Mikuru),
        new(392, "chokuretsu-wrapped-topic-mikuru's-courage", 5, TopicType.Mikuru),
        new(402, "chokuretsu-wrapped-topic-nagato's-book-search", 1, TopicType.Nagato),
        new(403, "chokuretsu-wrapped-topic-nagato's-affiliation", 1, TopicType.Nagato),
        new(404, "chokuretsu-wrapped-topic-nagato's-interest", 1, TopicType.Nagato),
        new(405, "chokuretsu-wrapped-topic-nagato's-composure", 1, TopicType.Nagato),
        new(406, "chokuretsu-wrapped-topic-nagato-and-the-pc", 1, TopicType.Nagato),
        new(407, "chokuretsu-wrapped-topic-nagato's-silence", 1, TopicType.Nagato),
        new(408, "chokuretsu-wrapped-topic-tough-girl-nagato", 1, TopicType.Nagato),
        new(409, "chokuretsu-wrapped-topic-nagato's-souvenir", 1, TopicType.Nagato),
        new(410, "chokuretsu-wrapped-topic-na-cat-o", 1, TopicType.Nagato),
        new(411, "chokuretsu-wrapped-topic-nagato's-sixth-sense", 1, TopicType.Nagato),
        new(412, "chokuretsu-wrapped-topic-nagato's-design-document", 1, TopicType.Nagato),
        new(413, "chokuretsu-wrapped-topic-nagato's-apology", 1, TopicType.Nagato),
        new(414, "chokuretsu-wrapped-topic-nagato's-book", 2, TopicType.Nagato),
        new(415, "chokuretsu-wrapped-topic-nagato's-stance", 2, TopicType.Nagato),
        new(416, "chokuretsu-wrapped-topic-nagato's-cooperation", 2, TopicType.Nagato),
        new(417, "chokuretsu-wrapped-topic-nagato's-complaint", 2, TopicType.Nagato),
        new(418, "chokuretsu-wrapped-topic-super-speed-reader-nagato", 2, TopicType.Nagato),
        new(419, "chokuretsu-wrapped-topic-nagato's-probability-note", 2, TopicType.Nagato),
        new(420, "chokuretsu-wrapped-topic-nagato's-hint-i", 2, TopicType.Nagato),
        new(425, "chokuretsu-wrapped-topic-nagato-the-skilled", 2, TopicType.Nagato),
        new(426, "chokuretsu-wrapped-topic-sit.", 2, TopicType.Nagato),
        new(427, "chokuretsu-wrapped-topic-nagato's-pressed-flower", 2, TopicType.Nagato),
        new(428, "chokuretsu-wrapped-topic-nagato's-bluntness", 2, TopicType.Nagato),
        new(430, "chokuretsu-wrapped-topic-trust-in-nagato", 3, TopicType.Nagato),
        new(431, "chokuretsu-wrapped-topic-curry-bread", 3, TopicType.Nagato),
        new(432, "chokuretsu-wrapped-topic-nagato's-banana", 3, TopicType.Nagato),
        new(433, "chokuretsu-wrapped-topic-nagato-and-meat", 3, TopicType.Nagato),
        new(434, "chokuretsu-wrapped-topic-marshmallow", 3, TopicType.Nagato),
        new(435, "chokuretsu-wrapped-topic-muryo-taisu", 3, TopicType.Nagato),
        new(436, "chokuretsu-wrapped-topic-nagato's-sigh", 3, TopicType.Nagato),
        new(437, "chokuretsu-wrapped-topic-nagato's-wisdom", 3, TopicType.Nagato),
        new(438, "chokuretsu-wrapped-topic-nagato-and-curry", 3, TopicType.Nagato),
        new(439, "chokuretsu-wrapped-topic-walking-encyclopedia", 3, TopicType.Nagato),
        new(440, "chokuretsu-wrapped-topic-nagato-and-puzzles", 3, TopicType.Nagato),
        new(441, "chokuretsu-wrapped-topic-coordinates", 3, TopicType.Nagato),
        new(442, "chokuretsu-wrapped-topic-identifying-trouble", 3, TopicType.Nagato),
        new(443, "chokuretsu-wrapped-topic-tin-soldiers", 3, TopicType.Nagato),
        new(444, "chokuretsu-wrapped-topic-nagato's-hint-ii", 3, TopicType.Nagato),
        new(445, "chokuretsu-wrapped-topic-information-warfare", 3, TopicType.Nagato),
        new(446, "chokuretsu-wrapped-topic-out-of-touch", 3, TopicType.Nagato),
        new(447, "chokuretsu-wrapped-topic-skewer-of-ultimate-misfortune", 3, TopicType.Nagato),
        new(448, "chokuretsu-wrapped-topic-bowl", 3, TopicType.Nagato),
        new(449, "chokuretsu-wrapped-topic-shaved-ice-date", 3, TopicType.Nagato),
        new(450, "chokuretsu-wrapped-topic-insert-advertisement", 3, TopicType.Nagato),
        new(451, "chokuretsu-wrapped-topic-nagato's-slide-rule", 4, TopicType.Nagato),
        new(452, "chokuretsu-wrapped-topic-nagato's-amulet", 4, TopicType.Nagato),
        new(453, "chokuretsu-wrapped-topic-unable-to-respond", 4, TopicType.Nagato),
        new(454, "chokuretsu-wrapped-topic-capable-nagato", 4, TopicType.Nagato),
        new(455, "chokuretsu-wrapped-topic-cicada-shell", 4, TopicType.Nagato),
        new(456, "chokuretsu-wrapped-topic-chocolate", 4, TopicType.Nagato),
        new(457, "chokuretsu-wrapped-topic-subculture-magazine", 4, TopicType.Nagato),
        new(458, "chokuretsu-wrapped-topic-mineral-water", 4, TopicType.Nagato),
        new(459, "chokuretsu-wrapped-topic-nagato's-consent", 4, TopicType.Nagato),
        new(460, "chokuretsu-wrapped-topic-nagato's-handheld-mirror", 4, TopicType.Nagato),
        new(461, "chokuretsu-wrapped-topic-nagato's-backup", 4, TopicType.Nagato),
        new(462, "chokuretsu-wrapped-topic-nagato's-appraisal", 4, TopicType.Nagato),
        new(463, "chokuretsu-wrapped-topic-nagato's-glasses", 4, TopicType.Nagato),
        new(464, "chokuretsu-wrapped-topic-nagato's-curry", 4, TopicType.Nagato),
        new(465, "chokuretsu-wrapped-topic-nagato's-reaction", 4, TopicType.Nagato),
        new(466, "chokuretsu-wrapped-topic-the-charm", 4, TopicType.Nagato),
        new(467, "chokuretsu-wrapped-topic-nagato's-stamp-of-approval", 4, TopicType.Nagato),
        new(468, "chokuretsu-wrapped-topic-evidence?", 4, TopicType.Nagato),
        new(469, "chokuretsu-wrapped-topic-dose-of-motivation", 4, TopicType.Nagato),
        new(470, "chokuretsu-wrapped-topic-nagato's-triumphant-look", 4, TopicType.Nagato),
        new(471, "chokuretsu-wrapped-topic-nagato's-estimation", 4, TopicType.Nagato),
        new(472, "chokuretsu-wrapped-topic-nagato's-hand", 4, TopicType.Nagato),
        new(473, "chokuretsu-wrapped-topic-nagato's-restraint", 4, TopicType.Nagato),
        new(474, "chokuretsu-wrapped-topic-mysterious-mineral", 4, TopicType.Nagato),
        new(475, "chokuretsu-wrapped-topic-instant-shutdown", 4, TopicType.Nagato),
        new(476, "chokuretsu-wrapped-topic-chance-of-success", 4, TopicType.Nagato),
        new(477, "chokuretsu-wrapped-topic-happiness", 4, TopicType.Nagato),
        new(478, "chokuretsu-wrapped-topic-countermeasure", 5, TopicType.Nagato),
        new(479, "chokuretsu-wrapped-topic-foresight", 5, TopicType.Nagato),
        new(480, "chokuretsu-wrapped-topic-rendezvous-with-nagato", 5, TopicType.Nagato),
        new(481, "chokuretsu-wrapped-topic-two-nagatos", 5, TopicType.Nagato),
        new(482, "chokuretsu-wrapped-topic-incredibly-chivalrous-nagato", 5, TopicType.Nagato),
        new(483, "chokuretsu-wrapped-topic-careful-explanation", 5, TopicType.Nagato),
        new(484, "chokuretsu-wrapped-topic-neo-chess", 5, TopicType.Nagato),
        new(502, "chokuretsu-wrapped-topic-koizumi's-flattery", 1, TopicType.Koizumi),
        new(503, "chokuretsu-wrapped-topic-a-clever-person", 1, TopicType.Koizumi),
        new(504, "chokuretsu-wrapped-topic-interesting-game", 1, TopicType.Koizumi),
        new(505, "chokuretsu-wrapped-topic-koizumi-and-the-computer", 1, TopicType.Koizumi),
        new(506, "chokuretsu-wrapped-topic-koizumi-and-the-game", 1, TopicType.Koizumi),
        new(507, "chokuretsu-wrapped-topic-koizumi's-inquiry", 1, TopicType.Koizumi),
        new(508, "chokuretsu-wrapped-topic-something-unusual", 1, TopicType.Koizumi),
        new(509, "chokuretsu-wrapped-topic-koizumi's-hypothesis", 1, TopicType.Koizumi),
        new(510, "chokuretsu-wrapped-topic-a-ghost's-true-form", 1, TopicType.Koizumi),
        new(511, "chokuretsu-wrapped-topic-superb-deputy-brigade-chief", 1, TopicType.Koizumi),
        new(512, "chokuretsu-wrapped-topic-prudent-koizumi", 1, TopicType.Koizumi),
        new(513, "chokuretsu-wrapped-topic-koizumi's-apology", 1, TopicType.Koizumi),
        new(514, "chokuretsu-wrapped-topic-esp", 2, TopicType.Koizumi),
        new(515, "chokuretsu-wrapped-topic-koizumi-the-nitpicker", 2, TopicType.Koizumi),
        new(516, "chokuretsu-wrapped-topic-the-koizumi-smile", 2, TopicType.Koizumi),
        new(517, "chokuretsu-wrapped-topic-koizumi-the-self-assured", 2, TopicType.Koizumi),
        new(518, "chokuretsu-wrapped-topic-koizumi's-cell-phone", 2, TopicType.Koizumi),
        new(519, "chokuretsu-wrapped-topic-koizumi's-candy", 2, TopicType.Koizumi),
        new(520, "chokuretsu-wrapped-topic-koizumi's-report", 2, TopicType.Koizumi),
        new(525, "chokuretsu-wrapped-topic-koizumi-the-prize-pupil", 2, TopicType.Koizumi),
        new(526, "chokuretsu-wrapped-topic-koizumi's-new-power?", 2, TopicType.Koizumi),
        new(527, "chokuretsu-wrapped-topic-koizumi's-haruhi-theory", 2, TopicType.Koizumi),
        new(528, "chokuretsu-wrapped-topic-koizumi's-advice", 2, TopicType.Koizumi),
        new(530, "chokuretsu-wrapped-topic-koizumi's-true-feelings", 3, TopicType.Koizumi),
        new(531, "chokuretsu-wrapped-topic-machiavellian-koizumi", 3, TopicType.Koizumi),
        new(532, "chokuretsu-wrapped-topic-lottery-ticket", 3, TopicType.Koizumi),
        new(533, "chokuretsu-wrapped-topic-koizumi's-calling", 3, TopicType.Koizumi),
        new(534, "chokuretsu-wrapped-topic-tendency-to-lecture", 3, TopicType.Koizumi),
        new(535, "chokuretsu-wrapped-topic-cheat-sheet", 3, TopicType.Koizumi),
        new(536, "chokuretsu-wrapped-topic-koizumi's-bitter-smile", 3, TopicType.Koizumi),
        new(537, "chokuretsu-wrapped-topic-madam-and-koizumi", 3, TopicType.Koizumi),
        new(538, "chokuretsu-wrapped-topic-koizumi's-gaze", 3, TopicType.Koizumi),
        new(539, "chokuretsu-wrapped-topic-koizumi's-dowsing-rod", 3, TopicType.Koizumi),
        new(540, "chokuretsu-wrapped-topic-koizumi's-matches", 3, TopicType.Koizumi),
        new(541, "chokuretsu-wrapped-topic-cowardly-koizumi", 3, TopicType.Koizumi),
        new(542, "chokuretsu-wrapped-topic-half-baked-koizumi", 3, TopicType.Koizumi),
        new(543, "chokuretsu-wrapped-topic-“agency”-non-involvement", 3, TopicType.Koizumi),
        new(544, "chokuretsu-wrapped-topic-koizumi's-hint", 3, TopicType.Koizumi),
        new(545, "chokuretsu-wrapped-topic-the-ends-justify-the-means", 3, TopicType.Koizumi),
        new(546, "chokuretsu-wrapped-topic-a-short-rest", 3, TopicType.Koizumi),
        new(547, "chokuretsu-wrapped-topic-koizumi's-warning", 3, TopicType.Koizumi),
        new(548, "chokuretsu-wrapped-topic-shower", 3, TopicType.Koizumi),
        new(549, "chokuretsu-wrapped-topic-koizumi's-trust", 3, TopicType.Koizumi),
        new(550, "chokuretsu-wrapped-topic-summer-schedule", 3, TopicType.Koizumi),
        new(551, "chokuretsu-wrapped-topic-skilled-koizumi", 4, TopicType.Koizumi),
        new(552, "chokuretsu-wrapped-topic-koizumi's-goddess?", 4, TopicType.Koizumi),
        new(553, "chokuretsu-wrapped-topic-look-of-envy", 4, TopicType.Koizumi),
        new(554, "chokuretsu-wrapped-topic-koizumi's-heinous-act", 4, TopicType.Koizumi),
        new(555, "chokuretsu-wrapped-topic-koizumi's-sarcasm", 4, TopicType.Koizumi),
        new(556, "chokuretsu-wrapped-topic-wealth-of-knowledge", 4, TopicType.Koizumi),
        new(557, "chokuretsu-wrapped-topic-capable-koizumi", 4, TopicType.Koizumi),
        new(558, "chokuretsu-wrapped-topic-tabletop-games", 4, TopicType.Koizumi),
        new(559, "chokuretsu-wrapped-topic-koizumi's-conversation-skills", 4, TopicType.Koizumi),
        new(560, "chokuretsu-wrapped-topic-koizumi's-“it's-up-to-you!”", 4, TopicType.Koizumi),
        new(561, "chokuretsu-wrapped-topic-harsh-koizumi", 4, TopicType.Koizumi),
        new(562, "chokuretsu-wrapped-topic-embarrassing-photo", 4, TopicType.Koizumi),
        new(563, "chokuretsu-wrapped-topic-koizumi's-supposition", 4, TopicType.Koizumi),
        new(564, "chokuretsu-wrapped-topic-koizumi's-gutsiness", 4, TopicType.Koizumi),
        new(565, "chokuretsu-wrapped-topic-koizumi's-nod", 4, TopicType.Koizumi),
        new(566, "chokuretsu-wrapped-topic-icy-stare", 4, TopicType.Koizumi),
        new(567, "chokuretsu-wrapped-topic-medicinal-herb", 4, TopicType.Koizumi),
        new(568, "chokuretsu-wrapped-topic-encouragement", 4, TopicType.Koizumi),
        new(569, "chokuretsu-wrapped-topic-koizumi's-gratitude", 4, TopicType.Koizumi),
        new(570, "chokuretsu-wrapped-topic-realism", 4, TopicType.Koizumi),
        new(571, "chokuretsu-wrapped-topic-bad-at-games", 4, TopicType.Koizumi),
        new(572, "chokuretsu-wrapped-topic-taniguchi-on-the-roof", 4, TopicType.Koizumi),
        new(573, "chokuretsu-wrapped-topic-koizumi's-pride", 4, TopicType.Koizumi),
        new(574, "chokuretsu-wrapped-topic-trust-in-koizumi", 4, TopicType.Koizumi),
        new(575, "chokuretsu-wrapped-topic-preaching-to-deaf-ears", 4, TopicType.Koizumi),
        new(576, "chokuretsu-wrapped-topic-shoulder-massage", 4, TopicType.Koizumi),
        new(577, "chokuretsu-wrapped-topic-visualization", 5, TopicType.Koizumi),
        new(578, "chokuretsu-wrapped-topic-koizumi,-the-main-act", 5, TopicType.Koizumi),
        new(579, "chokuretsu-wrapped-topic-a-tide-turning-move", 5, TopicType.Koizumi),
        new(580, "chokuretsu-wrapped-topic-tournament-bracket", 5, TopicType.Koizumi),
        new(581, "chokuretsu-wrapped-topic-koizumi's-silver-tongue", 5, TopicType.Koizumi),
        new(582, "chokuretsu-wrapped-topic-koizumi's-friend", 5, TopicType.Koizumi),
        new(583, "chokuretsu-wrapped-topic-camaraderie", 5, TopicType.Koizumi),
        new(584, "chokuretsu-wrapped-topic-koizumi's-own-way", 5, TopicType.Koizumi),
        new(585, "chokuretsu-wrapped-topic-koizumi's-keen-eyes", 5, TopicType.Koizumi),
        new(586, "chokuretsu-wrapped-topic-koizumi's-encouragement", 5, TopicType.Koizumi),
        new(587, "chokuretsu-wrapped-topic-koizumi's-self-confidence", 5, TopicType.Koizumi),
        new(588, "chokuretsu-wrapped-topic-simple-deduction", 5, TopicType.Koizumi),
        new(589, "chokuretsu-wrapped-topic-koizumi's-property", 5, TopicType.Koizumi),
        new(602, "chokuretsu-wrapped-topic-disappointment", 1, TopicType.Sub),
        new(603, "chokuretsu-wrapped-topic-japan,-the-nation-of-games", 1, TopicType.Sub),
        new(604, "chokuretsu-wrapped-topic-mascot", 1, TopicType.Sub),
        new(605, "chokuretsu-wrapped-topic-respect", 1, TopicType.Sub),
        new(606, "chokuretsu-wrapped-topic-the-clubroom-pc", 1, TopicType.Sub),
        new(607, "chokuretsu-wrapped-topic-literary-club", 1, TopicType.Sub),
        new(608, "chokuretsu-wrapped-topic-eye-strain", 1, TopicType.Sub),
        new(609, "chokuretsu-wrapped-topic-pc-game", 1, TopicType.Sub),
        new(610, "chokuretsu-wrapped-topic-board-game", 1, TopicType.Sub),
        new(611, "chokuretsu-wrapped-topic-warm-mood", 1, TopicType.Sub),
        new(612, "chokuretsu-wrapped-topic-sympathy", 1, TopicType.Sub),
        new(613, "chokuretsu-wrapped-topic-flattery", 1, TopicType.Sub),
        new(614, "chokuretsu-wrapped-topic-h₂o", 1, TopicType.Sub),
        new(615, "chokuretsu-wrapped-topic-calm-and-quick", 1, TopicType.Sub),
        new(616, "chokuretsu-wrapped-topic-darkest-under-the-lamp-post", 1, TopicType.Sub),
        new(617, "chokuretsu-wrapped-topic-group-action", 1, TopicType.Sub),
        new(618, "chokuretsu-wrapped-topic-sos-brigade-homepage", 1, TopicType.Sub),
        new(619, "chokuretsu-wrapped-topic-breakthrough", 1, TopicType.Sub),
        new(620, "chokuretsu-wrapped-topic-important-things", 1, TopicType.Sub),
        new(621, "chokuretsu-wrapped-topic-concern", 1, TopicType.Sub),
        new(622, "chokuretsu-wrapped-topic-final-wish", 1, TopicType.Sub),
        new(623, "chokuretsu-wrapped-topic-work-delay", 1, TopicType.Sub),
        new(624, "chokuretsu-wrapped-topic-a-small-kindness", 1, TopicType.Sub),
        new(625, "chokuretsu-wrapped-topic-electric-sheep", 1, TopicType.Sub),
        new(626, "chokuretsu-wrapped-topic-time-for-“something”", 1, TopicType.Sub),
        new(627, "chokuretsu-wrapped-topic-club-president's-anxiety", 1, TopicType.Sub),
        new(628, "chokuretsu-wrapped-topic-omitted-letter-misprint", 1, TopicType.Sub),
        new(629, "chokuretsu-wrapped-topic-treatment", 1, TopicType.Sub),
        new(630, "chokuretsu-wrapped-topic-going-in-circles", 1, TopicType.Sub),
        new(631, "chokuretsu-wrapped-topic-feigning-ignorance", 1, TopicType.Sub),
        new(632, "chokuretsu-wrapped-topic-useful-information", 1, TopicType.Sub),
        new(633, "chokuretsu-wrapped-topic-open-window", 1, TopicType.Sub),
        new(634, "chokuretsu-wrapped-topic-scary-object", 1, TopicType.Sub),
        new(635, "chokuretsu-wrapped-topic-ignorance-is-bliss", 1, TopicType.Sub),
        new(636, "chokuretsu-wrapped-topic-mysterious-radio-waves", 1, TopicType.Sub),
        new(637, "chokuretsu-wrapped-topic-the-power-of-imagination", 1, TopicType.Sub),
        new(638, "chokuretsu-wrapped-topic-nail-dirt", 1, TopicType.Sub),
        new(639, "chokuretsu-wrapped-topic-answer-sheet", 1, TopicType.Sub),
        new(640, "chokuretsu-wrapped-topic-extra-history-lesson", 1, TopicType.Sub),
        new(641, "chokuretsu-wrapped-topic-demonic-whispering", 1, TopicType.Sub),
        new(642, "chokuretsu-wrapped-topic-very-fast", 1, TopicType.Sub),
        new(643, "chokuretsu-wrapped-topic-band-aid", 1, TopicType.Sub),
        new(644, "chokuretsu-wrapped-topic-heroine", 1, TopicType.Sub),
        new(645, "chokuretsu-wrapped-topic-sos-brigade-activity-log", 1, TopicType.Sub),
        new(646, "chokuretsu-wrapped-topic-angel-descent", 1, TopicType.Sub),
        new(647, "chokuretsu-wrapped-topic-distance-between-buildings", 1, TopicType.Sub),
        new(648, "chokuretsu-wrapped-topic-strategic-retreat", 1, TopicType.Sub),
        new(649, "chokuretsu-wrapped-topic-empty-shell", 1, TopicType.Sub),
        new(650, "chokuretsu-wrapped-topic-pitiable-computer-society", 1, TopicType.Sub),
        new(651, "chokuretsu-wrapped-topic-a-profoundly-wonderful-story", 1, TopicType.Sub),
        new(652, "chokuretsu-wrapped-topic-a-matter-of-time", 1, TopicType.Sub),
        new(653, "chokuretsu-wrapped-topic-boy's-team", 1, TopicType.Sub),
        new(654, "chokuretsu-wrapped-topic-falling-on-your-backside", 1, TopicType.Sub),
        new(655, "chokuretsu-wrapped-topic-intricate-workmanship", 1, TopicType.Sub),
        new(656, "chokuretsu-wrapped-topic-acceptance-is-also-important", 1, TopicType.Sub),
        new(657, "chokuretsu-wrapped-topic-futuristic-ghost-stories", 2, TopicType.Sub),
        new(658, "chokuretsu-wrapped-topic-overthinking", 2, TopicType.Sub),
        new(659, "chokuretsu-wrapped-topic-the-cursed-music-room", 2, TopicType.Sub),
        new(660, "chokuretsu-wrapped-topic-sci-fi-novel", 2, TopicType.Sub),
        new(661, "chokuretsu-wrapped-topic-doing-it-yourself", 2, TopicType.Sub),
        new(662, "chokuretsu-wrapped-topic-treated-to-juice", 2, TopicType.Sub),
        new(663, "chokuretsu-wrapped-topic-silent-observer", 2, TopicType.Sub),
        new(664, "chokuretsu-wrapped-topic-the-fate-of-the-world", 2, TopicType.Sub),
        new(665, "chokuretsu-wrapped-topic-annoying-situation", 2, TopicType.Sub),
        new(666, "chokuretsu-wrapped-topic-danger:-do-not-touch", 2, TopicType.Sub),
        new(667, "chokuretsu-wrapped-topic-the-truth-of-the-matter", 2, TopicType.Sub),
        new(668, "chokuretsu-wrapped-topic-closing-ceremony", 2, TopicType.Sub),
        new(669, "chokuretsu-wrapped-topic-president's-wish", 2, TopicType.Sub),
        new(670, "chokuretsu-wrapped-topic-inexplicable-conduct", 2, TopicType.Sub),
        new(671, "chokuretsu-wrapped-topic-computer-club-&-sheet-music", 2, TopicType.Sub),
        new(672, "chokuretsu-wrapped-topic-suspicious-data", 2, TopicType.Sub),
        new(673, "chokuretsu-wrapped-topic-insufficient-data", 2, TopicType.Sub),
        new(674, "chokuretsu-wrapped-topic-president's-orders", 2, TopicType.Sub),
        new(675, "chokuretsu-wrapped-topic-plasma", 2, TopicType.Sub),
        new(676, "chokuretsu-wrapped-topic-pouting", 2, TopicType.Sub),
        new(677, "chokuretsu-wrapped-topic-haruhi-the-unwavering", 2, TopicType.Sub),
        new(678, "chokuretsu-wrapped-topic-speed-reading-nagato-style", 2, TopicType.Sub),
        new(679, "chokuretsu-wrapped-topic-haruhi's-theory", 2, TopicType.Sub),
        new(680, "chokuretsu-wrapped-topic-budget-application-form", 2, TopicType.Sub),
        new(681, "chokuretsu-wrapped-topic-supplementary-class-schedule", 2, TopicType.Sub),
        new(682, "chokuretsu-wrapped-topic-physical-examination-notice", 2, TopicType.Sub),
        new(683, "chokuretsu-wrapped-topic-literary-club-band", 2, TopicType.Sub),
        new(684, "chokuretsu-wrapped-topic-something-that-passed-by", 2, TopicType.Sub),
        new(685, "chokuretsu-wrapped-topic-speak-of-the-devil…", 2, TopicType.Sub),
        new(686, "chokuretsu-wrapped-topic-day-of-deadline", 2, TopicType.Sub),
        new(687, "chokuretsu-wrapped-topic-beautiful-flower", 2, TopicType.Sub),
        new(688, "chokuretsu-wrapped-topic-roadside-flower", 2, TopicType.Sub),
        new(689, "chokuretsu-wrapped-topic-bluebird-of-happiness", 2, TopicType.Sub),
        new(690, "chokuretsu-wrapped-topic-a-minor-coincidence", 2, TopicType.Sub),
        new(691, "chokuretsu-wrapped-topic-deadline", 2, TopicType.Sub),
        new(692, "chokuretsu-wrapped-topic-an-award-of-some-sort", 2, TopicType.Sub),
        new(693, "chokuretsu-wrapped-topic-chatting-over-tea", 2, TopicType.Sub),
        new(694, "chokuretsu-wrapped-topic-premature-jab", 2, TopicType.Sub),
        new(695, "chokuretsu-wrapped-topic-club-member's-gratitude", 2, TopicType.Sub),
        new(696, "chokuretsu-wrapped-topic-attack-of-the-club-member", 2, TopicType.Sub),
        new(697, "chokuretsu-wrapped-topic-something-summery", 3, TopicType.Sub),
        new(698, "chokuretsu-wrapped-topic-cheap-sympathy", 3, TopicType.Sub),
        new(699, "chokuretsu-wrapped-topic-mikuru's-quip", 3, TopicType.Sub),
        new(700, "chokuretsu-wrapped-topic-expensive-receipt", 3, TopicType.Sub),
        new(701, "chokuretsu-wrapped-topic-bell", 3, TopicType.Sub),
        new(702, "chokuretsu-wrapped-topic-outrageous-ingredients", 3, TopicType.Sub),
        new(703, "chokuretsu-wrapped-topic-book-barbecue", 3, TopicType.Sub),
        new(704, "chokuretsu-wrapped-topic-feelings-of-guilt", 3, TopicType.Sub),
        new(705, "chokuretsu-wrapped-topic-watermelon-seeds", 3, TopicType.Sub),
        new(706, "chokuretsu-wrapped-topic-dried-banana", 3, TopicType.Sub),
        new(707, "chokuretsu-wrapped-topic-mysterious-ofuda", 3, TopicType.Sub),
        new(708, "chokuretsu-wrapped-topic-junk", 3, TopicType.Sub),
        new(709, "chokuretsu-wrapped-topic-mysterious-mechanism", 3, TopicType.Sub),
        new(710, "chokuretsu-wrapped-topic-fruit-king", 3, TopicType.Sub),
        new(711, "chokuretsu-wrapped-topic-moon-viewing-banquet", 3, TopicType.Sub),
        new(712, "chokuretsu-wrapped-topic-summer-breeze-cd", 3, TopicType.Sub),
        new(713, "chokuretsu-wrapped-topic-seven-spotted-ladybug", 3, TopicType.Sub),
        new(714, "chokuretsu-wrapped-topic-meter-long-iron-skewer", 3, TopicType.Sub),
        new(715, "chokuretsu-wrapped-topic-artificial-feeding", 3, TopicType.Sub),
        new(716, "chokuretsu-wrapped-topic-junk-storage", 3, TopicType.Sub),
        new(717, "chokuretsu-wrapped-topic-kyon's-deduction", 3, TopicType.Sub),
        new(718, "chokuretsu-wrapped-topic-little-sister's-helping-hand", 3, TopicType.Sub),
        new(719, "chokuretsu-wrapped-topic-lucky-item", 3, TopicType.Sub),
        new(720, "chokuretsu-wrapped-topic-in-the-palm-of-your-hand", 3, TopicType.Sub),
        new(721, "chokuretsu-wrapped-topic-medal-of-honor", 3, TopicType.Sub),
        new(722, "chokuretsu-wrapped-topic-target", 3, TopicType.Sub),
        new(723, "chokuretsu-wrapped-topic-poltergeist", 3, TopicType.Sub),
        new(724, "chokuretsu-wrapped-topic-mosquito-coil", 3, TopicType.Sub),
        new(725, "chokuretsu-wrapped-topic-running-away", 3, TopicType.Sub),
        new(726, "chokuretsu-wrapped-topic-ray-gun", 3, TopicType.Sub),
        new(727, "chokuretsu-wrapped-topic-pocket-paperback", 3, TopicType.Sub),
        new(728, "chokuretsu-wrapped-topic-seal-of-approval", 3, TopicType.Sub),
        new(729, "chokuretsu-wrapped-topic-sometime", 3, TopicType.Sub),
        new(730, "chokuretsu-wrapped-topic-ghost", 3, TopicType.Sub),
        new(731, "chokuretsu-wrapped-topic-enthusiasm", 3, TopicType.Sub),
        new(732, "chokuretsu-wrapped-topic-secondary-disaster", 3, TopicType.Sub),
        new(733, "chokuretsu-wrapped-topic-resonance", 3, TopicType.Sub),
        new(734, "chokuretsu-wrapped-topic-chlorine", 4, TopicType.Sub),
        new(735, "chokuretsu-wrapped-topic-spirit-of-service", 4, TopicType.Sub),
        new(736, "chokuretsu-wrapped-topic-surprisingly-good-person", 4, TopicType.Sub),
        new(737, "chokuretsu-wrapped-topic-ventra-badge", 4, TopicType.Sub),
        new(738, "chokuretsu-wrapped-topic-marble", 4, TopicType.Sub),
        new(739, "chokuretsu-wrapped-topic-ordinary", 4, TopicType.Sub),
        new(740, "chokuretsu-wrapped-topic-being-diplomatic", 4, TopicType.Sub),
        new(741, "chokuretsu-wrapped-topic-good-fortune", 4, TopicType.Sub),
        new(742, "chokuretsu-wrapped-topic-emergency", 4, TopicType.Sub),
        new(743, "chokuretsu-wrapped-topic-air-freshener", 4, TopicType.Sub),
        new(744, "chokuretsu-wrapped-topic-fluorescent-panel", 4, TopicType.Sub),
        new(745, "chokuretsu-wrapped-topic-wet-cloth", 4, TopicType.Sub),
        new(746, "chokuretsu-wrapped-topic-bucket-sound", 4, TopicType.Sub),
        new(747, "chokuretsu-wrapped-topic-weekly-magazine", 4, TopicType.Sub),
        new(748, "chokuretsu-wrapped-topic-baseball-equipment", 4, TopicType.Sub),
        new(749, "chokuretsu-wrapped-topic-warabimochi", 4, TopicType.Sub),
        new(750, "chokuretsu-wrapped-topic-glowstick", 4, TopicType.Sub),
        new(751, "chokuretsu-wrapped-topic-manga-magazine", 4, TopicType.Sub),
        new(752, "chokuretsu-wrapped-topic-astrology-book", 4, TopicType.Sub),
        new(753, "chokuretsu-wrapped-topic-object-of-interest", 4, TopicType.Sub),
        new(754, "chokuretsu-wrapped-topic-budget-constraints", 4, TopicType.Sub),
        new(755, "chokuretsu-wrapped-topic-tabletop-game", 4, TopicType.Sub),
        new(756, "chokuretsu-wrapped-topic-standoff-surrender", 4, TopicType.Sub),
        new(757, "chokuretsu-wrapped-topic-clubroom-furnishing", 4, TopicType.Sub),
        new(758, "chokuretsu-wrapped-topic-shopping-squad", 4, TopicType.Sub),
        new(759, "chokuretsu-wrapped-topic-adenosine-receptors", 4, TopicType.Sub),
        new(760, "chokuretsu-wrapped-topic-unwarranted-spite", 4, TopicType.Sub),
        new(761, "chokuretsu-wrapped-topic-deliberate", 4, TopicType.Sub),
        new(762, "chokuretsu-wrapped-topic-the-idol-of-north-high", 4, TopicType.Sub),
        new(763, "chokuretsu-wrapped-topic-yellow-card", 4, TopicType.Sub),
        new(764, "chokuretsu-wrapped-topic-talking-privately", 4, TopicType.Sub),
        new(765, "chokuretsu-wrapped-topic-the-next-test-of-courage", 4, TopicType.Sub),
        new(766, "chokuretsu-wrapped-topic-time-limit", 4, TopicType.Sub),
        new(767, "chokuretsu-wrapped-topic-scarab-beetle", 4, TopicType.Sub),
        new(768, "chokuretsu-wrapped-topic-knees", 4, TopicType.Sub),
        new(769, "chokuretsu-wrapped-topic-unfortunate-circumstance", 4, TopicType.Sub),
        new(770, "chokuretsu-wrapped-topic-break-time", 4, TopicType.Sub),
        new(771, "chokuretsu-wrapped-topic-eloquence", 4, TopicType.Sub),
        new(772, "chokuretsu-wrapped-topic-coward", 4, TopicType.Sub),
        new(773, "chokuretsu-wrapped-topic-fun-test-of-courage", 4, TopicType.Sub),
        new(774, "chokuretsu-wrapped-topic-drone-beetle", 4, TopicType.Sub),
        new(775, "chokuretsu-wrapped-topic-hallway-echo", 4, TopicType.Sub),
        new(776, "chokuretsu-wrapped-topic-ghost?", 4, TopicType.Sub),
        new(777, "chokuretsu-wrapped-topic-sunflower-seeds", 4, TopicType.Sub),
        new(778, "chokuretsu-wrapped-topic-retribution", 4, TopicType.Sub),
        new(779, "chokuretsu-wrapped-topic-ordinary-human", 4, TopicType.Sub),
        new(780, "chokuretsu-wrapped-topic-computer-society-romanticism", 4, TopicType.Sub),
        new(781, "chokuretsu-wrapped-topic-trembling-with-fear", 4, TopicType.Sub),
        new(782, "chokuretsu-wrapped-topic-flower-seed", 4, TopicType.Sub),
        new(783, "chokuretsu-wrapped-topic-parasitism", 4, TopicType.Sub),
        new(784, "chokuretsu-wrapped-topic-indoor-shoes", 4, TopicType.Sub),
        new(785, "chokuretsu-wrapped-topic-notebook-paper-scrap", 4, TopicType.Sub),
        new(786, "chokuretsu-wrapped-topic-apology", 4, TopicType.Sub),
        new(787, "chokuretsu-wrapped-topic-punishment", 4, TopicType.Sub),
        new(788, "chokuretsu-wrapped-topic-intuition", 4, TopicType.Sub),
        new(789, "chokuretsu-wrapped-topic-photo-of-taniguchi", 4, TopicType.Sub),
        new(790, "chokuretsu-wrapped-topic-personal-relationship", 4, TopicType.Sub),
        new(791, "chokuretsu-wrapped-topic-hose", 4, TopicType.Sub),
        new(792, "chokuretsu-wrapped-topic-the-taste-of-victory", 5, TopicType.Sub),
        new(793, "chokuretsu-wrapped-topic-handmade-pieces", 5, TopicType.Sub),
        new(794, "chokuretsu-wrapped-topic-reliable-friend", 5, TopicType.Sub),
        new(795, "chokuretsu-wrapped-topic-special-chess-training", 5, TopicType.Sub),
        new(796, "chokuretsu-wrapped-topic-kyon-goes-first", 5, TopicType.Sub),
        new(797, "chokuretsu-wrapped-topic-three-heads-are-better-than-one", 5, TopicType.Sub),
        new(798, "chokuretsu-wrapped-topic-one-to-one", 5, TopicType.Sub),
        new(799, "chokuretsu-wrapped-topic-sos-brigade-chess-champion", 5, TopicType.Sub),
        new(800, "chokuretsu-wrapped-topic-kyon's-doubts", 5, TopicType.Sub),
        new(801, "chokuretsu-wrapped-topic-chewing-gum-strip", 5, TopicType.Sub),
        new(802, "chokuretsu-wrapped-topic-useless", 5, TopicType.Sub),
        new(803, "chokuretsu-wrapped-topic-indecipherable", 5, TopicType.Sub),
        new(804, "chokuretsu-wrapped-topic-mysterious-address", 5, TopicType.Sub),
        new(805, "chokuretsu-wrapped-topic-impromptu-decision", 5, TopicType.Sub),
        new(806, "chokuretsu-wrapped-topic-numb-legs", 5, TopicType.Sub),
        new(807, "chokuretsu-wrapped-topic-tarpaulin", 5, TopicType.Sub),
        new(808, "chokuretsu-wrapped-topic-bold-opinion", 5, TopicType.Sub),
        new(809, "chokuretsu-wrapped-topic-girls'-accessories", 5, TopicType.Sub),
        new(810, "chokuretsu-wrapped-topic-stray-bullet", 5, TopicType.Sub),
        new(811, "chokuretsu-wrapped-topic-standing-firm", 5, TopicType.Sub),
        new(812, "chokuretsu-wrapped-topic-misguided-ideas", 5, TopicType.Sub),
        new(813, "chokuretsu-wrapped-topic-give-up", 5, TopicType.Sub),
        new(814, "chokuretsu-wrapped-topic-consideration", 5, TopicType.Sub),
        new(815, "chokuretsu-wrapped-topic-safety-first", 5, TopicType.Sub),
        new(816, "chokuretsu-wrapped-topic-piercing-scream", 5, TopicType.Sub),
        new(817, "chokuretsu-wrapped-topic-withered-silver-grass", 5, TopicType.Sub),
        new(818, "chokuretsu-wrapped-topic-making-it-consistent", 5, TopicType.Sub),
        new(819, "chokuretsu-wrapped-topic-substitute", 5, TopicType.Sub),
        new(820, "chokuretsu-wrapped-topic-a-place-that-doesn't-exist", 5, TopicType.Sub),
    ];
}

public record Route(int Flag, string Name, Character[] Characters, Objective Objective, SideCharacter[] SideCharacters);

public record Topic(int Flag, string Name, int Episode, TopicType Type);

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

public enum TopicType
{
    Main,
    Haruhi,
    Mikuru,
    Nagato,
    Koizumi,
    Sub,
}