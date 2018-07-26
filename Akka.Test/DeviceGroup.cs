using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;

namespace Akka.Test
{
    public sealed class RequestDeviceList
    {
        public long RequestId { get; }

        public RequestDeviceList( long requestId )
        {
            RequestId = requestId;
        }
    }

    public sealed class ReplyDeviceList
    {
        public long RequestId { get; }
        public ICollection<string> DeviceIds { get; }

        /// <inheritdoc />
        public ReplyDeviceList( long requestId, ICollection<string> deviceIds )
        {
            RequestId = requestId;
            DeviceIds = deviceIds;
        }
    }

    public sealed class DeviceGroup : UntypedActor
    {
        #region Fields

        private readonly ILoggingAdapter _logger = Context.GetLogger<SerilogLoggingAdapter>();
        private readonly Dictionary<string, IActorRef> _devices = new Dictionary<string, IActorRef>();

        #endregion


        #region Auto-properties

        private string GroupId { get; }

        #endregion


        #region Events and invocation

        /// <inheritdoc />
        protected override void OnReceive( object message )
        {
            switch ( message )
            {
                case RequestTrackDevice request when request.GroupId == GroupId:
                    if ( !_devices.TryGetValue( request.DeviceId, out var deviceActorRef ) )
                    {
                        _logger.Info( "Create new device actor {DeviceId}", request.DeviceId );
                        deviceActorRef = Context.ActorOf( Device.Props( GroupId, request.DeviceId ), $"device-{request.DeviceId}" );
                        Context.Watch(deviceActorRef);
                        _devices[request.DeviceId] = deviceActorRef;
                    }

                    deviceActorRef.Forward( request );
                    break;

                case RequestTrackDevice request:
                    _logger.Warning( "Ignoring incorrect TrackDevice request for {RequestGroupId}. The actor is responsible for {GroupId}",
                                     request.GroupId, request.DeviceId, GroupId );
                    break;

                case RequestDeviceList request:
                    Context.Sender.Tell( new ReplyDeviceList( request.RequestId, _devices.Keys.ToList() ) );
                    break;

                case Terminated terminated:
                    var deviceId = _devices.FirstOrDefault( kvp => kvp.Value.Equals( terminated.ActorRef ) ).Key;
                    if ( deviceId != null )
                    {
                        _devices.Remove( deviceId );
                    }

                    break;
            }
        }

        #endregion


        #region Initialization

        /// <inheritdoc />
        public DeviceGroup( string groupId )
        {
            GroupId = groupId;
        }

        #endregion


        #region Public methods

        public static Props Props( string groupId ) => Actor.Props.Create( () => new DeviceGroup( groupId ) );

        #endregion


        #region Non-public methods

        /// <inheritdoc />
        protected override void PreStart() => _logger.Info( "Device group {GroupId} started", GroupId );

        /// <inheritdoc />
        protected override void PostStop() => _logger.Info( "Device group {GroupId} stopped", GroupId );

        #endregion
    }
}
