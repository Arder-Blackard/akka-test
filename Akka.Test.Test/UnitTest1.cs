using Shouldly;
using Xunit;

namespace Akka.Test.Test
{
    public class DeviceTest : TestKit.Xunit2.TestKit
    {
        #region Public methods

        [Fact]
        public void Device_actor_must_reply_with_empty_value_if_the_temperature_is_unknown()
        {
            var probe = CreateTestProbe();
            var deviceActor = Sys.ActorOf( Device.Props( "Group1", "Device1" ) );

            deviceActor.Tell( new ReadTemperature( requestId: 42 ), probe.Ref );

            var response = probe.ExpectMsg<RespondTemperature>();
            response.RequestId.ShouldBe( expected: 42 );
            response.Value.ShouldBeNull();
        }

        [Fact]
        public void Device_actor_must_reply_with_an_up_to_date_temperature()
        {
            var probe = CreateTestProbe();
            var deviceActor = Sys.ActorOf( Device.Props( "Group1", "Device1" ) );

            deviceActor.Tell( new RecordTemperature( requestId: 43, value: 78 ), probe.Ref );
            probe.ExpectMsg<TemperatureRecorded>( r => r.RequestId == 43 );

            deviceActor.Tell( new ReadTemperature( requestId: 44 ), probe.Ref );

            var response = probe.ExpectMsg<RespondTemperature>();
            response.RequestId.ShouldBe( expected: 44 );
            response.Value.ShouldBe( expected: 78 );
        }

        #endregion
    }
}
