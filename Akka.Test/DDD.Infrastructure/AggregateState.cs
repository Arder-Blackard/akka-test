using Akka.Test.DDD.Infrastructure.Event;

namespace Akka.Test.DDD.Infrastructure
{
    public abstract class AggregateState
    {
        public abstract AggregateState Apply( DomainEvent @event );
    }
}