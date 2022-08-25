# Azure Devops Agent Pool Migrator

This tool can be used to bulk edit release definitions and change the configured agent pools in all agent based phases.

When migrating Azure DevOps instances, for example from on-premise to the cloud, agent pools get different id's, so the existing configuration of the agent pools in release definitions (which are stored by id) become invalid.

The code can be executed in Visual Studio using a PAT token with sufficient rights to edit release definitions and list agent pools.

Instructions:

**Before migration** (on the old instance):
* Get all existing agent pools (using REST API or a script, also possible from the browser), see: https://docs.microsoft.com/en-us/rest/api/azure/devops/distributedtask/pools/get-agent-pools?view=azure-devops-rest-6.0 
* Add name and id of all agent pools to the mapping file `<project>_old_agent_pools.json` (rename the file). Each project has its own file.
* This file is used (after migration) to find the name of the agent pool using the id from the release definition. The name is then used to find the new id querying the new instance of Azure DevOps. 
 
**After migration** (on the new instance):

* Create PAT in Azure DevOps with scopes: 
  * Agent Pools (Read); Release (Read, write, execute, & manage). 
  * (Sometimes this doesn't work and Full Access is necessary)

* Open solution in Visual Studio 
* Open class Program.cs
  * Add PAT to constant
  * Edit collection and project constants
  * Add all root folders of the release definitions to edit to constant

* Run the application (Ctrl+F5)
* Logfile with trace is generated and shown when running is done 


