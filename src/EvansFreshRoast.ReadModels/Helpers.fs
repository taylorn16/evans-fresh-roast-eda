namespace EvansFreshRoast.ReadModels

open Thoth.Json.Net

module Helpers =
    let rec generateRecursiveJsonbSet column (pathValueTuples: (string * JsonValue) list) =
        match pathValueTuples with
        | [] ->
            ""
        | [(path, value)] ->
            $"jsonb_set({column}, '{{{path}}}', '{Encode.toString 0 value}', true)"
        | (path, value)::rest ->
            $"jsonb_set({generateRecursiveJsonbSet column rest}, '{{{path}}}', '{Encode.toString 0 value}', true)"