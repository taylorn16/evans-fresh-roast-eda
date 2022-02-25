namespace EvansFreshRoast.Dto

open EvansFreshRoast.Domain
open EvansFreshRoast.Framework

type Event =
    | RoastEvent of DomainEvent<Roast, Roast.Event>
    | CustomerEvent of DomainEvent<Customer, Customer.Event>
    | CoffeeEvent of DomainEvent<Coffee, Coffee.Event>
