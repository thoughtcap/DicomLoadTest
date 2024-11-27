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

[<EntryPoint>]
let main _argv =
    async {
        let numberOfRequests = 10
        let iterations = 50
        let results = ResizeArray<TestIterationResult>()

        printfn $"Starting DICOM Load Test with %d{iterations} iterations"
        printfn $"Each iteration will perform %d{numberOfRequests} requests per test"

        for i in 1..iterations do
            printfn $"\nIteration %d{i} of %d{iterations}"
            printfn "=============================="

            // Test 1: Multiple clients -> Single PACS
            printfn "\nTest 1: Multiple clients -> Single PACS"
            let startTime = DateTime.Now
            let! singlePACSResults = TestMultiClientsSinglePACS numberOfRequests
            let endTime = DateTime.Now

            let successful, failed =
                singlePACSResults
                |> List.partition (fun r ->
                    match r.Status with
                    | Ok _ -> true
                    | Error _ -> false)

            let averageRoundTrip =
                if successful.Length > 0 then
                    successful |> List.averageBy (fun r -> r.RoundTripSeconds)
                else
                    0.0

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
        // let! multiPACSResults = TestMultiClientsSinglePACS numberOfRequests
        // let endTime = DateTime.Now

        // let successful, failed =
        //     multiPACSResults
        //     |> List.partition (fun r ->
        //         match r.Status with
        //         | Ok _ -> true
        //         | Error _ -> false)

        // let averageRoundTrip =
        //     if successful.Length > 0 then
        //         successful |> List.averageBy (fun r -> r.RoundTripSeconds)
        //     else
        //         0.0

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
