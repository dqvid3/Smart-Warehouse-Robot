/*
--> Gestione del cerchio di raggi raycast 

Ho dei raggi raycast a 360 gradi 

questi raggi devono essere categorizzati in base al nome dell'angolo a cui corrispondono 

la dir_princ del mio robot di default la indichiamo corrispondente con il raggio posto a a 90 gradi ma è un qualcosa che varierà 

quindi mi troverò in una situazione in cui ho 

il parametro dist_raycast che è la lunghezza dei raggi del raycast 

il paramestro soglia è il valore al di sotto del quale i raggi del raycast diventano sotto soglia e quindi entrano nella logica di previsione della collisione 

lista_raggi_sopra_soglia[] e una lista_raggi_sotto_soglia[] 


int fase di avvio sarò quindi nella situazione lista_raggi_sopra_soglia[1.....360] e dir_princ = 90 

al momento in cui il mio robot incontra un ostacolo nella dir_princ mi troverò nella situazione 

lista_raggi_sopra_soglia[0...80,100....360] e lista_raggi_sotto_soglia[[81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99]] 

il che significa che i raggi sotto soglia sono quelli che vanno da 81 a 99 e quindi ho un ostacolo che prende un ampiezza di 20 gradi 

a questo punto interviene l'algoritmo di decisione della direzione che fa la seguente cosa 

prende la lista_raggi_sotto_soglia[] e mi crea due sotto array dir_sx=[] e dir_dx=[] gli array si compongono così a pratire dalla dir_princ 
prendo il suo opposto nella criconferenza in questo caso 270 e su dir_sx metto da 100 che è il primo raggio non compromesso a sx fino a 270 e poi su dir_dx
metto da 80 fino a 270 a ritroso che è la direzione opposta a dir_sx quindi in questo momento avrò:

dir_sx[100,101,....270] e dir_dx[80,79,78,77,...,0,359,358,357,....,270]
dir_princ = 90 e dir_opp = 270 

ora dopo che ho composto questi vedo quale dei due array è più corto se è uguale scelgo a random e nel caso in cui adesempio dir_dx sia più corto cambio 
dir_princ nel valore pari all'elemento di valore grad_sterzata dell' array quindi se grad_sterzata è 20 allora la mia dir_princ adesso sarà 60 perchè
nel caso di dir_dx è un array che va a ritroso se sceglievo dir_sx sarebbe stato 120 conseguentemente aggiorno anche dir_opp nel caso di 60 a 240 nel caso di 120 a 300
comunque di +180 rispetto alla dir_princ. 

Questo è il caso in cui ho un solo ostacolo ora descriviamo nel caso di ostacoli multipli 

Mettiamo caso di avere due ostacoli e dir_princ = 90 gli ostacoli si trovano il primo tra 100 e 140 e il secondo tra 40 e 0
avremmo una situazione del genere 
dir_princ = 90 
dir_opp = 270
lista_raggi_sopra_soglia[41...99,141...360] e lista_raggi_sotto_soglia[[0,...,40],[100,...,140]] 
da notare che i sotto soglia sono raggruppatti per adiacenza in sotto array nel caso di tre ostacoli sarebbe stato lista_raggi_sotto_soglia[[0,...,40],[100,...,140],[235]]  
ma continueremo l'esempio con due 

detto ciò cosa si fa per prima cosa vedo se tra i sotto array sotto soglia ho una differenza pari a grad_sterzata quindi in questo caso se grad_sterzata è 20 
da 40 a 100 ci rientra allora cambio dir_princ nel punto medio tra 40 e 100 quindi 70 e dir_opp in 250

se siamo nel caso in cui tra questi due non ho una differenza bastevole per esempio se ho ostacoli uno tra 60 e 80 e uno tra 100 e 120 cosa faccio
allora scriviamo il nostro array di lista_raggi_sotto_soglia in questo caso questo sarà così composto lista_raggi_sotto_soglia[[60,...,80],[100,...,120]]
e di conseguenza quello di sopra soglia 
allora che faccio visto che la verifica su grad_sterzata non va a buon fine 
compongo nuovamente gli array di dir_sx e dir_dx ma come questa volta, prendo il primo elemento del primo ostacolo e l'ultimo del secondo quindi in questo caso 
60 e 120 da questi punti compongo dir_sx e dir_dx che saranno così fatti 
dir_sx[120,101,....270] e dir_dx[60,79,78,77,...,0,359,358,357,....,270] scelgo quello più corto e opero con grad_sterzata come sopra enunciato 
e aggiorno i parametri 

Nel caso sfortunato di una lunghezza di lista_raggi_sotto_soglia maggiore di 2 faccio una ricerca dentro a lista_raggi_sopra_soglia, che ovviamente si decrementa
dei raggi sotto soglia, faccio la ricerca di un numero di raggi consequenziali pari a grad_sterzata e se per esempio ho 4 ostacoli e 
ho libero solo da 300 a 320 imposto dir_princ a 310 e proseguo 

dopo che il robot ha compiuto il giro su se stesso e si è piazzato lungo la nuova direzione resetto di nuovo dir_princ a 90 in maniera da proseguire reiteratamente 

*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RaycastManager : MonoBehaviour
{
    private int dirPrinc = 90; // Direzione principale iniziale
    private int dirOpp => (dirPrinc + 180) % 360; // Direzione opposta
    private const int gradSterzata = 20; // Gradi di sterzata

    public int DistRaycast { get; set; } = 10; // Lunghezza dei raggi del raycast
    public int Soglia { get; set; } = 5; // Valore soglia per i raggi

    private List<int> raggiSopraSoglia = new List<int>(Enumerable.Range(0, 360));
    private List<int> raggiSottoSoglia = new List<int>();
    private Dictionary<int, int> raycastDistances = new Dictionary<int, int>();

    // Metodo per aggiornare i dati del raycast
    public void UpdateRaycastData(Dictionary<int, int> raycastDistances)
    {
        raggiSopraSoglia.Clear();
        raggiSottoSoglia.Clear();

        foreach (var kvp in raycastDistances)
        {
            if (kvp.Value >= Soglia)
                raggiSopraSoglia.Add(kvp.Key);
            else
                raggiSottoSoglia.Add(kvp.Key);
        }

        Debug.Log("Dati del raycast aggiornati:");
        Debug.Log($"Raggi sopra soglia: {string.Join(", ", raggiSopraSoglia)}");
        Debug.Log($"Raggi sotto soglia: {string.Join(", ", raggiSottoSoglia)}");
    }

    // Metodo per raggruppare raggi consecutivi
    private List<List<int>> GroupConsecutiveRays(List<int> rays)
    {
        var grouped = new List<List<int>>();
        var currentGroup = new List<int>();

        foreach (var ray in rays.OrderBy(x => x))
        {
            if (currentGroup.Count == 0 || ray == currentGroup.Last() + 1)
            {
                currentGroup.Add(ray);
            }
            else
            {
                grouped.Add(currentGroup);
                currentGroup = new List<int> { ray };
            }
        }

        if (currentGroup.Count > 0)
        {
            grouped.Add(currentGroup);
        }

        return grouped;
    }

    // Metodo per comporre gli array di direzione sx e dx
    private (List<int> dirSx, List<int> dirDx) ComposeDirectionArrays(int start, int end)
    {
        var dirSx = new List<int>();
        var dirDx = new List<int>();

        for (int i = start; i != end; i = (i + 1) % 360)
            dirSx.Add(i);

        for (int i = start; i != end; i = (i - 1 + 360) % 360)
            dirDx.Add(i);

        dirDx.Reverse();
        return (dirSx, dirDx);
    }

    // Metodo per calcolare la nuova direzione in base all'array di direzione
    private int CalculateNewDirection(List<int> directionArray)
    {
        int index = gradSterzata / 2;
        return directionArray[Math.Min(index, directionArray.Count - 1)];
    }

    // Metodo per aggiornare la direzione principale
    private void UpdateDirection(List<int> dirSx, List<int> dirDx)
    {
        if (dirSx.Count < dirDx.Count || (dirSx.Count == dirDx.Count && UnityEngine.Random.Range(0, 2) == 0))
        {
            dirPrinc = CalculateNewDirection(dirSx);
        }
        else
        {
            dirPrinc = CalculateNewDirection(dirDx);
        }
    }

    // Metodo per decidere la nuova direzione
    public void DecideDirection()
    {
        var groupedObstacles = GroupConsecutiveRays(raggiSottoSoglia);

        if (groupedObstacles.Count == 1)
        {
            var obstacle = groupedObstacles[0];
            var (dirSx, dirDx) = ComposeDirectionArrays(obstacle.Last() + 1, obstacle.First());
            UpdateDirection(dirSx, dirDx);
        }
        else if (groupedObstacles.Count == 2)
        {
            var gap = (groupedObstacles[1].First() - groupedObstacles[0].Last() + 360) % 360;

            if (gap >= gradSterzata)
            {
                dirPrinc = (groupedObstacles[0].Last() + gap / 2) % 360;
            }
            else
            {
                var (dirSx, dirDx) = ComposeDirectionArrays(groupedObstacles[1].Last() + 1, groupedObstacles[0].First());
                UpdateDirection(dirSx, dirDx);
            }
        }
        else
        {
            foreach (var range in raggiSopraSoglia)
            {
                var consecutiveRays = GroupConsecutiveRays(raggiSopraSoglia).FirstOrDefault(g => g.Count >= gradSterzata);
                if (consecutiveRays != null)
                {
                    dirPrinc = consecutiveRays[consecutiveRays.Count / 2];
                    break;
                }
            }
        }
    }

    // Metodo per resettare la direzione principale
    public void ResetDirection()
    {
        dirPrinc = 90;
    }

    // Metodo per generare i raggi del raycast
    public void GenerateRaycasts()
    {
        raycastDistances.Clear();

        for (int i = 0; i < 360; i++)
        {
            Vector3 direction = Quaternion.Euler(0, i, 0) * transform.forward;
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, DistRaycast))
            {
                raycastDistances[i] = Mathf.FloorToInt(hit.distance);
                Debug.DrawLine(transform.position, hit.point, Color.red);
            }
            else
            {
                raycastDistances[i] = DistRaycast;
                Debug.DrawLine(transform.position, transform.position + direction * DistRaycast, Color.green);
            }
        }

        UpdateRaycastData(raycastDistances);
    }

    // Metodo per disegnare i raggi del raycast con i colori appropriati
    public void DrawRaycasts()
    {
        foreach (var kvp in raycastDistances)
        {
            Vector3 direction = Quaternion.Euler(0, kvp.Key, 0) * transform.forward;
            Color color = kvp.Value >= Soglia ? Color.green : Color.red;
            Debug.DrawLine(transform.position, transform.position + direction * kvp.Value, color);
        }
    }

    // Metodo per ottenere i parametri delle direzioni
    public (int dirPrinc, int dirOpp, List<int> sopraSoglia, List<int> sottoSoglia) GetParameters()
    {
        return (dirPrinc, dirOpp, new List<int>(raggiSopraSoglia), new List<int>(raggiSottoSoglia));
    }
}


