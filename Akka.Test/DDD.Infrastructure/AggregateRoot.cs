using System;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Akka.Test.DDD.Infrastructure.Event;
using Akka.Test.DDD.Infrastructure.Protocol;

namespace Akka.Test.DDD.Infrastructure
{
    public abstract class AggregateRoot<TState> : UntypedPersistentActor where TState : AggregateState
    {
        private ILoggingAdapter _logger = Context.GetLogger();
        protected TState State { get; private set; }

        public override string PersistenceId { get; }

        public bool IsInitialized => State != null;

        protected abstract Func<DomainEvent, TState> Factory { get; }

        public AggregateRoot( string persistenceId )
        {
            PersistenceId = persistenceId;
        }

        public void Raise<TEvent>( TEvent @event, Action<TEvent> handler = null ) where TEvent : DomainEvent
        {
            Persist( @event, persistedEvent =>
            {
                _logger.Debug( "Event persisted: {Event}", persistedEvent );
                UpdateState( @event );
                (handler ?? Handle).Invoke( @event );
            } );
        }

        public void Handle<TEvent>( TEvent @event ) where TEvent : DomainEvent
        {
            Publish( @event );
            Sender.Tell( new Acknowledged(), Self );
        }

        protected override void OnRecover( object message )
        {
            if ( message is DomainEvent @event )
            {
                UpdateState( @event );
            }
        }

        private void Publish<TEvent>( TEvent @event ) where TEvent : DomainEvent
        {
            Context.System.EventStream.Publish( @event );
        }

        private void UpdateState( DomainEvent @event )
        {
            State = IsInitialized ? (TState) State.Apply( @event ) : Factory( @event );
        }

        protected override void PreRestart( Exception reason, object message )
        {
            Sender.Tell( new Status.Failure( reason ), Self );
            base.PreRestart( reason, message );
        }
    }
}