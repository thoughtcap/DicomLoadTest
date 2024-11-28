namespace DicomLoadTest

open System
open System.IO
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions

type RequestTiming =
    { StartTime: DateTime
      EndTime: DateTime
      IsSuccessful: bool }

type TestIterationResult =
    { IterationNumber: int
      Timestamp: DateTime
      TestType: string
      SuccessfulRequests: int
      FailedRequests: int
      AverageRoundTripSeconds: float
      TotalDurationInSeconds: float
      PatientNames: string list
      RequestTimings: RequestTiming list }

module ResultReporting =
    let SaveResults (results: TestIterationResult list) (filePath: string) =
        let timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
        let fileName = sprintf "test_results_%s.yaml" timestamp
        let fullPath = Path.Combine(filePath, fileName)

        let serializer =
            SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()

        let yamlContent = serializer.Serialize(results)

        File.WriteAllText(fullPath, yamlContent)
        printfn "Results saved to: %s" fullPath
        fullPath
