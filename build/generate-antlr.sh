#!/usr/bin/env sh
set -eu

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repository_root=$(CDPATH= cd -- "$script_directory/.." && pwd)

if ! command -v dotnet >/dev/null 2>&1; then
    echo '.NET SDK is required to read the centrally managed ANTLR version.' >&2
    exit 1
fi

tools_directory="$script_directory/tools"
version_output_file="$tools_directory/antlr-version.txt"
mkdir -p "$tools_directory"

dotnet msbuild "$script_directory/GetPackageVersion.proj" \
    -nologo \
    -verbosity:quiet \
    -target:GetPackageVersion \
    -property:PackageId=Antlr4.Runtime.Standard \
    "-property:VersionOutputFile=$version_output_file"

antlr_version=''
IFS= read -r antlr_version < "$version_output_file" || true
if [ -z "$antlr_version" ]; then
    echo 'Antlr4.Runtime.Standard has an empty centrally managed version.' >&2
    exit 1
fi

antlr_jar="$tools_directory/antlr-$antlr_version-complete.jar"

if ! command -v java >/dev/null 2>&1; then
    echo 'Java 11 or newer is required to run ANTLR.' >&2
    exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
    echo 'curl is required to download ANTLR.' >&2
    exit 1
fi

if [ ! -f "$antlr_jar" ]; then
    curl --fail --location --output "$antlr_jar" \
        "https://www.antlr.org/download/antlr-$antlr_version-complete.jar"
fi

step_output="$repository_root/src/TedToolkit.Step21/Generated/STEP"
express_output="$repository_root/src/TedToolkit.Step21.Analyzer/Generated/Express"

rm -rf -- "$step_output" "$express_output"
mkdir -p "$step_output" "$express_output"

cd "$repository_root/src/grammar"
java -jar "$antlr_jar" -Dlanguage=CSharp -visitor -no-listener \
    -package TedToolkit.Step21.Grammar -o "$step_output" STEP.g4
java -jar "$antlr_jar" -Dlanguage=CSharp -visitor -no-listener \
    -package TedToolkit.Step21.Analyzer.Grammar -o "$express_output" Express.g4
