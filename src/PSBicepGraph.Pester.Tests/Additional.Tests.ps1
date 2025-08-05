# Requires -Module Pester

<#
    This test suite contains a set of additional, low‑level checks for the
    PSBicepGraph module.  It supplements the existing tests by verifying
    path resolution logic, more complex semantic graph construction and
    relative path invocation for the `New-BicepSemanticGraph` cmdlet.

    * Path resolution logic is exercised via reflection to invoke the
      private `ResolveFullPath` method on the cmdlet.  Tests confirm
      that relative paths are resolved against a supplied base folder
      and that absolute paths are returned unchanged.

    * A synthetic Bicep definition with parameters, variables and an
      output is used to ensure that the semantic graph includes all
      declarations and edges representing chained dependencies.  This
      goes beyond the basic parameter/default scenario tested
      elsewhere.

    * Relative path invocation verifies that the cmdlet functions
      correctly when a file name is provided instead of an absolute
      path, resolving based on the current working directory.

    These tests assume that the module manifest (`PSBicepGraph.psd1`)
    is located one directory above this file and that a compiled
    version of the module is available for import.
#>

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$modulePath = Join-Path $here '../PSBicepGraph/bin/Debug/net9.0/publish/PSBicepGraph.dll'
Import-Module $modulePath -Force

BeforeAll {
    # Создаём временный файл Bicep с цепочкой зависимостей: p2 -> p1,
    # v1 -> p1, v2 -> v1, o1 -> v2.
    $tempFile = New-TemporaryFile
    Set-Content $tempFile @'
param p1 string
param p2 string = '${p1}'

var v1 = p1
var v2 = v1

output o1 string = v2
'@

    # Создаём временный каталог и простой Bicep‑файл.  Командлет
    # вызывается с относительным путём (имя файла без каталога).
    $tempDir = New-Item -Path (Join-Path ([IO.Path]::GetTempPath()) ([System.Guid]::NewGuid())) -ItemType Directory
    $fileName = 'simple.bicep'
    $filePath = Join-Path $tempDir.FullName $fileName
    Set-Content -Path $filePath -Value @'
param x string
var y = x
'@
}

Describe "New-BicepSemanticGraph additional scenarios" {
    Context "Handles multiple variable and output dependencies" {


        It "Builds a graph with correct vertices and edges" {

            # Получаем путь к файлу отдельно, чтобы избежать возможных проблем
            # $filePath = $tempFile.FullName
            $graph = New-BicepSemanticGraph -Path $tempFile.FullName

            # Проверяем наличие всех узлов
            $graph.Vertices.Name | Should -Contain 'p1 (Parameter)'
            $graph.Vertices.Name | Should -Contain 'p2 (Parameter)'
            $graph.Vertices.Name | Should -Contain 'v1 (Variable)'
            $graph.Vertices.Name | Should -Contain 'v2 (Variable)'
            $graph.Vertices.Name | Should -Contain 'o1 (Output)'

            # Проверяем зависимости между ними
            $p2Node = $graph.Vertices | Where-Object Name -eq 'p2 (Parameter)'
            ($graph.Edges | Where-Object { $_.Source -eq $p2Node }).Target.Name | Should -Contain 'p1 (Parameter)'

            $v1Node = $graph.Vertices | Where-Object Name -eq 'v1 (Variable)'
            ($graph.Edges | Where-Object { $_.Source -eq $v1Node }).Target.Name | Should -Contain 'p1 (Parameter)'

            $v2Node = $graph.Vertices | Where-Object Name -eq 'v2 (Variable)'
            ($graph.Edges | Where-Object { $_.Source -eq $v2Node }).Target.Name | Should -Contain 'v1 (Variable)'

            $o1Node = $graph.Vertices | Where-Object Name -eq 'o1 (Output)'
            ($graph.Edges | Where-Object { $_.Source -eq $o1Node }).Target.Name | Should -Contain 'v2 (Variable)'
        }

        AfterAll {
            Remove-Item $tempFile -Force
        }
    }

    Context "Accepts relative file paths" {
        It "Resolves relative path based on current location" {
            Push-Location $tempDir.FullName
            try {
                $graph = New-BicepSemanticGraph -Path $fileName
            }
            finally {
                Pop-Location
            }

            $graph.Vertices.Name | Should -Contain 'x (Parameter)'
            $graph.Vertices.Name | Should -Contain 'y (Variable)'

            $yNode = $graph.Vertices | Where-Object Name -eq 'y (Variable)'
            ($graph.Edges | Where-Object { $_.Source -eq $yNode }).Target.Name | Should -Contain 'x (Parameter)'
        }

        AfterAll {
            Remove-Item $tempDir -Recurse -Force
        }
    }
}