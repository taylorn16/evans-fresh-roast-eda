namespace EvansFreshRoast.Framework

open System

type FrameworkTypeError =
    | IdIsEmpty
    | AggregateVersionIsNotPositive

type ConstrainedTypeError<'a> =
    | FrameworkTypeError of FrameworkTypeError
    | DomainTypeError of 'a

type Id<'a> = private Id of Guid
module Id =
    let create guid =
        if guid = Guid.Empty then
            Error <| FrameworkTypeError IdIsEmpty
        else
            Ok <| Id guid

    let value (Id guid) = guid

    let newId () = Id <| Guid.NewGuid()

type AggregateVersion = private AggregateVersion of int64
module AggregateVersion =
    let create (version: int64) =
        if version < 1L then
            Error <| FrameworkTypeError AggregateVersionIsNotPositive
        else
            Ok <| AggregateVersion version

    let apply f (AggregateVersion v) = f v

    let value = apply id

    let increment = apply (AggregateVersion << (+) 1L)

    let one = AggregateVersion 1L

type ConnectionString = private ConnectionString of string
module ConnectionString =
    let create connStr = ConnectionString connStr

    let value (ConnectionString connStr) = connStr