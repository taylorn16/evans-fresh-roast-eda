namespace EvansFreshRoast.Utils

[<AutoOpen>]
module Result =
    let toOption =
        function
        | Ok a -> Some a
        | Error _ -> None

    let ofOption noneValue opt =
        match opt with
        | Some a -> Ok a
        | None -> Error noneValue

    let isOk =
        function
        | Ok _ -> true
        | Error _ -> false

    let unsafeAssertOk res =
        match res with
        | Ok a -> a
        | _ -> failwith "You asserted Ok, but it was NOT Ok."

    let sequence (combineErrors: 'b seq -> 'b) (results: Result<'a, 'b> seq) =
        let successes = ResizeArray()
        let failures = ResizeArray()

        for result in results do
            match result with
            | Ok a -> successes.Add(a)
            | Error e -> failures.Add(e)

        if failures.Count > 0 then
            Error <| combineErrors failures
        else
            Ok <| successes.ToArray()

    let apply f res =
        match res, f with
        | Ok arg, Ok f' -> Ok <| f' arg
        | Error argErr, _ -> Error argErr
        | _, Error fErr -> Error fErr

    let (<*>) = apply

    let (<!>) = Result.map

[<RequireQualifiedAccess>]
module Async =
    let map f comp =
        async {
            let! x = comp
            return f x
        }

    let lift x =
        async {
            return x
        }