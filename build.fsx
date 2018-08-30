#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

let configuration =
    DotNet.BuildConfiguration.fromEnvironVarOrDefault "BuildConfiguration" DotNet.BuildConfiguration.Debug

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "artifacts"
    |> Shell.cleanDirs
)

Target.create "DotNetBuild" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = configuration }))
)

Target.create "Pack" (fun _ ->
    DotNet.pack
        (fun opts ->
            { opts with
                Configuration = configuration
                OutputPath = Some "../artifacts/Groomgy.HelloWorld"
                NoBuild = true
            })
        "./Groomgy.HelloWorld"
)

Target.create "All" ignore

"Clean"
  ==> "DotNetBuild"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "All"