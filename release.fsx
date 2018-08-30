#load ".fake/release.fsx/intellisense.fsx"

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
    ++ "artifacts"
    |> Shell.cleanDirs
)

Target.create "PatchAssemblyInfo" (fun _ ->
    GitVersion.exec "/updateassemblyinfo"
    |> ignore
)

Target.create "UpdateAppVeyorBuildVersion" (fun _ ->
    let fullSemVer =
        (GitVersion.exec "/showvariable FullSemVer").Messages |> List.head

    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" fullSemVer)
    |> ignore
)

Target.create "DotNetBuild" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = Env.configuration }))
)

Target.create "Pack" (fun _ ->
    let nugetVer  =
        (GitVersion.exec "/showvariable NuGetVersionV2").Messages |> List.head

    DotNet.pack
        (fun opts ->
            { opts with
                Configuration = Env.configuration
                OutputPath = Some "../artifacts/Groomgy.HelloWorld"
                NoBuild = true
                Common = { opts.Common with CustomParams = Some <| sprintf "/p:PackageVersion=%s" nugetVer }
            })
        "./Groomgy.HelloWorld"
)

Target.create "All" ignore

"Clean"
  =?> ("PatchAssemblyInfo", Env.isAppVeyor)
  =?> ("UpdateAppVeyorBuildVersion", Env.isAppVeyor)
  ==> "DotNetBuild"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "All"