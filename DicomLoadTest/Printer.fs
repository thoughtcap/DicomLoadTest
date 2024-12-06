namespace DicomLoadTest

module Printer =
    let warning (message: string): unit =
        printfn "\u001b[33mWarning: %s\u001b[0m" message

    let error (message: string): unit =
        printfn "\u001b[31mError: %s\u001b[0m" message