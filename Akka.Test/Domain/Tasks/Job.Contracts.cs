using System.Collections.Generic;
using Akka.Test.DDD.Infrastructure.Event;

namespace Akka.Test.Domain.Tasks
{
    public sealed partial class Job
    {
        public sealed class ProduceJob : Command
        {
            public string Origin { get; }
            public int Priority { get; }
            public string SerializedScript { get; }
            public Dictionary<string, string> Parameters { get; }

            public ProduceJob( string targetId, string origin, int priority, string serializedScript, Dictionary<string, string> parameters ) : base(targetId)
            {
                Origin = origin;
                Priority = priority;
                SerializedScript = serializedScript;
                Parameters = parameters;
            }
        }

        public sealed class FailJob: Command
        {
            public string StatusText { get; }
            public IReadOnlyList<string> Notes { get; }

            public FailJob( string targetId, string statusText, IReadOnlyList<string> notes ): base(targetId)
            {
                StatusText = statusText;
                Notes = notes;
            }
        }

        public sealed class FinishScriptStep: Command
        {
            public string Status { get; }
            public string StatusText { get; }
            public IReadOnlyList<string> Notes { get; }
            public double Progress { get; }

            public FinishScriptStep( string targetId, string status, string statusText, IReadOnlyList<string> notes, double progress ): base(targetId)
            {
                Status = status;
                StatusText = statusText;
                Notes = notes;
                Progress = progress;
            }
        }

        public class JobFailed : DomainEvent
        {
            public string JobId { get; }
            public string StatusText { get; }
            public IReadOnlyList<string> Notes { get; }

            public JobFailed( string jobId, string statusText, IReadOnlyList<string> notes )
            {
                JobId = jobId;
                StatusText = statusText;
                Notes = notes;
            }
        }

        public sealed class JobProduced : DomainEvent
        {
            public string JobId { get; }
            public string Origin { get; }
            public int Priority { get; }
            public string SerializedScript { get; }
            public Dictionary<string, string> Parameters { get; }

            public JobProduced( string jobId, string origin, int priority, string serializedScript, Dictionary<string, string> parameters )
            {
                JobId = jobId;
                Origin = origin;
                Priority = priority;
                SerializedScript = serializedScript;
                Parameters = parameters;
            }
        }

        public sealed class JobSucceeded : DomainEvent
        {
            public string JobId { get; }
            public string StatusText { get; }
            public IReadOnlyList<string> Notes { get; }

            public JobSucceeded( string jobId, string statusText, IReadOnlyList<string> notes )
            {
                JobId = jobId;
                StatusText = statusText;
                Notes = notes;
            }
        }

        public sealed class JobTerminated : DomainEvent
        {
            public string JobId { get; }
            public string StatusText { get; }
            public IReadOnlyList<string> Notes { get; }

            public JobTerminated( string jobId, string statusText, IReadOnlyList<string> notes )
            {
                JobId = jobId;
                StatusText = statusText;
                Notes = notes;
            }
        }

        public sealed class NextScriptStepStarted : DomainEvent
        {
        }

        public sealed class ScriptStepFinished : DomainEvent
        {
            public string JobId { get; }
            public string StatusText { get; }
            public IReadOnlyList<string> Notes { get; }
            public double Progress { get; }

            public ScriptStepFinished( string jobId, string statusText, IReadOnlyList<string> notes, double progress )
            {
                JobId = jobId;
                StatusText = statusText;
                Notes = notes;
                Progress = progress;
            }
        }
    }
}
