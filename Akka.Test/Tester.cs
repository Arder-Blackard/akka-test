using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;

namespace Akka.Test
{
    public class Tester : UntypedActor
    {
        private readonly ILoggingAdapter _logger = Context.GetLogger<SerilogLoggingAdapter>();

        private readonly IActorRef _manager;

        public Tester( IActorRef manager )
        {
            _manager = manager;
        }

        /// <inheritdoc />
        protected override void OnReceive( object message )
        {
            switch ( message )
            {
                case "test":
                    _manager.Tell( new RequestTrackDevice("Group1", "Device1") );
                    break;

                case DeviceRegistered _:
                    _logger.Info( "Device registered" );
                    break;
            }
        }

        public static Props Props( IActorRef manager ) => Actor.Props.Create( () => new Tester( manager ) );
    }
}
