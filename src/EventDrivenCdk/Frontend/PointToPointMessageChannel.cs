namespace EventDrivenCdk.Frontend;

using System.Collections.Generic;

using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Pipes;
using Amazon.CDK.AWS.SQS;

using Constructs;

using EventDrivenCdk.SharedConstruct;

public record PointToPointMessageChannelProps(Queue Queue, DefaultStateMachine Orchestrator);

public class PointToPointMessageChannel : Construct
{
    public PointToPointMessageChannel(
        Construct scope,
        string id,
        PointToPointMessageChannelProps props) : base(
        scope,
        id)
    {
        var sourcePolicy = new PolicyDocument(
            new PolicyDocumentProps
            {
                Statements = new[]
                {
                    new PolicyStatement(
                        new PolicyStatementProps
                        {
                            Resources = new[] { props.Queue.QueueArn },
                            Actions = new[] { "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes" },
                            Effect = Effect.ALLOW
                        })
                }
            });

        var targetPolicy = new PolicyDocument(
            new PolicyDocumentProps
            {
                Statements = new[]
                {
                    new PolicyStatement(
                        new PolicyStatementProps
                        {
                            Resources = new[] { props.Orchestrator.StateMachineArn },
                            Actions = new[] { "states:StartExecution" },
                            Effect = Effect.ALLOW
                        })
                }
            });

        var pipeRole = new Role(
            this,
            "PipeRole",
            new RoleProps
            {
                AssumedBy = new ServicePrincipal("pipes.amazonaws.com"),
                InlinePolicies = new Dictionary<string, PolicyDocument>(2)
                {
                    { "SourcePolicy", sourcePolicy },
                    { "TargetPolicy", targetPolicy }
                }
            });

        var pipe = new CfnPipe(
            this,
            "Pipe",
            new CfnPipeProps
            {
                RoleArn = pipeRole.RoleArn,
                Source = props.Queue.QueueArn,
                SourceParameters = new CfnPipe.PipeSourceParametersProperty
                {
                    SqsQueueParameters = new CfnPipe.PipeSourceSqsQueueParametersProperty
                    {
                        BatchSize = 1,
                        MaximumBatchingWindowInSeconds = 5
                    }
                },
                Target = props.Orchestrator.StateMachineArn,
                TargetParameters = new CfnPipe.PipeTargetParametersProperty
                {
                    StepFunctionStateMachineParameters = new CfnPipe.PipeTargetStateMachineParametersProperty
                    {
                        InvocationType = "FIRE_AND_FORGET"
                    }
                }
            });
    }
}