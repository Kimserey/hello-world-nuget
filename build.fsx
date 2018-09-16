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
        let [<Literal>] APPVEYOR_BUILD_NUMBER = "APPVEYOR_BUILD_NUMBER"
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

    let [<Literal>] private REGEX_STABLE_RELEASE_TAG = "^([0-9]+)\.([0-9]+)\.([0-9]+)$"

    let private isStableRelease tag = Regex.Match(tag, REGEX_STABLE_RELEASE_TAG).Success

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
    let private showVariable repository variable commit =
        Process.execWithSingleResult (fun info ->
            { info with
                FileName = "gitversion"
                Arguments = sprintf "/showvariable %s /url %s /b build /dynamicRepoLocation .\gitversion /c %s" variable repository commit })

    let fullSemVer = showVariable Environment.REPOSITORY  "FullSemVer"
    let assemblyVer = showVariable Environment.REPOSITORY  "AssemblySemVer"
    let nugetVersion = showVariable Environment.REPOSITORY  "NuGetVersionV2"

let commit =
    match Environment.environVarOrNone Environment.AppVeyor.APPVEYOR_REPO_COMMIT with
    | Some c -> c
    | None -> Process.execWithSingleResult (fun info -> { info with FileName = "git"; Arguments = "rev-parse HEAD" })

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "**/artifacts"
    ++ "gitversion"
    |> Shell.deleteDirs
)

Target.create "PrintVersion" (fun _ ->
    printfn "Full sementic version: '%s'`" (GitVersion.fullSemVer commit)
    printfn "Assembyly version: '%s'" (GitVersion.assemblyVer commit)
    printfn "NuGet sementic version: '%s'" (GitVersion.nugetVersion commit)
)

Target.create "AppVeyor_UpdateBuildVersion" (fun _ ->
    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s (%s)\"" (GitVersion.fullSemVer commit) (Environment.environVar Environment.AppVeyor.APPVEYOR_BUILD_NUMBER))
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
                printfn "AppVeyor_GatherReleaseNotes: Release notes found\n\n%s" notes
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
            Common = { buildOptions.Common with DotNet.CustomParams = Some (sprintf "/p:Version=%s /p:FileVersion=%s" (GitVersion.fullSemVer commit) (GitVersion.assemblyVer commit)) }
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
            Common = { packOptions.Common with CustomParams = Some (sprintf "/p:PackageVersion=%s" (GitVersion.nugetVersion commit)) } }

    !! "**/*.*proj"
    -- "**/Groomgy.*Test.*proj"
    -- "**/gitversion/**/*.*proj"
    |> Seq.iter (DotNet.pack setParams)
)

Target.create "All" ignore

"Clean"
  ==> "PrintVersion"
  =?> ("AppVeyor_UpdateBuildVersion", Environment.environVarAsBool Environment.AppVeyor.APPVEYOR)
  =?> ("AppVeyor_GatherReleaseNotes", not <| String.isNullOrWhiteSpace(Environment.environVar Environment.AppVeyor.APPVEYOR_REPO_TAG_NAME))
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"