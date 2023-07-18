namespace Enigmi.Common;

public class Settings
{
    public string EnvironmentPrefix { get; set; } = "";
    public BlockfrostSettings BlockfrostConfig { get; set; } = null!;

	public PolicyVaultSettings PolicyVaultConfig { get; set; } = null!;

	public SendInBlueSettings SendInBlueConfig { get; set; } = null!;

	public CardanoBlockchainSettings CardanoBlockchainConfig { get; set; } = null!;
    public BlobstorageConfigSettings BlobstorageConfig { get; set; } = null!;
    public EventHubConfigSettings EventHubConfig { get; set; } = null!;
    public ClusterConfigurationSettings ClusterConfiguration { get; set; } = null!;
    public TableStorageConfigSettings TableStorageConfig { get; set; } = null!;

    public JwtTokenConfigurationSettings JwtTokenConfiguration { get; set; } = null!;
    
    public string SignalRConnection { get; set; } = null!;

    public class CardanoBlockchainSettings
	{
		public int CardanoNetwork { get; set; }
	}

    public class BlobstorageConfigSettings
    {
        public string BlobStorageHost { get; set; } = "";
        public string ConnectionString { get; set; } = "";
    }

    public class EventHubConfigSettings
    {
        public string EventHubPath { get; set; } = "";
        public string EventHubConsumerGroup { get; set; } = "";
        public string ConnectionString { get; set; } = "";
    }
    
    public class JwtTokenConfigurationSettings
    {
	    public string SecretKey { get; set; } = "";
	    public string[] Origins { get; set; } = Array.Empty<string>();
    }

    public class ClusterConfigurationSettings
    {
        public string ClusterId { get; set; } = "";
        public string ServiceId { get; set; } = "";
    }

    public class TableStorageConfigSettings
    {
        public string ConnectionString { get; set; } = "";
    }

    public class SendInBlueSettings
	{
		public string ApiUrl { get; set; } = "";

		public string ApiKey { get; set; } = "";

		public string SenderEmail { get; set; } = "";

		public string FriendlyName { get; set; } = "";

		public string SubjectPrefix { get; set; } = "";
	}

	public class PolicyVaultSettings
	{
		public string PolicyVaultUrl { get; set; } = "";
	}

	public class BlockfrostSettings
	{
		public string ApiUrl { get; set; } = "";

		public string ApiKey { get; set; } = "";

		public string Network { get; set; } = "";

		public Dictionary<string, BinarySearchMethodSettings> BinarySearch { get; set; } = new();

		public class BinarySearchMethodSettings
		{
			public int MaxCallsForApiCountMethods { get; set; } = 10;

			public int PageSizeForApiCountMethods { get; set; } = 100;

			public int StartingPageForApiCountMethods { get; set; } = 20;
		}

		public int BurstRequestLimit { get; set; } = 500;

		public int RequestsPerSecond { get; set; } = 10;

		public int MaxRetryAttempts { get; set; } = 0;
	}
}