using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;

namespace Akka.Test
{
    public sealed class DeviceManager : UntypedActor
    {
        #region Fields

        private readonly ILoggingAdapter _logger = Context.GetLogger<SerilogLoggingAdapter>();
        private readonly Dictionary<string, IActorRef> _deviceGroups = new Dictionary<string, IActorRef>();

        #endregion


        #region Events and invocation

        /// <inheritdoc />
        protected override void OnReceive( object message )
        {
            switch ( message )
            {
                case RequestTrackDevice request:
                    if ( !_deviceGroups.TryGetValue( request.GroupId, out var groupActorRef ) )
                    {
                        _logger.Info( "Create new device group {GroupId}", request.GroupId );
                        groupActorRef = Context.ActorOf( DeviceGroup.Props( request.GroupId ), $"group-{request.GroupId}" );
                        Context.Watch( groupActorRef );
                        _deviceGroups[request.GroupId] = groupActorRef;
                    }

                    groupActorRef.Forward( request );
                    break;

                case Terminated terminated:
                    var groupId = _deviceGroups.FirstOrDefault( kvp => kvp.Value.Equals( terminated.ActorRef ) ).Key;
                    if ( groupId != null )
                    {
                        _deviceGroups.Remove( groupId );
                    }

                    break;
            }
        }

        #endregion


        #region Public methods

        public static Props Props() => Actor.Props.Create( () => new DeviceManager() );

        #endregion


        #region Non-public methods

        /// <inheritdoc />
        protected override void PreStart() => _logger.Info( "Device groups manager started" );

        /// <inheritdoc />
        protected override void PostStop() => _logger.Info( "Device groups manager stopped" );

        #endregion
    }
}
