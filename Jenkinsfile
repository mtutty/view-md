// CI for view-md: restore/build/test/package on a single docker-based agent.
//
// Base image: mcr.microsoft.com/dotnet/sdk:10.0-noble-aot — Microsoft's
// official .NET 10 SDK image with the Native AOT prerequisites (clang, llvm,
// zlib1g-dev) already installed. Verified directly against its Dockerfile:
// https://github.com/dotnet/dotnet-docker/blob/main/src/sdk/10.0/noble-aot/amd64/Dockerfile
// This exact image + a real `dotnet publish` for linux-x64/win-x64 was run
// against this repo during development to confirm the pipeline below
// actually works, not just that it looks right on paper.
//
// IMPORTANT — Native AOT cannot cross-compile between operating systems
// (Microsoft Learn, "Cross-compilation - .NET":
// https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/cross-compile).
// A single Linux docker agent can only produce a NativeAOT binary for Linux.
// The Windows and macOS artifacts built here are standard self-contained
// (JIT) publishes cross-compiled from Linux — that part IS supported and
// was also verified directly against this image. They start slightly slower
// than the Linux AOT build and are not exercised by the Test stage, since a
// win-x64/osx-arm64 binary cannot run inside this Linux container. See
// .charter/decisions.md for the full writeup.
pipeline {
    agent {
        docker {
            label 'jenkins-fleet-app'
            image 'mcr.microsoft.com/dotnet/sdk:10.0-noble-aot'
            // Jenkins' own SCM checkout runs inside this container and needs
            // git; packaging needs zip (for the win/mac artifacts) and
            // dpkg-deb (already present on any Debian/Ubuntu base, incl.
            // this one, as part of the core dpkg package).
            args '-u root'
        }
    }

    environment {
        DOTNET_NOLOGO = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
    }

    options {
        timestamps()
        buildDiscarder(logRotator(numToKeepStr: '20'))
    }

    stages {
        stage('Install container prerequisites') {
            steps {
                // git: Jenkins' own SCM checkout needs it inside this container.
                // zip: used by packaging/build-windows.sh and build-macos.sh.
                // libfontconfig1: runtime dependency of SkiaSharp (pulled in by
                // Avalonia.Skia) — the noble-aot image is a minimal SDK image
                // without desktop runtime libs, so the headless render smoke
                // test fails at startup (DllNotFoundException) without this.
                // Found by actually running this stage against the image
                // during development, not assumed from docs.
                sh '''
                    set -euo pipefail
                    apt-get update -qq
                    apt-get install -y -qq --no-install-recommends git zip libfontconfig1
                    rm -rf /var/lib/apt/lists/*
                    git config --global --add safe.directory "$WORKSPACE"
                '''
            }
        }

        stage('Restore') {
            steps {
                sh 'dotnet restore ViewMd.slnx'
            }
        }

        stage('Build') {
            steps {
                sh 'dotnet build ViewMd.slnx -c Release --no-restore'
            }
        }

        stage('Test (headless render smoke check)') {
            steps {
                // No GUI test framework here — tools/SmokeTest drives the real
                // App/MainWindow through Avalonia's headless Skia backend
                // (no X server needed, works fine in a container) and saves a
                // PNG. This is Linux-only: it validates the same rendering
                // code the win-x64/osx-arm64 builds share, even though those
                // binaries can't themselves execute in this container.
                sh '''
                    set -euo pipefail
                    dotnet run --project tools/SmokeTest -c Release --no-build -- \
                        tools/SmokeTest/fixtures/ci-check.md \
                        "$WORKSPACE/ci-smoketest.png"
                    test -s "$WORKSPACE/ci-smoketest.png"
                '''
            }
            post {
                always {
                    archiveArtifacts artifacts: 'ci-smoketest.png', allowEmptyArchive: true
                }
            }
        }

        stage('Package Linux (.deb, NativeAOT)') {
            steps {
                sh './packaging/build-deb.sh'
            }
        }

        stage('Package Windows (win-x64, self-contained)') {
            steps {
                sh './packaging/build-windows.sh'
            }
        }

        stage('Package macOS (osx-arm64, self-contained)') {
            steps {
                sh './packaging/build-macos.sh'
            }
        }
    }

    post {
        success {
            archiveArtifacts artifacts: 'dist/*', fingerprint: true
        }
    }
}
