module AceLang.Client.Interpreter

open AceLang.Client.AST

type LazyValue = { mutable Cached: Value option; Eval: unit -> EvalResult }

and Value =
    | VUnit
    | VInt of int
    | VString of string
    | VBool of bool
    | VContinuation of (Value -> EvalResult)
    | VLazy of LazyValue

and EvalResult =
    | Done of Value
    | Request of string * Value list * (Value -> EvalResult)
    | Error of string

type Env = Map<string, Value>

type HandlerFrame = { Clauses: HandlerClause list; Env: Env }

let rec valueToString = function
    | VUnit -> "unit"
    | VInt n -> string n
    | VString s -> s
    | VBool b -> if b then "true" else "false"
    | VContinuation _ -> "<continuation>"
    | VLazy _ -> "<lazy>"

let literalToValue = function
    | LInt n -> VInt n
    | LString s -> VString s
    | LBool b -> VBool b
    | LUnit -> VUnit

let rec forceLazy (lazyValue: LazyValue) (cont: Value -> EvalResult) : EvalResult =
    match lazyValue.Cached with
    | Some v -> cont v
    | None ->
        let rec step result =
            match result with
            | Done v ->
                lazyValue.Cached <- Some v
                cont v
            | Error e -> Error e
            | Request (eff, args, resume) ->
                Request (eff, args, fun v -> step (resume v))
        step (lazyValue.Eval())

let forceValue value cont =
    match value with
    | VLazy lazyValue -> forceLazy lazyValue cont
    | _ -> cont value

let bindParams (ps: string list) (args: Value list) =
    List.zip ps args |> List.fold (fun e (p, v) -> Map.add p v e) Map.empty

let rec tryFindHandler effName handlers =
    match handlers with
    | [] -> None
    | frame :: rest ->
        match frame.Clauses |> List.tryFind (fun c -> c.EffectName = effName) with
        | Some clause -> Some(frame, clause, rest)
        | None -> tryFindHandler effName rest

let rec evalWithHandlers (decls: Map<string, Decl>) (env: Env) (handlers: HandlerFrame list) (expr: Expr) (k: Value -> EvalResult) : EvalResult =
    match expr with
    | ELiteral lit -> k (literalToValue lit)

    | EVar name ->
        match Map.tryFind name env with
        | Some v -> forceValue v k
        | None -> Error (sprintf "Undefined variable: %s" name)

    | EBinOp (left, op, right) ->
        evalWithHandlers decls env handlers left (fun l ->
            evalWithHandlers decls env handlers right (fun r ->
                match op, l, r with
                | "+", VInt a, VInt b -> k (VInt (a + b))
                | "-", VInt a, VInt b -> k (VInt (a - b))
                | "*", VInt a, VInt b -> k (VInt (a * b))
                | "/", VInt a, VInt b -> k (VInt (a / b))
                | "+", VString a, b -> k (VString (a + valueToString b))
                | "+", a, VString b -> k (VString (valueToString a + b))
                | _ -> Error "Invalid operands for binary operator"))

    | ELet (name, valueExpr, body) ->
        evalWithHandlers decls env handlers valueExpr (fun v ->
            let env' = Map.add name v env
            match body with
            | ELiteral LUnit -> k v
            | _ -> evalWithHandlers decls env' handlers body k)

    | EBlock exprs ->
        let rec evalBlock blockEnv remaining =
            match remaining with
            | [] -> k VUnit
            | [e] -> evalWithHandlers decls blockEnv handlers e k
            | ELet(name, valueExpr, _) :: rest ->
                evalWithHandlers decls blockEnv handlers valueExpr (fun v ->
                    evalBlock (Map.add name v blockEnv) rest)
            | e :: rest ->
                evalWithHandlers decls blockEnv handlers e (fun _ -> evalBlock blockEnv rest)
        evalBlock env exprs

    | ECall (name, args) ->
        let rec evalArgs acc remaining =
            match remaining with
            | [] -> resolveEffect decls env handlers name (List.rev acc) k
            | arg :: rest ->
                evalWithHandlers decls env handlers arg (fun v -> evalArgs (v :: acc) rest)
        evalArgs [] args

    | EIf (cond, thenE, elseE) ->
        evalWithHandlers decls env handlers cond (fun v ->
            match v with
            | VBool true -> evalWithHandlers decls env handlers thenE k
            | VBool false -> evalWithHandlers decls env handlers elseE k
            | VInt n ->
                if n <> 0 then evalWithHandlers decls env handlers thenE k
                else evalWithHandlers decls env handlers elseE k
            | _ -> Error "Condition must be boolean")

    | EHandle (body, clauses) ->
        let frame = { Clauses = clauses; Env = env }
        evalWithHandlers decls env (frame :: handlers) body k

    | EContinue (kName, arg) ->
        match Map.tryFind kName env with
        | Some (VContinuation cont) ->
            match arg with
            | CA_Val e -> evalWithHandlers decls env handlers e cont
            | CA_Upstream ->
                match Map.tryFind "v" env with
                | Some v -> forceValue v cont
                | None -> Error "No upstream value"
        | _ -> Error (sprintf "Not a continuation: %s" kName)

and resolveEffect (decls: Map<string, Decl>) (env: Env) (handlers: HandlerFrame list) (eff: string) (args: Value list) (k: Value -> EvalResult) : EvalResult =
    match tryFindHandler eff handlers with
    | Some (frame, clause, outerHandlers) ->
        let upstream =
            { Cached = None
              Eval = fun () -> resolveEffect decls env outerHandlers eff args (fun v -> Done v) }
        let handlerEnv =
            List.zip clause.ArgNames args
            |> List.fold (fun e (n, v) -> Map.add n v e) frame.Env
            |> Map.add clause.KName (VContinuation (fun v -> k v))
            |> Map.add "v" (VLazy upstream)
        evalWithHandlers decls handlerEnv outerHandlers clause.Body (fun v -> Done v)
    | None ->
        match Map.tryFind eff decls with
        | Some { Body = Some body; Params = ps } ->
            let implEnv = bindParams ps args
            evalWithHandlers decls implEnv handlers body k
        | _ -> Request (eff, args, k)

let eval (decls: Map<string, Decl>) (env: Env) (expr: Expr) : EvalResult =
    evalWithHandlers decls env [] expr (fun v -> Done v)

let runProgram (prog: Program) (handleIO: string -> Value list -> Value) : EvalResult =
    let rec loop result =
        match result with
        | Done v -> Done v
        | Error e -> Error e
        | Request (eff, args, k) ->
            let v = handleIO eff args
            loop (k v)
    loop (eval prog.Decls Map.empty prog.Main)
