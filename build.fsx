#load ".fake/build.fsx/intellisense.fsx"
#nowarn "3180"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

module Environment =

    let [<Literal>] BUILD_CONFIGURATION = "BuildConfiguration"
    let [<Literal>] REPOSITORY = "https://github.com/Kimserey/hello-world-nuget.git"

    module AppVeyor =
        let [<Literal>] APPVEYOR = "APPVEYOR"
        let [<Literal>] APPVEYOR_BUILD_VERSION = "APPVEYOR_BUILD_VERSION"
        let [<Literal>] APPVEYOR_REPO_COMMIT = "APPVEYOR_REPO_COMMIT"
        let [<Literal>] APPVEYOR_REPO_TAG_NAME = "APPVEYOR_REPO_TAG_NAME"

module Process =
    let private timeout =
        System.TimeSpan.FromMinutes 2.

    let execWithMultiResult f =
        Process.execWithResult f timeout
        |> fun r -> r.Messages

    let execWithSingleResult f =
        let result = execWithMultiResult f

        match result with
        | [ value ] -> value
        | messages -> failwith <| sprintf "Expected single result but received multiple responses. %s" (messages |> String.concat "\n")

module GitRelease =
    open System.Text.RegularExpressions

    let [<Literal>] REGEX_STABLE_RELEASE_TAG = "^([0-9]+)\.([0-9]+)\.([0-9]+)$"

    let isStableRelease tag = Regex.Match(tag, REGEX_STABLE_RELEASE_TAG).Success

    let getPreviousRelease currentTag =
        // The Git command followed by the filtering assumes that only one type of tag is used, either lightweight tags or annotated tags.
        // A combination of both will fail as the for-each-ref categorize first commit/tag so the versions will grouped by commit/tag before being sorted by committer date.
        Process.execWithMultiResult (fun info ->
            { info with
                FileName = "git"
                Arguments = "for-each-ref refs/tags/ --sort=-committerdate --format=\"%(refname:short)\"" })
        |> Seq.map (fun t -> printfn "%s" t; t)
        |> Seq.skipWhile (fun tag -> currentTag <> tag)
        |> Seq.filter (fun tag ->  if (isStableRelease currentTag) then isStableRelease tag else true)
        |> Seq.tryItem 1

module GitVersion =
    type Repository = string
    type Branch = string
    type Commit = string

    let private showVariable variable (repository: Repository) (branchBuild: Branch) (commit: Commit) =
        Process.execWithSingleResult (fun info ->
            { info with
                FileName = "gitversion"
                Arguments = sprintf "/showvariable %s /url %s /dynamicRepoLocation .\gitversion /b %s /c %s" variable repository branchBuild commit })

    let fullSemVer = showVariable "FullSemVer"
    let assemblyVer = showVariable "AssemblySemVer"
    let nugetVersion = showVariable "NuGetVersionV2"

let buildBranch () =
    Environment.environVarOrNone Environment.AppVeyor.APPVEYOR_REPO_TAG_NAME
    |> Option.map GitRelease.isStableRelease
    |> Option.map (fun isStable -> if isStable then "build" else "alpha")
    |> Option.defaultValue "alpha"

let commit () =
    match Environment.environVarOrNone Environment.AppVeyor.APPVEYOR_REPO_COMMIT with
    | Some c -> c
    | None -> Process.execWithSingleResult (fun info -> { info with FileName = "git"; Arguments = "rev-parse HEAD" })

let fullSemVer () =
    let (branch, commit) = buildBranch(), commit()
    GitVersion.fullSemVer Environment.REPOSITORY branch commit

let assemblyVer () =
    let (branch, commit) = buildBranch(), commit()
    GitVersion.assemblyVer Environment.REPOSITORY branch commit

let nugetVersion () =
    let (branch, commit) = buildBranch(), commit()
    GitVersion.nugetVersion Environment.REPOSITORY branch commit

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "**/artifacts"
    ++ "gitversion"
    |> Shell.deleteDirs
)

Target.create "PrintVersion" (fun _ ->
    printfn "Full sementic version: '%s'`" (fullSemVer ())
    printfn "Assembyly version: '%s'" (assemblyVer ())
    printfn "NuGet sementic version: '%s'" (nugetVersion ())
)

Target.create "AppVeyor_UpdateBuildVersion" (fun _ ->
    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s (%s)\"" (fullSemVer ()) (Environment.environVar Environment.AppVeyor.APPVEYOR_BUILD_VERSION))
    |> ignore
)

Target.create "AppVeyor_GatherReleaseNotes" (fun _ ->
    let releaseNotes =
        match Environment.environVarOrNone Environment.AppVeyor.APPVEYOR_REPO_TAG_NAME with
        | Some tag ->
            match GitRelease.getPreviousRelease tag with
            | Some previousTag ->
                printfn "AppVeyor_GatherReleaseNotes: Gathering release notes..."
                let notes =
                    Process.execWithMultiResult (fun info ->
                        { info with FileName = "git"; Arguments = sprintf "log --pretty=format:\"%%h %%s\" %s..%s" previousTag tag })
                    |> String.concat(System.Environment.NewLine)
                printfn "AppVeyor_GatherReleaseNotes: Release notes found\n%s" notes
                notes
            | None ->
                printfn "AppVeyor_GatherReleaseNotes: Previous Release to tag '%s' could not be found. Skipping GatherReleaseNotes." tag
                "None"
        | None ->
            printfn "AppVeyor_GatherReleaseNotes: No tag found for this build. Skipping GatherReleaseNotes."
            "None"

    Shell.Exec("appveyor", sprintf "SetVariable -Name release_notes -Value \"%s\"" releaseNotes)
    |> ignore
)

Target.create "Build" (fun _ ->
    let setParams (buildOptions: DotNet.BuildOptions) =
        { buildOptions with
            Common = { buildOptions.Common with DotNet.CustomParams = Some (sprintf "/p:Version=%s /p:FileVersion=%s" (fullSemVer ()) (assemblyVer ())) }
            Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug }

    !! "**/*.*proj"
    -- "**/Groomgy.*Test.*proj"
    -- "**/gitversion/**/*.*proj"
    |> Seq.iter (DotNet.build setParams)
)

Target.create "Pack" (fun _ ->
    let setParams (packOptions: DotNet.PackOptions) =
        { packOptions with
            Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug
            OutputPath = Some "../artifacts"
            NoBuild = true
            Common = { packOptions.Common with CustomParams = Some (sprintf "/p:PackageVersion=%s" (nugetVersion ())) } }

    !! "**/*.*proj"
    -- "**/Groomgy.*Test.*proj"
    -- "**/gitversion/**/*.*proj"
    |> Seq.iter (DotNet.pack setParams)
)

Target.create "All" ignore

"Clean"
  =?> ("AppVeyor_UpdateBuildVersion", Environment.environVarAsBool Environment.AppVeyor.APPVEYOR)
  =?> ("AppVeyor_GatherReleaseNotes", not <| String.isNullOrWhiteSpace(Environment.environVar Environment.AppVeyor.APPVEYOR_REPO_TAG_NAME))
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"