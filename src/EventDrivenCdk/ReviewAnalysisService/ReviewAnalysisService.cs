using System.Collections.Generic;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using EventDrivenCdk.SharedConstruct;
using EventBus = Amazon.CDK.AWS.Events.EventBus;
using LogGroupProps = Amazon.CDK.AWS.Events.Targets.LogGroupProps;

namespace EventDrivenCdk.ReviewAnalysisService
{
    public class ReviewAnalysisServiceProps
    {
        public EventBus CentralEventBus { get; set; }
    }

    public class ReviewAnalysisService : Construct
    {
        public ReviewAnalysisService(Construct scope, string id, ReviewAnalysisServiceProps props) :
            base(scope, id)
        {
            var reviewAnalysisOrchestrator = new ReviewAnalysisOrchestrator(
                this, 
                "ReviewAnalysisOrchestrator",
                new ReviewAnalysisOrchestratorProps());

            // Add rule to event bus.
            MessageBus.SubscribeTo(this, 
                ruleName: "TriggerSentimentAnalysisRule",
                eventSource: "event-driven-cdk.api", 
                eventType: "newReview",
                target: reviewAnalysisOrchestrator.Orchestrator);
        }
    }
}