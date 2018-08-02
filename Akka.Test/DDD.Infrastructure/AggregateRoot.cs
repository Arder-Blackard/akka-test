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
        private readonly ILoggingAdapter _logger = Context.GetLogger();

        /// <summary>
        ///     Aggregate root persistence ID.
        /// </summary>
        public override string PersistenceId { get; }

        /// <summary>
        ///     Aggregate root state.
        /// </summary>
        protected TState State { get; private set; }

        /// <summary>
        ///     Factory to create new aggregate root instances.
        /// </summary>
        protected abstract Func<DomainEvent, TState> Factory { get; }

        protected AggregateRoot()
        {
            PersistenceId = Context.Parent.Path.Name + "-" + Self.Path.Name;
        }

        /// <summary>
        ///     Raises an <paramref name="event" /> persisting it in an ES-storage.
        /// </summary>
        protected void Raise<TEvent>( TEvent @event, Action<TEvent> handler = null ) where TEvent : DomainEvent
        {
            Persist( @event, persistedEvent =>
            {
                _logger.Debug( "Event persisted: {Event}", persistedEvent );
                UpdateState( @event );
                (handler ?? Handle).Invoke( @event );
            } );
        }

        protected void Handle<TEvent>( TEvent @event ) where TEvent : DomainEvent
        {
            Publish( @event );
            Sender.Tell( new Acknowledged(), Self );
        }

        /// <summary>
        /// </summary>
        /// <param name="message"></param>
        protected override void OnRecover( object message )
        {
            if ( message is DomainEvent @event )
            {
                UpdateState( @event );
            }
        }

        /// <summary>
        ///     Notifies command sender about command failure.
        /// </summary>
        protected override void PreRestart( Exception reason, object message )
        {
            Sender.Tell( new Status.Failure( reason ), Self );
            base.PreRestart( reason, message );
        }

        private void Publish<TEvent>( TEvent @event ) where TEvent : DomainEvent
        {
            Context.System.EventStream.Publish( @event );
        }

        /// <summary>
        ///     Updates an aggregate root state according to the given event.
        /// </summary>
        /// <param name="event"></param>
        private void UpdateState( DomainEvent @event )
        {
            State = State != null ? (TState) State.Apply( @event ) : Factory( @event );
        }
    }
}
