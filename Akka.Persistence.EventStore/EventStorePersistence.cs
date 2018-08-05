using System;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.EventStore.Journal;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Akka.Persistence.EventStore
{
    public class EventStorePersistence : IExtension
    {
        public static Config DefaultConfiguration()
        {
            return ConfigurationFactory.FromResource<EventStorePersistence>( "Akka.Persistence.EventStore.reference.conf" );
        }

        public static EventStorePersistence Get( ActorSystem system )
        {
            return system.WithExtension<EventStorePersistence, EventStorePersistenceProvider>();
        }

        public EventStoreJournalSettings JournalSettings { get; }
        public JsonSerializerSettings SerializerSettings { get; }

        public EventStorePersistence( ExtendedActorSystem system )
        {
            if (system == null)
                throw  new ArgumentNullException();

            system.Settings.InjectTopLevelFallback( DefaultConfiguration() );

            var journalConfig = system.Settings.Config.GetConfig( "akka.persistence.journal.eventstore" );
            JournalSettings = new EventStoreJournalSettings( journalConfig );

            SerializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                Converters = { new StringEnumConverter(), new ActorRefConverter(system.Provider) }
            };

        }

        private class ActorRefConverter : JsonConverter
        {
            private readonly IActorRefProvider _actorRefProvider;

            public ActorRefConverter( IActorRefProvider actorRefProvider )
            {
                _actorRefProvider = actorRefProvider;
            }

            /// <inheritdoc />
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var serializedActorPath = Akka.Serialization.Serialization.SerializedActorPath((IActorRef)value);
                writer.WriteValue(serializedActorPath);
            }

            /// <inheritdoc />
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var path = reader.ReadAsString();
                return _actorRefProvider.ResolveActorRef( path );
            }

            /// <inheritdoc />
            public override bool CanConvert(Type objectType) => typeof(IActorRef).IsAssignableFrom(objectType);
        }
    }
}