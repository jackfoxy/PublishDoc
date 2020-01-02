
module Build.Tasks

open System.IO

open BlackFox.Fake

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
//open Fake.IO.Globbing.Operators

// Information about the project is used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docsrc/tools/generate.fsx"

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "PublishDoc"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Stripped-down PublishDoc"

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "Stripped-down PublishDoc"

// List of author names (for NuGet package)
let authors = "Jack Fox"

// Tags for your project (for NuGet package)
let tags = "F# fsharp  parse parsing interpretter"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "jackfoxy"
let gitHome = sprintf "%s/%s" "https://github.com" gitOwner

// The name of the project on GitHub
let gitName = project

let website = sprintf "/%s" project

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let releaseNotes = ReleaseNotes.load "RELEASE_NOTES.md"

// --------------------------------------------------------------------------------------
// Generate the documentation

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "..\\..\\bin"
let content    = __SOURCE_DIRECTORY__ @@ "..\\..\\docsrc\\content"
let output     = __SOURCE_DIRECTORY__ @@ "..\\..\\docs"
let files      = __SOURCE_DIRECTORY__ @@ "..\\..\\docsrc\\files"
let templates  = __SOURCE_DIRECTORY__ @@ "..\\..\\docsrc\\tools\\templates"
let formatting = __SOURCE_DIRECTORY__ @@ "..\\..\\packages\\formatting\\FSharp.Formatting"
let docTemplate = "docpage.cshtml"

let github_release_user = Environment.environVarOrDefault "github_release_user" gitOwner
let githubLink = sprintf "https://github.com/%s/%s" github_release_user gitName

let info =
    [ 
        "project-name", project
        "project-author", authors
        "project-summary", summary
        "project-github", githubLink
        "project-nuget", sprintf "http://nuget.org/packages%s" website 
    ]

let root = website

let referenceBinaries = []

let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[   templates; 
                            formatting @@ "templates"
                            formatting @@ "templates/reference" ])

let copyFiles () =
    Shell.copyRecursive files output true 
    |> Trace.logItems "Copying file: "
    Directory.ensure (output @@ "content")
    Shell.copyRecursive (formatting @@ "styles") (output @@ "content") true 
    |> Trace.logItems "Copying styles and scripts: "
    
let replace (t : string) r (lines:seq<string>) =
    seq {
        for s in lines do
            if s.Contains(t) then 
                yield s.Replace(t, r)
            else yield s }

let postProcessDocs () =
    let dirInfo = DirectoryInfo.ofPath output

    let filePath = System.IO.Path.Combine(dirInfo.FullName, "operationalSemantics.html")
    let newContent =
        File.ReadAllLines filePath
        |> Array.toSeq
        |> replace "t1X2B62Xt1" "t<sub>1</sub> → t<sub>1</sub>"
        |> replace "t1Xt2X2B62Xt1xXt2" "t<sub>1</sub> t<sub>2</sub> → t<sub>1</sub>&#39; t<sub>2</sub>"
        |> replace "t2X2B62Xt2" "t<sub>2</sub> → t<sub>2</sub>"
        |> replace "v1Xt2X2B62Xv1Xt2x" "v<sub>1</sub> t<sub>2</sub> → v<sub>1</sub> t<sub>2</sub>&#39;"
        |> replace "Yt12Y" "t<sub>12</sub>"
        |> replace "Xv2X2B62" " v<sub>2</sub> →"
        |> replace "Yv2Y" "v<sub>2</sub>"
        |> replace @"22A6" "⊢"
        |> replace @"21B6" "↦"
    File.WriteAllLines(filePath, newContent)

    let filePath = System.IO.Path.Combine(dirInfo.FullName, "index.html")
    let newContent =
        File.ReadAllLines filePath
        |> Array.toSeq
        |> replace "<h2>global Namespace</h2>" ""
    File.WriteAllLines(filePath, newContent)

let createAndGetDefault () =
    let cleanDocs = BuildTask.create "CleanDocs" [] {
        Shell.cleanDirs ["docs/reference"; "docs"]
        File.delete "docsrc/content/release-notes.md"
        File.delete "docsrc/content/license.md"
        }

    let docs = BuildTask.create "Docs" [cleanDocs] {       
        Shell.copyFile "docsrc/content/" "RELEASE_NOTES.md"
        Shell.rename "docsrc/content/release-notes.md" "docsrc/content/RELEASE_NOTES.md"
        
        Shell.copyFile "docsrc/content/" "LICENSE.txt"
        Shell.rename "docsrc/content/license.md" "docsrc/content/LICENSE.txt"

        DirectoryInfo.getSubDirectories (DirectoryInfo.ofPath templates)
        |> Seq.iter (fun d ->
                        let name = d.Name
                        if name.Length = 2 || name.Length = 3 then
                            layoutRootsAll.Add(
                                    name, [ templates @@ name
                                            formatting @@ "templates"
                                            formatting @@ "templates/reference" ]))
        copyFiles ()
    
        for dir in  [ content; ] do
            let langSpecificPath(lang, path:string) =
                path.Split([|'/'; '\\'|], System.StringSplitOptions.RemoveEmptyEntries)
                |> Array.exists(fun i -> i = lang)
            let layoutRoots =
                let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> langSpecificPath(i, dir))
                match key with
                | Some lang -> layoutRootsAll.[lang]
                | None -> layoutRootsAll.["en"] // "en" is the default language

            FSFormatting.createDocs (fun args ->
                { args with
                    Source = content
                    OutputDirectory = output 
                    LayoutRoots = layoutRoots
                    ProjectParameters = ("root", root)::info
                    Template = docTemplate } )

        postProcessDocs()
    }

    BuildTask.createEmpty "All" [  docs ]

let listAvailable() = BuildTask.listAvailable()
