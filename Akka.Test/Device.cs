using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;

namespace Akka.Test
{
    public sealed class ReadTemperature
    {
        #region Auto-properties

        public long RequestId { get; }

        #endregion


        #region Initialization

        /// <inheritdoc />
        public ReadTemperature( long requestId )
        {
            RequestId = requestId;
        }

        #endregion
    }

    public sealed class RespondTemperature
    {
        #region Auto-properties

        public long RequestId { get; }
        public double? Value { get; }

        #endregion


        #region Initialization

        /// <inheritdoc />
        public RespondTemperature( long requestId, double? value )
        {
            RequestId = requestId;
            Value = value;
        }

        #endregion
    }

    public sealed class RecordTemperature
    {
        #region Auto-properties

        public long RequestId { get; }
        public double? Value { get; }

        #endregion


        #region Initialization

        /// <inheritdoc />
        public RecordTemperature( long requestId, double? value )
        {
            RequestId = requestId;
            Value = value;
        }

        #endregion
    }

    public sealed class TemperatureRecorded
    {
        #region Auto-properties

        public long RequestId { get; }

        #endregion


        #region Initialization

        public TemperatureRecorded( long requestId )
        {
            RequestId = requestId;
        }

        #endregion
    }

    public sealed class Device : UntypedActor
    {
        #region Fields

        private readonly ILoggingAdapter _logger = Context.GetLogger<SerilogLoggingAdapter>();

        private double? _lastTemperatureReading;

        #endregion


        #region Auto-properties

        private string GroupId { get; }
        private string DeviceId { get; }

        #endregion


        #region Events and invocation

        /// <inheritdoc />
        protected override void OnReceive( object message )
        {
            switch ( message )
            {
                case RecordTemperature recordTemperature:
                    _logger.Info( "{RequestId}: Settings temperature to {Temperature}", recordTemperature.Value );
                    _lastTemperatureReading = recordTemperature.Value;
                    Sender.Tell( new TemperatureRecorded( recordTemperature.RequestId ) );
                    break;

                case ReadTemperature readTemperature:
                    Sender.Tell( new RespondTemperature( readTemperature.RequestId, _lastTemperatureReading ) );
                    break;
            }
        }

        #endregion


        #region Initialization

        /// <inheritdoc />
        public Device( string groupId, string deviceId )
        {
            GroupId = groupId;
            DeviceId = deviceId;
        }

        #endregion


        #region Public methods

        public static Props Props( string groupId, string deviceId ) => Actor.Props.Create( () => new Device( groupId, deviceId ) );

        #endregion


        #region Non-public methods

        /// <inheritdoc />
        protected override void PreStart() => _logger.Info( "Device actor {GroupId}-{DeviceId} started", GroupId, DeviceId );

        /// <inheritdoc />
        protected override void PostStop() => _logger.Info( "Device actor {GroupId}-{DeviceId} started", GroupId, DeviceId );

        #endregion
    }
}
