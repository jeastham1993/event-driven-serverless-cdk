﻿using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using EventDrivenCdk.SharedConstruct;

namespace EventDrivenCdk.Frontend
{
    public class FrontendApiServiceProps
    {
        public EventBus CentralEventBridge { get; set; }
    }

    public class FrontendApiService : Construct
    {
        public FrontendApiService(Construct scope, string id, FrontendApiServiceProps props) : base(scope, id)
        {
            // Define the table to support the storage first API pattern.
            var apiTable = new Table(scope, "StorageFirstInput", new TableProps()
            {
                TableName = "EventDrivenCDKApiStore",
                PartitionKey = new Attribute()
                {
                    Name = "PK",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            });

            // Define the business workflow to integrate with the HTTP request, generate the case id
            // store and publish.
            var stateMachine = new DefaultStateMachine(this, "ApiStateMachine", WorkflowStep
                .GenerateCaseId(this, apiTable)
                .Next(WorkflowStep.StoreApiData(this, apiTable))
                .Next(WorkflowStep.PublishNewApiRequestEvent(this, props.CentralEventBridge))
                .Next(WorkflowStep.FormatStateForHttpResponse(this)), StateMachineType.EXPRESS);
            
            stateMachine.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Actions = new string[1] {"dynamodb:PutItem"},
                Resources = new string[1] {apiTable.TableArn}
            }));
            stateMachine.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Actions = new string[1] {"events:PutEvents"},
                Resources = new string[1] {props.CentralEventBridge.EventBusArn}
            }));

            // Create the API
            var api = new StepFunctionsRestApi(this, "StepFunctionsRestApi", new StepFunctionsRestApiProps
            {
                StateMachine = stateMachine,
            });
        }
    }
}