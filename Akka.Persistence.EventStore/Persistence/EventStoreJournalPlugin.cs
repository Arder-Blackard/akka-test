using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
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
    }

    public class EventStoreJournalPlugin : AsyncWriteJournal
    {
        /// <summary>
        ///     Header storing the event object CLR type.
        /// </summary>
        private const string EventClrTypeHeader = "EventClrType";

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            Converters = { new StringEnumConverter() }
        };

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
        public override async Task ReplayMessagesAsync( IActorContext context,
                                                  string persistenceId,
                                                  long fromSequenceNr,
                                                  long toSequenceNr,
                                                  long max,
                                                  Action<IPersistentRepresentation> recoveryCallback )
        {
            var connection = EventStoreConnection.Value;
            var streamId = StreamIdFromPersistenceId(persistenceId);

            var sliceStart = fromSequenceNr;
            StreamEventsSlice currentSlice;

            long ReadPageSize = 50;
            do
            {
                var sliceCount = sliceStart + ReadPageSize <= toSequenceNr ? ReadPageSize : (int)(toSequenceNr - sliceStart + 1);
                currentSlice = await connection.ReadStreamEventsForwardAsync(streamId, sliceStart, sliceCount, resolveLinkTos: false);

                if (currentSlice.Status == SliceReadStatus.StreamNotFound)
                {
                    return;
                }

                if (currentSlice.Status == SliceReadStatus.StreamDeleted)
                {
                    throw new InvalidOperationException( $"Stream {streamId} was deleted" );
                }

                sliceStart = currentSlice.NextEventNumber;

                foreach ( var @event in currentSlice.Events )
                {
                    var (persistent, metadata) = Deserialize( @event.OriginalEvent );

                }
                //  Apply loaded events to the aggregate
                if (aggregate == null)
                {
                    var firstEvent = DeserializeEvent<IAggregateEvent<TId>>(currentSlice.Events[0].OriginalEvent);
                    aggregate = (TAggregate)ConstructAggregate((string)firstEvent.metadata.Property(AggregateClrTypeHeader).Value);
                }

                var aggregateEvents = currentSlice.Events.Select(@event => DeserializeEvent<IAggregateEvent<TId>>(@event.OriginalEvent).@event);
                aggregate.LoadFromHistory(aggregateEvents);

            } while (version >= currentSlice.NextEventNumber && !currentSlice.IsEndOfStream);

        }

        /// <inheritdoc />
        public override async Task<long> ReadHighestSequenceNrAsync( string persistenceId, long fromSequenceNr )
        {
            var connection = EventStoreConnection.Value;
            var streamId = StreamIdFromPersistenceId( persistenceId );

            var slice = await connection.ReadEventAsync( streamId, StreamPosition.End, resolveLinkTos: false );
            if ( slice.Status == EventReadStatus.NoStream || slice.Status == EventReadStatus.NotFound )
                return 0;

            return slice.EventNumber + 1;
        }

        /// <inheritdoc />
        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync( IEnumerable<AtomicWrite> messages )
        {
            var connection = EventStoreConnection.Value;
            var messageGroups = messages.GroupBy( m => m.PersistenceId );

            var writeTasks = messageGroups.Select( group =>
            {
                var persistenceId = group.Key;
                var streamId = StreamIdFromPersistenceId( persistenceId );

                var lowerSequenceId = group.Min( m => m.LowestSequenceNr );

                var events = group.SelectMany( message => from persistent in (IImmutableList<IPersistentRepresentation>) message.Payload
                                                          orderby persistent.SequenceNr
                                                          select ToEventData(persistent )
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
        protected EventData ToEventData( IPersistentRepresentation persistent )
        {
            var json = JsonConvert.SerializeObject( persistent.Payload, SerializerSettings );
            var data = Encoding.UTF8.GetBytes( json );
            var eventHeaders = new Dictionary<string, object>( headers )
            {
                [EventClrTypeHeader] = @event.GetType().AssemblyQualifiedName
            };

            var metadata = Encoding.UTF8.GetBytes( JsonConvert.SerializeObject( eventHeaders, SerializerSettings ) );
            var typeName = GetEventTypeName( @event );

            return new EventData( eventId, typeName, isJson: true, data: data, metadata: metadata );
        }

        /// <inheritdoc />
        protected override Task DeleteMessagesToAsync( string persistenceId, long toSequenceNr ) => throw new NotImplementedException();

        private string StreamIdFromPersistenceId( string persistenceId ) => _settings.Prefix + persistenceId;

        private static string GetEventTypeName( object @event )
        {
            var typeName = @event.GetType().Name;
            var firstChar = typeName.First();
            return char.IsUpper( firstChar )
                       ? char.ToLower( firstChar ) + typeName.Substring( startIndex: 1 )
                       : typeName;
        }
    }
}
