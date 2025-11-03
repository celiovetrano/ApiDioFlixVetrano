using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace fnGetMovieDetail;

public class fnGetMovieDetail
{
    private readonly ILogger<fnGetMovieDetail> _logger;
    private readonly CosmosClient _cosmosClient;

    public fnGetMovieDetail(ILogger<fnGetMovieDetail> logger, CosmosClient cosmosClient)
    {
        _logger = logger;
        _cosmosClient = cosmosClient;
    }

    [Function("detail")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
    _logger.LogInformation("Função HTTP C# processou uma requisição.");

        // Ler nomes do Database/Container das variáveis de ambiente (não usar literais "%...%")
        var databaseName = Environment.GetEnvironmentVariable("DatabaseName");
        var containerName = Environment.GetEnvironmentVariable("ContainerName");

        if (string.IsNullOrWhiteSpace(databaseName) || string.IsNullOrWhiteSpace(containerName))
        {
            _logger.LogError("As variáveis de ambiente DatabaseName ou ContainerName não estão configuradas.");
            var errResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errResp.WriteStringAsync("Erro na configuração do servidor: DatabaseName/ContainerName ausentes.");
            return errResp;
        }

        var container = _cosmosClient.GetContainer(databaseName, containerName);

        // Extrair query string 'id' de HttpRequestData (req.Query não existe no modelo isolado)
    string? id = null;
        var query = req.Url?.Query; // ex: ?id=123
        if (!string.IsNullOrEmpty(query))
        {
            var qs = query.StartsWith("?") ? query.Substring(1) : query;
            var pairs = qs.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in pairs)
            {
                var kv = p.Split('=', 2);
                if (kv.Length == 2 && Uri.UnescapeDataString(kv[0]) == "id")
                {
                    id = Uri.UnescapeDataString(kv[1]);
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            var badResp = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResp.WriteStringAsync("Por favor, forneça o parâmetro de query 'id'.");
            return badResp;
        }

        try
        {
            var sql = "SELECT * FROM c WHERE c.id = @id";
            var queryDefinition = new QueryDefinition(sql).WithParameter("@id", id);
            var iterator = container.GetItemQueryIterator<MovieResult>(queryDefinition);
            MovieResult? found = null;

            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                found = page.FirstOrDefault();
                if (found != null) break;
            }

            if (found == null)
            {
                var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Movie with id '{id}' not found.");
                return notFound;
            }

            var responseMessage = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await responseMessage.WriteAsJsonAsync(found);
            return responseMessage;
        }
        catch (CosmosException cex)
        {
            _logger.LogError(cex, "Erro no Cosmos DB ao consultar filme id={Id}", id);
            var errResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errResp.WriteStringAsync("Ocorreu um erro no Cosmos DB.");
            return errResp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção não tratada na função 'detail'");
            var errResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errResp.WriteStringAsync("Ocorreu um erro no servidor.");
            return errResp;
        }
    }
}