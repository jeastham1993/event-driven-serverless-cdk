using System.Collections.Generic;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Sagemaker;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using Newtonsoft.Json;
using EventBus = Amazon.CDK.AWS.Events.EventBus;

namespace EventDrivenCdk
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
                .When(Condition.StringEquals(JsonPath.StringAt("$.detail.type"), "positiveReview"), new CallAwsService(
                    this, "SendPositiveEmail", new CallAwsServiceProps()
                    {
                        Service = "ses",
                        Action = "sendEmail",
                        Parameters = new Dictionary<string, object>(2)
                        {
                            {
                                "Destination", new Dictionary<string, object>()
                                {
                                    {"ToAddresses", JsonPath.Array(JsonPath.StringAt("$.detail.emailAddress"))}
                                }
                            },
                            {"Source", "jamesuk@amazon.co.uk"},
                            {
                                "Message", new Dictionary<string, object>()
                                {
                                    {
                                        "Body", new Dictionary<string, object>()
                                        {
                                            {
                                                "Html", new Dictionary<string, object>()
                                                {
                                                    {"Charset", "UTF-8"},
                                                    {"Data", "<html><head></head><body><p>Thank you for your positive review</p></body></html>"}
                                                }
                                            },
                                            {
                                                "Text", new Dictionary<string, object>()
                                                {
                                                    {"Charset", "UTF-8"},
                                                    {"Data", "Thank you for your positive review."}
                                                }
                                            }
                                        }
                                    },
                                    {
                                        "Subject", new Dictionary<string, object>()
                                        {
                                            {"Data", "This is some test data"},
                                            {"Charset", "UTF-8"},
                                        }
                                    },
                                }
                            }
                        },
                        IamResources = new string[1] {"*"},
                    }));
            
            var stateMachine = new StateMachine(this, "NotificationServiceStateMachine", new StateMachineProps
            {
                Definition = choice,
                StateMachineType = StateMachineType.STANDARD
            });

            var rule = new Rule(this, "NotificationPositiveReviewRule", new RuleProps()
            {
                EventBus = props.CentralEventBus,
                RuleName = "NotificationPositiveReviewRule",
                EventPattern = new EventPattern()
                {
                    DetailType = new string[1] {"positive-review"},
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