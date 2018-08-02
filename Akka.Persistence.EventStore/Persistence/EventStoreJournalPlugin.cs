using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Journal;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Akka.Persistence.EventStore.Persistence
{
    public class EventStoreJournalSettings
    {
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

        public static EventStoreJournalSettings Default = new EventStoreJournalSettings(
            host: "tcp://tom-server:1113",
            username: "test-o-matic",
            password: "2wsx#EDC",
            prefix: "akka-" );
    }

    public class EventStoreJournalPlugin : AsyncWriteJournal
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            Converters = { new StringEnumConverter() }
        };

        /// <summary>
        ///     Header storing the event object CLR type.
        /// </summary>
        private const string EventClrTypeHeader = "EventClrType";

        private readonly EventStoreJournalSettings _settings = EventStoreJournalSettings.Default;

        private Lazy<IEventStoreConnection> EventStoreConnection { get; }

        public EventStoreJournalPlugin()
        {
            EventStoreConnection = new Lazy<IEventStoreConnection>( () =>
            {
                var connectionSettings = ConnectionSettings.Create()
                                                           .SetDefaultUserCredentials( new UserCredentials( _settings.Username, _settings.Password ) )

                    //  TODO: Enable
                    // .KeepReconnecting()
                    // .KeepRetrying()
                    ;

                var eventStoreConnection = global::EventStore.ClientAPI.EventStoreConnection.Create( connectionSettings, new Uri( _settings.Host ) );
                eventStoreConnection.ConnectAsync().Wait();

                return eventStoreConnection;
            } );
        }

        /// <inheritdoc />
        public override Task ReplayMessagesAsync( IActorContext context,
                                                  string persistenceId,
                                                  long fromSequenceNr,
                                                  long toSequenceNr,
                                                  long max,
                                                  Action<IPersistentRepresentation> recoveryCallback ) => TODO_IMPLEMENT_ME;

        /// <inheritdoc />
        public override Task<long> ReadHighestSequenceNrAsync( string persistenceId, long fromSequenceNr )
        {
            var connection = EventStoreConnection.Value;
            var streamId = _settings.Prefix + persistenceId;


        }

        /// <inheritdoc />
        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync( IEnumerable<AtomicWrite> messages )
        {
            var connection = EventStoreConnection.Value;
            var messageGroups = messages.GroupBy( m => m.PersistenceId );

            var writeTasks = messageGroups.Select( group =>
            {
                var persistenceId = @group.Key;
                var streamId = _settings.Prefix + persistenceId;

                var lowerSequenceId = @group.Min( m => m.LowestSequenceNr );

                var events = @group.SelectMany( message => from persistent in (IImmutableList<IPersistentRepresentation>) message.Payload
                                                           orderby persistent.SequenceNr
                                                           select ToEventData( Guid.NewGuid(), persistent.Payload, new Dictionary<string, object>() ) )
                                   .ToArray();

                return connection.AppendToStreamAsync( streamId, lowerSequenceId, events );
            } ).ToArray();

            return await Task.Factory.ContinueWhenAll( writeTasks, tasks => tasks.Select( t => t.IsFaulted ? TryUnwrapException( t.Exception ) : null ).ToImmutableList() );
        }

        protected static (object data, JObject metadata) Deserialize( RecordedEvent eventStoreEvent )
        {
            var deserializedMetadata = JObject.Parse( Encoding.UTF8.GetString( eventStoreEvent.Metadata ) );
            var eventClrTypeName = deserializedMetadata.Property( EventClrTypeHeader ).Value;
            var deserializedData = JsonConvert.DeserializeObject( Encoding.UTF8.GetString( eventStoreEvent.Data ), Type.GetType( (string) eventClrTypeName ) );

            return (deserializedData, deserializedMetadata);
        }

        /// <summary>
        ///     Converts an <paramref name="event" /> object to an EventStore representation.
        /// </summary>
        /// <param name="eventId">A unique event identifier.</param>
        /// <param name="event">An event object.</param>
        /// <param name="headers">An event metadata headers.</param>
        /// <returns>An EventStore event representation ready to be written to a stream.</returns>
        protected EventData ToEventData( Guid eventId, object @event, IDictionary<string, object> headers )
        {
            var json = JsonConvert.SerializeObject( @event, SerializerSettings );
            var data = Encoding.UTF8.GetBytes( json );
            var eventHeaders = new Dictionary<string, object>( headers )
            {
                [EventClrTypeHeader] = @event.GetType().AssemblyQualifiedName
            };

            var metadata = Encoding.UTF8.GetBytes( JsonConvert.SerializeObject( eventHeaders, SerializerSettings ) );
            var typeName = GetEventTypeName( @event );

            return new EventData( eventId, typeName, isJson: true, data: data, metadata: metadata );
        }

        private static string GetEventTypeName( object @event )
        {
            var typeName = @event.GetType().Name;
            var firstChar = typeName.First();
            return char.IsUpper( firstChar )
                       ? char.ToLower( firstChar ) + typeName.Substring( startIndex: 1 )
                       : typeName;
        }

        /// <inheritdoc />
        protected override Task DeleteMessagesToAsync( string persistenceId, long toSequenceNr ) => throw new NotImplementedException();
    }
}
