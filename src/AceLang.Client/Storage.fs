module AceLang.Client.Storage

let example1 = """
defn main() {
    IO.print("Hello Ace!")
    IO.print("Effect System working.")
}
"""

let example2 = """
defn get_data() : Number { 100 }

defn main() {
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

let examples = [
    ("Basic IO", example1)
    ("Handler Mock", example2)
    ("Upstream v", example3)
    ("Deep Stack", example4)
    ("Handle In Defn", example5)
]
