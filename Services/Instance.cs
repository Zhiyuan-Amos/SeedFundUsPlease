namespace HealthHackSgSeedFundUsPlease.Services
{
    public class Instance
    {
        public long timestamp {  get; set; }

        public Instance()
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
