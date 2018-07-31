using System;
using Akka.Actor;
using Serilog;

namespace Akka.Test
{
    public static class Program
    {
        private static void Main( string[] args )
        {
            Log.Logger = new LoggerConfiguration()
                         .MinimumLevel.Verbose()
                         .WriteTo.Console()
                         .CreateLogger();

            Log.Logger.Information( "Go!" );

            var system = ActorSystem.Create( "akka-test",
                                             @"
akka { 
    loglevel=DEBUG,  
    stdout-loglevel = DEBUG
    loggers=[""Akka.Logger.Serilog.SerilogLogger, Akka.Logger.Serilog""]}
    actor {
        debug {
            receive = true
            autoreceive = true
            lifecycle = true
            event-stream = true
            unhandled = true
            fsm = true
            router-misconfiguration = true
        }
    }
    log-dead-letters-during-shutdown = true
    log-dead-letters = true

     actor.provider = cluster
    remote {
        dot-netty.tcp {
            port = 8081
            hostname = localhost
        }
    }
    cluster {
        seed-nodes = [""akka.tcp://ClusterSystem@localhost:8081""]
    }
}" );

            

            Console.ReadLine();
        }
    }
}
