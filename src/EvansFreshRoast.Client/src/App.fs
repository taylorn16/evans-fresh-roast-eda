module App

open EvansFreshRoast.Dto
open Elmish
open Elmish.Navigation
open Elmish.React
open Thoth.Json
open Fable.Core.JsInterop
open Routes
open State
open RootView

importSideEffects "./styles.scss"
    
let getLocalStorageSession() =
    Browser.WebStorage.localStorage.getItem "efr.session"
    |> Option.ofObj
    |> Option.bind (
        Decode.fromString Session.decoder
        >> function
            | Ok a -> Some a
            | Error _ -> None)

Program.mkProgram (init <| getLocalStorageSession()) update view
|> Program.toNavigable (UrlParser.parseHash Route.parse) setCurrentPage 
|> Program.withReactSynchronous "app-root"
|> Program.run
