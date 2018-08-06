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

namespace Akka.Persistence.EventStore.Journal
{
    public class EventStoreJournal : AsyncWriteJournal
    {
        private const long ReadPageSize = 50;
        private readonly JsonSerializerSettings _serializerSettings;
        private readonly EventStoreJournalSettings _settings;
        private Lazy<IEventStoreConnection> _eventStoreConnection;

        public EventStoreJournal()
        {
            var eventStorePersistence = EventStorePersistence.Get( Context.System );
            _settings = eventStorePersistence.JournalSettings;
            _serializerSettings = eventStorePersistence.SerializerSettings;
        }

        /// <inheritdoc />
        public override async Task ReplayMessagesAsync( IActorContext context,
                                                        string persistenceId,
                                                        long fromSequenceNr,
                                                        long toSequenceNr,
                                                        long max,
                                                        Action<IPersistentRepresentation> recoveryCallback )
        {
            var streamId = StreamIdFromPersistenceId( persistenceId );

            var sliceStart = fromSequenceNr - 1;
            StreamEventsSlice currentSlice;

            do
            {
                var sliceCount = sliceStart + ReadPageSize < toSequenceNr
                                     ? ReadPageSize
                                     : toSequenceNr - sliceStart;

                currentSlice = await _eventStoreConnection.Value.ReadStreamEventsForwardAsync( streamId, sliceStart, (int) sliceCount, resolveLinkTos: false );

                switch ( currentSlice.Status )
                {
                    case SliceReadStatus.StreamNotFound:
                        return;
                    case SliceReadStatus.StreamDeleted:
                        throw new InvalidOperationException( $"Stream {streamId} was deleted" );
                }

                foreach ( var @event in currentSlice.Events )
                {
                    var persistent = DeserializeEvent( @event.OriginalEvent, context.Sender );
                    recoveryCallback( persistent );
                }

                sliceStart = currentSlice.NextEventNumber;
            } while ( currentSlice.NextEventNumber < toSequenceNr && !currentSlice.IsEndOfStream );
        }

        /// <inheritdoc />
        public override async Task<long> ReadHighestSequenceNrAsync( string persistenceId, long fromSequenceNr )
        {
            var streamId = StreamIdFromPersistenceId( persistenceId );

            var slice = await _eventStoreConnection.Value.ReadEventAsync( streamId, StreamPosition.End, resolveLinkTos: false );
            if ( slice.Status == EventReadStatus.NoStream || slice.Status == EventReadStatus.NotFound )
            {
                return 0;
            }

            return slice.Event.Value.OriginalEventNumber + 1;
        }

        /// <inheritdoc />
        protected override void PreStart()
        {
            base.PreStart();
            _eventStoreConnection = new Lazy<IEventStoreConnection>( () =>
            {
                var connectionSettings = ConnectionSettings.Create()
                                                           .SetDefaultUserCredentials( new UserCredentials( _settings.Username, _settings.Password ) )

                    //  TODO: Enable
                    // .KeepReconnecting()
                    // .KeepRetrying()
                    ;

                var eventStoreConnection = EventStoreConnection.Create( connectionSettings, new Uri( _settings.Host ) );
                eventStoreConnection.ConnectAsync().Wait();

                return eventStoreConnection;
            } );
        }

        /// <inheritdoc />
        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync( IEnumerable<AtomicWrite> messages )
        {
            var messageGroups = messages.GroupBy( m => m.PersistenceId );

            var writeTasks = messageGroups.Select( group =>
            {
                var persistenceId = group.Key;
                var streamId = StreamIdFromPersistenceId( persistenceId );

                var lowerSequenceId = group.Min( m => m.LowestSequenceNr );

                var events = group.SelectMany( message => from persistent in (IImmutableList<IPersistentRepresentation>) message.Payload
                                                          orderby persistent.SequenceNr
                                                          select ToEventData( persistent ) )
                                  .ToArray();

                return _eventStoreConnection.Value.AppendToStreamAsync( streamId, lowerSequenceId - 2, events );
            } ).ToArray();

            return await Task.Factory.ContinueWhenAll( writeTasks, tasks => tasks.Select( t => t.IsFaulted ? TryUnwrapException( t.Exception ) : null ).ToImmutableList() );
        }

        /// <inheritdoc />
        protected override Task DeleteMessagesToAsync( string persistenceId, long toSequenceNr ) => throw new NotImplementedException();

        private Persistent DeserializeEvent( RecordedEvent eventStoreEvent, IActorRef sender )
        {
            var metadata = JsonConvert.DeserializeObject<EventMetadata>( Encoding.UTF8.GetString( eventStoreEvent.Metadata ) );
            var payloadTypeName = metadata.PayloadType;
            var type = Type.GetType( payloadTypeName );
            var payload = JsonConvert.DeserializeObject( Encoding.UTF8.GetString( eventStoreEvent.Data ), type, _serializerSettings );

            return new Persistent( payload, eventStoreEvent.EventNumber + 1, metadata.PersistenceId, metadata.Manifest, metadata.IsDeleted, sender, metadata.WriterGuid );
        }

        /// <summary>
        ///     Converts a <paramref name="persistent" /> object to an EventStore representation.
        /// </summary>
        /// <returns>An EventStore event representation ready to be written to a stream.</returns>
        private EventData ToEventData( IPersistentRepresentation persistent )
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
                PayloadType = FormatPayloadTypeName( persistent.Payload.GetType() )
            };

            var metadataBytes = Encoding.UTF8.GetBytes( JsonConvert.SerializeObject( metadata, _serializerSettings ) );
            var typeName = GetEventTypeName( persistent.Payload );

            return new EventData( Guid.NewGuid(), typeName, isJson: true, data: jsonBytes, metadata: metadataBytes );
        }

        private static string FormatPayloadTypeName( Type type ) => $"{type.FullName}, {type.Assembly.GetName().Name}";

        private string StreamIdFromPersistenceId( string persistenceId ) => _settings.Prefix + persistenceId.Replace( oldChar: '/', newChar: '_' );

        private static string GetEventTypeName( object @event )
        {
            var typeName = @event.GetType().Name;
            var firstChar = typeName.First();
            return char.IsUpper( firstChar )
                       ? char.ToLower( firstChar ) + typeName.Substring( startIndex: 1 )
                       : typeName;
        }

        /// <summary>
        ///     Describes an event metadata.
        /// </summary>
        private sealed class EventMetadata
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
