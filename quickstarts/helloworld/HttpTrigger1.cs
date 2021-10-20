
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Azure.Management.ContainerService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Rest;
using System.Collections;
using Microsoft.Azure.Management.RecoveryServices;
using Microsoft.Azure.Management.RecoveryServices.Models;
using Microsoft.Azure.Management.RecoveryServices.Backup;

namespace Company.Function
{
    public static class HttpTrigger1
    {

        private static readonly Lazy<TokenCredential> _msiCredential = new Lazy<TokenCredential>(() =>
        {
            // https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet
            // Using DefaultAzureCredential allows for local dev by setting environment variables for the current user, provided said user
            // has the necessary credentials to perform the operations the MSI of the Function app needs in order to do its work. Including
            // interactive credentials will allow browser-based login when developing locally.
            return new Azure.Identity.DefaultAzureCredential(includeInteractiveCredentials: true);
        });

        private static readonly Lazy<AzureCredentials> _azureCredentials = new Lazy<AzureCredentials>(() =>
        {
            // If we find tenant and subscription in environment variables, configure accordingly
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(@"WEBSITE_OWNER_NAME")))
            {
                var subId = Environment.GetEnvironmentVariable(@"WEBSITE_OWNER_NAME").Split("+")[0];
                var tenantId = "Dummy";

                var tokenCred = _msiCredential.Value;
                var armToken = tokenCred.GetToken(new TokenRequestContext(scopes: new[] { "https://management.azure.com/.default" }, parentRequestId: null), default).Token;
                var armCreds = new Microsoft.Rest.TokenCredentials(armToken);

                var graphToken = tokenCred.GetToken(new TokenRequestContext(scopes: new[] { "https://graph.windows.net/.default" }, parentRequestId: null), default).Token;
                var graphCreds = new Microsoft.Rest.TokenCredentials(graphToken);

                return new AzureCredentials(armCreds, graphCreds, tenantId, AzureEnvironment.AzureGlobalCloud);
            }
            else
            {
                return SdkContext.AzureCredentialsFactory
                    .FromSystemAssignedManagedServiceIdentity(MSIResourceType.AppService, AzureEnvironment.AzureGlobalCloud);
            }
        });


        private static readonly Lazy<IAzure> _legacyAzure = new Lazy<IAzure>(() =>
        {
             var credentials = _azureCredentials.Value;
             return Microsoft.Azure.Management.Fluent.Azure
                    .Authenticate(credentials)
                    .WithDefaultSubscription();
        });

        private static readonly Lazy<RecoveryServicesClient> _rsvClient = new Lazy<RecoveryServicesClient>(() =>  
        {
             var subId = Environment.GetEnvironmentVariable(@"WEBSITE_OWNER_NAME").Split("+")[0];
             var credentials = _azureCredentials.Value;
             return new RecoveryServicesClient(credentials) {
                 SubscriptionId =  subId
             };
        });

        [FunctionName("HttpTrigger1")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            /*foreach(DictionaryEntry e in System.Environment.GetEnvironmentVariables())
            {
                log.LogInformation(e.Key  + ":" + e.Value);
            }*/

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;
    
            /*var rgList = _legacyAzure.Value.ResourceGroups.List();
            if (rgList != null)
            {
                foreach(IResourceGroup rg in rgList)
                {
                    log.LogInformation(rg.Name);
                }
            }*/

            var mrg = Environment.GetEnvironmentVariable(@"WEBSITE_RESOURCE_GROUP");

            var location = Environment.GetEnvironmentVariable(@"REGION_NAME");

            var vaultName = "ManagedRSVault";
            
            Vault vault  = new Vault()
            {
                Sku = new Sku(SkuName.Standard),
                Location = location,
                Properties = new VaultProperties()
            };

            _rsvClient.Value.Vaults.CreateOrUpdate(mrg, vaultName, vault);

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}


