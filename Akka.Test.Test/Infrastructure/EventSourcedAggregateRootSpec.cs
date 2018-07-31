using System;
using Akka.Actor;
using Akka.Event;

namespace Akka.Test.Test.Infrastructure
{
    public abstract class EventSourcedAggregateRootSpec : TestKit.Xunit2.TestKit
    {
        private const string ParentName = "parent";

        private static readonly string Config = @"
akka : {
    loggers : [\""Akka.TestKit.TestEventListener, Akka.TestKit\""] 
    //akka.persistence.journal.plugin = ""akka.persistence.journal.inmem""
    suppress-json-serializer-warning : true 
    test : { 
        timefactor : 1.0 
        filter-leeway : 3s 
        single-expect-default : 3s 
        default-timeout : 5s 
        calling-thread-dispatcher : { 
            type : \""Akka.TestKit.CallingThreadDispatcherConfigurator, Akka.TestKit\"" 
            throughput : 2147483647 
        } 
        test-actor : { 
            dispatcher : { 
                type : \""Akka.TestKit.CallingThreadDispatcherConfigurator, Akka.TestKit\"" 
                throughput : 2147483647 
            } 
        } 
    } 
}";

        private IActorRef Parent { get; }
        private IActorRef Subscriber { get; }
        protected abstract string AggregateRootId { get; }

        public EventSourcedAggregateRootSpec() : base( ActorSystem.Create( "test", FullDebugConfig.WithFallback( DefaultConfig ) ) )
        {
            Parent = Sys.ActorOf( Props.Create<ParentActor>(), ParentName );
        }

        protected IActorRef GetActor( Props props, string name = null )
        {
            var timeout = TimeSpan.FromSeconds( value: 5 );
            return Parent.Ask( new GetOrCreateChild( props, name ?? AggregateRootId ), timeout ).Result as IActorRef;
        }

        protected void ExpectEventPersisted<TEvent>( Action when )
        {
            var subscriber = CreateTestProbe();
            Sys.EventStream.Subscribe<TEvent>( subscriber );
            when();
            subscriber.ExpectMsg<TEvent>();
            Sys.EventStream.Unsubscribe<TEvent>( subscriber );
        }

        protected void ExpectEventPersisted<TEvent>( Action when, Func<TEvent, IActorRef, bool> isMessage )
        {
            var subscriber = CreateTestProbe();
            Sys.EventStream.Subscribe<TEvent>( subscriber );
            when();
            subscriber.ExpectMsg( isMessage );
            Sys.EventStream.Unsubscribe<TEvent>( subscriber );
        }

        private sealed class ParentActor : UntypedActor
        {
            protected override void OnReceive( object message )
            {
                switch ( message )
                {
                    case GetOrCreateChild getOrCreateChild:
                        Sender.Tell( GetOrCreateChildActor( getOrCreateChild.Props, getOrCreateChild.Name ) );
                        break;
                }
            }

            private IActorRef GetOrCreateChildActor( Props props, string name ) => Context.ActorOf( props, name );
        }

        private sealed class GetOrCreateChild
        {
            public Props Props { get; }
            public string Name { get; }

            public GetOrCreateChild( Props props, string name )
            {
                Props = props;
                Name = name;
            }
        }
    }
}
