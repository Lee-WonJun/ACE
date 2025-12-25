module AceLang.Client.Main

open Elmish
open Bolero
open AceLang.Client.AST
open AceLang.Client.Parser
open AceLang.Client.Interpreter
open AceLang.Client.Storage

type Model = { Code: string; Output: string; IsRunning: bool }

let initModel = { Code = snd examples.[0]; Output = ""; IsRunning = false }

type Message =
    | SetCode of string
    | RunCode
    | ExecutionStep of EvalResult
    | ClearOutput
    | LoadExample of int
    | AppendOutput of string

let update message model =
    match message with
    | SetCode code -> { model with Code = code }, Cmd.none
    | LoadExample idx ->
        if idx >= 0 && idx < examples.Length then
            { model with Code = snd examples.[idx] }, Cmd.none
        else model, Cmd.none
    | ClearOutput -> { model with Output = "" }, Cmd.none
    | AppendOutput s -> { model with Output = model.Output + s }, Cmd.none
    | RunCode ->
        match parseAce model.Code with
        | Result.Ok prog ->
            let result = eval prog.Decls Map.empty prog.Main
            { model with Output = ""; IsRunning = true }, Cmd.ofMsg (ExecutionStep result)
        | Result.Error e -> { model with Output = sprintf "Parse Error: %s" e }, Cmd.none
    | ExecutionStep result ->
        match result with
        | Done v ->
            { model with Output = model.Output + sprintf "\n=> %s" (valueToString v); IsRunning = false }, Cmd.none
        | Error e ->
            { model with Output = model.Output + sprintf "\nError: %s" e; IsRunning = false }, Cmd.none
        | Request (eff, args, k) ->
            if eff = "IO.print" then
                let msg = args |> List.map valueToString |> String.concat " "
                { model with Output = model.Output + msg + "\n" }, Cmd.ofMsg (ExecutionStep (k VUnit))
            else
                { model with Output = model.Output + sprintf "\nUnhandled effect: %s" eff; IsRunning = false }, Cmd.none

type Main = Template<"wwwroot/main.html">

let view model dispatch =
    Main.Home()
        .Code(model.Code, fun code -> dispatch (SetCode code))
        .RunCode(fun _ -> dispatch RunCode)
        .ClearOutput(fun _ -> dispatch ClearOutput)
        .Output(model.Output)
        .LoadExample(fun e -> dispatch (LoadExample (int (string e.Value))))
        .Elt()

type MyApp() =
    inherit ProgramComponent<Model, Message>()
    override _.Program =
        Program.mkProgram (fun _ -> initModel, Cmd.none) update view
