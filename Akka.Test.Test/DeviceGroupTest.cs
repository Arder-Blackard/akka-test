using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Shouldly;
using Xunit;

namespace Akka.Test.Test
{
    public sealed class DeviceGroupTest : TestKit.Xunit2.TestKit
    {
        #region Public methods

        [Fact]
        public void Device_group_should_accept_device_registration_requests()
        {
            var probe = CreateTestProbe();
            var groupActor = Sys.ActorOf( DeviceGroup.Props( "Group1" ) );

            groupActor.Tell( new RequestTrackDevice( "Group1", "Device1" ), probe.Ref );
            probe.ExpectMsg<DeviceRegistered>();
            var device1Actor = probe.LastSender;

            groupActor.Tell( new RequestTrackDevice( "Group1", "Device2" ), probe.Ref );
            probe.ExpectMsg<DeviceRegistered>();
            var device2Actor = probe.LastSender;
            device2Actor.ShouldNotBe( device1Actor );

            //  Check that devices are working.
            device1Actor.Tell( new RecordTemperature( requestId: 55, value: 42.0 ), probe.Ref );
            probe.ExpectMsg<TemperatureRecorded>( r => r.RequestId == 55 );
            device2Actor.Tell( new RecordTemperature( requestId: 56, value: 42.0 ), probe.Ref );
            probe.ExpectMsg<TemperatureRecorded>( r => r.RequestId == 56 );
        }

        [Fact]
        public void Device_group_should_ignore_device_registration_requests_with_wrong_group_id()
        {
            var probe = CreateTestProbe();
            var groupActor = Sys.ActorOf( DeviceGroup.Props( "Group1" ) );

            groupActor.Tell( new RequestTrackDevice( "Group2", "Device1" ), probe.Ref );
            probe.ExpectNoMsg( TimeSpan.FromMilliseconds( value: 500 ) );
        }

        [Fact]
        public void Device_group_should_return_the_same_actor_for_the_same_device_id()
        {
            var probe = CreateTestProbe();
            var groupActor = Sys.ActorOf( DeviceGroup.Props( "Group1" ) );

            groupActor.Tell( new RequestTrackDevice( "Group1", "Device1" ), probe.Ref );
            probe.ExpectMsg<DeviceRegistered>();
            var device1Actor = probe.LastSender;

            groupActor.Tell( new RequestTrackDevice( "Group1", "Device1" ), probe.Ref );
            probe.ExpectMsg<DeviceRegistered>();
            var device2Actor = probe.LastSender;

            device2Actor.ShouldBe( device1Actor );
        }

        [Fact]
        public void Device_group_should_be_able_to_list_devices()
        {
            var probe = CreateTestProbe();
            var groupActor = Sys.ActorOf( DeviceGroup.Props( "Group1" ) );

            var deviceIds = new HashSet<string> { "Device1", "Device2" };

            foreach ( var deviceId in deviceIds )
            {
                groupActor.Tell( new RequestTrackDevice( "Group1", deviceId ), probe.Ref );
                probe.ExpectMsg<DeviceRegistered>();
            }

            groupActor.Tell( new RequestDeviceList( requestId: 128 ), probe.Ref );
            probe.ExpectMsg<ReplyDeviceList>( r => r.RequestId == 128 && deviceIds.SetEquals( r.DeviceIds ) );
        }

        [Fact]
        public void Device_group_should_be_able_to_list_devices_after_one_shutdown()
        {
            var probe = CreateTestProbe();
            var groupActor = Sys.ActorOf( DeviceGroup.Props( "Group1" ) );

            var deviceIds = new HashSet<string> { "Device1", "Device2" };

            var deviceToShutdownId = deviceIds.First();
            IActorRef deviceToShutdown = null;
            foreach ( var deviceId in deviceIds )
            {
                groupActor.Tell( new RequestTrackDevice( "Group1", deviceId ), probe.Ref );
                probe.ExpectMsg<DeviceRegistered>();
                if ( deviceId == deviceToShutdownId )
                    deviceToShutdown = probe.LastSender;
            }
            
            //  Check devices after registration.
            groupActor.Tell(new RequestDeviceList(requestId: 128), probe.Ref);
            probe.ExpectMsg<ReplyDeviceList>(r => r.RequestId == 128 && deviceIds.SetEquals(r.DeviceIds));


            deviceIds.Remove( deviceToShutdownId );
            probe.Watch( deviceToShutdown );

            deviceToShutdown.Tell( PoisonPill.Instance );

            probe.ExpectTerminated( deviceToShutdown );

            //  Check devices after termination.
            probe.AwaitAssert( () =>
            {
                groupActor.Tell( new RequestDeviceList( requestId: 129 ), probe.Ref );
                probe.ExpectMsg<ReplyDeviceList>( r => r.RequestId == 129 && deviceIds.SetEquals( r.DeviceIds ) );
            } );
        }

        #endregion
    }
}