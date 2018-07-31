using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Test.Domain.Tasks;
using Serilog;

namespace Akka.Test
{
    public static class Program
    {
        private const string Config = @"
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
}";

        private static async Task Main( string[] args )
        {
            Log.Logger = new LoggerConfiguration()
                         .MinimumLevel.Verbose()
                         .WriteTo.Console()
                         .CreateLogger();

            Log.Logger.Information( "Go!" );

            var system = ActorSystem.Create( "akka-test", Config);

            var region = await ClusterSharding.Get( system )
                                              .StartAsync( 
                                                  "job-manager",
                                                  Job.Props(), 
                                                  ClusterShardingSettings.Create( system ), 
                                                  new MessageExtractor()
                             );
            

            Console.ReadLine();
        }
    }
}
