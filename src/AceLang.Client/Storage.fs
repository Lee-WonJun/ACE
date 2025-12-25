module AceLang.Client.Storage

let example1 = """defn IO.print (msg) : Unit

defn main() {
    IO.print("Hello Ace!")
    IO.print("Effect System working.")
}
"""

let example2 = """defn IO.print (msg) : Unit
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

let example3 = """defn IO.print (msg) : Unit
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

let examples = [
    ("Basic IO", example1)
    ("Handler Mock", example2)
    ("Upstream v", example3)
]
