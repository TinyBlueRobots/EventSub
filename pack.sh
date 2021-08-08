#!/bin/bash
NUGETVERSION=1.0.0
dotnet pack ./src/EventSub -c Release /p:PackageVersion=$NUGETVERSION
dotnet nuget push ./src/EventSub/bin/Release/EventSub.$NUGETVERSION.nupkg -k "$NUGETKEY" -s nuget.org