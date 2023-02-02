namespace EventDrivenCdk.Frontend
{
    using Amazon.CDK;
    using Amazon.CDK.AWS.Events;
    using Amazon.CDK.AWS.SQS;

    using Constructs;

    public class FrontendApiServiceProps
    {
        public EventBus CentralEventBridge { get; set; }
    }

    public class FrontendApiService : Construct
    {
        public FrontendApiService(
            Construct scope,
            string id,
            FrontendApiServiceProps props) : base(
            scope,
            id)
        {
            var dataStore = new DataStorage(
                this,
                "DataStore",
                new DataStorageProps("ApiStore"));

            var queue = new Queue(
                this,
                "FrontendQueue",
                new QueueProps
                {
                    Encryption = QueueEncryption.UNENCRYPTED
                });

            var storageFirstApi = new StorageFirstApi(
                this,
                "FrontendApi",
                new StorageFirstApiProps(queue));

            var orchestrator = new InboundRequestOrchestrator(
                this,
                "InboundRequestOrchestrator",
                new InboundRequestOrchestratorProps(
                    dataStore.Table,
                    props.CentralEventBridge));

            var messageChannel = new PointToPointMessageChannel(
                this,
                "QueueToWorkflowMessageChannel",
                new PointToPointMessageChannelProps(
                    queue,
                    orchestrator.Orchestrator));

            var output = new CfnOutput(
                this,
                "ApiEndpoint",
                new CfnOutputProps
                {
                    ExportName = "APIEndpoint",
                    Description = "The endpoint for the created API",
                    Value = storageFirstApi.Api.Url
                });
        }
    }
}