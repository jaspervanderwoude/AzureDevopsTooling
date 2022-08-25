namespace AzureDevopsAgentPoolMigrator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.TeamFoundation.DistributedTask.WebApi;
    using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;
    using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;
    using Newtonsoft.Json;

    public class AgentPoolMigrator
    {
        private const string comment = "All agent pools linked in this release definition updated with new correct id";

        private readonly ClientManager clients;

        private int releaseDefsToMigrate = 0;
        private readonly List<string> failedReleaseDefs = new List<string>();

        public AgentPoolMigrator(ClientManager clients)
        {
            this.clients = clients;
        }

        public async Task Migrate(string collection, string project, string[] rootFoldersToInclude)
        {
            Program.Log($"Start migrating agent pools in collection: '{collection}' and project: '{project}'");
            Program.Log($"Scope: all release definitions in root folder(s): {string.Join(", ", rootFoldersToInclude)}");

            var taskgroups = await clients.AgentClient.GetTaskGroupsAsync(project);

            var oldAgentMapping = LoadOldAgentPoolMapping(project);
            var queues = await GetCurrentAgentQueues(project);
            var currentDefinition = 0;

            var releaseDefinitions = await GetRelevantReleaseDefinitions(project, rootFoldersToInclude);
            var amountOfDefinitions = releaseDefinitions.Count();

            foreach (var item in releaseDefinitions)
            {
                currentDefinition++;
                Console.WriteLine($"Definition: {currentDefinition} / {amountOfDefinitions}");

                // Get full release definition
                var definition = await clients.ReleaseClient.GetReleaseDefinitionAsync(project, item.Id);
                Program.Log($"Release definition: '{definition.Name}' in folder: {definition.Path}");

                try
                {
                    bool changesMade = false;
                    // Loop all environments / stages to find all agent phases
                    foreach (var environment in definition.Environments)
                    {
                        Program.Log($"Environment/stage: {environment.Name}");

                        // Loop all agent phases to find all agent pools
                        // Only AgentBasedDeployments are relevant, RunOnServer and other types do not uses agent pools
                        var agentBasedPhases = environment.DeployPhases.Where(x => x.PhaseType == DeployPhaseTypes.AgentBasedDeployment);
                        foreach (var agentPhase in agentBasedPhases)
                        {
                            var deploymentInput = (DeploymentInput)agentPhase.GetDeploymentInput();
                            var poolName = oldAgentMapping.FirstOrDefault(x => x.Id == deploymentInput.QueueId)?.Name;
                            if (string.IsNullOrEmpty(poolName))
                            {
                                Program.Log($"Agent pool/queue met Id {deploymentInput.QueueId} niet gevonden in json");
                            }

                            // Edit the QueueId (pools are called queues in the API) to the new correct version
                            var queue = queues.FirstOrDefault(x => x.Name == poolName);
                            if (queue == null)
                            {
                                Program.Log($"Agent pool/queue '{poolName}' not found in Azure Devops");
                            }

                            var newId = queue.Id;
                            Program.Log($"Agent pool: '{poolName} ({deploymentInput.QueueId})' edited to '{poolName} ({newId})'");
                            deploymentInput.QueueId = newId;
                            changesMade = true;
                        }
                    }

                    if (changesMade)
                    {
                        // Execute the update
                        // Add comment to release definition for traceability and save
                        definition.Comment = comment;
                        await clients.ReleaseClient.UpdateReleaseDefinitionAsync(definition, project);

                        Program.Log($"Release definition: '{definition.Name}' is updated successfully and saved with comment");
                    }
                    else
                    {
                        Program.Log($"No agent pools to edit found. No update performed");
                    }
                }
                catch (Exception e)
                {
                    Program.Log($"Error editing release definition: '{definition.Name}'. Error: {e}");
                    failedReleaseDefs.Add(definition.Name);
                }

                Program.Log(string.Empty, includeTimestamp: false);
                Program.Log(string.Empty, includeTimestamp: false);
            }
            Program.Log($"Successfully updated release definitions: {releaseDefsToMigrate - failedReleaseDefs.Count}. Failed: {failedReleaseDefs.Count}");
            Program.Log($"Failed release definitions:\r\n{string.Join("\r\n", failedReleaseDefs)}");
        }

        private IList<AgentPool> LoadOldAgentPoolMapping(string project)
        {
            string file = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @$"OldAgentPoolMappings\{project}_old_agent_pools.json");
            var mapping = JsonConvert.DeserializeObject<List<AgentPool>>(File.ReadAllText(file, Encoding.UTF8));
            Program.Log($"Old agent pool mapping read from file: {file}");
            return mapping;
        }

        private async Task<List<TaskAgentQueue>> GetCurrentAgentQueues(string project)
        {
            // Agent pools are not (directly) linked to release definitions as the GUI suggests
            // Instead agent queues are linked to release definitions (which do have a 1-1 relationship with agent pools)
            List<TaskAgentQueue> queues;
            try
            {
                queues = await clients.AgentClient.GetAgentQueuesAsync(project: project);
            }
            catch (Exception e)
            {
                var errorMsg = "Connecting to Azure Devops or retrieving agent queues failed.";
                Program.Log($"{errorMsg} Error: {e}");
                throw new Exception(errorMsg, e);
            }
            return queues;
        }

        private async Task<List<ReleaseDefinition>> GetRelevantReleaseDefinitions(string project, string[] rootFoldersToInclude)
        {
            // Get all release definitions
            var allDefinitions = await clients.ReleaseClient.GetReleaseDefinitionsAsync(project);

            // Filter release definitions by their rootfolder(s)
            List<ReleaseDefinition> filteredDefinitions;
            try
            {
                filteredDefinitions = allDefinitions.Where(def => rootFoldersToInclude.Any(folder => def.Path.GetRootFolder().ToLower() == folder.ToLower())).ToList();

                releaseDefsToMigrate = filteredDefinitions.Count;
                Program.Log($"Amount of release definitions to migrate: {releaseDefsToMigrate}");
            }
            catch (Exception e)
            {
                var errorMsg = $"Error filtering release definitions by rootfolder(s): {string.Join(", ", rootFoldersToInclude)}";
                Program.Log($"{errorMsg} Error: {e}");
                throw new Exception(errorMsg, e);
            }

            return filteredDefinitions;
        }
    }
}