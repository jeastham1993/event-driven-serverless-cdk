using System.Collections.Generic;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;

namespace EventDrivenCdk
{
    public class SendEmailProps
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string UniqueIdentifier { get; set; }
    }
    
    public static class WorkflowManager
    {
        public static CallAwsService SendEmail(Construct scope, string id, SendEmailProps props)
        {
            return new CallAwsService(
                scope, props.UniqueIdentifier,
                new CallAwsServiceProps()
                {
                    Service = "ses",
                    Action = "sendEmail",
                    Parameters = new Dictionary<string, object>(2)
                    {
                        {
                            "Destination", new Dictionary<string, object>()
                            {
                                {"ToAddresses", JsonPath.Array(props.To)}
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
                                                {
                                                    "Data",
                                                    $"<html><head></head><body><p>{props.Body}</p></body></html>"
                                                }
                                            }
                                        },
                                        {
                                            "Text", new Dictionary<string, object>()
                                            {
                                                {"Charset", "UTF-8"},
                                                {"Data", props.Body}
                                            }
                                        }
                                    }
                                },
                                {
                                    "Subject", new Dictionary<string, object>()
                                    {
                                        {"Data", props.Subject},
                                        {"Charset", "UTF-8"},
                                    }
                                },
                            }
                        }
                    },
                    IamResources = new string[1] {"*"},
                });
        }
    }
}