namespace DicomLoadTest

open FellowOakDicom
open FellowOakDicom.Network
open FellowOakDicom.Network.Client
open System

type DicomRequestResult =
    { RequestId: string
      StartTime: DateTime
      EndTime: DateTime
      RoundTripSeconds: float
      StudiesFound: int
      Status: Result<unit, exn>
      PatientNames: string list }

type ResultMap = Map<string, DicomRequestResult>

type ResultAggregator = MailboxProcessor<ResultAggregatorMsg>

and ResultAggregatorMsg =
    | AddResult of DicomRequestResult
    | GetResults of AsyncReplyChannel<DicomRequestResult list>

module ResultAggregator =
    let create () =
        MailboxProcessor.Start(fun inbox ->
            let rec loop (state: ResultMap) =
                async {
                    let! msg = inbox.Receive()

                    match msg with
                    | AddResult result ->
                        let newState = Map.add result.RequestId result state
                        return! loop newState
                    | GetResults reply ->
                        reply.Reply(state |> Map.values |> Seq.toList)
                        return! loop state
                }

            loop Map.empty)

    let addResult (aggregator: ResultAggregator) (result: DicomRequestResult) = aggregator.Post(AddResult result)

    let getResults (aggregator: ResultAggregator) = aggregator.PostAndReply(GetResults)

type PacsConfig =
    { Host: string
      Port: int
      AETitle: string }

type ResponseMessage =
    | CollectResponse of DicomCFindResponse
    | GetResults of AsyncReplyChannel<Async<int * string list>>

module TestBattery =
    let private _pacsHost: string = "localhost"
    let private _pacsPort: int = 4242
    let private _pacsAET: string = "MARKO-ORTHANC" // Your PACS AET here

    let private createDicomClient (clientAET: string) (pacsConfig: PacsConfig) : IDicomClient =
        DicomClientFactory.Create(pacsConfig.Host, pacsConfig.Port, false, clientAET, pacsConfig.AETitle)
        |> fun client ->
            client.NegotiateAsyncOps()
            client.ServiceOptions.RequestTimeout <- TimeSpan.FromSeconds 30.0
            client.ClientOptions.AssociationRequestTimeoutInMs <- 30000 // 30 seconds
            client.ClientOptions.AssociationLingerTimeoutInMs <- 5000 // 5 seconds

            client

    let private executeCfindWithClient
        (client: IDicomClient)
        (requestId: string)
        (aggregator: ResultAggregator)
        : Async<unit> =
        async {
            let startTime = DateTime.Now

            let request =
                let req = DicomCFindRequest(DicomQueryRetrieveLevel.Study)
                let tags = [ DicomTag.PatientName; DicomTag.StudyDate; DicomTag.StudyInstanceUID ]
                tags |> List.iter (fun tag -> req.Dataset.AddOrUpdate(tag, "") |> ignore)
                req

            let responseCollector: MailboxProcessor<ResponseMessage> =
                MailboxProcessor.Start(fun inbox ->
                    let rec loop (studies, names) =
                        async {
                            let! message = inbox.Receive()

                            match message with
                            | CollectResponse response ->
                                if response.Status = DicomStatus.Pending && response.HasDataset then
                                    let patientName = response.Dataset.GetSingleValue<string>(DicomTag.PatientName)
                                    return! loop (studies + 1, patientName :: names)
                                else
                                    return! loop (studies, names)
                            | GetResults reply ->
                                reply.Reply(async.Return(studies, names))
                                return! loop (studies, names)
                        }

                    loop (0, []))

            request.OnResponseReceived <-
                DicomCFindRequest.ResponseDelegate(fun _ res -> responseCollector.Post(CollectResponse res))

            let! result =
                async {
                    try
                        do! client.AddRequestAsync request |> Async.AwaitTask
                        do! client.SendAsync() |> Async.AwaitTask
                        return Ok()
                    with ex ->
                        printfn $"\nError in request %s{requestId}:%i{client.Port}: %s{ex.Message}\n"
                        return Error ex
                }

            let endTime = DateTime.Now

            let! (studiesFound, patientNames) = responseCollector.PostAndReply(GetResults)

            ResultAggregator.addResult
                aggregator
                { RequestId = requestId
                  StartTime = startTime
                  EndTime = endTime
                  RoundTripSeconds = endTime.Subtract(startTime).TotalSeconds
                  StudiesFound = studiesFound
                  Status = result
                  PatientNames = patientNames }
        }

    /// <summary>
    /// Tests multiple parallel C-FIND requests from different clients to a single PACS server.
    /// </summary>
    /// <param name="requestCount">Number of parallel requests to send.</param>
    /// <returns>List of results for each request.</returns>
    let TestMultiClientsSinglePACS (requestCount: int) : Async<DicomRequestResult list> =
        async {
            try
                let aggregator = ResultAggregator.create ()
                printfn $"Starting %i{requestCount} concurrent C-FIND requests from different clients..."

                let pacs =
                    { Host = _pacsHost
                      Port = _pacsPort
                      AETitle = _pacsAET }

                let startTime = DateTime.Now

                let! _ =
                    [ 1..requestCount ]
                    |> List.map (fun i ->
                        async {
                            return!
                                executeCfindWithClient
                                    (createDicomClient (sprintf $"TEST-CLIENT-%i{i}") pacs)
                                    (sprintf $"TEST-CLIENT-%02i{i}")
                                    aggregator
                        })
                    |> Async.Parallel

                let totalTime = DateTime.Now.Subtract(startTime).TotalMilliseconds
                printfn $"Completed %i{requestCount} concurrent C-FIND requests in %f{totalTime}ms"
                printfn $"Average time per request: %f{totalTime / float requestCount}ms"
                return ResultAggregator.getResults aggregator
            with ex ->
                printfn $"An exception occurred in test_multiple_requests_single_connection: %s{ex.Message}"
                return []
        }

    /// <summary>
    /// Tests multiple parallel C-FIND requests to different PACS servers. One client will be spawned for each PACS server, depending on the <code>requestCount</code> parameter.
    /// </summary>
    /// <param name="requestCount">Number of parallel requests to send.</param>
    /// <returns>Aggregated results from all requests.</returns>
    let TestMultiplePACSConnections (requestCount: int) : Async<DicomRequestResult list> =
        async {
            try
                let aggregator = ResultAggregator.create ()
                printfn $"Starting %i{requestCount} concurrent C-FIND requests to different PACS instances..."
                let startTime = DateTime.Now

                let clientFactory = createDicomClient "TEST-CLIENT"

                let! _ =
                    [ 0 .. requestCount - 1 ]
                    |> List.map (fun i ->
                        let pacsConfig =
                            { Host = _pacsHost
                              Port = _pacsPort + i
                              AETitle = sprintf $"ORTHANC_%i{i + 1}" }

                        executeCfindWithClient (clientFactory pacsConfig) (sprintf $"Request-%i{i}") aggregator)
                    |> Async.Parallel

                let totalTime = DateTime.Now.Subtract(startTime).TotalMilliseconds
                printfn $"Completed %i{requestCount} concurrent C-FIND requests in %f{totalTime}ms"
                printfn $"Average time per request: %f{totalTime / float requestCount}ms"
                return ResultAggregator.getResults aggregator

            with ex ->
                printfn $"An exception occurred in test_multiple_pacs_connections: %s{ex.Message}"
                return []
        }
