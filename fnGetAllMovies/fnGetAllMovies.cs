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

namespace fnGetAllMovies;

public class fnGetAllMovies
{
    private readonly ILogger<fnGetAllMovies> _logger;
    private readonly CosmosClient _cosmosClient;

    public fnGetAllMovies(ILogger<fnGetAllMovies> logger, CosmosClient cosmosClient)
    {
        _logger = logger;
        _cosmosClient = cosmosClient;
    }

    [Function("AllMovies")]
    public async Task<HttpResponseData>Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
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

        try
        {
            var sql = "SELECT * FROM c";
            var queryDefinition = new QueryDefinition(sql);
            var result = container.GetItemQueryIterator<MovieResult>(queryDefinition);
            var results = new List<MovieResult>();

            while (result.HasMoreResults)
            {
                foreach (var item in await result.ReadNextAsync())
                {
                    results.Add(item);
                }

                var responseMessage = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await responseMessage.WriteAsJsonAsync(results);

                return responseMessage;
            }
        }
        catch (CosmosException cex)
        {
            _logger.LogError(cex, "Erro no Cosmos DB ao consultar lista de filmes");
            var errResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errResp.WriteStringAsync("Ocorreu um erro no Cosmos DB.");
            return errResp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção não tratada na função 'AllMovies'");
            var errResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errResp.WriteStringAsync("Ocorreu um erro no servidor.");
            return errResp;
        }

        // Retorno padrão caso nenhum dos caminhos acima seja executado
        var defaultResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
        await defaultResp.WriteStringAsync("Nenhuma resposta foi gerada pela função.");
        return defaultResp;
    }
}