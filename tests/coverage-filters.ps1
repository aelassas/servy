# Single source of truth for ReportGenerator version and coverage exclusion filters
@{
    Version    = '5.5.10'
    Assemblies = '-*.UnitTests;-*.IntegrationTests;-Servy.Testing;-Servy.Restarter.Net48;-Dapper;-Moq;-xunit*;-Castle*;-CommandLine;-System*;-Microsoft*'
    Files      = '-**/*.xaml;-**/*.xaml.cs;-**/*.g.cs;-**/*.Designer.cs;-**/obj/**/*'
}
