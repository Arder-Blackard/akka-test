using System;
using System.Text;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.EventStore.Journal;
using Akka.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Akka.Persistence.EventStore
{
    public class EventStorePersistence : IExtension
    {
        public EventStoreJournalSettings JournalSettings { get; }
        public JsonSerializerSettings SerializerSettings { get; }

        public EventStorePersistence( ExtendedActorSystem system )
        {
            if ( system == null )
            {
                throw new ArgumentNullException();
            }

            system.Settings.InjectTopLevelFallback( DefaultConfiguration() );

            var journalConfig = system.Settings.Config.GetConfig( "akka.persistence.journal.eventstore" );
            JournalSettings = new EventStoreJournalSettings( journalConfig );

            SerializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                Converters =
                {
                    new StringEnumConverter(),
                    // new ActorRefConverter( system.Provider ),
                    new AkkaConverter( system )
                }
            };
        }

        public static Config DefaultConfiguration() => ConfigurationFactory.FromResource<EventStorePersistence>( "Akka.Persistence.EventStore.reference.conf" );

        public static EventStorePersistence Get( ActorSystem system ) => system.WithExtension<EventStorePersistence, EventStorePersistenceProvider>();

        private class ActorRefConverter : JsonConverter
        {
            private readonly IActorRefProvider _actorRefProvider;

            public ActorRefConverter( IActorRefProvider actorRefProvider )
            {
                _actorRefProvider = actorRefProvider;
            }

            /// <inheritdoc />
            public override void WriteJson( JsonWriter writer, object value, JsonSerializer serializer )
            {
                var serializedActorPath = Akka.Serialization.Serialization.SerializedActorPath( (IActorRef) value );
                writer.WriteValue( serializedActorPath );
            }

            /// <inheritdoc />
            public override object ReadJson( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer )
            {
                var value = reader.Value.ToString();
                return _actorRefProvider.ResolveActorRef( value );
            }

            /// <inheritdoc />
            public override bool CanConvert( Type objectType ) => typeof(IActorRef).IsAssignableFrom( objectType );
        }

        private class AkkaConverter : JsonConverter
        {
            private readonly ExtendedActorSystem _system;

            public AkkaConverter( ExtendedActorSystem system )
            {
                _system = system;
            }

            /// <inheritdoc />
            public override void WriteJson( JsonWriter writer, object value, JsonSerializer serializer )
            {
                var systemSerializer = _system.Serialization.FindSerializerFor( value );
                var serialized = Encoding.UTF8.GetString( systemSerializer.ToBinary( value ) );
                writer.WriteValue( serialized );
            }

            /// <inheritdoc />
            public override object ReadJson( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer )
            {
                var systemSerializer = _system.Serialization.FindSerializerForType( objectType );
                var value = reader.Value.ToString();
                return systemSerializer.FromBinary( Encoding.UTF8.GetBytes( value ), objectType );
            }

            /// <inheritdoc />
            public override bool CanConvert( Type objectType )
            {
                var serializer = _system.Serialization.FindSerializerForType( objectType );
                return !(serializer is NullSerializer);
            }
        }
    }
}
