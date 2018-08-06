using System;
using Akka.Actor;
using Akka.Configuration;
using Akka.Streams.Dsl;
using Akka.Streams.Implementation;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using Reactive.Streams;

namespace Akka.Persistence.Query.EventStore
{
    public class EventStoreReadJournalSettings
    {
        public static EventStoreReadJournalSettings Default = new EventStoreReadJournalSettings(
            "tcp://tom-server:1113",
            "test-o-matic",
            "2wsx#EDC",
            "akka-");

        public string Host { get; }
        public string Username { get; }
        public string Password { get; }
        public string Prefix { get; }

        /// <inheritdoc />
        public EventStoreReadJournalSettings(string host, string username, string password, string prefix)
        {
            Host = host;
            Username = username;
            Password = password;
            Prefix = prefix;
        }

        public EventStoreReadJournalSettings(Config config)
        {
            Host = config.GetString("host");
            Prefix = config.GetString("prefix");
            Username = config.GetString("username");
            Password = config.GetString("password");
        }
    }

    public class EventStoreReadJournal : IReadJournal,
                                         // IEventsByPersistenceIdQuery,
                                         // ICurrentEventsByPersistenceIdQuery, 
                                         // IEventsByTagQuery,
                                         ICurrentEventsByTagQuery
    {
        private Lazy<IEventStoreConnection> _eventStoreConnection;
        private readonly EventStoreReadJournalSettings _settings = EventStoreReadJournalSettings.Default;

        public EventStoreReadJournal()
        {
            _eventStoreConnection = new Lazy<IEventStoreConnection>(() =>
            {
                var connectionSettings = ConnectionSettings.Create()
                                                           .SetDefaultUserCredentials(new UserCredentials(_settings.Username, _settings.Password))

                    //  TODO: Enable
                    // .KeepReconnecting()
                    // .KeepRetrying()
                    ;

                var eventStoreConnection = EventStoreConnection.Create(connectionSettings, new Uri(_settings.Host));
                eventStoreConnection.ConnectAsync().Wait();

                return eventStoreConnection;
            });

            // var task = _eventStoreConnection.Value.CreatePersistentSubscriptionAsync(
            //     "$category-akka2",
            //     "akka-read-journal",
            //     PersistentSubscriptionSettings.Create()
            //                                   .StartFromBeginning()
            //                                   .Build(),
            //     null );
            //
            // task.Wait();
        }

        public Source<EventEnvelope, NotUsed> CurrentEventsByTag(string tag, Offset offset)
        {
            _eventStoreConnection.Value.ConnectToPersistentSubscription(
                $"$category-{tag}",
                "akka-read-journal",
                ( subscription, @event ) =>
                {

                },
                ( subscription, reason, ex ) =>
                {

                }
            );

            return Source.FromPublisher(new Publisher());
        }
    }

    class Publisher : IPublisher<EventEnvelope>
    {
        private ISubscriber<EventEnvelope> _subscriber;

        public void Subscribe( ISubscriber<EventEnvelope> subscriber )
        {
            _subscriber = subscriber;

        }


    }

    public class EventsByTagPublisher : ActorPublisher<EventEnvelope>
    {
        public EventsByTagPublisher( IActorRef impl ) : base( impl )
        {
        }
    }

}
