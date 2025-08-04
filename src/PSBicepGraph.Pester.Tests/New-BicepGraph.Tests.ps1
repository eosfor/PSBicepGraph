# Requires -Module Pester
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$modulePath = Join-Path $here '..\PSBicepGraph.psd1'
Import-Module $modulePath -Force

Describe "New-BicepGraphCmdlet" {

    Context "When given a valid Bicep file" {

        $tempFile = New-TemporaryFile
        Set-Content $tempFile @'
param foo string
param bar string = '${foo}'
'@

        It "Parses the file and returns a graph with correct dependencies" {
            $graph = New-BicepGraph -Path $tempFile.FullName

            $graph | Should -Not -BeNullOrEmpty
            $graph.Vertices.Name | Should -Contain 'foo'
            $graph.Vertices.Name | Should -Contain 'bar'

            $barNode = $graph.Vertices | Where-Object Name -eq 'bar'
            $edges = $graph.Edges | Where-Object Source -eq $barNode

            $edges.Target.Name | Should -Contain 'foo'
        }

        AfterAll {
            Remove-Item $tempFile -Force
        }
    }

    Context "When given a non-existent file path" {
        It "Throws a terminating error" {
            { New-BicepGraph -Path 'nonexistent.bicep' } | Should -Throw
        }
    }
}