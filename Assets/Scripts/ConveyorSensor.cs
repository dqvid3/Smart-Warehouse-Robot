using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;
using System.Collections.Generic;

public class ConveyorSensor : MonoBehaviour
{
    public Transform position;
    public GameObject piano;
    private Neo4jHelper neo4jHelper;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
    }

    void OnTriggerEnter(Collider other)
    {
            Vector3 detectedPosition = position.position;
            float x = detectedPosition.x;
            float z = detectedPosition.z;

            Debug.Log($"Box rilevato alla posizione: x = {x}, z = {z}");
            // Esegui la query per aggiornare hasParcel a true
            Task.Run(() => UpdatePositionInDatabase(x, z));

        var pianoScript = piano.GetComponent<ConveyorPhysic>();
            if (pianoScript != null)
            {
                // Aggiorna le variabili pubbliche dello script
                pianoScript.speed = 0;
                pianoScript.meshSpeed = 0;
            }
            else
            {
                Debug.LogError("Lo script 'ConveyorPhysic' non è stato trovato sul GameObject 'piano'.");
            }           

    }

    private async Task UpdatePositionInDatabase(float x, float z)
    {
        try
        {
            string query = @"
                MATCH (p:Position {x: $x, z: $z})
                SET p.hasParcel = true
                RETURN p";

            var parameters = new Dictionary<string, object>
            {
                { "x", x },
                { "z", z }
            };

            await neo4jHelper.ExecuteWriteAsync(query, parameters);
            Debug.Log($"Aggiornamento completato per la posizione: x={x}, z={z}");

        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Errore durante l'aggiornamento della posizione in Neo4j: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        // Chiudi la connessione con Neo4j
        neo4jHelper?.CloseConnection();
    }
}
