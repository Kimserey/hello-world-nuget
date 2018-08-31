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

module Environment =
    let [<Literal>] appveyorRepoTagName = "appveyor_repo_tag_name"

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
    let version =
        match Environment.environVarOrNone Environment.appveyorRepoTagName with
        | Some v -> v
        | None   -> (GitVersion.exec "/showvariable FullSemVer").Messages |> List.head

    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" version)
    |> ignore
)

Target.create "Build" (fun _ ->
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun opts -> { opts with Configuration = Environment.configuration }))
)

Target.create "Pack" (fun _ ->
    let version =
        match Environment.environVarOrNone Environment.appveyorRepoTagName with
        | Some v -> v
        | None   -> (GitVersion.exec "/showvariable NuGetVersionV2").Messages |> List.head

    DotNet.pack
        (fun opts ->
            { opts with
                Configuration = Environment.configuration
                OutputPath = Some "../artifacts/Groomgy.HelloWorld"
                NoBuild = true
                Common = { opts.Common with CustomParams = Some (sprintf "/p:PackageVersion=%s" version) }
            })
        "./Groomgy.HelloWorld"
)

Target.create "All" ignore

"Clean"
  =?> ("PatchAssemblyInfo", Environment.isAppVeyor)
  =?> ("UpdateAppVeyorBuildVersion", Environment.isAppVeyor)
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"