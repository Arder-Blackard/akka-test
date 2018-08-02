﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.Persistence.MongoDb;
using Akka.Test.Domain.Tasks;
using MongoDB.Driver;
using Serilog;

namespace Akka.Test
{
    public static class Program
    {
        private const string Config = @"
mongodb.connection-string = ""mongodb://by1-woiisqa-02:27017/akka-test""        
akka { 
    loglevel=DEBUG,  
    stdout-loglevel = DEBUG
    loggers=[""Akka.Logger.Serilog.SerilogLogger, Akka.Logger.Serilog""]

    actor.debug {
        receive = true
        autoreceive = true
        lifecycle = true
        event-stream = true
        unhandled = true
        fsm = true
        router-misconfiguration = true
    }
    log-dead-letters-during-shutdown = true
    log-dead-letters = true

    persistence {


        max-concurrent-recoveries = 50
        
        ###  Journal plugins.  ##
        journal.plugin = ""akka.persistence.journal.inmem""

        journal.plugin = ""akka.persistence.journal.mongodb""
        journal.mongodb.connection-string = ${mongodb.connection-string}
        journal.mongodb.collection = ""EventJournal""

        journal.auto-start-journals = []

        ###  Snapshot store plugins.  ###
        
        snapshot-store.plugin = ""akka.persistence.snapshot-store.local""
        snapshot-store.plugin = ""akka.persistence.snapshot-store.mongodb""
        snapshot-store.mongodb.connection-string = ${mongodb.connection-string}
        snapshot-store.mongodb.collection = ""SnapshotStore""
        snapshot-store.auto-start-snapshot-stores = []        
    }

    remote {
        dot-netty.tcp {
            port = 8081
            hostname = localhost
        }
    }

    #actor.provider = cluster
    actor.provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
    cluster {
        seed-nodes = [""akka.tcp://akka-test@localhost:8081""]
    }

    actor : {
      serializers : {
        akka-sharding : ""Akka.Cluster.Sharding.Serialization.ClusterShardingMessageSerializer, Akka.Cluster.Sharding""
      }
      serialization-bindings : {
        ""Akka.Cluster.Sharding.IClusterShardingSerializable, Akka.Cluster.Sharding"" : akka-sharding
      }
      serialization-identifiers : {
        ""Akka.Cluster.Sharding.Serialization.ClusterShardingMessageSerializer, Akka.Cluster.Sharding"" : 13
      }
    }
}";

        private static async Task Main( string[] args )
        {
            Log.Logger = new LoggerConfiguration()
                         .MinimumLevel.Verbose()
                         .WriteTo.Console()
                         .CreateLogger();

            Log.Information( "Go!" );



            var clusterConfig = ConfigurationFactory.FromResource("Akka.Test.akka-cluster.conf", typeof(Program).Assembly);
            var persistenceConfig = ConfigurationFactory.FromResource("Akka.Test.akka-persistence.conf", typeof(Program).Assembly);
            var shardingConfig = ClusterSharding.DefaultConfig();

            var config = ConfigurationFactory.ParseString( Config );

            config = config.WithFallback( shardingConfig )
                // .WithFallback( clusterConfig )
                // .WithFallback( persistenceConfig )
                ;

            var v = config.GetConfig( "akka.actor.serializers" );


            Log.Debug( "{Config}", config.ToString( true ) );
            Log.Debug( "{Config}", shardingConfig.ToString( true ) );

            var system = ActorSystem.Create( "akka-test", config );

            MongoDbPersistence.Get( system );

            var inbox = Inbox.Create( system );

            var deviceManager = system.ActorOf( DeviceManager.Props() );

            inbox.Send( deviceManager, new RequestTrackDevice( "group", "device" ) );

            var response = inbox.Receive();

            var clusterSharding = ClusterSharding.Get( system );
            var region = await clusterSharding
                             .StartAsync(
                                 "job-manager",
                                 Job.Props(),
                                 ClusterShardingSettings.Create( system ),
                                 new MessageExtractor()
                             );


            // var collection = new MongoClient( "mongodb://by1-woiisqa-02:27017" ).GetDatabase( "akka-test" ).GetCollection<Dummy>( "dummy" );
            // collection.InsertOne( new Dummy(region) );
            //
            // var dummy = collection.Find( d => true ).First();

            inbox.Send( region,
                        new Job.ProduceJob(
                            "Job-001",
                            "Author",
                            priority: 4,
                            "Whatever",
                            new Dictionary<string, string>()
                        )
            );

            inbox.Send( region, new Job.FinishScriptStep( "Job-001", "success", "Succeeded", new string [0], 0.1 ) );
            Log.Information("{@Response}", inbox.Receive(TimeSpan.FromHours( 1 )));
            inbox.Send( region, new Job.FinishScriptStep( "Job-001", "success", "Succeeded", new string [0], 0.1 ) );
            Log.Information("{@Response}", inbox.Receive(TimeSpan.FromHours(1)));
            inbox.Send( region, new Job.FinishScriptStep( "Job-001", "success", "Succeeded", new string [0], 0.1 ) );
            Log.Information("{@Response}", inbox.Receive(TimeSpan.FromHours(1)));
            inbox.Send( region, new Job.FinishScriptStep( "Job-001", "success", "Succeeded", new string [0], 0.1 ) );
            Log.Information("{@Response}", inbox.Receive(TimeSpan.FromHours(1)));
            inbox.Send( region, new Job.FinishScriptStep( "Job-001", "success", "Succeeded", new string [0], 0.1 ) );
            Log.Information("{@Response}", inbox.Receive(TimeSpan.FromHours(1)));

            Console.ReadLine();

        }

        private static string ReadAllResourceText( string resourceName )
        {
            var assembly = typeof(Program).Assembly;
            using ( var reader = new StreamReader(assembly.GetManifestResourceStream(typeof(Program), resourceName)) )
            {
                return reader.ReadToEnd();
            }
        }
    }
}
