using System;
using Akka.Actor;
using Serilog;

namespace Akka.Test
{
    public static class Program
    {
        #region Non-public methods

        private static void Main( string[] args )
        {
            HoconTest.Run();

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

        #endregion
    }
}
