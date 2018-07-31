using System;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Test.DDD.Infrastructure;

namespace Akka.Test.Domain.Tasks
{

    public abstract class Command
    {
        protected Command( string targetId )
        {
            TargetId = targetId;
        }

        public string TargetId { get; }
    }


    public sealed class MessageExtractor: HashCodeMessageExtractor
    {
        public override string EntityId( object message ) => (message as Command)?.TargetId;
        public MessageExtractor() : base( 16 )
        {
        }
    }
}