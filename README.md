# ACE Language

**ACE (Algebraic Call Effects)** is an **experimental language** where **every function call is interpreted as an algebraic effect**.

> In most languages:
> function call = value computation
>
> In ACE:
> **function call = effect performance**

🌐 **[Try it online](https://lee-wonjun.github.io/ACE/)**

---

## One-sentence summary

> **ACE explores the idea of treating function calls as a universal abstraction**,
> unifying interception, dependency injection, middleware, and mocking under a single **handler model**.

---

## Core Idea

### `defn` does *not* define a function

In ACE, `defn` does **not** define a function in the traditional sense.
Instead, at **compile time (or preprocessing time)** it does two things:

```ace
defn double(n) : Number { n * 2 }
```

This single declaration:

1. Declares an **algebraic effect** named `double`
2. Registers `{ n * 2 }` as its **root handler** (default implementation)

So when you write:

```ace
double(10)
```

you are **not calling a function** —
you are **performing the `double` effect**.

At runtime, ACE searches the **handler stack** to decide who handles this effect.

---

## The ACE mental model

| Conventional languages | ACE                 |
| ---------------------- | ------------------- |
| Function call          | Effect performance  |
| Function body          | Root handler        |
| Overriding / DI        | Effect interception |
| Middleware             | Nested handlers     |
| Mocking                | Test handlers       |

---

## Syntax Overview

### 1. Effect definition (`defn`)

```ace
// Explicit effect (interface only)
defn `Print` (msg) : Unit

// Implicit effect (effect + root handler)
defn add(x, y) : Number { x + y }
```

* **Backticked names (`Print`)**

  * Declare an effect only
  * No default implementation
* **Normal names (`add`)**

  * Declare an effect **and** its root handler

---

### 2. Performing effects

There is no `perform` keyword.
**Every call expression performs an effect.**

```ace
`Print`("hello")   // explicit effect
add(1, 2)          // also an effect (handled by the root handler)
```

---

### 3. Handling effects (`handle / with`)

```ace
handle {
    let x = getValue()
    x + 1
} with (getValue) {
    continue k (42)
}
```

* `handle { ... }` — code that may perform effects
* `with (effect)` — intercepts a specific effect
* `continue k` — resumes the suspended computation

---

### 4. The `v` variable — upstream delegation

Inside a handler, `v` represents:

> **“What would have happened if I did not intercept this effect?”**

```ace
defn getValue() : Number { 100 }

handle {
    getValue()
} with (getValue) {
    continue k (v + 1)
}
```

* `v = 100` (from the root handler)
* Final result = `101`

---

### 5. Control-flow summary

| Expression          | Upstream executed? | Meaning                                      |
| ------------------- | ------------------ | -------------------------------------------- |
| `continue k`        | ❌                  | Resume with `Unit`                           |
| `continue k (expr)` | ❌                  | Resume with `expr`                           |
| `continue k v`      | ✅                  | Delegate upstream and resume with its result |

---

## Design Goals (POC-level)

> ⚠️ **ACE is a proof-of-concept language.**
> There is no plan to turn it into a production language.

ACE:

* Is **not** an attempt to implement a mathematically rigorous algebraic effects system
  (like Eff, Koka, or Unison)
* Is **not** primarily about modeling side effects
* Instead, it explores:

  > **Can a handler system serve as a universal abstraction over function calls?**

The design is intentionally experimental and acknowledges many open problems.

---

## Key Ideas

### 1. Effects as a universal abstraction

Because *every call is an effect*, the following patterns collapse into one mechanism:

* AOP (logging, tracing, metrics)
* Dependency Injection
* Test mocking
* Middleware / interceptors

---

### 2. `defn` as compile-time effect registration

```text
defn name(args) { body }
≡
register_effect("name", args)
register_root_handler("name", body)
```

Runtime behavior:

1. Search the handler stack from top to bottom
2. If a handler matches, execute it
3. Otherwise, fall back to the root handler
4. If no root handler exists → unhandled effect error

---

### 3. Static effect inference (idea)

```ace
defn log(msg) : Unit with `Print` {
    `Print`(msg)
}
```

* The `with` clause declares which effects may be triggered
* A compiler could statically verify:

  * All effects are handled before program termination

---

### 4. `sealed` functions

```ace
sealed defn add(x, y) { x + y }
```

* `sealed` effects cannot be intercepted
* Useful for performance- or security-critical code

---

### 5. Backtick-only interception (optional design)

```ace
add(1, 2)     // always calls the root handler
`add`(1, 2)   // interceptable
```

* Normal calls are “pure”
* Backticked calls are effectful

---

### 6. Handler re-entrance prevention

```ace
handle {
    foo()
} with (foo) {
    foo()   // ❌ error: infinite recursion detected
}
```

The runtime detects and prevents handlers from re-invoking the same effect.

---

### 7. Lexically scoped handlers

```ace
handle { ... } with (effect) { ... }
```

* Handlers are **syntactic constructs**
* Not first-class values
* Enables static analysis:

  * The compiler can track where effects are handled

---

## Current Implementation

Built with:

* **F#**
* **Bolero (WebAssembly)**
* **XParsec**

```bash
dotnet build AceLang.sln
dotnet run --project src/AceLang.Client/AceLang.Client.fsproj
dotnet test tests/AceLang.Tests
```

```text
AceLang/
├── src/AceLang.Client/
│   ├── AST.fs           # AST definitions
│   ├── Parser.fs        # XParsec grammar
│   ├── Interpreter.fs  # Request / Done / Error evaluation
│   └── Main.fs         # Bolero Web UI
└── tests/AceLang.Tests/
```

---

## Inspiration

* [Eff](https://www.eff-lang.org/)
* [Koka](https://koka-lang.github.io/)
* [Unison](https://www.unison-lang.org/)
