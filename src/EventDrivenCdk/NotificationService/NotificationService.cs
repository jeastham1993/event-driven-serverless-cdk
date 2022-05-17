using System.Collections.Generic;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Sagemaker;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using Newtonsoft.Json;
using EventBus = Amazon.CDK.AWS.Events.EventBus;

namespace EventDrivenCdk.NotificationService
{
    public class NotificationServiceProps
    {
        public EventBus CentralEventBus { get; set; }    
    }
    
    public class NotificationService : Construct
    {
        public NotificationService(Construct scope, string id, NotificationServiceProps props) : base(scope, id)
        {
            var choice = new Choice(this, "EventTypeChoice")
                .When(Condition.StringEquals(JsonPath.StringAt("$.detail.type"), "positiveReview"),
                    WorkflowStep.SendEmail(this, "SendPositiveEmail", new SendEmailProps()
                    {
                        To = JsonPath.StringAt("$.detail.emailAddress"),
                        Subject = "Thankyou for your review",
                        Body = "Thankyou for your positive review",
                    }))
                .When(Condition.StringEquals(JsonPath.StringAt("$.detail.type"), "negativeReview"),
                    WorkflowStep.SendEmail(this, "SendNegativeEmail", new SendEmailProps()
                    {
                        To = JsonPath.StringAt("$.detail.emailAddress"),
                        Subject = "Sorry!",
                        Body =
                            "I'm sorry our product didn't meet your satisfaction. One of our customer service agents will be in touch shortly",
                    }));
            
            var stateMachine = new StateMachine(this, "NotificationServiceStateMachine", new StateMachineProps
            {
                Definition = choice,
                StateMachineType = StateMachineType.STANDARD
            });

            var rule = new Rule(this, "NotificationRule", new RuleProps()
            {
                EventBus = props.CentralEventBus,
                RuleName = "NotificationRule",
                EventPattern = new EventPattern()
                {
                    DetailType = new string[2] {"positive-review", "negative-review"},
                    Source = new string[1] {"event-driven-cdk.sentiment-analysis"},
                },
                Targets = new IRuleTarget[1]
                {
                    new SfnStateMachine(stateMachine)
                }
            });
            
        }
    }
}