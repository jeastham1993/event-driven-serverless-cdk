using Amazon.CDK;
using Constructs;
using EventDrivenCdk.CustomerContactService;
using EventDrivenCdk.EventAuditService;
using EventDrivenCdk.Frontend;
using EventDrivenCdk.NotificationService;
using EventDrivenCdk.ReviewAnalysisService;
using EventDrivenCdk.EventAuditService;
using Newtonsoft.Json.Linq;

namespace EventDrivenCdk
{
    public class EventDrivenCdkStack : Stack
    {
        internal EventDrivenCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var sharedStack = new SharedResources(this, "SharedResources");

            var api = new FrontendApiService(this, "ApiStack", new FrontendApiServiceProps()
            {
                CentralEventBridge = sharedStack.CentralEventBus
            });
            
            var sentimentAnalysis = new ReviewAnalysisService.ReviewAnalysisService(this, "SentimentAnalysis", new ReviewAnalysisServiceProps()
            {
                CentralEventBus = sharedStack.CentralEventBus
            });

            var eventAuditor = new EventAuditService.EventAuditService(this, "EventAuditService");

            var notificationService = new NotificationService.NotificationService(this, "NotificationService");

            var customerContactService = new CustomerContactService.CustomerContactService(this,
                "CustomerContactService");
        }
    }
}