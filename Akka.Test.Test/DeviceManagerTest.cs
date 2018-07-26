using System;
using Shouldly;
using Xunit;

namespace Akka.Test.Test
{
    public sealed class DeviceManagerTest : TestKit.Xunit2.TestKit
    {
        #region Public methods

        [Fact]
        public void Device_manager_should_accept_device_registration_requests()
        {
            var probe = CreateTestProbe();
            var managerActor = Sys.ActorOf( DeviceManager.Props() );

            managerActor.Tell( new RequestTrackDevice( "Group1", "Device1" ), probe.Ref );
            probe.ExpectMsg<DeviceRegistered>();
            var device1Actor = probe.LastSender;

            managerActor.Tell( new RequestTrackDevice( "Group1", "Device2" ), probe.Ref );
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
        public void Device_group_should_return_the_same_actor_for_the_same_device_id()
        {
            var probe = CreateTestProbe();
            var managerActor = Sys.ActorOf( DeviceManager.Props() );

            managerActor.Tell( new RequestTrackDevice( "Group1", "Device1" ), probe.Ref );
            probe.ExpectMsg<DeviceRegistered>( TimeSpan.FromMilliseconds( value: 10000 ) );
            var device1Actor = probe.LastSender;

            managerActor.Tell( new RequestTrackDevice( "Group1", "Device1" ), probe.Ref );
            probe.ExpectMsg<DeviceRegistered>( TimeSpan.FromMilliseconds( value: 10000 ) );
            var device2Actor = probe.LastSender;

            device2Actor.ShouldBe( device1Actor );
        }

        #endregion
    }
}
