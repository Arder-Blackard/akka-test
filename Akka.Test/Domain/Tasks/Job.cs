using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Persistence;

namespace Akka.Test.Domain.Tasks
{
    public abstract class DomainEvent
    {
    }

    public sealed class Acknowledged
    {
    }

    public abstract class AggregateState
    {
        public abstract AggregateState Apply( DomainEvent @event );
    }

    public abstract class AggregateRoot<TState> : UntypedPersistentActor where TState : AggregateState
    {
        private TState _state;

        public override string PersistenceId { get; }

        public bool IsInitialized => _state != null;

        protected abstract Func<DomainEvent, TState> Factory { get; }

        public AggregateRoot( string persistenceId )
        {
            PersistenceId = persistenceId;
        }

        public void Raise<TEvent>( TEvent @event, Action<TEvent> handler = null ) where TEvent : DomainEvent
        {
            Persist( @event, persistedEvent =>
            {
                // _log.Verbose( "Event persisted: {@Event}", persistedEvent );
                UpdateState( @event );
                (handler ?? Handle).Invoke( @event );
            } );
        }

        public void Handle<TEvent>( TEvent @event ) where TEvent : DomainEvent
        {
            Publish( @event );
            Sender.Tell( new Acknowledged(), Self );
        }

        protected override void OnRecover( object message )
        {
            if ( message is DomainEvent @event )
            {
                UpdateState( @event );
            }
        }

        private void Publish<TEvent>( TEvent @event ) where TEvent : DomainEvent
        {
            Context.System.EventStream.Publish( @event );
        }

        private void UpdateState( DomainEvent @event )
        {
            _state = IsInitialized ? (TState) _state.Apply( @event ) : Factory( @event );
        }

        protected override void PreRestart( Exception reason, object message )
        {
            Sender.Tell( new Status.Failure( reason ), Self );
            base.PreRestart( reason, message );
        }
    }

    public class Job : AggregateRoot<JobState>
    {
        protected override Func<DomainEvent, JobState> Factory => domainEvent =>
        {
            if ( domainEvent is JobProduced jobProduced )
            {
                return new JobState(
                    jobProduced.Id,
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
                                   produceJob.Id,
                                   produceJob.Origin,
                                   produceJob.Priority,
                                   produceJob.SerializedScript,
                                   produceJob.Parameters ) );
                    }

                    break;

                case FailJob failJob:
                    Raise( new JobFailed( failJob.StatusText, failJob.Notes ) );
                    break;
            }
        }
    }

    public class FailJob
    {
        public string StatusText { get; }
        public IReadOnlyList<string> Notes { get; }

        public FailJob( string statusText, IReadOnlyList<string> notes )
        {
            StatusText = statusText;
            Notes = notes;
        }
    }

    public class ProduceJob
    {
        public string Id { get; set; }
        public string Origin { get; set; }
        public int Priority { get; set; }
        public string SerializedScript { get; set; }
        public Dictionary<string, string> Parameters { get; set; }

        public ProduceJob( string id, string origin, int priority, string serializedScript, Dictionary<string, string> parameters )
        {
            Id = id;
            Origin = origin;
            Priority = priority;
            SerializedScript = serializedScript;
            Parameters = parameters;
        }
    }

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
                case ScriptStepFinished stepFinished:
                    return With( statusText: stepFinished.StatusText, notes: stepFinished.Notes, progress: stepFinished.Progress );

                case NextScriptStepStarted nextStepStarted:
                    return With( scriptStepIndex: ScriptStepIndex + 1 );

                case JobFailed jobFailed:
                    return With( state: "finished", status: "failed", statusText: jobFailed.StatusText, notes: jobFailed.Notes, progress: 1 );

                case JobTerminated jobTerminated:
                    return With( state: "finished", status: "terminated", statusText: jobTerminated.StatusText, notes: jobTerminated.Notes, progress: 1 );

                case JobSucceeded jobSucceeded:
                    return With( state: "finished", status: "success", statusText: jobSucceeded.StatusText, notes: jobSucceeded.Notes, progress: 1 );

                default:
                    throw new InvalidOperationException( "Invalid event" );
            }
        }
    }

    public class ScriptStepFinished : DomainEvent
    {
        public string StatusText { get; }
        public IReadOnlyList<string> Notes { get; }
        public double Progress { get; }

        public ScriptStepFinished( string statusText, IReadOnlyList<string> notes, double progress )
        {
            StatusText = statusText;
            Notes = notes;
            Progress = progress;
        }
    }

    public class NextScriptStepStarted : DomainEvent
    {
    }

    public class JobFailed : DomainEvent
    {
        public string StatusText { get; }
        public IReadOnlyList<string> Notes { get; }

        public JobFailed( string statusText, IReadOnlyList<string> notes )
        {
            StatusText = statusText;
            Notes = notes;
        }
    }

    public class JobTerminated : DomainEvent
    {
        public string StatusText { get; }
        public IReadOnlyList<string> Notes { get; }

        public JobTerminated( string statusText, IReadOnlyList<string> notes )
        {
            StatusText = statusText;
            Notes = notes;
        }
    }

    public class JobSucceeded : DomainEvent
    {
        public string StatusText { get; }
        public IReadOnlyList<string> Notes { get; }

        public JobSucceeded( string statusText, IReadOnlyList<string> notes )
        {
            StatusText = statusText;
            Notes = notes;
        }
    }

    public class JobProduced : DomainEvent
    {
        public string Id { get; set; }
        public string Origin { get; set; }
        public int Priority { get; set; }
        public string SerializedScript { get; set; }
        public Dictionary<string, string> Parameters { get; set; }

        public JobProduced( string id, string origin, int priority, string serializedScript, Dictionary<string, string> parameters )
        {
            Id = id;
            Origin = origin;
            Priority = priority;
            SerializedScript = serializedScript;
            Parameters = parameters;
        }
    }
}
