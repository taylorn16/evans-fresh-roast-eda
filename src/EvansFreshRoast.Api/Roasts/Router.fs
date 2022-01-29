module EvansFreshRoast.Api.Roasts.Router

open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.Roasts.HttpHandlers
open Giraffe

let router (compositionRoot: CompositionRoot) = choose [
    GET >=> choose [
        routeCix "(/?)" >=> getRoasts compositionRoot
    ]
    POST >=> choose [
        routeCix "(/?)" >=> postRoast compositionRoot
        routeCif "/%O/open" (postOpenRoast compositionRoot)
        routeCif "/%O/follow-up" (postFollowUp compositionRoot)
        routeCif "/%O/complete" (postCompletion compositionRoot)
    ]
    PUT >=> choose [
        routeCif "/%O/coffees" (putCoffees compositionRoot)
        routeCif "/%O/customers" (putCustomers compositionRoot)
        routeCif "/%O/customers/%O/invoice" (putCustomerInvoice compositionRoot)
        routeCif "/%O" (putRoast compositionRoot)
    ]
    DELETE >=> choose [
        routeCif "/%O/coffees" (deleteCoffees compositionRoot)
        routeCif "/%O/customers" (deleteCustomers compositionRoot)
    ]
]
