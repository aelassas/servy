@{
    # Script module or binary module file associated with this manifest.
    RootModule        = 'Servy.psm1'

    # Version number of this module.
    ModuleVersion     = '8.4.0'

    # Supported PSEditions
    # CompatiblePSEditions = @('Desktop', 'Core')

    # ID used to uniquely identify this module
    GUID              = 'f8d4e5a1-3b2c-4d7e-9f1a-6c8b5e3d2a1f'

    # Author of this module
    Author            = 'Akram El Assas'

    # Company or vendor of this module
    CompanyName       = 'Akram El Assas'

    # Copyright statement for this module
    Copyright         = '(c) Akram El Assas. All rights reserved.'

    # Description of the functionality provided by this module
    Description       = 'PowerShell module to manage Windows services using the Servy CLI. Provides functions to install, uninstall, start, stop, restart, export and import configurations, and check the status of Windows services. Works with both installed and portable versions of Servy.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '2.0'

    # Name of the PowerShell host required by this module
    # PowerShellHostName = ''

    # Minimum version of the PowerShell host required by this module
    # PowerShellHostVersion = ''

    # Minimum version of Microsoft .NET Framework required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
    # DotNetFrameworkVersion = ''

    # Minimum version of the common language runtime (CLR) required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
    # ClrVersion = ''

    # Processor architecture (None, X86, Amd64) required by this module
    # ProcessorArchitecture = ''

    # Modules that must be imported into the global environment prior to importing this module
    RequiredModules   = @()

    # Assemblies that must be loaded prior to importing this module
    RequiredAssemblies = @()

    # Script files (.ps1) that are run in the caller's environment prior to importing this module.
    ScriptsToProcess  = @()

    # Type files (.ps1xml) to be loaded when importing this module
    TypesToProcess    = @()

    # Format files (.ps1xml) to be loaded when importing this module
    FormatsToProcess  = @()

    # Modules to import as nested modules of the module specified in RootModule/ModuleToProcess
    NestedModules     = @()

    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
    FunctionsToExport = @(
        'Get-ServyVersion',
        'Get-ServyHelp',
        'Install-ServyService',
        'Uninstall-ServyService',
        'Start-ServyService',
        'Stop-ServyService',
        'Restart-ServyService',
        'Get-ServyServiceStatus',
        'Export-ServyServiceConfig',
        'Import-ServyServiceConfig'
    )

    AliasesToExport = @('Show-ServyVersion', 'Show-ServyHelp')

    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
    CmdletsToExport   = @()

    # Variables to export from this module
    VariablesToExport = @()

    # DSC resources to export from this module
    # DscResourcesToExport = @()

    # List of all modules packaged with this module
    # ModuleList = @()

    # List of all files packaged with this module
    # FileList = @()

    # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    PrivateData       = @{

        PSData = @{

            # Tags applied to this module. These help with module discovery in online galleries.
            Tags       = @('Windows', 'Service', 'Management', 'CLI', 'Administration', 'Servy')

            # A URL to the license for this module.
            LicenseUri = 'https://github.com/aelassas/servy/blob/main/LICENSE.txt'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/aelassas/servy'

            # A URL to an icon representing this module.
            # IconUri = ''

            # Prerelease string of this module
            # Prerelease = ''

            # Flag to indicate whether the module requires explicit user acceptance for install/update/save
            # RequireLicenseAcceptance = $false

            # External dependent modules of this module
            # ExternalModuleDependencies = @()

        } # End of PSData hashtable

    } # End of PrivateData hashtable

    # HelpInfo URI of this module
    HelpInfoURI = 'https://github.com/aelassas/servy/wiki/Servy-PowerShell-Module'

    # Default prefix for commands exported from this module. Override the default prefix using Import-Module -Prefix.
    # DefaultCommandPrefix = ''

}
