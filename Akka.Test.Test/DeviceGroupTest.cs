using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Util.Internal;
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
                {
                    deviceToShutdown = probe.LastSender;
                }
            }

            //  Check devices after registration.
            groupActor.Tell( new RequestDeviceList( requestId: 128 ), probe.Ref );
            probe.ExpectMsg<ReplyDeviceList>( r => r.RequestId == 128 && deviceIds.SetEquals( r.DeviceIds ) );

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

        [Fact]
        public void Device_group_query_must_return_temperature_value_for_working_devices()
        {
            var requester = CreateTestProbe();

            var device1 = CreateTestProbe();
            var device2 = CreateTestProbe();

            var queryActor = Sys.ActorOf( DeviceGroupQuery.Props(
                                              new Dictionary<IActorRef, string> { [device1.Ref] = "device1", [device2.Ref] = "device2" },
                                              requestId: 1,
                                              requester: requester.Ref,
                                              timeout: TimeSpan.FromSeconds( value: 3 ) ) );

            device1.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );
            device2.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );

            queryActor.Tell( new RespondTemperature( requestId: 0, value: 1.0 ), device1.Ref );
            queryActor.Tell( new RespondTemperature( requestId: 0, value: 3.0 ), device2.Ref );

            requester.ExpectMsg<RespondAllTemperatures>(
                msg => msg.Temperatures["device1"].AsInstanceOf<Temperature>().Value == 1.0 &&
                       msg.Temperatures["device2"].AsInstanceOf<Temperature>().Value == 3.0 &&
                       msg.RequestId == 1 );
        }

        [Fact]
        public void Device_group_query_must_return_TemperatureNotAvailable_for_devices_with_no_readings()
        {
            var requester = CreateTestProbe();

            var device1 = CreateTestProbe();
            var device2 = CreateTestProbe();

            var queryActor = Sys.ActorOf( DeviceGroupQuery.Props(
                                              new Dictionary<IActorRef, string> { [device1.Ref] = "device1", [device2.Ref] = "device2" },
                                              requestId: 1,
                                              requester: requester.Ref,
                                              timeout: TimeSpan.FromSeconds( value: 3 ) ) );

            device1.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );
            device2.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );

            queryActor.Tell( new RespondTemperature( requestId: 0, value: 1.0 ), device1.Ref );
            queryActor.Tell( new RespondTemperature( requestId: 0, value: null ), device2.Ref );

            requester.ExpectMsg<RespondAllTemperatures>(
                msg => msg.Temperatures["device1"].AsInstanceOf<Temperature>().Value == 1.0 &&
                       msg.Temperatures["device2"] is TemperatureNotAvailable &&
                       msg.RequestId == 1 );
        }

        [Fact]
        public void Device_group_query_must_return_DeviceNotAvailable_for_terminated_devices()
        {
            var requester = CreateTestProbe();

            var device1 = CreateTestProbe();
            var device2 = CreateTestProbe();

            var queryActor = Sys.ActorOf( DeviceGroupQuery.Props(
                                              new Dictionary<IActorRef, string> { [device1.Ref] = "device1", [device2.Ref] = "device2" },
                                              requestId: 1,
                                              requester: requester.Ref,
                                              timeout: TimeSpan.FromSeconds( value: 3 ) ) );

            device1.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );
            device2.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );

            queryActor.Tell( new RespondTemperature( requestId: 0, value: 1.0 ), device1.Ref );
            device2.Tell( PoisonPill.Instance );

            requester.ExpectMsg<RespondAllTemperatures>(
                msg => msg.Temperatures["device1"].AsInstanceOf<Temperature>().Value == 1.0 &&
                       msg.Temperatures["device2"] is DeviceNotAvailable &&
                       msg.RequestId == 1 );
        }

        [Fact]
        public void Device_group_query_must_return_temperature_even_if_device_is_stopped_after_answer()
        {
            var requester = CreateTestProbe();

            var device1 = CreateTestProbe();
            var device2 = CreateTestProbe();

            var queryActor = Sys.ActorOf( DeviceGroupQuery.Props(
                                              new Dictionary<IActorRef, string> { [device1.Ref] = "device1", [device2.Ref] = "device2" },
                                              requestId: 1,
                                              requester: requester.Ref,
                                              timeout: TimeSpan.FromSeconds( value: 3 ) ) );

            device1.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );
            device2.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );

            queryActor.Tell( new RespondTemperature( requestId: 0, value: 1.0 ), device1.Ref );
            queryActor.Tell( new RespondTemperature( requestId: 0, value: 3 ), device2.Ref );
            device2.Tell( PoisonPill.Instance );

            requester.ExpectMsg<RespondAllTemperatures>(
                msg => msg.Temperatures["device1"].AsInstanceOf<Temperature>().Value == 1.0 &&
                       msg.Temperatures["device2"].AsInstanceOf<Temperature>().Value == 3.0 &&
                       msg.RequestId == 1 );
        }

        [Fact]
        public void Device_group_query_must_return_DeviceTimedOut_for_not_replying_devices()
        {
            var requester = CreateTestProbe();

            var device1 = CreateTestProbe();
            var device2 = CreateTestProbe();

            var queryActor = Sys.ActorOf( DeviceGroupQuery.Props(
                                              new Dictionary<IActorRef, string> { [device1.Ref] = "device1", [device2.Ref] = "device2" },
                                              requestId: 1,
                                              requester: requester.Ref,
                                              timeout: TimeSpan.FromSeconds( value: 1 ) ) );

            device1.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );
            device2.ExpectMsg<ReadTemperature>( read => read.RequestId == 0 );

            queryActor.Tell( new RespondTemperature( requestId: 0, value: 1.0 ), device1.Ref );

            requester.ExpectMsg<RespondAllTemperatures>(
                msg => msg.Temperatures["device1"].AsInstanceOf<Temperature>().Value == 1.0 &&
                       msg.Temperatures["device2"] is DeviceTimedOut &&
                       msg.RequestId == 1 );
        }

        [Fact]
        public void Device_group_must_return_temperature_for_all_devices()
        {
            var probe = CreateTestProbe();

            var groupActor = Sys.ActorOf( DeviceGroup.Props( "group1" ) );

            groupActor.Tell( new RequestTrackDevice( "group1", "device1" ), probe.Ref );
            probe.ExpectMsg<DeviceRegistered>( o => true );
            var deviceActor1 = probe.LastSender;

            groupActor.Tell( new RequestTrackDevice( "group1", "device2" ), probe.Ref);
            probe.ExpectMsg<DeviceRegistered>();
            var deviceActor2 = probe.LastSender;

            groupActor.Tell( new RequestTrackDevice( "group1", "device3" ), probe.Ref);
            probe.ExpectMsg<DeviceRegistered>();
            var deviceActor3 = probe.LastSender;

            deviceActor1.Tell( new RecordTemperature( requestId: 1, value: 25.0 ), probe.Ref);
            probe.ExpectMsg<TemperatureRecorded>( r => r.RequestId == 1 );

            deviceActor2.Tell( new RecordTemperature( requestId: 2, value: 75.0 ), probe.Ref);
            probe.ExpectMsg<TemperatureRecorded>( r => r.RequestId == 2 );

            //  Get all devices temperature.
            groupActor.Tell( new RequestAllTemperatures( requestId: 3 ), probe.Ref);
            probe.ExpectMsg<RespondAllTemperatures>( 
                r => r.Temperatures["device1"].AsInstanceOf<Temperature>().Value == 25.0 &&
                     r.Temperatures["device2"].AsInstanceOf<Temperature>().Value == 75.0 &&
                     r.Temperatures["device3"] is TemperatureNotAvailable &&
                     r.RequestId == 3
            );
        }

        #endregion
    }
}
