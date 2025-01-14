using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class Neo4jHelper
{
    private readonly IDriver _driver;

    public Neo4jHelper(string uri, string user, string password)
    {
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
    }

    public void CloseConnection()
    {
        _driver?.Dispose();
    }

    public async Task<List<IRecord>> ExecuteReadListAsync(string query, Dictionary<string, object> parameters = null)
    {
        await using var session = _driver.AsyncSession();
        try
        {
            return await session.ExecuteReadAsync(async tx =>
            {
                var result = await tx.RunAsync(query, parameters);
                return await result.ToListAsync();
            });
        }
        catch (Exception ex)
        {
            throw new Neo4jException($"Error executing read query: {ex.Message}");
        }
    }

    public async Task<IRecord> ExecuteReadAsync(string query, Dictionary<string, object> parameters = null)
    {
        await using var session = _driver.AsyncSession();
        try
        {
            return await session.ExecuteReadAsync(async tx =>
            {
                var result = await tx.RunAsync(query, parameters);
                return await result.SingleAsync();
            });
        }
        catch (Exception ex)
        {
            throw new Neo4jException($"Error executing read query: {ex.Message}");
        }
    }

    public async Task ExecuteWriteAsync(string query, Dictionary<string, object> parameters = null)
    {
        await using var session = _driver.AsyncSession();
        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(query, parameters);
            });
        }
        catch (Exception ex)
        {
            throw new Neo4jException($"Error executing write query: {ex.Message}");
        }
    }

    public async Task<List<string>> GetProductsByCategoryAsync(string category)
    {
        try
        {
            var parameters = new Dictionary<string, object> { { "category", category } };
            return await ExecuteReadListAsync("MATCH (p:Product {category: $category}) RETURN p.product_name", parameters)
                .ContinueWith(task => task.Result.Select(record => record[0].As<string>()).ToList());
        }
        catch (Exception ex)
        {
            throw new Neo4jException($"Error retrieving products for category '{category}': {ex.Message}");
        }
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        try
        {
            string query = "MATCH (s:Shelf) WHERE s.category <> 'Backup' RETURN DISTINCT s.category AS category";
            return await ExecuteReadListAsync(query)
                .ContinueWith(task => task.Result.Select(record => record["category"].As<string>()).ToList());
        }
        catch (Exception ex)
        {
            throw new Neo4jException($"Error retrieving categories: {ex.Message}");
        }
    }

    public async Task UpdateParcelPositionStatusAsync(string positionId, bool hasParcel)
    {
        string query = @"
        MATCH (p:Position)
        WHERE p.id = $positionId
        SET p.hasParcel = $hasParcel";
        var parameters = new Dictionary<string, object> { { "positionId", positionId }, { "hasParcel", hasParcel } };
        await ExecuteWriteAsync(query, parameters);
    }

    public async Task<Vector3> GetBackupPosition()
    {
        string query = @"
        MATCH (p:Position)
        WHERE p.id = 'backup_0'
        RETURN p.x, p.z, p.y";

        var record = await ExecuteReadAsync(query);

        float x = record["p.x"].As<float>();
        float z = record["p.z"].As<float>();
        float y = record["p.y"].As<float>();
        return new Vector3(x, y, z);
    }

    public async Task DeleteParcel(string timestamp)
    {
        string query = @"
        MATCH (p:Parcel)
        WHERE p.timestamp = $timestamp
        DETACH DELETE p";

        var parameters = new Dictionary<string, object> { { "timestamp", timestamp } };
        await ExecuteWriteAsync(query, parameters);
    }
}