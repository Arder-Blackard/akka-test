using System;
using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;
using Serilog;

namespace Akka.Test
{
    public static class Program
    {
        #region Non-public methods

        private static void Main( string[] args )
        {
            Log.Logger = new LoggerConfiguration()
                         .MinimumLevel.Verbose()
                         .WriteTo.Console()
                         .CreateLogger();

            Log.Logger.Information( "Go!" );

            var system = ActorSystem.Create( "akka-test", "akka { loglevel=INFO,  loggers=[\"Akka.Logger.Serilog.SerilogLogger, Akka.Logger.Serilog\"]}");

            var manager = system.ActorOf( DeviceManager.Props() );
            var tester = system.ActorOf( Tester.Props( manager ) );
            tester.Tell( "test" );

            Console.ReadLine();
        }

        class Tester : UntypedActor
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

                    case DeviceRegistered response:
                        _logger.Info( "Device registered" );
                        break;
                }
            }

            public static Props Props( IActorRef manager ) => Actor.Props.Create( () => new Tester( manager ) );
        }

        #endregion
    }
}
