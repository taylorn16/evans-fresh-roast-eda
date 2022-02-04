module EvansFreshRoast.Api.Coffees.Router

open Giraffe
open EvansFreshRoast.Api.Coffees.HttpHandlers

let router compositionRoot =
    choose [
        GET >=> choose [
            routeCif "/%O" (getCoffee compositionRoot)
            routeCix "(/?)" >=> getCoffees compositionRoot
        ]
        POST >=> choose [
            routeCix "(/?)" >=> postCoffee compositionRoot
        ]
        PUT >=> choose [
            routeCif "/%O/activate" (activateCoffee compositionRoot)
            routeCif "/%O/deactivate" (deactivateCoffee compositionRoot)
            routeCif "/%O" (putCoffee compositionRoot)
        ]
    ]
