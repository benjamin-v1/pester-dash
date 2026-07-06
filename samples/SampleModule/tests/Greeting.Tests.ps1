BeforeAll {
    . $PSScriptRoot/../src/Greeting.ps1
}

Describe 'Get-Greeting' {
    It 'returns a greeting' {
        Get-Greeting -Name 'World' | Should -Be 'Hello, World!'
    }

    It 'is pending implementation' -Skip {
        $false | Should -Be $true
    }
}
