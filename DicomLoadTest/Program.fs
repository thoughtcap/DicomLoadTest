open DicomLoadTest
open DicomLoadTest.TestBattery
open DicomLoadTest.ResultReporting
open System
open System.IO

let formatPatientNames (results: DicomRequestResult list) =
    results
    |> List.map (fun r -> r.PatientNames)
    |> List.concat
    |> List.distinct
    |> List.map (fun s -> s.Trim())

type TestConfig = {
    NumberOfRequests: int
    Iterations: int
}

let defaultConfig = {
    NumberOfRequests = 50
    Iterations = 50
}

/// <summary>
/// Parse the results of the DICOM requests and calculate the average round trip time.
/// </summary>
/// <param name="results">The results of the DICOM requests.</param>
/// <returns>A tuple containing the number of successful and failed requests, and the average round trip time.</returns>
let parseDicomResults (results: DicomRequestResult list) =
    let successful, failed =
        results
        |> List.partition (fun r ->
            match r.Status with
            | Ok _ -> true
            | Error _ -> false)

    let averageRoundTrip =
        match successful.Length with
        | n when n > 0 -> successful |> List.averageBy (fun r -> r.RoundTripSeconds)
        | _ -> 0.0

    (successful, failed), averageRoundTrip

let printUsage() =
    printfn "Usage: DicomLoadTest [options]"
    printfn "Options:"
    printfn "  --help                     Show this help message"
    printfn "  --number-of-requests <n>   Number of requests to perform (default: 50)"
    printfn "  --iterations <n>           Number of iterations to run (default: 10)"

let parseCommandLineArgs (args: string[]) : Option<TestConfig> =
    let rec parseArgs config = function
        | "--help" :: _ ->
            printUsage()
            None
        | "--number-of-requests" :: value :: rest ->
            match Int32.TryParse value with
            | true, num -> parseArgs { config with NumberOfRequests = num } rest
            | false, _ ->
                printfn "\u001b[33mWarning: Invalid value for --number-of-requests, using default value of 50\u001b[0m\n"
                parseArgs config rest
        | "--iterations" :: value :: rest ->
            match Int32.TryParse value with
            | true, num -> parseArgs { config with Iterations = num } rest
            | false, _ ->
                printfn "\u001b[33mWarning: Invalid value for --iterations, using default value of 10\u001b[0m\n"
                parseArgs config rest
        | [] -> Some config
        | _ :: rest -> parseArgs config rest
    
    args 
    |> Array.toList 
    |> parseArgs defaultConfig

[<EntryPoint>]
let main argv =
    match parseCommandLineArgs argv with
    | None -> 0  // Exit successfully after showing help
    | Some config ->
        async {
            let results = ResizeArray<TestIterationResult>()

            printfn $"Starting DICOM Load Test with %d{config.Iterations} iterations"
            printfn $"Each iteration will perform %d{config.NumberOfRequests} requests per test"

            for i in 1..config.Iterations do
                printfn $"\nIteration %d{i} of %d{config.Iterations}"
                printfn "=============================="

                // Test 1: Multiple clients -> Single PACS
                printfn "\nTest 1: Multiple clients -> Single PACS"
                let startTime = DateTime.Now
                let! singlePACSResults = TestMultiClientsSinglePACS config.NumberOfRequests
                let endTime = DateTime.Now

                let (successful, failed), averageRoundTrip = parseDicomResults singlePACSResults

                results.Add(
                    { IterationNumber = i
                      Timestamp = DateTime.Now
                      TestType = "Multiple Clients -> Single PACS"
                      SuccessfulRequests = successful.Length
                      FailedRequests = failed.Length
                      AverageRoundTripSeconds = averageRoundTrip
                      TotalDurationInSeconds = endTime.Subtract(startTime).TotalSeconds
                      PatientNames = formatPatientNames successful }
                )

                printfn
                    $"Successful: %d{successful.Length}, Failed: %d{failed.Length}, Avg Round Trip: %.2f{averageRoundTrip}s, Total Duration: %.2f{endTime.Subtract(startTime).TotalSeconds}s"

            // Test 2: Single client -> Multiple PACS
            // printfn "\nTest 2: Single client -> Multiple PACS"
            // let startTime = DateTime.Now
            // let! multiPACSResults = TestMultiClientsSinglePACS config.NumberOfRequests
            // let endTime = DateTime.Now

            // let (successful, failed), averageRoundTrip = parseDicomResults multiPACSResults

            // results.Add(
            //     { IterationNumber = i
            //       Timestamp = DateTime.Now
            //       TestType = "Single Client -> Multiple PACS"
            //       SuccessfulRequests = successful.Length
            //       FailedRequests = failed.Length
            //       AverageRoundTripSeconds = averageRoundTrip
            //       TotalDurationMs = endTime.Subtract(startTime).TotalMilliseconds
            //       PatientNames = formatPatientNames successful }
            // )

            // printfn
            //     $"Successful: %d{successful.Length}, Failed: %d{failed.Length}, Avg Round Trip: %.2f{averageRoundTrip}s"

            // Save results to file
            let resultsDir = "test_results"
            Directory.CreateDirectory(resultsDir) |> ignore
            let resultFile = SaveResults (results |> Seq.toList) resultsDir

            printfn "\nAll tests completed. Results saved to: %s" resultFile
            printfn "\nPress any key to exit."
            System.Console.ReadKey() |> ignore
        }
        |> Async.RunSynchronously

        0
