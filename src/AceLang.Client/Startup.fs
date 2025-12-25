namespace AceLang.Client

open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open System

module Program =
    [<EntryPoint>]
    let Main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        builder.RootComponents.Add<Main.MyApp>("#main")
        builder.Build().RunAsync() |> ignore
        0
