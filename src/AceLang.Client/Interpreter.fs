module AceLang.Client.Interpreter

open AceLang.Client.AST

type Value =
    | VUnit
    | VInt of int
    | VString of string
    | VBool of bool
    | VContinuation of (Value -> EvalResult)

and EvalResult =
    | Done of Value
    | Request of string * Value list * (Value -> EvalResult)
    | Error of string

type Env = Map<string, Value>

let rec valueToString = function
    | VUnit -> "unit"
    | VInt n -> string n
    | VString s -> s
    | VBool b -> if b then "true" else "false"
    | VContinuation _ -> "<continuation>"

let rec eval (decls: Map<string, Decl>) (env: Env) (expr: Expr): EvalResult =
    match expr with
    | ELiteral lit ->
        Done (match lit with
              | LInt n -> VInt n
              | LString s -> VString s
              | LBool b -> VBool b
              | LUnit -> VUnit)

    | EVar name ->
        match Map.tryFind name env with
        | Some v -> Done v
        | None -> Error (sprintf "Undefined variable: %s" name)

    | EBinOp (left, op, right) ->
        match eval decls env left with
        | Done (VInt l) ->
            match eval decls env right with
            | Done (VInt r) ->
                Done (VInt (match op with
                            | "+" -> l + r
                            | "-" -> l - r
                            | "*" -> l * r
                            | "/" -> l / r
                            | _ -> 0))
            | Done (VString r) when op = "+" -> Done (VString (string l + r))
            | other -> other
        | Done (VString l) ->
            match eval decls env right with
            | Done v -> Done (VString (l + valueToString v))
            | other -> other
        | other -> other

    | ELet (name, value, body) ->
        match eval decls env value with
        | Done v -> 
            let env' = Map.add name v env
            match body with
            | ELiteral LUnit -> Done v
            | _ -> eval decls env' body
        | Request (eff, args, k) ->
            Request (eff, args, fun v ->
                let env' = Map.add name v env
                match body with
                | ELiteral LUnit -> Done v
                | _ -> eval decls env' body)
        | err -> err

    | EBlock exprs ->
        let rec evalBlock env = function
            | [] -> Done VUnit
            | [e] -> eval decls env e
            | ELet(n, v, _) :: rest ->
                match eval decls env v with
                | Done value -> evalBlock (Map.add n value env) rest
                | Request (eff, args, k) ->
                    Request (eff, args, fun v -> evalBlock (Map.add n v env) rest)
                | err -> err
            | e :: rest ->
                match eval decls env e with
                | Done _ -> evalBlock env rest
                | Request (eff, args, k) ->
                    Request (eff, args, fun _ -> evalBlock env rest)
                | err -> err
        evalBlock env exprs

    | ECall (name, args) ->
        let rec evalArgs acc remaining =
            match remaining with
            | [] -> Some (List.rev acc)
            | arg :: rest ->
                match eval decls env arg with
                | Done v -> evalArgs (v :: acc) rest
                | _ -> None
        match evalArgs [] args with
        | Some values ->
            Request (name, values, fun v -> Done v)
        | None -> Error "Failed to evaluate arguments"

    | EIf (cond, thenE, elseE) ->
        match eval decls env cond with
        | Done (VBool true) -> eval decls env thenE
        | Done (VBool false) -> eval decls env elseE
        | Done (VInt n) -> eval decls env (if n <> 0 then thenE else elseE)
        | Done _ -> Error "Condition must be boolean"
        | other -> other

    | EHandle (body, clauses) ->
        let rec handle result =
            match result with
            | Done v -> Done v
            | Error e -> Error e
            | Request (eff, args, k) ->
                match clauses |> List.tryFind (fun c -> c.EffectName = eff) with
                | Some clause ->
                    let upstreamValue =
                        match Map.tryFind eff decls with
                        | Some { Body = Some implBody; Params = ps } ->
                            let implEnv = List.zip ps args |> List.fold (fun e (p, v) -> Map.add p v e) Map.empty
                            match eval decls implEnv implBody with
                            | Done v -> v
                            | _ -> VUnit
                        | _ -> VUnit
                    let env' = 
                        clause.ArgNames 
                        |> List.zip args 
                        |> List.fold (fun e (v, n) -> Map.add n v e) env
                        |> Map.add clause.KName (VContinuation (fun v -> handle (k v)))
                        |> Map.add "v" upstreamValue
                    eval decls env' clause.Body |> handle
                | None -> Request (eff, args, fun v -> handle (k v))
        handle (eval decls env body)

    | EContinue (kName, arg) ->
        match Map.tryFind kName env with
        | Some (VContinuation k) ->
            match arg with
            | CA_Val e ->
                match eval decls env e with
                | Done v -> k v
                | other -> other
            | CA_Upstream ->
                match Map.tryFind "v" env with
                | Some (VContinuation upstream) ->
                    match upstream VUnit with
                    | Done v -> k v
                    | other -> other
                | _ -> Error "No upstream value"
        | _ -> Error (sprintf "Not a continuation: %s" kName)

let runProgram (prog: Program) (handleIO: string -> Value list -> Value): EvalResult =
    let rec loop result =
        match result with
        | Done v -> Done v
        | Error e -> Error e
        | Request (eff, args, k) ->
            match Map.tryFind eff prog.Decls with
            | Some { Body = Some body; Params = ps } ->
                let env = List.zip ps args |> List.fold (fun e (p, v) -> Map.add p v e) Map.empty
                match eval prog.Decls env body with
                | Done v -> loop (k v)
                | other -> other
            | _ ->
                let v = handleIO eff args
                loop (k v)
    loop (eval prog.Decls Map.empty prog.Main)
