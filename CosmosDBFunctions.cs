using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FunctionApp1.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

/*

DISCLAIMER
=========

Notice: Any links, references, or attachments that contain sample scripts, code, or commands comes with the following notification.
This Sample Code is provided for the purpose of illustration only and is not intended to be used in a production environment.  THIS SAMPLE CODE AND ANY RELATED INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.  We grant You a nonexclusive, royalty-free right to use and modify the Sample Code and to reproduce and distribute the object code form of the Sample Code, provided that You agree: (i) to not use Our name, logo, or trademarks to market Your software product in which the Sample Code is embedded; (ii) to include a valid copyright notice on Your software product in which the Sample Code is embedded; and (iii) to indemnify, hold harmless, and defend Us and Our suppliers from and against any claims or lawsuits, including attorneys’ fees, that arise or result from the use or distribution of the Sample Code.
Please note: None of the conditions outlined in the disclaimer above will superseded the terms and conditions contained within the Premier Customer Services Description.

*/

namespace SIgnalRDemoFunctions
{
    public static class StocksChangedFunction
    {
        [FunctionName("stocksChanged")]
        public static void Run([CosmosDBTrigger(
            databaseName: "stocksdb",
            collectionName: "stocks",
            ConnectionStringSetting = "AzureCosmosDBConnectionString",
            LeaseCollectionName = "leases",  CreateLeaseCollectionIfNotExists = true)] IReadOnlyList<Document> input,
            [SignalR(HubName = "stocks", ConnectionStringSetting = "AzureSignalRConnectionString")] IAsyncCollector<SignalRMessage> stocksChanged,
            ILogger log)
        {
            if (input != null && input.Count > 0)
            {
                log.LogInformation("Documents modified " + input.Count);
                log.LogInformation("First document Id " + input[0].Id);
            }

            var message = input?.Select(doc => new Stock
            {
                Id = doc.GetPropertyValue<string>("id"),
                symbol = doc.GetPropertyValue<string>("symbol"),
                price = doc.GetPropertyValue<double>("price"),
                change = doc.GetPropertyValue<double>("change"),
                changeDirection = doc.GetPropertyValue<string>("changeDirection")
            }).ToList();

            stocksChanged.AddAsync(new SignalRMessage { Target = "updated", Arguments = new[] { input } });
        }
    }

    public static class NegotiateFunction
    {
        [FunctionName("negotiate")]
        public static SignalRConnectionInfo Negotiate(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "stocks")] SignalRConnectionInfo connectionInfo)
        {
            return connectionInfo;
        }
    }

    public static class GetStocksFunction
    {
        [FunctionName("getStocks")]
        public static async Task<IEnumerable<Stock>> Run(
            [HttpTrigger(AuthorizationLevel.Function, 
            "get", Route = "getStocks")] HttpRequest req, 
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function to get all data from Cosmos DB");

            IDocumentDBRepository<Stock> Respository = new DocumentDBRepository<Stock>();
            return await Respository.GetItemsAsync("stocks");
        }
    }

    public static class CreateOrUpdateFunction
    {
        [FunctionName("CreateOrUpdate")]
        public static async Task<bool> Run([HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "CreateOrUpdate")] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function to create a record into Cosmos DB");
            try
            {
                IDocumentDBRepository<Stock> Respository = new DocumentDBRepository<Stock>();
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var stock = JsonConvert.DeserializeObject<Stock>(requestBody);
                if (req.Method == "POST")
                {
                    stock.Id = null;
                    await Respository.CreateItemAsync(stock, "stocks");
                }
                else
                {
                    await Respository.UpdateItemAsync(stock.Id, stock, "stocks");
                }
                return true;
            }
            catch
            {
                log.LogError("Error occured while creating a record into Cosmos DB");
                return false;
            }
        }
    }

    public static class DeleteFunction
    {
        [FunctionName("Delete")]
        public static async Task<bool> Run([HttpTrigger(AuthorizationLevel.Function, "delete", Route = "Delete/{id}")] HttpRequest req, ILogger log, string id)
        {
            // -- needs to tested - is it good now?

            log.LogInformation("C# HTTP trigger function to delete a record from Cosmos DB");

            IDocumentDBRepository<Stock> Respository = new DocumentDBRepository<Stock>();
            try
            {
                await Respository.DeleteItemAsync(id, "stocks", id);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
