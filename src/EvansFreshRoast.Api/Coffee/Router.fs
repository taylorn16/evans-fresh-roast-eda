namespace EvansFreshRoast.Api.Coffee

open Giraffe
open EvansFreshRoast.Api.Coffee.HttpHandlers

module Router =
    let getRouter compositionRoot =
        choose [
            GET >=> choose [
                routeCif "/coffees/%O" (getCoffee compositionRoot)
                routeCix "/coffees(/?)" >=> getCoffees compositionRoot
            ]
            POST >=> choose [
                routeCix "/coffees(/?)" >=> postCoffee compositionRoot
            ]
            PUT >=> choose [
                routeCif "/coffees/%O/activate" (activateCoffee compositionRoot)
                routeCif "/coffees/%O" (putCoffee compositionRoot)
            ]
        ]
