using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.StepFunctions;
using Constructs;
using EventDrivenCdk.SharedConstruct;
using EventBus = Amazon.CDK.AWS.Events.EventBus;

namespace EventDrivenCdk.CustomerContactService
{
    public class CustomerContactService : Construct
    {
        public CustomerContactService(Construct scope, string id) : base(scope, id)
        {
            // Create the customer contact workflow.
            var stateMachine = new DefaultStateMachine(this, "CustomerContactWorkflow", WorkflowStep.NotifyBadReview(this)
                .Next(WorkflowStep.WaitForCustomerAgentClaim(this))
                .Next(WorkflowStep.StoreCustomerServiceClaim(this)), StateMachineType.STANDARD);

            CentralEventBus.AddRule(this, "NegativeReviewRule", "event-driven-cdk.sentiment-analysis",
                "negative-review", stateMachine);
        }
    }
}