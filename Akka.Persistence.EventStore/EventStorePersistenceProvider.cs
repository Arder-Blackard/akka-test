using Akka.Actor;
using Akka.Persistence.EventStore;

namespace Akka.Persistence.EventStore
{
    public class EventStorePersistenceProvider : ExtensionIdProvider<EventStorePersistence>
    {
        /// <inheritdoc />
        public override EventStorePersistence CreateExtension( ExtendedActorSystem system ) => new EventStorePersistence( system );
    }
}