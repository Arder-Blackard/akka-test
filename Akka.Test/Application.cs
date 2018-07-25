using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;

namespace Akka.Test
{

    public sealed class Application : UntypedActor
    {
        #region Fields

        private readonly ILoggingAdapter _logger = Context.GetLogger<SerilogLoggingAdapter>();
        public static Props Props() => Actor.Props.Create<Application>();
        private IActorRef _reader;

        #endregion


        #region Events and invocation

        /// <inheritdoc />
        protected override void OnReceive( object message )
        {
            Context.GetLogger().Info( "Application received message {Message}", message );
            _logger.Info( "Application received message {Message}", message );
            switch ( message )
            {
                case "print":
                    _reader.Tell( "print" );
                    break;

                case "shutdown":
                    Context.Stop( Self );
                    break;
            }
        }

        #endregion


        #region Non-public methods

        /// <inheritdoc />
        protected override void PreStart()
        {
            _reader = Context.ActorOf( Reader.Props(), "reader" );
            base.PreStart();
        }

        #endregion
    }
}