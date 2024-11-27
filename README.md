# What is this?

This is a simple application to benchmark how well the [fo-dicom](https://github.com/fo-dicom/fo-dicom) library performs under heavy load.
It tests the performance of both multiple clients sending multiple requests to multiple PACS servers, and a single client sending multiple requests to a single PACS server.

# How to use?

Run the application using the `dotnet run` command.

Change the number of requests and iterations using the `numberOfRequests` and `iterations` variables in the `Program.fs` file.

## Orthanc

This test suite was imagined to work with one or more locally hosted Orthanc instances. The second test, "Single client -> Multiple PACS", is intended to start with Orthanc running on http://localhost:8042 and work its way up to the last Orthanc instance running on http://localhost:8042 + (numberOfRequests - 1).

### TODO:

- [x] Make the number of requests and iterations configurable from the command line.
- [ ] Make second test configurable (local vs docker etc.)