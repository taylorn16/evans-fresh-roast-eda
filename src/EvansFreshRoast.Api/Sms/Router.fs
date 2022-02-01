module EvansFreshRoast.Api.Sms.Router

open Giraffe
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.Sms.HttpHandlers

let router (compositionRoot: CompositionRoot) = choose [
    POST >=> choose [
        routeCix "(/?)" >=> verifyTwilioId compositionRoot >=> receiveIncomingSms compositionRoot
    ]
]
