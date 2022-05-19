﻿using System.Collections.Generic;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;

namespace EventDrivenCdk.NotificationService
{
    public class SendEmailProps
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }
    
    public static class WorkflowStep
    {
        public static IChainable SendEmail(Construct scope, string id, SendEmailProps props)
        {
            return new CallAwsService(
                scope, id,
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
                        {"Source", props.To},
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