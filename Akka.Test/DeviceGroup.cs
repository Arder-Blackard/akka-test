using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;

namespace Akka.Test
{
    public sealed class RequestDeviceList
    {
        #region Auto-properties

        public long RequestId { get; }

        #endregion


        #region Initialization

        public RequestDeviceList( long requestId )
        {
            RequestId = requestId;
        }

        #endregion
    }

    public sealed class ReplyDeviceList
    {
        #region Auto-properties

        public long RequestId { get; }
        public ICollection<string> DeviceIds { get; }

        #endregion


        #region Initialization

        /// <inheritdoc />
        public ReplyDeviceList( long requestId, ICollection<string> deviceIds )
        {
            RequestId = requestId;
            DeviceIds = deviceIds;
        }

        #endregion
    }

    public sealed class RequestAllTemperatures
    {
        #region Auto-properties

        public long RequestId { get; }

        #endregion


        #region Initialization

        public RequestAllTemperatures( long requestId )
        {
            RequestId = requestId;
        }

        #endregion
    }

    public sealed class RespondAllTemperatures
    {
        #region Auto-properties

        public long RequestId { get; }
        public IReadOnlyDictionary<string, ITemperatureValue> Temperatures { get; }

        #endregion


        #region Initialization

        public RespondAllTemperatures( long requestId, IReadOnlyDictionary<string, ITemperatureValue> temperatures )
        {
            RequestId = requestId;
            Temperatures = temperatures;
        }

        #endregion
    }

    public interface ITemperatureValue
    {
    }

    public sealed class Temperature : ITemperatureValue
    {
        #region Auto-properties

        public double Value { get; }

        #endregion


        #region Initialization

        public Temperature( double value )
        {
            Value = value;
        }

        #endregion
    }

    public sealed class TemperatureNotAvailable : ITemperatureValue
    {
        #region Auto-properties

        public static TemperatureNotAvailable Instance { get; } = new TemperatureNotAvailable();

        #endregion


        #region Initialization

        private TemperatureNotAvailable()
        {
        }

        #endregion
    }

    public sealed class DeviceNotAvailable : ITemperatureValue
    {
        #region Auto-properties

        public static DeviceNotAvailable Instance { get; } = new DeviceNotAvailable();

        #endregion


        #region Initialization

        private DeviceNotAvailable()
        {
        }

        #endregion
    }

    public sealed class DeviceTimedOut : ITemperatureValue
    {
        #region Auto-properties

        public static DeviceTimedOut Instance { get; } = new DeviceTimedOut();

        #endregion


        #region Initialization

        private DeviceTimedOut()
        {
        }

        #endregion
    }

    public sealed class CollectionTimeout
    {
        #region Auto-properties

        public static CollectionTimeout Instance { get; } = new CollectionTimeout();

        #endregion


        #region Initialization

        private CollectionTimeout()
        {
        }

        #endregion
    }

    public sealed class DeviceGroupQuery : UntypedActor
    {
        #region Fields

        private readonly ILoggingAdapter _logger = Context.GetLogger<SerilogLoggingAdapter>();

        private readonly ICancelable _queryTimeoutTimer;

        #endregion


        #region Auto-properties

        public IDictionary<IActorRef, string> DeviceMap { get; }
        public long RequestId { get; }
        public IActorRef Requester { get; }
        public TimeSpan Timeout { get; }

        #endregion


        #region Events and invocation

        protected override void OnReceive( object message )
        {
        }

        #endregion


        #region Initialization

        /// <inheritdoc />
        public DeviceGroupQuery( IDictionary<IActorRef, string> deviceMap, long requestId, IActorRef requester, TimeSpan timeout )
        {
            DeviceMap = deviceMap;
            RequestId = requestId;
            Requester = requester;
            Timeout = timeout;
            _queryTimeoutTimer = Context.System.Scheduler.ScheduleTellOnceCancelable( timeout, Self, CollectionTimeout.Instance, Self );
            Become( WaitingForReplies( new Dictionary<string, ITemperatureValue>(), new HashSet<IActorRef>( deviceMap.Keys ) ) );
        }

        #endregion


        #region Public methods

        public static Props Props( IDictionary<IActorRef, string> deviceMap, long requestId, IActorRef requester, TimeSpan timeout )
        {
            return Actor.Props.Create( () => new DeviceGroupQuery( deviceMap, requestId, requester, timeout ) );
        }

        #endregion


        #region Non-public methods

        private UntypedReceive WaitingForReplies( Dictionary<string, ITemperatureValue> repliesSoFar, HashSet<IActorRef> stillWaiting )
        {
            return message =>
            {
                switch ( message )
                {
                    case RespondTemperature temperature when temperature.RequestId == 0:
                        var deviceActor = Sender;
                        ITemperatureValue value = null;
                        if ( temperature.Value.HasValue )
                        {
                            value = new Temperature( (double) temperature.Value );
                        }
                        else
                        {
                            value = TemperatureNotAvailable.Instance;
                        }

                        ReceivedResponse( deviceActor, value, stillWaiting, repliesSoFar );
                        break;

                    case Terminated terminated:
                        ReceivedResponse( terminated.ActorRef, DeviceNotAvailable.Instance, stillWaiting, repliesSoFar );
                        break;

                    case CollectionTimeout _:
                        var replies = new Dictionary<string, ITemperatureValue>( repliesSoFar );
                        foreach ( var actorRef in stillWaiting )
                        {
                            replies.Add( DeviceMap[actorRef], DeviceTimedOut.Instance );
                        }

                        Requester.Tell( new RespondAllTemperatures( RequestId, replies ) );
                        Context.Stop( Self );
                        break;
                }
            };
        }

        private void ReceivedResponse( IActorRef deviceActor, ITemperatureValue value, HashSet<IActorRef> stillWaiting, Dictionary<string, ITemperatureValue> repliesSoFar )
        {
            Context.Unwatch( deviceActor );
            var deviceId = DeviceMap[deviceActor];
            stillWaiting.Remove( deviceActor );
            repliesSoFar.Add( deviceId, value );
            if ( stillWaiting.Count == 0 )
            {
                Requester.Tell( new RespondAllTemperatures( RequestId, repliesSoFar ) );
                Context.Stop( Self );
            }
            else
            {
                Context.Become( WaitingForReplies( repliesSoFar, stillWaiting ) );
            }
        }

        /// <inheritdoc />
        protected override void PreStart()
        {
            foreach ( var device in DeviceMap.Keys )
            {
                Context.Watch( device );
                device.Tell( new ReadTemperature( requestId: 0 ) );
            }
        }

        /// <inheritdoc />
        protected override void PostStop()
        {
            _queryTimeoutTimer.Cancel();
        }

        #endregion
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
                        Context.Watch( deviceActorRef );
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

                case RequestAllTemperatures request:
                    Context.ActorOf( DeviceGroupQuery.Props(
                                         _devices.ToImmutableDictionary( kvp => kvp.Value, kvp => kvp.Key ),
                                         request.RequestId,
                                         Sender,
                                         TimeSpan.FromSeconds( value: 3 ) ) );
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
