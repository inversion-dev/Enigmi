namespace Enigmi;

public class Constants
{
	public static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public const string PuzzleCollectionSnifferGrainKey = "PuzzleCollectionSnifferGrain";
    public const string ActiveUtxoReservationsListGrainKey = "ActiveUtxoReservationsListGrain";
    
    public const string StreamNamespace = "enigmi";
    public const string StreamProvider = "event-hub";

    public class Claim
    {
        public const string StakeAddress = "StakeAddress";
    }
    
    public const string PolicyCollectionGrainSubscription  = "PolicyCollectionGrainSubscription";
    public const string PuzzleCollectionListGrainSubscription  = "PuzzleCollectionListGrainSubscription";
    
    public const int SingletonGrain = 0;

    public const int LovelacePerAda = 1000000;
    public static readonly string LovelaceTokenAssetId = "000000000000000000000000000000000000000000000000000000006c6f76656c616365";

    public const string GrainStorageProviderName = "orleansprovider";
    public readonly struct Unit { };
    
    public const string JwtToken = "JwtToken";

    public static string NicknameStorageKey { get; set; } = "Nickname";
}