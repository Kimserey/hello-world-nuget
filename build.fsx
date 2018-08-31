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

Target.create "PatchAssemblyInfo" (fun _ ->
    GitVersion.exec "/updateassemblyinfo"
    |> ignore
)

Target.create "UpdateAppVeyorBuildVersion" (fun _ ->
    let args =
        (GitVersion.exec "/showvariable FullSemVer").Messages
        |> List.head
        |> Environment.environVarOrDefault "appveyor_repo_tag_name"
        |>  sprintf "UpdateBuild -Version \"%s\""

    Shell.Exec("appveyor", args)
    |> ignore
)

Target.create "Build" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = Env.configuration }))
)

Target.create "Pack" (fun _ ->
    let nuGetVer  =
        (GitVersion.exec "/showvariable NuGetVersionV2").Messages |> List.head

    printfn "NuGet version: %s" nuGetVer

    DotNet.pack
        (fun opts ->
            { opts with
                Configuration = Env.configuration
                OutputPath = Some "../artifacts/Groomgy.HelloWorld"
                NoBuild = true
                Common = { opts.Common with CustomParams = Some <| sprintf "/p:PackageVersion=%s" nuGetVer }
            })
        "./Groomgy.HelloWorld"
)

Target.create "All" ignore

"Clean"
  =?> ("PatchAssemblyInfo", Env.isAppVeyor)
  =?> ("UpdateAppVeyorBuildVersion", Env.isAppVeyor)
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"