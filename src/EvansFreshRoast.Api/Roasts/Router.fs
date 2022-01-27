module EvansFreshRoast.Api.Roasts.Router

open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.Roasts.HttpHandlers
open Giraffe

let router (compositionRoot: CompositionRoot) = choose [
    POST >=> choose [
        routeCix "/roasts(/?)" >=> handlePostRoast compositionRoot
    ]
]
