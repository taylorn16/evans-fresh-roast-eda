module AsyncHelpers

type Deferred<'a> =
    | NotStarted
    | InProgress
    | Resolved of 'a

[<RequireQualifiedAccess>]
module Deferred =
    let isInProgress =
        function
        | InProgress -> true
        | _ -> false

    let didFail =
        function
        | Resolved (Error _) -> true
        | _ -> false

type AsyncOperationEvt<'a> =
    | Started
    | Finished of 'a
