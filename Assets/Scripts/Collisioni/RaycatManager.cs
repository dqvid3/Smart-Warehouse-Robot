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

using System.Collections.Generic;
using UnityEngine;

public class RaycastManager : MonoBehaviour
{
    public int rayCount = 360;  // Numero di raggi del raycast
    public float distRaycast = 5f;  // Lunghezza massima dei raggi
    public float threshold = 3f;  // Soglia sotto la quale i raggi diventano sotto soglia
    public int steeringAngle = 20;  // Angolo di sterzata per determinare deviazione
    public float rayHeight = 0.4f;  // Altezza del raggio di raycast
    public int dirPrinc = 90;  // Direzione principale (inizialmente 90 gradi)
    public int dirOpp = 270;   // Direzione opposta
    public List<int> raysAboveThreshold = new List<int>();
    public List<int> raysBelowThreshold = new List<int>();

    private void Start()
    {
        // Simula l'inizializzazione del raycast (può essere integrato con il tuo sistema di sensori)
        PerformRaycasts();
    }

    private void PerformRaycasts()
    {
        raysAboveThreshold.Clear();
        raysBelowThreshold.Clear();

        // Simulazione dei raggi con veri raycast di Unity
        for (int i = 0; i < rayCount; i++)
        {
            // Calcola la direzione del raggio a 360 gradi
            float angle = i * Mathf.Deg2Rad;
            Vector3 rayDirection = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

            // Posizione di partenza del raycast, leggermente sopra la superficie
            Vector3 rayStartPosition = new Vector3(transform.position.x, transform.position.y + rayHeight, transform.position.z);

            RaycastHit hit;
            // Usa Physics.Raycast per fare il vero raycast
            if (Physics.Raycast(rayStartPosition, rayDirection, out hit, distRaycast))
            {
                // Se il raycast colpisce qualcosa, confronta la distanza
                if (hit.distance < threshold)
                    raysBelowThreshold.Add(i);  // Aggiungi il raggio sotto soglia
                else
                    raysAboveThreshold.Add(i);  // Aggiungi il raggio sopra soglia
            }
            else
            {
                raysAboveThreshold.Add(i);  // Se non c'è collisione, considera il raggio sopra soglia
            }
        }
    }

    public void UpdateDirectionAndPath(ref Vector3[] path)
    {
        // Controlla se ci sono ostacoli rilevati prima di decidere la direzione
        if (raysBelowThreshold.Count == 0)
        {
            // Se non ci sono ostacoli sotto soglia, non cambiare la direzione
            return;
        }

        // Se ci sono ostacoli, aggiorna la direzione
        if (raysBelowThreshold.Count == 1)
        {
            HandleSingleObstacle();
        }
        else if (raysBelowThreshold.Count > 1)
        {
            HandleMultipleObstacles();
        }

        // Ora che la direzione principale è aggiornata, modifica il percorso
        ModifyPathWithDeviation(ref path, 0, path.Length - 1, -2f, 2f, dirPrinc < dirOpp);
    }

    private void HandleSingleObstacle()
    {
        // Gestisce il caso di un singolo ostacolo
        List<int> dirSx = new List<int>();
        List<int> dirDx = new List<int>();

        // Creazione dei sotto array per dir_sx e dir_dx
        foreach (int ray in raysBelowThreshold)
        {
            if (ray > dirPrinc)
                dirSx.Add(ray);
            else
                dirDx.Add(ray);
        }

        // Decidi quale direzione è più corta (a sinistra o a destra)
        if (dirSx.Count <= dirDx.Count)
        {
            dirPrinc = dirSx[dirSx.Count / 2]; // Scegli il punto medio della direzione sx
            dirOpp = (dirPrinc + 180) % 360;  // Aggiorna la direzione opposta
        }
        else
        {
            dirPrinc = dirDx[dirDx.Count / 2]; // Scegli il punto medio della direzione dx
            dirOpp = (dirPrinc + 180) % 360;  // Aggiorna la direzione opposta
        }
    }

    private void HandleMultipleObstacles()
    {
        // Gestisce il caso di più ostacoli
        if (raysBelowThreshold.Count == 2)
        {
            int startObstacle = raysBelowThreshold[0];
            int endObstacle = raysBelowThreshold[1];

            // Calcola la distanza tra gli ostacoli e controlla se c'è abbastanza spazio
            if (Mathf.Abs(startObstacle - endObstacle) >= steeringAngle)
            {
                dirPrinc = (startObstacle + endObstacle) / 2;  // Punto medio
                dirOpp = (dirPrinc + 180) % 360;
            }
            else
            {
                // Se non c'è abbastanza spazio, prendi la direzione più corta
                int diffLeft = Mathf.Abs(dirPrinc - startObstacle);
                int diffRight = Mathf.Abs(dirPrinc - endObstacle);

                if (diffLeft < diffRight)
                {
                    dirPrinc = startObstacle;
                    dirOpp = (dirPrinc + 180) % 360;
                }
                else
                {
                    dirPrinc = endObstacle;
                    dirOpp = (dirPrinc + 180) % 360;
                }
            }
        }
        else
        {
            // Nel caso ci siano più ostacoli, seleziona quello più lontano per cambiare direzione
            List<int> possibleDirections = new List<int>();

            foreach (int ray in raysBelowThreshold)
            {
                possibleDirections.Add(ray);
            }

            possibleDirections.Sort();

            // Calcola la direzione migliore tra quelli liberi
            for (int i = 0; i < possibleDirections.Count - 1; i++)
            {
                int midPoint = (possibleDirections[i] + possibleDirections[i + 1]) / 2;
                if (Mathf.Abs(midPoint - dirPrinc) > steeringAngle)
                {
                    dirPrinc = midPoint;
                    dirOpp = (dirPrinc + 180) % 360;
                    break;
                }
            }
        }
    }

    private void ModifyPathWithDeviation(ref Vector3[] path, int startIndex, int endIndex, float deviationAmountRangeMin, float deviationAmountRangeMax, bool isLeft)
    {
        if (path.Length > endIndex)
        {
            for (int i = startIndex; i < endIndex; i++)
            {
                Vector3 currentNode = path[i];
                Vector3 nextNode = path[i + 1];

                Vector3 direction = (nextNode - currentNode).normalized;
                Vector3 deviationDirection = isLeft ? new Vector3(-direction.z, 0, direction.x) : new Vector3(direction.z, 0, -direction.x);

                float deviationAmount = Random.Range(deviationAmountRangeMin, deviationAmountRangeMax);
                Vector3 deviation = deviationDirection * deviationAmount;

                Vector3 newDeviatedNode = currentNode + deviation;

                List<Vector3> modifiedPath = new List<Vector3>(path);
                modifiedPath.Insert(i + 1, newDeviatedNode);
                path = modifiedPath.ToArray();
            }
        }
    }

    private void OnDrawGizmos()
    {
        PerformRaycasts();

        Gizmos.color = Color.green;
        foreach (int rayIndex in raysAboveThreshold)
        {
            float angle = rayIndex * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distRaycast;
            Vector3 startPosition = new Vector3(transform.position.x, transform.position.y + rayHeight, transform.position.z);
            Gizmos.DrawLine(startPosition, startPosition + direction);
        }

        Gizmos.color = Color.red;
        foreach (int rayIndex in raysBelowThreshold)
        {
            float angle = rayIndex * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distRaycast;
            Vector3 startPosition = new Vector3(transform.position.x, transform.position.y + rayHeight, transform.position.z);
            Gizmos.DrawLine(startPosition, startPosition + direction);
        }
    }
}


