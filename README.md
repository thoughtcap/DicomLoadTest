# What is this?

This is a simple application to benchmark how well the [fo-dicom](https://github.com/fo-dicom/fo-dicom) library performs under heavy load.
It tests the performance of both multiple clients sending multiple requests to multiple PACS servers, and a single client sending multiple requests to a single PACS server.

## Running the Tests

You can run the tests using `dotnet run`. By default, this will execute 10 requests over 50 iterations. 

To configure the number of requests and iterations, use the following syntax:
```
dotnet run -- --number-of-requests <n> --iterations <n>
```
For example:
```
dotnet run -- --number-of-requests 20 --iterations 100
```
To see all available options:
```
dotnet run -- --help
```

## Orthanc

This test suite was imagined to work with one or more locally hosted Orthanc instances. The second test, "Single client -> Multiple PACS", is intended to start with Orthanc running on http://localhost:8042 and work its way up to the last Orthanc instance running on http://localhost:8042 + (numberOfRequests - 1).

### TODO:

- [x] Make the number of requests and iterations configurable from the command line.
- [ ] Make second test configurable (local vs docker etc.)