module AceLang.Client.AST

type Literal =
    | LInt of int
    | LString of string
    | LBool of bool
    | LUnit

type ContinueArg =
    | CA_Val of Expr
    | CA_Upstream

and Expr =
    | ELiteral of Literal
    | EVar of string
    | ELet of string * Expr * Expr
    | ECall of string * Expr list
    | EHandle of Expr * HandlerClause list
    | EContinue of string * ContinueArg
    | EIf of Expr * Expr * Expr
    | EBlock of Expr list
    | EBinOp of Expr * string * Expr

and HandlerClause = { EffectName: string; ArgNames: string list; KName: string; Body: Expr }

type Decl = { Name: string; Params: string list; Body: Expr option }

type Program = { Decls: Map<string, Decl>; Main: Expr }
