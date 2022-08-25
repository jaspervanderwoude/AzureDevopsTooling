namespace AzureDevopsAgentPoolMigrator
{
    using System;
    using Microsoft.TeamFoundation.DistributedTask.WebApi;
    using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
    using Microsoft.VisualStudio.Services.Common;
    using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Clients;
    using Microsoft.VisualStudio.Services.WebApi;

    public class ClientManager
    {
        public TaskAgentHttpClient AgentClient { get; private set; }

        public ReleaseHttpClient2 ReleaseClient { get; private set; }

        public WorkItemTrackingHttpClient WorkitemClient { get; private set; }

        public ClientManager()
        {
        }

        public void ConfigureConnectionAndClients(string azureDevopsUri, string collection, string project, string pat = null)
        {
            var url = $"{azureDevopsUri}/{collection}";

            VssConnection connection;
            connection = new VssConnection(new Uri(url), new VssBasicCredential(string.Empty, pat));

            Program.Log($"Connecting to collection: {url} and project: {project}");

            this.AgentClient = connection.GetClient<TaskAgentHttpClient>();
            this.ReleaseClient = connection.GetClient<ReleaseHttpClient2>();
            this.WorkitemClient = connection.GetClient<WorkItemTrackingHttpClient>();
        }
    }
}