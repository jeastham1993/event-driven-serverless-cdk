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
            var stateMachine = new DefaultStateMachine(this, "CustomerContactWorkflow", 
            // First notify agents that a bad review has been entered
                WorkflowStep.NotifyBadReview(this)
                // Then send to a queue waiting for a customer service agent to claim
                .Next(WorkflowStep.WaitForCustomerAgentClaim(this))
                // Then store the customer service agent claim in a database
                .Next(WorkflowStep.StoreCustomerServiceClaim(this)), StateMachineType.STANDARD);

            CentralEventBus.AddRule(this, "NegativeReviewRule", "event-driven-cdk.sentiment-analysis",
                "negative-review", stateMachine);
        }
    }
}