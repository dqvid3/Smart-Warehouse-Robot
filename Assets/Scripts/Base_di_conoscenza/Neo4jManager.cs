using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class Neo4jManager : MonoBehaviour
{
    private string url = "http://localhost:7474/db/neo4j/tx/commit"; 
    private string username = "neo4j"; 
    private string password = "12345678"; 

    void Start()
    {
        string cypherQuery = "{\"statements\": [{\"statement\": \"MATCH (n) RETURN n LIMIT 5\"}]}";
        StartCoroutine(SendCypherQuery(cypherQuery));
    }

    IEnumerator SendCypherQuery(string queryJson)
    {
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(queryJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        string auth = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
        request.SetRequestHeader("Authorization", "Basic " + auth);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Risposta da Neo4j: " + request.downloadHandler.text);
        }
        else
        {
            Debug.LogError("Errore: " + request.error);
        }
    }
}
