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

namespace Akka.Persistence.EventStore.Journal
{
    public class EventStoreJournal : AsyncWriteJournal
    {



        /// <summary>
        ///     Header storing the event object CLR type.
        /// </summary>
        private const string EventClrTypeHeader = "EventClrType";

        private readonly EventStoreJournalSettings _settings;
        private readonly JsonSerializerSettings _serializerSettings;

        private Lazy<IEventStoreConnection> EventStoreConnection { get; set; }

        public EventStoreJournal()
        {
            var eventStorePersistence = EventStorePersistence.Get( Context.System );
            _settings = eventStorePersistence.JournalSettings;
            _serializerSettings = eventStorePersistence.SerializerSettings;
        }

        /// <inheritdoc />
        protected override void PreStart()
        {
            base.PreStart();
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

            var sliceStart = fromSequenceNr - 1;
            StreamEventsSlice currentSlice;

            long ReadPageSize = 50;
            do
            {
                var sliceCount = sliceStart + ReadPageSize < toSequenceNr ? ReadPageSize : (toSequenceNr - sliceStart);
                currentSlice = await connection.ReadStreamEventsForwardAsync(streamId, sliceStart, (int) sliceCount, resolveLinkTos: false);

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
                    var persistent = Deserialize( @event.OriginalEvent, context.Sender );
                    recoveryCallback(persistent);
                }

            } while (currentSlice.NextEventNumber < toSequenceNr && !currentSlice.IsEndOfStream);

        }

        /// <inheritdoc />
        public override async Task<long> ReadHighestSequenceNrAsync( string persistenceId, long fromSequenceNr )
        {
            var connection = EventStoreConnection.Value;
            var streamId = StreamIdFromPersistenceId( persistenceId );

            var slice = await connection.ReadEventAsync( streamId, StreamPosition.End, resolveLinkTos: false );
            if ( slice.Status == EventReadStatus.NoStream || slice.Status == EventReadStatus.NotFound )
                return 0;

            return slice.Event.Value.OriginalEventNumber + 1;
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
                                                          select ToEventData(persistent ))
                                  .ToArray();

                return connection.AppendToStreamAsync( streamId, lowerSequenceId - 2, events );
            } ).ToArray();

            return await Task.Factory.ContinueWhenAll( writeTasks, tasks => tasks.Select( t => t.IsFaulted ? TryUnwrapException( t.Exception ) : null ).ToImmutableList() );
        }

        private static Persistent Deserialize( RecordedEvent eventStoreEvent, IActorRef sender )
        {
            var metadata = JsonConvert.DeserializeObject<EventMetadata>( Encoding.UTF8.GetString( eventStoreEvent.Metadata ) );
            var payloadTypeName = metadata.PayloadType;
            var type = Type.GetType( payloadTypeName );
            var payload = JsonConvert.DeserializeObject( Encoding.UTF8.GetString( eventStoreEvent.Data ), type );


            return new Persistent( payload, eventStoreEvent.EventNumber + 1, metadata.PersistenceId, metadata.Manifest, metadata.IsDeleted, sender, metadata.WriterGuid );
        }

        /// <summary>
        ///     Converts a <paramref name="persistent" /> object to an EventStore representation.
        /// </summary>
        /// <returns>An EventStore event representation ready to be written to a stream.</returns>
        protected EventData ToEventData( IPersistentRepresentation persistent )
        {
            var json = JsonConvert.SerializeObject( persistent.Payload, _serializerSettings );
            var jsonBytes = Encoding.UTF8.GetBytes( json );

            var metadata = new EventMetadata
            {
                Manifest = persistent.Manifest,
                IsDeleted = persistent.IsDeleted,
                PersistenceId = persistent.PersistenceId,
                SequenceNr = persistent.SequenceNr,
                WriterGuid = persistent.WriterGuid,
                PayloadType = persistent.Payload.GetType().FullName
            };

            var metadataBytes = Encoding.UTF8.GetBytes( JsonConvert.SerializeObject( metadata, _serializerSettings ) );
            var typeName = GetEventTypeName( persistent.Payload );

            return new EventData( Guid.NewGuid(), typeName, isJson: true, data: jsonBytes, metadata: metadataBytes );
        }

        /// <inheritdoc />
        protected override Task DeleteMessagesToAsync( string persistenceId, long toSequenceNr ) => throw new NotImplementedException();

        private string StreamIdFromPersistenceId( string persistenceId ) => _settings.Prefix + persistenceId.Replace( '/', '_' );

        private static string GetEventTypeName( object @event )
        {
            var typeName = @event.GetType().Name;
            var firstChar = typeName.First();
            return char.IsUpper( firstChar )
                       ? char.ToLower( firstChar ) + typeName.Substring( startIndex: 1 )
                       : typeName;
        }

        private class EventMetadata
        {
            public string Manifest { get; set; }
            public bool IsDeleted { get; set; }
            public string PersistenceId { get; set; }
            public long SequenceNr { get; set; }
            public string WriterGuid { get; set; }
            public string PayloadType { get; set; }
        }
    }
}
