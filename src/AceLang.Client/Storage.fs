module AceLang.Client.Storage

let example1 = """
defn main() {
    IO.print("Hello Ace!")
    // Toggle this line with Ctrl+/ to run it.
    // IO.print("This line is commented out.")
    IO.print("Effect System working.")
}
"""

let example2 = """
defn get_data() : Number { 100 }

defn main() {
    // Without handle: default root returns 100.
    // let x = get_data()
    // IO.print("Data is: " + x)

    handle {
        let x = get_data()
        IO.print("Data is: " + x)
    } with (get_data) -> k {
        continue k (999)
    }
}
"""

let example3 = """
defn value() : Number { 10 }

defn main() {
    // Without handlers: value() returns 10 directly.
    // let x = value()
    // IO.print("Result: " + x)

    handle {
        handle {
            let x = value()
            IO.print("Result: " + x)
        } with (value) -> k {
            continue k (v + 5)
        }
    } with (value) -> k {
        continue k (v + 20)
    }
}
"""

let example4 = """
defn fetch_base() : Number { 7 }
defn adjust(x) : Number { x + 2 }
defn compute() : Number {
    let a = fetch_base()
    let b = adjust(a)
    b * 3
}
defn pipeline() : Number {
    let v1 = compute()
    IO.print("Pipeline: " + v1)
    v1
}

defn main() {
    // Without handlers: fetch_base() root -> 7, compute -> 27, pipeline prints 27.
    // pipeline()

    handle {
        handle {
            pipeline()
        } with (fetch_base) {
            continue k (v + 5)
        }
    } with (fetch_base) {
        continue k (v + 20)
    }
}
"""

let example5 = """
defn get_data() : Number { 10 }
defn calc() : Number { get_data() * 2 }
defn wrapped_calc() : Number {
    // Without handle: calc() uses root get_data() -> 10, result 20.
    // calc()

    handle {
        calc()
    } with (get_data) {
        continue k (v + 1)
    }
}

defn main() {
    let result = wrapped_calc()
    IO.print("Wrapped result: " + result)
}
"""

let example6 = """
defn get_flag() : Bool { true }

defn main() {
    // Without handle: get_flag() -> true, result 1.
    // let result = if get_flag() then 1 else 2
    // IO.print("If result: " + result)

    let result = handle {
        if get_flag() then 1 else 2
    } with (get_flag) {
        continue k (false)
    }
    IO.print("If result: " + result)
}
"""

let examples = [
    ("Basic IO", example1)
    ("Handler Mock", example2)
    ("Upstream v", example3)
    ("Deep Stack", example4)
    ("Handle In Defn", example5)
    ("If + Handle", example6)
]
