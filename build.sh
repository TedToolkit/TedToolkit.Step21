#!/usr/bin/env sh

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

exec dotnet run \
    --project "$script_dir/build/TedToolkit.Step21.Build/TedToolkit.Step21.Build.csproj" \
    --configuration Release
