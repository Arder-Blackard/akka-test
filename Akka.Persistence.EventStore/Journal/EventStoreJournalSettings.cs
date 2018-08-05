using Akka.Configuration;

namespace Akka.Persistence.EventStore.Journal
{
    public class EventStoreJournalSettings
    {
        public static EventStoreJournalSettings Default = new EventStoreJournalSettings(
            "tcp://tom-server:1113",
            "test-o-matic",
            "2wsx#EDC",
            "akka-" );

        public string Host { get; }
        public string Username { get; }
        public string Password { get; }
        public string Prefix { get; }

        /// <inheritdoc />
        public EventStoreJournalSettings( string host, string username, string password, string prefix )
        {
            Host = host;
            Username = username;
            Password = password;
            Prefix = prefix;
        }

        public EventStoreJournalSettings( Config config )
        {
            Host = config.GetString("host");
            Prefix = config.GetString("prefix");
            Username = config.GetString("username");
            Password = config.GetString("password");
        }
    }
}