using System;
using Akka.Actor;
using Akka.Test.DDD.Infrastructure;
using Akka.Test.DDD.Infrastructure.Event;

namespace Akka.Test.Domain.Tasks
{
    public sealed partial class Job : AggregateRoot<JobState>
    {
        protected override Func<DomainEvent, JobState> Factory => domainEvent =>
        {
            if ( domainEvent is JobProduced jobProduced )
            {
                return new JobState(
                    jobProduced.JobId,
                    jobProduced.Origin,
                    jobProduced.Priority,
                    jobProduced.SerializedScript,
                    jobProduced.Parameters
                );
            }

            throw new InvalidOperationException( "Invalid event" );
        };

        public Job( string persistenceId ) : base( persistenceId )
        {
        }

        public static Props Props( string jobId ) => Actor.Props.Create( () => new Job( jobId ) );

        protected override void OnCommand( object command )
        {
            switch ( command )
            {
                case ProduceJob produceJob:
                    if ( IsInitialized )
                    {
                        throw new InvalidOperationException( "The job is already produced" );
                    }
                    else
                    {
                        Raise( new JobProduced(
                                   produceJob.JobId,
                                   produceJob.Origin,
                                   produceJob.Priority,
                                   produceJob.SerializedScript,
                                   produceJob.Parameters ) );
                    }

                    break;

                case FinishScriptStep finish when finish.Status == "success":
                    Raise( new ScriptStepFinished( State.Id, finish.StatusText, finish.Notes, finish.Progress ) );
                    break;

                case FinishScriptStep finish when finish.Status == "failed":
                    Raise( new JobFailed( State.Id, finish.StatusText, finish.Notes ) );
                    break;

                case FinishScriptStep finish when finish.Status == "terminated":
                    Raise( new JobTerminated( State.Id, finish.StatusText, finish.Notes ) );
                    break;
            }
        }
    }
}
