module AceLang.Tests.ParserTests

open Xunit
open FsUnit.Xunit
open AceLang.Client.AST
open AceLang.Client.Parser

[<Fact>]
let ``Basic arithmetic precedence - multiplication binds tighter than addition`` () =
    let code = "defn main() { 1 + 2 * 3 }"
    let result = parseAce code
    match result with
    | Ok prog ->
        prog.Main |> should equal (EBinOp(ELiteral(LInt 1), "+", EBinOp(ELiteral(LInt 2), "*", ELiteral(LInt 3))))
    | Error e -> failwith e

[<Fact>]
let ``Implicit effect definition has body`` () =
    let code = "defn add(a, b) { a + b }"
    let result = parseAce code
    match result with
    | Ok prog ->
        prog.Decls |> Map.containsKey "add" |> should be True
        let decl = prog.Decls.["add"]
        decl.Params |> should equal ["a"; "b"]
        decl.Body.IsSome |> should be True
    | Error e -> failwith e

[<Fact>]
let ``Explicit effect definition has no body`` () =
    let code = "defn `Print` (msg)"
    let result = parseAce code
    match result with
    | Ok prog ->
        prog.Decls |> Map.containsKey "Print" |> should be True
        prog.Decls.["Print"].Body |> should equal None
    | Error e -> failwith e

[<Fact>]
let ``Handler syntax parses correctly`` () =
    let code = """
defn main() {
    handle {
        doSomething()
    } with (doSomething) -> k {
        continue k (10)
    }
}
"""
    let result = parseAce code
    match result with
    | Ok prog ->
        match prog.Main with
        | EHandle(_, clauses) ->
            clauses |> List.length |> should equal 1
            clauses.[0].EffectName |> should equal "doSomething"
            match clauses.[0].Body with
            | EContinue(_, CA_Val(ELiteral(LInt 10))) -> ()
            | _ -> failwith "Expected EContinue with value 10"
        | _ -> failwith "Expected EHandle"
    | Error e -> failwith e
