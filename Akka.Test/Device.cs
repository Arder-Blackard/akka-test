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

    public sealed class RequestTrackDevice
    {
        #region Auto-properties

        public string GroupId { get; }
        public string DeviceId { get; }

        #endregion


        #region Initialization

        public RequestTrackDevice( string groupId, string deviceId )
        {
            GroupId = groupId;
            DeviceId = deviceId;
        }

        #endregion
    }

    public sealed class DeviceRegistered
    {
        #region Auto-properties

        public static DeviceRegistered Instance { get; } = new DeviceRegistered();

        #endregion


        #region Initialization

        private DeviceRegistered()
        {
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

                case RequestTrackDevice request when request.GroupId == GroupId && request.DeviceId == DeviceId:
                    Sender.Tell( DeviceRegistered.Instance );
                    break;

                case RequestTrackDevice request:
                    _logger.Warning( "Ignoring incorrect TrackDevice request for {RequestGroupId}-{RequestDeviceId}. The actor is responsible for {GroupId}-{DeviceId}",
                                     request.GroupId, request.DeviceId, GroupId, DeviceId );
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
        protected override void PostStop() => _logger.Info( "Device actor {GroupId}-{DeviceId} stopped", GroupId, DeviceId );

        #endregion
    }
}
