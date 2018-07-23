using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;
using RestSharp;
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

            var application = system.ActorOf( Application.Props() );
            application.Tell( "print" );

            Console.ReadLine();
        }

        #endregion
    }

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

    public class Reader : ReceiveActor
    {
        #region Fields

        private readonly ILoggingAdapter _logger = Context.GetLogger<SerilogLoggingAdapter>();

        #endregion


        /// <inheritdoc />
        public Reader()
        {
            ReceiveAsync<string>( async message =>
            {
                _logger.Info("Reader received message {Message}", message);
                switch (message)
                {
                    case "print":
                        var result = await GetResultAsync();
                        _logger.Info("Result: {Result}", result);
                        break;

                    case "shutdown":
                        Context.Stop(Self);
                        break;
                }

            });

        }


        #region Events and invocation

        public async Task<string> GetResultAsync()
        {
            var request = new RestRequest
            {
                Resource = "?amount=1"
            };

            var client = new RestClient( "http://uinames123.com/api/" );
            var response = await client.ExecuteTaskAsync<string>( request );
            return response.Data;
        }

        #endregion


        #region Public methods

        public static Props Props() => Actor.Props.Create<Reader>();

        #endregion
    }
}
