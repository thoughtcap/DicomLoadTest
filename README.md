# What is this?

This is a simple application to benchmark how well the [fo-dicom](https://github.com/fo-dicom/fo-dicom) library performs under heavy load.
It tests the performance of both multiple clients sending multiple requests to multiple PACS servers, and a single client sending multiple requests to a single PACS server.

# How to use?

Run the application using the `dotnet run` command.

Change the number of requests and iterations using the `numberOfRequests` and `iterations` variables in the `Program.fs` file.

### TODO:

- [ ] Make the number of requests and iterations configurable from the command line.