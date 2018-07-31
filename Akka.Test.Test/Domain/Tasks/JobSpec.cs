using System.Collections.Generic;
using Akka.Actor;
using Akka.Test.Domain.Tasks;
using Akka.Test.Test.Infrastructure;
using AutoFixture;
using Xunit;

namespace Akka.Test.Test.Domain.Tasks
{
    public sealed class JobSpec : EventSourcedAggregateRootSpec
    {
        protected override string AggregateRootId => "Job-001";

        [Fact]
        public void Job_must_handle_job_process()
        {
            var fixture = new Fixture();

            var jobId = AggregateRootId;
            var job = GetJobActor( jobId );

            ExpectEventPersisted<Job.JobProduced>( () =>
            {
                var command = new Job.ProduceJob(
                    jobId,
                    fixture.Create<string>(),
                    fixture.Create<int>(),
                    fixture.Create<string>(),
                    fixture.Create<Dictionary<string, string>>() );

                job.Tell( command );
            } );

            ExpectEventPersisted<Job.ScriptStepFinished>( () =>
            {
                var command = new Job.FinishScriptStep(
                    jobId,
                    "success",
                    fixture.Create<string>(),
                    fixture.Create<IReadOnlyList<string>>(),
                    progress: 0.5 );

                job.Tell( command );
            } );
        }

        private IActorRef GetJobActor( string name ) => GetActor( Job.Props( AggregateRootId ), name );
    }
}
