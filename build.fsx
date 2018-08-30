#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

module Environment =
    let configuration =
        DotNet.BuildConfiguration.fromEnvironVarOrDefault "BuildConfiguration" DotNet.BuildConfiguration.Debug

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    |> Shell.cleanDirs
)

Target.create "DotNetBuild" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = Environment.configuration }))
)

Target.create "Pack" (fun _ ->
    DotNet.pack
        (fun opts ->
            { opts with
                Configuration = Environment.configuration
                OutputPath = Some "../artifacts/Groomgy.HelloWorld"
                NoBuild = true
                Common = { opts.Common with CustomParams = Some <| sprintf "/p:PackageVersion=%s" (GitVersion.nugetVer()) }
            })
        "./Groomgy.HelloWorld"
)

Target.create "All" ignore

"Clean"
  ==> "DotNetBuild"
  ==> "All"

Target.runOrDefault "All"