using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Test.DDD.Infrastructure;
using Akka.Test.DDD.Infrastructure.Event;

namespace Akka.Test.Domain.Tasks
{
    public class JobState : AggregateState
    {
        public string Id { get; }
        public string Origin { get; }

        public int Priority { get; }
        public string SerializedScript { get; }
        public Dictionary<string, string> Parameters { get; }

        public double Progress { get; private set; }
        public string State { get; private set; }
        public string StatusText { get; private set; }
        public IReadOnlyList<string> Notes { get; private set; } = ImmutableList.Create<string>();

        public string Status { get; private set; }

        public int ScriptStepIndex { get; set; }

        public JobState( string id, string origin, int priority, string serializedScript, Dictionary<string, string> parameters )
        {
            Id = id;
            Origin = origin;
            Priority = priority;
            SerializedScript = serializedScript;
            Parameters = parameters;
            ScriptStepIndex = 0;
        }

        public JobState With( double? progress = null,
                              string state = null,
                              string status = null,
                              string statusText = null,
                              IReadOnlyList<string> notes = null,
                              int? scriptStepIndex = null ) => new JobState(
            Id,
            Origin,
            Priority,
            SerializedScript,
            Parameters
        )
        {
            Progress = progress ?? Progress,
            State = state ?? State,
            Status = status ?? Status,
            StatusText = statusText ?? StatusText,
            Notes = notes != null ? Notes.Concat( notes ).ToImmutableList() : Notes,
            ScriptStepIndex = scriptStepIndex ?? ScriptStepIndex
        };

        public override AggregateState Apply( DomainEvent @event )
        {
            switch ( @event )
            {
                case Job.ScriptStepFinished stepFinished:
                    return With( statusText: stepFinished.StatusText, notes: stepFinished.Notes, progress: stepFinished.Progress );

                case Job.NextScriptStepStarted nextStepStarted:
                    return With( scriptStepIndex: ScriptStepIndex + 1 );

                case Job.JobFailed jobFailed:
                    return With( state: "finished", status: "failed", statusText: jobFailed.StatusText, notes: jobFailed.Notes, progress: 1 );

                case Job.JobTerminated jobTerminated:
                    return With( state: "finished", status: "terminated", statusText: jobTerminated.StatusText, notes: jobTerminated.Notes, progress: 1 );

                case Job.JobSucceeded jobSucceeded:
                    return With( state: "finished", status: "success", statusText: jobSucceeded.StatusText, notes: jobSucceeded.Notes, progress: 1 );

                default:
                    throw new InvalidOperationException( "Invalid event" );
            }
        }
    }
}