module EvansFreshRoast.Api.Customers.Router

open Giraffe
open EvansFreshRoast.Api.Customers.HttpHandlers

let router compositionRoot = choose [
    GET >=> choose [
        routeCif "/%O" (getCustomer compositionRoot)
        routeCix "(/?)" >=> getCustomers compositionRoot
    ]
    POST >=> choose [
        routeCix "(/?)" >=> postCustomer compositionRoot
    ]
    PUT >=> choose [
        routeCif "/%O" (putCustomer compositionRoot)
    ]
]
