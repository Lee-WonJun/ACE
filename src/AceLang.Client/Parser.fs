module AceLang.Client.Parser

open FParsec
open AceLang.Client.AST

let comment = pstring "//" >>. skipRestOfLine true
let ws = spaces >>. skipMany (comment >>. spaces)
let keyword s = pstring s .>> ws

let isIdentChar c = System.Char.IsLetterOrDigit c || c = '_' || c = '.'
let isIdentStartChar c = System.Char.IsLetter c || c = '_'

let identifier = many1Satisfy2 isIdentStartChar isIdentChar .>> ws

let backtickIdent = between (pchar '`') (pchar '`') (many1Satisfy (fun c -> c <> '`')) .>> ws

let name = backtickIdent <|> identifier

let pint' = pint32 .>> ws

let pstring_lit = between (pchar '"') (pchar '"') (manyChars (noneOf "\"")) .>> ws

let literal =
    (pstring_lit |>> LString)
    <|> (keyword "true" >>% LBool true)
    <|> (keyword "false" >>% LBool false)
    <|> (keyword "unit" >>% LUnit)
    <|> (pint' |>> LInt)

let expr, exprRef = createParserForwardedToRef<Expr, unit>()

let atom =
    (literal |>> ELiteral)
    <|> (between (keyword "(") (keyword ")") expr)
    <|> (attempt (name .>>. opt (between (keyword "(") (keyword ")") (sepBy expr (keyword ",")))) |>> fun (n, args) ->
        match args with
        | Some a -> ECall(n, a)
        | None -> EVar n)

let mulDiv =
    let op = (keyword "*" >>% "*") <|> (keyword "/" >>% "/")
    chainl1 atom (op |>> fun o l r -> EBinOp(l, o, r))

let addSub =
    let op = (keyword "+" >>% "+") <|> (keyword "-" >>% "-")
    chainl1 mulDiv (op |>> fun o l r -> EBinOp(l, o, r))

let simpleExpr = addSub

let letExpr =
    keyword "let" >>. name .>> keyword "=" .>>. expr
    |>> fun (n, v) -> ELet(n, v, ELiteral LUnit)

let continueExpr =
    let continueArg =
        (keyword "v" >>% CA_Upstream)
        <|> (between (keyword "(") (keyword ")") expr |>> CA_Val)
        <|> (simpleExpr |>> CA_Val)
    keyword "continue" >>. name .>>. opt continueArg
    |>> fun (k, argOpt) ->
        let arg = defaultArg argOpt (CA_Val (ELiteral LUnit))
        EContinue(k, arg)

let ifExpr =
    keyword "if" >>. expr .>> keyword "then" .>>. expr .>> keyword "else" .>>. expr
    |>> fun ((c, t), e) -> EIf(c, t, e)

let handlerClause =
    keyword "(" >>. name .>>. many name .>> keyword ")" .>>. opt (keyword "->" >>. name) .>> keyword "{" .>>. many expr .>> keyword "}"
    |>> fun (((eff, args), kOpt), bodyExprs) ->
        let kName = defaultArg kOpt "k"
        let body = match bodyExprs with [e] -> e | es -> EBlock es
        { EffectName = eff; ArgNames = args; KName = kName; Body = body }

let handleExpr =
    keyword "handle" >>. keyword "{" >>. many expr .>> keyword "}" .>> keyword "with" .>>. many1 handlerClause
    |>> fun (bodyExprs, clauses) -> 
        let body = match bodyExprs with [e] -> e | es -> EBlock es
        EHandle(body, clauses)

let blockExpr =
    between (keyword "{") (keyword "}") (many expr)
    |>> function [e] -> e | es -> EBlock es

do exprRef.Value <-
    attempt handleExpr
    <|> attempt ifExpr
    <|> attempt continueExpr
    <|> attempt letExpr
    <|> attempt blockExpr
    <|> simpleExpr

let typeAnnotation = keyword ":" >>. name |>> ignore
let effectAnnotation = keyword "with" >>. sepBy1 name (keyword ",") |>> ignore

let decl =
    keyword "defn" >>. name .>> keyword "(" .>>. sepBy name (keyword ",") .>> keyword ")" .>>. opt typeAnnotation .>>. opt effectAnnotation .>>. opt blockExpr
    |>> fun ((((n, ps), _), _), body) -> { Name = n; Params = ps; Body = body }

let program =
    ws >>. many decl .>> eof
    |>> fun decls ->
        let declMap = decls |> List.map (fun d -> d.Name, d) |> Map.ofList
        let main = declMap |> Map.tryFind "main" |> Option.bind (fun d -> d.Body) |> Option.defaultValue (ELiteral LUnit)
        { Decls = declMap; Main = main }

let parseAce (input: string): Result<Program, string> =
    match run program input with
    | ParserResult.Success(result, _, _) -> Result.Ok result
    | ParserResult.Failure(errorMsg, _, _) -> Result.Error errorMsg
