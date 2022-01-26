namespace EvansFreshRoast.Api.Coffees

open Giraffe
open EvansFreshRoast.Api.Coffees.HttpHandlers

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
