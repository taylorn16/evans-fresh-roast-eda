module EvansFreshRoast.Api.Customers.Router

open Giraffe
open EvansFreshRoast.Api.Customers.HttpHandlers

let router compositionRoot = choose [
    GET >=> choose [
        routeCif "/customers/%O" (getCustomer compositionRoot)
        routeCix "/customers(/?)" >=> getCustomers compositionRoot
    ]
    POST >=> choose [
        routeCix "/customers(/?)" >=> postCustomer compositionRoot
    ]
]
