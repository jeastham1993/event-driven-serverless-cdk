using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Constructs;

namespace EventDrivenCdk
{
    public class SharedResources : Construct
    {
        public EventBus CentralEventBus { get; private set; }
        public SharedResources(Construct scope, string id) : base(scope, id)
        {
            var centralEventBridge = new EventBus(this, "CentralEventBridge", new EventBusProps()
            {
                EventBusName = "CentralEventBus",
            });

            this.CentralEventBus = centralEventBridge;
        }
    }
}