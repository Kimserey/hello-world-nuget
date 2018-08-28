#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

let configuration =
    DotNet.BuildConfiguration.fromEnvironVarOrDefault "BUILD_CONFIGURATION" DotNet.BuildConfiguration.Debug

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "artifacts"
    |> Shell.cleanDirs 
)

Target.create "Build" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = configuration }))
)

Target.create "Pack" (fun _ ->
    DotNet.pack 
        (fun opts -> 
            { opts with 
                Configuration = configuration
                OutputPath = Some "../artifacts/Groomgy.HelloWorld"
                VersionSuffix = Some ""
                NoBuild = true
            })
        "./Groomgy.HelloWorld"
)

Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"