module EvansFreshRoast.Api.Auth.Router

open Giraffe
open EvansFreshRoast.Api.HttpHandlers
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.Auth.HttpHandlers

let router (compositionRoot: CompositionRoot) = choose [
    GET >=> choose [
        routeCix "/code(/?)" >=> getLoginCode compositionRoot
        routeCix "/refresh-token(/?)" >=> authenticate >=> getNewCookie compositionRoot
    ]
    POST >=> choose [
        routeCix "/login(/?)" >=> login compositionRoot
    ]
]
