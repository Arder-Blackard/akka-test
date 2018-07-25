using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;
using RestSharp;

namespace Akka.Test
{
    public sealed class Reader : ReceiveActor
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