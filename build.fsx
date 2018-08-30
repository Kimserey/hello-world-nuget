#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

module GitVersion =
    let exec args =
        Process.execWithResult
            (fun info -> { info with FileName = "gitversion"; Arguments = args })
            (System.TimeSpan.FromMinutes 2.)

module Env =
    let configuration =
        DotNet.BuildConfiguration.fromEnvironVarOrDefault "BuildConfiguration" DotNet.BuildConfiguration.Debug

    let isAppVeyor =
        Environment.environVar "BuildEnvironment" = "appveyor"

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    |> Shell.cleanDirs
)

Target.create "UpdateAppVeyorBuildVersion" (fun _ ->
    let fullSemVer =
        (GitVersion.exec "/showvariable FullSemVer").Messages |> List.head

    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" fullSemVer)
    |> ignore
)

Target.create "Build" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = Env.configuration }))
)

Target.create "All" ignore

"Clean"
  =?> ("UpdateAppVeyorBuildVersion", Env.isAppVeyor)
  ==> "Build"
  ==> "All"

Target.runOrDefault "Build"